using System.Collections.Generic;
using System.Linq;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static SameShore.EditorTools.BuilderUtil;

namespace SameShore.EditorTools
{
    /// <summary>
    /// Builds Assets/SameShore/Scenes/SameShore.unity from the team's SampleScene (which is left
    /// untouched). Idempotent: run it again after changing SampleScene or this script.
    /// Menu: Same Shore / Build Scene. Batch: -executeMethod SameShore.EditorTools.SameShoreSceneBuilder.BuildBatch
    /// </summary>
    public static partial class SameShoreSceneBuilder
    {
        const string SourceScene = "Assets/Scenes/SampleScene.unity";
        const string TargetScene = Root + "/Scenes/SameShore.unity";
        const string Art = Root + "/Art";
        const string PH = Art + "/ThirdParty/PolyHaven";
        const float WaterLevel = 0.31f;

        // layout (computed)
        static float cx = 4f, zs;
        static Terrain[] _terrains;
        static Transform _env;
        static readonly Dictionary<string, Material> M = new Dictionary<string, Material>();
        static readonly System.Random Rng = new System.Random(12);

        [MenuItem("Same Shore/Build Scene")]
        public static void Build()
        {
            M.Clear();
            _meshes.Clear();
            ConfigureImporters();
            CreateMaterials();
            CreateStoryMaterials();
            SameShoreAssets assets = CreateAssetsObject();

            EnsureFolder(Root + "/Scenes");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScene) != null)
            {
                // overwrite the content but keep the .meta, so the scene GUID (and every reference to it) is stable
                System.IO.File.Copy(SourceScene, TargetScene, true);
                AssetDatabase.ImportAsset(TargetScene, ImportAssetOptions.ForceUpdate);
            }
            else AssetDatabase.CopyAsset(SourceScene, TargetScene);
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);

            _terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            zs = FindShoreZ(cx);
            Debug.Log($"[SameShore] shoreline z = {zs:F2} at x = {cx}");
            SculptCorridor();   // the dune berms that frame the hatchling's way (before anything is placed on the sand)

            _env = new GameObject("SameShore_Environment").transform;
            var systems = new GameObject("SameShore_Systems").transform;

            SetupRenderingAndSky();
            PrepareExistingObjects();
            Transform nest = BuildNest();
            BuildSea();
            var props = PlaceProps();
            BuildDecor();
            BuildCorridor();
            var footprints = BuildFootprints(systems);
            var gulls = BuildGulls();
            BuildHotel();
            var audio = BuildAudio(systems);
            var story = BuildStory(systems, nest, props);
            WireSystems(systems, nest, props, footprints, gulls, audio, story);

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[SameShore] Scene built: " + TargetScene);
        }

        public static void BuildBatch()
        {
            try
            {
                Build();
                SameShoreValidator.Validate();
                string shots = SameShoreInspector.Arg("-shots", "");
                if (!string.IsNullOrEmpty(shots)) SameShorePreview.Shots(shots);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[SameShore] BUILD FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        // ================================================================== terrain helpers
        public static float Ground(float x, float z)
        {
            foreach (Terrain t in _terrains)
            {
                Vector3 p = t.transform.position, s = t.terrainData.size;
                if (x >= p.x && z >= p.z && x <= p.x + s.x && z <= p.z + s.z)
                    return t.SampleHeight(new Vector3(x, 0, z)) + p.y;
            }
            return 0f;
        }

        static Vector3 G(float x, float z, float lift = 0f) => new Vector3(x, Ground(x, z) + lift, z);
        static Vector3 L(float dx, float d, float lift = 0f) => G(cx + dx, zs - d, lift); // d = metres up the beach from the shore

        static Vector3 Normal(float x, float z)
        {
            float e = 0.2f;
            Vector3 a = G(x - e, z), b = G(x + e, z), c = G(x, z - e), d = G(x, z + e);
            return Vector3.Cross(d - c, b - a).normalized;
        }

        static float FindShoreZ(float x)
        {
            float prev = -10f;
            for (float z = -10f; z < 40f; z += 0.05f)
            {
                if (Ground(x, z) <= WaterLevel) return Mathf.Lerp(prev, z, 0.5f);
                prev = z;
            }
            return 21f;
        }

        static Quaternion OnGround(Vector3 p, float yaw)
        {
            Vector3 n = Normal(p.x, p.z);
            return Quaternion.FromToRotation(Vector3.up, n) * Quaternion.Euler(0, yaw, 0);
        }

        // ================================================================== import & materials
        static void ConfigureImporters()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { PH, Art + "/Textures" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                bool normal = p.Contains("_nor_");
                bool linear = p.Contains("_ms_") || p.Contains("_alpha_");
                ConfigureTexture(p, normal, !linear, 1024);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Art }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var imp = (ModelImporter)AssetImporter.GetAtPath(p);
                bool changed = false;
                if (imp.importAnimation) { imp.importAnimation = false; changed = true; }
                if (imp.importCameras || imp.importLights) { imp.importCameras = false; imp.importLights = false; changed = true; }
                if (changed) imp.SaveAndReimport();
            }
        }

        static Material PHMat(string id, Color tint, float smooth = 0.6f, bool cutout = false)
        {
            string dir = $"{PH}/{id}";
            Texture2D diff = Tex($"{dir}/{id}_diff_1k.jpg");
            if (cutout)
            {
                diff = CombineAlpha(id, diff, Tex($"{dir}/{id}_alpha_1k.png") ?? Tex($"{dir}/{id}_opacity_1k.png"));
            }
            var m = LitMat("PH_" + id, tint, smooth, diff, Tex($"{dir}/{id}_nor_gl_1k.png"), Tex($"{dir}/{id}_ms_1k.png"));
            if (cutout) MakeCutout(m, 0.45f);
            return m;
        }

        static Texture2D CombineAlpha(string id, Texture2D diff, Texture2D alpha)
        {
            if (diff == null || alpha == null) return diff;
            Texture2D d = ReadableCopy(diff), a = ReadableCopy(alpha);
            var o = new Texture2D(d.width, d.height, TextureFormat.RGBA32, false);
            var dp = d.GetPixels();
            for (int y = 0; y < d.height; y++)
            for (int x = 0; x < d.width; x++)
            {
                Color c = dp[y * d.width + x];
                c.a = a.GetPixelBilinear((x + 0.5f) / d.width, (y + 0.5f) / d.height).r;
                o.SetPixel(x, y, c);
            }
            o.Apply();
            return SavePng(id + "_diff_alpha", o, true, true, TextureWrapMode.Repeat);
        }

        static Texture2D ReadableCopy(Texture2D src)
        {
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, true);
            t.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            t.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return t;
        }

        static void CreateMaterials()
        {
            Texture2D shell = Tex(Art + "/Textures/Hatchling_Shell.png");
            M["Hatchling_Shell"] = LitMat("Hatchling_Shell", Color.white, 0.62f, shell);
            M["Hatchling_Skin"] = LitMat("Hatchling_Skin", new Color(0.12f, 0.115f, 0.1f), 0.5f);
            M["Hatchling_Eye"] = LitMat("Hatchling_Eye", new Color(0.01f, 0.01f, 0.01f), 0.95f);
            M["Gull_White"] = LitMat("Gull_White", new Color(0.82f, 0.83f, 0.84f), 0.15f);
            M["Gull_Grey"] = LitMat("Gull_Grey", new Color(0.45f, 0.49f, 0.53f), 0.15f);
            M["Gull_Black"] = LitMat("Gull_Black", new Color(0.03f, 0.03f, 0.035f), 0.3f);
            M["Gull_Beak"] = LitMat("Gull_Beak", new Color(0.85f, 0.65f, 0.2f), 0.4f);
            M["Gull_BeakSpot"] = LitMat("Gull_BeakSpot", new Color(0.75f, 0.12f, 0.06f), 0.4f);
            M["Gull_Eye"] = LitMat("Gull_Eye", new Color(0.9f, 0.85f, 0.4f), 0.9f);
            M["Gull_Legs"] = LitMat("Gull_Legs", new Color(0.82f, 0.58f, 0.55f), 0.3f);
            M["Hatchling_Belly"] = LitMat("Hatchling_Belly", new Color(0.42f, 0.38f, 0.3f), 0.4f);
            M["Hatchling_Beak"] = LitMat("Hatchling_Beak", new Color(0.3f, 0.28f, 0.22f), 0.55f);
            M["Avatar_Skin"] = LitMat("Avatar_Skin", new Color(0.72f, 0.55f, 0.44f), 0.35f);
            M["Avatar_Shirt"] = LitMat("Avatar_Shirt", new Color(0.12f, 0.3f, 0.34f), 0.15f);
            M["Avatar_Shorts"] = LitMat("Avatar_Shorts", new Color(0.18f, 0.2f, 0.28f), 0.15f);
            M["Avatar_Shoe"] = LitMat("Avatar_Shoe", new Color(0.78f, 0.78f, 0.74f), 0.3f);
            M["Avatar_Sole"] = LitMat("Avatar_Sole", new Color(0.12f, 0.12f, 0.12f), 0.2f);
            var pebble = LitMat("Pebble", new Color(0.55f, 0.5f, 0.45f), 0.25f, Tex($"{PH}/sand_rocks_small_01/sand_rocks_small_01_diff_1k.jpg"));
            pebble.enableInstancing = true;
            M["Pebble"] = pebble;
            M["LightCone"] = SaveMat("FlashlightCone", Shader.Find("SameShore/LightCone"));
            var decal = SaveMat("GroundDecal", Shader.Find("Universal Render Pipeline/Unlit"));
            decal.SetColor("_BaseColor", new Color(0, 0, 0, 0.5f));
            MakeTransparent(decal);
            M["GroundDecal"] = decal;
            var ring = SaveMat("WarningRing", Shader.Find("SameShore/SkyBillboard"));
            ring.SetColor("_Color", new Color(1f, 0.35f, 0.15f, 1f));
            M["WarningRing"] = ring;
            var waveWater = LitMat("WaveWater", new Color(0.05f, 0.13f, 0.16f, 0.82f), 0.95f);
            MakeTransparent(waveWater, true);
            M["WaveWater"] = waveWater;
            var waveFoam = LitMat("WaveFoam", new Color(0.75f, 0.82f, 0.86f, 0.9f), 0.6f);
            MakeTransparent(waveFoam, true);
            M["WaveFoam"] = waveFoam;

            var pet = LitMat("Trash_PET", new Color(0.78f, 0.88f, 0.92f, 0.38f), 0.92f);
            MakeTransparent(pet, true);
            M["Trash_PET"] = pet;
            M["Trash_Cap"] = LitMat("Trash_Cap", new Color(0.1f, 0.3f, 0.75f), 0.5f);
            M["Trash_Label"] = LitMat("Trash_Label", new Color(0.85f, 0.15f, 0.12f), 0.35f);
            var glass = LitMat("Trash_GlassBrown", new Color(0.32f, 0.16f, 0.05f, 0.82f), 0.95f);
            MakeTransparent(glass);
            M["Trash_GlassBrown"] = glass;
            var bag = LitMat("Trash_Bag", new Color(0.88f, 0.9f, 0.92f, 0.62f), 0.55f);
            MakeTransparent(bag, true);
            M["Trash_Bag"] = bag;

            Texture2D sand = Tex("Assets/Textures/Sand2/sand_03_diff_1k.jpg");
            M["Sandcastle"] = LitMat("Sandcastle", new Color(0.86f, 0.8f, 0.7f), 0.08f, sand);
            M["SandFill"] = LitMat("SandFill", new Color(0.7f, 0.62f, 0.5f), 0.05f, sand);
            M["Wood"] = LitMat("WoodPost", new Color(0.36f, 0.27f, 0.19f), 0.2f, Tex($"{PH}/dead_quiver_branch_01/dead_quiver_branch_01_diff_1k.jpg"));
            M["Rope"] = LitMat("Rope", new Color(0.62f, 0.55f, 0.42f), 0.1f);
            M["Sign"] = LitMat("SignBoard", new Color(0.82f, 0.78f, 0.66f), 0.25f);
            var bulb = LitMat("AmberBulb", new Color(1f, 0.55f, 0.2f), 0.6f);
            Emission(bulb, new Color(1f, 0.45f, 0.12f) * 2.2f);
            M["Bulb"] = bulb;
            M["Eggshell"] = LitMat("Eggshell", new Color(0.62f, 0.6f, 0.55f), 0.3f);
            M["Bin"] = LitMat("TrashBin", new Color(0.12f, 0.28f, 0.18f), 0.35f, null, null, null, 0.4f);
            M["BinLid"] = LitMat("TrashBinRim", new Color(0.18f, 0.2f, 0.2f), 0.5f, null, null, null, 0.8f);

            M["coast_rocks_01"] = PHMat("coast_rocks_01", new Color(0.85f, 0.85f, 0.85f), 0.35f);
            M["sand_rocks_small_01"] = PHMat("sand_rocks_small_01", Color.white, 0.3f);
            M["can_rusted"] = PHMat("can_rusted", Color.white, 0.6f);
            M["small_plastic_torch"] = PHMat("small_plastic_torch", Color.white, 0.6f);
            M["lambis_shell"] = PHMat("lambis_shell", Color.white, 0.5f);
            M["dead_quiver_branch_01"] = PHMat("dead_quiver_branch_01", new Color(0.75f, 0.72f, 0.68f), 0.4f);
            M["grass_medium_01"] = PHMat("grass_medium_01", new Color(0.62f, 0.66f, 0.5f), 0.3f, true);
            M["plastic_container"] = PHMat("plastic_container", Color.white, 0.6f);
            M["street_lamp_01"] = PHMat("street_lamp_01", Color.white, 0.6f);

            var ocean = SaveMat("Ocean", Shader.Find("SameShore/Ocean"));
            M["Ocean"] = ocean;
            M["Swash"] = SaveMat("Swash", Shader.Find("SameShore/Swash"));

            var moon = SaveMat("CrescentMoon", Shader.Find("SameShore/SkyBillboard"));
            moon.SetTexture("_MainTex", SavePng("CrescentMoon", ProceduralArt.Crescent()));
            moon.SetColor("_Color", new Color(1.3f, 1.3f, 1.25f, 1));
            M["Moon"] = moon;
            var hotel = SaveMat("HotelFacade", Shader.Find("Universal Render Pipeline/Unlit"));
            Texture2D windows = SavePng("HotelWindows", ProceduralArt.HotelWindows(), true, false, TextureWrapMode.Repeat);
            hotel.SetTexture("_BaseMap", windows);
            hotel.SetColor("_BaseColor", new Color(0.55f, 0.55f, 0.55f));
            M["Hotel"] = hotel;

            var foot = LitMat("Footprint", new Color(0.13f, 0.1f, 0.08f, 0.55f), 0.05f,
                              SavePng("Footprint", ProceduralArt.Footprint(), true, true));
            MakeTransparent(foot);
            foot.SetFloat("_ReceiveShadows", 1);
            M["Footprint"] = foot;

            var particle = SaveMat("SandParticle", Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            particle.SetColor("_BaseColor", new Color(0.55f, 0.47f, 0.36f, 1));
            M["SandParticle"] = particle;

            var fade = SaveMat("ScreenFade", Shader.Find("SameShore/ScreenFade"));
            M["Fade"] = fade;
            M["TurtleVision"] = SaveMat("TurtleVision", Shader.Find("SameShore/TurtleVision"));
            var ui = SaveMat("UIPanel", Shader.Find("SameShore/UIOverlay"));
            ui.SetTexture("_MainTex", SavePng("RoundedRect", ProceduralArt.RoundedRect()));
            M["UI"] = ui;
            var line = SaveMat("UILine", Shader.Find("SameShore/UIOverlay"));
            line.SetColor("_Color", new Color(0.75f, 0.92f, 1f, 0.9f));
            M["UILine"] = line;
            AssetDatabase.SaveAssets();
        }

        static SameShoreAssets CreateAssetsObject()
        {
            EnsureFolder(Root + "/Resources");
            string path = Root + "/Resources/SameShoreAssets.asset";
            var a = AssetDatabase.LoadAssetAtPath<SameShoreAssets>(path);
            if (a == null)
            {
                a = ScriptableObject.CreateInstance<SameShoreAssets>();
                AssetDatabase.CreateAsset(a, path);
            }
            a.fade = M["Fade"];
            a.turtleVision = M["TurtleVision"];
            a.uiPanel = M["UI"];
            a.uiLine = M["UILine"];
            a.sandParticle = M["SandParticle"];
            a.roundedRect = (Texture2D)M["UI"].GetTexture("_MainTex");
            a.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            AudioClip C(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Audio/{n}.wav");
            a.footsteps = new[] { C("footstep_sand_1"), C("footstep_sand_2"), C("footstep_sand_3"), C("footstep_sand_4") };
            a.flipperStrokes = new[] { C("flipper_stroke_1"), C("flipper_stroke_2"), C("flipper_stroke_3") };
            a.sandDrag = C("sand_drag_loop");
            a.gullCalls = new[] { C("gull_call_1"), C("gull_call_2"), C("gull_call_3") };
            a.gullFlap = C("gull_flap");
            a.click = C("flashlight_click");
            a.thud = C("thud_sand");
            a.pickup = C("pickup_rustle");
            a.sandPour = C("sand_pour_loop");
            a.heartbeat = C("heartbeat_loop");
            a.success = C("success_chime");
            a.transition = C("transition_swell");
            a.turtleBreath = C("turtle_breath_loop");
            a.nestScratching = C("nest_scratching");
            a.swashWash = C("swash_wash");
            a.waveCrashBig = C("wave_crash_big");
            a.underwater = C("underwater_loop");
            a.objectiveDone = C("objective_done");
            a.objectiveLeft = C("objective_left");
            a.uiTick = C("ui_tick");
            a.lightHum = C("light_hum_loop");
            a.gullScreech = C("gull_screech");
            a.gullDive = C("gull_dive_whoosh");
            a.endingPad = C("ending_pad");
            a.lightCone = M["LightCone"];
            a.groundDecal = M["GroundDecal"];
            a.warningRing = M["WarningRing"];
            a.turtleTrack = SavePng("TurtleTrack", ProceduralArt.TurtleTrack(), true, true);
            a.pebbles = AssetDatabase.LoadAllAssetsAtPath(Art + "/Models/Pebbles.fbx").OfType<Mesh>().ToArray();
            a.pebble = M["Pebble"];
            a.clips = AssetDatabase.FindAssets("t:AudioClip", new[] { Root + "/Audio" })
                .Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g))).Where(c => c != null).ToArray();
            a.ringTexture = SavePng("SafeZoneRing", ProceduralArt.Ring(), true, true);
            a.bulbOn = M["BulbOn"];
            a.bulbOff = M["BulbOff"];
            a.cable = M["Cable"];
            a.fireworkParticle = M["Firework"];
            a.sandCastle = M["Sandcastle"];
            EditorUtility.SetDirty(a);
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Root + "/Audio" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var imp = (AudioImporter)AssetImporter.GetAtPath(p);
                bool loop = p.Contains("_loop");
                var s = imp.defaultSampleSettings;
                var load = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                if (s.loadType != load || imp.forceToMono != (!p.Contains("ocean") && !p.Contains("wind")))
                {
                    s.loadType = load;
                    s.compressionFormat = AudioCompressionFormat.Vorbis;
                    s.quality = 0.6f;
                    imp.defaultSampleSettings = s;
                    imp.forceToMono = !p.Contains("ocean") && !p.Contains("wind");
                    imp.SaveAndReimport();
                }
            }
            return a;
        }

        // ================================================================== render settings & sky
        static void SetupRenderingAndSky()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.03f, 0.045f, 0.075f);
            RenderSettings.fogDensity = 0.012f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.07f, 0.09f, 0.16f);
            RenderSettings.ambientEquatorColor = new Color(0.045f, 0.055f, 0.085f);
            RenderSettings.ambientGroundColor = new Color(0.02f, 0.02f, 0.025f);

            // starry night sky from the project's CC0 HDRI (the team's procedural skybox material stays untouched;
            // its sun disc would otherwise draw a full moon where the moonlight comes from)
            // day -> sunset -> night sky (the night half uses the project's CC0 starry HDRI); DayCycle drives it
            RenderSettings.skybox = M["Sky"];

            // crescent moon low over the sea; the moonlight comes from the same side, a bit higher so the
            // cove cliffs do not throw the whole beach into shadow
            Vector3 moonDir = Quaternion.Euler(-12f, -24f, 0) * Vector3.forward;
            Vector3 lightDir = Quaternion.Euler(-36f, -24f, 0) * Vector3.forward;
            GameObject moonLightGo = Find("Moon");
            if (moonLightGo != null)
            {
                moonLightGo.transform.rotation = Quaternion.LookRotation(-lightDir);
                var l = moonLightGo.GetComponent<Light>();
                l.color = new Color(0.62f, 0.72f, 1f);
                l.intensity = 0.55f;
                l.shadows = LightShadows.Soft;
                l.shadowStrength = 0.75f;
                RenderSettings.sun = l;
            }
            _moonQuad = Prim(PrimitiveType.Quad, "CrescentMoon", _env, new Vector3(cx, 0, zs) + moonDir * 900f,
                                Vector3.one * 55f, M["Moon"], false, Quaternion.LookRotation(moonDir) * Quaternion.Euler(0, 0, -25));
            _moonQuad.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static GameObject _moonQuad, _ocean, _swash;

        // ================================================================== existing scene objects
        static void PrepareExistingObjects()
        {
            // VR, not mixed reality: no passthrough
            foreach (var mgr in Object.FindObjectsByType<OVRManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Set(mgr, "isInsightPassthroughEnabled", false);
            }
            foreach (var pt in Object.FindObjectsByType<OVRPassthroughLayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                pt.enabled = false;
            Deactivate("[BuildingBlock] Passthrough");
            Deactivate("WaterBlock_50m");     // replaced by the animated ocean + swash
            Deactivate("BoundariesWater");   // replaced by HumanSeaBarrier at the real shoreline
            Deactivate("EndPopup");          // replaced by the end screen of ExperienceDirector
            Deactivate("EndCanvas");

            foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 1500f;
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null) data.renderPostProcessing = true;
            }

            var gm = Object.FindFirstObjectByType<GameManager>();
            Set(gm, "startState", (int)GameState.Intro);
        }

        static void Deactivate(string name)
        {
            GameObject go = Find(name);
            if (go != null) go.SetActive(false);
        }

        // ================================================================== nest
        static Transform BuildNest()
        {
            var nest = Child(_env, "TurtleNest").transform;
            nest.position = LX(NestXY);
            float r = 0.85f;
            var posts = new List<Vector3>();
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2 + 0.2f;
                Vector3 p = G(nest.position.x + Mathf.Cos(a) * r, nest.position.z + Mathf.Sin(a) * r);
                posts.Add(p);
                var post = Prim(PrimitiveType.Cylinder, "Post", nest, p + Vector3.up * 0.3f, new Vector3(0.05f, 0.33f, 0.05f), M["Wood"], true,
                                Quaternion.Euler(Rand(-4, 4), Rand(0, 360), Rand(-4, 4)));
                if (i % 2 == 0)
                {
                    Prim(PrimitiveType.Sphere, "AmberBulb", nest, p + Vector3.up * 0.64f, Vector3.one * 0.035f, M["Bulb"]);
                }
            }
            for (int i = 0; i < 8; i++)
            {
                Vector3 a = posts[i], b = posts[(i + 1) % 8];
                foreach (float h in new[] { 0.28f, 0.52f })
                {
                    Vector3 pa = a + Vector3.up * h, pb = b + Vector3.up * h;
                    Prim(PrimitiveType.Cylinder, "Rope", nest, (pa + pb) / 2, new Vector3(0.008f, (pb - pa).magnitude / 2, 0.008f), M["Rope"], false,
                         Quaternion.FromToRotation(Vector3.up, pb - pa));
                }
            }
            // turtle-safe amber light: dim and long-wave, NOT a LightSource (it does not attract hatchlings)
            var lamp = new GameObject("TurtleSafeAmberLight").AddComponent<Light>();
            lamp.transform.SetParent(nest, false);
            lamp.transform.position = nest.position + Vector3.up * 0.7f;
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.45f, 0.12f);
            lamp.intensity = 0.08f;
            lamp.range = 1.8f;
            lamp.shadows = LightShadows.None;

            // sign
            Vector3 sp = G(nest.position.x + 1.0f, nest.position.z - 0.55f);
            var signPost = Prim(PrimitiveType.Cylinder, "SignPost", nest, sp + Vector3.up * 0.45f, new Vector3(0.045f, 0.45f, 0.045f), M["Wood"], true);
            var board = Prim(PrimitiveType.Cube, "SignBoard", nest, sp + Vector3.up * 0.95f, new Vector3(0.5f, 0.32f, 0.025f), M["Sign"], true,
                             Quaternion.Euler(0, 200, 0));
            var textGo = new GameObject("SignText");
            textGo.transform.SetParent(board.transform, false);
            textGo.transform.localPosition = new Vector3(0, 0, -0.56f);
            textGo.transform.localScale = new Vector3(1 / 0.5f, 1 / 0.32f, 1) * 0.1f;
            var tmp = textGo.AddComponent<TextMeshPro>();
            tmp.rectTransform.sizeDelta = new Vector2(4.6f, 2.9f);
            tmp.fontSize = 2.6f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.12f, 0.1f, 0.08f);
            tmp.text = "<b>SEA TURTLE NEST</b>\n<size=70%>Please do not disturb.\nKeep the beach dark and clean.</size>";

            // excavated sand mound where the clutch was laid
            var mound = Model(Art + "/Models/NestMound.fbx", nest, "NestMound");
            if (mound != null)
            {
                Remap(mound, new Dictionary<string, Material>(), M["SandFill"]);
                // follow the slope of the beach so no edge sticks out of the sand
                mound.transform.rotation = Quaternion.FromToRotation(Vector3.up, Normal(nest.position.x, nest.position.z)) * mound.transform.rotation;
                mound.transform.localScale = new Vector3(0.8f, 0.55f, 0.8f);
                mound.transform.position = nest.position - Vector3.up * 0.012f;
                foreach (var mrd in mound.GetComponentsInChildren<MeshRenderer>()) mrd.shadowCastingMode = ShadowCastingMode.Off;
            }
            var scratch = new GameObject("NestScratching").AddComponent<AudioSource>();
            scratch.transform.SetParent(nest, false);
            scratch.transform.position = nest.position + Vector3.up * 0.05f;
            scratch.spatialBlend = 1f;
            scratch.minDistance = 0.6f;
            scratch.maxDistance = 8f;
            scratch.playOnAwake = false;

            // a few eggshell fragments
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = G(nest.position.x + Rand(-0.35f, 0.35f), nest.position.z + Rand(-0.35f, 0.35f), 0.005f);
                Prim(PrimitiveType.Sphere, "Eggshell", nest, p, new Vector3(0.022f, 0.007f, 0.018f), M["Eggshell"], false,
                     Quaternion.Euler(Rand(-30, 30), Rand(0, 360), Rand(-30, 30)));
            }

            // turtle spawn inside the nest, facing the sea
            GameObject spawn = Find("TurtleSpawn");
            spawn.transform.SetPositionAndRotation(nest.position + Vector3.up * 0.02f, Quaternion.identity);
            return nest;
        }

        // ================================================================== sea
        static void BuildSea()
        {
            var ocean = _ocean = new GameObject("Ocean");
            ocean.transform.SetParent(_env, false);
            ocean.transform.position = new Vector3(cx, WaterLevel, zs);
            ocean.AddComponent<MeshFilter>().sharedMesh = SaveMesh("OceanGrid", ProceduralArt.OceanGrid(700, -3, 900, 120, 90));
            var r = ocean.AddComponent<MeshRenderer>();
            r.sharedMaterial = M["Ocean"];
            r.shadowCastingMode = ShadowCastingMode.Off;
            Vector4 shore = new Vector4(cx, WaterLevel, zs, 0);
            M["Ocean"].SetVector("_ShorePoint", shore);
            M["Ocean"].SetVector("_ShoreDir", new Vector4(0, 0, -1, 0));
            M["Swash"].SetVector("_ShorePoint", shore);
            M["Swash"].SetVector("_ShoreDir", new Vector4(0, 0, -1, 0));

            var swash = _swash = new GameObject("ShoreSwash");
            swash.transform.SetParent(_env, false);
            swash.AddComponent<MeshFilter>().sharedMesh = SaveMesh("SwashStrip",
                ProceduralArt.SwashStrip((x, z) => Mathf.Max(Ground(x, z), WaterLevel - 0.05f), cx - 22, cx + 22, zs + 1.5f, zs - 4f, 0.15f));
            var sr = swash.AddComponent<MeshRenderer>();
            sr.sharedMaterial = M["Swash"];
            sr.shadowCastingMode = ShadowCastingMode.Off;

            // goal: where the waves wash the sand (teammate's SeaGoal trigger, moved to the real shoreline)
            GameObject goal = Find("SeaGoal");
            goal.transform.position = L(0, 0.35f, 0.5f);
            goal.transform.rotation = Quaternion.identity;
            goal.transform.localScale = new Vector3(70f, 2.5f, 1.3f);
            goal.GetComponent<TurtleZone>().Type = TurtleZoneType.Goal;

            // the human may wet their feet but not walk into the sea
            var barrier = new GameObject("HumanSeaBarrier");
            barrier.transform.SetParent(_env, false);
            barrier.transform.position = L(0, -0.4f, 1.5f);
            var box = barrier.AddComponent<BoxCollider>();
            box.size = new Vector3(80f, 4f, 0.4f);

            // natural light over the sea horizon: what hatchlings use to find the water
            GameObject glow = Find("SeaGlow");
            glow.transform.position = new Vector3(cx, 2.2f, zs + 22f);
            var gl = glow.GetComponent<Light>();
            gl.color = new Color(0.55f, 0.66f, 0.9f);
            gl.intensity = 0.55f;
            gl.range = 80f;
            var ls = glow.GetComponent<LightSource>();
            Set(ls, "strength", 1f);
            Set(ls, "artificial", false);
        }

        // ================================================================== props & interactables
        class Props
        {
            public Transform chair, flashlight, hole;
            public List<GameObject> buckets = new List<GameObject>();
            public SandHole sandHole;
            public Transform safeZone, bin;
        }

        static Props PlaceProps()
        {
            var props = new Props();
            // the team's monobloc chair and wooden buckets are replaced by the beach camp (storyboard v2)
            Deactivate("plastic_monobloc_chair_01_1k");
            Deactivate("Wooden_Bucket");
            Deactivate("Wooden_Bucket (1)");

            // flashlight (team object): held with the VRHand grab in a defined pose, beam along the hand
            GameObject fl = Find("Flashlight");
            fl.transform.SetPositionAndRotation(L(3f, 7.6f, 0.035f), Quaternion.identity);   // identity while aligning the model
            props.flashlight = fl.transform;
            Transform beamT = fl.transform.Find("Beam");
            var beam = beamT.GetComponent<Light>();
            beamT.localPosition = new Vector3(0, 0, 0.1f);
            beamT.localRotation = Quaternion.identity;
            beam.type = LightType.Spot;
            beam.range = 11f;
            beam.spotAngle = 52f;
            beam.innerSpotAngle = 26f;
            beam.intensity = 6f;
            beam.color = new Color(1f, 0.87f, 0.72f);
            beam.shadows = LightShadows.Soft;
            Set(beamT.GetComponent<LightSource>(), "strength", 2.2f);
            Set(beamT.GetComponent<LightSource>(), "artificial", true);
            Transform cyl = fl.transform.Find("Cylinder");
            if (cyl != null) cyl.GetComponent<MeshRenderer>().enabled = false;
            foreach (Transform old in fl.transform.Cast<Transform>().ToArray())
                if (old.name == "TorchModel") Object.DestroyImmediate(old.gameObject);
            var torch = Model($"{PH}/small_plastic_torch/small_plastic_torch.fbx", fl.transform, "TorchModel");
            if (torch != null)
            {
                AlignLongAxis(torch, fl.transform, 0f, 0.2f);
                FaceHeadForward(torch, fl.transform);
                Remap(torch, new Dictionary<string, Material>(), M["small_plastic_torch"]);
            }
            // remove the ISDK grab (its free transform held the torch sideways) and use VRGrabbable instead
            foreach (var c in fl.GetComponentsInChildren<Component>(true).ToArray())
            {
                if (c == null) continue;
                string ns = c.GetType().Namespace ?? "";
                if (ns.StartsWith("Oculus.Interaction")) Object.DestroyImmediate(c);
            }
            foreach (var t in fl.GetComponentsInChildren<Transform>(true).ToArray())
                if (t != null && t.name.StartsWith("ISDK")) Object.DestroyImmediate(t.gameObject);
            foreach (var c in new System.Type[] { typeof(GripAlignment), typeof(GrabFeedback) })
                if (fl.GetComponent(c) != null) Object.DestroyImmediate(fl.GetComponent(c));
            var flRb = fl.GetComponent<Rigidbody>();
            if (flRb == null) flRb = fl.AddComponent<Rigidbody>();
            flRb.isKinematic = false;
            flRb.mass = 0.3f;
            var flGrab = MakeGrab(fl, 0.3f, "flashlight", false);
            flGrab.usePose = true;
            flGrab.posePosition = new Vector3(0f, -0.015f, 0.03f);   // in the fist, beam along the controller's pointing ray
            flGrab.poseEuler = Vector3.zero;
            flGrab.desktopOffset = new Vector3(0.2f, -0.2f, 0.42f);
            flGrab.desktopEuler = Vector3.zero;
            GetOrAdd<FlashlightUse>(fl);
            Set(fl.GetComponent<Flashlight>(), "grabbable", null);
            Set(fl.GetComponent<Flashlight>(), "startOn", false);
            GetOrAdd<LightConeVisual>(beamT.gameObject);
            var click = GetOrAdd<AudioSource>(fl);
            click.clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Audio/flashlight_click.wav");
            click.playOnAwake = false;
            click.spatialBlend = 1f;
            click.minDistance = 0.5f;
            click.maxDistance = 8f;
            Set(fl.GetComponent<Flashlight>(), "clickSound", click);

            // hole (team's Hole object): becomes the hole dug for the sandcastle (positioned by BuildSandcastle)
            GameObject hole = Find("Hole");
            hole.transform.rotation = Quaternion.identity;
            hole.transform.localScale = Vector3.one;
            var mr = hole.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
            var capsule = hole.GetComponent<CapsuleCollider>();
            capsule.isTrigger = true;
            capsule.radius = 0.4f;
            capsule.height = 0.5f;
            capsule.direction = 1;
            capsule.center = new Vector3(0, -0.1f, 0);
            var zone = hole.GetComponent<TurtleZone>();
            zone.Type = TurtleZoneType.Trap;
            zone.SpeedMultiplier = 0.25f;
            EditorUtility.SetDirty(zone);
            var sandHole = GetOrAdd<SandHole>(hole);
            hole.transform.position = LX(DigXY);
            Set(sandHole, "terrain", TerrainAt(hole.transform.position));
            Set(sandHole, "trapZone", zone);
            props.hole = hole.transform;
            props.sandHole = sandHole;
            return props;
        }

        /// <summary>The torch model's wide end (the head) must point along +Z (the beam).</summary>
        static void FaceHeadForward(GameObject model, Transform parent)
        {
            float front = 0, back = 0;
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                var m = mf.sharedMesh;
                foreach (var v in m.vertices)
                {
                    Vector3 p = parent.InverseTransformPoint(mf.transform.TransformPoint(v));
                    float r = new Vector2(p.x, p.y).magnitude;
                    if (p.z > 0.06f) front = Mathf.Max(front, r);
                    if (p.z < -0.06f) back = Mathf.Max(back, r);
                }
            }
            if (back > front * 1.05f)
            {
                model.transform.RotateAround(parent.position, parent.up, 180f);
                Debug.Log("[SameShore] torch model flipped so its head faces the beam");
            }
        }

        static void Trash(Transform parent, string name, string model, Vector3 pos, float yaw, float mass, string label,
                          bool isBag = false, bool lying = false, float maxSize = 0f)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0, yaw + Rand(-10, 10), 0);
            var visual = Model(model, root.transform, "Visual");
            if (visual == null) return;
            Remap(visual, M, M["Trash_PET"]);
            if (model.Contains("can_rusted")) Remap(visual, new Dictionary<string, Material>(), M["can_rusted"]);
            if (model.Contains("plastic_container")) Remap(visual, new Dictionary<string, Material>(), M["plastic_container"]);
            if (lying)
            {
                // models stand upright; lay bottles on their side
                visual.transform.localRotation = Quaternion.Euler(90, 0, 0) * visual.transform.localRotation;
            }
            Bounds b = RendererBounds(visual);
            if (maxSize > 0f)
            {
                float big = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                visual.transform.localScale *= maxSize / Mathf.Max(0.001f, big);
                b = RendererBounds(visual);
            }
            visual.transform.position += pos - new Vector3(b.center.x, b.min.y, b.center.z);
            b = RendererBounds(visual);

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.linearDamping = isBag ? 3f : 0.2f;
            rb.angularDamping = isBag ? 3f : lying ? 3f : 0.6f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            Vector3 localCenter = root.transform.InverseTransformPoint(b.center);
            Vector3 localSize = Abs(root.transform.InverseTransformVector(b.size));
            if (lying || name.StartsWith("Can"))
            {
                var cap = root.AddComponent<CapsuleCollider>();
                cap.center = localCenter;
                int axis = localSize.x >= localSize.y && localSize.x >= localSize.z ? 0 : localSize.y >= localSize.z ? 1 : 2;
                cap.direction = axis;
                cap.height = localSize[axis];
                cap.radius = Mathf.Min(localSize[(axis + 1) % 3], localSize[(axis + 2) % 3]) * 0.5f;
            }
            else
            {
                var box = root.AddComponent<BoxCollider>();
                box.center = localCenter;
                box.size = new Vector3(localSize.x, Mathf.Max(localSize.y, 0.015f), localSize.z);
            }
            if (isBag)
            {
                var tangle = new GameObject("TangleZone");
                tangle.transform.SetParent(root.transform, false);
                var t = tangle.AddComponent<BoxCollider>();
                t.isTrigger = true;
                t.center = localCenter;
                t.size = new Vector3(localSize.x * 1.1f, 0.08f, localSize.z * 1.1f);
                var z = tangle.AddComponent<TurtleZone>();
                z.Type = TurtleZoneType.Tangle;
                z.SpeedMultiplier = 0.3f;
                z.FirstEnterMessage = "A jellyfish? Food!\n...No. It's a plastic bag. It wraps around your flippers.";
            }
            root.AddComponent<TrashItem>();
            Set(root.GetComponent<TrashItem>(), "label", label);
            MakeGrabbable(root, rb);
            root.AddComponent<GrabFeedback>();
            Set(root.GetComponent<GrabFeedback>(), "grabbable", root.GetComponent<Grabbable>());
            var respawn = root.AddComponent<RespawnIfOutOfBounds>();
            Set(respawn, "maxDistanceFromStart", 80f); // litter carried to the bin must stay there
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        /// <summary>Same ISDK grab setup the team used on the flashlight/chair/buckets.</summary>
        static void MakeGrabbable(GameObject root, Rigidbody rb)
        {
            var grabbable = root.AddComponent<Grabbable>();
            Set(grabbable, "_rigidbody", rb);
            Set(grabbable, "_targetTransform", root.transform);
            Set(grabbable, "_kinematicWhileSelected", true);
            Set(grabbable, "_throwWhenUnselected", true);
            var isdk = new GameObject("ISDK_HandGrabInteraction");
            isdk.transform.SetParent(root.transform, false);
            var gi = isdk.AddComponent<GrabInteractable>();
            Set(gi, "_pointableElement", grabbable);
            Set(gi, "_rigidbody", rb);
            var hgi = isdk.AddComponent<HandGrabInteractable>();
            Set(hgi, "_pointableElement", grabbable);
            Set(hgi, "_rigidbody", rb);
        }

        static void AddFeedback(GameObject go)
        {
            var f = GetOrAdd<GrabFeedback>(go);
            Set(f, "grabbable", go.GetComponent<Grabbable>());
        }

        static GameObject Model(string path, Transform parent, string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) { Debug.LogWarning("[SameShore] missing model " + path); return null; }
            var go = (GameObject)Object.Instantiate(asset, parent, false);
            go.name = name;
            return go;
        }

        static void AlignLongAxis(GameObject model, Transform parent, float frontZ, float length)
        {
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            Bounds b = RendererBounds(model);
            Vector3 s = parent.InverseTransformVector(b.size);
            s = Abs(s);
            if (s.x > s.y && s.x > s.z) model.transform.localRotation = Quaternion.Euler(0, 90, 0);
            else if (s.y > s.z) model.transform.localRotation = Quaternion.Euler(90, 0, 0);
            b = RendererBounds(model);
            float len = Abs(parent.InverseTransformVector(b.size)).z;
            model.transform.localScale *= length / Mathf.Max(0.001f, len);
            b = RendererBounds(model);
            model.transform.position += parent.position - b.center;
        }

        static void AddMeshColliders(GameObject go, bool convex)
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = convex;
            }
        }

        static Terrain TerrainAt(Vector3 p)
        {
            foreach (Terrain t in _terrains)
            {
                Vector3 o = t.transform.position, s = t.terrainData.size;
                if (p.x >= o.x && p.z >= o.z && p.x <= o.x + s.x && p.z <= o.z + s.z) return t;
            }
            return Terrain.activeTerrain;
        }

        // ================================================================== natural decor
        static void BuildDecor()
        {
            var decor = Child(_env, "Decor").transform;
            // rocks
            // (dx, d, max size in metres)
            // framing the hatchlings' route: low rocks on both sides guide the eye from the nest to the sea
            (float dx, float d, float s)[] small = { (-2.7f, 13.6f, 0.55f), (-2.6f, 9.2f, 0.7f), (-3.1f, 4.8f, 0.6f), (2.9f, 15.8f, 0.5f), (3.4f, 12.2f, 0.45f), (6.5f, 6.0f, 0.5f), (-2.8f, 0.9f, 0.8f), (-4.6f, 16.8f, 0.7f) };
            int k = 0;
            foreach (var r in small) Rock(decor, $"{PH}/sand_rocks_small_01/sand_rocks_small_01.fbx", k++, L(r.dx, r.d), r.s, M["sand_rocks_small_01"]);
            (float dx, float d, float s)[] big = { (-10f, 0.5f, 3.2f), (12.5f, 0f, 3.6f), (-8.5f, -2.5f, 2.6f), (15f, -3f, 3f) };
            foreach (var r in big) Rock(decor, $"{PH}/coast_rocks_01/coast_rocks_01.fbx", k++, L(r.dx, r.d), r.s, M["coast_rocks_01"]);

            // driftwood branches
            foreach (var (dx, d, yaw) in new[] { (-2.1f, 11.6f, 30f), (2.6f, 17.0f, -50f), (-1.9f, 2.6f, -70f) })
            {
                var w = Model($"{PH}/dead_quiver_branch_01/dead_quiver_branch_01.fbx", decor, "Driftwood");
                if (w == null) continue;
                Vector3 p = L(dx, d);
                w.transform.rotation = OnGround(p, yaw) * w.transform.rotation;
                PlaceOn(w, p, 0.03f);
                Remap(w, new Dictionary<string, Material>(), M["dead_quiver_branch_01"]);
                AddMeshColliders(w, true);
            }
            // shells
            for (int i = 0; i < 9; i++)
            {
                var s = Model($"{PH}/lambis_shell/lambis_shell.fbx", decor, "Shell");
                if (s == null) continue;
                Vector3 p = L(Rand(-5, 7), Rand(0.5f, 6f), 0.005f);
                s.transform.rotation = OnGround(p, Rand(0, 360)) * s.transform.rotation;
                s.transform.localScale *= Rand(0.5f, 0.9f);
                PlaceOn(s, p, 0.01f);
                Remap(s, new Dictionary<string, Material>(), M["lambis_shell"]);
            }
            // dune grass at the back and sides of the beach
            for (int i = 0; i < 70; i++)
            {
                float side = Rand(0, 1);
                Vector3 p = side < 0.55f ? L(Rand(-9, 17), Rand(20.5f, 29f)) : L(side < 0.78f ? Rand(-11, -7.5f) : Rand(15.5f, 19f), Rand(6, 24));
                if (p.y < 1.5f) continue;
                var g = ModelPart($"{PH}/grass_medium_01/grass_medium_01.fbx", Rng.Next(100), decor, "DuneGrass");
                if (g == null) break;
                g.transform.rotation = Quaternion.Euler(0, Rand(0, 360), 0) * g.transform.rotation;
                g.transform.localScale *= Rand(0.9f, 1.5f);
                PlaceOn(g, p, 0.03f);
                foreach (var mr in g.GetComponentsInChildren<MeshRenderer>())
                {
                    mr.sharedMaterial = M["grass_medium_01"];
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
        }

        /// <summary>Instantiate one mesh of a multi-mesh model, keeping the importer's orientation.</summary>
        static GameObject ModelPart(string path, int index, Transform parent, string name)
        {
            var go = Model(path, parent, name);
            if (go == null) return null;
            var renderers = go.GetComponentsInChildren<MeshRenderer>();
            MeshRenderer keep = renderers[index % renderers.Length];
            foreach (var r in renderers)
                if (r != keep && !keep.transform.IsChildOf(r.transform)) Object.DestroyImmediate(r.gameObject);
            return go;
        }

        /// <summary>Move a model so its bounds are centred on p (xz) with the bottom sunk by 'sink' metres.</summary>
        static void PlaceOn(GameObject go, Vector3 p, float sink)
        {
            Bounds b = RendererBounds(go);
            go.transform.position += new Vector3(p.x - b.center.x, p.y - b.min.y - sink, p.z - b.center.z);
        }

        static void Rock(Transform parent, string path, int i, Vector3 p, float maxSize, Material mat)
        {
            var go = ModelPart(path, i, parent, "Rock");
            if (go == null) return;
            go.transform.rotation = Quaternion.Euler(0, Rand(0, 360), 0) * go.transform.rotation;
            Bounds b = RendererBounds(go);
            float size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            go.transform.localScale *= maxSize / Mathf.Max(0.01f, size);
            Remap(go, new Dictionary<string, Material>(), mat);
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = true;
            }
            b = RendererBounds(go);
            PlaceOn(go, p, b.size.y * 0.15f);
        }

        // ================================================================== footprints
        static FootprintSystem BuildFootprints(Transform systems)
        {
            var go = new GameObject("Footprints");
            go.transform.SetParent(_env, false);
            var fs = go.AddComponent<FootprintSystem>();
            Mesh quad = SaveMesh("FootprintQuad", FootprintSystem.BuildQuad());
            Set(fs, "footprintMaterial", M["Footprint"]);
            Set(fs, "printMesh", quad);
            Set(fs, "hatchlingSpeedMultiplier", 0.9f);
            // older trails left by people earlier in the evening
            // the way the human came to the beach this afternoon, and a stroll along the water
            Trail(go.transform, quad, new[] { L(8f, 21f), L(5.5f, 14f), L(3.8f, 10.4f) });
            Trail(go.transform, quad, new[] { L(9f, 1.2f), L(5f, 1.8f), L(2.5f, 1.4f) });
            return fs;
        }

        static void Trail(Transform parent, Mesh quad, Vector3[] pts)
        {
            bool left = false;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                Vector3 a = pts[i], b = pts[i + 1];
                Vector3 dir = (b - a);
                dir.y = 0;
                float len = dir.magnitude;
                dir.Normalize();
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                for (float t = 0; t < len; t += 0.7f)
                {
                    left = !left;
                    Vector3 p = a + dir * t + side * (left ? -0.11f : 0.11f);
                    p = G(p.x, p.z);
                    FootprintSystem.CreatePrint(parent, p, Normal(p.x, p.z), dir, left, M["Footprint"], new Vector2(0.11f, 0.27f), 0.9f, quad);
                }
            }
        }

        // ================================================================== gulls
        static Seagull[] BuildGulls()
        {
            var root = Child(_env, "Seagulls").transform;
            var list = new List<Seagull>();
            foreach (var (dx, d, yaw) in new[] { (4f, 1.6f, 200f), (5.3f, 1.1f, 160f), (-3.2f, 2.7f, 120f), (13.5f, 0.6f, 230f) })
            {
                var g = new GameObject("Seagull");
                g.transform.SetParent(root, false);
                Vector3 p = L(dx, d, 0.13f);
                g.transform.SetPositionAndRotation(p, Quaternion.Euler(0, yaw, 0));
                var bodyPivot = Child(g.transform, "Body").transform;
                var model = Model(Art + "/Models/Seagull.fbx", bodyPivot, "Model");
                Remap(model, M);
                OrientByChild(model, "Head");
                // wing pivots aligned with the bird so the flap axis is its long axis
                Transform pli = Pivot(bodyPivot, FindDeep(model.transform, "WingL_In"));
                Transform pri = Pivot(bodyPivot, FindDeep(model.transform, "WingR_In"));
                Transform plo = Pivot(pli, FindDeep(model.transform, "WingL_Out"));
                Transform pro = Pivot(pri, FindDeep(model.transform, "WingR_Out"));
                var voice = g.AddComponent<AudioSource>();
                voice.spatialBlend = 1;
                voice.minDistance = 1.5f;
                voice.maxDistance = 45f;
                voice.rolloffMode = AudioRolloffMode.Logarithmic;
                voice.dopplerLevel = 0;
                var s = g.AddComponent<Seagull>();
                Set(s, "wingLIn", pli);
                Set(s, "wingLOut", plo);
                Set(s, "wingRIn", pri);
                Set(s, "wingROut", pro);
                if (list.Count == 3) g.name = "HunterGull";
                Set(s, "body", bodyPivot);
                Set(s, "voice", voice);
                list.Add(s);
            }
            return list.ToArray();
        }

        static Transform Pivot(Transform parent, Transform part)
        {
            if (part == null) return null;
            var p = new GameObject(part.name + "Pivot").transform;
            p.SetParent(parent, false);
            Bounds b = part.GetComponent<Renderer>().bounds;
            // shoulder = the inner end of the wing (closest to the body centre)
            Vector3 inner = parent.position;
            Vector3 shoulder = b.ClosestPoint(new Vector3(inner.x, b.center.y, inner.z));
            p.position = shoulder;
            p.rotation = parent.rotation;
            part.SetParent(p, true);
            return p;
        }

        /// <summary>Rotate a model so its named child (head) points along the parent's +Z.</summary>
        /// <summary>
        /// Hatchling model scale. The Blender model is a 6 cm hatchling with its eyes ~0.8 cm up; the experience is
        /// perceived at 1:20 with the eyes ~8 cm over the sand (PDF), so the body is scaled to carry the camera at its
        /// eyes: x2.7 plus the crawling posture (front raised, head up) gives ~8 cm eyes on a ~16 cm shell.
        /// </summary>
        const float HatchlingScale = 2.7f;

        /// <summary>Crawling posture: neck and head raised to look at the horizon, front of the body a little up.</summary>
        static void PoseHatchling(Transform root, GameObject model)
        {
            Transform neck = FindDeep(model.transform, "Neck");
            Vector3 fwd = root.forward, right = root.right;
            if (neck != null && neck.GetComponent<Renderer>() != null)
            {
                Bounds nb = neck.GetComponent<Renderer>().bounds;
                Vector3 pivot = nb.center - fwd * Vector3.Dot(nb.extents, new Vector3(Mathf.Abs(fwd.x), Mathf.Abs(fwd.y), Mathf.Abs(fwd.z))) * 0.7f;
                foreach (var part in new[] { "Neck", "Head", "Beak", "EyeL", "EyeR" })
                {
                    var t = FindDeep(model.transform, part);
                    if (t != null) t.RotateAround(pivot, right, part == "Neck" ? -24f : -40f);
                }
            }
            // front up by 8 degrees around the rear of the plastron, then back onto the ground
            Bounds all = RendererBounds(model);
            Vector3 rear = new Vector3(all.center.x, all.min.y, all.center.z) - fwd * Vector3.Dot(all.extents, new Vector3(Mathf.Abs(fwd.x), 0, Mathf.Abs(fwd.z))) * 0.8f;
            model.transform.RotateAround(rear, right, -8f);
            all = RendererBounds(model);
            model.transform.position += Vector3.up * (root.position.y - all.min.y - 0.002f);
        }

        static void OrientByChild(GameObject model, string child)
        {
            Transform c = FindDeep(model.transform, child);
            if (c == null) return;
            Bounds all = RendererBounds(model);
            Vector3 headCenter = c.GetComponent<Renderer>().bounds.center;
            Vector3 dir = model.transform.parent.InverseTransformDirection(headCenter - all.center);
            dir.y = 0;
            if (dir.sqrMagnitude < 1e-6f) return;
            float yaw = Vector3.SignedAngle(dir.normalized, Vector3.forward, Vector3.up);
            model.transform.localRotation = Quaternion.Euler(0, yaw, 0) * model.transform.localRotation;
            // feet on the ground: lowest point at the parent origin
            all = RendererBounds(model);
            model.transform.position += new Vector3(0, model.transform.parent.position.y - all.min.y, 0);
        }

        // ================================================================== hotel
        static void BuildHotel()
        {
            var root = Child(_env, "DistantHotel").transform;
            (float x, float z, float w, float h, float d)[] blocks = { (-38f, -95f, 30f, 34f, 14f), (-12f, -100f, 22f, 26f, 14f), (-60f, -88f, 18f, 22f, 12f) };
            foreach (var b in blocks)
            {
                var go = Prim(PrimitiveType.Cube, "HotelBlock", root, new Vector3(b.x, b.h / 2, b.z), new Vector3(b.w, b.h, b.d), M["Hotel"]);
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                var mf = go.GetComponent<MeshFilter>();
                // tile the windows texture to the block size
                var mesh = Object.Instantiate(mf.sharedMesh);
                var uv = mesh.uv;
                var n = mesh.normals;
                for (int i = 0; i < uv.Length; i++)
                {
                    float sx = Mathf.Abs(n[i].z) > 0.5f ? b.w : b.d;
                    uv[i] = new Vector2(uv[i].x * sx / 16f, uv[i].y * b.h / 24f * 1.4f);
                    if (Mathf.Abs(n[i].y) > 0.5f) uv[i] = Vector2.zero;
                }
                mesh.uv = uv;
                mf.sharedMesh = SaveMesh("HotelBlock_" + b.x, mesh);
            }
            // the hotel's glare: weak artificial pull on hatchlings, from behind the dunes
            var glare = new GameObject("HotelGlare").AddComponent<Light>();
            glare.transform.SetParent(root, false);
            glare.transform.position = new Vector3(-30f, 25f, -90f);
            glare.type = LightType.Point;
            glare.range = 150f;
            glare.intensity = 0f; // purely perceptual: it does not light the beach
            var ls = glare.gameObject.AddComponent<LightSource>();
            Set(ls, "strength", 0.35f);
            Set(ls, "artificial", true);
        }

        // ================================================================== audio
        static AmbientAudio BuildAudio(Transform systems)
        {
            var root = Child(_env, "Soundscape").transform;
            AudioClip C(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Audio/{n}.wav");
            var channels = new List<AmbientAudio.Channel>();
            AudioSource Src(string name, AudioClip clip, Vector3 pos, float blend, float min, float max)
            {
                var a = new GameObject(name).AddComponent<AudioSource>();
                a.transform.SetParent(root, false);
                a.transform.position = pos;
                a.clip = clip;
                a.loop = true;
                a.playOnAwake = true;
                a.spatialBlend = blend;
                a.rolloffMode = AudioRolloffMode.Logarithmic;
                a.minDistance = min;
                a.maxDistance = max;
                a.dopplerLevel = 0;
                a.volume = 0;
                a.time = 0;
                return a;
            }
            foreach (float dx in new[] { -10f, 4f, 18f })
            {
                channels.Add(new AmbientAudio.Channel { name = "Waves", source = Src("Waves", C("ocean_waves_loop"), L(dx, -2f, 0.5f), 0.85f, 7f, 90f), humanVolume = 0.85f, hatchlingVolume = 0f });
                channels.Add(new AmbientAudio.Channel { name = "WavesClose", source = Src("WavesHatchling", C("ocean_waves_close_loop"), L(dx, -2f, 0.5f), 0.7f, 9f, 90f), humanVolume = 0f, hatchlingVolume = 0.5f });   // the sea stays under the human music for the hatchling
            }
            channels.Add(new AmbientAudio.Channel { name = "Wind", source = Src("Wind", C("wind_loop"), L(0, 8, 2), 0f, 1, 10), humanVolume = 0.32f, hatchlingVolume = 0.08f, hatchlingLowpass = 2500f });
            // (the music now comes from the human's own speaker, see MusicPlayer / TurtleHearing)
            channels.Add(new AmbientAudio.Channel { name = "Voices", source = Src("PeopleVoices", C("voices_muffled_loop"), new Vector3(-6f, 6f, -32f), 1f, 10f, 120f), humanVolume = 0.12f, hatchlingVolume = 0.12f, humanLowpass = 2500f, hatchlingLowpass = 520f });
            var amb = systems.gameObject.AddComponent<AmbientAudio>();
            var so = new SerializedObject(amb);
            var arr = so.FindProperty("channels");
            arr.arraySize = channels.Count;
            for (int i = 0; i < channels.Count; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("name").stringValue = channels[i].name;
                e.FindPropertyRelative("source").objectReferenceValue = channels[i].source;
                e.FindPropertyRelative("humanVolume").floatValue = channels[i].humanVolume;
                e.FindPropertyRelative("hatchlingVolume").floatValue = channels[i].hatchlingVolume;
                e.FindPropertyRelative("humanLowpass").floatValue = channels[i].humanLowpass;
                e.FindPropertyRelative("hatchlingLowpass").floatValue = channels[i].hatchlingLowpass;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return amb;
        }

        // ================================================================== player, turtle, systems
        static void WireSystems(Transform systems, Transform nest, Props props, FootprintSystem footprints, Seagull[] gulls, AmbientAudio audio, Story story)
        {
            var rig = Object.FindFirstObjectByType<OVRCameraRig>();
            Transform rigRoot = rig.transform;
            while (rigRoot.parent != null) rigRoot = rigRoot.parent;

            // ---- human body (smooth stick locomotion on the rig root)
            var cc = GetOrAdd<CharacterController>(rigRoot.gameObject);
            cc.radius = 0.2f;
            cc.height = 1.6f;
            cc.center = new Vector3(0, 0.82f, 0);
            cc.stepOffset = 0.3f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.03f;
            cc.minMoveDistance = 0f; // never drop small per-frame moves (high frame rates)
            var loco = GetOrAdd<SmoothLocomotion>(rigRoot.gameObject);
            Set(loco, "head", rig.centerEyeAnchor);
            Vector3 start = MeshKit.OnGround(story.seat.position);
            rigRoot.position = start;
            rigRoot.rotation = Quaternion.Euler(0, story.seat.eulerAngles.y, 0);

            var humanStart = Child(_env, "HumanStart").transform;
            humanStart.SetPositionAndRotation(start, Quaternion.Euler(0, story.seat.eulerAngles.y, 0));

            var human = systems.gameObject.AddComponent<HumanController>();
            Set(human, "locomotion", loco);
            Set(human, "footprints", footprints);
            GameObject hotspot = Find("Teleport Hotspot");
            Set(human, "disableObjects", hotspot != null ? new Object[] { hotspot } : new Object[0]);

            // ---- hatchling body (teammate's TurtleBody + controller)
            GameObject body = Find("TurtleBody");
            body.transform.SetPositionAndRotation(Find("TurtleSpawn").transform.position, Quaternion.identity);
            var tcc = body.GetComponent<CharacterController>();
            // body sized to the 1:20 perception: shell ~14 cm, eyes ~8 cm over the sand (PDF), see HatchlingScale
            tcc.radius = 0.058f;
            tcc.height = 0.12f;
            tcc.center = new Vector3(0, 0.06f, 0);
            tcc.stepOffset = 0.03f;
            tcc.slopeLimit = 50f;      // walks into and out of the bowl and over the berm toes (the hidden limits keep the corridor)
            tcc.skinWidth = 0.005f;
            tcc.minMoveDistance = 0f;
            var turtle = body.GetComponent<TurtleController>();
            foreach (Transform old in body.transform.Cast<Transform>().ToArray())
                if (old.name == "HatchlingModel" || old.name == "EyeAnchor") Object.DestroyImmediate(old.gameObject);
            var avatarRoot = Child(body.transform, "HatchlingModel");
            avatarRoot.transform.localPosition = Vector3.zero;
            avatarRoot.transform.localScale = Vector3.one * HatchlingScale;
            var model = Model(Art + "/Models/Hatchling.fbx", avatarRoot.transform, "Mesh");
            Remap(model, M);
            OrientByChild(model, "Head");
            PoseHatchling(avatarRoot.transform, model);
            // the camera is calibrated onto the eyes (PlayerPhaseSwitcher); the player's own head is not drawn
            // (only its shadow), so it never covers the view; shell, neck and flippers stay visible looking down
            var eyeAnchor = Child(body.transform, "EyeAnchor").transform;
            Transform eyeL = FindDeep(model.transform, "EyeL"), eyeR = FindDeep(model.transform, "EyeR");
            Vector3 eyes = eyeL != null && eyeR != null
                ? (eyeL.GetComponent<Renderer>().bounds.center + eyeR.GetComponent<Renderer>().bounds.center) * 0.5f
                : body.transform.TransformPoint(new Vector3(0, 0.08f, 0.08f));
            eyeAnchor.position = eyes + Vector3.up * 0.004f;
            eyeAnchor.rotation = body.transform.rotation;
            Debug.Log($"[SameShore] hatchling eye height = {eyeAnchor.position.y - body.transform.position.y:F3} m, shell length = {0.06f * HatchlingScale:F3} m");
            var avatar = avatarRoot.AddComponent<HatchlingAvatar>();
            WireAvatar(avatar, model);
            avatar.controller = turtle;
            foreach (var part in new[] { "Head", "Beak", "EyeL", "EyeR" })
            {
                var t = FindDeep(model.transform, part);
                if (t != null && t.GetComponent<Renderer>() != null) t.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
            var strokeSrc = body.AddComponent<AudioSource>();
            strokeSrc.spatialBlend = 0;
            strokeSrc.playOnAwake = false;
            var dragSrc = body.AddComponent<AudioSource>();
            dragSrc.clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Audio/sand_drag_loop.wav");
            dragSrc.loop = true;
            dragSrc.playOnAwake = true;
            dragSrc.volume = 0;
            dragSrc.spatialBlend = 0;
            Set(turtle, "strokeSource", strokeSrc);
            Set(turtle, "dragSource", dragSrc);
            // design values (the scene still held the first prototype's numbers)
            Set(turtle, "strokeDistance", 0.14f);   // ~0.17 m/s average: about 1:0.12 of the human walk, x20 perceived
            Set(turtle, "strokeDuration", 0.6f);
            Set(turtle, "strokeThreshold", 0.55f);
            Set(turtle, "turnSpeed", 25f);
            Set(turtle, "lightSteer", 0.55f);
            Set(turtle, "maxPushMass", 1f);
            Set(turtle, "pushStrength", 0.3f);
            Set(turtle, "pushGain", 25f);
            Set(turtle, "zoneProbeRadius", 0.06f);

            // ---- nest mates
            var sibs = new List<Object>();
            for (int i = 0; i < 4; i++)
            {
                float a = i / 4f * Mathf.PI * 2 + 0.6f;
                Vector3 p = G(nest.position.x + Mathf.Cos(a) * 0.42f, nest.position.z + Mathf.Sin(a) * 0.42f);
                var sib = Child(nest, "Sibling" + i);
                sib.transform.SetPositionAndRotation(p, Quaternion.Euler(0, Rand(-50, 50), 0));
                sib.transform.localScale = Vector3.one * HatchlingScale;
                var m = Model(Art + "/Models/Hatchling.fbx", sib.transform, "Mesh");
                Remap(m, M);
                OrientByChild(m, "Head");
                PoseHatchling(sib.transform, m);
                var av = sib.AddComponent<HatchlingAvatar>();
                WireAvatar(av, m);
                var hs = sib.AddComponent<HatchlingSibling>();
                Set(hs, "avatar", av);
                sibs.Add(hs);
            }
            var nestCtl = nest.gameObject.AddComponent<NestController>();
            Set(nestCtl, "siblings", sibs.ToArray());
            Set(nestCtl, "mound", FindDeep(nest, "NestMound"));
            Set(nestCtl, "scratchSource", FindDeep(nest, "NestScratching").GetComponent<AudioSource>());

            // ---- phase switcher (teammate's component on the GameManager object)
            var switcher = Object.FindFirstObjectByType<PlayerPhaseSwitcher>();
            Set(switcher, "turtle", turtle);
            Set(switcher, "turtleSpawn", Find("TurtleSpawn").transform);
            Set(switcher, "playerRoot", rigRoot);
            Set(switcher, "head", rig.centerEyeAnchor);
            Set(switcher, "turtleScale", 0.05f);
            Set(switcher, "eyeAnchor", eyeAnchor);
            GameObject interactionRig = Find("[BuildingBlock] OVRComprehensiveInteractionRig");
            Set(switcher, "humanOnlyObjects", new Object[] { Find("HumanSeaBarrier"), interactionRig });

            // ---- perception (post-processing volumes)
            var humanVol = MakeVolume("Volume_HumanView", HumanProfile(), 1f);
            var hatchVol = MakeVolume("Volume_HatchlingView", HatchlingProfile(), 0f);
            var perception = systems.gameObject.AddComponent<PerceptionController>();
            Set(perception, "humanVolume", humanVol);
            Set(perception, "hatchlingVolume", hatchVol);

            // ---- shared memory of the day, day cycle, hands
            var seaPoint = Child(_env, "SeaPoint").transform;
            seaPoint.position = L(0, 0);
            var log = systems.gameObject.AddComponent<BeachLog>();
            var day = systems.gameObject.AddComponent<DayCycle>();
            Set(day, "sun", Find("Moon").GetComponent<Light>());
            Set(day, "skyMaterial", M["Sky"]);
            Set(day, "ocean", _ocean.GetComponent<MeshRenderer>());
            Set(day, "swash", _swash.GetComponent<MeshRenderer>());
            Set(day, "moon", _moonQuad.GetComponent<MeshRenderer>());
            Set(day, "hotel", GameObject.Find("DistantHotel").GetComponentsInChildren<MeshRenderer>().Cast<Object>().ToArray());
            foreach (var hand in new[] { Hand.Left, Hand.Right })
            {
                var h = Child(systems, "VRHand" + hand).AddComponent<VRHand>();
                h.hand = hand;
            }

            // ---- the hatchling's act
            var hunt = systems.gameObject.AddComponent<GullEncounters>();
            Set(hunt, "gull", gulls[gulls.Length - 1]);
            Set(hunt, "sea", seaPoint);
            Set(hunt, "shadowMaterial", M["GroundDecal"]);
            Set(hunt, "ringMaterial", M["WarningRing"]);
            var ts = systems.gameObject.AddComponent<TurtleStory>();
            var vitality = systems.gameObject.AddComponent<TurtleVitality>();
            Set(vitality, "turtle", turtle);
            Set(vitality, "garland", story.garland);
            Set(hunt, "vitality", vitality);
            var hearing = systems.gameObject.AddComponent<TurtleHearing>();
            Set(hearing, "music", story.music);
            Set(hearing, "ambient", audio);
            // narrative triggers across the corridor (the way to the sea always crosses them)
            Set(hearing, "musicZone", CorridorTrigger("MusicZone", 11.6f, 1.4f));
            Set(ts, "gullZone1", CorridorTrigger("GullZone1", 13.2f, 0.8f));
            Set(ts, "gullZone2", CorridorTrigger("GullZone2", 5.75f, 0.8f));
            Set(ts, "jellyZone", CorridorTrigger("JellyfishZone", BagLandXY.y + 1.1f, 0.6f));
            Set(ts, "vitality", vitality);
            Set(ts, "turtle", turtle);
            Set(ts, "nest", nest);
            Set(ts, "sea", seaPoint);
            Set(ts, "route", story.route);
            Set(ts, "cans", story.cans);
            Set(ts, "hole", story.hole);
            Set(ts, "bag", story.bag);
            Set(ts, "garland", story.garland);
            Set(ts, "giant", story.giant);
            Set(ts, "gulls", hunt);
            Set(ts, "hearing", hearing);

            var fx = body.AddComponent<TurtleFX>();
            Set(fx, "turtle", turtle);
            Set(fx, "flipperL", avatar.flipperFL);
            Set(fx, "flipperR", avatar.flipperFR);
            var fxSo = new SerializedObject(fx);
            fxSo.FindProperty("pebbleArea").rectValue = new Rect(cx - 7f, zs - 20f, 14f, 21f);
            fxSo.ApplyModifiedPropertiesWithoutUndo();

            var objectives = systems.gameObject.AddComponent<ObjectiveTracker>();
            var finalWave = systems.gameObject.AddComponent<FinalWave>();
            Set(finalWave, "waterMaterial", M["WaveWater"]);
            Set(finalWave, "foamMaterial", M["WaveFoam"]);

            BuildHumanBody(systems);
            var humanAvatar = systems.GetComponentInChildren<HumanAvatar>();
            var seat = systems.gameObject.AddComponent<HumanSeat>();
            Set(seat, "avatar", humanAvatar);
            Set(seat, "locomotion", loco);

            var fader = systems.gameObject.AddComponent<ScreenFader>();
            // Editor without a headset: mouse/keyboard testing (inactive on Quest / with Link)
            systems.gameObject.AddComponent<DesktopTestingMode>();
            foreach (var hand in new[] { Hand.Left, Hand.Right })
            {
                var p = Child(systems, "Pointer" + hand).AddComponent<VRPointer>();
                p.hand = hand;
            }

            var director = systems.gameObject.AddComponent<ExperienceDirector>();
            Set(director, "phaseSwitcher", switcher);
            Set(director, "turtle", turtle);
            Set(director, "human", human);
            Set(director, "seat", seat);
            Set(director, "fader", fader);
            Set(director, "perception", perception);
            Set(director, "ambient", audio);
            Set(director, "objectives", objectives);
            Set(director, "finalWave", finalWave);
            Set(director, "nest", nestCtl);
            Set(director, "turtleFX", fx);
            Set(director, "day", day);
            Set(director, "log", log);
            Set(director, "turtleStory", ts);
            Set(director, "gulls", hunt);
            Set(director, "chairSeat", story.seat);
            Set(director, "towel", story.towel);
            Set(director, "drink", story.drink);
            Set(director, "music", story.music);
            Set(director, "canToss", story.cans);
            Set(director, "castle", story.castle);
            Set(director, "shovel", story.shovel);
            Set(director, "sandwich", story.sandwich);
            Set(director, "bag", story.bag);
            Set(director, "fireworks", story.fireworks);
            Set(director, "garland", story.garland);
            Set(director, "flashlight", props.flashlight.GetComponent<Flashlight>());
            Set(director, "phone", story.phone);
            Set(director, "photoTurtle", story.photoTurtle);
            Set(director, "photoTurtleStart", story.photoTurtleStart);
            Set(director, "humanStart", humanStart);
            Set(director, "sea", seaPoint);
        }

        /// <summary>First-person body: one pivot per segment at the proximal joint, mesh aligned to +Y.</summary>
        static void BuildHumanBody(Transform systems)
        {
            var src = Model(Art + "/Models/HumanBody.fbx", null, "~HumanBodySource");
            Remap(src, M);
            var root = Child(systems, "HumanBody").transform;
            var avatar = root.gameObject.AddComponent<HumanAvatar>();
            Transform Seg(string part, string name, bool foot = false) => Segment(root, src, part, name, foot);
            avatar.torso = Seg("Torso", "Torso");
            avatar.upperArmL = Seg("UpperArm", "UpperArmL");
            avatar.forearmL = Seg("Forearm", "ForearmL");
            avatar.handL = Seg("Hand", "HandL");
            avatar.upperArmR = Seg("UpperArm", "UpperArmR");
            avatar.forearmR = Seg("Forearm", "ForearmR");
            avatar.handR = Seg("Hand", "HandR");
            avatar.thighL = Seg("Thigh", "ThighL");
            avatar.shinL = Seg("Shin", "ShinL");
            avatar.footL = Seg("Foot", "FootL", true);
            avatar.thighR = Seg("Thigh", "ThighR");
            avatar.shinR = Seg("Shin", "ShinR");
            avatar.footR = Seg("Foot", "FootR", true);
            Object.DestroyImmediate(src);
            EditorUtility.SetDirty(avatar);
        }

        static Transform Segment(Transform parent, GameObject model, string part, string name, bool foot)
        {
            Transform srcT = FindDeep(model.transform, part);
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            if (srcT == null) { Debug.LogWarning("[SameShore] body part missing: " + part); return pivot; }
            var mf = srcT.GetComponent<MeshFilter>();
            var mr = srcT.GetComponent<MeshRenderer>();
            var vis = new GameObject("Mesh").transform;
            vis.SetParent(pivot, false);
            vis.gameObject.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var r = vis.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterials = mr.sharedMaterials;
            r.shadowCastingMode = ShadowCastingMode.On;
            // the part's own import rotation/scale relative to the model root
            Quaternion rot = Quaternion.Inverse(model.transform.rotation) * srcT.rotation;
            Vector3 ls = srcT.lossyScale, ms = model.transform.lossyScale;
            Vector3 scl = new Vector3(ls.x / ms.x, ls.y / ms.y, ls.z / ms.z);
            var m = Matrix4x4.TRS(Vector3.zero, rot, scl);
            Vector3 mn = Vector3.one * 1e9f, mx = Vector3.one * -1e9f;
            foreach (var v in mf.sharedMesh.vertices) { Vector3 w = m.MultiplyPoint3x4(v); mn = Vector3.Min(mn, w); mx = Vector3.Max(mx, w); }
            Vector3 ext = mx - mn;
            int k = ext.x >= ext.y && ext.x >= ext.z ? 0 : ext.y >= ext.z ? 1 : 2;
            Vector3 a = Vector3.zero;
            a[k] = Mathf.Abs(mx[k]) >= Mathf.Abs(mn[k]) ? 1 : -1;
            Quaternion align;
            if (foot)
            {
                // long axis forward (+Z); the axis that extends below the ankle points down (-Y)
                int dk = -1; float best = 0;
                for (int i = 0; i < 3; i++)
                {
                    if (i == k) continue;
                    float below = Mathf.Max(-mn[i], mx[i]);
                    if (below > best) { best = below; dk = i; }
                }
                Vector3 down = Vector3.zero;
                if (dk >= 0) down[dk] = -mn[dk] > mx[dk] ? 1 : -1; else down = Vector3.down;
                align = Quaternion.Inverse(Quaternion.LookRotation(a, -down));
            }
            else
            {
                int bk = -1; float best = -1;
                for (int i = 0; i < 3; i++) if (i != k && ext[i] > best) { best = ext[i]; bk = i; }
                Vector3 b = Vector3.zero;
                b[bk] = 1;
                align = Quaternion.Inverse(Quaternion.LookRotation(Vector3.Cross(b, a), a));
            }
            vis.localRotation = align * rot;
            vis.localScale = scl;
            Debug.Log($"[SameShore] body segment {name}: long axis {a}, extent {ext}");
            return pivot;
        }

        static void WireAvatar(HatchlingAvatar av, GameObject model)
        {
            av.flipperFL = FindDeep(model.transform, "FlipperFL");
            av.flipperFR = FindDeep(model.transform, "FlipperFR");
            av.flipperBL = FindDeep(model.transform, "FlipperBL");
            av.flipperBR = FindDeep(model.transform, "FlipperBR");
            av.head = FindDeep(model.transform, "Head");
            // Blender exports mirror X; if the "left" flipper ended up on +X, swap.
            if (av.flipperFL != null && av.flipperFR != null &&
                av.transform.InverseTransformPoint(av.flipperFL.position).x > av.transform.InverseTransformPoint(av.flipperFR.position).x)
            {
                (av.flipperFL, av.flipperFR) = (av.flipperFR, av.flipperFL);
                (av.flipperBL, av.flipperBR) = (av.flipperBR, av.flipperBL);
            }
            foreach (var r in model.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
            EditorUtility.SetDirty(av);
        }

        static Volume MakeVolume(string name, VolumeProfile profile, float weight)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_env, false);
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = 10;
            v.sharedProfile = profile;
            v.weight = weight;
            return v;
        }

        static VolumeProfile Profile(string name)
        {
            EnsureFolder(Gen + "/Volumes");
            string path = $"{Gen}/Volumes/{name}.asset";
            AssetDatabase.DeleteAsset(path);
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);
            return p;
        }

        static T Add<T>(VolumeProfile p) where T : VolumeComponent
        {
            var c = p.Add<T>(true);
            c.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(c, p);
            return c;
        }

        /// <summary>PDF VS1: "Dull view (reduced colour visibility)".</summary>
        static VolumeProfile HumanProfile()
        {
            var p = Profile("HumanView");
            // PDF VS1 "dull view": a little less colour than the hatchling sees, but a bright, pleasant holiday
            var ca = Add<ColorAdjustments>(p);
            ca.saturation.Override(-12f);
            ca.contrast.Override(6f);
            ca.postExposure.Override(0.15f);
            ca.colorFilter.Override(new Color(1.03f, 0.99f, 0.92f));   // warm holiday light on the pale sand
            var bloom = Add<Bloom>(p);
            bloom.intensity.Override(0.8f);      // the festoon glows at night; daylight stays below the threshold
            bloom.threshold.Override(1.1f);
            bloom.scatter.Override(0.68f);
            bloom.tint.Override(new Color(1f, 0.9f, 0.78f));
            var tm = Add<Tonemapping>(p);
            tm.mode.Override(TonemappingMode.Neutral);
            EditorUtility.SetDirty(p);
            return p;
        }

        /// <summary>PDF VS2: "Added hues (invisible to human eye)": richer colour shifted towards violet/UV.</summary>
        static VolumeProfile HatchlingProfile()
        {
            var p = Profile("HatchlingView");
            var ca = Add<ColorAdjustments>(p);
            ca.saturation.Override(12f);
            ca.contrast.Override(12f);
            ca.postExposure.Override(0.6f);
            ca.hueShift.Override(-5f);
            ca.colorFilter.Override(new Color(0.96f, 0.98f, 1.06f));
            // artificial lights become big glowing points for the hatchling
            var hb = Add<Bloom>(p);
            hb.intensity.Override(1.6f);         // from 8 cm, the bulbs are huge, swollen suns
            hb.threshold.Override(0.85f);
            hb.scatter.Override(0.8f);
            hb.tint.Override(new Color(1f, 0.86f, 0.7f));
            var mix = Add<ChannelMixer>(p);
            mix.blueOutRedIn.Override(6f);
            mix.blueOutBlueIn.Override(104f);
            mix.greenOutBlueIn.Override(5f);
            var sm = Add<SplitToning>(p);
            sm.shadows.Override(new Color(0.47f, 0.48f, 0.54f));
            sm.highlights.Override(new Color(0.55f, 0.62f, 0.56f));
            sm.balance.Override(-30f);
            var tm = Add<Tonemapping>(p);
            tm.mode.Override(TonemappingMode.Neutral);
            EditorUtility.SetDirty(p);
            return p;
        }

        static void AddToBuildSettings()
        {
            var guid = new GUID(AssetDatabase.AssetPathToGUID(TargetScene));
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != TargetScene && s.guid != guid).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(guid, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        static float Rand(float a, float b) => a + (float)Rng.NextDouble() * (b - a);
    }
}
