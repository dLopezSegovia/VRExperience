using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// Visible beam for a spot light (cheap additive cone mesh). Follows the light's range/angle and
    /// on/off state, with a short flicker-free fade so switching the flashlight off reads clearly.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class LightConeVisual : MonoBehaviour
    {
        [SerializeField] private float lengthFraction = 0.55f;
        [SerializeField] private float intensity = 0.22f;

        Light _light;
        MeshRenderer _renderer;
        Material _mat;
        float _k;
        static Mesh _cone;

        void Awake()
        {
            _light = GetComponent<Light>();
            var go = new GameObject("BeamCone");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = Cone();
            _renderer = go.AddComponent<MeshRenderer>();
            _mat = new Material(SameShoreAssets.I.lightCone);
            _renderer.sharedMaterial = _mat;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _k = _light.enabled ? 1 : 0;
        }

        void LateUpdate()
        {
            float len = _light.range * lengthFraction;
            float radius = Mathf.Tan(_light.spotAngle * 0.5f * Mathf.Deg2Rad) * len * 0.8f;
            _renderer.transform.localScale = new Vector3(radius, radius, len);
            _k = Mathf.MoveTowards(_k, _light.enabled && _light.isActiveAndEnabled ? 1 : 0, Time.deltaTime * 6f);
            _renderer.enabled = _k > 0.01f;
            Color c = _light.color;
            c.a = intensity * _k;
            _mat.SetColor("_Color", c);
        }

        static Mesh Cone()
        {
            if (_cone != null) return _cone;
            const int seg = 24, rings = 6;
            var v = new Vector3[(seg + 1) * (rings + 1)];
            var n = new Vector3[v.Length];
            var uv = new Vector2[v.Length];
            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;
                for (int s = 0; s <= seg; s++)
                {
                    float a = s / (float)seg * Mathf.PI * 2;
                    int i = r * (seg + 1) + s;
                    v[i] = new Vector3(Mathf.Cos(a) * (0.02f + t), Mathf.Sin(a) * (0.02f + t), t);
                    n[i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), -0.5f).normalized;
                    uv[i] = new Vector2(s / (float)seg, t);
                }
            }
            var tri = new int[seg * rings * 6];
            int k = 0;
            for (int r = 0; r < rings; r++)
            for (int s = 0; s < seg; s++)
            {
                int i = r * (seg + 1) + s;
                tri[k++] = i; tri[k++] = i + seg + 1; tri[k++] = i + 1;
                tri[k++] = i + 1; tri[k++] = i + seg + 1; tri[k++] = i + seg + 2;
            }
            _cone = new Mesh { name = "BeamCone", vertices = v, normals = n, uv = uv, triangles = tri };
            _cone.RecalculateBounds();
            return _cone;
        }
    }
}
