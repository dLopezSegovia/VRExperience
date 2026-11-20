using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// Kinematic seagull. Human phase: rests on the sand and flies away when approached.
    /// Hatchling phase: circles above the hatchling and dives at it (driven by SeagullFlock).
    /// </summary>
    public class Seagull : MonoBehaviour
    {
        public enum Mode { Grounded, Flying, Circling, Diving }

        [SerializeField] private Transform wingL, wingR, body;
        [SerializeField] private float cruiseSpeed = 5f;
        [SerializeField] private float diveSpeed = 9f;
        [SerializeField] private float turnRate = 140f;
        [SerializeField] private float fleeRadius = 3.5f;
        [SerializeField] private AudioSource voice;

        public Mode State { get; private set; } = Mode.Grounded;
        public bool IsHunting => State == Mode.Circling || State == Mode.Diving;

        Vector3 _target;
        Vector3 _vel;
        Vector3 _landSpot;
        float _flap, _nextCall, _timer;
        Quaternion _wlRest, _wrRest;
        Transform _orbit;
        float _orbitRadius, _orbitHeight, _orbitAngle;
        Vector3 _diveTarget;
        System.Action<Seagull> _onDiveEnd;

        void Awake()
        {
            if (wingL) _wlRest = wingL.localRotation;
            if (wingR) _wrRest = wingR.localRotation;
            _landSpot = transform.position;
            _nextCall = Time.time + Random.Range(4f, 20f);
            _orbitAngle = Random.value * 360f;
        }

        public void FlyTo(Vector3 point, bool landThere)
        {
            State = Mode.Flying;
            _target = point;
            if (landThere) _landSpot = point;
            _timer = landThere ? 0 : -1;
        }

        public void Hunt(Transform target, float radius, float height)
        {
            _orbit = target;
            _orbitRadius = radius;
            _orbitHeight = height;
            if (State == Mode.Grounded) TakeOffBoost();
            State = Mode.Circling;
        }

        public void Dive(Vector3 strikePoint, System.Action<Seagull> onEnd)
        {
            State = Mode.Diving;
            _diveTarget = strikePoint;
            _onDiveEnd = onEnd;
            Call(1f);
        }

        public void Call(float volume = 0.8f)
        {
            if (voice == null) return;
            voice.pitch = Random.Range(0.9f, 1.15f);
            voice.PlayOneShot(SameShoreAssets.Pick(SameShoreAssets.I.gullCalls), volume);
        }

        void TakeOffBoost()
        {
            _vel = Vector3.up * 2.5f + transform.forward * 1.5f;
            if (voice != null && SameShoreAssets.I.gullFlap != null) voice.PlayOneShot(SameShoreAssets.I.gullFlap, 0.7f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            switch (State)
            {
                case Mode.Grounded: Grounded(dt); break;
                case Mode.Flying: Steer(_target, cruiseSpeed, dt); ArriveCheck(); break;
                case Mode.Circling:
                    if (_orbit == null) { FlyTo(_landSpot, true); break; }
                    _orbitAngle += dt * cruiseSpeed / Mathf.Max(1f, _orbitRadius) * Mathf.Rad2Deg;
                    Vector3 c = _orbit.position + Vector3.up * _orbitHeight;
                    Vector3 p = c + Quaternion.Euler(0, _orbitAngle, 0) * Vector3.forward * _orbitRadius;
                    Steer(p, cruiseSpeed, dt);
                    break;
                case Mode.Diving:
                    Steer(_diveTarget, diveSpeed, dt, 400f);
                    if ((transform.position - _diveTarget).magnitude < 0.25f)
                    {
                        State = Mode.Circling;
                        _vel = Vector3.up * 4f + _vel.normalized * 2f;
                        var cb = _onDiveEnd;
                        _onDiveEnd = null;
                        cb?.Invoke(this);
                    }
                    break;
            }
            Animate(dt);

            if (Time.time > _nextCall)
            {
                _nextCall = Time.time + Random.Range(6f, 22f);
                Call(State == Mode.Grounded ? 0.5f : 0.8f);
            }
        }

        void Grounded(float dt)
        {
            _vel = Vector3.zero;
            // flee from the human
            if (GameManager.Instance != null && GameManager.Instance.Current == GameState.Human && PlayerRig.Head != null)
            {
                Vector3 away = transform.position - PlayerRig.Head.position;
                away.y = 0;
                if (away.magnitude < fleeRadius)
                {
                    Vector3 dir = (away.normalized + Random.insideUnitSphere * 0.3f);
                    dir.y = 0;
                    FlyTo(transform.position + dir.normalized * 25f + Vector3.up * 7f, false);
                    TakeOffBoost();
                    Call();
                    _timer = 0;
                }
            }
        }

        void ArriveCheck()
        {
            if ((transform.position - _target).magnitude > 0.6f) return;
            if (_timer >= 0)
            {
                // landed
                State = Mode.Grounded;
                transform.position = _landSpot;
                transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            }
            else
            {
                // fled: wander a bit then come back to land elsewhere near the water
                Vector3 back = _landSpot + new Vector3(Random.Range(-4f, 4f), 0, Random.Range(-1.5f, 1.5f));
                if (FootprintSystem.GroundHit(back + Vector3.up * 5f, out RaycastHit h)) back = h.point;
                FlyTo(back, true);
            }
        }

        void Steer(Vector3 point, float speed, float dt, float turn = -1f)
        {
            Vector3 desired = (point - transform.position);
            float dist = desired.magnitude;
            desired = desired.normalized * Mathf.Min(speed, dist * 2.5f + 0.5f);
            _vel = Vector3.MoveTowards(_vel, desired, dt * (turn > 0 ? turn : turnRate) * 0.05f * speed);
            transform.position += _vel * dt;
            Vector3 look = _vel;
            if (look.sqrMagnitude > 0.01f)
            {
                Quaternion q = Quaternion.LookRotation(look, Vector3.up);
                float bank = Mathf.Clamp(Vector3.SignedAngle(transform.forward, look, Vector3.up) * 3f, -40f, 40f);
                transform.rotation = Quaternion.Slerp(transform.rotation, q * Quaternion.Euler(0, 0, -bank), dt * 4f);
            }
        }

        void Animate(float dt)
        {
            bool flying = State != Mode.Grounded;
            float rate = State == Mode.Diving ? 0f : (_vel.y > 0.5f ? 9f : 5.5f);
            _flap += dt * rate;
            float a = flying ? (State == Mode.Diving ? -35f : Mathf.Sin(_flap) * 38f + 8f) : -70f; // folded when grounded
            if (wingL) wingL.localRotation = Quaternion.AngleAxis(-a, Vector3.forward) * _wlRest;
            if (wingR) wingR.localRotation = Quaternion.AngleAxis(a, Vector3.forward) * _wrRest;
            if (body != null && !flying)
            {
                float peck = Mathf.Max(0, Mathf.Sin(Time.time * 0.8f + _orbitAngle)) ;
                body.localRotation = Quaternion.Euler(peck * peck * 25f, 0, 0);
            }
            else if (body != null) body.localRotation = Quaternion.identity;
        }
    }
}
