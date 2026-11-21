using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// First-person body for the human (3-point: HMD + two controllers), solved procedurally:
    ///  - torso hangs below and behind the head (no head mesh, so the camera is never blocked) and turns
    ///    towards the walking direction, or towards the gaze once it twists more than ~40 deg;
    ///  - arms use analytic two-bone IK to the controllers (or, on desktop, to the held object / a rest pose);
    ///  - legs step procedurally: each foot stays planted until the hips move away, then takes an arcing step,
    ///    knees solved with two-bone IK, feet on the terrain.
    /// Segment pivots sit at the proximal joint and point along local +Y (set up by the scene builder).
    /// </summary>
    public class HumanAvatar : MonoBehaviour
    {
        [Header("Segments (pivot at proximal joint, +Y along the bone)")]
        public Transform torso;
        public Transform upperArmL, forearmL, handL, upperArmR, forearmR, handR;
        public Transform thighL, shinL, footL, thighR, shinR, footR;

        [Header("Proportions (m)")]
        public float torsoLength = 0.56f, upperArm = 0.29f, forearm = 0.26f, thigh = 0.44f, shin = 0.43f;
        public float shoulderHalfWidth = 0.18f, hipHalfWidth = 0.095f, standingHipHeight = 0.95f;
        public Vector3 neckFromEye = new Vector3(0f, -0.14f, -0.1f);
        public Vector3 wristFromController = new Vector3(0f, -0.02f, -0.08f);

        [Header("Gait")]
        public float stepThreshold = 0.28f;
        public float stepDuration = 0.32f;
        public float stepHeight = 0.08f;

        [Header("Hands")]
        [Tooltip("Show the mitten hands only when the controller hands are not drawn (desktop / untracked).")]
        public bool autoHideHands = true;

        Transform _seat;
        /// <summary>Sitting on the beach chair: hips on the seat, legs stretched forward, feet on the sand.</summary>
        public void SetSeat(Transform seat)
        {
            _seat = seat;
            if (seat == null) ResetFeet();
        }
        public bool Seated => _seat != null;

        float _bodyYaw;
        Vector3 _lastRoot;
        Vector3 _vel;
        Foot _left = new Foot(), _right = new Foot();
        Renderer[] _renderers;
        bool _shown = true;

        class Foot
        {
            public Vector3 plant, from, to;
            public float t = 1f;
            public bool Stepping => t < 1f;
        }

        void Start()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            if (PlayerRig.Head != null) _bodyYaw = PlayerRig.Head.eulerAngles.y;
            _lastRoot = PlayerRig.Root != null ? PlayerRig.Root.position : Vector3.zero;
            ResetFeet();
        }

        bool ShouldShow()
        {
            var gm = GameManager.Instance;
            return gm == null || gm.Current == GameState.Human || gm.Current == GameState.Intro || gm.Current == GameState.Transition;
        }

        void Show(bool on)
        {
            if (_shown == on) return;
            _shown = on;
            foreach (var r in _renderers) if (r != null) r.enabled = on;
            if (on) ResetFeet();
        }

        public void ResetFeet()
        {
            Transform head = PlayerRig.Head;
            if (head == null) return;
            Vector3 f = Flat(head.forward), r = Vector3.Cross(Vector3.up, f);
            _left.plant = Ground(head.position - r * 0.11f);
            _right.plant = Ground(head.position + r * 0.11f);
            _left.t = _right.t = 1f;
            _bodyYaw = head.eulerAngles.y;
        }

        void LateUpdate()
        {
            bool show = ShouldShow() && PlayerRig.Head != null && Mathf.Abs(PlayerRig.Scale - 1f) < 0.01f;
            Show(show);
            if (!show) return;

            Transform head = PlayerRig.Head;
            Transform root = PlayerRig.Root;
            if (_seat != null) { SeatedPose(head); return; }
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 rootPos = root != null ? root.position : head.position;
            Vector3 headFlatVel = (rootPos - _lastRoot) / dt;
            headFlatVel.y = 0;
            _vel = Vector3.Lerp(_vel, headFlatVel, 1 - Mathf.Exp(-10f * dt));
            _lastRoot = rootPos;
            float floor = rootPos.y;

            // ---- body yaw: walking direction, or catch up with the gaze when twisted too much
            float headYaw = head.eulerAngles.y;
            float speed = _vel.magnitude;
            float targetYaw = _bodyYaw;
            if (speed > 0.3f)
            {
                float moveYaw = Mathf.Atan2(_vel.x, _vel.z) * Mathf.Rad2Deg;
                // walking backwards/sideways: keep facing roughly where the head looks
                targetYaw = Mathf.Abs(Mathf.DeltaAngle(moveYaw, headYaw)) < 100f ? Mathf.LerpAngle(headYaw, moveYaw, 0.5f) : headYaw;
            }
            else if (Mathf.Abs(Mathf.DeltaAngle(_bodyYaw, headYaw)) > 40f) targetYaw = headYaw;
            _bodyYaw = Mathf.MoveTowardsAngle(_bodyYaw, targetYaw, 220f * dt * Mathf.Clamp01(Mathf.Abs(Mathf.DeltaAngle(_bodyYaw, targetYaw)) / 20f + 0.2f));
            Quaternion yaw = Quaternion.Euler(0, _bodyYaw, 0);
            Vector3 fwd = yaw * Vector3.forward, right = yaw * Vector3.right;

            // ---- torso
            Vector3 neck = head.position + Quaternion.Euler(0, headYaw, 0) * new Vector3(neckFromEye.x, 0, neckFromEye.z) + Vector3.up * neckFromEye.y;
            float hipY = Mathf.Min(floor + standingHipHeight, neck.y - torsoLength * 0.95f);
            Vector3 hips = new Vector3(neck.x, hipY, neck.z) - fwd * 0.03f;
            Vector3 spine = neck - hips;
            Place(torso, hips, spine, fwd, Mathf.Clamp(spine.magnitude / torsoLength, 0.9f, 1.08f));
            Vector3 spineDir = spine.normalized;
            Vector3 chest = hips + spineDir * (torsoLength * 0.86f);
            Vector3 shoulderL = chest - right * shoulderHalfWidth, shoulderR = chest + right * shoulderHalfWidth;

            // ---- arms
            float swing = Mathf.Sin(Time.time * 6f) * Mathf.Clamp01(speed / 1.4f) * 0.12f;
            Arm(Hand.Left, shoulderL, -right, fwd, swing, upperArmL, forearmL, handL);
            Arm(Hand.Right, shoulderR, right, fwd, -swing, upperArmR, forearmR, handR);

            // ---- legs
            Vector3 hipL = hips - right * hipHalfWidth - Vector3.up * 0.04f;
            Vector3 hipR = hips + right * hipHalfWidth - Vector3.up * 0.04f;
            Vector3 lead = _vel * 0.22f;
            StepFoot(_left, Ground(hipL + lead), _right);
            StepFoot(_right, Ground(hipR + lead), _left);
            Leg(hipL, _left, fwd, right, thighL, shinL, footL);
            Leg(hipR, _right, fwd, right, thighR, shinR, footR);
        }

        void SeatedPose(Transform head)
        {
            Vector3 f = Flat(_seat.forward), r = Vector3.Cross(Vector3.up, f);
            _bodyYaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            float headYaw = head.eulerAngles.y;
            Vector3 neck = head.position + Quaternion.Euler(0, headYaw, 0) * new Vector3(neckFromEye.x, 0, neckFromEye.z) + Vector3.up * neckFromEye.y;
            Vector3 hips = _seat.position + Vector3.up * 0.06f - f * 0.04f;
            Vector3 spine = neck - hips;
            Place(torso, hips, spine, f, Mathf.Clamp(spine.magnitude / torsoLength, 0.85f, 1.1f));
            Vector3 chest = hips + spine.normalized * (torsoLength * 0.86f);
            Arm(Hand.Left, chest - r * shoulderHalfWidth, -r, f, 0f, upperArmL, forearmL, handL);
            Arm(Hand.Right, chest + r * shoulderHalfWidth, r, f, 0f, upperArmR, forearmR, handR);
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 hip = hips + r * (hipHalfWidth * side) - Vector3.up * 0.02f;
                Vector3 foot = Ground(hip + f * 0.72f + r * (0.05f * side));
                Vector3 ankle = foot + Vector3.up * 0.075f;
                Vector3 knee = SolveIK(hip, ankle, thigh, shin, hip + f * 0.6f + Vector3.up * 0.6f);
                Place(side < 0 ? thighL : thighR, hip, knee - hip, f, 1f);
                Place(side < 0 ? shinL : shinR, knee, ankle - knee, f, 1f);
                Transform ft = side < 0 ? footL : footR;
                if (ft != null) ft.SetPositionAndRotation(ankle, Quaternion.LookRotation(f, Vector3.up));
                (side < 0 ? _left : _right).plant = foot;
            }
        }

        void Arm(Hand hand, Vector3 shoulder, Vector3 outward, Vector3 fwd, float swing, Transform upper, Transform fore, Transform handT)
        {
            Vector3 wrist;
            Vector3 handDir;
            Transform anchor = PlayerRig.Hand(hand);
            bool tracked = !DesktopTestingMode.Active && anchor != null &&
                           OVRInput.IsControllerConnected(hand == Hand.Left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch);
            if (tracked)
            {
                wrist = anchor.TransformPoint(wristFromController);
                handDir = anchor.forward;
            }
            else
            {
                Transform held = hand == Hand.Right ? DesktopHeldTransform() : null;
                if (held != null)
                {
                    wrist = held.position - PlayerRig.Head.forward * 0.06f - Vector3.up * 0.02f;
                    handDir = PlayerRig.Head.forward;
                }
                else
                {
                    wrist = shoulder - Vector3.up * (upperArm + forearm - 0.04f) + outward * 0.05f + fwd * (0.05f + swing);
                    handDir = (Vector3.down + fwd * 0.15f).normalized;
                }
            }
            Vector3 pole = shoulder + Vector3.down * 0.4f + outward * 0.3f - fwd * 0.25f;
            Vector3 elbow = SolveIK(shoulder, wrist, upperArm, forearm, pole);
            Place(upper, shoulder, elbow - shoulder, fwd, 1f);
            Place(fore, elbow, wrist - elbow, fwd, 1f);
            Place(handT, wrist, handDir, fwd, 1f);
            if (autoHideHands && handT != null)
            {
                var r = handT.GetComponentInChildren<Renderer>();
                if (r != null) r.enabled = !tracked;
            }
        }

        static Transform DesktopHeldTransform()
        {
            if (!DesktopTestingMode.Active) return null;
            return VRHand.Right != null && VRHand.Right.Held != null ? VRHand.Right.Held.transform : null;
        }

        void StepFoot(Foot f, Vector3 ideal, Foot other)
        {
            if (f.Stepping)
            {
                f.t = Mathf.Min(1f, f.t + Time.deltaTime / stepDuration);
                Vector3 p = Vector3.Lerp(f.from, f.to, Mathf.SmoothStep(0, 1, f.t));
                p.y += Mathf.Sin(f.t * Mathf.PI) * stepHeight;
                f.plant = p;
                if (f.t >= 1f) f.plant = f.to;
                return;
            }
            Vector3 d = ideal - f.plant;
            d.y = 0;
            if (d.magnitude > stepThreshold && !other.Stepping)
            {
                f.from = f.plant;
                f.to = ideal;
                f.t = 0f;
            }
        }

        void Leg(Vector3 hip, Foot f, Vector3 fwd, Vector3 right, Transform th, Transform sh, Transform ft)
        {
            Vector3 ankle = f.plant + Vector3.up * 0.075f;
            Vector3 pole = hip + fwd * 0.8f;
            Vector3 knee = SolveIK(hip, ankle, thigh, shin, pole);
            Place(th, hip, knee - hip, fwd, 1f);
            Place(sh, knee, ankle - knee, fwd, 1f);
            if (ft != null) ft.SetPositionAndRotation(ankle, Quaternion.LookRotation(fwd, Vector3.up));
        }

        /// <summary>Analytic two-bone IK: returns the middle joint (elbow/knee) bending towards the pole.</summary>
        static Vector3 SolveIK(Vector3 a, Vector3 target, float l1, float l2, Vector3 pole)
        {
            Vector3 ab = target - a;
            float d = Mathf.Clamp(ab.magnitude, 0.01f, l1 + l2 - 0.001f);
            Vector3 dir = ab.normalized;
            float x = (l1 * l1 - l2 * l2 + d * d) / (2 * d);           // along a->target
            float h = Mathf.Sqrt(Mathf.Max(0, l1 * l1 - x * x));       // away from the line
            Vector3 bend = Vector3.ProjectOnPlane(pole - a, dir);
            if (bend.sqrMagnitude < 1e-6f) bend = Vector3.ProjectOnPlane(Vector3.forward, dir);
            return a + dir * x + bend.normalized * h;
        }

        static void Place(Transform t, Vector3 from, Vector3 along, Vector3 fwd, float stretch)
        {
            if (t == null || along.sqrMagnitude < 1e-8f) return;
            Vector3 up = along.normalized;
            Vector3 f = Vector3.ProjectOnPlane(fwd, up);
            if (f.sqrMagnitude < 1e-6f) f = Vector3.ProjectOnPlane(Vector3.forward, up);
            t.SetPositionAndRotation(from, Quaternion.LookRotation(f.normalized, up));
            Vector3 s = t.localScale;
            s.y = stretch;
            t.localScale = s;
        }

        static Vector3 Ground(Vector3 p)
        {
            if (FootprintSystem.GroundHit(p + Vector3.up * 1.5f, out RaycastHit hit)) return hit.point;
            return new Vector3(p.x, PlayerRig.Root != null ? PlayerRig.Root.position.y : p.y, p.z);
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }
    }
}
