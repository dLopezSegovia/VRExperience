using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using static SameShore.Tests.TestInput;

namespace SameShore.Tests
{
    /// <summary>
    /// End-to-end story runs driven by simulated mouse/keyboard input (buttons are clicked through the
    /// EventSystem, objects grabbed with E, the bodies moved with WASD). Teleports are used only to skip
    /// long walks/crawls, never to trigger outcomes.
    /// </summary>
    public static class StoryTests
    {
        static SmokeTestRunner T;

        static ObjectiveTracker Obj => ObjectiveTracker.Instance;
        static GameManager GM => GameManager.Instance;
        static TurtleController Turtle => Object.FindFirstObjectByType<TurtleController>();
        static GullHunt Gull => Object.FindFirstObjectByType<GullHunt>();
        static Transform Sea => GameObject.Find("SeaPoint").transform;

        static bool HumanReady() => GM.Current == GameState.Human && Object.FindFirstObjectByType<SmoothLocomotion>().MovementEnabled && Obj.Running;
        static bool TurtleReady() => GM.Current == GameState.Hatchling && Turtle.enabled && ScreenFader.Instance.Alpha < 0.05f && Obj.Running;
        static ObjectiveState State(string id) => Obj.Get(id) != null ? Obj.Get(id).State : (ObjectiveState)(-1);

