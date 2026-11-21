using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SameShore
{
    // =====================================================================================================
    // Scene 4: snack and the wind-blown bag
    // =====================================================================================================

    /// <summary>The sandwich. Bring it to the mouth (or press the trigger) to take a bite; three bites and it is gone.</summary>
    public class Sandwich : MonoBehaviour, IVRUsable
    {
        [SerializeField] private int bites = 3;
        [SerializeField] private WindBag wrapper;

        public int Bites { get; private set; }
        public bool Eaten => Bites >= bites;
        public int BitesNeeded => bites;
        public bool PickedUp { get; private set; }
        public WindBag Wrapper => wrapper;

        VRGrabbable _g;
        float _last;
        Vector3 _scale;
        ParticleSystem _crumbs;

        void Awake()
        {
            _g = GetComponent<VRGrabbable>();
            _scale = transform.localScale;
            if (_g != null) _g.Grabbed += (g, h) => PickedUp = true;
        }

        void Start() => _crumbs = SandFX.CreateBurst(transform, SameShoreAssets.I.sandParticle);

        void Update()
        {
            if (_g == null || !_g.IsHeld || Eaten || PlayerRig.Head == null) return;
            Vector3 mouth = PlayerRig.Head.TransformPoint(new Vector3(0, -0.08f, 0.06f));
            if ((transform.position - mouth).magnitude < 0.15f) Bite();
        }

        public void OnUse(VRHand hand) => Bite();

        public void Bite()
        {
            if (Eaten || Time.time - _last < 1.1f) return;
            _last = Time.time;
            Bites++;
            if (Bites == 1 && wrapper != null) wrapper.Unwrap(transform);
            AudioOneShot.PlayAt(SameShoreAssets.I.Clip("bite"), transform.position, 0.7f, 0.2f, 3f, UnityEngine.Random.Range(0.92f, 1.08f));
            if (_crumbs != null) { _crumbs.transform.position = transform.position; _crumbs.Emit(8); }
            float k = 1f - Bites / (float)bites;
            transform.localScale = new Vector3(_scale.x, _scale.y, _scale.z * Mathf.Max(0.25f, k));
            if (Eaten) StartCoroutine(Finish());
        }

        IEnumerator Finish()
        {
            yield return new WaitForSeconds(0.4f);
            if (_g.IsHeld) _g.Holder.ForceRelease();
            gameObject.SetActive(false);
        }

        public void ForceComplete()
        {
            PickedUp = true;
            while (!Eaten) { _last = -9; Bite(); }
            if (wrapper != null && wrapper.State == WindBag.Phase.Wrapped) wrapper.Unwrap(transform);
            gameObject.SetActive(false);
        }
    }
}
