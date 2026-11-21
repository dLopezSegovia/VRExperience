using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SameShore.EditorTools
{
    /// <summary>Batch diagnostics: hatchling zones and what surrounds the dug hole.</summary>
    public static class SameShoreProbe
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/SameShore/Scenes/SameShore.unity");
            Physics.SyncTransforms();
            var sb = new StringBuilder();
            sb.AppendLine("--- TurtleZones");
            foreach (var z in Object.FindObjectsByType<TurtleZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var c in z.GetComponents<Collider>())
                    sb.AppendLine($"{z.name} type {z.Type} speed {z.SpeedMultiplier} enabled {z.enabled}/{c.enabled} {c.GetType().Name} center {c.bounds.center} size {c.bounds.size}");
            var hole = GameObject.Find("Hole");
            if (hole != null)
            {
                foreach (var c in hole.GetComponentsInChildren<Component>(true)) sb.AppendLine($"Hole part: {c.name} {c.GetType().Name}");
                sb.AppendLine("--- colliders within 1.3 m of the hole");
                foreach (var c in Physics.OverlapSphere(hole.transform.position, 1.3f, ~0, QueryTriggerInteraction.Collide))
                    sb.AppendLine($"{c.name} ({c.GetType().Name}{(c.isTrigger ? ", trigger" : "")}) at {c.bounds.center} size {c.bounds.size}");
            }
            Debug.Log("[SameShoreProbe]\n" + sb);
            EditorApplication.Exit(0);
        }
    }
}
