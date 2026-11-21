using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SameShore
{
    /// <summary>
    /// An object the human can pick up with the grip button (Quest controllers) or E / left click (desktop).
    ///  - Optional held pose (position + rotation in the controller anchor's frame): used for tools that must
    ///    point where the hand points (flashlight, phone, shovel). Without it the grab keeps the natural offset.
    ///  - Released objects keep the hand's real velocity (averaged over the last frames), so throwing is physical.
    ///  - The trigger while holding calls every IVRUsable on the object.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VRGrabbable : MonoBehaviour
    {
        public static readonly List<VRGrabbable> All = new List<VRGrabbable>();

        [Tooltip("Hold with a fixed pose relative to the controller (tools). Off = keep the grab offset.")]
        public bool usePose;
        [Tooltip("Object position in the RIGHT controller anchor's frame (mirrored on X for the left hand).")]
        public Vector3 posePosition = new Vector3(0f, -0.01f, 0.04f);
        public Vector3 poseEuler;
        [Tooltip("Desktop testing: where the object is held relative to the camera.")]
        public Vector3 desktopOffset = new Vector3(0.22f, -0.22f, 0.5f);
        public Vector3 desktopEuler;
        [Tooltip("Multiplier on the hand velocity when released (1 = physical).")]
        public float throwScale = 1.1f;
        public bool throwable = true;
        [Tooltip("Grab distance from the controller (m).")]
        public float grabRadius = 0.13f;
        public string displayName = "";
        public bool grabbingEnabled = true;

        public VRHand Holder { get; private set; }
        public bool IsHeld => Holder != null;
        public Rigidbody Body { get; private set; }
        public bool WasEverGrabbed { get; private set; }
        public float LastReleaseSpeed { get; private set; }
        public Vector3 LastReleaseVelocity { get; private set; }

        public event Action<VRGrabbable, VRHand> Grabbed;
        public event Action<VRGrabbable, VRHand> Released;

        Collider[] _cols;
        bool _wasKinematic;
        float _lastThud;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            _cols = GetComponentsInChildren<Collider>();
        }

        void OnEnable() => All.Add(this);

        void OnDisable()
        {
            All.Remove(this);
            if (Holder != null) Holder.ForceRelease();
        }

        public string Label => string.IsNullOrEmpty(displayName) ? name.Replace("_", " ") : displayName;

        public Collider[] Colliders => _cols;

        /// <summary>Distance from a point to the closest collider surface (0 inside).</summary>
        public float DistanceTo(Vector3 p)
        {
            float best = float.MaxValue;
            foreach (var c in _cols)
            {
                if (c == null || !c.enabled || c.isTrigger) continue;
                Vector3 q = c is MeshCollider mc && !mc.convex ? c.bounds.ClosestPoint(p) : c.ClosestPoint(p);
                best = Mathf.Min(best, (q - p).magnitude);
            }
            return best == float.MaxValue ? (transform.position - p).magnitude : best;
        }

        internal void OnGrab(VRHand h)
        {
            Holder = h;
            WasEverGrabbed = true;
            _wasKinematic = Body.isKinematic;
            Body.isKinematic = true;
            Body.interpolation = RigidbodyInterpolation.None;
            SetIgnorePlayer(true);
            AudioOneShot.PlayAt(SameShoreAssets.I.pickup, transform.position, 0.45f, 0.3f, 6f, UnityEngine.Random.Range(0.9f, 1.15f));
            Grabbed?.Invoke(this, h);
        }

        internal void OnRelease(VRHand h, Vector3 velocity, Vector3 angular)
        {
            Holder = null;
            Body.isKinematic = _wasKinematic;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            if (!Body.isKinematic)
            {
                Vector3 v = throwable ? velocity * throwScale : velocity * 0.3f;
                Body.linearVelocity = v;
                Body.angularVelocity = throwable ? angular : Vector3.zero;
                LastReleaseVelocity = v;
                LastReleaseSpeed = v.magnitude;
            }
            // collide with the player again once it is clear of the body
            Invoke(nameof(RestorePlayerCollision), 0.5f);
            Released?.Invoke(this, h);
        }

        void RestorePlayerCollision() { if (!IsHeld) SetIgnorePlayer(false); }

        void SetIgnorePlayer(bool ignore)
        {
            Transform root = PlayerRig.Root;
            if (root == null) return;
            var body = root.GetComponent<CharacterController>();
            if (body == null) return;
            foreach (var c in _cols) if (c != null) Physics.IgnoreCollision(body, c, ignore);
        }

        internal void Use(VRHand h)
        {
            foreach (var u in GetComponents<IVRUsable>()) u.OnUse(h);
        }

        void OnCollisionEnter(Collision c)
        {
            if (IsHeld || Time.time - _lastThud < 0.25f || c.relativeVelocity.magnitude < 0.9f) return;
            _lastThud = Time.time;
            float v = Mathf.Clamp01(c.relativeVelocity.magnitude / 4f);
            AudioOneShot.PlayAt(SameShoreAssets.I.thud, transform.position, 0.5f * v, 0.4f, 8f, UnityEngine.Random.Range(0.9f, 1.2f));
        }
    }
}
