using Oculus.Interaction;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// Holds an object in a defined grip pose instead of the arbitrary offset it had when grabbed.
    /// While the ISDK Grabbable is selected, the object is placed relative to the controller anchor
    /// that holds it (the closest one at grab time). For the flashlight: beam (local +Z) along the
    /// controller's pointing direction, body in the fist.
    /// Runs in LateUpdate, after the ISDK grab transformer, so it wins; release/throw stay ISDK's.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class GripAlignment : MonoBehaviour
    {
        [SerializeField] private Grabbable grabbable;
        [Tooltip("Object position in the controller anchor's space (metres).")]
        [SerializeField] private Vector3 positionOffset = new Vector3(0f, -0.012f, 0.035f);
        [Tooltip("Object rotation relative to the controller anchor (degrees).")]
        [SerializeField] private Vector3 rotationOffset = Vector3.zero;
        [Tooltip("Desktop testing: where the object is held relative to the camera.")]
        [SerializeField] private Vector3 desktopOffset = new Vector3(0.2f, -0.2f, 0.42f);

        public Vector3 DesktopOffset => desktopOffset;
        public Quaternion RotationOffset => Quaternion.Euler(rotationOffset);

        Transform _hand;
        Rigidbody _rb;

        void Reset() => grabbable = GetComponent<Grabbable>();

        void Awake()
        {
            if (grabbable == null) grabbable = GetComponent<Grabbable>();
            _rb = GetComponent<Rigidbody>();
        }

        void LateUpdate()
        {
            bool held = grabbable != null && grabbable.SelectingPointsCount > 0;
            if (!held) { _hand = null; return; }
            if (_hand == null) _hand = ClosestHand();
            if (_hand == null) return;
            Vector3 p = _hand.TransformPoint(positionOffset);
            Quaternion r = _hand.rotation * Quaternion.Euler(rotationOffset);
            transform.SetPositionAndRotation(p, r);
            if (_rb != null && _rb.isKinematic) { _rb.position = p; _rb.rotation = r; }
        }

        Transform ClosestHand()
        {
            Transform l = PlayerRig.Hand(Hand.Left), r = PlayerRig.Hand(Hand.Right);
            if (l == null) return r;
            if (r == null) return l;
            return (l.position - transform.position).sqrMagnitude < (r.position - transform.position).sqrMagnitude ? l : r;
        }
    }
}
