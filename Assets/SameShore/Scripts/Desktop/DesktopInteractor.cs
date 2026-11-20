using Oculus.Interaction;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SameShore
{
    /// <summary>
    /// Desktop stand-in for the hands (only runs in Desktop Testing mode, human phase).
    /// Works on the same objects as the XR grab: anything with an ISDK Grabbable can be carried;
    /// the Flashlight, SandBucket, TrashBin and GrabFeedback components behave exactly as in VR.
    ///   LMB / E : grab what is under the crosshair, or drop what you carry
    ///   RMB / F : use: switch the flashlight (held or aimed at), tip the bucket while held down
    /// A carried object rests where you look (so a bucket can be dipped in the sand to refill it,
    /// and litter can be dropped into the bin).
    /// </summary>
    public class DesktopInteractor : MonoBehaviour
    {
        [SerializeField] private float reach = 3f;
        [SerializeField] private float holdDistance = 0.9f;
        [SerializeField] private float pourTilt = 125f;

        public Rigidbody Held { get; private set; }
        public Collider Target { get; private set; }
        public string Prompt { get; private set; }

        GrabFeedback _feedback;
        Quaternion _relRot;
        float _pivotToBottom;
        bool _wasKinematic;
        CharacterController _body;

        void Update()
        {
            bool human = GameManager.Instance != null && GameManager.Instance.Current == GameState.Human;
            if (!DesktopTestingMode.Active || !human || PlayerRig.Head == null)
            {
                if (Held != null) Drop(false);
                Target = null;
                Prompt = null;
                return;
            }
            if (_body == null && PlayerRig.Root != null) _body = PlayerRig.Root.GetComponent<CharacterController>();

            Target = Raycast(out _);
            UpdatePrompt();

            bool interact = XRInput.KeyDown(XRInput.KeyId.E) || DesktopTestingMode.Instance.WorldClickDown;
            if (interact)
            {
                if (Held != null) Drop(true);
                else if (Target != null) TryGrab(Target);
            }
            if (UseDown) TryUse();
        }

        static bool UseDown
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var m = Mouse.current;
                return XRInput.KeyDown(XRInput.KeyId.F) || (m != null && m.rightButton.wasPressedThisFrame && DesktopTestingMode.Instance.Captured);
#else
                return false;
#endif
            }
        }

        static bool UseHeld
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var m = Mouse.current;
                return XRInput.KeyHeld(XRInput.KeyId.F) || (m != null && m.rightButton.isPressed);
#else
                return false;
#endif
            }
        }

        void UpdatePrompt()
        {
            if (Held != null)
            {
                bool bucket = Held.GetComponent<SandBucket>() != null;
                bool lamp = Held.GetComponent<Flashlight>() != null;
                Prompt = bucket ? "E: drop · hold F / RMB: tip the bucket (aim at the sand to scoop)"
                       : lamp ? "E: drop · F / RMB: switch the flashlight"
                       : "E: drop (aim at the litter bin to throw it away)";
                return;
            }
            if (Target == null) { Prompt = null; return; }
            var grab = Target.attachedRigidbody != null ? Target.attachedRigidbody.GetComponent<Grabbable>() : null;
            var lampT = Target.GetComponentInParent<Flashlight>();
            if (lampT != null) Prompt = "E: pick up the flashlight · F: switch it " + (lampT.IsOn ? "off" : "on");
            else if (grab != null) Prompt = "E: pick up " + Nice(grab.name);
            else Prompt = null;
        }

        static string Nice(string n) => n.Replace("_", " ").Replace("(1)", "").Trim();

        Collider Raycast(out RaycastHit hit)
        {
            Transform h = PlayerRig.Head;
            hit = default;
            var hits = Physics.RaycastAll(h.position, h.forward, reach, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Collider pick = null;
            foreach (var x in hits)
            {
                if (x.distance >= best) continue;
                if (Held != null && x.rigidbody == Held) continue;
                if (x.collider == _body || (PlayerRig.Root != null && x.collider.transform.IsChildOf(PlayerRig.Root))) continue;
                best = x.distance;
                pick = x.collider;
                hit = x;
            }
            return pick;
        }

        void TryGrab(Collider c)
        {
            Rigidbody rb = c.attachedRigidbody;
            if (rb == null || rb.GetComponent<Grabbable>() == null) return;
            Held = rb;
            _wasKinematic = rb.isKinematic;
            rb.isKinematic = true;
            _feedback = rb.GetComponent<GrabFeedback>();
            if (_feedback != null) _feedback.DesktopHeld = true;
            _relRot = Quaternion.Inverse(Quaternion.Euler(0, PlayerRig.Head.eulerAngles.y, 0)) * rb.rotation;
            _pivotToBottom = rb.position.y - Bounds(rb).min.y;
            SetIgnoreBody(rb, true);
        }

        void Drop(bool toss)
        {
            Rigidbody rb = Held;
            Held = null;
            if (rb == null) return;
            if (_feedback != null) _feedback.DesktopHeld = false;
            _feedback = null;
            rb.isKinematic = _wasKinematic;
            if (!rb.isKinematic && toss) rb.linearVelocity = PlayerRig.FlatForward * 0.4f;
            SetIgnoreBody(rb, false);
        }

        void TryUse()
        {
            Flashlight lamp = Held != null ? Held.GetComponent<Flashlight>() : Target != null ? Target.GetComponentInParent<Flashlight>() : null;
            if (lamp != null)
            {
                lamp.Toggle();
                return;
            }
        }

        void LateUpdate()
        {
            if (Held == null || PlayerRig.Head == null) return;
            Transform h = PlayerRig.Head;
            bool bucket = Held.GetComponent<SandBucket>() != null;
            bool pouring = bucket && UseHeld;

            Vector3 pos;
            Collider c = Raycast(out RaycastHit hit);
            TrashBin bin = c != null ? NearBin(hit.point) : null;
            if (bin != null)
                pos = bin.transform.position + Vector3.up * 1.2f;           // above the bin: dropping lets it fall in
            else if (c != null && hit.distance < reach)
            {
                pos = hit.point + Vector3.up * _pivotToBottom;              // rests where you look
                if (bucket && !pouring && c is TerrainCollider) pos -= Vector3.up * 0.12f; // pushed into the sand: scoop
            }
            else pos = h.position + h.forward * holdDistance;
            if (pouring) pos += Vector3.up * 0.45f;

            Quaternion yaw = Quaternion.Euler(0, h.eulerAngles.y, 0);
            Quaternion rot = yaw * _relRot;
            if (pouring) rot = Quaternion.AngleAxis(pourTilt, yaw * Vector3.forward) * rot;
            Held.transform.SetPositionAndRotation(pos, rot);
            Held.position = pos;
            Held.rotation = rot;
        }

        static TrashBin NearBin(Vector3 p)
        {
            foreach (var b in FindObjectsByType<TrashBin>(FindObjectsSortMode.None))
            {
                Vector3 d = p - b.transform.position;
                if (new Vector2(d.x, d.z).magnitude < 0.75f) return b;
            }
            return null;
        }

        static Bounds Bounds(Rigidbody rb)
        {
            var rs = rb.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(rb.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        void SetIgnoreBody(Rigidbody rb, bool ignore)
        {
            if (_body == null) return;
            foreach (var col in rb.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(_body, col, ignore);
        }
    }
}
