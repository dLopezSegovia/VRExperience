using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SameShore
{
    // =====================================================================================================
    // Scene 1: drink + music
    // =====================================================================================================

    /// <summary>A drink on the towel. Picking it up counts; bringing it to the mouth plays a sip.</summary>
    public class BeachDrink : MonoBehaviour
    {
        public bool PickedUp { get; private set; }
        public int Sips { get; private set; }
        public void ForceComplete() => PickedUp = true;
        VRGrabbable _g;
        float _lastSip;

        void Awake()
        {
            _g = GetComponent<VRGrabbable>();
            if (_g != null) _g.Grabbed += (g, h) => PickedUp = true;
        }

        void Update()
        {
            if (_g == null || !_g.IsHeld || PlayerRig.Head == null) return;
            Vector3 mouth = PlayerRig.Head.TransformPoint(new Vector3(0, -0.09f, 0.05f));
            bool atMouth = (transform.position - mouth).magnitude < 0.14f || (DesktopTestingMode.Active && _g.Holder.UseDown);
            if (atMouth && Time.time - _lastSip > 1.6f)
            {
                _lastSip = Time.time;
                Sips++;
                AudioOneShot.PlayAt(SameShoreAssets.I.Clip("sip"), mouth, 0.6f, 0.2f, 3f);
            }
        }
    }
}