        // ================================================================== CLEAN RUN
        public static IEnumerator Clean(SmokeTestRunner t)
        {
            T = t;
            Setup();
            Object.DontDestroyOnLoad(t.gameObject);
            T.Check("Desktop Testing mode active", DesktopTestingMode.Active);
            yield return ClickButton("Full Experience");
            yield return WaitFor(() => GameObject.Find("Subtitle") != null, 20f);
            T.Check("Intro starts on a black screen with subtitles", ScreenFader.Instance.Alpha > 0.9f && GameObject.Find("Subtitle") != null);
            yield return SkipStoryUntil(HumanReady, 120f);
            T.Check($"Human phase with objectives '{Obj.Title}' ({Obj.Objectives.Count})", HumanReady() && Obj.Title == "YOUR OBJECTIVES" && Obj.Objectives.Count == 5);
            T.Check("Objectives HUD shown (world-space canvas)", Obj.HUD.gameObject.activeSelf && Obj.HUD.Visible && Obj.HUD.GetComponent<Canvas>().renderMode == RenderMode.WorldSpace);
            T.Check($"First objective is the flashlight ({Obj.Current?.Id})", Obj.Current != null && Obj.Current.Id == "flashlight");
            yield return Click();   // capture the mouse
            yield return new WaitForSeconds(1f);
            var avatar = Object.FindFirstObjectByType<HumanAvatar>();
            float torsoDrop = PlayerRig.Head.position.y - avatar.torso.position.y;
            T.Check($"Human body below the eyes (hips {torsoDrop:F2} m under the camera)", torsoDrop > 0.55f && torsoDrop < 0.9f &&
                    avatar.torso.GetComponentInChildren<Renderer>().enabled);
            SaveView("01_human_intro.png");

            // ---- 1. flashlight
            var interactor = Object.FindFirstObjectByType<DesktopInteractor>();
            var lamp = Object.FindFirstObjectByType<Flashlight>();
            yield return WalkTo(lamp.transform.position, 1.3f);
            yield return AimAt(lamp.transform.position);
            yield return Tap(Key.E);
            T.Check("E picks up the flashlight", interactor.Held != null && interactor.Held.GetComponent<Flashlight>() == lamp);
            yield return null;
            float align = Vector3.Dot(lamp.transform.forward, PlayerRig.Head.forward);
            T.Check($"Flashlight held pointing forward (beam/view alignment {align:F2})", align > 0.95f);
            T.Check("Visible light cone while on", lamp.GetComponentInChildren<LightConeVisual>() != null && lamp.IsOn);
            yield return AimAt(lamp.transform.position + PlayerRig.FlatForward * 3f - Vector3.up * 1.4f);
            yield return new WaitForSeconds(0.3f);
            SaveView("02_flashlight_held.png");
            yield return Tap(Key.F);
            yield return new WaitForSeconds(0.3f);
            T.Check("F switches it off -> objective done", !lamp.IsOn && State("flashlight") == ObjectiveState.Done);
            yield return Tap(Key.E);

            // ---- 2. plastic (one piece by hand, the rest moved by the harness to keep the run short)
            TrashItem bottle = TrashItem.All.Find(x => x.Label == "bottle" && !x.Cleared);
            yield return WalkTo(bottle.transform.position, 1.2f);
            yield return AimAt(Center(bottle.gameObject));
            yield return Tap(Key.E);
            T.Check("E picks up a bottle", interactor.Held == bottle.GetComponent<Rigidbody>());
            var bin = Object.FindFirstObjectByType<TrashBin>();
            yield return WalkTo(bin.transform.position, 1.6f);
            yield return AimAt(bin.transform.position + Vector3.up * 0.4f);
            yield return Click();
            yield return new WaitForSeconds(2f);
            T.Check($"Bottle dropped into the bin; progress {Obj.Get("plastic").Progress()}", bottle.InBin && Obj.Get("plastic").Progress().StartsWith("1/"));
            foreach (var item in TrashItem.All.ToArray())
            {
                if (item.Cleared) continue;
                var rb = item.GetComponent<Rigidbody>();
                rb.position = bin.transform.position + Vector3.up * 0.3f;
                item.transform.position = rb.position;
                yield return new WaitForSeconds(0.25f);
            }
            yield return new WaitForSeconds(1.5f);
            T.Check($"All plastic removed -> objective done ({Obj.Get("plastic").Progress()})", State("plastic") == ObjectiveState.Done);

            // ---- 3. hole
            var hole = Object.FindFirstObjectByType<SandHole>();
            SandBucket bucket = null;
            foreach (var b in Object.FindObjectsByType<SandBucket>(FindObjectsSortMode.None))
                if (bucket == null || (b.transform.position - hole.transform.position).sqrMagnitude < (bucket.transform.position - hole.transform.position).sqrMagnitude) bucket = b;
            yield return WalkTo(bucket.transform.position, 1.2f);
            yield return AimAt(Center(bucket.gameObject));
            yield return Tap(Key.E);
            T.Check("E picks up the bucket", interactor.Held != null && interactor.Held.GetComponent<SandBucket>() == bucket);
            yield return WalkTo(hole.transform.position, 1.6f);
            yield return AimAt(hole.transform.position);
            yield return HoldRight(2.6f);
            T.Check($"Sand pours into the hole gradually ({hole.Fill01 * 100:F0}%)", hole.Fill01 > 0.4f && hole.Fill01 < 0.99f);
            // scoop: aim the bucket at open sand next to the player (pushes it into the sand)
            Vector3 sand = PlayerRig.Head.position + PlayerRig.FlatForward * 0.9f + Vector3.Cross(Vector3.up, PlayerRig.FlatForward) * 0.6f;
            sand = TurtleJourney.Ground(sand);
            yield return AimAt(sand);
            yield return new WaitForSeconds(0.8f);
            {
                Vector3 mouth = bucket.transform.TransformPoint(Vector3.up * 0.34f);
                FootprintSystem.GroundHit(mouth, out RaycastHit gh);
                var gf = bucket.GetComponent<GrabFeedback>();
                Debug.Log($"[SameShoreTest] DIAG scoop target={interactor.Target?.name} bucket={bucket.Sand:F2} held={interactor.Held?.name} " +
                          $"tilt={Vector3.Angle(bucket.transform.up, Vector3.up):F0} pos={bucket.transform.position.y:F3} mouth={mouth.y:F3} ground={gh.point.y:F3} isHeld={gf?.IsHeld}");
            }
            T.Check($"Aiming the bucket at the sand scoops a new load ({bucket.Sand:F2})", bucket.Sand > 0.95f);
            yield return AimAt(hole.transform.position);
            yield return Hold(2.6f, Key.F);
            T.Check("Second bucket fills the hole -> objective done", hole.IsFilled && State("hole") == ObjectiveState.Done);
            yield return Tap(Key.E);

            // ---- 4. chair
            var chair = Object.FindFirstObjectByType<BeachChairMarker>();
            yield return WalkTo(chair.transform.position, 1.3f);
            yield return AimAt(Center(chair.gameObject));
            yield return Tap(Key.E);
            T.Check("E grabs the chair", interactor.Held != null && interactor.Held.GetComponent<BeachChairMarker>() != null);
            var safe = GameObject.Find("ChairSafeZone").transform;
            yield return WalkTo(safe.position, 1.5f, 40f);
            yield return AimAt(safe.position);
            yield return new WaitForSeconds(0.2f);
            yield return Tap(Key.E);
            yield return new WaitForSeconds(1.5f);
            T.Check($"Chair carried off the path -> objective done", State("chair") == ObjectiveState.Done);
            T.Check($"Current objective is now the nest ({Obj.Current?.Id})", Obj.Current != null && Obj.Current.Id == "nest");

            // ---- 5. nest
            var nest = Object.FindFirstObjectByType<NestController>();
            yield return WalkTo(nest.transform.position, 1.5f, 40f);
            yield return WaitFor(() => FindButton("Become the hatchling") != null, 30f);
            T.Check("Nest: a hatchling breaks through, then the choice appears", FindButton("Become the hatchling") != null && FindButton("Go back") == null);
            SaveView("03_nest_choice.png");
            yield return ClickButton("Become the hatchling");
            yield return WaitFor(() => GM.Current == GameState.Transition || GM.Current == GameState.Hatchling, 30f);
            T.Check("Fades to black with her breath before the change", ScreenFader.Instance.Alpha > 0.9f);
            yield return SkipStoryUntil(TurtleReady, 120f);
            T.Check($"Hatchling journey '{Obj.Title}'", TurtleReady() && Obj.Title == "YOUR JOURNEY");
            yield return Click();   // capture the mouse again after the nest choice
            float eye = PlayerRig.Head.position.y - Turtle.transform.position.y;
            T.Check($"Eyes about 8 cm above the sand ({eye * 100:F1} cm), scale {PlayerRig.Scale:F2}", eye > 0.06f && eye < 0.11f && Mathf.Abs(PlayerRig.Scale - 0.05f) < 0.001f);
            yield return AimAt(PlayerRig.Head.position + Turtle.transform.forward * 0.4f - Vector3.up * 1f);
            SaveView("04_hatchling_flippers.png");
            yield return Hold(0.4f, Key.W);
            yield return AimAt(PlayerRig.Head.position + Turtle.transform.forward * 0.4f - Vector3.up * 1f);
            SaveView("04b_hatchling_stroke.png");
            yield return AimAt(PlayerRig.Head.position + Turtle.transform.forward);
            SaveView("05_hatchling_view.png");
            var beach = Object.FindFirstObjectByType<BeachStateTracker>();
            T.Check("Beach changes persisted (light off, plastic gone, hole filled, chair moved)",
                    !lamp.IsOn && beach.TrashCleared == beach.TrashTotal && hole.IsFilled && beach.ChairClear);

            // ---- crawl out of the nest and towards the sea
            yield return Hold(20f, Key.W, () => State("ocean") == ObjectiveState.Done);
            T.Check("Leave the nest / Find the ocean completed by crawling", State("nest") == ObjectiveState.Done && State("ocean") == ObjectiveState.Done);

            // ---- seagull: dodge by crawling out of the circle
            var journey = Object.FindFirstObjectByType<TurtleJourney>();
            Vector3 cp = Sea.position - Vector3.forward * 9.4f;
            Turtle.TeleportTo(TurtleJourney.Ground(cp) + Vector3.up * 0.02f, 0f);
            yield return Hold(60f, Key.W, () => Gull.Current == GullHunt.Phase.Warning || journey.DistanceToSea < 3f);
            if (Gull.Current == GullHunt.Phase.Warning)
            {
                SaveView("06_gull_warning.png");
                T.Check("Gull warning: hover + shrinking circle on the sand", GameObject.Find("StrikeRing") != null);
                yield return Hold(3.5f, Key.W);
                T.Check($"Crawling out of the circle dodges the dive (dodged {Gull.Dodged}, strikes {Gull.Strikes})", Gull.Dodged >= 1 && Gull.Strikes == 0);
            }
            else T.Check("Gull attacked before the sea", false);
            yield return Hold(80f, Key.W, () => GM.Current == GameState.End);
            T.Check($"Escape the danger completed ({State("danger")})", State("danger") == ObjectiveState.Done);
            T.Check("Reaching the swash ends the run", GM.Current == GameState.End);
            yield return new WaitForSeconds(1.2f);
            SaveView("07_final_wave.png");
            yield return WaitFor(() => FindButton("Main menu") != null, 60f);
            var body = GameObject.Find("EndScreen").GetComponentsInChildren<TextMeshPro>();
            string all = string.Join("\n", System.Array.ConvertAll(body, x => x.text));
            T.Check("Ending: 'Great Job' + positive consequences + closing line",
                    all.Contains("Great Job") && all.Contains("You turned off the flashlight") && all.Contains("You removed the plastic") &&
                    all.Contains("You filled the hole") && all.Contains("You moved the chair") && all.Contains("Same shore. Different lives."));
            SaveView("08_summary.png");

            yield return ClickButton("Main menu");
            yield return WaitFor(() => GameObject.Find("StartMenu") != null && GM != null && GM.Current == GameState.Intro, 60f);
            T.Check("'Main menu' reloads the scene to the start menu", GameObject.Find("StartMenu") != null && Object.FindFirstObjectByType<Flashlight>().IsOn);
            T.Finish();
        }

