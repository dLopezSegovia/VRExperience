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
    public static class SameShoreSceneBuilder
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
            ConfigureImporters();
            CreateMaterials();
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

            _env = new GameObject("SameShore_Environment").transform;
            var systems = new GameObject("SameShore_Systems").transform;

            SetupRenderingAndSky();
            PrepareExistingObjects();
            Transform nest = BuildNest();
            BuildSea();
            var props = PlaceProps();
            BuildDecor();
            var footprints = BuildFootprints(systems);
            var gulls = BuildGulls();
            BuildHotel();
            var audio = BuildAudio(systems);
            WireSystems(systems, nest, props, footprints, gulls, audio);

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
            Texture hdr = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Skybox/qwantani_night_puresky_1k.hdr");
            var sky = SaveMat("SkyboxNight_SameShore", Shader.Find("Skybox/Panoramic"));
            sky.SetTexture("_MainTex", hdr);
            sky.SetFloat("_Exposure", 0.32f);
            sky.SetFloat("_Rotation", 200f);
            sky.SetColor("_Tint", new Color(0.36f, 0.4f, 0.52f));
            RenderSettings.skybox = sky;

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
            var moonQuad = Prim(PrimitiveType.Quad, "CrescentMoon", _env, new Vector3(cx, 0, zs) + moonDir * 900f,
                                Vector3.one * 55f, M["Moon"], false, Quaternion.LookRotation(moonDir) * Quaternion.Euler(0, 0, -25));
            moonQuad.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

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
            nest.position = L(0, 18f);
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
            var ocean = new GameObject("Ocean");
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

            var swash = new GameObject("ShoreSwash");
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
            var trashRoot = Child(_env, "Litter").transform;

            // chair (teammate's monobloc chair): an obstacle the hatchling must go around, or shelter under
            GameObject chair = Find("plastic_monobloc_chair_01_1k");
            Vector3 cp = L(-0.8f, 10.5f);
            chair.transform.position = cp;
            chair.transform.rotation = Quaternion.Euler(-90, 30, 0);
            props.chair = chair.transform;
            AddFeedback(chair);
            var chairRb = chair.GetComponent<Rigidbody>();
            chairRb.mass = 3.5f;
            chairRb.linearDamping = 0.6f;
            chairRb.angularDamping = 2.5f;
            chairRb.interpolation = RigidbodyInterpolation.Interpolate;
            chairRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            foreach (var mc in chair.GetComponentsInChildren<MeshCollider>()) mc.convex = true;
            GetOrAdd<BeachChairMarker>(chair);
            // where to leave it: beside the dunes, out of the hatchlings' way
            var safe = Child(_env, "ChairSafeZone").transform;
            safe.position = L(-5.6f, 13f);
            var ringDecal = Prim(PrimitiveType.Quad, "SafeZoneMark", safe, safe.position + Vector3.up * 0.01f, Vector3.one * 1.3f, M["GroundDecal"], false, Quaternion.Euler(90, 0, 0));
            string ringPath = Gen + "/Materials/SafeZoneRing.mat";
            AssetDatabase.DeleteAsset(ringPath);
            var ringMat = new Material(M["GroundDecal"]);
            ringMat.SetTexture("_BaseMap", SavePng("SafeZoneRing", ProceduralArt.Ring(), true, true));
            ringMat.SetColor("_BaseColor", new Color(0.85f, 0.8f, 0.6f, 0.35f));
            AssetDatabase.CreateAsset(ringMat, ringPath);
            ringDecal.GetComponent<MeshRenderer>().sharedMaterial = ringMat;
            props.safeZone = safe;

            // flashlight: lying lit on the sand, its beam across the hatchlings' path
            GameObject fl = Find("Flashlight");
            Vector3 fp = L(3.3f, 8.2f, 0.035f);
            Vector3 aim = L(-1.6f, 12.8f) - fp;
            aim.y = 0;
            fl.transform.SetPositionAndRotation(fp, Quaternion.LookRotation(aim.normalized + Vector3.up * 0.06f));
            props.flashlight = fl.transform;
            Transform beamT = fl.transform.Find("Beam");
            var beam = beamT.GetComponent<Light>();
            beamT.localRotation = Quaternion.identity;
            beam.type = LightType.Spot;
            beam.range = 11f;
            beam.spotAngle = 58f;
            beam.innerSpotAngle = 30f;
            beam.intensity = 7f;
            beam.color = new Color(1f, 0.87f, 0.72f);
            beam.shadows = LightShadows.Soft;
            Set(beamT.GetComponent<LightSource>(), "strength", 2.2f);
            Set(beamT.GetComponent<LightSource>(), "artificial", true);
            Transform cyl = fl.transform.Find("Cylinder");
            if (cyl != null) cyl.GetComponent<MeshRenderer>().enabled = false;
            var torch = Model($"{PH}/small_plastic_torch/small_plastic_torch.fbx", fl.transform, "TorchModel");
            if (torch != null)
            {
                AlignLongAxis(torch, fl.transform, beamT.localPosition.z, 0.2f);
                Remap(torch, new Dictionary<string, Material>(), M["small_plastic_torch"]);
            }
            Set(fl.GetComponent<Flashlight>(), "startOn", true);
            AddFeedback(fl);
            // held like a real torch: beam along the controller's pointing direction
            var grip = GetOrAdd<GripAlignment>(fl);
            Set(grip, "grabbable", fl.GetComponent<Grabbable>());
            GetOrAdd<LightConeVisual>(beamT.gameObject);
            var click = GetOrAdd<AudioSource>(fl);
            click.clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Audio/flashlight_click.wav");
            click.playOnAwake = false;
            click.spatialBlend = 1f;
            click.minDistance = 0.5f;
            click.maxDistance = 8f;
            Set(fl.GetComponent<Flashlight>(), "clickSound", click);

            // hole (teammate's Hole object becomes the real, fillable hole)
            GameObject hole = Find("Hole");
            hole.transform.position = L(0.9f, 6.0f);
            hole.transform.rotation = Quaternion.identity;
            hole.transform.localScale = Vector3.one;
            var mr = hole.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
            var capsule = hole.GetComponent<CapsuleCollider>();
            capsule.isTrigger = true;
            capsule.radius = 0.5f;
            capsule.height = 0.5f;
            capsule.direction = 1;
            capsule.center = new Vector3(0, -0.1f, 0);
            var zone = hole.GetComponent<TurtleZone>();
            zone.Type = TurtleZoneType.Trap;
            zone.SpeedMultiplier = 0.2f;
            zone.FirstEnterMessage = "You fell into a hole dug by people.\nKeep pushing: crawl or burst to climb out.";
            EditorUtility.SetDirty(zone);
            var sandHole = GetOrAdd<SandHole>(hole);
            Set(sandHole, "terrain", TerrainAt(hole.transform.position));
            Set(sandHole, "trapZone", zone);
            props.hole = hole.transform;
            props.sandHole = sandHole;

            var castle = Model(Art + "/Models/Sandcastle.fbx", _env, "Sandcastle");
            castle.transform.position = L(2.0f, 5.5f, -0.02f);
            castle.transform.rotation = Quaternion.Euler(0, 20, 0);
            Remap(castle, new Dictionary<string, Material>(), M["Sandcastle"]);
            AddMeshColliders(castle, true);

            // buckets of sand next to the hole
            foreach (var (name, pos) in new[] { ("Wooden_Bucket", L(2.4f, 6.8f)), ("Wooden_Bucket (1)", L(-1.9f, 3.4f)) })
            {
                GameObject b = Find(name);
                if (b == null) continue;
                b.transform.SetPositionAndRotation(pos + Vector3.up * 0.01f, Quaternion.Euler(0, Rand(0, 360), 0));
                var fill = Prim(PrimitiveType.Cylinder, "SandFill", b.transform, b.transform.position + Vector3.up * 0.3f,
                                new Vector3(0.27f, 0.005f, 0.27f), M["SandFill"]);
                fill.transform.localPosition = new Vector3(0, 0.3f, 0);
                AddFeedback(b);
                var sb = GetOrAdd<SandBucket>(b);
                Set(sb, "sandFill", fill.transform);
                Set(sb, "mouthHeight", 0.34f);
                Set(sb, "pourAngle", 80f);
                props.buckets.Add(b);
            }

            // litter along the nest -> sea corridor
            Trash(trashRoot, "PlasticBag", Art + "/Models/PlasticBag.fbx", L(-0.4f, 14.2f, 0.01f), 40, 0.03f, "bag", true);
            Trash(trashRoot, "PlasticBag", Art + "/Models/PlasticBag.fbx", L(1.7f, 2.9f, 0.01f), -25, 0.03f, "bag", true);
            Trash(trashRoot, "Bottle_PET", Art + "/Models/Bottle_PET.fbx", L(0.6f, 12.6f, 0.034f), 70, 0.25f, "bottle", false, true);
            Trash(trashRoot, "Bottle_PET", Art + "/Models/Bottle_PET.fbx", L(-1.3f, 8.3f, 0.034f), -20, 0.25f, "bottle", false, true);
            Trash(trashRoot, "Bottle_PET", Art + "/Models/Bottle_PET.fbx", L(-0.2f, 4.6f, 0.034f), 110, 0.25f, "bottle", false, true);
            Trash(trashRoot, "Bottle_PET_Crushed", Art + "/Models/Bottle_PET_Crushed.fbx", L(2.0f, 10.2f, 0.02f), 15, 0.12f, "bottle", false, true);
            Trash(trashRoot, "Bottle_Glass", Art + "/Models/Bottle_Glass.fbx", L(1.3f, 15.4f, 0.032f), -60, 0.4f, "glass bottle", false, true);
            Trash(trashRoot, "Can", $"{PH}/can_rusted/can_rusted.fbx", L(-1.6f, 11.6f, 0.0f), 0, 0.05f, "can", false, false, 0.12f);
            Trash(trashRoot, "Can", $"{PH}/can_rusted/can_rusted.fbx", L(2.8f, 13.3f, 0.0f), 0, 0.05f, "can", false, false, 0.12f);
            Trash(trashRoot, "FoodContainer", $"{PH}/plastic_container/plastic_container.fbx", L(0.2f, 9.2f, 0.0f), 30, 0.06f, "container", false, false, 0.19f);

            // litter bin at the side of the beach
            Vector3 bp = L(-5.2f, 9f);
            var bin = Prim(PrimitiveType.Cylinder, "LitterBin", _env, bp + Vector3.up * 0.45f, new Vector3(0.5f, 0.45f, 0.5f), M["Bin"], true);
            Object.DestroyImmediate(bin.GetComponent<Collider>());
            Prim(PrimitiveType.Cylinder, "Rim", _env, bp + Vector3.up * 0.9f, new Vector3(0.54f, 0.02f, 0.54f), M["BinLid"]);
            // hollow-ish: four walls so things can drop inside
            for (int i = 0; i < 4; i++)
            {
                var wall = new GameObject("Wall").AddComponent<BoxCollider>();
                wall.transform.SetParent(bin.transform, false);
                wall.transform.localRotation = Quaternion.Euler(0, i * 90, 0);
                wall.transform.localPosition = wall.transform.localRotation * new Vector3(0, 0, 0.48f);
                wall.size = new Vector3(1f, 2f, 0.06f);
            }
            var floor = new GameObject("Floor").AddComponent<BoxCollider>();
            floor.transform.SetParent(bin.transform, false);
            floor.transform.localPosition = new Vector3(0, -0.95f, 0);
            floor.size = new Vector3(1f, 0.1f, 1f);
            var binTrigger = new GameObject("BinTrigger").AddComponent<BoxCollider>();
            binTrigger.transform.SetParent(bin.transform, false);
            binTrigger.size = new Vector3(0.9f, 1.8f, 0.9f);
            binTrigger.isTrigger = true;
            binTrigger.gameObject.AddComponent<TrashBin>();
            props.bin = binTrigger.transform;
            var label = new GameObject("BinLabel");
            label.transform.SetParent(_env, false);
            label.transform.position = bp + Vector3.up * 1.15f;
            Vector3 toPath = L(0, 9f) - bp;
            toPath.y = 0;
            label.transform.rotation = Quaternion.LookRotation(-toPath.normalized, Vector3.up);
            var lt = label.AddComponent<TextMeshPro>();
            lt.rectTransform.sizeDelta = new Vector2(1.2f, 0.4f);
            lt.fontSize = 1.6f;
            lt.alignment = TextAlignmentOptions.Center;
            lt.color = new Color(0.75f, 0.95f, 0.8f);
            lt.text = "<b>PLASTIC & LITTER</b>";
            return props;
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
            (float dx, float d, float s)[] small = { (-4.2f, 6f, 0.7f), (5.6f, 11f, 0.9f), (-3.6f, 15f, 0.6f), (6.5f, 3.5f, 0.5f), (-2.8f, 0.9f, 0.8f), (3.8f, 16.5f, 0.45f), (2.6f, 13.8f, 0.35f) };
            int k = 0;
            foreach (var r in small) Rock(decor, $"{PH}/sand_rocks_small_01/sand_rocks_small_01.fbx", k++, L(r.dx, r.d), r.s, M["sand_rocks_small_01"]);
            (float dx, float d, float s)[] big = { (-10f, 0.5f, 3.2f), (12.5f, 0f, 3.6f), (-8.5f, -2.5f, 2.6f), (15f, -3f, 3f) };
            foreach (var r in big) Rock(decor, $"{PH}/coast_rocks_01/coast_rocks_01.fbx", k++, L(r.dx, r.d), r.s, M["coast_rocks_01"]);

            // driftwood branches
            foreach (var (dx, d, yaw) in new[] { (3.6f, 15.5f, 40f), (-2.9f, 2.3f, -70f), (6.8f, 7.5f, 10f) })
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
            // older trails left by people earlier in the evening
            Trail(go.transform, quad, new[] { L(-6f, 17f), L(-2f, 11f), L(2.5f, 5f), L(5.5f, 0.6f) });
            Trail(go.transform, quad, new[] { L(9f, 15f), L(5f, 12.5f), L(0.2f, 10.8f) });
            Trail(go.transform, quad, new[] { L(-1f, 0.8f), L(3f, 1.6f), L(8f, 1.2f) });
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
                    FootprintSystem.CreatePrint(parent, p, Normal(p.x, p.z), dir, left, M["Footprint"], new Vector2(0.11f, 0.27f), 0.6f, quad);
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
                channels.Add(new AmbientAudio.Channel { name = "WavesClose", source = Src("WavesHatchling", C("ocean_waves_close_loop"), L(dx, -2f, 0.5f), 0.7f, 9f, 90f), humanVolume = 0f, hatchlingVolume = 1f });
            }
            channels.Add(new AmbientAudio.Channel { name = "Wind", source = Src("Wind", C("wind_loop"), L(0, 8, 2), 0f, 1, 10), humanVolume = 0.32f, hatchlingVolume = 0.16f, hatchlingLowpass = 2500f });
            channels.Add(new AmbientAudio.Channel { name = "HotelMusic", source = Src("HotelMusic", C("hotel_music_loop"), new Vector3(-14f, 9f, -40f), 1f, 14f, 140f), humanVolume = 0.5f, hatchlingVolume = 0.35f, humanLowpass = 2200f, hatchlingLowpass = 420f });
            channels.Add(new AmbientAudio.Channel { name = "Voices", source = Src("PeopleVoices", C("voices_muffled_loop"), new Vector3(-6f, 6f, -32f), 1f, 10f, 120f), humanVolume = 0.25f, hatchlingVolume = 0.55f, humanLowpass = 2500f, hatchlingLowpass = 520f });
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
        static void WireSystems(Transform systems, Transform nest, Props props, FootprintSystem footprints, Seagull[] gulls, AmbientAudio audio)
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
            Vector3 start = L(1f, 2.6f);
            rigRoot.position = start;
            rigRoot.rotation = Quaternion.identity;

            var humanStart = Child(_env, "HumanStart").transform;
            humanStart.SetPositionAndRotation(start, Quaternion.identity);

            var human = systems.gameObject.AddComponent<HumanController>();
            Set(human, "locomotion", loco);
            Set(human, "footprints", footprints);
            GameObject hotspot = Find("Teleport Hotspot");
            Set(human, "disableObjects", hotspot != null ? new Object[] { hotspot } : new Object[0]);

            // ---- hatchling body (teammate's TurtleBody + controller)
            GameObject body = Find("TurtleBody");
            body.transform.SetPositionAndRotation(Find("TurtleSpawn").transform.position, Quaternion.identity);
            var tcc = body.GetComponent<CharacterController>();
            tcc.radius = 0.035f;
            tcc.height = 0.07f;
            tcc.center = new Vector3(0, 0.037f, 0);
            tcc.stepOffset = 0.02f;
            tcc.slopeLimit = 40f;
            tcc.skinWidth = 0.004f;
            tcc.minMoveDistance = 0f;
            var turtle = body.GetComponent<TurtleController>();
            foreach (Transform old in body.transform.Cast<Transform>().ToArray())
                if (old.name == "HatchlingModel") Object.DestroyImmediate(old.gameObject);
            var avatarRoot = Child(body.transform, "HatchlingModel");
            // the eyes (camera) sit above the neck: shell below/behind, front flippers at the lower edge of the view
            avatarRoot.transform.localPosition = new Vector3(0, 0, 0.004f);
            avatarRoot.transform.localScale = Vector3.one * 1.25f;
            var model = Model(Art + "/Models/Hatchling.fbx", avatarRoot.transform, "Mesh");
            Remap(model, M);
            OrientByChild(model, "Head");
            var avatar = avatarRoot.AddComponent<HatchlingAvatar>();
            WireAvatar(avatar, model);
            avatar.controller = turtle;
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
            Set(turtle, "strokeDistance", 0.13f);   // ~0.17 m/s average: about 1:0.12 of the human walk, x20 perceived
            Set(turtle, "strokeDuration", 0.6f);
            Set(turtle, "strokeThreshold", 0.55f);
            Set(turtle, "turnSpeed", 25f);
            Set(turtle, "lightSteer", 0.55f);
            Set(turtle, "maxPushMass", 1f);
            Set(turtle, "pushStrength", 0.3f);
            Set(turtle, "pushGain", 25f);
            Set(turtle, "zoneProbeRadius", 0.05f);

            // ---- nest mates
            var sibs = new List<Object>();
            for (int i = 0; i < 4; i++)
            {
                float a = i / 4f * Mathf.PI * 2 + 0.6f;
                Vector3 p = G(nest.position.x + Mathf.Cos(a) * 0.28f, nest.position.z + Mathf.Sin(a) * 0.28f);
                var sib = Child(nest, "Sibling" + i);
                sib.transform.SetPositionAndRotation(p, Quaternion.Euler(0, Rand(-50, 50), 0));
                var m = Model(Art + "/Models/Hatchling.fbx", sib.transform, "Mesh");
                Remap(m, M);
                OrientByChild(m, "Head");
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
            GameObject interactionRig = Find("[BuildingBlock] OVRComprehensiveInteractionRig");
            Set(switcher, "humanOnlyObjects", new Object[] { Find("HumanSeaBarrier"), interactionRig });

            // ---- perception (post-processing volumes)
            var humanVol = MakeVolume("Volume_HumanView", HumanProfile(), 1f);
            var hatchVol = MakeVolume("Volume_HatchlingView", HatchlingProfile(), 0f);
            var perception = systems.gameObject.AddComponent<PerceptionController>();
            Set(perception, "humanVolume", humanVol);
            Set(perception, "hatchlingVolume", hatchVol);

            // ---- beach state, gulls, fader, pointers, director
            var beach = systems.gameObject.AddComponent<BeachStateTracker>();
            var seaPoint = Child(_env, "SeaPoint").transform;
            seaPoint.position = L(0, 0);
            Set(beach, "nest", nest);
            Set(beach, "seaPoint", seaPoint);
            Set(beach, "flashlight", props.flashlight.GetComponent<Flashlight>());
            Set(beach, "hole", props.sandHole);
            Set(beach, "chair", props.chair);

            var hunt = systems.gameObject.AddComponent<GullHunt>();
            Set(hunt, "hunter", gulls[gulls.Length - 1]);
            Set(hunt, "turtle", turtle);
            Set(hunt, "sea", seaPoint);

            var journey = systems.gameObject.AddComponent<TurtleJourney>();
            Set(journey, "turtle", turtle);
            Set(journey, "nest", nest);
            Set(journey, "sea", seaPoint);
            Set(journey, "gull", hunt);

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

            var fader = systems.gameObject.AddComponent<ScreenFader>();
            // Editor without a headset: mouse/keyboard testing (inactive on Quest / with Link)
            systems.gameObject.AddComponent<DesktopTestingMode>();
            systems.gameObject.AddComponent<DesktopInteractor>();
            foreach (var hand in new[] { Hand.Left, Hand.Right })
            {
                var p = Child(systems, "Pointer" + hand).AddComponent<VRPointer>();
                p.hand = hand;
            }

            var director = systems.gameObject.AddComponent<ExperienceDirector>();
            Set(director, "phaseSwitcher", switcher);
            Set(director, "turtle", turtle);
            Set(director, "human", human);
            Set(director, "fader", fader);
            Set(director, "perception", perception);
            Set(director, "ambient", audio);
            Set(director, "beach", beach);
            Set(director, "gull", hunt);
            Set(director, "journey", journey);
            Set(director, "objectives", objectives);
            Set(director, "finalWave", finalWave);
            Set(director, "nest", nestCtl);
            Set(director, "turtleFX", fx);
            Set(director, "humanStart", humanStart);
            Set(director, "menuSpot", humanStart);
            Set(director, "sea", seaPoint);
            Set(director, "chairSafeZone", props.safeZone);
            Set(director, "litterBin", props.bin);
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
            var ca = Add<ColorAdjustments>(p);
            ca.saturation.Override(-38f);
            ca.contrast.Override(8f);
            ca.postExposure.Override(0.35f);
            ca.colorFilter.Override(new Color(0.93f, 0.97f, 1.04f));
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
