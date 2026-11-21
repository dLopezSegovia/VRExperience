using UnityEngine;

namespace SameShore
{
    /// <summary>The trigger switches the team's Flashlight while it is held with the VRHand grab.</summary>
    [RequireComponent(typeof(Flashlight))]
    public class FlashlightUse : MonoBehaviour, IVRUsable
    {
        public void OnUse(VRHand hand)
        {
            var f = GetComponent<Flashlight>();
            f.Toggle();
            if (BeachLog.I != null) { BeachLog.I.FlashlightOn = f.IsOn; BeachLog.I.FlashlightPosition = transform.position; }
        }
    }
}
