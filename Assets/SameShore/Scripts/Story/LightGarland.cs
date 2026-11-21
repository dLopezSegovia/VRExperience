using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SameShore
{
    // =====================================================================================================
    // Scene 6: the light string
    // =====================================================================================================

    /// <summary>
    /// Warm festoon lights. The player carries the reel from the umbrella to each post and clips the string on
    /// its hook (touch the hook with the hand holding the reel, or press the trigger near it). Each span is a
    /// sagging cable (parabolic, no rope physics) with bulbs. When the last post is reached the string lights
    /// up, bulb after bulb. The same lights then pull the hatchling off course (LightSource).
    /// </summary>
    public class LightGarland : MonoBehaviour, IVRUsable
    {
        [SerializeField] private Transform[] hooks;          // 0 = umbrella, then the posts in order
        [SerializeField] private VRGrabbable reel;
        [SerializeField] private float hookRadius = 0.4f;
        [SerializeField] private float bulbSpacing = 0.45f;
        [SerializeField] private float sagPerMetre = 0.17f;      // deep festoon: the middle bulbs hang low over the sand
        [SerializeField] private Mesh bulbMesh;
        [SerializeField] private Material cableMaterial;
        [SerializeField] private Material bulbOffMaterial;
        [SerializeField] private Material bulbOnMaterial;
        [SerializeField] private float lightStrength = 1.35f;
        [Header("Glow (cheap on Quest: additive halos + one point light per span)")]
        [SerializeField] private Material haloMaterial;
        [SerializeField] private Material poolMaterial;
        [SerializeField] private float haloSize = 0.32f;
        [SerializeField] private float spanLightIntensity = 2.4f;
        [SerializeField] private float spanLightRange = 6f;

        public int Hooked { get; private set; }
        public int HooksTotal => hooks != null ? hooks.Length : 0;
        public int Spans => Mathf.Max(0, Hooked - 1);
        public bool Complete => Hooked >= HooksTotal;
        public bool LightsOn { get; private set; }
        public Transform NextHook => !Complete ? hooks[Hooked] : null;
        public Transform[] Hooks => hooks;
        public VRGrabbable Reel => reel;

        readonly List<Renderer> _bulbs = new List<Renderer>();
        readonly List<Light> _lights = new List<Light>();
        readonly List<GameObject> _glows = new List<GameObject>();
        readonly List<Vector3> _bulbPositions = new List<Vector3>();

        /// <summary>World positions of every bulb (the hatchling's light hazard reads them).</summary>
        public IReadOnlyList<Vector3> BulbPositions => _bulbPositions;
        LineRenderer _live;

        void Start()
        {
            _live = gameObject.AddComponent<LineRenderer>();
            _live.positionCount = 12;
            _live.widthMultiplier = 0.008f;
            _live.sharedMaterial = cableMaterial;
            _live.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _live.enabled = false;
            foreach (var h in hooks) Highlight(h, false);
        }

        public void OnUse(VRHand hand)
        {
            // trigger with the reel near the next hook (accessibility / desktop)
            if (!Complete && hand != null && (hand.Position - hooks[Hooked].position).magnitude < 1.6f) Hook();
        }

        void Update()
        {
            if (Complete || reel == null) { if (_live != null) _live.enabled = false; return; }
            Transform next = hooks[Hooked];
            Highlight(next, reel.IsHeld);
            if (reel.IsHeld)
            {
                Vector3 hand = reel.Holder.Position;
                if ((hand - next.position).magnitude < hookRadius || (reel.transform.position - next.position).magnitude < hookRadius) Hook();
            }
            // the loose string from the last hook to the reel
            if (Hooked > 0)
            {
                _live.enabled = true;
                Vector3 a = hooks[Hooked - 1].position, b = reel.transform.position;
                var pts = MeshKit.Sag(a, b, Mathf.Clamp((a - b).magnitude * 0.12f, 0.05f, 0.6f), _live.positionCount);
                _live.SetPositions(pts);
            }
        }

        void Highlight(Transform hook, bool on)
        {
            var marker = hook.Find("HookMarker");
            if (marker != null)
            {
                marker.gameObject.SetActive(on && !Complete && hook == NextHook);
                marker.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(Time.time * 5f));
            }
        }

        public void Hook()
        {
            if (Complete) return;
            Transform h = hooks[Hooked];
            Highlight(h, false);
            if (Hooked > 0) BuildSpan(hooks[Hooked - 1].position, h.position, Hooked);
            Hooked++;
            AudioOneShot.PlayAt(SameShoreAssets.I.Clip("hook_clip"), h.position, 0.8f, 0.4f, 8f);
            if (reel != null && reel.IsHeld) XRInput.Haptic(reel.Holder.hand, 0.4f, 0.06f);
            if (BeachLog.I != null) { BeachLog.I.LightAnchors.Add(h.position); BeachLog.I.Record($"light string clipped to hook {Hooked}", h.position); }
            if (Complete)
            {
                if (reel != null)
                {
                    if (reel.IsHeld) reel.Holder.ForceRelease();
                    reel.gameObject.SetActive(false);
                }
                StartCoroutine(SwitchOn());
            }
        }

        void BuildSpan(Vector3 a, Vector3 b, int index)
        {
            float len = (b - a).magnitude;
            float sag = len * sagPerMetre;
            var pts = MeshKit.Sag(a, b, sag, Mathf.Max(8, Mathf.CeilToInt(len * 4)));
            var cable = new GameObject("Span" + index);
            cable.transform.SetParent(transform, false);
            cable.AddComponent<MeshFilter>().sharedMesh = MeshKit.Tube("Cable", pts, 0.004f, 5, false);
            var mr = cable.AddComponent<MeshRenderer>();
            mr.sharedMaterial = cableMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            int n = Mathf.Max(2, Mathf.FloorToInt(len / bulbSpacing));
            for (int i = 1; i < n; i++)
            {
                float t = i / (float)n;
                Vector3 p = Vector3.Lerp(a, b, t) + Vector3.down * (4f * sag * t * (1f - t));
                var bulb = new GameObject("Bulb");
                bulb.transform.SetParent(cable.transform, false);
                bulb.transform.position = p + Vector3.down * 0.03f;
                bulb.AddComponent<MeshFilter>().sharedMesh = bulbMesh;
                var br = bulb.AddComponent<MeshRenderer>();
                br.sharedMaterial = LightsOn ? bulbOnMaterial : bulbOffMaterial;
                br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _bulbs.Add(br);
                _bulbPositions.Add(bulb.transform.position);
                if (haloMaterial != null)
                {
                    // soft warm halo around the bulb (faces the viewer): the glow that bloom then spreads
                    var halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    halo.name = "Halo";
                    Destroy(halo.GetComponent<Collider>());
                    halo.transform.SetParent(bulb.transform, false);
                    halo.transform.position = bulb.transform.position + Vector3.down * 0.012f;
                    halo.transform.localScale = Vector3.one * haloSize * (0.85f + 0.3f * Mathf.PerlinNoise(p.x * 3f, p.z * 3f));
                    var hr = halo.GetComponent<MeshRenderer>();
                    hr.sharedMaterial = haloMaterial;
                    hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    hr.receiveShadows = false;
                    halo.AddComponent<Billboard>();
                    halo.SetActive(LightsOn);
                    _glows.Add(halo);
                }
            }
            if (poolMaterial != null)
            {
                // warm pool of light on the sand under the low middle of the span (baked look, no extra light)
                Vector3 low = Vector3.Lerp(a, b, 0.5f) + Vector3.down * sag;
                var pool = GameObject.CreatePrimitive(PrimitiveType.Quad);
                pool.name = "LightPool";
                Destroy(pool.GetComponent<Collider>());
                pool.transform.SetParent(cable.transform, false);
                pool.transform.position = MeshKit.OnGround(low, 0.015f);
                Vector3 along = b - a; along.y = 0;
                pool.transform.rotation = Quaternion.LookRotation(Vector3.down, along.normalized);
                pool.transform.localScale = new Vector3(2.4f, Mathf.Max(2.6f, along.magnitude * 0.9f), 1f);
                var pr = pool.GetComponent<MeshRenderer>();
                pr.sharedMaterial = poolMaterial;
                pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                pr.receiveShadows = false;
                pool.SetActive(LightsOn);
                _glows.Add(pool);
            }
            // one real light per span (cheap on Quest), also what the hatchling perceives
            var lg = new GameObject("SpanLight");
            lg.transform.SetParent(cable.transform, false);
            lg.transform.position = Vector3.Lerp(a, b, 0.5f) + Vector3.down * (sag + 0.05f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.72f, 0.42f);
            l.range = spanLightRange;
            l.intensity = spanLightIntensity;
            l.shadows = LightShadows.None;
            l.enabled = LightsOn;
            var src = lg.AddComponent<LightSource>();
            src.Strength = lightStrength;
            _lights.Add(l);
        }

        IEnumerator SwitchOn()
        {
            yield return new WaitForSeconds(0.6f);
            AudioOneShot.PlayAt(SameShoreAssets.I.Clip("lights_on"), hooks[0].position, 0.8f, 1f, 20f);
            for (int i = 0; i < _bulbs.Count; i++)
            {
                _bulbs[i].sharedMaterial = bulbOnMaterial;
                if (i % 4 == 0) yield return null;
            }
            foreach (var l in _lights) l.enabled = true;
            foreach (var g in _glows) if (g != null) g.SetActive(true);
            LightsOn = true;
            if (BeachLog.I != null) { BeachLog.I.LightsInstalled = true; BeachLog.I.Record("light string switched on", hooks[0].position); }
        }

        /// <summary>Skip / defaults: the whole string installed and lit.</summary>
        public void ForceComplete()
        {
            while (!Complete) Hook();
            StopAllCoroutines();
            foreach (var b in _bulbs) b.sharedMaterial = bulbOnMaterial;
            foreach (var l in _lights) l.enabled = true;
            foreach (var g in _glows) if (g != null) g.SetActive(true);
            LightsOn = true;
            if (BeachLog.I != null) BeachLog.I.LightsInstalled = true;
        }
    }
}
