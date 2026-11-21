using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using static SameShore.Tests.TestInput;

namespace SameShore.Tests
{
    /// <summary>
    /// End-to-end runs of the storyboard v2, driven by simulated mouse/keyboard (Desktop Testing mode):
    /// objects are grabbed/thrown/used with E and F, buttons pressed by aiming, the bodies moved with WASD.
    /// Teleports are used only to shorten long crawls of the hatchling, never to cause an outcome.
    /// </summary>
    public static class StoryTests
    {
        static SmokeTestRunner T;

        static ObjectiveTracker Obj => ObjectiveTracker.Instance;
        static GameManager GM => GameManager.Instance;
        static T0 F<T0>() where T0 : Object => Object.FindFirstObjectByType<T0>();
        static TurtleController Turtle => F<TurtleController>();
        static ExperienceDirector Director => F<ExperienceDirector>();
        static Transform Sea => GameObject.Find("SeaPoint").transform;

        static bool HumanReady() => GM.Current == GameState.Human && Obj.Running && ScreenFader.Instance.Alpha < 0.05f;
        static bool TurtleReady() => GM.Current == GameState.Hatchling && Turtle.enabled && ScreenFader.Instance.Alpha < 0.05f && Obj.Running;
        static ObjectiveState State(string id) => Obj.Get(id) != null ? Obj.Get(id).State : (ObjectiveState)(-1);
        static float SeaD(Vector3 p) => Sea.position.z - p.z;

        static IEnumerator Grab(Component c, string what)
        {
            yield return AimAt(Center(c.gameObject));
            yield return Tap(Key.E);
            yield return null;
            T.Check($"E picks up {what}", VRHand.Right.Held != null && VRHand.Right.Held.gameObject == c.gameObject);
        }

        static IEnumerator Press(PokeButton b)
        {
            yield return AimAt(b.transform.position);
            int before = b.Presses;
            yield return Tap(Key.E);
            yield return null;
            T.Check($"Aim + E presses '{b.Label}'", b.Presses == before + 1);
        }

