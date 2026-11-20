using System.Collections.Generic;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// A sandcastle hole dug into the terrain. At runtime the terrain data is cloned (the asset is
    /// never modified) and a real depression with a rim of dug sand is carved, so the hatchling can
    /// physically fall in. Pouring sand from a bucket raises it back until it is filled.
    /// </summary>
    public class SandHole : MonoBehaviour
    {
        public static readonly List<SandHole> All = new List<SandHole>();

        [SerializeField] private Terrain terrain;
        [SerializeField] private float radius = 0.5f;
        [SerializeField] private float depth = 0.3f;
        [SerializeField] private float rimHeight = 0.07f;
        [SerializeField] private float rimWidth = 0.35f;
        [Tooltip("How much of the hole one full bucket fills (0..1).")]
        [SerializeField] private float fillPerBucket = 0.5f;
        [SerializeField] private TurtleZone trapZone;
        [SerializeField] private GameObject[] disableWhenFilled;

        public float Fill01 { get; private set; }
        public bool IsFilled => Fill01 >= 0.97f;
        public float Radius => radius;
        public float FillPerBucket => fillPerBucket;

        TerrainData _data;
        float[,] _orig;
        int _x0, _y0, _w, _h;
        float _lastApplied = -1f;
        bool _dirty;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            if (terrain == null) terrain = Terrain.activeTerrain;
            _data = TerrainCloner.EnsureClone(terrain);
            Vector3 size = _data.size;
            int res = _data.heightmapResolution;
            float outer = radius + rimWidth;
            Vector3 local = transform.position - terrain.transform.position;
            _x0 = Mathf.Clamp(Mathf.FloorToInt((local.x - outer) / size.x * (res - 1)), 0, res - 1);
            _y0 = Mathf.Clamp(Mathf.FloorToInt((local.z - outer) / size.z * (res - 1)), 0, res - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((local.x + outer) / size.x * (res - 1)), 0, res - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt((local.z + outer) / size.z * (res - 1)), 0, res - 1);
            _w = x1 - _x0 + 1;
            _h = y1 - _y0 + 1;
            _orig = _data.GetHeights(_x0, _y0, _w, _h);
            Apply(0f, true);
        }

        public void AddSand(float bucketFraction)
        {
            if (IsFilled) return;
            Fill01 = Mathf.Clamp01(Fill01 + bucketFraction * fillPerBucket);
            if (Fill01 - _lastApplied > 0.03f || IsFilled) Apply(Fill01, false);
            if (IsFilled)
            {
                Fill01 = 1f;
                Apply(1f, true);
                if (trapZone != null)
                {
                    trapZone.enabled = false;
                    foreach (var c in trapZone.GetComponents<Collider>()) c.enabled = false;
                }
                foreach (var go in disableWhenFilled) if (go != null) go.SetActive(false);
                AudioOneShot.PlayAt(SameShoreAssets.I.success, transform.position, 0.25f, 1f, 10f, 1.5f);
            }
        }

        void Apply(float fill, bool syncCollider)
        {
            _lastApplied = fill;
            Vector3 size = _data.size;
            int res = _data.heightmapResolution;
            var h = new float[_h, _w];
            Vector3 tp = terrain.transform.position;
            for (int y = 0; y < _h; y++)
            for (int x = 0; x < _w; x++)
            {
                float wx = tp.x + (_x0 + x) / (float)(res - 1) * size.x;
                float wz = tp.z + (_y0 + y) / (float)(res - 1) * size.z;
                float r = new Vector2(wx - transform.position.x, wz - transform.position.z).magnitude;
                float delta = 0f;
                if (r < radius)
                {
                    float k = r / radius;
                    delta = -depth * (1f - k * k) * (1f - fill);   // bowl
                }
                else if (r < radius + rimWidth)
                {
                    float k = (r - radius) / rimWidth;
                    delta = rimHeight * Mathf.Sin(k * Mathf.PI) * (1f - fill); // dug sand piled around
                }
                h[y, x] = _orig[y, x] + delta / size.y;
            }
            _data.SetHeightsDelayLOD(_x0, _y0, h);
            _dirty = true;
            if (syncCollider) Sync();
        }

        void LateUpdate()
        {
            if (_dirty && Time.frameCount % 10 == 0) Sync();
        }

        void Sync()
        {
            _data.SyncHeightmap();
            _dirty = false;
        }
    }

    /// <summary>Clones a terrain's data once at runtime so gameplay changes never touch the asset.</summary>
    public static class TerrainCloner
    {
        static readonly Dictionary<Terrain, TerrainData> Clones = new Dictionary<Terrain, TerrainData>();

        public static TerrainData EnsureClone(Terrain t)
        {
            if (Clones.TryGetValue(t, out TerrainData d) && d != null) return d;
            d = Object.Instantiate(t.terrainData);
            d.name = t.terrainData.name + " (runtime)";
            t.terrainData = d;
            var col = t.GetComponent<TerrainCollider>();
            if (col != null) col.terrainData = d;
            Clones[t] = d;
            return d;
        }
    }
}
