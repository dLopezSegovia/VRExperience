using System.Collections.Generic;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// Footprints pressed into the sand. Humans leave them while walking; for the hatchling each
    /// print is a small depression that slows it down ("footprints, can get trapped").
    /// They are scene objects, so they persist from the human phase into the hatchling phase.
    /// </summary>
    public class FootprintSystem : MonoBehaviour
    {
        [SerializeField] private Material footprintMaterial;
        [SerializeField] private Mesh printMesh;
        [SerializeField] private int maxPrints = 160;
        [SerializeField] private Vector2 printSize = new Vector2(0.11f, 0.27f);
        [SerializeField, Range(0.05f, 1f)] private float hatchlingSpeedMultiplier = 0.6f;

        readonly Queue<GameObject> _prints = new Queue<GameObject>();
        static Mesh _quad;

        public int Count => _prints.Count;

        public void Add(Vector3 headPos, Vector3 forward, bool left)
        {
            if (!GroundHit(headPos, out RaycastHit hit)) return;
            GameObject p = CreatePrint(transform, hit.point, hit.normal, forward, left, footprintMaterial, printSize,
                                       hatchlingSpeedMultiplier, printMesh);
            _prints.Enqueue(p);
            while (_prints.Count > maxPrints) Destroy(_prints.Dequeue());
        }

        public static bool GroundHit(Vector3 above, out RaycastHit ground)
        {
            ground = default;
            var hits = Physics.RaycastAll(above + Vector3.up * 0.5f, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            foreach (var h in hits)
            {
                if (!(h.collider is TerrainCollider) || h.distance >= best) continue;
                best = h.distance;
                ground = h;
                found = true;
            }
            return found;
        }

        /// <summary>Also used by the editor scene builder to pre-place older footprint trails.</summary>
        public static GameObject CreatePrint(Transform parent, Vector3 point, Vector3 normal, Vector3 forward, bool left,
                                             Material mat, Vector2 size, float speedMultiplier, Mesh mesh = null)
        {
            if (mesh == null)
            {
                if (_quad == null) _quad = BuildQuad();
                mesh = _quad;
            }
            var go = new GameObject(left ? "Footprint_L" : "Footprint_R");
            go.transform.SetParent(parent, true);
            Vector3 f = Vector3.ProjectOnPlane(forward, normal).normalized;
            if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
            // feet point slightly outwards
            f = Quaternion.AngleAxis(left ? -6f : 6f, normal) * f;
            go.transform.SetPositionAndRotation(point + normal * 0.004f, Quaternion.LookRotation(f, normal));
            go.transform.localScale = new Vector3(size.x, 1, size.y);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(0.8f, 0.04f, 0.85f);
            var zone = go.AddComponent<TurtleZone>();
            zone.Type = TurtleZoneType.Slow;
            zone.SpeedMultiplier = speedMultiplier;
            return go;
        }

        public static Mesh BuildQuad()
        {
            var m = new Mesh { name = "FootprintQuad" };
            m.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            m.RecalculateBounds();
            return m;
        }
    }
}
