using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SameShore
{
    // =====================================================================================================
    // Scene 3: sandcastle
    // =====================================================================================================

    /// <summary>The shovel: dig in the sand patch to load it, tip it over the pail to empty it.</summary>
    public class BeachShovel : MonoBehaviour, IVRUsable
    {
        [SerializeField] private Transform bladeTip;
        [SerializeField] private GameObject sandLoad;

        public bool Loaded { get; private set; }
        VRGrabbable _g;
        float _cool;

        void Awake()
        {
            _g = GetComponent<VRGrabbable>();
            SetLoaded(false);
        }

        public Vector3 Tip => bladeTip != null ? bladeTip.position : transform.position;

        public void SetLoaded(bool on)
        {
            Loaded = on;
            if (sandLoad != null) sandLoad.SetActive(on);
        }

        public void OnUse(VRHand hand)
        {
            // desktop / accessibility: the trigger performs the next sensible action at what you aim at
            var site = FindFirstObjectByType<SandcastleSite>();
            if (site != null) site.ShovelAction(this);
        }

        void Update()
        {
            if (_g == null || !_g.IsHeld) return;
            _cool -= Time.deltaTime;
            if (_cool > 0) return;
            var site = SandcastleSite.Instance;
            if (site == null) return;
            if (!Loaded && site.TryDig(this)) _cool = 0.5f;
            else if (Loaded && site.TryPour(this)) _cool = 0.5f;
        }
    }
}