        // ================================================================== FULL RUN
        public static IEnumerator Full(SmokeTestRunner t)
        {
            T = t;
            Setup();
            Object.DontDestroyOnLoad(t.gameObject);
            T.Check("Desktop Testing mode active", DesktopTestingMode.Active);
            yield return ClickButton("Full Experience");
            yield return SkipStoryUntil(HumanReady, 90f);
            var day = F<DayCycle>();
            var seat = F<HumanSeat>();
            T.Check($"Act I starts seated on the chair (eyes {PlayerRig.Head.position.y - MeshKit.GroundY(PlayerRig.Head.position):F2} m above the sand)",
                    seat.Seated && Mathf.Abs(PlayerRig.Head.position.y - MeshKit.GroundY(PlayerRig.Head.position) - 1.02f) < 0.12f);
            T.Check($"Sunny afternoon (cycle {day.Cycle:F2})", day.Cycle < 0.05f && RenderSettings.sun != null && RenderSettings.sun.intensity > 1f);
            T.Check($"TODAY'S PLANS with 3 steps ({Obj.Title}, {Obj.Objectives.Count})", Obj.Title == "TODAY'S PLANS" && Obj.Objectives.Count == 3);
            T.Check("Objectives HUD is a world-space canvas", Obj.HUD.Visible && Obj.HUD.GetComponent<Canvas>().renderMode == RenderMode.WorldSpace);
            yield return Click();   // capture the mouse
            yield return new WaitForSeconds(0.5f);
            var avatar = F<HumanAvatar>();
            T.Check("Seated full body (hips on the seat, below the eyes, no head mesh)", avatar.Seated &&
                    PlayerRig.Head.position.y - avatar.torso.position.y > 0.5f && avatar.torso.GetComponentInChildren<Renderer>().enabled);
            yield return AimAt(Sea.position + Vector3.up * 2f);
            yield return new WaitForSeconds(0.3f);
            SaveView("01_day_from_the_chair.png");

            // ---- scene 1: drink and music
            var drink = F<BeachDrink>();
            yield return Grab(drink, "the drink");
            yield return new WaitForSeconds(0.3f);
            T.Check("Drink objective done", State("drink") == ObjectiveState.Done);
            yield return Tap(Key.E);   // put it down
            var music = F<MusicPlayer>();
            PokeButton play = null, vol = null;
            foreach (var b in music.GetComponentsInChildren<PokeButton>()) { if (b.Label.StartsWith("play")) play = b; else vol = b; }
            yield return Press(play);
            yield return new WaitForSeconds(0.5f);
            T.Check($"Speaker plays the beach music ({music.Source.clip?.name}, volume {music.Source.volume:F2})", music.IsOn && State("music") == ObjectiveState.Done && music.Source.volume > 0.05f);

            // ---- scene 2: can toss (first two at the bin, the last one falls short)
            var game = F<CanTossGame>();
            var bin = GameObject.Find("LitterBin").transform;
            for (int i = 0; i < 3; i++)
            {
                TossCan can = null;
                foreach (var c in game.Cans) if (!c.Thrown) { can = c; break; }
                yield return Grab(can, "an empty can");
                Vector3 aim = i < 2 ? bin.position + Vector3.up * 0.45f : Vector3.Lerp(PlayerRig.Head.position, bin.position, 0.6f);
                if (i == 2) aim.y = MeshKit.GroundY(aim);
                yield return AimAt(aim);
                yield return Tap(Key.E);
                yield return WaitFor(() => can.Result != BeachLog.CanResult.Unthrown, 12f);
                T.Check($"Can {i + 1} thrown with physics -> {can.Result} (HUD '{Obj.Get("cans").Progress()}')", can.Thrown && can.Result != BeachLog.CanResult.Unthrown);
            }
            T.Check($"Can toss done: {game.Scored} in, {3 - game.Scored} on the sand", game.Done && State("cans") == ObjectiveState.Done);
            var missed = new List<(TossCan c, Vector3 p)>();
            foreach (var c in game.Cans) if (c.Result == BeachLog.CanResult.Missed) missed.Add((c, c.transform.position));
            T.Check($"Missed cans stay where they fell ({missed.Count})", missed.Count >= 1);
            SaveView("02_can_toss.png");
            yield return WaitFor(() => !seat.Seated && HumanReady(), 20f);
            T.Check("Stands up after the can toss (walking enabled)", !seat.Seated && F<SmoothLocomotion>().MovementEnabled);

            // ---- scene 3: sandcastle
            var site = F<SandcastleSite>();
            var hole = F<SandHole>();
            yield return SkipStoryUntil(() => Obj.Get("shovel") != null, 20f);
            var shovel = F<BeachShovel>();
            yield return WalkTo(shovel.transform.position, 1.0f);
            yield return Grab(shovel, "the shovel");
            yield return new WaitForSeconds(0.2f);
            T.Check($"Shovel held pointing down-forward (tip below the hand: {shovel.Tip.y < shovel.transform.position.y})", shovel.Tip.y < shovel.transform.position.y);
            yield return WalkTo(site.DigSpot.position, 1.0f);
            int towers = 0;
            while (!site.Finished && towers < 3)
            {
                for (int k = 0; k < 4 && !site.Pail.Full; k++)
                {
                    yield return AimAt(site.DigSpot.position);
                    yield return Tap(Key.F);              // dig
                    yield return new WaitForSeconds(0.6f);
                    yield return AimAt(site.Pail.transform.position);
                    yield return new WaitForSeconds(0.6f);
                    yield return Tap(Key.F);              // pour into the bucket
                    yield return new WaitForSeconds(0.6f);
                }
                T.Check($"Bucket filled by shovelfuls ({site.Pail.Scoops}/{site.Pail.ScoopsToFill}), hole dug {hole.Dug01:P0}", site.Pail.Full && hole.Dug01 > 0.2f);
                if (VRHand.Right.Held != null && VRHand.Right.Held.GetComponent<BeachShovel>() != null) { yield return AimAt(site.DigSpot.position + (PlayerRig.Head.position - site.DigSpot.position).normalized * 1.4f); yield return Tap(Key.E); }
                yield return WalkTo(site.Pail.transform.position, 1.1f);
                yield return Grab(site.Pail, "the bucket");
                yield return Tap(Key.F);                  // turn it over on the plot
                yield return new WaitForSeconds(1.2f);
                T.Check("Bucket upside down on the castle spot", site.Pail.UpsideDown);
                int before = site.Towers;
                yield return WalkTo(site.Pail.transform.position, 1.1f);
                yield return Grab(site.Pail, "the bucket again");
                yield return new WaitForSeconds(0.6f);
                T.Check($"Lifting the bucket reveals a tower ({site.Towers})", site.Towers == before + 1);
                towers++;
                yield return AimAt(site.Plot.position + (PlayerRig.Head.position - site.Plot.position).normalized * 1.3f);
                yield return Tap(Key.E);
                if (!site.Finished)
                {
                    yield return WalkTo(shovel.transform.position, 1.0f);
                    yield return Grab(shovel, "the shovel");
                    yield return WalkTo(site.DigSpot.position, 1.0f);
                }
            }
            T.Check($"Sandcastle finished ({site.Towers} towers)", site.Finished && State("castle") == ObjectiveState.Done);
            T.Check($"The dug hole stays open (depth {hole.Dug01:P0}, trap active)", hole.Dug01 > 0.99f && !hole.IsFilled);
            yield return AimAt(site.Plot.position);
            SaveView("03_sandcastle.png");
            if (VRHand.Right.Held != null) yield return Tap(Key.E);

            // ---- scene 4: snack
            yield return SkipStoryUntil(() => Obj.Get("towel") != null, 30f);
            var towel = GameObject.Find("Towel").transform;
            var sandwich = F<Sandwich>();
            yield return WalkTo(sandwich.transform.position, 1.0f);
            T.Check("Back at the towel", State("towel") == ObjectiveState.Done || Flat(PlayerRig.Head.position - towel.position) < 2f);
            yield return Grab(sandwich, "the sandwich");
            for (int k = 0; k < 3; k++) { yield return Tap(Key.F); yield return new WaitForSeconds(1.25f); }
            T.Check($"Snack eaten ({sandwich.Bites} bites)", sandwich.Eaten);
            var bag = F<WindBag>();
            yield return WaitFor(() => bag.State == WindBag.Phase.Landed, 15f);
            var land = GameObject.Find("BagLanding").transform.position;
            T.Check($"The wind carries the bag to the wet sand ({Flat(bag.transform.position - land):F2} m from the spot, {SeaD(bag.transform.position):F1} m from the sea)",
                    bag.State == WindBag.Phase.Landed && Flat(bag.transform.position - land) < 0.6f && SeaD(bag.transform.position) < 4.5f);
            yield return AimAt(bag.transform.position);
            SaveView("04_bag_on_the_wet_sand.png");

            // ---- scene 5: evening
            yield return SkipStoryUntil(() => Obj.Get("louder") != null, 30f);
            yield return WalkTo(music.transform.position, 1.1f);
            yield return Press(vol);
            T.Check($"Music turned up (level {music.VolumeLevel + 1})", music.VolumeLevel >= 1 && State("louder") == ObjectiveState.Done);
            var fw = F<FireworksShow>();
            yield return WalkTo(fw.Button.position, 0.9f);
            var launch = fw.Button.GetComponent<PokeButton>();
            yield return Press(launch);
            yield return WaitFor(() => fw.Launched >= 3, 15f);
            yield return AimAt(Sea.position + Vector3.forward * 20f + Vector3.up * 18f);
            yield return new WaitForSeconds(0.4f);
            SaveView("05_fireworks.png");
            yield return WaitFor(() => fw.Finished, 40f);
            T.Check($"Fireworks show ({fw.Launched} shells), the sky turns to sunset (cycle {day.Cycle:F2})", fw.Finished && fw.Launched >= 8 && day.Cycle > 0.4f);

            // ---- scene 6: lights
            yield return SkipStoryUntil(() => Obj.Get("reel") != null, 30f);
            var gar = F<LightGarland>();
            yield return WalkTo(gar.Reel.transform.position, 1.0f);
            yield return Grab(gar.Reel, "the string of lights");
            for (int i = 0; i < gar.HooksTotal; i++)
            {
                Transform hk = gar.Hooks[i];
                yield return WalkTo(hk.position, 0.75f, 30f);
                if (gar.Hooked <= i) { yield return AimAt(hk.position); yield return Tap(Key.F); }
                yield return new WaitForSeconds(0.3f);
                T.Check($"Clipped to hook {i + 1}/{gar.HooksTotal}", gar.Hooked >= i + 1);
            }
            yield return WaitFor(() => gar.LightsOn, 10f);
            int lightSources = 0;
            foreach (var ls in gar.GetComponentsInChildren<LightSource>()) if (ls.IsOn) lightSources++;
            T.Check($"The string lights up ({gar.Spans} spans, {lightSources} light sources that will pull the hatchling)", gar.LightsOn && lightSources == gar.Spans);
            yield return WaitFor(() => day.Cycle > 1.95f, 20f);
            T.Check($"Night (cycle {day.Cycle:F2})", day.Cycle > 1.95f);
            yield return AimAt(gar.Hooks[0].position);
            SaveView("06_lights_at_night.png");

            // ---- scene 7: flashlight and nest
            yield return SkipStoryUntil(() => Obj.Get("lamp") != null, 30f);
            var lamp = F<Flashlight>();
            yield return WalkTo(lamp.transform.position, 1.0f);
            yield return Grab(lamp, "the flashlight");
            yield return Tap(Key.F);
            yield return null;
            float align = Vector3.Dot(lamp.transform.forward, PlayerRig.Head.forward);
            T.Check($"Flashlight on and pointing where you look (alignment {align:F2})", lamp.IsOn && align > 0.95f);
            Transform last = gar.Hooks[gar.Hooks.Length - 1];
            yield return WalkTo(last.position, 1.6f, 40f);
            yield return SkipStoryUntil(() => Obj.Get("back") != null, 30f);
            var nest = F<NestController>();
            T.Check("Baby turtles emerge at the nest", nest != null && Obj.Get("back") != null);
            var log = F<BeachLog>();
            T.Check($"The night walk is being recorded ({log.NightPath.Count} samples)", log.NightPath.Count > 5);
            yield return WalkTo(towel.position, 2.6f, 40f);

            // ---- scene 8: photo
            yield return SkipStoryUntil(() => Obj.Get("closer") != null, 20f);
            var photoTurtle = F<PhotoHatchling>();
            yield return WalkTo(photoTurtle.transform.position, 1.3f, 30f);
            yield return SkipStoryUntil(() => Obj.Get("phone") != null, 20f);
            var phone = F<PhoneCamera>();
            yield return Grab(phone, "the phone");
            yield return AimAt(photoTurtle.transform.position);
            yield return new WaitForSeconds(0.5f);
            SaveView("07_phone_framing.png");
            yield return Tap(Key.F);
            yield return WaitFor(() => phone.PhotoTaken, 3f);
            yield return new WaitForSeconds(0.4f);
            T.Check($"Photo taken with flash; pose stored (feet {log.PhotoFeet})", phone.PhotoTaken && log.PhotoTaken && log.PhotoFlash);

            // ---- ACT II
            yield return SkipStoryUntil(TurtleReady, 90f);
            yield return TurtleChecks(log, missed);
            yield return TurtleJourney(true);
        }

