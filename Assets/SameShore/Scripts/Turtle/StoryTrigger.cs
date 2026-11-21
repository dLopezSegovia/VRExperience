using System;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// A narrative trigger volume across the hatchling's corridor (music, gulls, the "jellyfish"). Its box spans the
    /// whole corridor between the berms, so the way to the sea always crosses it. It tests the hatchling's position
    /// against the box every frame (robust at any speed, no physics callbacks), and fires once per act.
    /// </summary>
    public class StoryTrigger : MonoBehaviour
    {
        public string Id = "trigger";
        [Tooltip("Box in local space (no physics collider: nothing can bump into or raycast it).")]
        public Vector3 size = new Vector3(6f, 1.5f, 0.6f);
        public Vector3 center = new Vector3(0f, 0.5f, 0f);
        public bool Fired { get; private set; }
        public bool Inside { get; private set; }
        public event Action<StoryTrigger> Entered;

        Transform _target;

        public void Arm(Transform target)
        {
            _target = target;
            Fired = false;
            Inside = false;
        }

        public void Disarm() => _target = null;

        public bool Contains(Vector3 p)
        {
            Vector3 l = transform.InverseTransformPoint(p) - center;
            Vector3 h = size * 0.5f;
            return Mathf.Abs(l.x) <= h.x && Mathf.Abs(l.y) <= h.y && Mathf.Abs(l.z) <= h.z;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(center, size);
        }

        void Update()
        {
            if (_target == null) return;
            Inside = Contains(_target.position);
            if (Inside && !Fired)
            {
                Fired = true;
                Entered?.Invoke(this);
            }
        }
    }
}
