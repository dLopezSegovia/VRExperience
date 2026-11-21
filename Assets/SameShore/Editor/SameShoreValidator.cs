using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SameShore.EditorTools
{
    /// <summary>
    /// Checks the open scene for missing scripts and unassigned references on the project's own
    /// components (Same Shore + the team's Assets/Scripts). Menu: Same Shore / Validate Scene.
    /// </summary>
    public static class SameShoreValidator
    {
        // references that are optional by design
        static readonly string[] Optional =
        {
            "lensRenderer", "lensOnMaterial", "lensOffMaterial", "clickSound", "head", "playerRoot", "humanOnlyBehaviours",
            "controller", "body", "terrain", "disableWhenFilled", "chair", "menuSpot", "_data", "_grabSource", "_movementProvider",
            "grabbable", "sunArc", "swash"
        };

        [MenuItem("Same Shore/Validate Scene")]
        public static int Validate()
        {
            var sb = new StringBuilder();
            int issues = 0;
            var scene = EditorSceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if (missing > 0)
                {
                    issues++;
                    sb.AppendLine($"MISSING SCRIPT x{missing}: {Path(t)}");
                }
                foreach (MonoBehaviour mb in t.GetComponents<MonoBehaviour>())
                {
                    if (mb == null) continue;
                    var type = mb.GetType();
                    bool ours = (type.Namespace != null && type.Namespace.StartsWith("SameShore")) ||
                                type.Assembly.GetName().Name == "Assembly-CSharp" && type.Namespace == null;
                    if (!ours) continue;
                    var so = new SerializedObject(mb);
                    var it = so.GetIterator();
                    while (it.NextVisible(true))
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue != null) continue;
                        if (it.name == "m_Script" || Optional.Any(o => it.propertyPath.StartsWith(o))) continue;
                        if (it.propertyPath.Contains("Array.data")) { /* array element */ }
                        issues++;
                        sb.AppendLine($"NULL REF {type.Name}.{it.propertyPath} on {Path(t)}");
                    }
                }
            }
            var assets = Resources.Load<SameShoreAssets>("SameShoreAssets");
            if (assets == null) { issues++; sb.AppendLine("Resources/SameShoreAssets missing"); }
            else
            {
                var so = new SerializedObject(assets);
                var it = so.GetIterator();
                while (it.NextVisible(true))
                    if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue == null && it.name != "m_Script")
                    {
                        issues++;
                        sb.AppendLine("NULL ASSET SameShoreAssets." + it.propertyPath);
                    }
            }
            Debug.Log($"[SameShore] VALIDATION {(issues == 0 ? "OK" : "FOUND " + issues + " ISSUES")}\n{sb}");
            return issues;
        }

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    }
}
