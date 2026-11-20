using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SameShore.EditorTools
{
    /// <summary>Small helpers shared by the Same Shore editor tools.</summary>
    public static class BuilderUtil
    {
        public const string Root = "Assets/SameShore";
        public const string Gen = Root + "/Generated";

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static GameObject Find(string name)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Transform t = FindDeep(root.transform, name);
                if (t != null) return t.gameObject;
            }
            return null;
        }

        public static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                Transform r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        // ------------------------------------------------------------------ serialized fields
        public static void Set(Object target, string prop, object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(prop);
            if (p == null)
            {
                Debug.LogWarning($"[SameShore] {target.GetType().Name}.{prop} not found");
                return;
            }
            switch (value)
            {
                case Object o: p.objectReferenceValue = o; break;
                case float f: p.floatValue = f; break;
                case int i:
                    if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex = i; else p.intValue = i;
                    break;
                case bool b: p.boolValue = b; break;
                case string s: p.stringValue = s; break;
                case Color c: p.colorValue = c; break;
                case Vector2 v2: p.vector2Value = v2; break;
                case Vector3 v3: p.vector3Value = v3; break;
                case Object[] arr:
                    p.arraySize = arr.Length;
                    for (int k = 0; k < arr.Length; k++) p.GetArrayElementAtIndex(k).objectReferenceValue = arr[k];
                    break;
                case null: p.objectReferenceValue = null; break;
                default: Debug.LogWarning($"[SameShore] unsupported value for {prop}"); break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ materials
        static Shader _lit;
        public static Shader Lit => _lit ? _lit : (_lit = Shader.Find("Universal Render Pipeline/Lit"));

        public static Material LitMat(string name, Color color, float smoothness = 0.3f, Texture baseMap = null,
                                      Texture normal = null, Texture metalSmooth = null, float metallic = 0f)
        {
            Material m = SaveMat(name, Lit);
            m.SetColor("_BaseColor", color);
            m.SetTexture("_BaseMap", baseMap);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (normal != null) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
            else m.DisableKeyword("_NORMALMAP");
            if (metalSmooth != null)
            {
                m.SetTexture("_MetallicGlossMap", metalSmooth);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetFloat("_Smoothness", 1f);
                m.SetFloat("_GlossMapScale", smoothness);
            }
            else m.DisableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(m);
            return m;
        }

        public static void MakeTransparent(Material m, bool doubleSided = false)
        {
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", 0);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("DepthOnly", false);
            if (doubleSided) m.SetFloat("_Cull", 0);
            EditorUtility.SetDirty(m);
        }

        public static void MakeCutout(Material m, float cutoff = 0.4f, bool doubleSided = true)
        {
            m.SetFloat("_AlphaClip", 1);
            m.SetFloat("_Cutoff", cutoff);
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)RenderQueue.AlphaTest;
            m.SetOverrideTag("RenderType", "TransparentCutout");
            if (doubleSided) m.SetFloat("_Cull", 0);
            EditorUtility.SetDirty(m);
        }

        public static void Emission(Material m, Color c)
        {
            m.SetColor("_EmissionColor", c);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
        }

        public static Material SaveMat(string name, Shader shader)
        {
            EnsureFolder(Gen + "/Materials");
            string path = $"{Gen}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;
            m.name = name;
            return m;
        }

        // ------------------------------------------------------------------ textures / meshes
        public static Texture2D SavePng(string name, Texture2D tex, bool sRGB = true, bool alpha = true,
                                        TextureWrapMode wrap = TextureWrapMode.Clamp, bool normal = false)
        {
            EnsureFolder(Gen + "/Textures");
            string path = $"{Gen}/Textures/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = sRGB;
            imp.alphaIsTransparency = alpha;
            imp.wrapMode = wrap;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static Mesh SaveMesh(string name, Mesh mesh)
        {
            EnsureFolder(Gen + "/Meshes");
            string path = $"{Gen}/Meshes/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        public static Texture2D Tex(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        public static void ConfigureTexture(string path, bool normal = false, bool sRGB = true, int maxSize = 1024)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            bool changed = false;
            var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (imp.textureType != type) { imp.textureType = type; changed = true; }
            if (imp.sRGBTexture != sRGB && !normal) { imp.sRGBTexture = sRGB; changed = true; }
            if (imp.maxTextureSize != maxSize) { imp.maxTextureSize = maxSize; changed = true; }
            if (changed) imp.SaveAndReimport();
        }

        // ------------------------------------------------------------------ scene helpers
        public static GameObject Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        public static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale,
                                      Material mat, bool keepCollider = false, Quaternion? rot = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        public static Bounds RendererBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Replace imported materials by name (case-insensitive contains) with project materials.</summary>
        public static void Remap(GameObject go, Dictionary<string, Material> map, Material fallback = null)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string n = mats[i] != null ? mats[i].name.Replace(" (Instance)", "").Trim().ToLowerInvariant() : "";
                    Material pick = fallback;
                    foreach (var kv in map)
                        if (n == kv.Key.ToLowerInvariant() || n.StartsWith(kv.Key.ToLowerInvariant() + ".")) { pick = kv.Value; break; }
                    if (pick != null) mats[i] = pick;
                }
                r.sharedMaterials = mats;
            }
        }
    }
}
