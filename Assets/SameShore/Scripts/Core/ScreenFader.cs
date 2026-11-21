using System.Collections;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// VR-safe fade: a sphere around the head drawn on top of everything. All player
    /// relocations happen while fully faded, so the camera never jumps in view.
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        public static ScreenFader Instance { get; private set; }

        [SerializeField] private Color color = Color.black;
        [SerializeField] private float radius = 0.4f;

        public float Alpha { get; private set; }
        Material _mat;
        GameObject _sphere;

        void Awake()
        {
            Instance = this;
        }

        void EnsureSphere()
        {
            if (_sphere != null) return;
            Transform head = PlayerRig.Head;
            if (head == null) return;
            _sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _sphere.name = "~FadeSphere";
            Destroy(_sphere.GetComponent<Collider>());
            _sphere.transform.SetParent(head, false);
            _sphere.transform.localScale = Vector3.one * radius * 2f;
            var r = _sphere.GetComponent<MeshRenderer>();
            _mat = new Material(SameShoreAssets.I.fade);
            r.sharedMaterial = _mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            Apply(Alpha);
        }

        /// <summary>Fade colour (black by default; deep teal under the final wave).</summary>
        public void SetColor(Color c)
        {
            color = c;
            Apply(Alpha);
        }

        public void SetImmediate(float a)
        {
            EnsureSphere();
            Apply(a);
        }

        /// <summary>Sets the veil immediately (driven frame by frame by a sequence).</summary>
        public void SetAlphaDirect(float a)
        {
            EnsureSphere();
            Apply(Mathf.Clamp01(a));
        }

        public Coroutine FadeTo(float target, float seconds) => StartCoroutine(Run(target, seconds));

        IEnumerator Run(float target, float seconds)
        {
            EnsureSphere();
            float start = Alpha;
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                Apply(Mathf.Lerp(start, target, Mathf.SmoothStep(0, 1, t / seconds)));
                yield return null;
            }
            Apply(target);
        }

        /// <summary>Brief coloured flash (e.g. a seagull strike) that returns to the current fade state.</summary>
        public void Flash(Color c, float peak, float seconds) => StartCoroutine(RunFlash(c, peak, seconds));

        IEnumerator RunFlash(Color c, float peak, float seconds)
        {
            EnsureSphere();
            Color baseColor = color;
            float baseAlpha = Alpha;
            color = c;
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                Apply(Mathf.Max(baseAlpha, peak * (1f - t / seconds)));
                yield return null;
            }
            color = baseColor;
            Apply(baseAlpha);
        }

        void Apply(float a)
        {
            Alpha = a;
            if (_mat == null) return;
            Color c = color;
            c.a = a;
            _mat.SetColor("_Color", c);
            _sphere.SetActive(a > 0.001f);
        }
    }
}