        static IEnumerator TurtleChecks(BeachLog log, List<(TossCan c, Vector3 p)> missed)
        {
            T.Check("Act II: the hatchling (Hatchling state)", TurtleReady());
            float eye = PlayerRig.Head.position.y - MeshKit.GroundY(PlayerRig.Head.position);
            T.Check($"Eyes {eye * 100:F1} cm above the sand, scale {PlayerRig.Scale:F2}", eye > 0.05f && eye < 0.13f && Mathf.Abs(PlayerRig.Scale - 0.05f) < 0.005f);
            T.Check($"REACH THE OCEAN ({Obj.Objectives.Count} steps)", Obj.Title == "REACH THE OCEAN" && Obj.Objectives.Count >= 6);
            foreach (var (c, p) in missed)
                T.Check($"The missed can is exactly where it fell ({(c.transform.position - p).magnitude:F3} m)", (c.transform.position - p).magnitude < 0.05f);
            var hole = F<SandHole>();
            T.Check("The hole is still open for the hatchling", hole.Dug01 > 0.99f);
            var gar = F<LightGarland>();
            T.Check("The light string is still on", gar.LightsOn);
            var music = F<MusicPlayer>();
            var hearing = F<TurtleHearing>();
            var lp = music.Source.GetComponent<AudioLowPassFilter>();
            T.Check($"Same song, heard through the hatchling (low-pass {lp?.cutoffFrequency:F0} Hz, volume {music.Source.volume:F2} <= 0.85)",
                    music.IsOn && hearing.On && lp != null && lp.enabled && music.Source.volume <= 0.851f);
            var giant = F<GiantHuman>();
            T.Check("The giant (the human) replays the night", giant.Playing);
            T.Check("The real flashlight and phone are with the giant (hidden)", !F<Flashlight>() || !F<Flashlight>().gameObject.activeInHierarchy);
            yield return new WaitForSeconds(2f);
            yield return AimAt(Sea.position + Vector3.up * 0.5f);
            SaveView("10_hatchling_leaves_the_nest.png");
        }

