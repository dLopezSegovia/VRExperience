using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// The sandcastle activity. The sand comes from one dig spot: every shovelful deepens a real hole in the
    /// terrain (SandHole), which stays for the hatchling. A full pail turned over on the castle plot and
    /// lifted leaves a tower; with two towers the castle is finished (walls, gate and a little flag).
    /// </summary>
    public class SandcastleSite : MonoBehaviour
    {
        public static SandcastleSite Instance { get; private set; }

        [SerializeField] private SandHole hole;
        [SerializeField] private Transform digSpot;
        [SerializeField] private float digRadius = 0.55f;
        [SerializeField] private Transform plot;
        [SerializeField] private float plotRadius = 0.6f;
        [SerializeField] private SandPail pail;
        [SerializeField] private Transform[] towerSlots;
        [SerializeField] private Mesh towerMesh;
        [SerializeField] private Material sandMaterial;
        [SerializeField] private GameObject finishedExtras;
        [SerializeField] private int digsForFullHole = 6;
        [Tooltip("Guides for the human (spot marks), removed when the castle is finished.")]
        [SerializeField] private GameObject[] hideWhenFinished;

        public int Digs { get; private set; }
        public int Towers { get; private set; }
        public int TowersNeeded => towerSlots != null ? towerSlots.Length : 2;
        public bool Finished { get; private set; }
        public bool ShovelGrabbed { get; set; }
        public bool PailOnPlotUpsideDown { get; private set; }
        public Transform Plot => plot;
        public Transform DigSpot => digSpot;
        public SandPail Pail => pail;

        public event Action<int> TowerBuilt;

        ParticleSystem _dust;
        bool _pailPlaced;
        Transform _pendingTower;

        void Awake() => Instance = this;

        void Start()
        {
            _dust = SandFX.CreateBurst(transform, SameShoreAssets.I.sandParticle);
            if (finishedExtras != null) finishedExtras.SetActive(false);
            foreach (var s in towerSlots) if (s != null) s.gameObject.SetActive(false);
        }

        public bool TryDig(BeachShovel s)
        {
            Vector3 tip = s.Tip;
            Vector3 c = digSpot.position;
            float flat = new Vector2(tip.x - c.x, tip.z - c.z).magnitude;
            float ground = MeshKit.GroundY(tip);
            if (flat > digRadius || tip.y > ground + 0.04f) return false;
            Dig(s);
            return true;
        }

        void Dig(BeachShovel s)
        {
            s.SetLoaded(true);
            Digs++;
            if (hole != null) hole.SetDug01(Mathf.Clamp01(Digs / (float)digsForFullHole));
            _dust.transform.position = s.Tip;
            _dust.Emit(25);
            AudioOneShot.PlayAt(SameShoreAssets.I.Clip("shovel_dig"), s.Tip, 0.8f, 0.4f, 8f, UnityEngine.Random.Range(0.9f, 1.1f));
            XRInput.HapticBoth(0.3f, 0.08f);
            if (BeachLog.I != null) { BeachLog.I.HolePosition = digSpot.position; BeachLog.I.HoleDepth01 = hole != null ? hole.Dug01 : 0; }
        }

        public bool TryPour(BeachShovel s)
        {
            if (pail == null || pail.Full || pail.UpsideDown) return false;
            Vector3 mouth = pail.transform.position + pail.transform.up * 0.18f;
            if ((s.Tip - mouth).magnitude > 0.22f) return false;
            // tip the blade (or just hold it over the pail, desktop): the shovelful drops in
            if (Vector3.Dot(s.transform.up, Vector3.up) > 0.5f && !DesktopTestingMode.Active) return false;
            Pour(s);
            return true;
        }

        void Pour(BeachShovel s)
        {
            s.SetLoaded(false);
            pail.AddScoop();
            _dust.transform.position = s.Tip;
            _dust.Emit(18);
            AudioOneShot.PlayAt(SameShoreAssets.I.Clip("sand_dump"), s.Tip, 0.8f, 0.4f, 8f);
            XRInput.HapticBoth(0.2f, 0.06f);
        }

        /// <summary>Trigger with the shovel: dig if it is over the dig spot, otherwise pour into the pail if near.</summary>
        public void ShovelAction(BeachShovel s)
        {
            if (!s.Loaded)
            {
                Vector3 tip = s.Tip;
                if (new Vector2(tip.x - digSpot.position.x, tip.z - digSpot.position.z).magnitude < digRadius + 1.8f) Dig(s);
            }
            else if (pail != null && !pail.Full && (s.Tip - pail.transform.position).magnitude < 2.6f) Pour(s);
        }

        /// <summary>Trigger with the pail (desktop): flip it onto the plot, or lift it to reveal the tower.</summary>
        public void PailAction(SandPail p)
        {
            if (!p.Full || Finished) return;
            var g = p.Grab;
            if (g.IsHeld && (p.transform.position - plot.position).magnitude < 2.2f)
            {
                g.Holder.ForceRelease();
                Vector3 at = NextSlot().position;
                p.transform.SetPositionAndRotation(at + Vector3.up * 0.2f, Quaternion.Euler(180, 0, 0));
                p.GetComponent<Rigidbody>().position = p.transform.position;
                p.GetComponent<Rigidbody>().rotation = p.transform.rotation;
            }
        }

        Transform NextSlot() => towerSlots[Mathf.Clamp(Towers, 0, towerSlots.Length - 1)];

        void Update()
        {
            if (pail == null || Finished) return;
            Vector3 pp = pail.transform.position;
            bool onPlot = new Vector2(pp.x - plot.position.x, pp.z - plot.position.z).magnitude < plotRadius;
            bool onSand = pp.y - MeshKit.GroundY(pp) < 0.32f;
            PailOnPlotUpsideDown = onPlot && pail.UpsideDown && pail.Full;

            // full pail upside down on the plot, released (or pressed down) -> pat it: the sand settles inside
            if (PailOnPlotUpsideDown && onSand && !pail.Grab.IsHeld && !_pailPlaced)
            {
                _pailPlaced = true;
                AudioOneShot.PlayAt(SameShoreAssets.I.Clip("bucket_thump"), pp, 0.8f, 0.4f, 8f);
                XRInput.HapticBoth(0.4f, 0.1f);
                _pendingTower = NextSlot();
                SnapPail(_pendingTower);
            }
            // lifted again (grabbed and raised, or turned back) -> the tower stands where the pail was
            if (_pailPlaced && (pail.Grab.IsHeld && pp.y - MeshKit.GroundY(pp) > 0.12f || !pail.UpsideDown))
            {
                _pailPlaced = false;
                RevealTower(_pendingTower, new Vector3(pp.x, 0, pp.z));
            }
            // pail grabbed while upside down on the plot but it was never released: also fine if it is
            // pushed into the sand and lifted (VR natural motion)
            if (!_pailPlaced && PailOnPlotUpsideDown && pail.Grab.IsHeld && pp.y - MeshKit.GroundY(pp) < 0.2f)
            {
                _pailPlaced = true;
                _pendingTower = NextSlot();
                AudioOneShot.PlayAt(SameShoreAssets.I.Clip("bucket_thump"), pp, 0.8f, 0.4f, 8f);
                XRInput.HapticBoth(0.4f, 0.1f);
            }
        }

        /// <summary>The pail settles onto the next tower spot of the plot (a gentle magnet, so the castle stays tidy).</summary>
        void SnapPail(Transform slot)
        {
            if (slot == null) return;
            var rb = pail.GetComponent<Rigidbody>();
            // upside down: the rim rests on the sand, so the pail's origin (its bottom) is one pail-height up
            Vector3 p = new Vector3(slot.position.x, MeshKit.GroundY(slot.position) + 0.205f, slot.position.z);
            Quaternion r = Quaternion.Euler(180f, pail.transform.eulerAngles.y, 0f);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = p;
            rb.rotation = r;
            pail.transform.SetPositionAndRotation(p, r);
        }

        void RevealTower(Transform slot, Vector3 where)
        {
            if (slot == null) return;
            slot.gameObject.SetActive(true);
            StartCoroutine(Settle(slot));
            pail.Empty();
            if (Towers == 0) pail.ScoopsToFill = 2; // the second tower needs a little less sand (the plot is already wet)
            Towers++;
            _dust.transform.position = slot.position + Vector3.up * 0.05f;
            _dust.Emit(30);
            TowerBuilt?.Invoke(Towers);
            BeachLog.I?.Record("sand tower " + Towers, slot.position);
            if (Towers >= TowersNeeded) Finish();
        }

        IEnumerator Settle(Transform t)
        {
            Vector3 s = t.localScale;
            for (float k = 0; k < 0.35f; k += Time.deltaTime)
            {
                float a = k / 0.35f;
                t.localScale = new Vector3(s.x * (1.04f - 0.04f * a), s.y * (0.9f + 0.1f * a), s.z * (1.04f - 0.04f * a));
                yield return null;
            }
            t.localScale = s;
        }

        void Finish()
        {
            Finished = true;
            if (hideWhenFinished != null) foreach (var g in hideWhenFinished) if (g != null) g.SetActive(false);
            if (finishedExtras != null)
            {
                finishedExtras.SetActive(true);
                StartCoroutine(Grow(finishedExtras.transform));
            }
            if (hole != null && hole.Dug01 < 1f) hole.SetDug01(1f);
            AudioOneShot.PlayAt(SameShoreAssets.I.Clip("castle_done"), plot.position, 0.7f, 1f, 12f);
            if (BeachLog.I != null)
            {
                BeachLog.I.CastleBuilt = true;
                BeachLog.I.CastlePosition = plot.position;
                BeachLog.I.HolePosition = digSpot.position;
                BeachLog.I.HoleDepth01 = hole != null ? hole.Dug01 : 1f;
                BeachLog.I.Record("sandcastle finished; the hole stays open", digSpot.position);
            }
        }

        IEnumerator Grow(Transform t)
        {
            Vector3 s = t.localScale;
            for (float k = 0; k < 0.6f; k += Time.deltaTime)
            {
                t.localScale = new Vector3(s.x, s.y * Mathf.SmoothStep(0, 1, k / 0.6f), s.z);
                yield return null;
            }
            t.localScale = s;
        }

        /// <summary>Skip / defaults: dig the hole and build the castle instantly.</summary>
        public void ForceComplete()
        {
            Digs = digsForFullHole;
            if (hole != null) hole.SetDug01(1f);
            while (Towers < TowersNeeded)
            {
                var slot = NextSlot();
                slot.gameObject.SetActive(true);
                Towers++;
            }
            if (!Finished) Finish();
        }
    }
}
