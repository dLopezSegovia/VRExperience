using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static SameShore.EditorTools.BuilderUtil;

namespace SameShore.EditorTools
{
    /// <summary>
    /// Storyboard v2 content: the human's beach camp and activities, the turtle route and the giant.
    /// Layout in (dx, d): dx = metres across the beach (+ = right when facing the sea), d = metres up the beach
    /// from the shoreline. The nest is inland, the sea in front; the camp is near the shore beside the route,
    /// and every hazard the human creates lands on the route naturally (see comments).
    /// </summary>
    public static partial class SameShoreSceneBuilder
    {
        // ---- layout (storyboard v3: every consequence of the human's day lies on the hatchling's way)
        // A natural corridor (low dune berms with grass, rocks and driftwood, see BuildCorridor) runs from the nest
        // to the sea; the camp sits on its right edge and every activity leaves its trace inside it:
        //   d 17.5 nest -> d 16..10.6 the light string zigzags across the corridor (posts left/right)
        //   d 12.5..10  cans that missed the bin (thrown from the chair across the corridor)
        //   d 9.4       the chair, the speaker (music zone)  ·  d 7.7 the sandcastle + the dug hole side by side
        //   d 4.4       the plastic bag (blown from the towel)  ·  d 2.5 the photo  ·  d 0 the sea
        static readonly Vector2 NestXY = new Vector2(-0.5f, 17.5f);
        static readonly Vector2[] RouteXY =
        {
            new Vector2(-0.5f, 17.5f), new Vector2(0.2f, 15.6f), new Vector2(0.3f, 13.6f), new Vector2(0.2f, 11.6f),
            new Vector2(0.55f, 9.8f), new Vector2(0.42f, 8.3f), new Vector2(0.4f, 7.0f), new Vector2(0.6f, 5.8f),
            new Vector2(0.75f, 4.4f), new Vector2(1.0f, 2.6f), new Vector2(0.9f, 0.3f)
        };
        static readonly Vector2 SeatXY = new Vector2(2.3f, 9.4f);
        static readonly Vector2 UmbrellaXY = new Vector2(3.0f, 8.4f);
        static readonly Vector2 TowelXY = new Vector2(2.75f, 7.1f);
        static readonly Vector2 BinXY = new Vector2(-1.2f, 12.5f);        // 4.7 m from the chair, across the corridor
        static readonly Vector2 PlotXY = new Vector2(-0.2f, 7.75f);       // sandcastle, in the middle of the way
        static readonly Vector2 DigXY = new Vector2(1.05f, 6.95f);        // the hole, beside the castle (gap ~0.4 m)
        static readonly Vector2 BagLandXY = new Vector2(0.75f, 4.4f);     // wet sand, right on the way down
        static readonly Vector2 FireworksXY = new Vector2(6.3f, 3.4f);    // roped-off zone by the water, outside the corridor
        static readonly Vector2[] PostXY = { new Vector2(-1.45f, 10.6f), new Vector2(1.95f, 12.6f), new Vector2(-1.4f, 14.4f), new Vector2(1.3f, 16.2f) };
        static readonly Vector2 PhotoTurtleXY = new Vector2(1.0f, 2.6f);
        // corridor crests (dx, d), nest -> sea
        static readonly Vector2[] LeftEdgeXY =
        {
            new Vector2(-2.4f, 19.6f), new Vector2(-2.2f, 17.2f), new Vector2(-2.3f, 14.6f), new Vector2(-2.3f, 12.4f), new Vector2(-2.0f, 10.4f),
            new Vector2(-1.55f, 8.6f), new Vector2(-1.5f, 6.6f), new Vector2(-1.4f, 4.8f), new Vector2(-1.5f, 3.0f), new Vector2(-1.8f, 1.3f)
        };
        static readonly Vector2[] RightEdgeXY =
        {
            new Vector2(1.9f, 19.6f), new Vector2(2.6f, 17.4f), new Vector2(3.0f, 14.8f), new Vector2(3.4f, 12.6f), new Vector2(4.1f, 10.6f),
            new Vector2(4.3f, 8.6f), new Vector2(4.0f, 6.6f), new Vector2(3.2f, 5.0f), new Vector2(2.9f, 3.2f), new Vector2(3.1f, 1.3f)
        };
        static readonly Vector2[] BackEdgeXY = { new Vector2(-2.4f, 19.6f), new Vector2(-0.4f, 20.1f), new Vector2(1.9f, 19.6f) };

        static Transform _camp;

        class Story
        {
            public Transform seat, towel, sea, photoTurtleStart;
            public BeachDrink drink;
            public MusicPlayer music;
            public CanTossGame cans;
            public SandcastleSite castle;
            public VRGrabbable shovel;
            public Sandwich sandwich;
            public WindBag bag;
            public FireworksShow fireworks;
            public LightGarland garland;
            public PhoneCamera phone;
            public PhotoHatchling photoTurtle;
            public GiantHuman giant;
            public Transform[] route;
            public SandHole hole;
        }

        // ================================================================== materials
        static void CreateStoryMaterials()
        {
            Texture2D Stripes(string name, Color[] cols, int w, int h, bool vertical)
            {
                var t = new Texture2D(w, h, TextureFormat.RGBA32, true);
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = vertical ? x : y;
                    int n = (vertical ? w : h) / cols.Length;
                    Color c = cols[(i / Mathf.Max(1, n)) % cols.Length];
                    float noise = Mathf.PerlinNoise(x * 0.31f, y * 0.29f) * 0.06f - 0.03f;
                    t.SetPixel(x, y, new Color(c.r + noise, c.g + noise, c.b + noise, 1));
                }
                t.Apply();
                return SavePng(name, t, true, false, TextureWrapMode.Repeat);
            }
            Color coral = new Color(0.93f, 0.42f, 0.36f), cream = new Color(0.97f, 0.93f, 0.84f), teal = new Color(0.12f, 0.55f, 0.6f),
                  navy = new Color(0.14f, 0.24f, 0.45f), sun = new Color(0.98f, 0.78f, 0.3f);
            M["Towel"] = LitMat("Towel", Color.white, 0.05f, Stripes("TowelStripes", new[] { coral, cream, teal, cream, coral, sun, cream }, 8, 256, false));
            M["Canopy"] = LitMat("UmbrellaCanopy", Color.white, 0.25f, Stripes("UmbrellaStripes", new[] { coral, cream }, 16, 4, true));
            M["Canopy"].SetFloat("_Cull", 0);
            M["ChairFabric"] = LitMat("ChairFabric", Color.white, 0.1f, Stripes("ChairStripes", new[] { navy, cream, navy, cream, teal }, 64, 8, true));
            M["ChairFabric"].SetFloat("_Cull", 0);
            M["WoodLight"] = LitMat("WoodLight", new Color(0.72f, 0.55f, 0.38f), 0.35f, Tex($"{PH}/dead_quiver_branch_01/dead_quiver_branch_01_diff_1k.jpg"));
            M["MetalWhite"] = LitMat("MetalWhite", new Color(0.9f, 0.9f, 0.88f), 0.6f, null, null, null, 0.6f);
            M["CoolerBody"] = LitMat("CoolerBody", new Color(0.32f, 0.62f, 0.86f), 0.55f);
            M["CoolerLid"] = LitMat("CoolerLid", new Color(0.95f, 0.95f, 0.93f), 0.5f);
            M["CanRed"] = LitMat("CanRed", new Color(0.85f, 0.1f, 0.12f), 0.75f, null, null, null, 0.7f);
            M["CanBlue"] = LitMat("CanBlue", new Color(0.1f, 0.35f, 0.85f), 0.75f, null, null, null, 0.7f);
            M["CanLime"] = LitMat("CanLime", new Color(0.45f, 0.8f, 0.2f), 0.75f, null, null, null, 0.7f);
            M["CanTop"] = LitMat("CanTop", new Color(0.82f, 0.83f, 0.85f), 0.85f, null, null, null, 1f);
            M["CupPaper"] = LitMat("CupPaper", new Color(0.93f, 0.89f, 0.8f), 0.2f);
            M["CupLid"] = LitMat("CupLid", new Color(0.18f, 0.12f, 0.1f), 0.5f);
            M["Black"] = LitMat("BlackPlastic", new Color(0.04f, 0.04f, 0.045f), 0.7f);
            var lens = LitMat("SunglassLens", new Color(0.05f, 0.08f, 0.1f, 0.85f), 0.95f);
            MakeTransparent(lens, true);
            M["Lens"] = lens;
            M["Speaker"] = LitMat("SpeakerBody", new Color(0.1f, 0.42f, 0.46f), 0.45f);
            M["Grille"] = LitMat("SpeakerGrille", new Color(0.08f, 0.08f, 0.09f), 0.2f);
            var play = LitMat("ButtonGreen", new Color(0.2f, 0.75f, 0.35f), 0.5f);
            Emission(play, new Color(0.05f, 0.25f, 0.08f));
            M["ButtonGreen"] = play;
            M["ButtonWhite"] = LitMat("ButtonWhite", new Color(0.92f, 0.92f, 0.9f), 0.5f);
            var red = LitMat("ButtonRed", new Color(0.85f, 0.08f, 0.06f), 0.6f);
            Emission(red, new Color(0.4f, 0.02f, 0.01f));
            M["ButtonRed"] = red;
            var led = LitMat("Led", new Color(0.2f, 0.05f, 0.05f), 0.8f);
            Emission(led, Color.black);
            M["Led"] = led;
            M["Straw"] = LitMat("BeachBagStraw", new Color(0.82f, 0.68f, 0.45f), 0.15f, Stripes("StrawWeave", new[] { new Color(0.85f, 0.71f, 0.47f), new Color(0.74f, 0.6f, 0.38f) }, 32, 32, false));
            M["Bread"] = LitMat("Bread", new Color(0.86f, 0.68f, 0.42f), 0.15f);
            M["Filling"] = LitMat("SandwichFilling", new Color(0.45f, 0.7f, 0.25f), 0.3f);
            M["Ham"] = LitMat("SandwichHam", new Color(0.88f, 0.5f, 0.52f), 0.35f);
            M["PlasticRed"] = LitMat("PlasticRed", new Color(0.9f, 0.18f, 0.12f), 0.55f);
            M["PlasticYellow"] = LitMat("PlasticYellow", new Color(0.98f, 0.8f, 0.15f), 0.55f);
            M["BinGreen"] = LitMat("BinGreen", new Color(0.12f, 0.36f, 0.22f), 0.45f, null, null, null, 0.5f);
            M["BinGreen"].SetFloat("_Cull", 0);
            M["Cardboard"] = LitMat("FireworkBox", new Color(0.72f, 0.12f, 0.1f), 0.25f);
            M["Scorch"] = LitMat("Scorched", new Color(0.05f, 0.045f, 0.04f), 0.1f);
            M["Phone"] = LitMat("PhoneBody", new Color(0.12f, 0.13f, 0.16f), 0.8f, null, null, null, 0.4f);
            var screen = SaveMat("PhoneScreen", Shader.Find("Universal Render Pipeline/Unlit"));
            screen.SetColor("_BaseColor", new Color(0.04f, 0.05f, 0.07f));
            M["Screen"] = screen;
            M["Cable"] = LitMat("LightCable", new Color(0.05f, 0.05f, 0.05f), 0.4f);
            var bulbOff = LitMat("BulbOff", new Color(0.85f, 0.8f, 0.7f, 0.7f), 0.9f);
            MakeTransparent(bulbOff);
            M["BulbOff"] = bulbOff;
            var bulbOn = LitMat("BulbOn", new Color(1f, 0.82f, 0.55f), 0.9f);
            Emission(bulbOn, new Color(1f, 0.62f, 0.3f) * 11f);   // well above the bloom threshold: a real glow at night
            M["BulbOn"] = bulbOn;
            var hookGlow = SaveMat("HookMarker", Shader.Find("SameShore/SkyBillboard"));
            hookGlow.SetColor("_Color", new Color(1f, 0.85f, 0.45f, 1f));
            M["HookMarker"] = hookGlow;
            M["Hair"] = LitMat("Avatar_Hair", new Color(0.12f, 0.08f, 0.05f), 0.3f);

