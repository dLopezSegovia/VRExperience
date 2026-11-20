using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SameShore
{
    public enum ExperienceMode { Full, HumanOnly, TurtleOnly }

    /// <summary>
    /// Narrative director.
    ///   ACT I  - THE HUMAN: WHAT YOU LEAVE BEHIND. Black screen, sea and wind, subtitles, the beach fades in.
    ///            Objectives (flashlight, plastic, hole, chair, nest) - the player may leave any of them undone.
    ///            Back at the nest a hatchling breaks through the sand; the player chooses to go on.
    ///   ACT II - THE TURTLE: THE SAME WORLD, A DIFFERENT LIFE. Breath, sand, darkness, the eyes open 8 cm
    ///            above the sand. Journey objectives, the misleading light, the seagull, the sea.
    ///   ENDING - a wave washes over her; a summary of how the human's choices shaped her run.
    /// Both bodies share one scene, so every change persists. Relocations happen only under a full fade.
    /// Uses the team's GameManager states (Intro / Human / Transition / Hatchling / End).
    /// </summary>
    public class ExperienceDirector : MonoBehaviour
    {
        /// <summary>Set before reloading the scene to skip the menu.</summary>
        public static ExperienceMode? PendingMode;

        [Header("Systems")]
        [SerializeField] private PlayerPhaseSwitcher phaseSwitcher;
        [SerializeField] private TurtleController turtle;
        [SerializeField] private HumanController human;
        [SerializeField] private ScreenFader fader;
        [SerializeField] private PerceptionController perception;
        [SerializeField] private AmbientAudio ambient;
        [SerializeField] private BeachStateTracker beach;
        [SerializeField] private GullHunt gull;
        [SerializeField] private TurtleJourney journey;
        [SerializeField] private ObjectiveTracker objectives;
        [SerializeField] private FinalWave finalWave;
        [SerializeField] private NestController nest;
        [SerializeField] private TurtleFX turtleFX;

        [Header("Places")]
        [SerializeField] private Transform humanStart;
        [SerializeField] private Transform menuSpot;
        [SerializeField] private Transform sea;
        [SerializeField] private Transform chairSafeZone;
        [SerializeField] private Transform litterBin;

        [Header("Timing")]
        [SerializeField] private float fadeSeconds = 1.2f;
        [SerializeField] private float nestRadius = 1.9f;

        public ExperienceMode Mode { get; private set; }
        public bool Paused { get; private set; }
        public bool NestMomentActive => _nestActive;

        UIPanel _menu, _subtitle, _choice, _end, _hint, _pause;
        TextMeshPro _subText, _choiceTitle, _choiceBody, _endTitle, _endBody, _hintText;
        bool _busy, _nestActive, _nestCooldown;
        float _hintUntil;
        Flashlight _flashlight;
        SandHole _hole;

        // ================================================================== startup
        void Start()
        {
            _flashlight = FindFirstObjectByType<Flashlight>();
            _hole = FindFirstObjectByType<SandHole>();
            BuildUI();
            turtle.ReachedGoal += () => StartCoroutine(Ending());
            turtle.Message += ShowHint;
            gull.Caught += () => StartCoroutine(RespawnAtCheckpoint("The seagull caught you.\nBack to the last safe spot."));
            gull.Message += ShowHint;
            objectives.TipRequested += (o, tip) => ShowHint(tip, 9f);

            fader.SetImmediate(1f);
            if (PendingMode.HasValue)
            {
                var m = PendingMode.Value;
                PendingMode = null;
                StartCoroutine(Begin(m));
            }
            else StartCoroutine(ShowMenu());
        }

        void Update()
        {
            if (_hint != null && _hint.gameObject.activeSelf && Time.unscaledTime > _hintUntil) _hint.Hide();
            PauseInput();
            if (!_busy && !_nestActive && !_nestCooldown && GameManager.Instance.Current == GameState.Human && NearNest())
                StartCoroutine(NestMoment());
            if (_nestCooldown && !NearNest(nestRadius + 0.8f)) _nestCooldown = false;
        }

        bool NearNest(float r = -1f)
        {
            if (nest == null || PlayerRig.Head == null) return false;
            Vector3 d = PlayerRig.Head.position - nest.transform.position;
            d.y = 0;
            return d.magnitude < (r > 0 ? r : nestRadius);
        }

        // ================================================================== menu
        IEnumerator ShowMenu()
        {
            GameManager.Instance.SetState(GameState.Intro);
            human.SetActive(false);
            yield return null;
            human.PlaceAt(menuSpot != null ? menuSpot : humanStart);
            ambient.Apply(Perception.Human, 0.1f);
            _menu.Show();
            _menu.Follow.Snap();
            yield return fader.FadeTo(0f, fadeSeconds * 1.5f);
        }

        void Choose(ExperienceMode mode)
        {
            if (_busy) return;
            StartCoroutine(Begin(mode));
        }

        IEnumerator Begin(ExperienceMode mode)
        {
            _busy = true;
            Mode = mode;
            _menu.Hide();
            if (fader.Alpha < 0.99f) yield return fader.FadeTo(1f, fadeSeconds);
            if (mode == ExperienceMode.TurtleOnly)
            {
                yield return Subtitle("Every night, this beach becomes a path to survival.", 4f, true);
                yield return Subtitle("Tonight, nobody has cleared the way.", 3.2f, true);
                yield return BecomeHatchling(false);
            }
            else yield return HumanIntro();
            _busy = false;
        }

        // ================================================================== ACT I - the human
        IEnumerator HumanIntro()
        {
            human.PlaceAt(humanStart);
            GameManager.Instance.SetState(GameState.Human);
            perception.Apply(Perception.Human, true);
            ambient.Apply(Perception.Human, 0.5f);
            yield return new WaitForSeconds(1.2f);
            AudioOneShot.Play2D(SameShoreAssets.Pick(SameShoreAssets.I.gullCalls), 0.25f);
            yield return Subtitle("Every night, this beach becomes a path to survival.", 4.2f, true);
            StartCoroutine(FadeRoutine(0f, 4f));
            yield return Subtitle("Somewhere beneath the sand, a nest of sea turtles is about to hatch.", 4.4f, false);
            yield return Subtitle("But tonight, something stands in the way.", 3.6f, false);
            human.SetActive(true);
            objectives.Begin("YOUR OBJECTIVES", HumanObjectives());
            ShowHint(DesktopTestingMode.Active
                ? "<b>Walk</b> WASD  ·  <b>Look</b> mouse  ·  <b>Grab / drop</b> left click or E\n<b>Use</b> (flashlight, tip the bucket) right click or F  ·  <b>Pause</b> Esc"
                : "<b>Walk</b> left stick  ·  <b>Turn</b> right stick  ·  <b>Grab</b> grip\n<b>Flashlight</b> trigger while holding it  ·  <b>Pause</b> menu button", 12f);
        }

        IEnumerator FadeRoutine(float to, float seconds)
        {
            yield return fader.FadeTo(to, seconds);
        }

        List<Objective> HumanObjectives()
        {
            return new List<Objective>
            {
                new Objective
                {
                    Id = "flashlight", Title = "Turn off the flashlight",
                    Check = () => _flashlight == null || !_flashlight.IsOn,
                    Target = () => _flashlight != null ? _flashlight.transform.position : (Vector3?)null,
                    Tip = "Artificial light pulls hatchlings away from the sea.\nPick up the flashlight and switch it off."
                },
                new Objective
                {
                    Id = "plastic", Title = "Remove the plastic waste",
                    Check = () => beach.TrashTotal > 0 && beach.TrashCleared >= beach.TrashTotal,
                    Progress = () => $"{beach.TrashCleared}/{beach.TrashTotal}",
                    Target = TrashTarget,
                    Tip = "To a hatchling, a plastic bag can look like a jellyfish - and trap it.\nCarry the litter to the bin."
                },
                new Objective
                {
                    Id = "hole", Title = "Fill the hole in the sand",
                    Check = () => _hole == null || _hole.IsFilled,
                    Progress = () => _hole != null && _hole.Fill01 > 0.01f ? $"{Mathf.RoundToInt(_hole.Fill01 * 100)}%" : "",
                    Target = HoleTarget,
                    Tip = "A hatchling that falls in here may not climb out.\nTip a bucket of sand into the hole."
                },
                new Objective
                {
                    Id = "chair", Title = "Move the beach chair",
                    Check = () => beach.ChairClear,
                    Target = () => HeldWith<BeachChairMarker>() != null && chairSafeZone != null ? chairSafeZone.position : beach.ChairPosition,
                    Tip = "The chair stands right on the way to the sea.\nCarry it to the side of the beach."
                },
                new Objective
                {
                    Id = "nest", Title = "Return to the turtle nest",
                    Target = () => nest != null ? nest.transform.position : (Vector3?)null,
                    Tip = "Listen... something is moving under the sand.", TipRadius = 4f
                },
            };
        }

        Vector3? TrashTarget()
        {
            if (HeldWith<TrashItem>() != null && litterBin != null) return litterBin.position;
            TrashItem best = null;
            float bestD = float.MaxValue;
            Vector3 h = PlayerRig.Head != null ? PlayerRig.Head.position : Vector3.zero;
            foreach (var t in TrashItem.All)
            {
                if (t.Cleared) continue;
                float d = (t.transform.position - h).sqrMagnitude;
                if (d < bestD) { bestD = d; best = t; }
            }
            return best != null ? best.transform.position : (Vector3?)null;
        }

        Vector3? HoleTarget()
        {
            if (_hole == null) return null;
            if (HeldWith<SandBucket>() != null) return _hole.transform.position;
            SandBucket b = null;
            float bd = float.MaxValue;
            foreach (var x in FindObjectsByType<SandBucket>(FindObjectsSortMode.None))
            {
                float d = (x.transform.position - _hole.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; b = x; }
            }
            // point at the bucket until the player has one, then at the hole
            return b != null && _hole.Fill01 < 0.01f ? b.transform.position : _hole.transform.position;
        }

        static T HeldWith<T>() where T : Component
        {
            foreach (var g in GrabFeedback.All)
                if (g != null && g.IsHeld && g.GetComponent<T>() != null) return g.GetComponent<T>();
            return null;
        }

        // ---------------------------------------------------------------- the nest
        IEnumerator NestMoment()
        {
            _nestActive = true;
            ShowHint("Shh... something is moving under the sand.", 5f);
            yield return nest.HatchSequence();
            var pending = PendingHumanActions();
            _choiceTitle.text = "A hatchling breaks through the sand";
            var body = new StringBuilder();
            if (pending.Count == 0) body.Append("You cleared her way to the sea.\nNow see this beach through her eyes.");
            else
            {
                body.Append("Some things are still on her way:\n");
                foreach (var p in pending) body.Append("<color=#e2b07a>·  ").Append(p).Append("</color>\n");
                body.Append("\n<size=85%>You can go back and finish, or let the night go on.</size>");
            }
            _choiceBody.text = body.ToString();
            _choice.ClearButtons();
            string go = Mode == ExperienceMode.HumanOnly ? "Let the night go on" : "Become the hatchling";
            if (pending.Count > 0)
            {
                _choice.AddButton("Go back", "Finish clearing the beach", new Vector2(-0.3f, -0.3f), new Vector2(0.56f, 0.16f), CloseNestChoice, XRInput.KeyId.Digit1);
                _choice.AddButton(go, "Leave things as they are", new Vector2(0.3f, -0.3f), new Vector2(0.56f, 0.16f), () => StartCoroutine(LeaveHumanPhase()), XRInput.KeyId.Digit2);
            }
            else _choice.AddButton(go, null, new Vector2(0f, -0.3f), new Vector2(0.8f, 0.15f), () => StartCoroutine(LeaveHumanPhase()), XRInput.KeyId.Digit1);
            _choice.Show();
            _choice.Follow.Snap();
        }

        List<string> PendingHumanActions()
        {
            var list = new List<string>();
            if (!objectives.IsDone("flashlight")) list.Add("The flashlight is still on");
            if (!objectives.IsDone("plastic")) list.Add($"{beach.TrashTotal - beach.TrashCleared} pieces of plastic on the sand");
            if (!objectives.IsDone("hole")) list.Add("The hole is still open");
            if (!objectives.IsDone("chair")) list.Add("The chair blocks the way");
            return list;
        }

        void CloseNestChoice()
        {
            _choice.Hide();
            _nestActive = false;
            _nestCooldown = true;
            ShowHint("The hatchlings will wait a little longer.\nCome back to the nest when you are ready.", 6f);
        }

        IEnumerator LeaveHumanPhase()
        {
            if (_busy) yield break;
            _busy = true;
            _choice.Hide();
            human.SetActive(false);
            objectives.ForceComplete("nest");
            objectives.MarkPendingAsLeft();
            yield return new WaitForSeconds(1.6f);           // let the player read the final state of the list
            objectives.Stop();
            if (Mode == ExperienceMode.HumanOnly)
            {
                yield return fader.FadeTo(1f, fadeSeconds);
                ShowEnd("The night goes on", beach.Summary(),
                        ("Continue as a hatchling", () => StartCoroutine(ContinueAsTurtle())),
                        ("Try other choices", () => Reload(ExperienceMode.HumanOnly)),
                        ("Main menu", () => Reload(null)));
                _busy = false;
                yield break;
            }
            yield return BecomeHatchling(true);
            _busy = false;
        }

        IEnumerator ContinueAsTurtle()
        {
            _end.Hide();
            Mode = ExperienceMode.Full;
            yield return BecomeHatchling(true);
        }

        // ================================================================== ACT II - the hatchling
        IEnumerator BecomeHatchling(bool fromHuman)
        {
            if (fromHuman)
            {
                // her breath and the sand, then darkness
                AudioOneShot.Play2D(SameShoreAssets.I.nestScratching, 0.6f);
                AudioOneShot.Play2D(SameShoreAssets.I.turtleBreath, 0.7f);
                yield return fader.FadeTo(1f, 2.8f);
            }
            GameManager.Instance.SetState(GameState.Transition);
            ambient.Apply(Perception.Hatchling, 2.5f);
            AudioOneShot.Play2D(SameShoreAssets.I.turtleBreath, 0.8f);
            yield return Subtitle("One beach.\nA completely different world.", 3.8f, true);
            yield return EnterTurtle(true);
        }

        IEnumerator EnterTurtle(bool emerge)
        {
            GameManager.Instance.SetState(GameState.Hatchling);   // PlayerPhaseSwitcher shrinks the rig into the nest
            turtle.enabled = false;
            journey.ResetRun();
            turtleFX.ClearTracks();
            gull.ResetTotals();
            gull.Arm();
            perception.Body = turtle.transform;
            perception.Apply(Perception.Hatchling, false);
            ambient.Apply(Perception.Hatchling, 2f);
            Vector3 spawn = phaseSwitcher.TurtleSpawn.position;
            float yaw = phaseSwitcher.TurtleSpawn.eulerAngles.y;
            const float buried = 0.03f;
            if (emerge)
            {
                perception.CloseEyes();
                turtle.TeleportTo(spawn - Vector3.up * buried, yaw);
            }
            yield return new WaitForSeconds(0.4f);
            StartCoroutine(FadeRoutine(0f, 3f));
            if (emerge)
            {
                perception.OpenEyes(3.5f);
                AudioOneShot.Play2D(SameShoreAssets.I.nestScratching, 0.9f);
                var cc = turtle.GetComponent<CharacterController>();
                cc.enabled = false;
                Vector3 from = turtle.transform.position, to = spawn;
                for (float t = 0; t < 3f; t += Time.deltaTime)
                {
                    turtle.transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, t / 3f));
                    yield return null;
                }
                turtle.transform.position = to;
                cc.enabled = true;
            }
            else yield return new WaitForSeconds(2.5f);
            turtle.Begin();
            objectives.Begin("YOUR JOURNEY", journey.BuildObjectives());
            ShowHint(DesktopTestingMode.Active
                ? "<b>W</b> crawl  ·  <b>A / D</b> turn your body slowly  ·  <b>S</b> back\n<b>Space</b> sudden burst  ·  mouse: look around"
                : "<b>Left stick forward</b>: crawl  ·  <b>left / right</b>: turn your body slowly\n<b>A / X</b>: sudden burst  ·  look around freely", 11f);
        }

        IEnumerator RespawnAtCheckpoint(string message)
        {
            if (_busy) yield break;
            _busy = true;
            turtle.enabled = false;
            ShowHint(message, 4f);
            yield return new WaitForSeconds(1f);
            yield return fader.FadeTo(1f, fadeSeconds);
            float yaw = sea != null ? Quaternion.LookRotation(Vector3.ProjectOnPlane(sea.position - turtle.transform.position, Vector3.up)).eulerAngles.y : 0f;
            turtle.TeleportTo(journey.CheckpointPosition(), Mathf.Round(yaw / 180f) * 180f);
            turtleFX.ClearTracks();
            gull.Arm();
            yield return new WaitForSeconds(0.3f);
            yield return fader.FadeTo(0f, fadeSeconds);
            turtle.Resume();
            _busy = false;
        }

        IEnumerator RetryTurtle()
        {
            _end.Hide();
            finalWave.StopUnderwater();
            fader.SetColor(Color.black);
            if (fader.Alpha < 0.99f) yield return fader.FadeTo(1f, fadeSeconds);
            ambient.Duck(1f, 0.5f);
            phaseSwitcher.ResetTurtle();
            yield return EnterTurtle(true);
        }

        // ================================================================== ending
        IEnumerator Ending()
        {
            _busy = true;
            gull.Stop();
            objectives.HUD.Suppressed = true;
            yield return finalWave.Play(turtle.transform, sea, ambient);
            objectives.Stop();
            AudioOneShot.Play2D(SameShoreAssets.I.endingPad, 0.55f);
            ShowEnd("Great Job", ConsequenceSummary(),
                    ("Try other choices", () => Reload(ExperienceMode.Full)),
                    ("Crawl again", () => StartCoroutine(RetryTurtle())),
                    ("Main menu", () => Reload(null)));
            _busy = false;
        }

        /// <summary>How the human's choices shaped the hatchling's run. Factual, not preachy.</summary>
        public string ConsequenceSummary()
        {
            var sb = new StringBuilder();
            bool lightOff = _flashlight == null || !_flashlight.IsOn;
            float pulled = journey.PulledByArtificialLight;
            sb.AppendLine(lightOff
                ? "You turned off the flashlight. The ocean remained the brightest thing she could see."
                : pulled > 1f ? $"The flashlight stayed on. Its glow pulled her off course for {Mathf.RoundToInt(pulled)} seconds."
                              : "The flashlight stayed on. She was lucky its beam did not catch her.");
            int left = beach.TrashTotal - beach.TrashCleared;
            sb.AppendLine(left == 0
                ? "You removed the plastic. Her path was clearer."
                : journey.Tangled ? $"{left} pieces of plastic stayed on the sand. A bag wrapped around her flippers."
                : journey.HitLitter ? $"{left} pieces of plastic stayed on the sand. She had to push and squeeze past them."
                : $"{left} pieces of plastic stayed on the sand, waiting for the next hatchling.");
            bool filled = _hole == null || _hole.IsFilled;
            sb.AppendLine(filled
                ? "You filled the hole. One less danger stood in her way."
                : journey.FellInHole ? "The hole was still open. She fell in and had to fight her way out."
                : "The hole was still open. She was lucky to crawl around it.");
            sb.AppendLine(beach.ChairClear
                ? "You moved the chair. Nothing blocked her way to the sea."
                : gull.EverSheltered ? "The chair was still in her way - but it also hid her from the seagull."
                : journey.HitChair ? "The chair was still in her way. She had to find a way around it."
                : "The chair was still standing on the beach.");
            sb.AppendLine(gull.TimesCaught > 0 ? $"A seagull caught her {(gull.TimesCaught == 1 ? "once" : gull.TimesCaught + " times")}. On open sand, she had nowhere to hide."
                        : gull.TotalStrikes > 0 ? $"A seagull struck her {gull.TotalStrikes} time{(gull.TotalStrikes > 1 ? "s" : "")}, but she made it."
                        : "She escaped the seagull.");
            sb.AppendLine();
            sb.Append("<size=125%><b>Same shore. Different lives. Your actions matter.</b></size>");
            return sb.ToString();
        }

        void Reload(ExperienceMode? mode)
        {
            StartCoroutine(ReloadRoutine(mode));
        }

        IEnumerator ReloadRoutine(ExperienceMode? mode)
        {
            ClosePause();
            if (_end != null) _end.Hide();
            Time.timeScale = 1f;
            fader.SetColor(Color.black);
            yield return fader.FadeTo(1f, fadeSeconds);
            PendingMode = mode;
            AudioListener.volume = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex >= 0 ? SceneManager.GetActiveScene().buildIndex : 0);
        }

        // ================================================================== pause menu
        void PauseInput()
        {
            bool pressed = OVRInput.GetDown(OVRInput.Button.Start) || XRInput.KeyDown(XRInput.KeyId.P) ||
                           (DesktopTestingMode.Active && XRInput.KeyDown(XRInput.KeyId.Escape));
            if (!pressed) return;
            var s = GameManager.Instance.Current;
            if (Paused) ClosePause();
            else if (s == GameState.Human || s == GameState.Hatchling) OpenPause();
        }

        void OpenPause()
        {
            Paused = true;
            Time.timeScale = 0f;
            _pause.ClearButtons();
            bool turtlePhase = GameManager.Instance.Current == GameState.Hatchling;
            float y = 0.2f;
            void Btn(string label, string sub, System.Action a, XRInput.KeyId key)
            {
                _pause.AddButton(label, sub, new Vector2(0, y), new Vector2(0.7f, 0.12f), a, key);
                y -= 0.145f;
            }
            Btn("Resume", null, ClosePause, XRInput.KeyId.Digit1);
            Btn(objectives.HUD.Visible ? "Hide objectives" : "Show objectives", null, () => { objectives.HUD.Visible = !objectives.HUD.Visible; ClosePause(); }, XRInput.KeyId.Digit2);
            if (turtlePhase)
            {
                Btn("Restart from checkpoint", "Back to the last safe spot", () => { ClosePause(); StartCoroutine(RespawnAtCheckpoint("Back to the last safe spot.")); }, XRInput.KeyId.Digit3);
                Btn("Restart the hatchling's journey", "From the nest, same beach", () => { ClosePause(); StartCoroutine(RetryTurtle()); }, XRInput.KeyId.R);
            }
            else Btn("Restart the human phase", "The beach is reset", () => Reload(Mode == ExperienceMode.HumanOnly ? ExperienceMode.HumanOnly : ExperienceMode.Full), XRInput.KeyId.R);
            Btn("Main menu", null, () => Reload(null), XRInput.KeyId.M);
            _pause.Show();
            _pause.Follow.Snap();
        }

        void ClosePause()
        {
            if (!Paused) return;
            Paused = false;
            Time.timeScale = 1f;
            _pause.Hide();
        }

        // ================================================================== UI
        IEnumerator Subtitle(string text, float seconds, bool centered)
        {
            _subText.text = text;
            _subText.alpha = 0;
            _subtitle.Follow.heightOffset = centered ? 0f : -0.32f;
            _subtitle.Follow.distance = centered ? 1.6f : 1.5f;
            _subtitle.Show();
            _subtitle.Follow.Snap();
            for (float t = 0; t < 0.7f; t += Time.deltaTime) { _subText.alpha = t / 0.7f; yield return null; }
            _subText.alpha = 1;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                if (XRInput.AnyTriggerDown || XRInput.KeyDown(XRInput.KeyId.Space)) break;
                yield return null;
            }
            for (float t = 0; t < 0.6f; t += Time.deltaTime) { _subText.alpha = 1 - t / 0.6f; yield return null; }
            _subtitle.Hide();
        }

        void ShowEnd(string title, string body, params (string label, System.Action action)[] buttons)
        {
            _endTitle.text = title;
            _endBody.text = body;
            _end.ClearButtons();
            float w = 0.5f, gap = 0.05f;
            float x0 = -(buttons.Length - 1) * (w + gap) * 0.5f;
            XRInput.KeyId[] keys = { XRInput.KeyId.Digit1, XRInput.KeyId.Digit2, XRInput.KeyId.Digit3 };
            for (int i = 0; i < buttons.Length; i++)
            {
                var b = buttons[i];
                _end.AddButton(b.label, null, new Vector2(x0 + i * (w + gap), -0.43f), new Vector2(w, 0.13f), () => b.action(), keys[i]);
            }
            _end.Show();
            _end.Follow.Snap();
            if (_hint != null) _hint.Hide();
        }

        public void ShowHint(string text) => ShowHint(text, 6f);

        public void ShowHint(string text, float seconds)
        {
            _hintText.text = text;
            _hint.Show();
            _hint.Follow.Snap();
            _hintUntil = Time.unscaledTime + seconds;
        }

        void BuildUI()
        {
            Color title = new Color(0.88f, 0.94f, 1f);
            Color accent = new Color(0.45f, 0.78f, 0.82f);

            _menu = UIPanel.Create("StartMenu", new Vector2(1.5f, 1.02f), 1.7f);
            _menu.Follow.follow = false;
            _menu.AddText("O N E   B E A C H ,   T W O   B O D I E S", 0.32f, new Vector2(0, 0.42f), new Vector2(1.4f, 0.08f), accent);
            _menu.AddText("SAME SHORE", 1.45f, new Vector2(0, 0.3f), new Vector2(1.4f, 0.2f), title, FontStyles.Bold);
            _menu.AddText("Live one night on the same beach: first as a person, then as a newborn sea turtle.\nWhat you leave behind is what she will have to face.",
                          0.32f, new Vector2(0, 0.15f), new Vector2(1.38f, 0.13f), new Color(0.8f, 0.85f, 0.9f));
            _menu.AddButton("Full Experience", "Human, then hatchling  ·  8-12 minutes", new Vector2(0, -0.03f), new Vector2(0.9f, 0.15f),
                            () => Choose(ExperienceMode.Full), XRInput.KeyId.Digit1);
            _menu.AddButton("Human Experience", "The beach at night", new Vector2(-0.235f, -0.22f), new Vector2(0.44f, 0.15f),
                            () => Choose(ExperienceMode.HumanOnly), XRInput.KeyId.Digit2);
            _menu.AddButton("Turtle Experience", "Start in the nest", new Vector2(0.235f, -0.22f), new Vector2(0.44f, 0.15f),
                            () => Choose(ExperienceMode.TurtleOnly), XRInput.KeyId.Digit3);
            _menu.AddText(DesktopTestingMode.Active ? "Desktop testing: click a button with the mouse (or press 1 / 2 / 3)"
                                                    : "Point with a controller and pull the trigger", 0.26f,
                          new Vector2(0, -0.42f), new Vector2(1.3f, 0.06f), new Color(0.6f, 0.68f, 0.75f));

            _subtitle = UIPanel.Create("Subtitle", new Vector2(1.4f, 0.26f), 1.5f, new Color(0, 0, 0, 0.35f));
            _subtitle.Follow.follow = true;
            _subText = _subtitle.AddText("", 0.5f, Vector2.zero, new Vector2(1.34f, 0.24f), title);

            _choice = UIPanel.Create("NestChoice", new Vector2(1.25f, 0.82f), 1.35f);
            _choice.Follow.follow = false;
            _choiceTitle = _choice.AddText("", 0.62f, new Vector2(0, 0.3f), new Vector2(1.18f, 0.12f), title, FontStyles.Bold);
            _choiceBody = _choice.AddText("", 0.42f, new Vector2(0, 0.04f), new Vector2(1.15f, 0.38f), new Color(0.86f, 0.9f, 0.95f));

            _end = UIPanel.Create("EndScreen", new Vector2(1.75f, 1.2f), 1.55f);
            _end.Follow.follow = false;
            _endTitle = _end.AddText("", 1.1f, new Vector2(0, 0.47f), new Vector2(1.6f, 0.16f), title, FontStyles.Bold);
            _endBody = _end.AddText("", 0.42f, new Vector2(0, 0.05f), new Vector2(1.62f, 0.66f), new Color(0.86f, 0.9f, 0.95f));

            _pause = UIPanel.Create("PauseMenu", new Vector2(0.86f, 1.0f), 1.3f);
            _pause.Follow.follow = false;
            _pause.AddText("PAUSED", 0.7f, new Vector2(0, 0.4f), new Vector2(0.8f, 0.1f), title, FontStyles.Bold);

            _hint = UIPanel.Create("Hint", new Vector2(1.1f, 0.3f), 1.25f);
            _hint.Follow.heightOffset = -0.3f;
            _hintText = _hint.AddText("", 0.34f, Vector2.zero, new Vector2(1.04f, 0.28f), new Color(0.92f, 0.96f, 1f));
        }
    }
}
