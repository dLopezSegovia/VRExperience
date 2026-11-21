using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SameShore
{
    /// <summary>
    /// One hand of the human (one per controller). Grip = grab / release (throw), trigger = use the held
    /// object. In Desktop Testing mode the right hand is driven by the mouse: E or left click grabs what is
    /// under the crosshair, the same again throws/drops it (towards the crosshair), F or right click uses it.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public class VRHand : MonoBehaviour
    {
        public Hand hand = Hand.Right;
        [SerializeField] private float desktopReach = 3.2f;
        [SerializeField] private float velocityWindow = 0.09f;

        public static VRHand Left, Right;
        public static VRHand Get(Hand h) => h == Hand.Left ? Left : Right;
        /// <summary>Hands work only in the human phase (and can be paused by the story).</summary>
        public static bool Enabled = true;
        /// <summary>Text for the desktop crosshair.</summary>
        public static string DesktopPrompt;

        public VRGrabbable Held { get; private set; }
        public VRGrabbable Hovered { get; private set; }
        public bool UseHeld { get; private set; }
        public bool UseDown { get; private set; }
        public Vector3 Velocity { get; private set; }

        /// <summary>Where the hand is (controller anchor, or the desktop stand-in in front of the camera).</summary>
        public Vector3 Position => Anchor != null ? Anchor.position : Vector3.zero;
        public Quaternion Rotation => Anchor != null ? Anchor.rotation : Quaternion.identity;
        /// <summary>Approximate index fingertip (for poking buttons).</summary>
        public Vector3 Fingertip => Position + Rotation * new Vector3(hand == Hand.Left ? 0.01f : -0.01f, -0.015f, 0.07f);

        public bool Desktop => DesktopTestingMode.Active;
        public bool Tracked => Desktop ? hand == Hand.Right :
            OVRInput.IsControllerConnected(hand == Hand.Left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch);

        Transform _desktopAnchor;
        readonly Queue<(float t, Vector3 p, Quaternion r)> _hist = new Queue<(float, Vector3, Quaternion)>();
        Quaternion _relRot;
        Vector3 _relPos;
        Vector3 _desktopThrowTarget;
        bool _desktopThrowPending;
        GameObject _hoverRing;

        Transform Anchor
        {
            get
            {
                if (Desktop)
                {
                    if (hand == Hand.Left) return null;
                    if (_desktopAnchor == null)
                    {
                        _desktopAnchor = new GameObject("~DesktopHand").transform;
                        _desktopAnchor.hideFlags = HideFlags.DontSave;
                    }
                    Transform h = PlayerRig.Head;
                    if (h != null) _desktopAnchor.SetPositionAndRotation(h.TransformPoint(new Vector3(0.2f, -0.24f, 0.42f)), h.rotation);
                    return _desktopAnchor;
                }
                return PlayerRig.Hand(hand);
            }
        }

        void Awake()
        {
            if (hand == Hand.Left) Left = this; else Right = this;
        }

        void OnEnable() => Application.onBeforeRender += BeforeRender;
        void OnDisable() => Application.onBeforeRender -= BeforeRender;

        bool Active()
        {
            var gm = GameManager.Instance;
            return Enabled && gm != null && gm.Current == GameState.Human && PlayerRig.Head != null && Mathf.Abs(PlayerRig.Scale - 1f) < 0.01f;
        }

        void Update()
        {
            UseDown = false;
            UseHeld = false;
            if (!Active())
            {
                if (Held != null) ForceRelease();
                Hovered = null;
                ShowHover(null);
                if (hand == Hand.Right) DesktopPrompt = null;
                return;
            }
            if (Desktop) { if (hand == Hand.Right) DesktopUpdate(); return; }
            Transform a = Anchor;
            if (a == null) return;
            Track(a);

            var grip = hand == Hand.Left ? OVRInput.Button.PrimaryHandTrigger : OVRInput.Button.SecondaryHandTrigger;
            var trig = hand == Hand.Left ? OVRInput.Button.PrimaryIndexTrigger : OVRInput.Button.SecondaryIndexTrigger;
            UseDown = OVRInput.GetDown(trig);
            UseHeld = OVRInput.Get(trig);

            if (Held == null)
            {
                Hovered = FindNearest(a.position);
                ShowHover(Hovered);
                if (OVRInput.GetDown(grip) && Hovered != null) Grab(Hovered);
            }
            else
            {
                ShowHover(null);
                if (OVRInput.GetUp(grip)) Release(Velocity, AngularVelocity());
                else if (UseDown) { Held.Use(this); XRInput.Haptic(hand, 0.3f, 0.04f); }
            }
        }

        void Track(Transform a)
        {
            float now = Time.time;
            _hist.Enqueue((now, a.position, a.rotation));
            while (_hist.Count > 2 && now - _hist.Peek().t > velocityWindow) _hist.Dequeue();
            var first = _hist.Peek();
            float dt = now - first.t;
            Velocity = dt > 1e-4f ? (a.position - first.p) / dt : Vector3.zero;
        }

        Vector3 AngularVelocity()
        {
            if (_hist.Count < 2) return Vector3.zero;
            var first = _hist.Peek();
            float dt = Time.time - first.t;
            if (dt < 1e-4f) return Vector3.zero;
            (Rotation * Quaternion.Inverse(first.r)).ToAngleAxis(out float ang, out Vector3 axis);
            if (ang > 180f) ang -= 360f;
            return axis * (ang * Mathf.Deg2Rad / dt);
        }

        VRGrabbable FindNearest(Vector3 p)
        {
            VRGrabbable best = null;
            float bd = float.MaxValue;
            foreach (var g in VRGrabbable.All)
            {
                if (g == null || !g.grabbingEnabled || g.IsHeld || !g.isActiveAndEnabled) continue;
                if ((g.transform.position - p).sqrMagnitude > 4f) continue;
                float d = g.DistanceTo(p);
                if (d < g.grabRadius && d < bd) { bd = d; best = g; }
            }
            return best;
        }

        public void Grab(VRGrabbable g)
        {
            if (g == null || g.IsHeld) return;
            var other = hand == Hand.Left ? Right : Left;
            if (other != null && other.Held == g) other.ForceRelease();
            Held = g;
            Transform a = Anchor;
            _relRot = Quaternion.Inverse(a.rotation) * g.transform.rotation;
            _relPos = a.InverseTransformPoint(g.transform.position);
            g.OnGrab(this);
            XRInput.Haptic(hand, 0.25f, 0.05f);
            Follow();
        }

        public void Release(Vector3 velocity, Vector3 angular)
        {
            var g = Held;
            if (g == null) return;
            Held = null;
            g.OnRelease(this, velocity, angular);
        }

        public void ForceRelease()
        {
            if (Held != null) Release(Vector3.zero, Vector3.zero);
        }

        void LateUpdate()
        {
            if (Held != null) Follow();
        }

        void BeforeRender()
        {
            if (Held != null && !Desktop) Follow();
        }

        /// <summary>The pose the held object should have now.</summary>
        public void HeldPose(VRGrabbable g, out Vector3 pos, out Quaternion rot)
        {
            Transform a = Anchor;
            if (Desktop)
            {
                Transform h = PlayerRig.Head;
                rot = h.rotation * Quaternion.Euler(g.desktopEuler);
                pos = h.TransformPoint(g.desktopOffset);
                if (!g.usePose)
                {
                    // keep the object's own orientation around the view yaw
                    rot = Quaternion.Euler(0, h.eulerAngles.y, 0) * _relRotDesktop;
                }
                return;
            }
            if (g.usePose)
            {
                Vector3 pp = g.posePosition;
                Vector3 pe = g.poseEuler;
                if (hand == Hand.Left) { pp.x = -pp.x; pe.y = -pe.y; pe.z = -pe.z; }
                rot = a.rotation * Quaternion.Euler(pe);
                pos = a.TransformPoint(pp);
            }
            else
            {
                rot = a.rotation * _relRot;
                pos = a.TransformPoint(_relPos);
            }
        }

        void Follow()
        {
            if (Held == null || Anchor == null) return;
            HeldPose(Held, out Vector3 p, out Quaternion r);
            Held.transform.SetPositionAndRotation(p, r);
            Held.Body.position = p;
            Held.Body.rotation = r;
        }

        // ------------------------------------------------------------------ hover highlight
        void ShowHover(VRGrabbable g)
        {
            if (g == null) { if (_hoverRing != null) _hoverRing.SetActive(false); return; }
            if (_hoverRing == null)
            {
                _hoverRing = UIPanel.Quad("~GrabHover" + hand, null, Vector3.zero, Vector2.one * 0.08f, SameShoreAssets.I.uiLine, new Color(1f, 0.95f, 0.75f, 0.55f), 2);
                _hoverRing.GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_MainTex", SameShoreAssets.I.ringTexture);
            }
            bool wasOff = !_hoverRing.activeSelf;
            _hoverRing.SetActive(true);
            Bounds b = Bounds(g);
            _hoverRing.transform.position = b.center + Vector3.up * (b.extents.y + 0.05f);
            if (PlayerRig.Head != null) _hoverRing.transform.rotation = Quaternion.LookRotation(_hoverRing.transform.position - PlayerRig.Head.position);
            float s = Mathf.Clamp(b.size.magnitude * 0.6f, 0.05f, 0.25f) * (1f + 0.08f * Mathf.Sin(Time.time * 6f));
            _hoverRing.transform.localScale = Vector3.one * s / 0.08f;
            if (wasOff && !Desktop) XRInput.Haptic(hand, 0.08f, 0.02f);
        }

        static Bounds Bounds(VRGrabbable g)
        {
            var rs = g.GetComponentsInChildren<MeshRenderer>();
            if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.one * 0.05f);
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        // ------------------------------------------------------------------ desktop testing
        Quaternion _relRotDesktop = Quaternion.identity;

        /// <summary>Crosshair ray that ignores the player's own body/capsule and what the hand carries.</summary>
        public static bool DesktopRay(Transform h, float reach, out RaycastHit hit, Rigidbody ignore = null)
        {
            hit = default;
            Transform root = PlayerRig.Root;
            float best = float.MaxValue;
            bool found = false;
            foreach (var x in Physics.RaycastAll(h.position, h.forward, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (x.distance >= best) continue;
                if (ignore != null && x.rigidbody == ignore) continue;
                if (root != null && x.collider.transform.IsChildOf(root)) continue;
                best = x.distance;
                hit = x;
                found = true;
            }
            return found;
        }

        void DesktopUpdate()
        {
            Transform h = PlayerRig.Head;
            Track(Anchor);
            bool interact = XRInput.KeyDown(XRInput.KeyId.E) || (DesktopTestingMode.Instance != null && DesktopTestingMode.Instance.WorldClickDown);
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            bool captured = DesktopTestingMode.Instance != null && DesktopTestingMode.Instance.Captured;
            UseDown = XRInput.KeyDown(XRInput.KeyId.F) || (m != null && captured && m.rightButton.wasPressedThisFrame);
            UseHeld = XRInput.KeyHeld(XRInput.KeyId.F) || (m != null && captured && m.rightButton.isPressed);
#endif
            DesktopRay(h, desktopReach, out RaycastHit hit, Held != null ? Held.Body : null);
            VRGrabbable aimed = null;
            if (hit.collider != null && (Held == null || hit.rigidbody != Held.Body))
                aimed = hit.collider.GetComponentInParent<VRGrabbable>();
            if (aimed != null && (!aimed.grabbingEnabled || aimed.IsHeld)) aimed = null;
            Hovered = Held == null ? aimed : null;
            ShowHover(Hovered);

            if (Held == null)
            {
                DesktopPrompt = aimed != null ? "E / click: pick up " + aimed.Label : PokeButton.DesktopAimed != null ? "E / click: press " + PokeButton.DesktopAimed.Label : null;
                if (interact && aimed == null && PokeButton.DesktopAimed == null)
                    Debug.Log($"[SameShore] desktop grab: nothing grabbable under the crosshair (hit '{(hit.collider != null ? hit.collider.name : "none")}' at {hit.distance:F2} m)");
                if (interact && aimed != null)
                {
                    _relRotDesktop = Quaternion.Inverse(Quaternion.Euler(0, h.eulerAngles.y, 0)) * aimed.transform.rotation;
                    Grab(aimed);
                }
                return;
            }
            DesktopPrompt = (Held.throwable ? "E / click: throw " : "E / click: put down ") + Held.Label +
                            (Held.GetComponent<IVRUsable>() != null ? "  ·  F / right click: use" : "");
            if (UseDown) Held.Use(this);
            if (interact)
            {
                if (Held.throwable)
                {
                    // throw towards the crosshair on a natural arc (flight time grows with distance)
                    Vector3 target = hit.collider != null ? hit.point : h.position + h.forward * 4f;
                    var tt = hit.collider != null ? hit.collider.GetComponentInParent<ThrowTarget>() : null;
                    if (tt != null && tt.aimPoint != null) target = tt.aimPoint.position;
                    Vector3 from = Held.transform.position;
                    float dist = new Vector2(target.x - from.x, target.z - from.z).magnitude;
                    // a mouse click is not an arm: add the scatter of a real throw (grows with distance), so the
                    // desktop toss is a skill shot like in VR and not a guaranteed basket
                    Vector2 scatter = UnityEngine.Random.insideUnitCircle * (0.04f + 0.055f * dist);
                    target += new Vector3(scatter.x, 0f, scatter.y);
                    float tf = Mathf.Clamp(0.45f + dist * 0.16f, 0.45f, 1.3f);
                    Vector3 v = (target - from) / tf - 0.5f * Physics.gravity * tf;
                    Release(v / Mathf.Max(0.01f, Held.throwScale), Vector3.zero);
                }
                else
                {
                    Vector3 drop = hit.collider != null && hit.distance < desktopReach ? hit.point + Vector3.up * 0.05f : h.position + h.forward * 0.9f;
                    var g = Held;
                    // laid down level (tools flat on the sand), its lowest point just above the surface
                    Quaternion rot = g.usePose ? Quaternion.Euler(0, h.eulerAngles.y, 0) : g.transform.rotation;
                    g.transform.SetPositionAndRotation(drop, rot);
                    Physics.SyncTransforms();
                    float low = float.MaxValue;
                    foreach (var c in g.Colliders) if (c != null && c.enabled && !c.isTrigger) low = Mathf.Min(low, c.bounds.min.y);
                    float ground = Mathf.Max(MeshKit.GroundY(drop), hit.collider != null ? hit.point.y : drop.y - 0.05f);
                    if (low < float.MaxValue) drop.y += ground + 0.01f - low;
                    g.transform.position = drop;
                    g.Body.position = drop;
                    g.Body.rotation = rot;
                    Release(Vector3.zero, Vector3.zero);
                }
            }
        }
    }
}
