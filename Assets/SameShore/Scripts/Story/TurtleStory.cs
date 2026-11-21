using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// ACT II: the same night, a few minutes earlier, through the hatchling's body. Reads the beach the human
    /// left (cans where they fell, the hole, the bag, the light string, the music) and stages the encounters
    /// along a semi-linear route from the nest to the sea: objectives, the two gull encounters (never lethal),
    /// the "jellyfish", the giant and the photo, "Almost there...". Death is reserved for the ending.
    /// </summary>
    public class TurtleStory : MonoBehaviour
    {
        [SerializeField] private TurtleController turtle;
        [SerializeField] private Transform nest;
        [SerializeField] private Transform sea;
        [SerializeField] private Transform[] route;
        [SerializeField] private CanTossGame cans;
        [SerializeField] private SandHole hole;
        [SerializeField] private WindBag bag;
        [SerializeField] private LightGarland garland;
        [SerializeField] private GiantHuman giant;
        [SerializeField] private GullEncounters gulls;
        [SerializeField] private TurtleHearing hearing;
        [SerializeField] private TurtleVitality vitality;
        [Header("Narrative triggers (boxes across the corridor; distances below are only the fallback)")]
        [SerializeField] private StoryTrigger gullZone1;
        [SerializeField] private StoryTrigger gullZone2;
        [SerializeField] private StoryTrigger jellyZone;
        [SerializeField] private float firstGullAt = 13.2f;      // metres from the shore
        [SerializeField] private float secondGullAt = 5.7f;
        [SerializeField] private float almostThereAt = 1.4f;

        public bool Running { get; private set; }
        public bool LeftNest { get; private set; }
        public bool FellInHole { get; private set; }
        public bool PassedHole { get; private set; }
        public bool PassedCans { get; private set; }
        public bool BitBag { get; private set; }
        public bool PassedBag { get; private set; }
        public bool SawGiant { get; private set; }
        public bool PassedGiant { get; private set; }
        public bool PulledByLights { get; private set; }
        public float SecondsOffCourse { get; private set; }
        public bool AlmostThere { get; private set; }
        public int CansOnRoute { get; private set; }
        /// <summary>How many times the giant's flash hit the hatchling, and the strongest screen veil it caused (comfort check).</summary>
        public int Glares { get; private set; }
        public float GlarePeak { get; private set; }

        public event Action<string, float> Message;

        float _holeD = 7.2f, _bagD = 3.2f, _cansD = 10.5f, _giantD = 2f;
        bool _jellyPrompt, _gull1, _gull2, _photoStarted, _lightMsg;
        float _slowUntil;
        AudioSource _breath, _hum;

        public float DistanceToSea => sea == null ? 99f : Mathf.Max(0f, sea.position.z - turtle.transform.position.z);
        float D(Vector3 p) => sea.position.z - p.z;

        void Start()
        {
            turtle.ZoneEntered += z =>
            {
                if (!Running) return;
                if (z.Type == TurtleZoneType.Trap && !FellInHole)
                {
                    FellInHole = true;
                    Say("The sand gives way - you slide into the hole the human dug.\nKeep crawling: dig your flippers in and climb out.", 6f);
                }
            };
            if (vitality != null) vitality.Message += Say;
            if (hearing != null) hearing.Message += Say;
            _breath = Loop(SameShoreAssets.I.turtleBreath);
            _hum = Loop(SameShoreAssets.I.lightHum);
            if (giant != null) giant.Flashed += OnFlash;
        }

        AudioSource Loop(AudioClip c)
        {
            var a = gameObject.AddComponent<AudioSource>();
            a.clip = c;
            a.loop = true;
            a.spatialBlend = 0;
            a.volume = 0;
            if (c != null) a.Play();
            return a;
        }

        void Say(string s, float seconds = 5f) => Message?.Invoke(s, seconds);

        /// <summary>Starts the act: reads where the human left things, starts the replay of the human.</summary>
        public void Begin()
        {
            Running = true;
            LeftNest = FellInHole = PassedHole = PassedCans = BitBag = PassedBag = SawGiant = PassedGiant = false;
            PulledByLights = AlmostThere = false;
            Glares = 0;
            GlarePeak = 0f;
            SecondsOffCourse = 0;
            _jellyPrompt = _gull1 = _gull2 = _photoStarted = _lightMsg = false;
            turtle.ExternalSpeedMultiplier = 1f;
            turtle.BurstEnabled = true;

            if (hole != null) _holeD = D(hole.transform.position);
            if (bag != null) { _bagD = D(bag.transform.position); bag.Breath = 2.2f; bag.Bitten = false; }
            CansOnRoute = 0;
            float maxCan = -1;
            if (cans != null)
                foreach (var c in cans.Cans)
                {
                    if (c.Result != BeachLog.CanResult.Missed) continue;
                    if (DistanceToRoute(c.transform.position) < 1.6f) { CansOnRoute++; maxCan = Mathf.Max(maxCan, D(c.transform.position)); }
                }
            _cansD = maxCan > 0 ? maxCan - 0.6f : 10.5f;

            var log = BeachLog.I;
            if (giant != null && log != null)
            {
                var path = log.NightPath.Count >= 2 ? log.NightPath : DefaultNightPath(log);
                Vector3 feet = log.PhotoTaken ? log.PhotoFeet : DefaultPhotoFeet();
                float yaw = log.PhotoTaken ? log.PhotoYaw : -70f;
                giant.TimeScale = 1f;
                giant.Play(path, feet, yaw);
                _giantD = D(feet);
            }
            if (hearing != null) hearing.Enable(true, turtle.transform);
            if (gulls != null) gulls.ResetEncounters();
            if (vitality != null) { vitality.ResetState(); vitality.Active = true; }
            foreach (var z in new[] { gullZone1, gullZone2, jellyZone }) if (z != null) z.Arm(turtle.transform);
        }

        public void Stop()
        {
            Running = false;
            if (hearing != null) hearing.Enable(false);
            if (vitality != null) vitality.Active = false;
            foreach (var z in new[] { gullZone1, gullZone2, jellyZone }) if (z != null) z.Disarm();
            if (gulls != null) gulls.Stop();
        }

        Vector3 DefaultPhotoFeet() => route.Length > 2 ? route[route.Length - 2].position + new Vector3(0.8f, 0, 0.5f) : sea.position;

        /// <summary>Turtle-only start: the human walks back from the nest along the lights to the towel, then to the photo spot.</summary>
        List<BeachLog.PathSample> DefaultNightPath(BeachLog log)
        {
            var pts = new List<Vector3> { nest.position + new Vector3(0.9f, 0, -0.4f) };
            if (garland != null && garland.Hooks != null)
                for (int i = garland.Hooks.Length - 1; i >= 0; i--)
                    pts.Add(MeshKit.OnGround(garland.Hooks[i].position + new Vector3(0.6f, 0, 0.3f)));
            pts.Add(DefaultPhotoFeet());
            var list = new List<BeachLog.PathSample>();
            float t = 3f;
            list.Add(new BeachLog.PathSample { t = 0, feet = pts[0], yaw = 180, flashlightOn = true });
            for (int i = 1; i < pts.Count; i++)
            {
                Vector3 d = pts[i] - pts[i - 1];
                t += d.magnitude / 1.0f;
                list.Add(new BeachLog.PathSample { t = t, feet = pts[i], yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, flashlightOn = true });
            }
            return list;
        }

        float DistanceToRoute(Vector3 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < route.Length - 1; i++)
            {
                Vector3 a = route[i].position, b = route[i + 1].position;
                Vector3 ab = b - a; ab.y = 0;
                Vector3 ap = p - a; ap.y = 0;
                float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                best = Mathf.Min(best, (ap - ab * t).magnitude);
            }
            return best;
        }

        /// <summary>Closest point on the route ahead of the hatchling (used for the checkpoints).</summary>
        public Vector3 CheckpointPosition()
        {
            float d = DistanceToSea;
            float[] marks = { 16.5f, 12.5f, 8.8f, 5.5f, 3.6f };
            float cp = 18f;
            foreach (float m in marks) if (d < m + 0.3f) cp = m;
            if (cp >= 18f) return nest.position;
            // point on the route at that distance from the sea, nudged clear of obstacles
            for (int i = 0; i < route.Length - 1; i++)
            {
                float da = D(route[i].position), db = D(route[i + 1].position);
                if ((da - cp) * (db - cp) <= 0)
                {
                    float k = Mathf.InverseLerp(da, db, cp);
                    Vector3 p = Vector3.Lerp(route[i].position, route[i + 1].position, k);
                    for (int n = 0; n < 8; n++)
                    {
                        Vector3 probe = p + Vector3.right * ((n % 2 == 0 ? 1 : -1) * 0.3f * ((n + 1) / 2));
                        if (!Physics.CheckSphere(MeshKit.OnGround(probe, 0.06f), 0.05f, ~0, QueryTriggerInteraction.Ignore)) return MeshKit.OnGround(probe, 0.02f);
                    }
                    return MeshKit.OnGround(p, 0.02f);
                }
            }
            return nest.position;
        }

        public List<Objective> BuildObjectives()
        {
            var list = new List<Objective>
            {
                new Objective { Id = "nest", Title = "Crawl out of the nest", Sticky = true, HideMarker = true, Check = () => LeftNest },
                new Objective { Id = "glow", Title = "Head for the glow of the sea", Sticky = true, HideMarker = true, Check = () => DistanceToSea < 12.8f },
            };
            if (CansOnRoute > 0)
                list.Add(new Objective { Id = "cans", Title = "Find a way past the giant cans", Sticky = true, HideMarker = true, Check = () => PassedCans });
            list.Add(new Objective { Id = "hole", Title = "Get past the hole", Sticky = true, HideMarker = true, Check = () => PassedHole,
                                     Progress = () => FellInHole && !PassedHole ? "climb out!" : "" });
            list.Add(new Objective { Id = "gull", Title = "Watch the sky", Sticky = true, HideMarker = true, Check = () => gulls == null || gulls.SecondDone });
            list.Add(new Objective { Id = "food", Title = "Something to eat?", Sticky = true, HideMarker = true, Check = () => PassedBag || BitBag });
            list.Add(new Objective { Id = "giant", Title = "Get past the giant", Sticky = true, HideMarker = true, Check = () => PassedGiant });
            list.Add(new Objective { Id = "ocean", Title = "Reach the ocean", Sticky = true, HideMarker = true,
                                     Check = () => GameManager.Instance != null && GameManager.Instance.Current == GameState.End,
                                     Progress = () => vitality != null ? vitality.StatusText() : "" });
            return list;
        }

        void Update()
        {
            bool running = Running && GameManager.Instance != null && GameManager.Instance.Current == GameState.Hatchling && turtle.enabled;
            _breath.volume = Mathf.MoveTowards(_breath.volume, running ? 0.3f + Mathf.Clamp01(turtle.CurrentSpeed * 3f) * 0.3f : 0f, Time.deltaTime);
            float artificial = running ? turtle.ArtificialShare * Mathf.Clamp01(turtle.LightPull.magnitude) : 0f;
            _hum.volume = Mathf.MoveTowards(_hum.volume, artificial > 0.25f ? artificial * 0.4f : 0f, Time.deltaTime * 0.6f);
            if (!running) return;

            Vector3 p = turtle.transform.position;
            float d = DistanceToSea;
            if (!LeftNest && Flat(p - nest.position) > 1.0f) LeftNest = true;

            // lights: notice when they pull, say it once
            if (artificial > 0.45f && turtle.CurrentSpeed > 0.01f)
            {
                SecondsOffCourse += Time.deltaTime;
                if (SecondsOffCourse > 1.5f) PulledByLights = true;
                if (!_lightMsg && SecondsOffCourse > 2f)
                {
                    _lightMsg = true;
                    Say("Bright lights everywhere... which one is the sea?\nThe sea is the low, wide glow on the horizon. Turn your body towards it.", 7f);
                }
            }

            if (!PassedCans && (d < _cansD || CansOnRoute == 0 && d < 9.5f)) PassedCans = true;
            if (!PassedHole && d < _holeD - 0.9f) PassedHole = true;
            if (!_gull1 && gulls != null && ((gullZone1 != null && gullZone1.Fired) || d < firstGullAt)) { _gull1 = true; gulls.StartSwoop(turtle); }
            // the second attack waits for the first to finish (never two birds at once)
            if (!_gull2 && gulls != null && gulls.FirstDone && ((gullZone2 != null && gullZone2.Fired) || d < secondGullAt)) { _gull2 = true; gulls.StartAttack(turtle); }

            // the bag on the wet sand
            if (bag != null && bag.State == WindBag.Phase.Landed && !BitBag)
            {
                float dist = Flat(bag.transform.position - p);
                if (!_jellyPrompt && (dist < 1.1f || (jellyZone != null && jellyZone.Fired)))
                {
                    _jellyPrompt = true;
                    Say("You're hungry...\nIs that a jellyfish?", 5f);
                }
                if (_jellyPrompt && (dist < 0.24f || (dist < 0.45f && (XRInput.AnyTriggerDown || XRInput.KeyDown(XRInput.KeyId.E)))))
                    StartCoroutine(BiteBag());
            }
            if (!PassedBag && d < _bagD - 0.6f) PassedBag = true;

            // the giant and the photo
            if (giant != null && giant.Playing)
            {
                float toGiant = Flat(giant.Feet - p);
                if (d < _giantD + 3.5f) giant.HurryToPhotoSpot();
                if (!SawGiant && toGiant < 2.4f && giant.AtPhotoSpot)
                {
                    SawGiant = true;
                    Say("Huge legs block the way. A shadow towers over you.\nWhat is it?", 5f);
                }
                // the photo comes after the "jellyfish" (unless the human stood further up the beach than the bag)
                bool bagDone = BitBag || PassedBag || bag == null || _giantD > _bagD + 0.5f;
                if (!_photoStarted && giant.AtPhotoSpot && ((toGiant < 1.15f && bagDone) || d < _giantD - 0.3f))
                {
                    _photoStarted = true;
                    StartCoroutine(giant.TakePhoto(p));
                }
            }
            if (!PassedGiant && _photoStarted && giant != null && giant.PhotoDone && d < _giantD - 0.5f) PassedGiant = true;
            if (!PassedGiant && giant == null && d < _giantD - 0.5f) PassedGiant = true;

            if (!AlmostThere && d < almostThereAt)
            {
                AlmostThere = true;
                Say("Almost there...", 4f);
            }

            if (Time.time > _slowUntil && turtle.ExternalSpeedMultiplier < 1f)
                turtle.ExternalSpeedMultiplier = Mathf.MoveTowards(turtle.ExternalSpeedMultiplier, 1f, Time.deltaTime * 0.3f);
        }

        IEnumerator BiteBag()
        {
            BitBag = true;
            bag.Bitten = true;
            turtle.Stun(0.6f);
            AudioOneShot.PlayAt(SameShoreAssets.I.Clip("plastic_crinkle"), bag.transform.position, 0.9f, 0.05f, 2f);
            XRInput.HapticBoth(0.5f, 0.2f);
            BeachLog.I?.Record("the hatchling bit the plastic bag", bag.transform.position);
            yield return new WaitForSeconds(0.8f);
            Say("It's not a jellyfish.\nIt's a plastic bag.", 5f);
            AudioOneShot.Play2D(SameShoreAssets.I.Clip("turtle_gag"), 0.7f);
            turtle.ExternalSpeedMultiplier = 0.45f;
            _slowUntil = Time.time + 5f;
            if (ScreenFader.Instance != null) ScreenFader.Instance.Flash(new Color(0.05f, 0.04f, 0.03f), 0.35f, 1.6f);
            yield return new WaitForSeconds(5.5f);
            Say("You spit it out. Your throat still burns.\nPlastic can stay in a turtle's gut for weeks. Keep going.", 6f);
        }

        void OnFlash()
        {
            StartCoroutine(Glare());
        }

        /// <summary>Moderate glare: a warm, soft bloom that fades over two seconds (no strobe, no white-out).</summary>
        IEnumerator Glare()
        {
            if (ScreenFader.Instance != null) ScreenFader.Instance.Flash(new Color(1f, 0.97f, 0.9f), 0.55f, 2.4f);
            turtle.Stun(1.2f);
            turtle.ExternalSpeedMultiplier = 0.6f;
            _slowUntil = Time.time + 2.5f;
            Glares++;
            BeachLog.I?.Record("the giant's phone flash dazzled the hatchling", turtle.transform.position);
            for (float t = 0; t < 2.6f; t += Time.deltaTime)
            {
                if (ScreenFader.Instance != null) GlarePeak = Mathf.Max(GlarePeak, ScreenFader.Instance.Alpha);
                yield return null;
            }
            Say("It wasn't a monster.\nIt was the human... it was you, taking the perfect photo.", 7f);
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
