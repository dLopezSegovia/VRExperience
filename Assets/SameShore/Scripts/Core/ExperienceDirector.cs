using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SameShore
{
    public enum ExperienceMode { Full, HumanOnly, TurtleOnly }

    /// <summary>
    /// Narrative director (storyboard v2).
    ///   ACT I  - THE HUMAN: A PERFECT DAY AT THE BEACH. Afternoon on the chair (drink, music, can toss),
    ///            sandcastle (the dug hole stays), snack (the wrapper blows away), sunset (music up, fireworks),
    ///            night (light string from the umbrella to the nest), flashlight and hatching nest, the photo.
    ///   ACT II - THE TURTLE: A JOURNEY TO SURVIVE. The same night, a few minutes earlier, 8 cm above the sand:
    ///            the cans, the hole, the lights, the gulls, the "jellyfish", the giant and the flash, the sea.
    ///   ENDING - the wave, darkness, a gull. SAME SHORE.
    /// Both bodies share one scene, so everything the human left behind is physically there for the hatchling;
    /// BeachLog keeps the timeline (music, fireworks, the human's night walk and the photo) for the replay.
    /// Every relocation of the player happens under a full fade.
    /// </summary>
    public class ExperienceDirector : MonoBehaviour
    {
        /// <summary>Set before reloading the scene to skip the menu.</summary>
        public static ExperienceMode? PendingMode;

        [Header("Systems")]
        [SerializeField] private PlayerPhaseSwitcher phaseSwitcher;
        [SerializeField] private TurtleController turtle;
        [SerializeField] private HumanController human;
        [SerializeField] private HumanSeat seat;
        [SerializeField] private ScreenFader fader;
        [SerializeField] private PerceptionController perception;
        [SerializeField] private AmbientAudio ambient;
        [SerializeField] private ObjectiveTracker objectives;
        [SerializeField] private FinalWave finalWave;
        [SerializeField] private NestController nest;
        [SerializeField] private TurtleFX turtleFX;
        [SerializeField] private DayCycle day;
        [SerializeField] private BeachLog log;
        [SerializeField] private TurtleStory turtleStory;
        [SerializeField] private GullEncounters gulls;

        [Header("Act I")]
        [SerializeField] private Transform chairSeat;
        [SerializeField] private Transform towel;
        [SerializeField] private BeachDrink drink;
        [SerializeField] private MusicPlayer music;
        [SerializeField] private CanTossGame canToss;
        [SerializeField] private SandcastleSite castle;
        [SerializeField] private VRGrabbable shovel;
        [SerializeField] private Sandwich sandwich;
        [SerializeField] private WindBag bag;
        [SerializeField] private FireworksShow fireworks;
        [SerializeField] private LightGarland garland;
        [SerializeField] private Flashlight flashlight;
        [SerializeField] private PhoneCamera phone;
        [SerializeField] private PhotoHatchling photoTurtle;
        [SerializeField] private Transform photoTurtleStart;

        [Header("Places")]
        [SerializeField] private Transform humanStart;
        [SerializeField] private Transform sea;

        [Header("Timing")]
        [SerializeField] private float fadeSeconds = 1.2f;
        [SerializeField] private float idleTipSeconds = 40f;

        public ExperienceMode Mode { get; private set; }
        public bool Paused { get; private set; }
        public string Scene { get; private set; } = "";

        UIPanel _menu, _subtitle, _end, _hint, _pause;
        TextMeshPro _subText, _endTitle, _endBody, _hintText;
        bool _busy, _skip, _ending;
        float _hintUntil;
        Action _skipAction;

        // ================================================================== startup
        void Start()
        {
            BuildUI();
            turtle.ReachedGoal += () => { if (!_ending) StartCoroutine(Ending()); };
            turtle.Message += m => ShowHint(m, 5f);
            if (turtleStory != null) turtleStory.Message += ShowHint;
            if (gulls != null) gulls.Message += ShowHint;
            objectives.TipRequested += (o, tip) => ShowHint(tip, 8f);
            if (canToss != null) canToss.CanResolved += (c, scored) =>
                ShowHint(scored ? "<b>Nice shot!</b>" : "<b>Missed!</b>  <size=80%>Never mind, it's just a can.</size>", 2.6f);
            if (phone != null) phone.gameObject.SetActive(false);
            if (photoTurtle != null) photoTurtle.gameObject.SetActive(false);

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
        }

        // ================================================================== menu
        IEnumerator ShowMenu()
        {
            GameManager.Instance.SetState(GameState.Intro);
            human.SetActive(false);
            day.Set(0.15f);
            yield return null;
            human.PlaceAt(humanStart);
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
                ApplyDefaultBeach();
                day.Set(2f);
                yield return TurtleAct(false);
            }
            else yield return HumanAct();
            _busy = false;
        }

        /// <summary>Turtle-only start: the beach as a typical day would have left it.</summary>
        public void ApplyDefaultBeach()
        {
            if (log != null) log.UsedDefaults = true;
            canToss?.ForceComplete();
            castle?.ForceComplete();
            if (sandwich != null) sandwich.ForceComplete();
            if (bag != null) bag.ForceLanded();
            if (music != null) { music.SetOn(true); music.SetVolumeLevel(2); }
            fireworks?.ForceComplete();
            garland?.ForceComplete();
        }

        // ================================================================== ACT I - the human
        IEnumerator HumanAct()
        {
            GameManager.Instance.SetState(GameState.Human);
            perception.Apply(Perception.Human, true);
            ambient.Apply(Perception.Human, 0.5f);
            day.Set(0f);
            human.PlaceAt(humanStart);
            human.SetActive(true);
            seat.Sit(chairSeat);
            // each activity's objects wake up with its scene (no fireworks in the afternoon)
            Allow(shovel, false); Allow(castle.Pail.Grab, false); Allow(sandwich.GetComponent<VRGrabbable>(), false);
            Allow(garland.Reel, false); Allow(flashlight.GetComponent<VRGrabbable>(), false);
            fireworks.Button.GetComponent<PokeButton>().interactable = false;
            yield return new WaitForSeconds(0.5f);
            AudioOneShot.Play2D(SameShoreAssets.Pick(SameShoreAssets.I.gullCalls), 0.2f);
            StartCoroutine(FadeRoutine(0f, 3f));
            yield return Subtitle("A perfect day at the beach.", 3.2f, false);
            yield return Subtitle("Relax and enjoy your afternoon.", 2.8f, false);
            ShowHint(DesktopTestingMode.Active
                ? "<b>Look</b> mouse  ·  <b>Grab / throw / press</b> E or left click\n<b>Use</b> what you hold: F or right click  ·  <b>Pause</b> Esc"
                : "<b>Grab</b> grip button  ·  <b>Use / press</b> trigger or touch\n<b>Pause</b> menu button", 10f);

            // ---- scenes 1-2: drink, music, can toss (seated)
            Scene = "afternoon";
            canToss.Active = true;
            objectives.Begin("TODAY'S PLANS", new List<Objective>
            {
                new Objective { Id = "drink", Title = "Pick up your drink", Sticky = true, Check = () => drink.PickedUp,
                                Target = () => drink.transform.position, Tip = "Reach for the bottle and squeeze the grip button.\nBring it to your mouth for a sip." },
                new Objective { Id = "music", Title = "Turn on the music", Check = () => music.IsOn,
                                Target = () => music.transform.position, Tip = "Touch the play button on the speaker (or point and pull the trigger)." },
                new Objective { Id = "cans", Title = "Try the can toss challenge", Sticky = true, Check = () => canToss.Done,
                                Progress = () => $"{canToss.Thrown}/{canToss.Total} thrown", Target = () => NextCan(),
                                Tip = "Three empty cans, one bin.\nGrab a can and throw it - let go of the grip mid-swing." },
            });
            yield return Step(() => objectives.IsDone("drink") && objectives.IsDone("music") && canToss.Done,
                              () => { drink.ForceComplete(); if (!music.IsOn) music.SetOn(true); canToss.ForceComplete(); },
                              () => !canToss.Done || !music.IsOn ? null : "drink");
            yield return new WaitForSeconds(1.6f);
            ShowHint($"CAN TOSS - {canToss.Scored} of {canToss.Total} in the bin.\nTime to stretch your legs.", 4f);
            yield return fader.FadeTo(1f, 0.5f);
            seat.Stand();
            yield return new WaitForSeconds(0.2f);
            yield return fader.FadeTo(0f, 0.6f);

            // ---- scene 3: sandcastle
            Scene = "sandcastle";
            Allow(shovel, true); Allow(castle.Pail.Grab, true);
            yield return Subtitle("Time to build a sandcastle!", 2.6f, false);
            var pail = castle.Pail;
            objectives.Begin("TODAY'S PLANS", new List<Objective>
            {
                new Objective { Id = "shovel", Title = "Pick up the shovel", Sticky = true, Check = () => shovel.WasEverGrabbed,
                                Target = () => shovel.transform.position, Tip = "The shovel and the bucket are waiting by the castle spot." },
                new Objective { Id = "fill", Title = "Dig sand and fill the bucket", Check = () => castle.Finished || pail.Full || pail.Scoops == 0 && castle.Towers > 0 && castle.Towers >= castle.TowersNeeded,
                                Progress = () => castle.Finished ? "" : $"{pail.Scoops}/{pail.ScoopsToFill}",
                                Target = () => shovel.GetComponent<BeachShovel>().Loaded ? pail.transform.position : castle.DigSpot.position,
                                Tip = "Push the shovel into the sand by the castle spot to scoop,\nthen tip it over the bucket. (The trigger also works.)" },
                new Objective { Id = "flip", Title = "Turn it over on the sand and lift it", Check = () => castle.Finished || castle.Towers > 0 && pail.Scoops == 0,
                                Target = () => pail.Grab.IsHeld ? castle.Plot.position : pail.transform.position,
                                Tip = "Hold the full bucket upside down over the marked spot, let it go,\nthen grab it again and lift it. (Or press the trigger while holding it.)" },
                new Objective { Id = "castle", Title = "Finish your castle", Sticky = true, Check = () => castle.Finished,
                                Progress = () => $"{castle.Towers}/{castle.TowersNeeded} towers", Target = () => castle.Plot.position },
            });
            yield return Step(() => castle.Finished, () => castle.ForceComplete(), null);
            ShowHint("What a castle!", 3f);
            yield return new WaitForSeconds(2.5f);
            if (shovel.IsHeld) shovel.Holder.ForceRelease();
            if (pail.Grab.IsHeld) pail.Grab.Holder.ForceRelease();

            // ---- scene 4: snack
            Scene = "snack";
            Allow(sandwich.GetComponent<VRGrabbable>(), true);
            yield return Subtitle("Time for a snack!", 2.4f, false);
            objectives.Begin("TODAY'S PLANS", new List<Objective>
            {
                new Objective { Id = "towel", Title = "Go back to your towel", Sticky = true, Check = () => Near(towel.position, 1.8f), Target = () => towel.position },
                new Objective { Id = "take", Title = "Take the sandwich from your beach bag", Sticky = true, Check = () => sandwich.PickedUp,
                                Target = () => sandwich.transform.position, Tip = "It's in the beach bag next to the towel." },
                new Objective { Id = "eat", Title = "Eat your snack", Sticky = true, Check = () => sandwich.Eaten,
                                Progress = () => $"{sandwich.Bites}/{sandwich.BitesNeeded} bites", Target = () => sandwich.isActiveAndEnabled ? sandwich.transform.position : (Vector3?)null,
                                Tip = "Bring the sandwich to your mouth to take a bite." },
            });
            yield return Step(() => sandwich.Eaten, () => sandwich.ForceComplete(), null);
            yield return new WaitForSeconds(1.2f);
            yield return bag.BlowAway();
            yield return new WaitForSeconds(0.6f);
            ShowHint("<i>Oh well... it's just a bag.</i>", 4f);
            yield return new WaitForSeconds(3.5f);

            // ---- scene 5: evening entertainment
            Scene = "evening";
            fireworks.Button.GetComponent<PokeButton>().interactable = true;
            day.TransitionTo(1f, 35f);
            yield return Subtitle("Let's enjoy the evening!", 2.6f, false);
            objectives.Begin("TODAY'S PLANS", new List<Objective>
            {
                new Objective { Id = "louder", Title = "Turn up the music", Sticky = true, Check = () => music.IsOn && music.VolumeLevel >= 1,
                                Target = () => music.transform.position, Tip = "Press the volume button on the speaker." },
                new Objective { Id = "fireworks", Title = "Launch the fireworks", Sticky = true, Check = () => fireworks.Started,
                                Target = () => fireworks.Button.position, Tip = "The fireworks are set up in the roped-off area by the water.\nPress the red button on the launcher." },
                new Objective { Id = "show", Title = "Enjoy the show", Sticky = true, Check = () => fireworks.Finished, HideMarker = true },
            });
            yield return Step(() => fireworks.Finished, () => { if (!music.IsOn) music.SetOn(true); music.SetVolumeLevel(1); fireworks.ForceComplete(); }, null);

            // ---- scene 6: night, the light string
            Scene = "lights";
            Allow(garland.Reel, true);
            day.TransitionTo(2f, 14f);
            yield return new WaitForSeconds(2f);
            yield return Subtitle("Let's decorate the beach with lights!", 2.8f, false);
            objectives.Begin("TODAY'S PLANS", new List<Objective>
            {
                new Objective { Id = "reel", Title = "Pick up the string of lights", Sticky = true, Check = () => garland.Reel == null || garland.Reel.WasEverGrabbed || garland.Hooked > 0,
                                Target = () => garland.Reel != null ? garland.Reel.transform.position : (Vector3?)null, Tip = "The reel of lights is by the umbrella." },
                new Objective { Id = "umbrella", Title = "Clip it to the umbrella", Sticky = true, Check = () => garland.Hooked >= 1,
                                Target = () => garland.Hooks[0].position, Tip = "Bring the reel to the hook on the umbrella pole." },
                new Objective { Id = "posts", Title = "Clip it to every post", Sticky = true, Check = () => garland.Complete,
                                Progress = () => $"{Mathf.Max(0, garland.Hooked - 1)}/{garland.HooksTotal - 1}",
                                Target = () => garland.NextHook != null ? garland.NextHook.position : (Vector3?)null,
                                Tip = "Walk to the glowing hook on the next post and touch it with the reel." },
                new Objective { Id = "on", Title = "Switch on the lights", Sticky = true, Check = () => garland.LightsOn, HideMarker = true },
            });
            yield return Step(() => garland.LightsOn, () => garland.ForceComplete(), null);
            yield return new WaitForSeconds(1.5f);
            ShowHint("Beautiful.", 3f);

            // ---- scene 7: the flashlight and the nest
            Scene = "flashlight";
            Allow(flashlight.GetComponent<VRGrabbable>(), true);
            var lampGrab = flashlight.GetComponent<VRGrabbable>();
            Vector3 nestPos = nest.transform.position;
            Vector3 lastHook = garland.Hooks[garland.Hooks.Length - 1].position;
            objectives.Begin("TONIGHT", new List<Objective>
            {
                new Objective { Id = "lamp", Title = "Take your flashlight", Sticky = true, Check = () => lampGrab.WasEverGrabbed,
                                Target = () => flashlight.transform.position, Tip = "It's dark away from the lights.\nYour flashlight is on the towel." },
                new Objective { Id = "on", Title = "Switch it on", Sticky = true, Check = () => flashlight.IsOn, HideMarker = true,
                                Tip = "Pull the trigger while you hold it." },
                new Objective { Id = "follow", Title = "Follow the lights to the end of the string", Sticky = true,
                                Check = () => Near(lastHook, 2.2f) || Near(nestPos, 2.4f), Target = () => lastHook },
            });
            yield return Step(() => objectives.IsDone("lamp"), () =>
            {
                var hand = VRHand.Right != null ? VRHand.Right : VRHand.Left;
                if (hand != null && hand.Held == null) hand.Grab(lampGrab);
                if (!flashlight.IsOn) flashlight.Toggle();
            }, null);
            log.StartNightRecording();
            yield return Step(() => objectives.IsDone("follow"), () => TeleportNear(lastHook), null);
            yield return nest.HatchSequence();
            yield return Subtitle("Wow... baby turtles!", 3f, false);
            yield return new WaitForSeconds(1f);
            objectives.Begin("TONIGHT", new List<Objective>
            {
                new Objective { Id = "back", Title = "Return to your towel", Sticky = true, Check = () => Near(towel.position, 3.2f), Target = () => towel.position },
            });
            yield return Step(() => objectives.IsDone("back"), () => TeleportNear(towel.position), null);

            // ---- scene 8: the photo
            Scene = "photo";
            photoTurtle.gameObject.SetActive(true);
            photoTurtle.transform.SetPositionAndRotation(photoTurtleStart.position, photoTurtleStart.rotation);
            photoTurtle.StartCrawl(0f);
            ShowHint("Look! A little turtle, heading for the sea.", 5f);
            objectives.Begin("TONIGHT", new List<Objective>
            {
                new Objective { Id = "closer", Title = "Get closer to the little turtle", Sticky = true, Check = () => Near(photoTurtle.transform.position, 1.7f),
                                Target = () => photoTurtle.transform.position },
            });
            yield return Step(() => objectives.IsDone("closer"), () => TeleportNear(photoTurtle.transform.position), null);
            photoTurtle.Freeze(true);
            yield return Subtitle("What a perfect photo!", 2.4f, false);
            phone.gameObject.SetActive(true);
            phone.Subject = photoTurtle.transform;
            phone.Armed = true;
            PlacePhoneInPocket();
            var phoneGrab = phone.GetComponent<VRGrabbable>();
            objectives.Begin("TONIGHT", new List<Objective>
            {
                new Objective { Id = "phone", Title = "Take out your phone", Sticky = true, Check = () => phoneGrab.WasEverGrabbed,
                                Target = () => phone.transform.position, Tip = "Your phone is right in front of you - grab it." },
                new Objective { Id = "shoot", Title = "Take the photo", Sticky = true, Check = () => phone.PhotoTaken, HideMarker = true,
                                Tip = "Frame the turtle on the screen and pull the trigger." },
            });
            yield return Step(() => phone.PhotoTaken, () => phone.Shoot(), null);
            yield return new WaitForSeconds(1.8f);
            log.StopNightRecording();
            objectives.Stop();
            human.SetActive(false);
            yield return fader.FadeTo(1f, 2.5f);
            photoTurtle.gameObject.SetActive(false);
            yield return Subtitle("For you, it was just another perfect day.", 4f, true);
            yield return new WaitForSeconds(1.4f);
            yield return Subtitle("But what was it like for her?", 3.6f, true);

            if (Mode == ExperienceMode.HumanOnly)
            {
                ShowEnd("A perfect day", log.Summary(),
                        ("Continue as the hatchling", () => StartCoroutine(ContinueAsTurtle())),
                        ("Play the day again", () => Reload(ExperienceMode.HumanOnly)),
                        ("Main menu", () => Reload(null)));
                yield break;
            }
            yield return TurtleAct(true);
        }

        IEnumerator ContinueAsTurtle()
        {
            _end.Hide();
            Mode = ExperienceMode.Full;
            yield return TurtleAct(true);
        }

        /// <summary>
        /// Waits for an activity to be completed. Never stuck: the current objective's tip comes back after a
        /// while without progress, and the pause menu offers "Skip this activity" (skip action).
        /// </summary>
        IEnumerator Step(Func<bool> done, Action skip, Func<string> unused)
        {
            _skipAction = skip;
            Objective last = null;
            float since = Time.time;
            while (!done())
            {
                if (_skip)
                {
                    _skip = false;
                    skip?.Invoke();
                    yield return null;
                    continue;
                }
                if (objectives.Current != last) { last = objectives.Current; since = Time.time; }
                else if (last != null && !string.IsNullOrEmpty(last.Tip) && Time.time - since > idleTipSeconds)
                {
                    since = Time.time;
                    ShowHint(last.Tip + "\n<size=75%>(Stuck? The pause menu can skip this activity.)</size>", 8f);
                }
                yield return null;
            }
            _skipAction = null;
        }

        static void Allow(VRGrabbable g, bool on)
        {
            if (g != null) g.grabbingEnabled = on;
        }

        Vector3? NextCan()
        {
            foreach (var c in canToss.Cans)
                if (!c.Thrown) return c.transform.position;
            return null;
        }

        static bool Near(Vector3 p, float r)
        {
            if (PlayerRig.Head == null) return false;
            Vector3 d = PlayerRig.Head.position - p;
            d.y = 0;
            return d.magnitude < r;
        }

        void TeleportNear(Vector3 p)
        {
            StartCoroutine(TeleportRoutine(p));
        }

        IEnumerator TeleportRoutine(Vector3 p)
        {
            yield return fader.FadeTo(1f, 0.4f);
            Vector3 from = PlayerRig.Head.position;
            Vector3 dir = Vector3.ProjectOnPlane(p - from, Vector3.up).normalized;
            var loco = FindFirstObjectByType<SmoothLocomotion>();
            Vector3 spot = MeshKit.OnGround(p - dir * 1.2f);
            loco.PlaceAt(spot, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg);
            yield return fader.FadeTo(0f, 0.5f);
        }

        void PlacePhoneInPocket()
        {
            Transform h = PlayerRig.Head;
            Vector3 p = h.position + PlayerRig.FlatForward * 0.35f + Vector3.Cross(Vector3.up, PlayerRig.FlatForward) * 0.12f - Vector3.up * 0.55f;
            var rb = phone.GetComponent<Rigidbody>();
            rb.isKinematic = true;   // floats at hand height until grabbed (taken out of the pocket)
            phone.transform.SetPositionAndRotation(p, Quaternion.LookRotation(PlayerRig.FlatForward, Vector3.up));
            rb.position = p;
        }

        // ================================================================== ACT II - the hatchling
        IEnumerator TurtleAct(bool fromHuman)
        {
            GameManager.Instance.SetState(GameState.Transition);
            human.SetActive(false);
            if (flashlight != null) flashlight.gameObject.SetActive(false);   // it is in the giant's hand
            if (phone != null) phone.gameObject.SetActive(false);
            if (photoTurtle != null) photoTurtle.gameObject.SetActive(false);
            day.Set(2f);
            ambient.Apply(Perception.Hatchling, 2.5f);
            if (fader.Alpha < 0.99f) yield return fader.FadeTo(1f, fadeSeconds);
            // beneath the sand: the fireworks were felt more than heard
            if (log.FireworksLaunched > 0) StartCoroutine(UndergroundRumbles());
            AudioOneShot.Play2D(SameShoreAssets.I.nestScratching, 0.7f);
            yield return Subtitle("Earlier that evening, beneath the sand...", 3.8f, true);
            AudioOneShot.Play2D(SameShoreAssets.I.turtleBreath, 0.8f);
            yield return Subtitle("The same night.\nA few minutes before the photo.", 3.4f, true);
            yield return EnterTurtle(true);
        }

        IEnumerator UndergroundRumbles()
        {
            for (int i = 0; i < 3; i++)
            {
                AudioOneShot.Play2D(SameShoreAssets.I.Clip("firework_rumble"), 0.55f);
                XRInput.HapticBoth(0.35f, 0.4f, 0.15f);
                yield return new WaitForSeconds(UnityEngine.Random.Range(1.4f, 2.2f));
            }
        }

        IEnumerator EnterTurtle(bool emerge)
        {
            GameManager.Instance.SetState(GameState.Hatchling);   // PlayerPhaseSwitcher shrinks the rig into the nest
            turtle.enabled = false;
            turtleFX.ClearTracks();
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
            turtleStory.Begin();
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
            objectives.Begin("REACH THE OCEAN", turtleStory.BuildObjectives());
            ShowHint(DesktopTestingMode.Active
                ? "<b>W</b> crawl  ·  <b>S</b> back  ·  <b>A / D</b> turn your body slowly\n<b>Space</b> sudden burst  ·  mouse: look around"
                : "<b>Stick forward</b>: crawl  ·  <b>back</b>: reverse  ·  <b>left / right</b>: turn slowly\n<b>A / X</b>: sudden burst  ·  look around freely", 10f);
        }

        IEnumerator RespawnAtCheckpoint(string message)
        {
            if (_busy || _ending) yield break;
            _busy = true;
            turtle.enabled = false;
            ShowHint(message, 4f);
            yield return fader.FadeTo(1f, fadeSeconds);
            Vector3 cp = turtleStory.CheckpointPosition();
            float yaw = sea != null ? Quaternion.LookRotation(Vector3.ProjectOnPlane(sea.position - cp, Vector3.up)).eulerAngles.y : 0f;
            turtle.TeleportTo(cp, yaw);
            turtleFX.ClearTracks();
            yield return new WaitForSeconds(0.3f);
            yield return fader.FadeTo(0f, fadeSeconds);
            turtle.Resume();
            _busy = false;
        }

        // ================================================================== ending
        /// <summary>True when the last ending played the gull carrying her away (tests).</summary>
        public bool EndingCarried { get; private set; }
        /// <summary>Highest the hatchling was lifted during the ending (tests).</summary>
        public float EndingLift { get; private set; }

        /// <summary>
        /// The ending: as the wave reaches her, a gull swoops in from the sea (in front of her, visible), grabs her
        /// and climbs away with her over the water. Comfort: no rotation of the view, one smooth slow climb with
        /// eased acceleration, the view darkening progressively until black; the whole lift lasts ~3.6 s.
        /// </summary>
        IEnumerator GullTakesHer(Seagull gull)
        {
            Transform body = turtle.transform;
            Vector3 p = body.position;
            Vector3 seaDir = sea != null ? Vector3.ProjectOnPlane(sea.position - p, Vector3.up) : Vector3.forward;
            if (seaDir.sqrMagnitude < 0.01f) seaDir = Vector3.forward;
            seaDir.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, seaDir);

            // 1) out of the night sky over the waves, straight at her, while the wave rushes in
            gull.TeleportTo(p + seaDir * 9f + side * 1.5f + Vector3.up * 4.5f);
            gull.Screech();
            yield return new WaitForSeconds(0.5f);
            bool arrived = false;
            gull.Dive(p + Vector3.up * 0.1f, () => arrived = true);
            AudioOneShot.Play2D(SameShoreAssets.I.Clip("wings_fast"), 0.8f);
            for (float t = 0; t < 4f && !arrived; t += Time.deltaTime) yield return null;

            // 2) the grab: a thump, the sand drops away
            AudioOneShot.Play2D(SameShoreAssets.I.Clip("gull_strike"), 0.85f);
            XRInput.HapticBoth(0.7f, 0.25f);
            fader.SetColor(Color.black);
            var cc = body.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            gull.Carry(body, Vector3.up * 0.14f);
            ambient.Duck(0.5f, 2.5f);

            // 3) carried off over the water: one smooth climb (eased), the view darkens progressively
            Vector3 from = body.position;
            Vector3 to = from + seaDir * 4f + side * 0.8f + Vector3.up * 3f;
            const float seconds = 3.6f;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                float e = k * k * (3f - 2f * k);
                body.position = Vector3.Lerp(from, to, e) + Vector3.up * Mathf.Sin(t * 9f) * 0.01f * k;   // wing beats
                EndingLift = Mathf.Max(EndingLift, body.position.y - from.y);
                fader.SetAlphaDirect(Mathf.SmoothStep(0f, 0.75f, k));
                if (Mathf.Repeat(t, 0.45f) < Time.deltaTime) XRInput.HapticBoth(0.18f, 0.05f);
                yield return null;
            }
            finalWave.Clear();
            yield return fader.FadeTo(1f, 0.8f);
            ambient.Duck(0f, 1.2f);
            PlayInDark(SameShoreAssets.Pick(SameShoreAssets.I.gullCalls), 0.4f, 0.9f);
            yield return new WaitForSeconds(1.5f);
        }

        IEnumerator Ending()
        {
            _ending = true;
            _busy = true;
            turtleStory.Stop();
            objectives.HUD.Suppressed = true;
            ShowHint("", 0.01f);
            objectives.Stop();
            // the wave comes for her... and so does the gull
            var gull = gulls != null ? gulls.Gull : null;
            Coroutine wave = StartCoroutine(finalWave.Play(turtle.transform, sea, ambient, gull == null));
            if (gull != null) yield return GullTakesHer(gull);
            else
            {
                yield return wave;
                yield return new WaitForSeconds(1.6f);
                finalWave.StopUnderwater();
                ambient.Duck(0f, 1.2f);
                yield return new WaitForSeconds(1.3f);
                PlayInDark(SameShoreAssets.I.Clip("gull_strike"), 0.85f, 1f);
            }
            EndingCarried = gull != null;
            yield return new WaitForSeconds(3.2f);
            PlayInDark(SameShoreAssets.I.endingPad, 0.45f, 1f);
            ShowEnd("SAME SHORE",
                    "<size=120%>One beach. Two bodies. Two very different worlds.</size>\n\n" +
                    "<color=#c9d6e2>What seemed harmless to one became a struggle for the other.</color>",
                    ("Experience it again", () => Reload(ExperienceMode.Full)),
                    ("Main menu", () => Reload(null)));
            _busy = false;
        }

        static void PlayInDark(AudioClip clip, float volume, float pitch)
        {
            if (clip == null) return;
            var go = new GameObject("~sfxDark");
            var a = go.AddComponent<AudioSource>();
            a.clip = clip;
            a.volume = volume;
            a.pitch = pitch;
            a.spatialBlend = 0;
            a.ignoreListenerVolume = true;   // the world is silent, only this is heard
            a.Play();
            Destroy(go, clip.length / pitch + 0.2f);
        }

        void Reload(ExperienceMode? mode) => StartCoroutine(ReloadRoutine(mode));

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

        IEnumerator FadeRoutine(float to, float seconds)
        {
            yield return fader.FadeTo(to, seconds);
        }

        // ================================================================== pause menu
        void PauseInput()
        {
            bool pressed = OVRInput.GetDown(OVRInput.Button.Start) || XRInput.KeyDown(XRInput.KeyId.P) ||
                           (DesktopTestingMode.Active && XRInput.KeyDown(XRInput.KeyId.Escape));
            if (!pressed) return;
            var s = GameManager.Instance.Current;
            if (Paused) ClosePause();
            else if ((s == GameState.Human || s == GameState.Hatchling) && !_ending) OpenPause();
        }

        void OpenPause()
        {
            Paused = true;
            Time.timeScale = 0f;
            _pause.ClearButtons();
            bool turtlePhase = GameManager.Instance.Current == GameState.Hatchling;
            float y = 0.2f;
            void Btn(string label, string sub, Action a, XRInput.KeyId key)
            {
                _pause.AddButton(label, sub, new Vector2(0, y), new Vector2(0.7f, 0.12f), a, key);
                y -= 0.145f;
            }
            Btn("Resume", null, ClosePause, XRInput.KeyId.Digit1);
            Btn(objectives.HUD.Visible ? "Hide objectives" : "Show objectives", null, () => { objectives.HUD.Visible = !objectives.HUD.Visible; ClosePause(); }, XRInput.KeyId.Digit2);
            if (turtlePhase)
            {
                Btn("Back to the last checkpoint", "If you are lost or stuck", () => { ClosePause(); StartCoroutine(RespawnAtCheckpoint("Back on the path.")); }, XRInput.KeyId.Digit3);
                Btn("Restart the hatchling's journey", "From the nest, same beach", () => Reload(ExperienceMode.TurtleOnly), XRInput.KeyId.R);
            }
            else
            {
                if (_skipAction != null) Btn("Skip this activity", "Complete it automatically", () => { _skip = true; ClosePause(); }, XRInput.KeyId.Digit3);
                Btn("Restart the day", "The beach is reset", () => Reload(Mode == ExperienceMode.HumanOnly ? ExperienceMode.HumanOnly : ExperienceMode.Full), XRInput.KeyId.R);
            }
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

        /// <summary>Tests / tools: request the skip of the current activity.</summary>
        public void RequestSkip() => _skip = true;

        // ================================================================== UI
        IEnumerator Subtitle(string text, float seconds, bool centered)
        {
            _subText.text = text;
            _subText.alpha = 0;
            _subtitle.Follow.heightOffset = centered ? 0f : 0.12f;
            _subtitle.Follow.distance = centered ? 1.6f : 1.7f;
            _subtitle.Show();
            _subtitle.Follow.Snap();
            for (float t = 0; t < 0.7f; t += Time.deltaTime) { _subText.alpha = t / 0.7f; yield return null; }
            _subText.alpha = 1;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                if (XRInput.KeyDown(XRInput.KeyId.Space) || (centered && XRInput.AnyTriggerDown)) break;
                yield return null;
            }
            for (float t = 0; t < 0.6f; t += Time.deltaTime) { _subText.alpha = 1 - t / 0.6f; yield return null; }
            _subtitle.Hide();
        }

        void ShowEnd(string title, string body, params (string label, Action action)[] buttons)
        {
            _endTitle.text = title;
            _endBody.text = body;
            _end.ClearButtons();
            float w = 0.52f, gap = 0.05f;
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
            if (string.IsNullOrEmpty(text)) { _hint.Hide(); return; }
            _hintText.text = text;
            _hint.Show();
            _hint.Follow.Snap();
            _hintUntil = Time.unscaledTime + seconds;
        }

        void BuildUI()
        {
            Color title = new Color(0.97f, 0.96f, 0.92f);
            Color accent = new Color(0.98f, 0.72f, 0.45f);

            _menu = UIPanel.Create("StartMenu", new Vector2(1.5f, 1.02f), 1.7f);
            _menu.Follow.follow = false;
            _menu.AddText("O N E   B E A C H ,   T W O   B O D I E S", 0.32f, new Vector2(0, 0.42f), new Vector2(1.4f, 0.08f), accent);
            _menu.AddText("SAME SHORE", 1.45f, new Vector2(0, 0.3f), new Vector2(1.4f, 0.2f), title, FontStyles.Bold);
            _menu.AddText("Spend a perfect day at the beach. Then live the same night as a newborn sea turtle.",
                          0.32f, new Vector2(0, 0.15f), new Vector2(1.38f, 0.13f), new Color(0.85f, 0.87f, 0.9f));
            _menu.AddButton("Full Experience", "The human's day, then the hatchling's night", new Vector2(0, -0.03f), new Vector2(0.9f, 0.15f),
                            () => Choose(ExperienceMode.Full), XRInput.KeyId.Digit1);
            _menu.AddButton("Human Experience", "A day at the beach", new Vector2(-0.235f, -0.22f), new Vector2(0.44f, 0.15f),
                            () => Choose(ExperienceMode.HumanOnly), XRInput.KeyId.Digit2);
            _menu.AddButton("Turtle Experience", "Start in the nest", new Vector2(0.235f, -0.22f), new Vector2(0.44f, 0.15f),
                            () => Choose(ExperienceMode.TurtleOnly), XRInput.KeyId.Digit3);
            _menu.AddText(DesktopTestingMode.Active ? "Desktop testing: click a button with the mouse (or press 1 / 2 / 3)"
                                                    : "Point with a controller and pull the trigger", 0.26f,
                          new Vector2(0, -0.42f), new Vector2(1.3f, 0.06f), new Color(0.62f, 0.68f, 0.74f));

            _subtitle = UIPanel.Create("Subtitle", new Vector2(1.4f, 0.26f), 1.5f, new Color(0, 0, 0, 0.35f));
            _subtitle.Follow.follow = true;
            _subText = _subtitle.AddText("", 0.5f, Vector2.zero, new Vector2(1.34f, 0.24f), title);

            _end = UIPanel.Create("EndScreen", new Vector2(1.75f, 1.2f), 1.55f);
            _end.Follow.follow = false;
            _endTitle = _end.AddText("", 1.1f, new Vector2(0, 0.42f), new Vector2(1.6f, 0.2f), title, FontStyles.Bold);
            _endBody = _end.AddText("", 0.42f, new Vector2(0, 0.06f), new Vector2(1.62f, 0.5f), new Color(0.9f, 0.92f, 0.95f));

            _pause = UIPanel.Create("PauseMenu", new Vector2(0.86f, 1.1f), 1.3f);
            _pause.Follow.follow = false;
            _pause.AddText("PAUSED", 0.7f, new Vector2(0, 0.45f), new Vector2(0.8f, 0.1f), title, FontStyles.Bold);

            _hint = UIPanel.Create("Hint", new Vector2(1.1f, 0.3f), 1.25f);
            _hint.Follow.heightOffset = -0.3f;
            _hintText = _hint.AddText("", 0.34f, Vector2.zero, new Vector2(1.04f, 0.28f), new Color(0.95f, 0.96f, 1f));
        }
    }
}