            // fireworks: soft additive dot
            var dot = new Texture2D(64, 64, TextureFormat.RGBA32, true);
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float r = new Vector2(x - 31.5f, y - 31.5f).magnitude / 32f;
                float a = Mathf.Clamp01(1 - r);
                a = a * a * (3 - 2 * a);
                dot.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            dot.Apply();
            var dotTex = SavePng("SoftDot", dot, true, true);
            var fw = SaveMat("FireworkParticle", Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            fw.SetTexture("_BaseMap", dotTex);
            fw.SetColor("_BaseColor", Color.white);
            MakeAdditive(fw);
            M["Firework"] = fw;
            var refl = SaveMat("FireworkReflection", Shader.Find("Universal Render Pipeline/Unlit"));
            refl.SetTexture("_BaseMap", dotTex);
            refl.SetColor("_BaseColor", new Color(0, 0, 0, 0));
            MakeAdditive(refl);
            M["FireworkReflection"] = refl;
            // festoon glow: a warm halo around each bulb and a pool of light on the sand under each span
            var halo = SaveMat("BulbHalo", Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            halo.SetTexture("_BaseMap", dotTex);
            halo.SetColor("_BaseColor", new Color(1.6f, 0.85f, 0.35f, 1f));
            MakeAdditive(halo);
            M["BulbHalo"] = halo;
            var pool = SaveMat("LightPool", Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            pool.SetTexture("_BaseMap", dotTex);
            pool.SetColor("_BaseColor", new Color(0.32f, 0.17f, 0.06f, 1f));
            MakeAdditive(pool);
            pool.renderQueue = (int)RenderQueue.Transparent - 50;
            M["LightPool"] = pool;

            // sky for the day cycle
            var sky = SaveMat("SkyCycle_SameShore", Shader.Find("SameShore/SkyCycle"));
            sky.SetTexture("_NightTex", AssetDatabase.LoadAssetAtPath<Texture>("Assets/Skybox/qwantani_night_puresky_1k.hdr"));
            M["Sky"] = sky;
            AssetDatabase.SaveAssets();
        }

        static void MakeAdditive(Material m)
        {
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", 2);
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0);
            m.SetFloat("_Cull", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_BLENDMODE_ADD");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent + 10;
            EditorUtility.SetDirty(m);
        }

        // ================================================================== helpers
        static GameObject MeshObj(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos, Quaternion rot, bool shadows = true)
            => MeshObj(name, parent, mesh, new[] { mat }, pos, rot, shadows);

        static GameObject MeshObj(string name, Transform parent, Mesh mesh, Material[] mats, Vector3 pos, Quaternion rot, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        static GameObject LocalMesh(string name, Transform parent, Mesh mesh, Material mat, Vector3 localPos, Quaternion localRot, Vector3? scale = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = scale ?? Vector3.one;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            return go;
        }

        static readonly Dictionary<string, Mesh> _meshes = new Dictionary<string, Mesh>();
        static Mesh Saved(string name, System.Func<Mesh> make)
        {
            if (_meshes.TryGetValue(name, out var m) && m != null) return m;
            m = SaveMesh("Story_" + name, make());
            _meshes[name] = m;
            return m;
        }

        static VRGrabbable MakeGrab(GameObject go, float mass, string label, bool throwable = true)
        {
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.3f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var g = go.GetComponent<VRGrabbable>();
            if (g == null) g = go.AddComponent<VRGrabbable>();
            g.displayName = label;
            g.throwable = throwable;
            return g;
        }

        static Material MatCopy(string name, Material src)
        {
            string path = $"{Gen}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(src); AssetDatabase.CreateAsset(m, path); }
            else m.CopyPropertiesFromMaterial(src);
            return m;
        }

        static PhysicsMaterial CanPhysics()
        {
            string path = $"{Gen}/Materials/CanBounce.physicMaterial";
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (pm != null) return pm;
            pm = new PhysicsMaterial("CanBounce") { bounciness = 0.25f, dynamicFriction = 0.6f, staticFriction = 0.7f };
            AssetDatabase.CreateAsset(pm, path);
            return pm;
        }

        static Quaternion Facing(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            d.y = 0;
            return Quaternion.LookRotation(d.normalized, Vector3.up);
        }

        static Vector3 LX(Vector2 xy, float lift = 0f) => L(xy.x, xy.y, lift);

        // ================================================================== meshes
        static Mesh CanMesh() => Saved("Can", () =>
        {
            float r = 0.033f, h = 0.122f;
            var body = MeshKit.Lathe("b", new[] { new Vector2(r * 0.86f, 0.004f), new Vector2(r, 0.012f), new Vector2(r, h - 0.014f), new Vector2(r * 0.84f, h - 0.004f) }, 20, false, false);
            var ends = MeshKit.Lathe("e", new[] { new Vector2(0.0001f, 0f), new Vector2(r * 0.8f, 0f), new Vector2(r * 0.86f, 0.004f) }, 20, false, false);
            var top = MeshKit.Lathe("t", new[] { new Vector2(r * 0.84f, h - 0.004f), new Vector2(r * 0.8f, h), new Vector2(0.0001f, h - 0.003f) }, 20, false, false);
            return MeshKit.Combine("Can", new List<(Mesh, Matrix4x4, int)> { (body, Matrix4x4.identity, 0), (ends, Matrix4x4.identity, 1), (top, Matrix4x4.identity, 1) }, 2);
        });

        static Mesh CupMesh() => Saved("CoffeeCup", () =>
        {
            var cup = MeshKit.Lathe("c", new[] { new Vector2(0.026f, 0f), new Vector2(0.036f, 0.105f) }, 20, true, false);
            var lid = MeshKit.Lathe("l", new[] { new Vector2(0.039f, 0.1f), new Vector2(0.039f, 0.108f), new Vector2(0.033f, 0.115f), new Vector2(0.001f, 0.116f) }, 20, true, false);
            return MeshKit.Combine("CoffeeCup", new List<(Mesh, Matrix4x4, int)> { (cup, Matrix4x4.identity, 0), (lid, Matrix4x4.identity, 1) }, 2);
        });

        static Mesh PailMesh() => Saved("Pail", () =>
        {
            // open bucket: outer wall up, rim, inner wall down, inner floor
            var wall = MeshKit.Lathe("w", new[]
            {
                new Vector2(0.001f, 0f), new Vector2(0.075f, 0f), new Vector2(0.1f, 0.19f), new Vector2(0.108f, 0.2f),
                new Vector2(0.1f, 0.205f), new Vector2(0.092f, 0.195f), new Vector2(0.068f, 0.012f), new Vector2(0.001f, 0.012f)
            }, 24, false, false);
            var handle = MeshKit.Tube("h", Enumerable.Range(0, 13).Select(i =>
            {
                float a = Mathf.PI * i / 12f;
                return new Vector3(Mathf.Cos(a) * 0.104f, 0.19f + Mathf.Sin(a) * 0.09f, 0);
            }).ToArray(), 0.004f, 6);
            return MeshKit.Combine("Pail", new List<(Mesh, Matrix4x4, int)> { (wall, Matrix4x4.identity, 0), (handle, Matrix4x4.identity, 1) }, 2);
        });

        /// <summary>The pail's shape, upside down: a sand tower with four merlons.</summary>
        static Mesh TowerMesh() => Saved("SandTower", () =>
        {
            var body = MeshKit.Lathe("t", new[] { new Vector2(0.104f, 0f), new Vector2(0.1f, 0.012f), new Vector2(0.075f, 0.19f), new Vector2(0.06f, 0.195f), new Vector2(0.001f, 0.196f) }, 24, false, false,
                                     (s, a) => 1f + 0.02f * Mathf.Sin(a * 7f) + 0.015f * Mathf.Sin(a * 13f + 1f));
            var parts = new List<(Mesh, Matrix4x4, int)> { (body, Matrix4x4.identity, 0) };
            var merlon = MeshKit.RoundedBox("m", new Vector3(0.035f, 0.035f, 0.035f), 0.008f, 3);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2f + 0.4f;
                parts.Add((merlon, Matrix4x4.TRS(new Vector3(Mathf.Cos(a) * 0.052f, 0.21f, Mathf.Sin(a) * 0.052f), Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0), Vector3.one), 0));
            }
            return MeshKit.Combine("SandTower", parts, 1);
        });

        static Mesh ShovelMesh() => Saved("Shovel", () =>
        {
            // handle along +Z, blade at the far end (scoop slightly curved)
            var handle = MeshKit.Tube("h", new[] { new Vector3(0, 0, -0.06f), new Vector3(0, 0, 0.42f) }, 0.014f, 10);
            var grip = MeshKit.Tube("g", new[] { new Vector3(-0.05f, 0, -0.08f), new Vector3(0.05f, 0, -0.08f) }, 0.012f, 8);
            var blade = MeshKit.RoundedBox("b", new Vector3(0.14f, 0.012f, 0.16f), 0.005f, 4);
            var v = blade.vertices;
            for (int i = 0; i < v.Length; i++) v[i].y += (v[i].x * v[i].x) * 3.5f;   // scoop
            blade.vertices = v;
            blade.RecalculateNormals();
            return MeshKit.Combine("Shovel", new List<(Mesh, Matrix4x4, int)>
            {
                (handle, Matrix4x4.identity, 0), (grip, Matrix4x4.identity, 0),
                (blade, Matrix4x4.TRS(new Vector3(0, -0.01f, 0.49f), Quaternion.Euler(-12, 0, 0), Vector3.one), 1)
            }, 2);
        });

        static Mesh BulbMesh() => Saved("Bulb", () =>
            MeshKit.Lathe("Bulb", new[] { new Vector2(0.001f, -0.035f), new Vector2(0.014f, -0.03f), new Vector2(0.02f, -0.018f), new Vector2(0.018f, -0.006f),
                                          new Vector2(0.009f, 0.004f), new Vector2(0.008f, 0.02f), new Vector2(0.001f, 0.022f) }, 12, false, false));

        static Mesh ChairMesh() => Saved("BeachChair", () =>
        {
            // low wooden beach chair, origin at the front of the seat on the ground, facing +Z
            var parts = new List<(Mesh, Matrix4x4, int)>();
            Mesh T(Vector3 a, Vector3 b, float r = 0.016f) => MeshKit.Tube("t", new[] { a, b }, r, 8);
            float w = 0.27f;
            foreach (float x in new[] { -w, w })
            {
                parts.Add((T(new Vector3(x, 0, 0.05f), new Vector3(x, 0.34f, 0.0f)), Matrix4x4.identity, 0));          // front leg
                parts.Add((T(new Vector3(x, 0, -0.5f), new Vector3(x, 0.86f, -0.66f)), Matrix4x4.identity, 0));        // back leg / backrest
                parts.Add((T(new Vector3(x, 0.3f, 0.02f), new Vector3(x, 0.24f, -0.5f)), Matrix4x4.identity, 0));      // seat rail
                parts.Add((T(new Vector3(x, 0.5f, 0.0f), new Vector3(x, 0.5f, -0.5f), 0.02f), Matrix4x4.identity, 0)); // armrest
                parts.Add((T(new Vector3(x, 0.34f, 0.0f), new Vector3(x, 0.5f, 0.0f)), Matrix4x4.identity, 0));
            }
            parts.Add((T(new Vector3(-w, 0.3f, 0.02f), new Vector3(w, 0.3f, 0.02f)), Matrix4x4.identity, 0));
            parts.Add((T(new Vector3(-w, 0.86f, -0.66f), new Vector3(w, 0.86f, -0.66f)), Matrix4x4.identity, 0));
            // fabric: a sagging sling from the seat front to the top of the backrest
            var sling = new List<Vector3>();
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                Vector3 a = new Vector3(0, 0.3f, 0.02f), b = new Vector3(0, 0.86f, -0.66f);
                Vector3 p = Vector3.Lerp(a, b, t) + new Vector3(0, -0.22f, 0.05f) * Mathf.Sin(t * Mathf.PI) * (1 - t * 0.4f);
                sling.Add(p);
            }
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int i = 0; i < sling.Count; i++)
            {
                Vector3 tng = (sling[Mathf.Min(i + 1, sling.Count - 1)] - sling[Mathf.Max(0, i - 1)]).normalized;
                Vector3 nn = Vector3.Cross(Vector3.right, tng).normalized;
                v.Add(sling[i] + Vector3.left * (w - 0.01f)); n.Add(nn); uv.Add(new Vector2(0, i / 10f));
                v.Add(sling[i] + Vector3.right * (w - 0.01f)); n.Add(nn); uv.Add(new Vector2(1, i / 10f));
                if (i > 0) { int k = i * 2; tri.AddRange(new[] { k - 2, k, k - 1, k - 1, k, k + 1 }); }
            }
            parts.Add((MeshKit.Build("s", v, n, uv, tri), Matrix4x4.identity, 1));
            return MeshKit.Combine("BeachChair", parts, 2);
        });

