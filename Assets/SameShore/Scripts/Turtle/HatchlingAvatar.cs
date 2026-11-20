using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// Procedural flipper animation for the hatchling model (player body and NPC siblings).
    /// Front flippers do a power stroke (sweep back + press down) per crawl stroke; rear flippers
    /// push alternately; idle breathing otherwise.
    /// </summary>
    public class HatchlingAvatar : MonoBehaviour
    {
        public Transform flipperFL, flipperFR, flipperBL, flipperBR, head;
        [Tooltip("Optional driver. When empty, Phase/Moving are set from code (NPC siblings).")]
        public TurtleController controller;
        public float sweepAngle = 55f;
        public float liftAngle = 18f;
        public float idleSpeed = 1.3f;

        /// <summary>Used when there is no controller: 0..1 stroke phase or -1.</summary>
        public float Phase { get; set; } = -1f;

        Quaternion _fl, _fr, _bl, _br, _head;
        float _idle;
        float _recover; // eases flippers back after a stroke

        void Awake()
        {
            if (flipperFL) _fl = flipperFL.localRotation;
            if (flipperFR) _fr = flipperFR.localRotation;
            if (flipperBL) _bl = flipperBL.localRotation;
            if (flipperBR) _br = flipperBR.localRotation;
            if (head) _head = head.localRotation;
            _idle = Random.value * 10f;
        }

        void LateUpdate()
        {
            float phase = controller != null ? controller.StrokePhase : Phase;
            bool backward = controller != null && controller.StrokeIsBackward;
            _idle += Time.deltaTime * idleSpeed;

            float sweep, lift, rear;
            if (phase >= 0f)
            {
                // reach forward (0-0.15), power stroke back (0.15-0.7), recover (0.7-1)
                float s = phase < 0.15f ? -Mathf.Sin(phase / 0.15f * Mathf.PI * 0.5f)
                        : phase < 0.7f ? Mathf.Lerp(-1f, 1f, Mathf.SmoothStep(0, 1, (phase - 0.15f) / 0.55f))
                        : Mathf.Lerp(1f, 0f, (phase - 0.7f) / 0.3f);
                if (backward) s = -s;
                sweep = s;
                lift = Mathf.Sin(phase * Mathf.PI) * (phase < 0.7f ? -1f : 1f);
                rear = Mathf.Sin(phase * Mathf.PI * 2f);
                _recover = 1f;
            }
            else
            {
                _recover = Mathf.MoveTowards(_recover, 0f, Time.deltaTime * 3f);
                sweep = Mathf.Sin(_idle) * 0.08f;
                lift = Mathf.Sin(_idle * 0.7f) * 0.1f;
                rear = Mathf.Sin(_idle * 0.9f) * 0.1f;
            }

            Vector3 up = transform.up, fwd = transform.forward;
            Pose(flipperFL, _fl, -sweep * sweepAngle, lift * liftAngle, up, fwd, -1);
            Pose(flipperFR, _fr, sweep * sweepAngle, lift * liftAngle, up, fwd, 1);
            Pose(flipperBL, _bl, rear * 25f, 0f, up, fwd, -1);
            Pose(flipperBR, _br, -rear * 25f, 0f, up, fwd, 1);
            if (head) head.localRotation = _head * Quaternion.Euler(Mathf.Sin(_idle * 0.6f) * 4f, Mathf.Sin(_idle * 0.37f) * 6f, 0);
        }

        static void Pose(Transform t, Quaternion rest, float yawDeg, float rollDeg, Vector3 worldUp, Vector3 worldFwd, int side)
        {
            if (t == null) return;
            Transform p = t.parent;
            Vector3 upL = p != null ? p.InverseTransformDirection(worldUp) : worldUp;
            Vector3 fwdL = p != null ? p.InverseTransformDirection(worldFwd) : worldFwd;
            t.localRotation = Quaternion.AngleAxis(yawDeg, upL) * Quaternion.AngleAxis(rollDeg * side, fwdL) * rest;
        }
    }
}