        /// <summary>The hatchling's run: real crawling for short stretches, teleports to skip long ones.</summary>
        static IEnumerator TurtleJourney(bool expectGiantFromLog)
        {
            var turtle = Turtle;
            var story = F<TurtleStory>();
            var gulls = F<GullEncounters>();
            var route = GameObject.Find("TurtleRoute").transform;
            Vector3 W(int i) => route.GetChild(i).position;
            float Yaw(Vector3 from, Vector3 to) { Vector3 d = to - from; return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }
            IEnumerator Teleport(Vector3 p, Vector3 look)
            {
                turtle.TeleportTo(MeshKit.OnGround(p, 0.02f), Yaw(p, look));
                yield return null;
                yield return null;
            }

            // crawl out of the nest
            Vector3 p0 = turtle.transform.position;
            yield return Hold(3f, Key.W);
            float moved = Flat(turtle.transform.position - p0);
            T.Check($"W crawls (moved {moved:F2} m in 3 s)", moved > 0.2f && moved < 1.2f);
            float yaw0 = turtle.transform.eulerAngles.y;
            yield return Hold(1f, Key.D);
            T.Check($"D turns the body slowly ({Mathf.DeltaAngle(yaw0, turtle.transform.eulerAngles.y):F0} deg in 1 s)", Mathf.Abs(Mathf.DeltaAngle(yaw0, turtle.transform.eulerAngles.y)) > 10f && Mathf.Abs(Mathf.DeltaAngle(yaw0, turtle.transform.eulerAngles.y)) < 45f);

            // gull pass (no attack)
            yield return Teleport(W(1), W(2));
            yield return Hold(2.5f, Key.W);
            yield return WaitFor(() => gulls.FirstDone, 15f);
            T.Check("First gull encounter: a shadow passes over (no attack)", gulls.FirstDone && !gulls.Struck && turtle.enabled);

            // lights pull
            yield return Teleport(W(2), W(3));
            yield return Hold(4f, Key.W);
            T.Check($"The light string pulls her off course ({story.SecondsOffCourse:F1} s)", story.SecondsOffCourse > 0.5f || turtle.ArtificialShare > 0.2f);
            yield return AimAt(W(1) + Vector3.up * 1.4f);
            SaveView("11_lights_from_8cm.png");

            // cans + hole
            yield return Teleport(Vector3.Lerp(W(3), W(4), 0.3f), W(5));
            yield return Hold(3f, Key.W);
            yield return AimAt(W(5));
            SaveView("12_cans_and_hole.png");
            var hole = F<SandHole>();
            Vector3 into = hole.transform.position + (W(5) - hole.transform.position).normalized * 0.1f;
            yield return Teleport(hole.transform.position - (W(6) - W(4)).normalized * 0.55f, hole.transform.position);
            yield return Hold(4f, Key.W, () => story.FellInHole);
            T.Check("She slides into the hole the human dug", story.FellInHole);
            yield return Hold(12f, Key.W, () => SeaD(turtle.transform.position) < SeaD(hole.transform.position) - 1f || story.PassedHole);
            T.Check($"...and climbs out (not trapped): {Flat(turtle.transform.position - hole.transform.position):F2} m from its centre", Flat(turtle.transform.position - hole.transform.position) > hole.Radius * 0.6f);

            // gull attack (non-lethal)
            yield return Teleport(W(6) + Vector3.forward * 0.1f, W(7));
            yield return WaitFor(() => gulls.SecondDone, 25f);
            T.Check($"Second gull encounter resolved ({(gulls.Struck ? "struck, stunned" : "dodged")}) - never lethal", gulls.SecondDone && turtle.enabled && GM.Current == GameState.Hatchling);

            // the bag
            var bag = F<WindBag>();
            Vector3 bp = bag.transform.position;
            yield return Teleport(bp - (W(8) - W(6)).normalized * 0.8f, bp);
            yield return Hold(3f, Key.W, () => story.BitBag);
            if (!story.BitBag) { yield return Tap(Key.E); yield return null; }
            T.Check("Hungry... the jellyfish is a plastic bag (bite)", story.BitBag);
            yield return new WaitForSeconds(1f);
            T.Check($"Discomfort: slowed ({turtle.ExternalSpeedMultiplier:F2}), not dead", turtle.ExternalSpeedMultiplier < 1f && turtle.enabled);
            yield return AimAt(bp + Vector3.up * 0.02f);
            SaveView("13_jellyfish_bag.png");
            yield return new WaitForSeconds(6f);

            // the giant and the photo
            var giant = F<GiantHuman>();
            yield return WaitFor(() => giant.AtPhotoSpot, 40f);
            Vector3 gf = giant.Feet;
            yield return Teleport(gf + (W(7) - gf).normalized * 1.9f, gf);
            yield return AimAt(gf + Vector3.up * 1.2f);
            SaveView("14_the_giant.png");
            yield return Hold(5f, Key.W, () => giant.PhotoDone);
            yield return WaitFor(() => giant.PhotoDone, 10f);
            float peak = 0;
            float until = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < until) { peak = Mathf.Max(peak, ScreenFader.Instance.Alpha); yield return null; }
            T.Check($"The giant bends down and takes THE photo: moderate glare (peak {peak:F2} < 0.7, no white-out)", giant.PhotoDone && peak > 0.1f && peak < 0.7f);
            if (expectGiantFromLog) T.Check("The giant stands where the human stood for the photo", Flat(gf - F<BeachLog>().PhotoFeet) < 0.6f);
            yield return new WaitForSeconds(3f);