        static Mesh UmbrellaMesh() => Saved("Umbrella", () =>
        {
            var pole = MeshKit.Tube("p", new[] { new Vector3(0, -0.25f, 0), new Vector3(0, 2.15f, 0) }, 0.018f, 10);
            var canopy = MeshKit.Lathe("c", new[] { new Vector2(1.05f, 1.72f), new Vector2(0.75f, 1.92f), new Vector2(0.35f, 2.06f), new Vector2(0.03f, 2.12f) }, 32, false, false,
                                       (s, a) => 1f - 0.05f * Mathf.Abs(Mathf.Sin(a * 4f)));
            var top = MeshKit.Lathe("t", new[] { new Vector2(0.001f, 2.1f), new Vector2(0.035f, 2.13f), new Vector2(0.001f, 2.2f) }, 10, false, false);
            var ribs = new List<(Mesh, Matrix4x4, int)> { (pole, Matrix4x4.identity, 0), (canopy, Matrix4x4.identity, 1), (top, Matrix4x4.identity, 0) };
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                ribs.Add((MeshKit.Tube("r", new[] { new Vector3(0, 1.45f, 0) + d * 0.02f, d * 0.6f + Vector3.up * 1.88f }, 0.006f, 5), Matrix4x4.identity, 0));
            }
            return MeshKit.Combine("Umbrella", ribs, 2);
        });

        static Mesh TowelMesh(Vector3 center, Quaternion rot, Vector2 size) => Saved("Towel", () =>
        {
            int nx = 10, nz = 18;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int z = 0; z <= nz; z++)
            for (int x = 0; x <= nx; x++)
            {
                Vector3 local = new Vector3((x / (float)nx - 0.5f) * size.x, 0, (z / (float)nz - 0.5f) * size.y);
                Vector3 w = center + rot * local;
                float wrinkle = (Mathf.PerlinNoise(x * 0.7f, z * 0.6f) - 0.5f) * 0.012f;
                float y = Ground(w.x, w.z) + 0.008f + wrinkle - center.y;
                v.Add(new Vector3(local.x, y, local.z));
                n.Add(Vector3.up);
                uv.Add(new Vector2(x / (float)nx, z / (float)nz * 2f));
                if (x > 0 && z > 0)
                {
                    int i = z * (nx + 1) + x;
                    tri.AddRange(new[] { i - nx - 2, i - 1, i - nx - 1, i - nx - 1, i - 1, i });
                }
            }
            var m = MeshKit.Build("Towel", v, n, uv, tri);
            m.RecalculateNormals();
            return m;
        });

        // ================================================================== build
        static Story BuildStory(Transform systems, Transform nest, Props props)
        {
            var s = new Story();
            _camp = Child(_env, "BeachCamp").transform;
            Vector3 seatGround = LX(SeatXY);
            Vector3 binPos = LX(BinXY);
            // the chair faces the bin, turned a little towards the sea so the view opens on the water
            Quaternion toBin = Facing(seatGround, binPos);
            Quaternion chairRot = Quaternion.Euler(0, toBin.eulerAngles.y + 22f, 0);
            Vector3 F = chairRot * Vector3.forward, R = chairRot * Vector3.right;

            // ---- chair + seat anchor
            var chair = MeshObj("BeachChair", _camp, ChairMesh(), new[] { M["WoodLight"], M["ChairFabric"] }, seatGround + F * 0.3f, chairRot);
            chair.layer = 2;   // Ignore Raycast: the seated player aims past it at the table and the cooler
            var chairCol = chair.AddComponent<BoxCollider>();
            chairCol.center = new Vector3(0, 0.3f, -0.3f);
            chairCol.size = new Vector3(0.6f, 0.6f, 0.75f);
            var seat = Child(_camp, "ChairSeat").transform;
            seat.SetPositionAndRotation(seatGround + F * 0.05f + Vector3.up * 0.26f, chairRot);
            s.seat = seat;

            // ---- umbrella, towel
            Vector3 up = LX(UmbrellaXY);
            var umb = MeshObj("Umbrella", _camp, UmbrellaMesh(), new[] { M["MetalWhite"], M["Canopy"] }, up, Quaternion.Euler(4, 30, 2));
            var umbCol = umb.AddComponent<CapsuleCollider>();
            umbCol.radius = 0.04f; umbCol.height = 2.2f; umbCol.center = new Vector3(0, 1f, 0);
            Vector3 towelC = LX(TowelXY);
            Quaternion towelRot = Quaternion.Euler(0, 12, 0);
            var towelGo = MeshObj("Towel", _camp, TowelMesh(towelC, towelRot, new Vector2(0.95f, 1.85f)), M["Towel"], towelC, towelRot, false);
            towelGo.GetComponent<MeshRenderer>().receiveShadows = true;
            s.towel = towelGo.transform;

            // ---- cooler (left of the chair) with the three empty cans on its lid
            Vector3 coolerPos = MeshKit.OnGround(seatGround - R * 0.62f + F * 0.12f);
            var cooler = Child(_camp, "Cooler").transform;
            cooler.SetPositionAndRotation(coolerPos, chairRot);
            LocalMesh("Body", cooler, Saved("CoolerBody", () => MeshKit.RoundedBox("cb", new Vector3(0.52f, 0.34f, 0.36f), 0.03f, 5)), M["CoolerBody"], new Vector3(0, 0.17f, 0), Quaternion.identity);
            LocalMesh("Lid", cooler, Saved("CoolerLid", () => MeshKit.RoundedBox("cl", new Vector3(0.54f, 0.06f, 0.38f), 0.025f, 4)), M["CoolerLid"], new Vector3(0, 0.37f, 0), Quaternion.identity);
            LocalMesh("Handle", cooler, Saved("CoolerHandle", () => MeshKit.Tube("h", new[] { new Vector3(-0.27f, 0.25f, 0), new Vector3(-0.29f, 0.3f, 0), new Vector3(-0.29f, 0.3f, 0.0f) }, 0.012f, 6)), M["CoolerLid"], Vector3.zero, Quaternion.identity);
            var coolerCol = cooler.gameObject.AddComponent<BoxCollider>();
            coolerCol.center = new Vector3(0, 0.2f, 0); coolerCol.size = new Vector3(0.54f, 0.4f, 0.38f);

            // ---- side table (right) with the drinks, coffee, sunglasses and the speaker
            Vector3 tablePos = MeshKit.OnGround(seatGround + R * 0.62f + F * 0.12f);
            var table = Child(_camp, "SideTable").transform;
            table.SetPositionAndRotation(tablePos, chairRot);
            LocalMesh("Top", table, Saved("TableTop", () => MeshKit.RoundedBox("tt", new Vector3(0.5f, 0.03f, 0.44f), 0.012f, 4)), M["WoodLight"], new Vector3(0, 0.47f, 0), Quaternion.identity);
            foreach (var (x, z) in new[] { (-0.21f, -0.18f), (0.21f, -0.18f), (-0.21f, 0.18f), (0.21f, 0.18f) })
                LocalMesh("Leg", table, Saved("TableLeg", () => MeshKit.Tube("l", new[] { Vector3.zero, new Vector3(0, 0.46f, 0) }, 0.013f, 6)), M["WoodLight"], new Vector3(x, 0, z), Quaternion.identity);
            var tableCol = table.gameObject.AddComponent<BoxCollider>();
            tableCol.center = new Vector3(0, 0.47f, 0); tableCol.size = new Vector3(0.5f, 0.03f, 0.44f);
            var legsCol = table.gameObject.AddComponent<BoxCollider>();
            legsCol.center = new Vector3(0, 0.23f, 0); legsCol.size = new Vector3(0.44f, 0.44f, 0.38f);
            Vector3 TableTop(float x, float z) => table.TransformPoint(new Vector3(x, 0.487f, z));

            // drink (PET bottle), second bottle, coffee, sunglasses, soda cans (closed)
            s.drink = Drink("Drink", TableTop(-0.15f, -0.1f), chairRot, "your drink");
            var drink2 = Drink("Drink2", TableTop(-0.05f, -0.14f), chairRot * Quaternion.Euler(0, 40, 0), "the other drink");
            Object.DestroyImmediate(drink2);   // just a prop: only "your drink" counts for the objective
            var cup = MeshObj("Coffee", _camp, CupMesh(), new[] { M["CupPaper"], M["CupLid"] }, TableTop(0.16f, -0.13f), chairRot);
            AddCapsule(cup, 0.037f, 0.116f);
            MakeGrab(cup, 0.25f, "coffee", false);
            BuildSunglasses(TableTop(0.14f, 0.1f), chairRot * Quaternion.Euler(0, 160, 0));
            foreach (var (pos, mat, nm) in new[] { (LX(TowelXY) + towelRot * new Vector3(0.25f, 0.04f, 0.55f), M["CanRed"], "SodaCan"), (LX(TowelXY) + towelRot * new Vector3(0.33f, 0.04f, 0.48f), M["CanBlue"], "SodaCan") })
            {
                var c = MeshObj(nm, _camp, CanMesh(), new[] { mat, M["CanTop"] }, MeshKit.OnGround(pos, 0.01f), Quaternion.identity);
                AddCapsule(c, 0.033f, 0.122f);
                MakeGrab(c, 0.37f, "soda");
            }

            // speaker (music player) on the table
            s.music = BuildSpeaker(TableTop(0.02f, 0.12f), chairRot * Quaternion.Euler(0, 180, 0));

            // ---- can toss: three empty cans on the cooler lid, the bin a few metres away
            var game = Child(_camp, "CanToss").AddComponent<CanTossGame>();
            var cans = new List<TossCan>();
            Material[] canMats = { M["CanRed"], M["CanBlue"], M["CanLime"] };
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = cooler.TransformPoint(new Vector3(-0.15f + i * 0.13f, 0.405f, 0.05f * (i % 2)));
                var c = MeshObj("EmptyCan" + (i + 1), _camp, CanMesh(), new[] { canMats[i], M["CanTop"] }, p, Quaternion.Euler(0, i * 50, 0));
                AddCapsule(c, 0.033f, 0.122f);
                var g = MakeGrab(c, 0.015f, "empty can");
                g.throwScale = 1f;      // the controller's real release velocity, no boost
                g.grabRadius = 0.12f;
                var rb = c.GetComponent<Rigidbody>();
                rb.linearDamping = 0.02f;
                rb.angularDamping = 0.1f;
                c.GetComponent<Collider>().sharedMaterial = CanPhysics();
                cans.Add(c.AddComponent<TossCan>());
            }
            var bin = BuildBin(binPos, out Collider binTrigger);
            Set(game, "cans", cans.ToArray());
            Set(game, "binTrigger", binTrigger);
            var label = new GameObject("CanTossScore");
            label.transform.SetParent(bin, false);
            label.transform.position = binPos + Vector3.up * 1.25f;
            label.transform.rotation = Facing(seatGround, binPos);
            var lt = label.AddComponent<TextMeshPro>();
            lt.rectTransform.sizeDelta = new Vector2(1.2f, 0.4f);
            lt.fontSize = 1.5f;
            lt.alignment = TextAlignmentOptions.Center;
            lt.color = new Color(1f, 0.96f, 0.85f);
            lt.outlineWidth = 0.2f;
            lt.outlineColor = new Color32(30, 30, 30, 255);
            Set(game, "scoreLabel", lt);
            s.cans = game;

            // ---- sandcastle: plot, dig spot (the team's Hole becomes the dug hole), shovel and pail
            s.castle = BuildSandcastle(props, out s.shovel, out s.hole);

            // ---- snack: beach bag with the sandwich wrapped in a plastic bag
            BuildSnack(s, towelC, towelRot);

            // ---- fireworks
            s.fireworks = BuildFireworks();

            // ---- light string: umbrella hook + four posts zigzagging to the nest
            s.garland = BuildGarland(umb.transform);

            // ---- flashlight (team object, adapted) on the towel
            var fl = props.flashlight;
            Vector3 flPos = MeshKit.OnGround(towelC + towelRot * new Vector3(-0.2f, 0, -0.35f), 0.03f);
            fl.SetPositionAndRotation(flPos, towelRot * Quaternion.Euler(0, 75, 0));

            // ---- phone
            s.phone = BuildPhone();

            // ---- the little turtle of the photo
            s.photoTurtleStart = Child(_env, "PhotoTurtleStart").transform;
            s.photoTurtleStart.SetPositionAndRotation(LX(PhotoTurtleXY), Quaternion.identity);
            var pt = Child(_env, "PhotoTurtle");
            pt.transform.SetPositionAndRotation(LX(PhotoTurtleXY), Quaternion.identity);
            pt.transform.localScale = Vector3.one * HatchlingScale;
            var ptModel = Model(Art + "/Models/Hatchling.fbx", pt.transform, "Mesh");
            Remap(ptModel, M);
            OrientByChild(ptModel, "Head");
            PoseHatchling(pt.transform, ptModel);
            var ptAv = pt.AddComponent<HatchlingAvatar>();
            WireAvatar(ptAv, ptModel);
            s.photoTurtle = pt.AddComponent<PhotoHatchling>();
            Set(s.photoTurtle, "avatar", ptAv);

            // ---- the route (waypoints) and the natural frame along it
            var routeRoot = Child(_env, "TurtleRoute").transform;
            s.route = RouteXY.Select((xy, i) =>
            {
                var t = Child(routeRoot, "Waypoint" + i).transform;
                t.position = LX(xy, 0.02f);
                return t;
            }).ToArray();
            Set(s.photoTurtle, "sea", s.route[s.route.Length - 1]);

            // ---- the giant (the human, replayed in the hatchling's act)
            s.giant = BuildGiant(systems);
            return s;
        }

        static BeachDrink Drink(string name, Vector3 pos, Quaternion rot, string label)
        {
            var root = new GameObject(name);
            root.transform.SetParent(_camp, false);
            root.transform.SetPositionAndRotation(pos, rot);
            var vis = Model(Art + "/Models/Bottle_PET.fbx", root.transform, "Visual");
            Remap(vis, M, M["Trash_PET"]);
            Bounds b = RendererBounds(vis);
            float k = 0.21f / Mathf.Max(0.01f, b.size.y);
            vis.transform.localScale *= k;
            b = RendererBounds(vis);
            vis.transform.position += pos - new Vector3(b.center.x, b.min.y, b.center.z);
            b = RendererBounds(vis);
            var cap = root.AddComponent<CapsuleCollider>();
            cap.center = root.transform.InverseTransformPoint(b.center);
            cap.height = b.size.y;
            cap.radius = Mathf.Min(b.size.x, b.size.z) * 0.5f;
            var g = MakeGrab(root, 0.5f, label, false);
            return root.AddComponent<BeachDrink>();
        }

        static void AddCapsule(GameObject go, float r, float h)
        {
            var c = go.AddComponent<CapsuleCollider>();
            c.radius = r;
            c.height = h;
            c.center = new Vector3(0, h * 0.5f, 0);
        }

        static void BuildSunglasses(Vector3 pos, Quaternion rot)
        {
            var root = new GameObject("Sunglasses");
            root.transform.SetParent(_camp, false);
            root.transform.SetPositionAndRotation(pos + Vector3.up * 0.012f, rot);
            var lensMesh = Saved("Lens", () => MeshKit.Lathe("l", new[] { new Vector2(0.001f, -0.002f), new Vector2(0.024f, 0f), new Vector2(0.026f, 0.003f), new Vector2(0.001f, 0.004f) }, 16, false, false));
            foreach (float x in new[] { -0.034f, 0.034f })
                LocalMesh("Lens", root.transform, lensMesh, M["Lens"], new Vector3(x, 0.01f, 0), Quaternion.Euler(90, 0, 0), new Vector3(1.15f, 1, 0.9f));
            LocalMesh("Frame", root.transform, Saved("GlassesFrame", () => MeshKit.Tube("f", new[] { new Vector3(-0.065f, 0.02f, 0), new Vector3(-0.01f, 0.024f, 0), new Vector3(0.01f, 0.024f, 0), new Vector3(0.065f, 0.02f, 0) }, 0.003f, 5)), M["Black"], Vector3.zero, Quaternion.identity);
            foreach (float x in new[] { -0.066f, 0.066f })
                LocalMesh("Arm", root.transform, Saved("GlassesArm", () => MeshKit.Tube("a", new[] { new Vector3(0, 0.018f, 0), new Vector3(0, 0.012f, -0.12f), new Vector3(0, 0f, -0.135f) }, 0.0025f, 5)), M["Black"], new Vector3(x, 0, 0), Quaternion.identity);
            var bc = root.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 0.012f, -0.05f);
            bc.size = new Vector3(0.14f, 0.03f, 0.13f);
            MakeGrab(root, 0.05f, "sunglasses", false);
        }

        static MusicPlayer BuildSpeaker(Vector3 pos, Quaternion rot)
        {
            var root = new GameObject("MusicPlayer");
            root.transform.SetParent(_camp, false);
            root.transform.SetPositionAndRotation(pos, rot);
            LocalMesh("Body", root.transform, Saved("SpeakerBody", () => MeshKit.RoundedBox("s", new Vector3(0.2f, 0.1f, 0.075f), 0.03f, 6)), M["Speaker"], new Vector3(0, 0.05f, 0), Quaternion.identity);
            var grille = Saved("Grille", () => MeshKit.Lathe("g", new[] { new Vector2(0.001f, 0f), new Vector2(0.034f, 0f), new Vector2(0.036f, 0.004f), new Vector2(0.001f, 0.006f) }, 20, false, false));
            foreach (float x in new[] { -0.05f, 0.05f })
                LocalMesh("Grille", root.transform, grille, M["Grille"], new Vector3(x, 0.05f, 0.036f), Quaternion.Euler(90, 0, 0));
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.05f, 0); col.size = new Vector3(0.2f, 0.1f, 0.075f);
            PokeButton Btn(string label, Vector3 local, Material mat, float r)
            {
                var b = new GameObject("Button_" + label);
                b.transform.SetParent(root.transform, false);
                b.transform.localPosition = local;
                var capGo = LocalMesh("Cap", b.transform, Saved("ButtonCap" + r, () => MeshKit.Lathe("b", new[] { new Vector2(r, 0f), new Vector2(r, 0.006f), new Vector2(r * 0.8f, 0.009f), new Vector2(0.001f, 0.01f) }, 14, false, false)), mat, Vector3.zero, Quaternion.identity);
                var sc = b.AddComponent<SphereCollider>();
                sc.radius = r * 1.6f;
                sc.isTrigger = true;
                var pb = b.AddComponent<PokeButton>();
                pb.Label = label;
                Set(pb, "cap", capGo.transform);
                Set(pb, "radius", 0.04f);
                return pb;
            }
            var play = Btn("play / pause", new Vector3(-0.035f, 0.1f, 0f), M["ButtonGreen"], 0.012f);
            var vol = Btn("volume +", new Vector3(0.035f, 0.1f, 0f), M["ButtonWhite"], 0.011f);
            var led = LocalMesh("Led", root.transform, Saved("LedDot", () => MeshKit.Lathe("d", new[] { new Vector2(0.001f, 0f), new Vector2(0.004f, 0f), new Vector2(0.001f, 0.002f) }, 8, false, false)), M["Led"], new Vector3(0.08f, 0.088f, 0.03f), Quaternion.Euler(40, 0, 0));
            var src = root.AddComponent<AudioSource>();
            src.clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Audio/beach_music_loop.wav");
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.minDistance = 1.2f;
            src.maxDistance = 40f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.dopplerLevel = 0;
            src.volume = 0;
            var mp = root.AddComponent<MusicPlayer>();
            Set(mp, "source", src);
            Set(mp, "playButton", play);
            Set(mp, "volumeButton", vol);
            Set(mp, "statusLed", led.GetComponent<MeshRenderer>());
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            return mp;
        }

        static Transform BuildBin(Vector3 p, out Collider trigger)
        {
            var bin = Child(_env, "LitterBin").transform;
            bin.position = p;
            var mesh = Saved("Bin", () => MeshKit.Lathe("bin", new[]
            {
                // narrower mouth (r 0.25) than the first version (r 0.34): the toss is a small skill challenge
                new Vector2(0.001f, 0f), new Vector2(0.21f, 0f), new Vector2(0.25f, 0.85f), new Vector2(0.27f, 0.88f),
                new Vector2(0.245f, 0.89f), new Vector2(0.225f, 0.86f), new Vector2(0.19f, 0.04f), new Vector2(0.001f, 0.04f)
            }, 28, false, false));
            LocalMesh("Body", bin, mesh, M["BinGreen"], Vector3.zero, Quaternion.identity);
            for (int i = 0; i < 10; i++)
            {
                var wall = new GameObject("Wall").AddComponent<BoxCollider>();
                wall.transform.SetParent(bin, false);
                float a = i * 36f;
                wall.transform.localRotation = Quaternion.Euler(0, a, 0);
                wall.transform.localPosition = wall.transform.localRotation * new Vector3(0, 0.44f, 0.235f);
                wall.size = new Vector3(0.16f, 0.88f, 0.035f);
            }
            var floor = new GameObject("Floor").AddComponent<BoxCollider>();
            floor.transform.SetParent(bin, false);
            floor.transform.localPosition = new Vector3(0, 0.02f, 0);
            floor.size = new Vector3(0.42f, 0.04f, 0.42f);
            var aim = Child(bin, "AimPoint").transform;
            aim.localPosition = new Vector3(0, 0.95f, 0);
            bin.gameObject.AddComponent<ThrowTarget>().aimPoint = aim;
            var trig = new GameObject("BinTrigger").AddComponent<BoxCollider>();
            trig.transform.SetParent(bin, false);
            trig.transform.localPosition = new Vector3(0, 0.42f, 0);
            trig.size = new Vector3(0.36f, 0.8f, 0.36f);
            trig.isTrigger = true;
            trigger = trig;
            return bin;
        }

        static SandcastleSite BuildSandcastle(Props props, out VRGrabbable shovel, out SandHole hole)
        {
            var root = Child(_env, "Sandcastle").transform;
            Vector3 plot = LX(PlotXY);
            Vector3 dig = LX(DigXY);
            root.position = plot;
            var plotMark = Prim(PrimitiveType.Quad, "PlotMark", root, plot + Vector3.up * 0.01f, Vector3.one * 0.9f, null, false, Quaternion.Euler(90, 0, 0));
            var pm = MatCopy("CastlePlot", M["GroundDecal"]);
            pm.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Gen + "/Textures/SafeZoneRing.png"));
            pm.SetColor("_BaseColor", new Color(0.55f, 0.42f, 0.28f, 0.35f));
            plotMark.GetComponent<MeshRenderer>().sharedMaterial = pm;

            // hole: the team's Hole object, now dug by the human
            hole = props.sandHole;
            hole.transform.position = dig;
            Set(hole, "startDug", false);
            Set(hole, "radius", 0.42f);
            Set(hole, "depth", 0.2f);      // cosine bowl: max wall slope ~37 deg
            var zone = props.hole.GetComponent<TurtleZone>();
            zone.FirstEnterMessage = "";
            EditorUtility.SetDirty(zone);
            var digMark = Prim(PrimitiveType.Quad, "DigMark", root, dig + Vector3.up * 0.01f, Vector3.one * 0.7f, pm, false, Quaternion.Euler(90, 0, 0));

            // towers (hidden until built) at the two slots, walls/gate/flag appear when finished
            var tower = TowerMesh();
            Vector3 across = Vector3.right * 0.22f;
            var slots = new List<Transform>();
            foreach (var off in new[] { -across, across })
            {
                var t = MeshObj("Tower", root, tower, M["Sandcastle"], MeshKit.OnGround(plot + off, -0.005f), Quaternion.Euler(0, Rand(0, 90), 0));
                var mc = t.AddComponent<MeshCollider>();
                mc.sharedMesh = tower;
                mc.convex = true;
                slots.Add(t.transform);
            }
            var extras = Child(root, "Finished").transform;
            extras.position = plot;
            var wall = Saved("CastleWall", () => MeshKit.RoundedBox("w", new Vector3(0.26f, 0.09f, 0.07f), 0.02f, 4));
            var w1 = LocalMesh("Wall", extras, wall, M["Sandcastle"], new Vector3(0, 0.04f, 0), Quaternion.identity);
            w1.transform.position = MeshKit.OnGround(plot, 0.04f);
            foreach (var z in new[] { -0.12f, 0.12f })
            {
                var w = LocalMesh("Wall", extras, wall, M["Sandcastle"], Vector3.zero, Quaternion.Euler(0, 90, 0), new Vector3(0.9f, 0.8f, 1));
                w.transform.position = MeshKit.OnGround(plot + across * 1.0f + Vector3.forward * z * 0.5f + Vector3.forward * 0.1f, 0.035f);
            }
            var flagPole = LocalMesh("FlagPole", extras, Saved("FlagPole", () => MeshKit.Tube("p", new[] { Vector3.zero, new Vector3(0, 0.22f, 0) }, 0.003f, 5)), M["WoodLight"], Vector3.zero, Quaternion.identity);
            flagPole.transform.position = slots[0].position + Vector3.up * 0.2f;
            var flag = LocalMesh("Flag", flagPole.transform, Saved("Flag", () =>
            {
                var v = new List<Vector3> { new Vector3(0, 0.22f, 0), new Vector3(0, 0.16f, 0), new Vector3(0.09f, 0.19f, 0) };
                var n = new List<Vector3> { Vector3.back, Vector3.back, Vector3.back };
                var uv = new List<Vector2> { Vector2.zero, Vector2.up, Vector2.right };
                var m = MeshKit.Build("f", v, n, uv, new List<int> { 0, 1, 2 });
                return m;
            }), M["PlasticRed"], Vector3.zero, Quaternion.identity);
            flag.GetComponent<MeshRenderer>().sharedMaterial = M["Canopy"];
            foreach (var mf in extras.GetComponentsInChildren<MeshFilter>())
                if (mf.name == "Wall") { var c = mf.gameObject.AddComponent<BoxCollider>(); }

            // pail and shovel next to the plot
            var pail = MeshObj("SandPail", root, PailMesh(), new[] { M["PlasticRed"], M["PlasticYellow"] }, MeshKit.OnGround(plot + new Vector3(0.45f, 0, -0.7f), 0.005f), Quaternion.identity);
            var pc = pail.AddComponent<BoxCollider>();   // flat top and bottom: it stands either way up
            pc.size = new Vector3(0.19f, 0.2f, 0.19f); pc.center = new Vector3(0, 0.1f, 0);
            var pg = MakeGrab(pail, 0.6f, "bucket", false);
            pg.grabRadius = 0.15f;
            pg.desktopOffset = new Vector3(0.2f, -0.35f, 0.6f);
            // the sand inside: a mesh rebuilt by SandPail in the pail's own space, clamped to the inner wall
            var fill = LocalMesh("SandFill", pail.transform, Saved("PailFill", () => MeshKit.Lathe("f", new[] { new Vector2(0.001f, 0.014f), new Vector2(0.064f, 0.014f), new Vector2(0.066f, 0.05f), new Vector2(0.001f, 0.056f) }, 18, false, false)), M["SandFill"], Vector3.zero, Quaternion.identity);
            fill.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            fill.SetActive(false);
            var sp = pail.AddComponent<SandPail>();
            Set(sp, "fillVisual", fill.transform);
            Set(sp, "fullHeight", 0.17f);

            var sh = MeshObj("Shovel", root, ShovelMesh(), new[] { M["PlasticYellow"], M["PlasticRed"] }, MeshKit.OnGround(plot + new Vector3(0.95f, 0, -0.55f), 0.02f), Quaternion.Euler(0, 70, 0));
            var shc = sh.AddComponent<BoxCollider>();
            shc.size = new Vector3(0.14f, 0.07f, 0.66f); shc.center = new Vector3(0, 0, 0.24f);
            var tip = Child(sh.transform, "BladeTip").transform;
            tip.localPosition = new Vector3(0, 0, 0.56f);
            var load = LocalMesh("SandLoad", sh.transform, Saved("SandLoad", () => MeshKit.RoundedBox("s", new Vector3(0.1f, 0.04f, 0.1f), 0.02f, 3)), M["SandFill"], new Vector3(0, 0.02f, 0.49f), Quaternion.identity);
            shovel = MakeGrab(sh, 0.4f, "shovel", false);
            shovel.usePose = true;
            shovel.posePosition = new Vector3(0, -0.01f, 0.02f);
            shovel.poseEuler = new Vector3(55, 0, 0);    // blade points down and forward from the fist
            shovel.desktopOffset = new Vector3(0.18f, -0.3f, 0.35f);
            shovel.desktopEuler = new Vector3(50, 0, 0);
            var bs = sh.AddComponent<BeachShovel>();
            Set(bs, "bladeTip", tip);
            Set(bs, "sandLoad", load);

            var site = root.gameObject.AddComponent<SandcastleSite>();
            Set(site, "hole", hole);
            Set(site, "digSpot", hole.transform);
            Set(site, "plot", Child(root, "PlotCenter").transform);
            root.Find("PlotCenter").position = plot;
            Set(site, "pail", sp);
            Set(site, "towerSlots", slots.ToArray());
            Set(site, "towerMesh", tower);
            Set(site, "sandMaterial", M["Sandcastle"]);
            Set(site, "finishedExtras", extras.gameObject);
            Set(site, "hideWhenFinished", new Object[] { plotMark, digMark });
            return site;
        }

        static void BuildSnack(Story s, Vector3 towelC, Quaternion towelRot)
        {
            Vector3 bagPos = MeshKit.OnGround(towelC + towelRot * new Vector3(0.75f, 0, 0.35f));
            var tote = Child(_camp, "BeachBag").transform;
            tote.SetPositionAndRotation(bagPos, towelRot * Quaternion.Euler(0, -20, 0));
            var toteMesh = Saved("Tote", () => MeshKit.Lathe("t", new[]
            {
                new Vector2(0.001f, 0f), new Vector2(0.16f, 0f), new Vector2(0.19f, 0.28f), new Vector2(0.175f, 0.285f), new Vector2(0.15f, 0.02f), new Vector2(0.001f, 0.02f)
            }, 4, false, false));
            LocalMesh("Body", tote, toteMesh, M["Straw"], Vector3.zero, Quaternion.Euler(0, 45, 0), new Vector3(1.3f, 1, 0.65f));
            foreach (float x in new[] { -0.09f, 0.09f })
                LocalMesh("Handle", tote, Saved("ToteHandle", () => MeshKit.Tube("h", Enumerable.Range(0, 9).Select(i => { float a = Mathf.PI * i / 8f; return new Vector3(Mathf.Cos(a) * 0.08f, 0.28f + Mathf.Sin(a) * 0.12f, 0); }).ToArray(), 0.008f, 6)), M["Straw"], new Vector3(0, 0, x), Quaternion.identity);
            var tc = tote.gameObject.AddComponent<BoxCollider>();
            tc.center = new Vector3(0, 0.14f, 0); tc.size = new Vector3(0.38f, 0.28f, 0.2f);
            tc.isTrigger = true;

            // sandwich sticking out of the bag
            var sw = new GameObject("Sandwich");
            sw.transform.SetParent(_camp, false);
            sw.transform.SetPositionAndRotation(tote.TransformPoint(new Vector3(0.02f, 0.3f, 0)), tote.rotation * Quaternion.Euler(70, 0, 0));
            var bread = Saved("Bread", () => MeshKit.RoundedBox("b", new Vector3(0.11f, 0.016f, 0.11f), 0.007f, 4));
            LocalMesh("BreadTop", sw.transform, bread, M["Bread"], new Vector3(0, 0.017f, 0), Quaternion.identity);
            LocalMesh("BreadBottom", sw.transform, bread, M["Bread"], new Vector3(0, -0.017f, 0), Quaternion.identity);
            LocalMesh("Lettuce", sw.transform, Saved("Filling", () => MeshKit.RoundedBox("f", new Vector3(0.118f, 0.008f, 0.118f), 0.004f, 3)), M["Filling"], new Vector3(0, 0.004f, 0), Quaternion.Euler(0, 8, 0));
            LocalMesh("Ham", sw.transform, Saved("Filling", () => null), M["Ham"], new Vector3(0, -0.005f, 0), Quaternion.Euler(0, -6, 0), new Vector3(0.95f, 1, 0.95f));
            var swc = sw.AddComponent<BoxCollider>();
            swc.size = new Vector3(0.115f, 0.05f, 0.115f);
            var swg = MakeGrab(sw, 0.2f, "sandwich", false);
            swg.desktopOffset = new Vector3(0.12f, -0.18f, 0.38f);
            var sandwich = sw.AddComponent<Sandwich>();
            s.sandwich = sandwich;
            var rb = sw.GetComponent<Rigidbody>();
            rb.isKinematic = true;   // waits in the bag until grabbed

            // the plastic wrapper, around the sandwich
            var wrap = new GameObject("PlasticBag_Wrapper");
            wrap.transform.SetParent(sw.transform, false);
            wrap.transform.localPosition = new Vector3(0, 0, -0.02f);
            var vis = Model(Art + "/Models/PlasticBag.fbx", wrap.transform, "Visual");
            Remap(vis, new Dictionary<string, Material>(), M["Trash_Bag"]);
            Bounds b = RendererBounds(vis);
            float k = 0.2f / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));
            vis.transform.localScale *= k;
            b = RendererBounds(vis);
            vis.transform.position += wrap.transform.position - b.center;
            foreach (var r in vis.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.On;
            var landing = Child(_env, "BagLanding").transform;
            landing.position = LX(BagLandXY, 0.012f);
            var rest = Child(_camp, "WrapperRest").transform;
            rest.position = MeshKit.OnGround(towelC + towelRot * new Vector3(0.3f, 0, -0.1f), 0.012f);
            var wb = wrap.AddComponent<WindBag>();
            Set(wb, "landing", landing);
            Set(wb, "towelRest", rest);
            Set(wb, "visual", vis.transform);
            Set(sandwich, "wrapper", wb);
            s.bag = wb;
        }

        static FireworksShow BuildFireworks()
        {
            var zone = Child(_env, "FireworksZone").transform;
            Vector3 c = LX(FireworksXY);
            zone.position = c;
            // roped-off safe area
            var posts = new List<Vector3>();
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                Vector3 p = MeshKit.OnGround(c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 1.5f);
                posts.Add(p);
                Prim(PrimitiveType.Cylinder, "ZonePost", zone, p + Vector3.up * 0.35f, new Vector3(0.04f, 0.35f, 0.04f), M["Wood"], true);
                Prim(PrimitiveType.Sphere, "Flag", zone, p + Vector3.up * 0.72f, new Vector3(0.06f, 0.04f, 0.06f), M["PlasticRed"]);
            }
            for (int i = 0; i < 6; i++)
            {
                var pts = MeshKit.Sag(posts[i] + Vector3.up * 0.6f, posts[(i + 1) % 6] + Vector3.up * 0.6f, 0.06f, 8);
                MeshObj("Rope", zone, MeshKit.Tube("rope", pts, 0.006f, 5), M["Rope"], Vector3.zero, Quaternion.identity, false);
            }
            // the battery (cake)
            var cake = Child(zone, "Battery").transform;
            cake.position = MeshKit.OnGround(c);
            LocalMesh("Box", cake, Saved("FwBox", () => MeshKit.RoundedBox("b", new Vector3(0.3f, 0.22f, 0.3f), 0.01f, 3)), M["Cardboard"], new Vector3(0, 0.11f, 0), Quaternion.identity);
            var tubeTop = Saved("FwTube", () => MeshKit.Lathe("t", new[] { new Vector2(0.03f, 0f), new Vector2(0.03f, 0.01f), new Vector2(0.022f, 0.01f), new Vector2(0.022f, -0.01f) }, 10, false, false));
            var spent = Child(cake, "Spent").transform;
            for (int x = 0; x < 3; x++)
            for (int z = 0; z < 3; z++)
            {
                Vector3 lp = new Vector3(-0.09f + x * 0.09f, 0.22f, -0.09f + z * 0.09f);
                LocalMesh("Tube", cake, tubeTop, M["CanTop"], lp, Quaternion.identity);
                LocalMesh("Burnt", spent, Saved("FwBurnt", () => MeshKit.Lathe("b", new[] { new Vector2(0.001f, 0f), new Vector2(0.025f, 0.002f), new Vector2(0.001f, 0.004f) }, 10, false, false)), M["Scorch"], lp + Vector3.up * 0.004f, Quaternion.identity);
            }
            // a few burnt paper bits on the sand
            for (int i = 0; i < 7; i++)
            {
                Vector3 p = MeshKit.OnGround(c + new Vector3(Rand(-0.8f, 0.8f), 0, Rand(-0.8f, 0.8f)), 0.004f);
                var bit = Prim(PrimitiveType.Quad, "BurntPaper", spent, p, new Vector3(Rand(0.03f, 0.06f), Rand(0.02f, 0.05f), 1), M["Scorch"], false, Quaternion.Euler(90, Rand(0, 360), 0));
            }
            var bc = cake.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 0.11f, 0); bc.size = new Vector3(0.3f, 0.22f, 0.3f);
            var muzzle = Child(cake, "Muzzle").transform;
            muzzle.localPosition = new Vector3(0, 0.25f, 0);

            // launch box on a post at the edge of the zone, towards the camp
            Vector3 camp = LX(UmbrellaXY);
            Vector3 dir = Vector3.ProjectOnPlane(camp - c, Vector3.up).normalized;
            Vector3 bp = MeshKit.OnGround(c + dir * 1.9f);
            var stand = Child(zone, "LaunchStand").transform;
            stand.SetPositionAndRotation(bp, Quaternion.LookRotation(-dir));
            LocalMesh("Post", stand, Saved("StandPost", () => MeshKit.Tube("p", new[] { Vector3.zero, new Vector3(0, 0.95f, 0) }, 0.025f, 8)), M["MetalWhite"], Vector3.zero, Quaternion.identity);
            LocalMesh("Box", stand, Saved("StandBox", () => MeshKit.RoundedBox("b", new Vector3(0.18f, 0.07f, 0.14f), 0.015f, 4)), M["Speaker"], new Vector3(0, 0.98f, 0), Quaternion.Euler(-20, 0, 0));
            var sc = stand.gameObject.AddComponent<CapsuleCollider>();
            sc.radius = 0.05f; sc.height = 1f; sc.center = new Vector3(0, 0.5f, 0);
            var btn = new GameObject("LaunchButton");
            btn.transform.SetParent(stand, false);
            btn.transform.localPosition = new Vector3(0, 1.025f, -0.01f);
            btn.transform.localRotation = Quaternion.Euler(-20, 0, 0);
            var capGo = LocalMesh("Cap", btn.transform, Saved("BigButton", () => MeshKit.Lathe("b", new[] { new Vector2(0.03f, 0f), new Vector2(0.03f, 0.012f), new Vector2(0.024f, 0.018f), new Vector2(0.001f, 0.02f) }, 18, false, false)), M["ButtonRed"], Vector3.zero, Quaternion.identity);
            var bsc = btn.AddComponent<SphereCollider>();
            bsc.radius = 0.04f;
            bsc.isTrigger = true;
            var pb = btn.AddComponent<PokeButton>();
            pb.Label = "launch";
            Set(pb, "cap", capGo.transform);
            Set(pb, "radius", 0.05f);
            var label = new GameObject("Label").AddComponent<TextMeshPro>();
            label.transform.SetParent(stand, false);
            label.transform.localPosition = new Vector3(0, 1.12f, 0.02f);
            label.transform.localRotation = Quaternion.Euler(-20, 180, 0);
            label.rectTransform.sizeDelta = new Vector2(0.4f, 0.1f);
            label.fontSize = 0.35f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, 0.9f, 0.7f);
            label.text = "<b>FIREWORKS</b>";

            var show = zone.gameObject.AddComponent<FireworksShow>();
            var seaC = Child(_env, "FireworksTarget").transform;
            seaC.position = new Vector3(cx + 2f, WaterLevel, zs + 32f);
            Set(show, "launchButton", pb);
            Set(show, "muzzle", muzzle);
            Set(show, "seaCenter", seaC);
            Set(show, "spentVisual", spent.gameObject);
            Set(show, "particleMaterial", M["Firework"]);
            Set(show, "reflectionMaterial", M["FireworkReflection"]);
            Set(show, "waterLevel", WaterLevel);
            return show;
        }

        static LightGarland BuildGarland(Transform umbrella)
        {
            var root = Child(_env, "LightString").transform;
            var hooks = new List<Transform>();
            Transform Hook(string name, Vector3 p, Transform parent)
            {
                var h = Child(parent, name).transform;
                h.position = p;
                var ring = LocalMesh("Ring", h, Saved("HookRing", () => MeshKit.Tube("r", Enumerable.Range(0, 13).Select(i => { float a = i / 12f * Mathf.PI * 2f; return new Vector3(Mathf.Cos(a) * 0.025f, Mathf.Sin(a) * 0.025f, 0); }).ToArray(), 0.004f, 5, false)), M["MetalWhite"], Vector3.zero, Quaternion.identity);
                var marker = Prim(PrimitiveType.Quad, "HookMarker", h, p, Vector3.one * 0.22f, M["HookMarker"], false);
                marker.transform.localPosition = Vector3.zero;
                marker.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                var mat = MatCopy("HookMarkerRing", M["HookMarker"]);
                mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Gen + "/Textures/SafeZoneRing.png"));
                marker.GetComponent<MeshRenderer>().sharedMaterial = mat;
                marker.AddComponent<Billboard>();
                marker.SetActive(false);
                return h;
            }
            hooks.Add(Hook("UmbrellaHook", umbrella.position + Vector3.up * 1.62f + umbrella.right * 0.03f, root));
            for (int i = 0; i < PostXY.Length; i++)
            {
                Vector3 g = LX(PostXY[i]);
                var post = Prim(PrimitiveType.Cylinder, "LightPost" + (i + 1), root, g + Vector3.up * 0.8f, new Vector3(0.06f, 0.8f, 0.06f), M["Wood"], true,
                                Quaternion.Euler(Rand(-2, 2), Rand(0, 360), Rand(-2, 2)));
                hooks.Add(Hook("PostHook" + (i + 1), g + Vector3.up * 1.62f, root));
            }
            // the reel, leaning on the umbrella pole
            var reel = new GameObject("LightReel");
            reel.transform.SetParent(root, false);
            reel.transform.SetPositionAndRotation(MeshKit.OnGround(umbrella.position + umbrella.right * 0.35f - umbrella.forward * 0.2f, 0.09f), Quaternion.Euler(0, 30, 90));
            LocalMesh("Spool", reel.transform, Saved("Reel", () => MeshKit.Lathe("r", new[]
            {
                new Vector2(0.02f, -0.07f), new Vector2(0.09f, -0.07f), new Vector2(0.09f, -0.06f), new Vector2(0.05f, -0.06f),
                new Vector2(0.05f, 0.06f), new Vector2(0.09f, 0.06f), new Vector2(0.09f, 0.07f), new Vector2(0.02f, 0.07f)
            }, 20, false, false)), M["WoodLight"], Vector3.zero, Quaternion.identity);
            LocalMesh("Wound", reel.transform, Saved("ReelWound", () => MeshKit.Lathe("w", new[] { new Vector2(0.07f, -0.06f), new Vector2(0.075f, 0f), new Vector2(0.07f, 0.06f) }, 20, false, false)), M["Cable"], Vector3.zero, Quaternion.identity);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f;
                LocalMesh("Bulb", reel.transform, BulbMesh(), M["BulbOff"], Quaternion.Euler(0, a, 0) * new Vector3(0.078f, 0, 0) + Vector3.up * ((i % 3) - 1) * 0.035f, Quaternion.Euler(0, a, 90));
            }
            var rc = reel.AddComponent<CapsuleCollider>();
            rc.radius = 0.09f; rc.height = 0.16f; rc.direction = 1;
            var rg = MakeGrab(reel, 0.8f, "string of lights", false);
            rg.grabRadius = 0.16f;

            var gar = root.gameObject.AddComponent<LightGarland>();
            Set(reel.AddComponent<GarlandReel>(), "garland", gar);
            Set(gar, "hooks", hooks.ToArray());
            Set(gar, "reel", rg);
            Set(gar, "bulbMesh", BulbMesh());
            Set(gar, "cableMaterial", M["Cable"]);
            Set(gar, "bulbOffMaterial", M["BulbOff"]);
            Set(gar, "bulbOnMaterial", M["BulbOn"]);
            Set(gar, "haloMaterial", M["BulbHalo"]);
            Set(gar, "poolMaterial", M["LightPool"]);
            return gar;
        }

        static PhoneCamera BuildPhone()
        {
            var phone = new GameObject("Phone");
            phone.transform.SetParent(_camp, false);
            LocalMesh("Body", phone.transform, Saved("PhoneBody", () => MeshKit.RoundedBox("p", new Vector3(0.072f, 0.15f, 0.008f), 0.006f, 4)), M["Phone"], Vector3.zero, Quaternion.identity);
            // screen faces the user (-Z), the camera looks forward (+Z)
            var screen = Prim(PrimitiveType.Quad, "Screen", phone.transform, Vector3.zero, new Vector3(0.064f, 0.138f, 1), M["Screen"], false, Quaternion.identity);
            screen.transform.localPosition = new Vector3(0, 0, -0.0042f);
            screen.transform.localRotation = Quaternion.identity;
            screen.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            LocalMesh("Lens", phone.transform, Saved("PhoneLens", () => MeshKit.Lathe("l", new[] { new Vector2(0.001f, 0f), new Vector2(0.007f, 0f), new Vector2(0.006f, 0.003f), new Vector2(0.001f, 0.003f) }, 12, false, false)), M["Black"], new Vector3(-0.022f, 0.058f, 0.004f), Quaternion.Euler(90, 0, 0));
            var camGo = new GameObject("PreviewCamera");
            camGo.transform.SetParent(phone.transform, false);
            camGo.transform.localPosition = new Vector3(-0.022f, 0.058f, 0.01f);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 62f;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 120f;
            cam.enabled = false;
            cam.stereoTargetEye = StereoTargetEyeMask.None;
            var flashGo = new GameObject("Flash");
            flashGo.transform.SetParent(phone.transform, false);
            flashGo.transform.localPosition = new Vector3(-0.01f, 0.058f, 0.02f);
            var fl = flashGo.AddComponent<Light>();
            fl.type = LightType.Point;
            fl.range = 6f;
            fl.color = new Color(1f, 0.98f, 0.95f);
            fl.shadows = LightShadows.None;
            fl.enabled = false;
            var bc = phone.AddComponent<BoxCollider>();
            bc.size = new Vector3(0.075f, 0.152f, 0.012f);
            var g = MakeGrab(phone, 0.18f, "phone", false);
            g.usePose = true;
            g.posePosition = new Vector3(0f, 0.07f, 0.06f);
            g.poseEuler = new Vector3(-15f, 0, 0);
            g.desktopOffset = new Vector3(0.06f, -0.12f, 0.32f);
            g.desktopEuler = Vector3.zero;
            var pc = phone.AddComponent<PhoneCamera>();
            Set(pc, "previewCamera", cam);
            Set(pc, "screen", screen.GetComponent<MeshRenderer>());
            Set(pc, "flash", fl);
            return pc;
        }

        static GiantHuman BuildGiant(Transform systems)
        {
            var root = Child(_env, "TheGiant").transform;
            var src = Model(Art + "/Models/HumanBody.fbx", null, "~GiantSource");
            Remap(src, M);
            var pup = root.gameObject.AddComponent<HumanPuppet>();
            Transform Seg(string part, string name, bool foot = false) => Segment(root, src, part, name, foot);
            pup.torso = Seg("Torso", "Torso");
            pup.upperArmL = Seg("UpperArm", "UpperArmL");
            pup.forearmL = Seg("Forearm", "ForearmL");
            pup.handL = Seg("Hand", "HandL");
            pup.upperArmR = Seg("UpperArm", "UpperArmR");
            pup.forearmR = Seg("Forearm", "ForearmR");
            pup.handR = Seg("Hand", "HandR");
            pup.thighL = Seg("Thigh", "ThighL");
            pup.shinL = Seg("Shin", "ShinL");
            pup.footL = Seg("Foot", "FootL", true);
            pup.thighR = Seg("Thigh", "ThighR");
            pup.shinR = Seg("Shin", "ShinR");
            pup.footR = Seg("Foot", "FootR", true);
            Object.DestroyImmediate(src);
            // a head (the player's own avatar has none: the camera is the eyes)
            var head = Child(root, "Head").transform;
            LocalMesh("Face", head, Saved("Head", () => MeshKit.Lathe("h", new[]
            {
                new Vector2(0.045f, 0f), new Vector2(0.06f, 0.04f), new Vector2(0.085f, 0.1f), new Vector2(0.09f, 0.16f), new Vector2(0.075f, 0.22f), new Vector2(0.04f, 0.25f), new Vector2(0.001f, 0.26f)
            }, 18, false, false)), M["Avatar_Skin"], Vector3.zero, Quaternion.identity, new Vector3(0.9f, 1f, 1.05f));
            LocalMesh("Hair", head, Saved("Hair", () => MeshKit.Lathe("hr", new[] { new Vector2(0.094f, 0.15f), new Vector2(0.09f, 0.21f), new Vector2(0.05f, 0.255f), new Vector2(0.001f, 0.268f) }, 18, false, false)), M["Hair"], new Vector3(0, 0, -0.008f), Quaternion.identity, new Vector3(0.92f, 1f, 1.08f));
            pup.head = head;
            // the flashlight in the right hand (a beam that pulls hatchlings), the phone with its flash
            var lamp = Child(root, "GiantFlashlight").transform;
            var torch = Model($"{PH}/small_plastic_torch/small_plastic_torch.fbx", lamp, "Torch");
            if (torch != null)
            {
                Remap(torch, new Dictionary<string, Material>(), M["small_plastic_torch"]);
                AlignLongAxis(torch, lamp, 0, 0.2f);
            }
            var beamGo = Child(lamp, "Beam");
            beamGo.transform.localPosition = new Vector3(0, 0, 0.1f);
            var beam = beamGo.AddComponent<Light>();
            beam.type = LightType.Spot;
            beam.range = 10f;
            beam.spotAngle = 55f;
            beam.innerSpotAngle = 28f;
            beam.intensity = 6f;
            beam.color = new Color(1f, 0.87f, 0.72f);
            beam.shadows = LightShadows.None;
            var ls = beamGo.AddComponent<LightSource>();
            Set(ls, "strength", 1.8f);
            Set(ls, "artificial", true);
            beamGo.AddComponent<LightConeVisual>();
            pup.flashlight = lamp;
            var ph = Child(root, "GiantPhone").transform;
            LocalMesh("Body", ph, Saved("PhoneBody", () => null), M["Phone"], Vector3.zero, Quaternion.identity);
            var flashGo = Child(ph, "Flash");
            flashGo.transform.localPosition = new Vector3(0, 0.05f, 0.02f);
            var fl = flashGo.AddComponent<Light>();
            fl.type = LightType.Point;
            fl.range = 5f;
            fl.color = new Color(1f, 0.98f, 0.95f);
            fl.shadows = LightShadows.None;
            pup.phone = ph;
            // colliders at the feet/shins while standing (the hatchling must go around)
            var cols = new List<Collider>();
            foreach (var shin in new[] { pup.shinL, pup.shinR, pup.footL, pup.footR })
            {
                var c = shin.gameObject.AddComponent<CapsuleCollider>();
                c.radius = 0.06f;
                c.height = 0.45f;
                c.center = new Vector3(0, 0.2f, 0);
                c.enabled = false;
                cols.Add(c);
            }
            var steps = root.gameObject.AddComponent<AudioSource>();
            steps.spatialBlend = 1;
            steps.minDistance = 0.5f;
            steps.maxDistance = 25f;
            steps.playOnAwake = false;
            var giant = root.gameObject.AddComponent<GiantHuman>();
            Set(giant, "puppet", pup);
            Set(giant, "lampBeam", beam);
            Set(giant, "phoneFlash", fl);
            Set(giant, "standingColliders", cols.ToArray());
            Set(giant, "steps", steps);
            EditorUtility.SetDirty(pup);
            return giant;
        }
    }
}
