using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// Marks the beach chair and makes it easier to carry: low centre of mass, damping so it does not
    /// spin or bounce, and it settles upright on its legs when dropped roughly the right way up.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BeachChairMarker : MonoBehaviour
    {
        [SerializeField] private Vector3 centerOfMass = new Vector3(0f, 0.25f, 0f);
        [SerializeField] private float uprightTorque = 18f;

        Rigidbody _rb;
        GrabFeedback _grab;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _grab = GetComponent<GrabFeedback>();
            // world-space COM low between the legs, whatever the imported axes are
            _rb.centerOfMass = transform.InverseTransformPoint(transform.position + Vector3.up * centerOfMass.y) ;
        }

        void FixedUpdate()
        {
            if (_rb.isKinematic || (_grab != null && _grab.IsHeld)) return;
            // gentle self-righting so a dropped chair lands on its legs instead of tumbling oddly
            Vector3 up = UpAxis();
            Vector3 axis = Vector3.Cross(up, Vector3.up);
            float angle = Vector3.Angle(up, Vector3.up);
            if (angle > 2f && angle < 70f) _rb.AddTorque(axis.normalized * angle * Mathf.Deg2Rad * uprightTorque, ForceMode.Acceleration);
        }

        /// <summary>The chair's world "up" (the monobloc FBX is rotated -90 on X, so local +Z points up).</summary>
        Vector3 UpAxis()
        {
            Vector3[] c = { transform.up, -transform.up, transform.forward, -transform.forward, transform.right, -transform.right };
            Vector3 best = c[0];
            foreach (var v in c) if (Vector3.Dot(v, Vector3.up) > Vector3.Dot(best, Vector3.up)) best = v;
            return best;
        }
    }
}
