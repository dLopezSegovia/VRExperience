using System;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// The seagull threat for the hatchling, designed as tension that can always be escaped:
    ///   1. Approach  - a call far away; the gull arrives and circles high.
    ///   2. Shadow    - it makes a low pass: its shadow sweeps across the sand next to the hatchling.
    ///   3. Stalking  - circles above; it will not attack while the hatchling is under cover.
    ///   4. Warning   - it hovers and screeches; a shrinking circle marks where it will strike.
    ///   5. Dive      - it stoops to that spot. Outside the circle (or under cover) = missed.
    ///   6. Leaving   - after a few attempts (or once the hatchling is in the swash) it gives up.
    /// A strike stuns briefly (no camera shake). Two strikes = caught -> the director respawns the
    /// hatchling at the last checkpoint and the episode can start again. It never blocks the run.
    /// </summary>
    public class GullHunt : MonoBehaviour
    {
        public enum Phase { Idle, Approach, ShadowPass, Stalking, Warning, Diving, Leaving, Done }

        [SerializeField] private Seagull hunter;
        [SerializeField] private TurtleController turtle;
        [SerializeField] private Transform sea;
        [Header("Tuning")]
        [SerializeField] private float triggerDistance = 9f;     // metres from the shoreline
        [SerializeField] private float safeDistance = 1.6f;      // the swash protects the hatchling
        [SerializeField] private float warningSeconds = 2.6f;
        [SerializeField] private float strikeRadius = 0.2f;
        [SerializeField] private float ringStartRadius = 0.55f;
        [SerializeField] private float stunSeconds = 1.3f;
        [SerializeField] private int maxAttacks = 3;
        [SerializeField] private int strikesToCatch = 2;

        public Phase Current { get; private set; } = Phase.Idle;
        public int Strikes { get; private set; }
        public int Attacks { get; private set; }
        public int Dodged { get; private set; }
        /// <summary>Over the whole run (episodes restart after a checkpoint respawn).</summary>
        public int TotalStrikes { get; private set; }
        public int TimesCaught { get; private set; }

        public void ResetTotals() { TotalStrikes = 0; TimesCaught = 0; EverSheltered = false; }
        public bool Escaped => Current == Phase.Done;
        public bool EverSheltered { get; private set; }
        public bool Active => Current != Phase.Idle && Current != Phase.Done;

        public event Action Caught;
        public event Action Survived;
        public event Action<string> Message;

        float _t;
        Vector3 _strike;
        Transform _shadow, _ring;
        Material _ringMat, _shadowMat;
        bool _armed;
        bool _warnedShelter;
        AudioSource _heart;

        void Start()
        {
            _shadow = Decal("GullShadow", SameShoreAssets.I.groundDecal, RadialTexture(false), out _shadowMat);
            _ring = Decal("StrikeRing", SameShoreAssets.I.warningRing, RadialTexture(true), out _ringMat);
            _heart = gameObject.AddComponent<AudioSource>();
            _heart.clip = SameShoreAssets.I.heartbeat;
            _heart.loop = true;
            _heart.spatialBlend = 0;
            _heart.volume = 0;
            _heart.Play();
            if (GameManager.Instance != null) GameManager.Instance.OnStateChanged += (a, b) => { if (b == GameState.Hatchling && !_armed) Arm(); };
        }

        /// <summary>Ready for a new episode (start of the hatchling phase, or after a checkpoint respawn).</summary>
        public void Arm(bool keepDoneState = false)
        {
            _armed = true;
            if (keepDoneState && Current == Phase.Done) return;
            Current = Phase.Idle;
            Strikes = Attacks = Dodged = 0;
            _warnedShelter = false;
            if (hunter != null) hunter.ReturnHome();
            _ring.gameObject.SetActive(false);
        }

        public void Stop()
        {
            if (Active && hunter != null) hunter.Leave(SeaDir());
            Current = Phase.Done;
            _ring.gameObject.SetActive(false);
        }

        float DistanceToSea => sea == null ? 99f : Mathf.Max(0f, sea.position.z - turtle.transform.position.z);
        Vector3 SeaDir() => sea != null ? Vector3.ProjectOnPlane(sea.position - turtle.transform.position, Vector3.up).normalized : Vector3.forward;

        void Update()
        {
            UpdateShadow();
            bool run = _armed && turtle != null && turtle.isActiveAndEnabled && GameManager.Instance != null && GameManager.Instance.Current == GameState.Hatchling;
            _heart.volume = Mathf.MoveTowards(_heart.volume, run && (Current == Phase.Warning || Current == Phase.Diving) ? 0.9f : run && Active ? 0.2f : 0f, Time.deltaTime * 2f);
            if (!run || hunter == null) return;
            _t += Time.deltaTime;
            Vector3 tp = turtle.transform.position;

            if (Active && Current != Phase.Leaving && DistanceToSea < safeDistance)
            {
                Message?.Invoke("The waves are close. The gull won't follow you into the surf.");
                Leave();
                return;
            }

            switch (Current)
            {
                case Phase.Idle:
                    if (DistanceToSea < triggerDistance)
                    {
                        Current = Phase.Approach;
                        _t = 0;
                        hunter.TeleportTo(tp - SeaDir() * 14f + Vector3.Cross(Vector3.up, SeaDir()) * 10f + Vector3.up * 9f);
                        hunter.Circle(turtle.transform, 4.5f, 5.5f);
                        hunter.Call(1f);
                        Message?.Invoke("A seagull is patrolling the shore...");
                    }
                    break;
                case Phase.Approach:
                    if (_t > 4f)
                    {
                        Current = Phase.ShadowPass;
                        _t = 0;
                        Vector3 side = Vector3.Cross(Vector3.up, SeaDir());
                        Vector3 a = tp - side * 6f + SeaDir() * 0.6f + Vector3.up * 2f;
                        Vector3 b = tp + side * 7f + SeaDir() * 0.6f + Vector3.up * 2.4f;
                        hunter.FlyTo(a, false, () => hunter.FlyTo(b, false));
                    }
                    break;
                case Phase.ShadowPass:
                    if (_t > 4.5f)
                    {
                        Message?.Invoke("Its shadow just crossed the sand.\nWhen it hovers and screeches, get out of the circle - or hide under something.");
                        Current = Phase.Stalking;
                        _t = 0;
                        hunter.Circle(turtle.transform, 3.2f, 4.2f);
                    }
                    break;
                case Phase.Stalking:
                    if (_t < 3f) break;
                    if (Sheltered(tp))
                    {
                        EverSheltered = true;
                        if (!_warnedShelter) { _warnedShelter = true; Message?.Invoke("Under cover. The gull can't reach you here."); }
                        _t = 1.5f; // keep circling, check again soon
                        break;
                    }
                    Current = Phase.Warning;
                    _t = 0;
                    _strike = Ground(tp);
                    hunter.Hover(_strike + Vector3.up * 2.4f);
                    hunter.Screech();
                    _ring.gameObject.SetActive(true);
                    break;
                case Phase.Warning:
                    float k = Mathf.Clamp01(_t / warningSeconds);
                    PlaceDecal(_ring, _strike, Mathf.Lerp(ringStartRadius, strikeRadius, k) * 2f);
                    _ringMat.SetColor("_Color", new Color(1f, 0.35f, 0.15f, 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(_t * (6f + 10f * k)))));
                    if (_t >= warningSeconds)
                    {
                        Current = Phase.Diving;
                        _t = 0;
                        hunter.Dive(_strike + Vector3.up * 0.05f, Resolve);
                    }
                    break;
                case Phase.Diving:
                    if (_t > 3f) Resolve(); // safety: never wait forever
                    break;
                case Phase.Leaving:
                    if (_t > 3f)
                    {
                        Current = Phase.Done;
                        Survived?.Invoke();
                    }
                    break;
            }
        }

        void Resolve()
        {
            if (Current != Phase.Diving) return;
            _ring.gameObject.SetActive(false);
            Attacks++;
            Vector3 d = turtle.transform.position - _strike;
            d.y = 0;
            bool hit = d.magnitude < strikeRadius && !Sheltered(turtle.transform.position);
            if (hit)
            {
                Strikes++;
                TotalStrikes++;
                turtle.Stun(stunSeconds);
                if (ScreenFader.Instance != null) ScreenFader.Instance.Flash(new Color(0.45f, 0.05f, 0.03f), 0.35f, 0.5f);
                AudioOneShot.Play2D(SameShoreAssets.I.gullFlap, 1f);
                if (Strikes >= strikesToCatch)
                {
                    Current = Phase.Done;
                    _armed = false;
                    TimesCaught++;
                    hunter.Leave(SeaDir());
                    Caught?.Invoke();
                    return;
                }
                Message?.Invoke("It struck you! Shake it off and keep moving.\nWatch for the circle.");
            }
            else
            {
                Dodged++;
                Message?.Invoke(Dodged == 1 ? "Missed! Keep crawling to the sea." : "Missed again.");
            }
            if (Attacks >= maxAttacks) Leave();
            else
            {
                Current = Phase.Stalking;
                _t = 0;
                hunter.Circle(turtle.transform, 3.2f, 4.2f);
            }
        }

        void Leave()
        {
            Current = Phase.Leaving;
            _t = 0;
            _ring.gameObject.SetActive(false);
            hunter.Leave(SeaDir());
        }

        /// <summary>Under the chair, a rock, driftwood, the bin...: anything solid overhead.</summary>
        public static bool Sheltered(Vector3 p)
        {
            var hits = Physics.RaycastAll(p + Vector3.up * 0.08f, Vector3.up, 2.5f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.collider is TerrainCollider || h.collider is CharacterController) continue;
                if (PlayerRig.Root != null && h.collider.transform.IsChildOf(PlayerRig.Root)) continue;
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ shadow & ring decals
        void UpdateShadow()
        {
            bool show = hunter != null && hunter.State != Seagull.Mode.Grounded && Active;
            _shadow.gameObject.SetActive(show);
            if (!show) return;
            Vector3 g = Ground(hunter.transform.position);
            float h = Mathf.Max(0.1f, hunter.transform.position.y - g.y);
            PlaceDecal(_shadow, g, Mathf.Lerp(1.2f, 2.2f, Mathf.Clamp01(h / 8f)));
            _shadowMat.SetColor("_BaseColor", new Color(0, 0, 0, Mathf.Lerp(0.55f, 0.18f, Mathf.Clamp01(h / 8f))));
        }

        Transform Decal(string name, Material template, Texture2D tex, out Material mat)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(transform, false);
            mat = new Material(template);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            q.SetActive(false);
            return q.transform;
        }

        static void PlaceDecal(Transform t, Vector3 ground, float diameter)
        {
            t.position = ground + Vector3.up * 0.006f;
            t.rotation = Quaternion.Euler(90, 0, 0);
            t.localScale = Vector3.one * diameter;
        }

        static Vector3 Ground(Vector3 p)
        {
            return FootprintSystem.GroundHit(p + Vector3.up * 3f, out RaycastHit h) ? h.point : new Vector3(p.x, p.y, p.z);
        }

        static Texture2D RadialTexture(bool ring)
        {
            const int n = 128;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2 - 1, v = (y + 0.5f) / n * 2 - 1;
                float r = Mathf.Sqrt(u * u + v * v);
                float a = ring ? Mathf.Clamp01(1 - Mathf.Abs(r - 0.85f) / 0.08f) + Mathf.Clamp01(1 - r / 0.12f) * 0.6f
                               : Mathf.Clamp01(1 - Mathf.SmoothStep(0.25f, 1f, r)) * (0.85f + 0.15f * Mathf.Sin(Mathf.Atan2(v, u) * 2f));
                t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(a)));
            }
            t.Apply();
            return t;
        }
    }
}
