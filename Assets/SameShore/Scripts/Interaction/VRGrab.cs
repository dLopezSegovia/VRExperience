using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SameShore
{
    /// <summary>Components on a grabbable object that react to the controller trigger while it is held.</summary>
    public interface IVRUsable
    {
        void OnUse(VRHand hand);
    }
}
