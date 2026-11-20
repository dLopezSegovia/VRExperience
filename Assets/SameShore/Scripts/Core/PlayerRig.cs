using UnityEngine;

namespace SameShore
{
    /// <summary>Cached access to the Meta OVRCameraRig (head, hands, current player scale).</summary>
    public static class PlayerRig
    {
        static OVRCameraRig _rig;

        public static OVRCameraRig Rig
        {
            get
            {
                if (_rig == null) _rig = Object.FindFirstObjectByType<OVRCameraRig>();
                return _rig;
            }
        }

        public static Transform Head =>
            Rig != null ? Rig.centerEyeAnchor : (Camera.main != null ? Camera.main.transform : null);

        public static Camera HeadCamera => Head != null ? Head.GetComponent<Camera>() : Camera.main;

        /// <summary>Root object that moves the whole player (camera + hands).</summary>
        public static Transform Root
        {
            get
            {
                if (Rig == null) return null;
                Transform t = Rig.transform;
                // the building block rig is the top object of the player hierarchy unless parented to the turtle
                while (t.parent != null && t.parent.GetComponent<TurtleController>() == null) t = t.parent;
                return t;
            }
        }

        public static Transform Hand(Hand h)
        {
            if (Rig == null) return Head;
            return h == SameShore.Hand.Left ? Rig.leftControllerAnchor : Rig.rightControllerAnchor;
        }

        /// <summary>1 for the human, 0.05 for the hatchling.</summary>
        public static float Scale => Head != null ? Head.lossyScale.x : 1f;

        /// <summary>Yaw-only forward of the head.</summary>
        public static Vector3 FlatForward
        {
            get
            {
                if (Head == null) return Vector3.forward;
                Vector3 f = Vector3.ProjectOnPlane(Head.forward, Vector3.up);
                return f.sqrMagnitude < 1e-4f ? Vector3.ProjectOnPlane(Head.up, Vector3.up).normalized : f.normalized;
            }
        }
    }
}
