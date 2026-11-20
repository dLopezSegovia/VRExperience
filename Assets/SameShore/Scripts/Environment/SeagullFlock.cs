using System;
using System.Collections.Generic;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// Seagulls as a threat for the hatchling (PDF: "Seagulls: chase and attack").
    /// Two gulls circle above the hatchling and periodically dive at it. A dive is announced by a
    /// screech; crawling away (a burst works best) or sheltering under an object avoids it.
    /// Each strike stuns the hatchling briefly; after maxStrikes it is caught.
    /// </summary>
    public class SeagullFlock : MonoBehaviour
    {
        [SerializeField] private Seagull[] gulls;
        [SerializeField] private TurtleController turtle;
        [SerializeField] private int hunters = 2;
        [SerializeField] private float orbitRadius = 3.5f;
        [SerializeField] private float orbitHeight = 4.5f;
        [SerializeField] private Vector2 diveInterval = new Vector2(14f, 24f);
        [SerializeField] private float firstDiveDelay = 18f;
        [SerializeField] private float hitRadius = 0.16f;
        [SerializeField] private float stunSeconds = 1.6f;
        [SerializeField] private int maxStrikes = 3;
        [Tooltip("No dives while the hatchling is this close to the nest spawn (gives the player time to learn).")]
        [SerializeField] private float safeRadiusAroundSpawn = 1.2f;

        public int Strikes { get; private set; }
        public event Action Caught;
        public event Action<string> Message;

        readonly List<Seagull> _hunting = new List<Seagull>();
        float _nextDive;
        Vector3 _spawn;
        bool _active, _warned;
        AudioSource _heart;

        void Start()
        {
            if (GameManager.Instance != null) GameManager.Instance.OnStateChanged += OnState;
            _heart = gameObject.AddComponent<AudioSource>();
            _heart.clip = SameShoreAssets.I.heartbeat;
            _heart.loop = true;
            _heart.spatialBlend = 0;
            _heart.volume = 0;
            _heart.Play();
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= OnState;
        }

        void OnState(GameState prev, GameState next)
        {
            if (next == GameState.Hatchling) Begin();
            else if (next == GameState.End) Stop();
        }

        public void Begin()
        {
            Strikes = 0;
            _hunting.Clear();
            _spawn = turtle.transform.position;
            for (int i = 0; i < gulls.Length; i++)
            {
                if (gulls[i] == null) continue;
                if (_hunting.Count < hunters)
                {
                    _hunting.Add(gulls[i]);
                    gulls[i].Hunt(turtle.transform, orbitRadius + i * 1.2f, orbitHeight + i * 0.8f);
                }
            }
            _nextDive = Time.time + firstDiveDelay;
            _active = true;
            _warned = false;
        }

        public void Stop()
        {
            _active = false;
            _heart.volume = 0;
        }

        void Update()
        {
            if (!_active || turtle == null || !turtle.enabled)
            {
                _heart.volume = Mathf.MoveTowards(_heart.volume, 0, Time.deltaTime);
                return;
            }
            bool diving = _hunting.Exists(g => g.State == Seagull.Mode.Diving);
            _heart.volume = Mathf.MoveTowards(_heart.volume, diving ? 0.9f : 0.15f, Time.deltaTime * 2f);

            if (Time.time < _nextDive || diving) return;
            Vector3 tp = turtle.transform.position;
            if ((tp - _spawn).magnitude < safeRadiusAroundSpawn) { _nextDive = Time.time + 3f; return; }

            Seagull g = _hunting[UnityEngine.Random.Range(0, _hunting.Count)];
            if (Sheltered(tp, g.transform.position)) { _nextDive = Time.time + 4f; return; }
            if (!_warned)
            {
                _warned = true;
                Message?.Invoke("A seagull is diving at you!\nCrawl away fast (A / X) or hide under something.");
            }
            Vector3 strike = tp + Vector3.up * 0.04f;
            g.Dive(strike, gull => Resolve(strike));
            _nextDive = Time.time + UnityEngine.Random.Range(diveInterval.x, diveInterval.y);
        }

        void Resolve(Vector3 strike)
        {
            if (turtle == null || !turtle.enabled) return;
            Vector3 d = turtle.transform.position - strike;
            d.y = 0;
            if (d.magnitude > hitRadius) return; // dodged
            Strikes++;
            turtle.Stun(stunSeconds);
            if (ScreenFader.Instance != null) ScreenFader.Instance.Flash(new Color(0.5f, 0.02f, 0.02f), 0.55f, 0.6f);
            AudioOneShot.Play2D(SameShoreAssets.I.gullFlap, 1f);
            if (Strikes >= maxStrikes)
            {
                Stop();
                Caught?.Invoke();
            }
            else Message?.Invoke(Strikes == 1 ? "Ouch! It caught you for a moment.\nKeep moving towards the sea." : "Again! One more and you won't escape.");
        }

        static bool Sheltered(Vector3 turtlePos, Vector3 gullPos)
        {
            Vector3 o = turtlePos + Vector3.up * 0.05f;
            Vector3 dir = gullPos - o;
            if (Physics.Raycast(o, dir.normalized, out RaycastHit hit, dir.magnitude, ~0, QueryTriggerInteraction.Ignore))
                return !(hit.collider is TerrainCollider) && hit.collider.attachedRigidbody == null || (hit.collider.attachedRigidbody != null && hit.collider.attachedRigidbody.mass >= 1f);
            return false;
        }
    }
}
