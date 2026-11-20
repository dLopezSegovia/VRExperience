using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// One of the player's nest mates. Emerges from the sand when the nest hatches and, in the
    /// hatchling phase, crawls with the same light attraction as the player: they visibly show
    /// what a misplaced light does to the whole clutch.
    /// </summary>
    public class HatchlingSibling : MonoBehaviour
    {
        enum State { Buried, Emerged, Crawling, Gone }

        [SerializeField] private HatchlingAvatar avatar;
        [SerializeField] private float strokeDistance = 0.1f;
        [SerializeField] private float strokeDuration = 0.65f;
        [SerializeField] private float lightSteer = 0.8f;
        [SerializeField] private float buriedDepth = 0.035f;

        State _state = State.Buried;
        Vector3 _surfacePos;
        float _t, _strokeT, _delay, _strokeLen;
        Vector3 _dir;
        static readonly Collider[] Buf = new Collider[8];

        void Start()
        {
            _surfacePos = transform.position;
            transform.position = _surfacePos - Vector3.up * buriedDepth;
            _strokeLen = strokeDuration * Random.Range(0.85f, 1.2f);
            SetVisible(false);
        }

        void SetVisible(bool v)
        {
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = v;
        }

        public void Emerge(float delay)
        {
            if (_state != State.Buried) return;
            _state = State.Emerged;
            _t = -delay;
        }

        public void StartCrawl(float delay)
        {
            if (_state == State.Gone) return;
            if (_state == State.Buried) Emerge(0);
            _delay = delay;
            _state = State.Crawling;
        }

        void Update()
        {
            switch (_state)
            {
                case State.Emerged:
                    _t += Time.deltaTime;
                    if (_t < 0) return;
                    SetVisible(true);
                    float k = Mathf.Clamp01(_t / 3f);
                    transform.position = _surfacePos - Vector3.up * buriedDepth * (1 - k) + Vector3.up * Mathf.Sin(_t * 9f) * 0.002f * (1 - k);
                    avatar.Phase = Mathf.Repeat(_t * 1.4f, 1f) * (k < 1 ? 1 : 0) - (k < 1 ? 0 : 1);
                    break;
                case State.Crawling:
                    SetVisible(true);
                    _delay -= Time.deltaTime;
                    if (_delay > 0) { avatar.Phase = -1; return; }
                    Crawl();
                    break;
            }
        }

        void Crawl()
        {
            if (_strokeT <= 0f)
            {
                Vector3 f = transform.forward; f.y = 0; f.Normalize();
                Vector3 pull = LightSource.GetNetPull(transform.position, out _);
                if (pull.sqrMagnitude > 1e-4f)
                {
                    float bias = Mathf.Clamp01(pull.magnitude * lightSteer);
                    f = Vector3.Slerp(f, pull.normalized, bias).normalized;
                }
                f = Quaternion.Euler(0, Random.Range(-18f, 18f), 0) * f;
                // simple obstacle avoidance
                if (Physics.SphereCast(transform.position + Vector3.up * 0.03f, 0.02f, f, out RaycastHit hit, 0.1f, ~0, QueryTriggerInteraction.Ignore)
                    && !(hit.collider is TerrainCollider))
                    f = Quaternion.Euler(0, Random.value < 0.5f ? -70 : 70, 0) * f;
                _dir = f;
                _strokeT = _strokeLen + Random.Range(0.05f, 0.4f);
            }
            _strokeT -= Time.deltaTime;
            float phase = 1f - Mathf.Clamp01(_strokeT / _strokeLen);
            avatar.Phase = _strokeT > 0 && phase < 1f ? phase : -1f;
            float speed = phase < 1f ? strokeDistance / _strokeLen * 2f * Mathf.Pow(Mathf.Sin(Mathf.PI * phase), 2) : 0f;

            float mult = 1f;
            int n = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * 0.03f, 0.04f, Buf, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                if (!Buf[i].TryGetComponent(out TurtleZone z) || !z.isActiveAndEnabled) continue;
                if (z.Type == TurtleZoneType.Goal) { _state = State.Gone; gameObject.SetActive(false); return; }
                mult = Mathf.Min(mult, z.SpeedMultiplier);
            }

            Vector3 p = transform.position + _dir * speed * mult * Time.deltaTime;
            if (FootprintSystem.GroundHit(p + Vector3.up * 0.2f, out RaycastHit g)) p.y = g.point.y;
            transform.position = p;
            Quaternion target = Quaternion.LookRotation(_dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, 90f * Time.deltaTime);
        }
    }
}
