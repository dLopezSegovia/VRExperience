using System.Collections.Generic;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// The hatchling's run from the nest to the sea: objectives ("YOUR JOURNEY"), checkpoints for a
    /// quick respawn, and what actually happened on the way (used for the consequence summary).
    /// Also gives the artificial light an audible presence (an uneasy hum while it pulls).
    /// </summary>
    public class TurtleJourney : MonoBehaviour
    {
        [SerializeField] private TurtleController turtle;
        [SerializeField] private Transform nest;
        [SerializeField] private Transform sea;
        [SerializeField] private GullHunt gull;
        [Tooltip("Checkpoints as metres from the shoreline (the nest is the first one).")]
        [SerializeField] private float[] checkpointDistances = { 13f, 9.6f, 5f };
        [SerializeField] private float obstacleBandEnd = 4.5f;

        // ---- what happened (for the ending)
        public bool FellInHole { get; private set; }
        public bool Tangled { get; private set; }
        public bool HitLitter { get; private set; }
        public bool HitChair { get; private set; }
        public float PulledByArtificialLight => turtle != null ? turtle.SecondsPulledByArtificialLight : 0f;
        public int CheckpointIndex { get; private set; } = -1;

        float _facingSeaTime;
        bool _leftNest, _foundOcean;
        AudioSource _hum, _breath;
        float _spawnYaw;

        void Start()
        {
            turtle.ZoneEntered += z =>
            {
                if (z.Type == TurtleZoneType.Trap) FellInHole = true;
                if (z.Type == TurtleZoneType.Tangle) Tangled = true;
            };
            turtle.Bumped += c =>
            {
                if (c.GetComponentInParent<TrashItem>() != null) HitLitter = true;
                if (c.attachedRigidbody != null && c.attachedRigidbody.name.Contains("chair")) HitChair = true;
            };
            _hum = Loop(SameShoreAssets.I.lightHum);
            _breath = Loop(SameShoreAssets.I.turtleBreath);
        }

        AudioSource Loop(AudioClip c)
        {
            var a = gameObject.AddComponent<AudioSource>();
            a.clip = c;
            a.loop = true;
            a.spatialBlend = 0;
            a.volume = 0;
            a.Play();
            return a;
        }

        public void ResetRun()
        {
            FellInHole = Tangled = HitLitter = HitChair = false;
            CheckpointIndex = -1;
            _facingSeaTime = 0;
            _leftNest = _foundOcean = false;
        }

        public float DistanceToSea => sea == null ? 99f : Mathf.Max(0f, sea.position.z - turtle.transform.position.z);

        public List<Objective> BuildObjectives()
        {
            return new List<Objective>
            {
                new Objective { Id = "nest", Title = "Leave the nest", Sticky = true, HideMarker = true,
                                Check = () => _leftNest },
                new Objective { Id = "ocean", Title = "Find the ocean", Sticky = true, HideMarker = true,
                                Check = () => _foundOcean },
                new Objective { Id = "obstacles", Title = "Navigate the obstacles", Sticky = true, HideMarker = true,
                                Check = () => DistanceToSea < obstacleBandEnd },
                new Objective { Id = "danger", Title = "Escape the danger", Sticky = true, HideMarker = true,
                                Check = () => gull != null && gull.Escaped },
                new Objective { Id = "water", Title = "Reach the water", Sticky = true, HideMarker = true,
                                Check = () => GameManager.Instance != null && GameManager.Instance.Current == GameState.End },
            };
        }

        void Update()
        {
            bool running = GameManager.Instance != null && GameManager.Instance.Current == GameState.Hatchling && turtle.enabled;
            _breath.volume = Mathf.MoveTowards(_breath.volume, running ? 0.35f + Mathf.Clamp01(turtle.CurrentSpeed * 3f) * 0.3f : 0f, Time.deltaTime);
            float artificialPull = running ? turtle.ArtificialShare * Mathf.Clamp01(turtle.LightPull.magnitude) : 0f;
            _hum.volume = Mathf.MoveTowards(_hum.volume, artificialPull > 0.25f ? artificialPull * 0.45f : 0f, Time.deltaTime * 0.6f);
            if (!running) return;

            Vector3 p = turtle.transform.position;
            if (!_leftNest && nest != null && Flat(p - nest.position) > 1.15f) _leftNest = true;

            Vector3 toSea = sea != null ? Vector3.ProjectOnPlane(sea.position - p, Vector3.up) : Vector3.forward;
            toSea = new Vector3(0, 0, Mathf.Sign(toSea.z == 0 ? 1 : toSea.z));
            float heading = Vector3.Angle(Vector3.ProjectOnPlane(turtle.transform.forward, Vector3.up), toSea);
            if (_leftNest && heading < 35f && turtle.CurrentSpeed > 0.01f) _facingSeaTime += Time.deltaTime;
            if (!_foundOcean && (_facingSeaTime > 2.5f || DistanceToSea < 11f)) _foundOcean = true;

            for (int i = CheckpointIndex + 1; i < checkpointDistances.Length; i++)
                if (DistanceToSea < checkpointDistances[i]) CheckpointIndex = i;
        }

        /// <summary>Respawn point: on the nest->sea line at the last checkpoint reached, facing the sea.</summary>
        public Vector3 CheckpointPosition()
        {
            if (CheckpointIndex < 0 || nest == null || sea == null) return nest != null ? nest.position : turtle.transform.position;
            float d = checkpointDistances[CheckpointIndex] + 0.4f;
            Vector3 p = new Vector3(nest.position.x, Mathf.Max(nest.position.y, turtle.transform.position.y) + 3f, sea.position.z - d);
            p.x = Mathf.Lerp(nest.position.x, sea.position.x, Mathf.InverseLerp(sea.position.z - nest.position.z, 0, d));
            // avoid respawning inside an obstacle: nudge sideways until clear
            for (int k = 0; k < 8; k++)
            {
                Vector3 test = p + Vector3.right * (k % 2 == 0 ? 1 : -1) * 0.35f * ((k + 1) / 2);
                Vector3 probe = k == 0 ? p : test;
                if (!Physics.CheckSphere(Ground(probe) + Vector3.up * 0.05f, 0.06f, ~0, QueryTriggerInteraction.Ignore)) { p = probe; break; }
            }
            return Ground(p) + Vector3.up * 0.02f;
        }

        public static Vector3 Ground(Vector3 p)
        {
            foreach (Terrain t in Terrain.activeTerrains)
            {
                Vector3 o = t.transform.position, s = t.terrainData.size;
                if (p.x >= o.x && p.z >= o.z && p.x <= o.x + s.x && p.z <= o.z + s.z)
                    return new Vector3(p.x, t.SampleHeight(p) + o.y, p.z);
            }
            return p;
        }
        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
