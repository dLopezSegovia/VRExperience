using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// Discreet spatial indicator for the current objective: a soft glowing diamond floating above
    /// the target, depth-tested (it sits in the world, not on the screen), gently bobbing, and fading
    /// out when the player is close enough to see the object itself.
    /// </summary>
    public class ObjectiveMarker : MonoBehaviour
    {
        public float height = 0.75f;
        public float size = 0.16f;
        public float hideDistance = 1.3f;

        Transform _quad;
        Material _mat;
        Vector3? _target;
        float _alpha;
        Vector3 _pos;

        void Awake()
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = "MarkerQuad";
            q.transform.SetParent(transform, false);
            _quad = q.transform;
            var shader = Shader.Find("SameShore/SkyBillboard");
            _mat = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit"));
            _mat.SetTexture("_MainTex", MarkerTexture());
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = _mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            gameObject.SetActive(true);
        }

        public void SetTarget(Vector3? target)
        {
            if (target.HasValue && !_target.HasValue) _pos = target.Value + Vector3.up * height * PlayerRig.Scale;
            _target = target;
        }

        void LateUpdate()
        {
            Transform head = PlayerRig.Head;
            float want = 0f;
            if (_target.HasValue && head != null)
            {
                float s = PlayerRig.Scale;
                Vector3 goal = _target.Value + Vector3.up * (height * s + Mathf.Sin(Time.time * 2.2f) * 0.04f * s);
                _pos = Vector3.Lerp(_pos, goal, 1 - Mathf.Exp(-8f * Time.deltaTime));
                Vector3 d = _pos - head.position;
                float dist = new Vector2(d.x, d.z).magnitude / Mathf.Max(s, 1e-4f);
                want = Mathf.Clamp01((dist - hideDistance) / 1.2f);
                transform.position = _pos;
                transform.rotation = Quaternion.LookRotation(_pos - head.position, Vector3.up);
                // keep a readable angular size at distance
                float k = Mathf.Clamp(dist / 6f, 1f, 3f);
                _quad.localScale = Vector3.one * size * s * k;
                _quad.localRotation = Quaternion.Euler(0, 0, 45f);
            }
            _alpha = Mathf.MoveTowards(_alpha, want, Time.deltaTime * 2.5f);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 3f);
            _mat.SetColor("_Color", new Color(1f, 0.82f, 0.45f, _alpha * pulse));
            _quad.gameObject.SetActive(_alpha > 0.01f);
        }

        static Texture2D MarkerTexture()
        {
            const int n = 128;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2 - 1, v = (y + 0.5f) / n * 2 - 1;
                // square (rotated 45 deg by the quad -> diamond): outline + soft core + glow
                float d = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
                float outline = Mathf.Clamp01(1 - Mathf.Abs(d - 0.55f) / 0.07f);
                float core = Mathf.Clamp01(1 - d / 0.25f);
                float glow = Mathf.Exp(-d * 4f) * 0.35f;
                float a = Mathf.Clamp01(outline + core * 0.8f + glow);
                t.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            t.Apply();
            return t;
        }
    }
}
