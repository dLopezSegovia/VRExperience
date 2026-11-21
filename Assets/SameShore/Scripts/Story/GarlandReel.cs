using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SameShore
{
    /// <summary>On the reel: the trigger clips the string to the next hook when it is close enough.</summary>
    public class GarlandReel : UnityEngine.MonoBehaviour, IVRUsable
    {
        [UnityEngine.SerializeField] private LightGarland garland;
        public void OnUse(VRHand hand) { if (garland != null) garland.OnUse(hand); }
    }
}
