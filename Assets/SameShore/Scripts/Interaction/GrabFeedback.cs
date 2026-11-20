using Oculus.Interaction;
using UnityEngine;

namespace SameShore
{
    /// <summary>Sound + haptics when an ISDK Grabbable is picked up, and a soft thud when it lands on the sand.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class GrabFeedback : MonoBehaviour
    {
        [SerializeField] private Grabbable grabbable;
        [SerializeField] private float thudVolume = 0.6f;
        [SerializeField] private float minImpactSpeed = 0.8f;

        public bool IsHeld => grabbable != null && grabbable.SelectingPointsCount > 0;
        public bool WasEverMoved { get; private set; }

        bool _held;
        float _lastThud;

        void Reset() => grabbable = GetComponent<Grabbable>();

        void Awake()
        {
            if (grabbable == null) grabbable = GetComponent<Grabbable>();
        }

        void Update()
        {
            bool held = IsHeld;
            if (held && !_held)
            {
                WasEverMoved = true;
                AudioOneShot.PlayAt(SameShoreAssets.I.pickup, transform.position, 0.5f, 0.3f, 6f, Random.Range(0.9f, 1.15f));
                XRInput.HapticBoth(0.25f, 0.05f);
            }
            _held = held;
        }

        void OnCollisionEnter(Collision c)
        {
            if (_held || Time.time - _lastThud < 0.25f || c.relativeVelocity.magnitude < minImpactSpeed) return;
            _lastThud = Time.time;
            float v = Mathf.Clamp01(c.relativeVelocity.magnitude / 4f);
            AudioOneShot.PlayAt(SameShoreAssets.I.thud, transform.position, thudVolume * v, 0.4f, 8f, Random.Range(0.9f, 1.2f));
        }
    }
}