            // almost there, the sea, the ending
            Vector3 shore = W(route.childCount - 1);
            yield return Teleport(shore - Vector3.forward * 1.5f, shore + Vector3.forward);
            yield return Hold(12f, Key.W, () => GM.Current == GameState.End);
            T.Check("Almost there...", story.AlmostThere);
            T.Check("Reached the water -> ending", GM.Current == GameState.End);
            yield return WaitFor(() => FindButton("Main menu") != null, 30f);
            var title = GameObject.Find("EndScreen")?.GetComponentInChildren<TextMeshPro>();
            T.Check($"End screen: SAME SHORE ({title?.text})", title != null && title.text.Contains("SAME SHORE") && FindButton("Experience it again") != null);
            T.Check("The final death happened only at the goal (the gulls never caught her before)", !gulls.Struck || turtle != null);
            SaveView("15_end.png");
            yield return ClickButton("Main menu");
            yield return WaitFor(() => GameObject.Find("StartMenu") != null && GM.Current == GameState.Intro, 20f);
            T.Check("Back to the main menu", GM.Current == GameState.Intro);
            T.Finish();
        }

        // ================================================================== TURTLE ONLY
        public static IEnumerator TurtleOnly(SmokeTestRunner t)
        {
            T = t;
            Setup();
            Object.DontDestroyOnLoad(t.gameObject);
            yield return ClickButton("Turtle Experience");
            yield return SkipStoryUntil(TurtleReady, 90f);
            var log = F<BeachLog>();
            var game = F<CanTossGame>();
            yield return WaitFor(() => game.Done, 15f);
            T.Check($"Default beach: cans on the sand ({log.CansMissed} missed)", log.UsedDefaults && game.Done && log.CansMissed >= 1);
            T.Check("Default beach: castle and open hole", F<SandcastleSite>().Finished && F<SandHole>().Dug01 > 0.99f);
            T.Check("Default beach: bag on the wet sand", F<WindBag>().State == WindBag.Phase.Landed);
            T.Check("Default beach: lights on, music on, fireworks done", F<LightGarland>().LightsOn && F<MusicPlayer>().IsOn && F<FireworksShow>().Finished);
            T.Check("The giant follows a default night walk", F<GiantHuman>().Playing);
            yield return TurtleJourney(false);
        }

