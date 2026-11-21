using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SameShore
{
    // =====================================================================================================
    // Scene 2: can toss
    // =====================================================================================================

    /// <summary>An empty can for the toss challenge.</summary>
    public class TossCan : MonoBehaviour
    {
        public BeachLog.CanResult Result { get; internal set; }
        public bool Thrown { get; internal set; }
        public Vector3 StartPosition { get; private set; }
        internal float throwTime;
        internal float restTime;

        void Awake() => StartPosition = transform.position;
    }
}
