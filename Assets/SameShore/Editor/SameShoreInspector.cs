using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SameShore.EditorTools
{
    /// <summary>
    /// Batch-mode diagnostics: dumps terrain / layout data of a scene and renders reference
    /// screenshots. Used to validate the generated scene without a headset.
    ///   Unity -batchmode -executeMethod SameShore.EditorTools.SameShoreInspector.Inspect
    ///         -scene Assets/... -out C:/somewhere
    /// </summary>
    public static class SameShoreInspector
    {
        public static void Inspect()
        {
            string scene = Arg("-scene", "Assets/Scenes/SampleScene.unity");
            string outDir = Arg("-out", Path.GetFullPath("Logs/SameShoreInspect"));
            Directory.CreateDirectory(outDir);
            EditorSceneManager.OpenScene(scene);

            var sb = new StringBuilder();
            foreach (Terrain t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                TerrainData d = t.terrainData;
                sb.AppendLine($"TERRAIN {t.name} pos={t.transform.position} size={d.size} hmRes={d.heightmapResolution} " +
                              $"alphaRes={d.alphamapResolution} layers={d.terrainLayers.Length} asset={AssetDatabase.GetAssetPath(d)} " +
                              $"pixelError={t.heightmapPixelError} mat={(t.materialTemplate ? t.materialTemplate.name : "null")}");
            }

            sb.AppendLine("HEIGHT PROFILE (x = 0, 3, 6, 9)");
            for (float z = -45; z <= 45; z += 2.5f)
            {
                sb.Append($"z={z,6:F1}:");
                foreach (float x in new[] { -6f, 0f, 3f, 6f, 9f, 15f })
                    sb.Append($" {SampleHeight(new Vector3(x, 0, z)),7:F2}");
                sb.AppendLine();
            }

            foreach (string name in new[] { "WaterBlock_50m", "SeaGoal", "SeaGlow", "TurtleSpawn", "TurtleBody", "Hole",
                                             "Flashlight", "plastic_monobloc_chair_01_1k", "Wooden_Bucket", "Wooden_Bucket (1)",
                                             "[BuildingBlock] Camera Rig", "Moon", "BoundariesWater", "BoundariesBack",
                                             "BoundariesL", "BoundariesR", "EndCanvas", "EndPopup", "Teleport Hotspot" })
            {
                GameObject go = GameObject.Find(name);
                if (go == null) { sb.AppendLine($"OBJ {name}: NOT FOUND"); continue; }
                Bounds b = WorldBounds(go);
                sb.AppendLine($"OBJ {name}: pos={go.transform.position:F3} rot={go.transform.eulerAngles:F1} " +
                              $"scale={go.transform.lossyScale:F3} bounds c={b.center:F2} s={b.size:F2} active={go.activeInHierarchy}");
                foreach (Component c in go.GetComponents<Component>())
                {
                    if (c is CharacterController cc) sb.AppendLine($"   CC r={cc.radius} h={cc.height} c={cc.center} slope={cc.slopeLimit} step={cc.stepOffset} skin={cc.skinWidth}");
                    if (c is Light l) sb.AppendLine($"   LIGHT {l.type} int={l.intensity} range={l.range} col={l.color} spot={l.spotAngle}");
                    if (c is Rigidbody rb) sb.AppendLine($"   RB mass={rb.mass} kin={rb.isKinematic}");
                }
            }

            foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.AppendLine($"CAMERA {cam.name} near={cam.nearClipPlane} far={cam.farClipPlane} clear={cam.clearFlags}");
            sb.AppendLine($"RENDER fog={RenderSettings.fog} ambientMode={RenderSettings.ambientMode} sky={(RenderSettings.skybox ? RenderSettings.skybox.name : "null")}");
            sb.AppendLine($"PHYSICS contactOffset={Physics.defaultContactOffset} gravity={Physics.gravity}");
            sb.AppendLine($"QUALITY {QualitySettings.names[QualitySettings.GetQualityLevel()]} rp={(QualitySettings.renderPipeline ? QualitySettings.renderPipeline.name : "default")} " +
                          $"default rp={(UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline ? UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline.name : "null")}");

            File.WriteAllText(Path.Combine(outDir, "inspect.txt"), sb.ToString());

            string shots = Arg("-shots", "");
            foreach (string spec in shots.Split(';'))
            {
                // name|px,py,pz|rx,ry
                string[] p = spec.Split('|');
                if (p.Length < 3) continue;
                Vector3 pos = ParseV3(p[1]);
                string[] r = p[2].Split(',');
                Shot(Path.Combine(outDir, p[0] + ".png"), pos, Quaternion.Euler(F(r[0]), F(r[1]), 0),
                     p.Length > 3 ? F(p[3]) : 75f, p.Length > 4 ? F(p[4]) : 0.05f);
            }
            Debug.Log("[SameShoreInspector] done -> " + outDir);
        }

        public static float SampleHeight(Vector3 p)
        {
            if (Physics.Raycast(new Vector3(p.x, 200, p.z), Vector3.down, out RaycastHit hit, 400f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            foreach (Terrain t in Terrain.activeTerrains)
            {
                Vector3 local = p - t.transform.position;
                Vector3 size = t.terrainData.size;
                if (local.x >= 0 && local.z >= 0 && local.x <= size.x && local.z <= size.z)
                    return t.SampleHeight(p) + t.transform.position.y;
            }
            return float.NaN;
        }

        public static Bounds WorldBounds(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        public static void Shot(string path, Vector3 pos, Quaternion rot, float fov = 75f, float near = 0.05f,
                                int w = 1280, int h = 720)
        {
            var go = new GameObject("~ShotCamera");
            var cam = go.AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = fov;
            cam.nearClipPlane = near;
            cam.farClipPlane = 2000f;
            cam.allowHDR = true;
            var data = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            cam.Render(); // second pass lets temporal/terrain systems settle
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(go);
        }

        public static string Arg(string name, string fallback)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return fallback;
        }

        static float F(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

        static Vector3 ParseV3(string s)
        {
            string[] c = s.Split(',');
            return new Vector3(F(c[0]), F(c[1]), F(c[2]));
        }
    }
}
