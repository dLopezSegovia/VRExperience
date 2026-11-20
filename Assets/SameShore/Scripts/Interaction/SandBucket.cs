using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// Bucket of sand. Tip it over (mouth down) above the hole to pour; dip it into the sand
    /// to scoop a new load. Works with the existing ISDK grab setup of the Wooden_Bucket prefab.
    /// </summary>
    public class SandBucket : MonoBehaviour
    {
        [SerializeField] private Transform sandFill;          // disc showing the sand level
        [SerializeField] private float mouthHeight = 0.36f;    // local height of the rim
        [SerializeField] private float mouthRadius = 0.15f;
        [SerializeField] private float pourAngle = 95f;        // tilt (deg) at which sand starts to fall
        [SerializeField] private float pourRate = 0.55f;       // buckets per second
        [SerializeField] private float scoopDepth = 0.06f;
        [SerializeField] private float fillLowY = 0.05f, fillHighY = 0.31f;
        [SerializeField, Range(0, 1)] private float startSand = 1f;

        public float Sand { get; private set; }

        ParticleSystem _fx;
        AudioSource _pour;
        GrabFeedback _grab;

        void Start()
        {
            Sand = startSand;
            _grab = GetComponent<GrabFeedback>();
            BuildFx();
            _pour = gameObject.AddComponent<AudioSource>();
            _pour.clip = SameShoreAssets.I.sandPour;
            _pour.loop = true;
            _pour.spatialBlend = 1f;
            _pour.minDistance = 0.5f;
            _pour.maxDistance = 10f;
            _pour.volume = 0f;
            _pour.Play();
            UpdateVisual();
        }

        void BuildFx()
        {
            var go = new GameObject("SandStream");
            go.transform.SetParent(transform, false);
            _fx = go.AddComponent<ParticleSystem>();
            _fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _fx.main;
            main.startLifetime = 0.9f;
            main.startSpeed = 0.2f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.02f);
            main.startColor = new Color(0.62f, 0.53f, 0.4f);
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            var em = _fx.emission;
            em.rateOverTime = 0f;
            var shape = _fx.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = mouthRadius * 0.6f;
            var col = _fx.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.World;
            col.bounce = 0.05f;
            col.lifetimeLoss = 0.6f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = SameShoreAssets.I.sandParticle;
            _fx.Play();
        }

        void Update()
        {
            Vector3 mouth = transform.TransformPoint(Vector3.up * mouthHeight);
            float tilt = Vector3.Angle(transform.up, Vector3.up);
            bool pouring = tilt > pourAngle && Sand > 0f;

            var em = _fx.emission;
            em.rateOverTime = pouring ? 260f : 0f;
            _fx.transform.position = mouth;
            _fx.transform.rotation = Quaternion.LookRotation(Vector3.down);
            _pour.volume = Mathf.MoveTowards(_pour.volume, pouring ? 0.6f : 0f, Time.deltaTime * 4f);

            if (pouring)
            {
                float amount = Mathf.Min(Sand, pourRate * Time.deltaTime);
                Sand -= amount;
                SandHole hole = HoleBelow(mouth);
                if (hole != null) hole.AddSand(amount);
                UpdateVisual();
            }
            else if (Sand < 1f && tilt < 70f && IsDippedInSand(mouth))
            {
                Sand = 1f;
                AudioOneShot.PlayAt(SameShoreAssets.I.pickup, mouth, 0.7f, 0.4f, 6f, 0.7f);
                XRInput.HapticBoth(0.35f, 0.12f, 0.3f);
                UpdateVisual();
            }
        }

        bool IsDippedInSand(Vector3 mouth)
        {
            if (_grab != null && !_grab.IsHeld) return false;
            if (!FootprintSystem.GroundHit(mouth, out RaycastHit hit)) return false;
            return mouth.y < hit.point.y + scoopDepth + mouthHeight * 0.6f && transform.position.y < hit.point.y + scoopDepth;
        }

        static SandHole HoleBelow(Vector3 mouth)
        {
            foreach (SandHole h in SandHole.All)
            {
                Vector3 d = mouth - h.transform.position;
                if (new Vector2(d.x, d.z).magnitude < h.Radius + 0.3f && d.y < 1.6f) return h;
            }
            return null;
        }

        void UpdateVisual()
        {
            if (sandFill == null) return;
            sandFill.gameObject.SetActive(Sand > 0.02f);
            Vector3 p = sandFill.localPosition;
            p.y = Mathf.Lerp(fillLowY, fillHighY, Sand);
            sandFill.localPosition = p;
        }
    }
}