        // ================================================================== DIRTY RUN
        public static IEnumerator Dirty(SmokeTestRunner t)
        {
            T = t;
            Setup();
            Object.DontDestroyOnLoad(t.gameObject);
            yield return ClickButton("Full Experience");
            yield return SkipStoryUntil(HumanReady, 120f);
            yield return Click();
            var nest = Object.FindFirstObjectByType<NestController>();
            yield return WalkTo(nest.transform.position, 1.5f, 40f);
            yield return WaitFor(() => FindButton("Go back") != null, 30f);
            T.Check("Nest with work left: choice offers 'Go back' and 'Become the hatchling'", FindButton("Go back") != null && FindButton("Become the hatchling") != null);
            yield return ClickButton("Go back");
            yield return new WaitForSeconds(0.5f);
            T.Check("'Go back' closes the choice and keeps the human phase", FindButton("Go back") == null && GM.Current == GameState.Human);
            yield return Click();   // capture the mouse again (desktop: panels free the cursor)
            Vector3 away = PlayerRig.Head.position + PlayerRig.FlatForward * -3.5f;
            yield return WalkTo(away, 0.6f);
            yield return WalkTo(nest.transform.position, 1.5f, 40f);
            yield return WaitFor(() => FindButton("Become the hatchling") != null, 30f);
            yield return ClickButton("Become the hatchling");
            yield return new WaitForSeconds(0.4f);
            T.Check("Unfinished objectives are marked as left behind",
                    State("flashlight") == ObjectiveState.Left && State("plastic") == ObjectiveState.Left &&
                    State("hole") == ObjectiveState.Left && State("chair") == ObjectiveState.Left && State("nest") == ObjectiveState.Done);
            yield return SkipStoryUntil(TurtleReady, 120f);
            T.Check("Hatchling phase starts", TurtleReady());

            // ---- the gull catches a hatchling that stays still in the open -> checkpoint respawn
            var journey = Object.FindFirstObjectByType<TurtleJourney>();
            yield return Hold(3f, Key.W);
            Vector3 cp = Sea.position - Vector3.forward * 9.4f + Vector3.right * 2.5f;
            Turtle.TeleportTo(TurtleJourney.Ground(cp) + Vector3.up * 0.02f, 0f);
            yield return Hold(1.5f, Key.W);   // pass the 9.6 m checkpoint
            Vector3 open = Sea.position - Vector3.forward * 8.3f + Vector3.right * 2.5f;
            Turtle.TeleportTo(TurtleJourney.Ground(open) + Vector3.up * 0.02f, 0f);   // into the gull's hunting zone, then stay still
            yield return WaitFor(() => !Turtle.enabled && Gull.Strikes >= 2, 180f);
            T.Check($"Staying still: the gull strikes twice and catches her ({Gull.Strikes})", Gull.Strikes >= 2);
            yield return WaitFor(() => Turtle.enabled && ScreenFader.Instance.Alpha < 0.05f, 30f);
            T.Check($"Respawned at a checkpoint, controls back ({journey.DistanceToSea:F1} m from the sea)", Turtle.enabled && journey.DistanceToSea > 8.5f && journey.DistanceToSea < 11f);
            T.Check("The seagull episode can start again", Gull.Current == GullHunt.Phase.Idle || Gull.Active);

            // ---- pause menu
            yield return Tap(Key.Escape);
            yield return null;
            T.Check("Esc opens the pause menu (time stopped)", GameObject.Find("PauseMenu") != null && Time.timeScale == 0f);
            yield return ClickButton("Restart from checkpoint");
            yield return WaitFor(() => Turtle.enabled && ScreenFader.Instance.Alpha < 0.05f && Time.timeScale == 1f, 30f);
            T.Check("'Restart from checkpoint' resumes the journey", Turtle.enabled && Time.timeScale == 1f && GameObject.Find("PauseMenu") == null);

            // ---- straight to the surf
            Vector3 near = Sea.position - Vector3.forward * 1.3f;
            Turtle.TeleportTo(TurtleJourney.Ground(near) + Vector3.up * 0.02f, 0f);
            yield return Hold(30f, Key.W, () => GM.Current == GameState.End);
            yield return WaitFor(() => FindButton("Try other choices") != null, 60f);
            var body = GameObject.Find("EndScreen").GetComponentsInChildren<TextMeshPro>();
            string all = string.Join("\n", System.Array.ConvertAll(body, x => x.text));
            T.Check("Ending acknowledges what was left: flashlight, plastic, hole, chair",
                    all.Contains("The flashlight stayed on") && all.Contains("pieces of plastic stayed on the sand") &&
                    all.Contains("The hole was still open") && all.Contains("The chair") && all.Contains("A seagull caught her"));
            if (!all.Contains("A seagull caught her")) Debug.Log("[SameShoreTest] DIAG summary: " + all);
            yield return ClickButton("Crawl again");
            yield return SkipStoryUntil(TurtleReady, 90f);
            T.Check("'Crawl again' restarts the hatchling at the nest with the same beach",
                    TurtleReady() && Object.FindFirstObjectByType<Flashlight>().IsOn);
            T.Finish();
        }

