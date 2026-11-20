using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SameShore
{
    public enum Perception { Human, Hatchling }

    /// <summary>
    /// Switches how the world is perceived: colour grading (dull human view vs. hatchling hues),
    /// camera clip planes for the 1:20 scale, fog, and the hatchling attention/light-pull overlay.
    /// Head tracking is never modified.
    /// </summary>
    public class PerceptionController : MonoBehaviour
    {
        public static PerceptionController Instance { get; private set; }

        [Header("Post-processing volumes (global)")]
        [SerializeField] private Volume humanVolume;
        [SerializeField] private Volume hatchlingVolume;
        [SerializeField] private float blendSeconds = 1.5f;

        [Header("Camera")]
        [SerializeField] private float humanNear = 0.05f;
        [SerializeField] private float hatchlingNear = 0.003f;
        [SerializeField] private float humanFar = 1500f;
        [SerializeField] private float hatchlingFar = 600f;

        [Header("Fog")]
        [SerializeField] private Color humanFog = new Color(0.03f, 0.045f, 0.075f);
        [SerializeField] private float humanFogDensity = 0.012f;
        [SerializeField] private Color hatchlingFog = new Color(0.04f, 0.04f, 0.09f);
        [SerializeField] private float hatchlingFogDensity = 0.03f;

        [Header("Hatchling attention cone")]
        [Tooltip("The hatchling's attention is restricted to ~45 deg around its body (PDF: 'Look 45 degrees').")]
        [SerializeField] private float attentionInner = 45f;
        [SerializeField] private float attentionOuter = 100f;
        [SerializeField, Range(0, 1)] private float attentionDarkness = 0.8f;
        [SerializeField] private float overlayRadius = 0.3f;

        public Perception Current { get; private set; } = Perception.Human;
        public Transform Body { get; set; }
        public Vector3 PullDirection { get; set; } = Vector3.forward;
        public float PullStrength { get; set; }

        GameObject _overlay;
        Material _overlayMat;
        Coroutine _blend;

        void Awake()
        {
            Instance = this;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
        }

        void Start()
        {
            EnablePostProcessing();
            Apply(Perception.Human, true);
        }

        void EnablePostProcessing()
        {
            foreach (Camera cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null) data.renderPostProcessing = true;
            }
        }

        public void Apply(Perception p, bool immediate = false)
        {
            Current = p;
            bool turtle = p == Perception.Hatchling;
            if (_blend != null) StopCoroutine(_blend);
            _blend = StartCoroutine(Blend(turtle ? 0 : 1, turtle ? 1 : 0, immediate ? 0 : blendSeconds));

            RenderSettings.fogColor = turtle ? hatchlingFog : humanFog;
            RenderSettings.fogDensity = turtle ? hatchlingFogDensity : humanFogDensity;

            foreach (Camera cam in PlayerRig.Rig != null ? PlayerRig.Rig.GetComponentsInChildren<Camera>(true) : new Camera[0])
            {
                cam.nearClipPlane = turtle ? hatchlingNear : humanNear;
                cam.farClipPlane = turtle ? hatchlingFar : humanFar;
                cam.clearFlags = CameraClearFlags.Skybox;
            }
            SetOverlay(turtle);
        }

        IEnumerator Blend(float human, float hatch, float seconds)
        {
            float h0 = humanVolume ? humanVolume.weight : 0, t0 = hatchlingVolume ? hatchlingVolume.weight : 0;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                if (humanVolume) humanVolume.weight = Mathf.Lerp(h0, human, k);
                if (hatchlingVolume) hatchlingVolume.weight = Mathf.Lerp(t0, hatch, k);
                yield return null;
            }
            if (humanVolume) humanVolume.weight = human;
            if (hatchlingVolume) hatchlingVolume.weight = hatch;
        }

        void SetOverlay(bool on)
        {
            if (on && _overlay == null && PlayerRig.Head != null)
            {
                _overlay = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _overlay.name = "~HatchlingVision";
                Destroy(_overlay.GetComponent<Collider>());
                _overlay.transform.SetParent(PlayerRig.Head, false);
                _overlay.transform.localScale = Vector3.one * overlayRadius * 2;
                _overlayMat = new Material(SameShoreAssets.I.turtleVision);
                var r = _overlay.GetComponent<MeshRenderer>();
                r.sharedMaterial = _overlayMat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            if (_overlay != null) _overlay.SetActive(on);
        }

        void LateUpdate()
        {
            if (_overlay == null || !_overlay.activeSelf || _overlayMat == null) return;
            // keep the sphere centred on the head but world-aligned so the shader uses world directions
            _overlay.transform.rotation = Quaternion.identity;
            Vector3 f = Body != null ? Body.forward : PlayerRig.FlatForward;
            f = Quaternion.AngleAxis(-12f, Vector3.Cross(Vector3.up, f).normalized) * f; // attend slightly upward (horizon)
            _overlayMat.SetVector("_BodyForward", f);
            _overlayMat.SetFloat("_InnerAngle", attentionInner);
            _overlayMat.SetFloat("_OuterAngle", attentionOuter);
            _overlayMat.SetFloat("_MaxDarkness", attentionDarkness);
            _overlayMat.SetVector("_PullDir", PullDirection);
            _overlayMat.SetFloat("_PullStrength", Mathf.Clamp01(PullStrength));
        }
    }
}