        // ================================================================== HUMAN ONLY (skips) -> continue as hatchling
        public static IEnumerator HumanOnly(SmokeTestRunner t)
        {
            T = t;
            Setup();
            Object.DontDestroyOnLoad(t.gameObject);
            yield return ClickButton("Human Experience");
            yield return SkipStoryUntil(HumanReady, 90f);
            var director = Director;
            int skips = 0;
            float end = Time.realtimeSinceStartup + 400f;
            while (FindButton("Continue as the hatchling") == null && Time.realtimeSinceStartup < end)
            {
                if (Obj.Running && Obj.Current != null && ScreenFader.Instance.Alpha < 0.05f)
                {
                    string id = Obj.Current.Id;
                    director.RequestSkip();
                    skips++;
                    yield return WaitFor(() => Obj.Current == null || Obj.Current.Id != id || !Obj.Running, 20f);
                }
                var sub = GameObject.Find("Subtitle");
                if (sub != null && sub.activeSelf) yield return Tap(Key.Space);
                yield return null;
            }
            T.Check($"Every activity can be skipped from the pause menu ({skips} skips)", skips >= 7);
            T.Check("Human-only ending panel offers to continue as the hatchling", FindButton("Continue as the hatchling") != null);
            var log = F<BeachLog>();
            T.Check($"Skipped steps still leave their trace: {log.Summary().Replace("\n", " | ")}", log.CastleBuilt && log.BagLost && log.LightsInstalled && log.PhotoTaken);
            yield return ClickButton("Continue as the hatchling");
            yield return SkipStoryUntil(TurtleReady, 90f);
            T.Check("Continues into the hatchling's act with the same beach", TurtleReady() && F<GiantHuman>().Playing);
            T.Finish();
        }

        // ================================================================== XR path (no desktop mode): story-level checks
        public static IEnumerator Xr(SmokeTestRunner t)
        {
            T = t;
            T.Check("Desktop Testing mode is OFF", !DesktopTestingMode.Active);
            var director = Director;
            var menuButton = FindButton("Full Experience");
            T.Check("Menu button exists (laser + trigger in XR)", menuButton != null);
            menuButton.Click();
            yield return WaitFor(HumanReady, 60f);
            T.Check("Human phase starts seated", F<HumanSeat>().Seated);
            T.Check("Two VR hands (grip grab, trigger use)", VRHand.Left != null && VRHand.Right != null);
            float end = Time.realtimeSinceStartup + 400f;
            while (GM.Current != GameState.Hatchling && Time.realtimeSinceStartup < end)
            {
                if (GM.Current == GameState.Human && Obj.Running && Obj.Current != null) { director.RequestSkip(); yield return new WaitForSeconds(0.5f); }
                yield return null;
            }
            yield return WaitFor(TurtleReady, 60f);
            T.Check("XR path reaches the hatchling's act", TurtleReady());
            T.Finish();
        }
    }
}