        // ================================================================== XR PATH (no desktop mode)
        public static IEnumerator Xr(SmokeTestRunner t)
        {
            T = t;
            Setup();
            T.Check("Desktop mode is OFF (XR path)", !DesktopTestingMode.Active && Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null);
            yield return Tap(Key.Digit1);                    // menu hotkey (controllers would use the laser)
            yield return SkipStoryUntil(HumanReady, 120f);
            T.Check("XR: human phase with objectives", HumanReady());
            var human = Object.FindFirstObjectByType<HumanController>();
            var nest = Object.FindFirstObjectByType<NestController>();
            var spot = new GameObject("~spot").transform;
            spot.position = nest.transform.position - Vector3.forward * 1.2f;
            human.PlaceAt(spot);
            yield return WaitFor(() => FindButton("Become the hatchling") != null, 30f);
            yield return Tap(Key.Digit2);
            yield return SkipStoryUntil(TurtleReady, 120f);
            T.Check("XR: hatchling phase", TurtleReady() && Mathf.Abs(PlayerRig.Scale - 0.05f) < 0.001f);
            Vector3 p0 = Turtle.transform.position;
            yield return Hold(4f, Key.W);
            T.Check($"XR: stick (W) crawls ({Flat(Turtle.transform.position - p0):F2} m)", Flat(Turtle.transform.position - p0) > 0.3f);
            Turtle.TeleportTo(TurtleJourney.Ground(Sea.position - Vector3.forward * 1.2f) + Vector3.up * 0.02f, 0f);
            yield return Hold(30f, Key.W, () => GM.Current == GameState.End);
            yield return WaitFor(() => FindButton("Main menu") != null, 60f);
            T.Check("XR: ending reached with buttons", FindButton("Main menu") != null);
            T.Finish();
        }
    }
}
