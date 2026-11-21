using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SameShore
{
    /// <summary>The beach pail: filled by shovelfuls; turned upside down on the sand and lifted, it leaves a tower.</summary>
    public class SandPail : MonoBehaviour, IVRUsable
    {
        [SerializeField] private Transform fillVisual;
        [SerializeField] private float fullHeight = 0.15f;
        [SerializeField] private int scoopsToFill = 3;

        public int Scoops { get; private set; }
        public bool Full => Scoops >= scoopsToFill;
        public int ScoopsToFill { get => scoopsToFill; set => scoopsToFill = Mathf.Max(1, value); }
        public bool UpsideDown => Vector3.Dot(transform.up, Vector3.up) < -0.6f;
        public VRGrabbable Grab { get; private set; }

        void Awake()
        {
            Grab = GetComponent<VRGrabbable>();
            Refresh();
        }

        public void AddScoop()
        {
            Scoops = Mathf.Min(scoopsToFill, Scoops + 1);
            Refresh();
        }

        public void Empty()
        {
            Scoops = 0;
            Refresh();
        }

        void Refresh()
        {
            if (fillVisual == null) return;
            float k = Scoops / (float)scoopsToFill;
            fillVisual.gameObject.SetActive(k > 0.01f);
            fillVisual.localScale = new Vector3(1, Mathf.Max(0.02f, k), 1);
            fillVisual.localPosition = new Vector3(0, 0.02f + k * fullHeight * 0.5f, 0);
        }

        public void OnUse(VRHand hand)
        {
            var site = FindFirstObjectByType<SandcastleSite>();
            if (site != null) site.PailAction(this);
        }
    }
}
