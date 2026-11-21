using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// Three empty cans, a bin a few metres away. A can counts as thrown when released with some speed;
    /// it scores if it ends inside the bin, otherwise it stays on the sand exactly where it landed
    /// (and the hatchling will find it there).
    /// </summary>
    public class CanTossGame : MonoBehaviour
    {
        [SerializeField] private TossCan[] cans;
        [SerializeField] private Collider binTrigger;
        [SerializeField] private TMPro.TextMeshPro scoreLabel;
        [SerializeField] private float minThrowSpeed = 1.1f;

        public int Thrown { get; private set; }
        public int Scored { get; private set; }
        public int Total => cans != null ? cans.Length : 0;
        public bool Done => Total > 0 && Resolved >= Total;
        public int Resolved { get; private set; }
        public bool Active { get; set; }
        public IReadOnlyList<TossCan> Cans => cans;

        public event Action<TossCan, bool> CanResolved;

        void Start()
        {
            foreach (var c in cans)
            {
                var g = c.GetComponent<VRGrabbable>();
                var can = c;
                g.Released += (gg, h) => OnReleased(can, gg);
                g.Grabbed += (gg, h) => { if (!can.Thrown) can.restTime = 0; };
            }
            UpdateLabel();
        }

        void OnReleased(TossCan can, VRGrabbable g)
        {
            if (can.Thrown) return;
            bool far = (can.transform.position - can.StartPosition).magnitude > 1.2f;
            if (g.LastReleaseSpeed < minThrowSpeed && !far) return;   // just put down next to the chair
            can.Thrown = true;
            can.throwTime = Time.time;
            g.grabbingEnabled = false;                                  // one throw per can, like the game
            Thrown++;
            UpdateLabel();
            BeachLog.I?.Record("can thrown", can.transform.position);
        }

        void Update()
        {
            if (cans == null) return;
            foreach (var c in cans)
            {
                if (!c.Thrown || c.Result != BeachLog.CanResult.Unthrown) continue;
                if (InBin(c)) { Resolve(c, true); continue; }
                var rb = c.GetComponent<Rigidbody>();
                // dry sand stops a rolling can quickly (it does not roll all the way down the beach)
                if (Time.time - c.throwTime > 0.35f && MeshKit.GroundY(c.transform.position) > c.transform.position.y - 0.08f)
                {
                    rb.angularDamping = 4f;
                    rb.linearDamping = 1.5f;
                }
                bool still = rb.linearVelocity.magnitude < 0.08f && rb.angularVelocity.magnitude < 0.5f;
                c.restTime = still ? c.restTime + Time.deltaTime : 0f;
                if ((c.restTime > 0.6f && Time.time - c.throwTime > 0.5f) || Time.time - c.throwTime > 7f) Resolve(c, false);
            }
        }

        bool InBin(TossCan c)
        {
            if (binTrigger == null) return false;
            Bounds b = binTrigger.bounds;
            return b.Contains(c.transform.position);
        }

        void Resolve(TossCan c, bool scored)
        {
            c.Result = scored ? BeachLog.CanResult.Scored : BeachLog.CanResult.Missed;
            if (scored) { Scored++; c.gameObject.AddComponent<StayInBin>(); }
            Resolved++;
            if (scored) AudioOneShot.PlayAt(SameShoreAssets.I.Clip("can_bin"), c.transform.position, 0.8f, 0.5f, 12f);
            if (BeachLog.I != null)
            {
                BeachLog.I.Cans.Add(new BeachLog.CanRecord { id = c.name, result = c.Result, restPosition = c.transform.position, can = c.transform });
                BeachLog.I.Record(scored ? "can scored" : "can missed, left on the sand", c.transform.position);
            }
            UpdateLabel();
            CanResolved?.Invoke(c, scored);
        }

        /// <summary>Skip / defaults: throw the remaining cans deterministically (one in, the rest short).</summary>
        public void ForceComplete()
        {
            int k = 0;
            foreach (var c in cans)
            {
                if (c.Thrown) continue;
                var g = c.GetComponent<VRGrabbable>();
                if (g.IsHeld) g.Holder.ForceRelease();
                c.Thrown = true;
                c.throwTime = Time.time;
                g.grabbingEnabled = false;
                Thrown++;
                var rb = c.GetComponent<Rigidbody>();
                Vector3 target = binTrigger != null ? binTrigger.bounds.center : c.transform.position + Vector3.forward * 3f;
                // "missed" landing spots fall short of the bin, across the hatchlings' path
                Vector3 land = Vector3.Lerp(c.StartPosition, target, 0.55f + 0.12f * k) + new Vector3(0.35f * (k - 1), 0, 0.3f * ((k % 2) * 2 - 1));
                rb.position = MeshKit.OnGround(land, 0.04f);
                rb.rotation = Quaternion.Euler(90, 40 * k, 0);
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                c.transform.SetPositionAndRotation(rb.position, rb.rotation);
                k++;
            }
            UpdateLabel();
        }

        void UpdateLabel()
        {
            if (scoreLabel == null) return;
            scoreLabel.text = $"<b>CAN TOSS</b>\n<size=70%>{Thrown}/{Total} THROWN  ·  {Scored} IN</size>";
        }
    }
}
