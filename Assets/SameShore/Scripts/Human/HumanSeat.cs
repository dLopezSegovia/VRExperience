using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// Sitting on the beach chair (start of the day, can toss). The player is placed on the seat under a
    /// fade and the tracking space is offset once so the eyes are at seated height, whether the real
    /// player sits or stands. Walking is off while seated; standing up restores everything.
    /// </summary>
    public class HumanSeat : MonoBehaviour
    {
        [SerializeField] private HumanAvatar avatar;
        [SerializeField] private SmoothLocomotion locomotion;
        [SerializeField] private float seatedEyeHeight = 1.02f;

        public bool Seated { get; private set; }
        public Transform Seat { get; private set; }

        float _offset;
        float _desktopEye = 1.65f;

        public void Sit(Transform seat)
        {
            if (Seated) Stand();
            Seat = seat;
            Seated = true;
            locomotion.MovementEnabled = false;
            locomotion.PlaceAt(MeshKit.OnGround(seat.position - seat.forward * 0.05f), seat.eulerAngles.y);
            var rig = PlayerRig.Rig;
            if (DesktopTestingMode.Active && DesktopTestingMode.Instance != null)
            {
                _desktopEye = DesktopTestingMode.Instance.EyeHeight;
                DesktopTestingMode.Instance.EyeHeight = seatedEyeHeight;
            }
            else if (rig != null && PlayerRig.Root != null)
            {
                float headY = PlayerRig.Head.position.y - PlayerRig.Root.position.y;
                _offset = seatedEyeHeight - headY;
                rig.trackingSpace.localPosition += Vector3.up * _offset;
            }
            if (avatar != null) avatar.SetSeat(seat);
        }

        public void Stand()
        {
            if (!Seated) return;
            Seated = false;
            var rig = PlayerRig.Rig;
            if (DesktopTestingMode.Active && DesktopTestingMode.Instance != null) DesktopTestingMode.Instance.EyeHeight = _desktopEye;
            else if (rig != null) rig.trackingSpace.localPosition -= Vector3.up * _offset;
            _offset = 0;
            if (avatar != null) avatar.SetSeat(null);
            // step out in front of the chair
            if (Seat != null) locomotion.PlaceAt(MeshKit.OnGround(Seat.position + Seat.forward * 0.6f), Seat.eulerAngles.y);
            locomotion.MovementEnabled = true;
            Seat = null;
        }
    }
}
