using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SameShore.EditorTools
{
    /// <summary>Batch diagnostics: where key props ended up and what surrounds them.</summary>
    public static class SameShoreProbe
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/SameShore/Scenes/SameShore.unity");
            Physics.SyncTransforms();
            var sb = new StringBuilder();
            foreach (var name in new[] { "Flashlight", "Towel", "Umbrella", "BeachChair", "SideTable", "Cooler", "LitterBin", "Sandcastle", "Hole", "BagLanding", "MusicPlayer", "LightReel", "SandPail", "Shovel" })
            {
                var go = GameObject.Find(name);
                if (go == null) { sb.AppendLine($"{name}: NOT FOUND"); continue; }
                Vector3 p = go.transform.position;
                float g = SameShoreInspector.SampleHeight(p);
                sb.Append($"{name}: pos {p} ground {g:F3} (y-ground {p.y - g:F3}) active {go.activeInHierarchy}; near:");
                foreach (var c in Physics.OverlapSphere(p, 0.35f, ~0, QueryTriggerInteraction.Collide))
                    if (!c.transform.IsChildOf(go.transform)) sb.Append($" [{c.name}{(c.isTrigger ? " trig" : "")}]");
                sb.AppendLine();
                var vg = go.GetComponent<VRGrabbable>();
                if (vg != null) sb.AppendLine($"   grabbable enabled {vg.grabbingEnabled}, colliders {go.GetComponentsInChildren<Collider>().Length}");
            }
            Debug.Log("[SameShoreProbe]\n" + sb);
            EditorApplication.Exit(0);
        }
    }
}
