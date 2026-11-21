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

        // inner wall of the pail mesh (see SameShoreSceneBuilder.PailMesh): r = 0.068 at y 0.012 -> 0.092 at y 0.195
        const float FloorY = 0.014f, RimY = 0.188f;
        static float InnerRadius(float y) => Mathf.Lerp(0.068f, 0.092f, Mathf.InverseLerp(0.012f, 0.195f, y)) - 0.004f;

        Mesh _fillMesh;
        int _shownScoops = -1;

        /// <summary>
        /// The sand inside is rebuilt as a mesh in the pail's own space, clamped to the inner wall (a frustum up to
        /// the current level with a small mound on top). It never scales a unit mesh, so it can never poke through
        /// the bottom or the sides (the old version scaled a 1 m tall mesh up to 1 m: the "giant cylinder").
        /// </summary>
        void Refresh()
        {
            if (fillVisual == null || _shownScoops == Scoops) return;
            _shownScoops = Scoops;
            float k = Scoops / (float)scoopsToFill;
            fillVisual.gameObject.SetActive(k > 0.01f);
            fillVisual.localPosition = Vector3.zero;
            fillVisual.localRotation = Quaternion.identity;
            fillVisual.localScale = Vector3.one;
            if (k <= 0.01f) return;
            float top = Mathf.Lerp(FloorY + 0.02f, Mathf.Min(RimY, FloorY + fullHeight), k);
            float rTop = InnerRadius(top);
            float mound = 0.006f + 0.012f * k;
            var profile = new[]
            {
                new Vector2(0.001f, FloorY), new Vector2(InnerRadius(FloorY), FloorY), new Vector2(rTop, top),
                new Vector2(rTop * 0.55f, top + mound * 0.7f), new Vector2(0.001f, top + mound)
            };
            var m = MeshKit.Lathe("PailSand", profile, 18, false, false);
            var mf = fillVisual.GetComponent<MeshFilter>();
            if (_fillMesh != null) Destroy(_fillMesh);
            _fillMesh = m;
            if (mf != null) mf.sharedMesh = m;
        }

        public void OnUse(VRHand hand)
        {
            var site = FindFirstObjectByType<SandcastleSite>();
            if (site != null) site.PailAction(this);
        }
    }
}
