using System;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// Kinematic herring gull with distinct behaviours and a procedural two-segment wing animation:
    ///   Grounded  - stands, pecks, wings folded; flees when the human approaches
    ///   Flying    - flaps / glides to a point (optionally lands there)
    ///   Circling  - soaring loops around a moving centre (wings mostly gliding)
    ///   Hovering  - hangs in the wind above a point: body up, quick shallow flaps (the attack warning)
    ///   Diving    - wings tucked, fast stoop to a point, then pulls up
    ///   Leaving   - climbs away over the sea
    /// </summary>
    public class Seagull : MonoBehaviour
    {
        public enum Mode { Grounded, Flying, Circling, Hovering, Diving, Leaving }

        [SerializeField] private Transform body;
        [SerializeField] private Transform wingLIn, wingLOut, wingRIn, wingROut;
        [SerializeField] private AudioSource voice;
        [SerializeField] private float cruiseSpeed = 5f;
        [SerializeField] private float diveSpeed = 7.5f;
        [SerializeField] private float fleeRadius = 3.5f;

        public Mode State { get; private set; } = Mode.Grounded;
        public Vector3 Velocity => _vel;

        Quaternion _rLIn, _rLOut, _rRIn, _rROut;
        Vector3 _vel, _target, _landSpot, _home;
        bool _landAfter;
        Transform _orbit;
        float _orbitR, _orbitH, _orbitAngle, _flapPhase, _flapAmp, _flapRate, _fold, _bodyPitch, _nextCall;
        Action _onArrive;

        void Awake()
        {
            if (wingLIn) _rLIn = wingLIn.localRotation;
            if (wingLOut) _rLOut = wingLOut.localRotation;
            if (wingRIn) _rRIn = wingRIn.localRotation;
            if (wingROut) _rROut = wingROut.localRotation;
            _home = _landSpot = transform.position;
            _orbitAngle = UnityEngine.Random.value * 360f;
            _nextCall = Time.time + UnityEngine.Random.Range(5f, 20f);
        }

        // ------------------------------------------------------------------ commands
        public void FlyTo(Vector3 p, bool land, Action onArrive = null)
        {
            if (State == Mode.Grounded) TakeOff();
            State = Mode.Flying;
            _target = p;
            _landAfter = land;
            if (land) _landSpot = p;
            _onArrive = onArrive;
        }

        public void Circle(Transform center, float radius, float height)
        {
            if (State == Mode.Grounded) TakeOff();
            _orbit = center;
            _orbitR = radius;
            _orbitH = height;
            if (center != null)
            {
                Vector3 d = transform.position - center.position;
                _orbitAngle = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            }
            State = Mode.Circling;
        }

        public void Hover(Vector3 p)
        {
            _target = p;
            State = Mode.Hovering;
        }

        public void Dive(Vector3 p, Action onArrive)
        {
            _target = p;
            _onArrive = onArrive;
            State = Mode.Diving;
            if (voice != null && SameShoreAssets.I.gullDive != null) voice.PlayOneShot(SameShoreAssets.I.gullDive, 1f);
        }

        public void Leave(Vector3 seaward)
        {
            State = Mode.Leaving;
            _target = transform.position + seaward.normalized * 40f + Vector3.up * 15f;
        }

        public void ReturnHome()
        {
            FlyTo(_home, true);
        }

        public void TeleportTo(Vector3 p)
        {
            transform.position = p;
            _vel = Vector3.zero;
        }

        public void Call(float volume = 0.8f) => Play(SameShoreAssets.Pick(SameShoreAssets.I.gullCalls), volume);
        public void Screech() => Play(SameShoreAssets.I.gullScreech, 1f);

        void Play(AudioClip c, float v)
        {
            if (voice == null || c == null) return;
            voice.pitch = UnityEngine.Random.Range(0.92f, 1.08f);
            voice.PlayOneShot(c, v);
        }

        void TakeOff()
        {
            _vel = Vector3.up * 2.8f + transform.forward * 1.2f;
            if (voice != null && SameShoreAssets.I.gullFlap != null) voice.PlayOneShot(SameShoreAssets.I.gullFlap, 0.7f);
        }

        // ------------------------------------------------------------------ update
        void Update()
        {
            float dt = Time.deltaTime;
            switch (State)
            {
                case Mode.Grounded:
                    _vel = Vector3.zero;
                    FleeFromHuman();
                    break;
                case Mode.Flying:
                    Steer(_target, cruiseSpeed, 3f, dt);
                    if ((transform.position - _target).magnitude < 0.5f) Arrive();
                    break;
                case Mode.Circling:
                    if (_orbit == null) { ReturnHome(); break; }
                    _orbitAngle += dt * cruiseSpeed * 0.8f / Mathf.Max(1f, _orbitR) * Mathf.Rad2Deg;
                    Vector3 c = _orbit.position + Vector3.up * _orbitH;
                    Steer(c + Quaternion.Euler(0, _orbitAngle, 0) * Vector3.forward * _orbitR, cruiseSpeed, 2.5f, dt);
                    break;
                case Mode.Hovering:
                    Steer(_target, 2.5f, 4f, dt, true);
                    break;
                case Mode.Diving:
                    Steer(_target, diveSpeed, 12f, dt);
                    if ((transform.position - _target).magnitude < 0.2f)
                    {
                        _vel = Vector3.up * 4.5f + Vector3.ProjectOnPlane(_vel, Vector3.up).normalized * 2.5f;
                        State = Mode.Circling;
                        var cb = _onArrive;
                        _onArrive = null;
                        cb?.Invoke();
                    }
                    break;
                case Mode.Leaving:
                    Steer(_target, cruiseSpeed * 1.2f, 2f, dt);
                    break;
            }
            Animate(dt);
            if (Time.time > _nextCall && State != Mode.Diving)
            {
                _nextCall = Time.time + UnityEngine.Random.Range(6f, 18f);
                Call(State == Mode.Grounded ? 0.45f : 0.75f);
            }
        }

        void FleeFromHuman()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Current != GameState.Human || PlayerRig.Head == null) return;
            Vector3 away = transform.position - PlayerRig.Head.position;
            away.y = 0;
            if (away.magnitude > fleeRadius) return;
            Vector3 dir = away.normalized + UnityEngine.Random.insideUnitSphere * 0.3f;
            dir.y = 0;
            Call();
            FlyTo(transform.position + dir.normalized * 25f + Vector3.up * 8f, false, () =>
            {
                Vector3 back = _home + new Vector3(UnityEngine.Random.Range(-3f, 3f), 0, UnityEngine.Random.Range(-1f, 1f));
                if (FootprintSystem.GroundHit(back + Vector3.up * 4f, out RaycastHit h)) back = h.point;
                FlyTo(back, true);
            });
        }

        void Arrive()
        {
            if (_landAfter)
            {
                State = Mode.Grounded;
                transform.position = _landSpot;
                transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            }
            var cb = _onArrive;
            _onArrive = null;
            cb?.Invoke();
        }

        void Steer(Vector3 point, float speed, float agility, float dt, bool hover = false)
        {
            Vector3 to = point - transform.position;
            float dist = to.magnitude;
            Vector3 desired = to.normalized * Mathf.Min(speed, dist * 2f + (hover ? 0f : 0.6f));
            _vel = Vector3.Lerp(_vel, desired, 1 - Mathf.Exp(-agility * dt));
            transform.position += _vel * dt;
            Vector3 look = hover ? (Vector3.ProjectOnPlane(point - transform.position, Vector3.up) + transform.forward * 0.5f) : _vel;
            look.y *= hover ? 0 : 0.6f;
            if (look.sqrMagnitude > 0.01f)
            {
                float bank = hover ? 0f : Mathf.Clamp(Vector3.SignedAngle(transform.forward, _vel, Vector3.up) * 2.5f, -45f, 45f);
                Quaternion q = Quaternion.LookRotation(look, Vector3.up) * Quaternion.Euler(0, 0, -bank);
                transform.rotation = Quaternion.Slerp(transform.rotation, q, 1 - Mathf.Exp(-5f * dt));
            }
        }

        void Animate(float dt)
        {
            // targets per behaviour: flap amplitude/rate, wing fold, body pitch
            float amp, rate, fold, pitch;
            switch (State)
            {
                case Mode.Grounded: amp = 0; rate = 0; fold = 1f; pitch = 0; break;
                case Mode.Hovering: amp = 22; rate = 9f; fold = 0.15f; pitch = -28f; break;
                case Mode.Diving: amp = 0; rate = 0; fold = 0.75f; pitch = 0; break;
                case Mode.Circling:
                    // soaring: glide with occasional flaps
                    bool glide = Mathf.Sin(Time.time * 0.7f + _orbitAngle * 0.01f) > -0.2f;
                    amp = glide ? 4 : 30; rate = glide ? 1.5f : 6f; fold = 0f; pitch = 0; break;
                default:
                    bool climbing = _vel.y > 0.6f;
                    amp = climbing ? 40 : 30; rate = climbing ? 7.5f : 5.5f; fold = 0f; pitch = 0; break;
            }
            _flapAmp = Mathf.Lerp(_flapAmp, amp, 1 - Mathf.Exp(-6f * dt));
            _flapRate = Mathf.Lerp(_flapRate, rate, 1 - Mathf.Exp(-6f * dt));
            _fold = Mathf.Lerp(_fold, fold, 1 - Mathf.Exp(-5f * dt));
            _bodyPitch = Mathf.Lerp(_bodyPitch, pitch, 1 - Mathf.Exp(-4f * dt));
            _flapPhase += dt * _flapRate * Mathf.PI * 2f;

            float s = Mathf.Sin(_flapPhase);
            float inner = s * _flapAmp + 6f;                       // dihedral
            float outer = Mathf.Sin(_flapPhase - 0.9f) * _flapAmp * 0.7f;
            // folding: inner wing swept up/back, outer wing folded under it
            float foldIn = _fold * 70f, foldOut = _fold * 150f;
            Wing(wingLIn, _rLIn, inner + foldIn * 0.3f, foldIn, -1);
            Wing(wingRIn, _rRIn, inner + foldIn * 0.3f, foldIn, 1);
            Wing(wingLOut, _rLOut, outer, foldOut, -1);
            Wing(wingROut, _rROut, outer, foldOut, 1);
            if (body != null)
            {
                float peck = State == Mode.Grounded ? Mathf.Pow(Mathf.Max(0, Mathf.Sin(Time.time * 0.8f + _orbitAngle)), 6) * 30f : 0f;
                body.localRotation = Quaternion.Euler(_bodyPitch + peck, 0, 0);
            }
        }

        static void Wing(Transform t, Quaternion rest, float flapDeg, float sweepDeg, int side)
        {
            if (t == null) return;
            // flap around the bird's long axis (local Z), sweep back around the vertical axis (local Y)
            t.localRotation = Quaternion.AngleAxis(sweepDeg * side, Vector3.up) * Quaternion.AngleAxis(flapDeg * side, Vector3.forward) * rest;
        }
    }
}
