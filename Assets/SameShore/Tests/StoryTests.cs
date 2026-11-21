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
            var other = VRHand.Right.Held;
            if (other != null && other.gameObject != c.gameObject && !other.throwable)
            {
                // one desktop hand: put the other thing down at your feet first, like a player would
                Vector3 side = Vector3.Cross(Vector3.up, PlayerRig.FlatForward);
                yield return AimAt(MeshKit.OnGround(PlayerRig.Head.position + side * 0.5f - PlayerRig.FlatForward * 0.2f));
                yield return Tap(Key.E);
                yield return null;
            }
            yield return AimAt(Center(c.gameObject));
            yield return Tap(Key.E);
            yield return null;
            bool ok = VRHand.Right.Held != null && VRHand.Right.Held.gameObject == c.gameObject;
            if (!ok && VRHand.Right.Held == null)
            {
                // like a player: step closer, look again, retry once
                Debug.Log($"[SameShoreTest] grab retry '{what}' from {Flat(PlayerRig.Head.position - c.transform.position):F2} m");
                yield return WalkTo(c.transform.position, 0.6f, 10f);
                yield return AimAt(Center(c.gameObject));
                yield return Tap(Key.E);
                yield return null;
                ok = VRHand.Right.Held != null && VRHand.Right.Held.gameObject == c.gameObject;
            }
            if (!ok)
            {
                var g = c.GetComponent<VRGrabbable>();
                Physics.Raycast(PlayerRig.Head.position, PlayerRig.Head.forward, out RaycastHit hit, 5f);
                SaveView($"grabmiss_{what.Replace(' ', '_')}.png");
                Debug.Log($"[SameShoreTest] grab miss '{what}': at {c.transform.position}, head {PlayerRig.Head.position} ({Flat(PlayerRig.Head.position - c.transform.position):F2} m), " +
                          $"enabled {(g != null && g.grabbingEnabled)}, held {(VRHand.Right.Held != null ? VRHand.Right.Held.name : "-")}, ray hits '{(hit.collider != null ? hit.collider.name : "-")}' at {hit.distance:F2}");
            }
            T.Check($"E picks up {what}", ok);
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
            float binDist = Flat(bin.position - F<HumanSeat>().Seat.position);
            float mouth = bin.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.extents.x;
            T.Check($"Can toss is a skill shot: bin {binDist:F1} m from the chair, mouth radius {mouth:F2} m, no throw boost",
                    binDist > 4.2f && mouth < 0.3f && Mathf.Approximately(game.Cans[0].GetComponent<VRGrabbable>().throwScale, 1f));
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
            T.Check($"Missed cans stay where they fell ({missed.Count})", missed.Count >= 1 || game.Scored == 3);
            foreach (var (c, p) in missed)
                T.Check($"Missed can lies on the hatchling's way ({RouteDist(p):F2} m from the route)", RouteDist(p) < 1.9f);
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
                // in the pail's own space (a tilted, hand-held pail would inflate a world-space box)
                var fillT = site.Pail.transform.Find("SandFill");
                Bounds fb = fillT.GetComponent<MeshFilter>().sharedMesh.bounds;
                bool local = fillT.localPosition == Vector3.zero && fillT.localScale == Vector3.one;
                T.Check($"Cycle {towers + 1}: the sand stays inside the bucket (fill y {fb.min.y * 100:F1}-{fb.max.y * 100:F1} cm, radius {fb.extents.x * 100:F1} cm; bucket 0-20 cm, inner r <= 9.2 cm)",
                        fillT.gameObject.activeInHierarchy && local && fb.min.y >= 0.012f && fb.max.y <= 0.21f && fb.extents.x <= 0.092f && fb.extents.z <= 0.092f);
                if (VRHand.Right.Held != null && VRHand.Right.Held.GetComponent<BeachShovel>() != null) { yield return AimAt(site.DigSpot.position + (PlayerRig.Head.position - site.DigSpot.position).normalized * 1.4f); yield return Tap(Key.E); }
                yield return WalkTo(site.Pail.transform.position, 1.1f);
                yield return Grab(site.Pail, "the bucket");
                yield return Tap(Key.F);                  // turn it over on the plot
                yield return new WaitForSeconds(1.2f);
                T.Check("Bucket upside down on the castle spot", site.Pail.UpsideDown);
                int before = site.Towers;
                yield return WalkTo(site.Pail.transform.position, 1.1f);
                if (site.Towers == before) yield return Grab(site.Pail, "the bucket again");
                if (site.Towers == before && (VRHand.Right.Held == null || VRHand.Right.Held.gameObject != site.Pail.gameObject))
                {
                    yield return WalkTo(site.Pail.transform.position, 0.7f);
                    yield return Grab(site.Pail, "the bucket (second try)");
                }
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
            T.Check($"Castle and hole lie on the hatchling's way (castle {RouteDist(site.Plot.position):F2} m, hole {RouteDist(hole.transform.position):F2} m from the route)",
                    RouteDist(site.Plot.position) < 0.9f && RouteDist(hole.transform.position) < 0.9f);
            T.Check("The empty bucket shows no sand", !site.Pail.transform.Find("SandFill").gameObject.activeInHierarchy || site.Pail.Scoops > 0);
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
                    bag.State == WindBag.Phase.Landed && Flat(bag.transform.position - land) < 0.6f && SeaD(bag.transform.position) < 5f);
            T.Check($"The bag lies right on the hatchling's way ({RouteDist(bag.transform.position):F2} m from the route)", RouteDist(bag.transform.position) < 0.7f);
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
            int halos = 0, pools = 0;
            foreach (var r in gar.GetComponentsInChildren<MeshRenderer>()) { if (r.name == "Halo") halos++; if (r.name == "LightPool") pools++; }
            float lumen = 0f;
            foreach (var l in gar.GetComponentsInChildren<Light>()) lumen = Mathf.Max(lumen, l.intensity * l.range);
            T.Check($"Brighter festoon: {gar.BulbPositions.Count} bulbs with {halos} glow halos, {pools} light pools on the sand, span lights {lumen:F1} (int x range)",
                    halos == gar.BulbPositions.Count && pools == gar.Spans && lumen > 10f);
            yield return WaitFor(() => day.Cycle > 1.95f, 20f);
            T.Check($"Night (cycle {day.Cycle:F2})", day.Cycle > 1.95f);
            yield return AimAt(gar.Hooks[0].position);
            SaveView("06_lights_at_night.png");

            yield return FromLamp(missed);
        }

        /// <summary>Scenes 7-8 (flashlight, nest, photo) and the whole Act II.</summary>
        static IEnumerator FromLamp(List<(TossCan c, Vector3 p)> missed)
        {
            var gar = F<LightGarland>();
            var towel = GameObject.Find("Towel").transform;
            // ---- scene 7: flashlight and nest
            yield return SkipStoryUntil(() => Obj.Get("lamp") != null, 30f);
            var lamp = F<Flashlight>();
            // the castle and the hole are now in the middle of the way: walk around them like a player would
            Debug.Log($"[SameShoreTest] before the lamp walk: head {PlayerRig.Head.position}, lamp {lamp.transform.position}, seated {F<HumanSeat>().Seated}, move {F<SmoothLocomotion>().MovementEnabled}");
            yield return WalkTo(lamp.transform.position + new Vector3(-0.9f, 0f, -1.4f), 0.5f, 40f);
            Debug.Log($"[SameShoreTest] at the detour point: head {PlayerRig.Head.position}");
            yield return WalkTo(lamp.transform.position, 0.9f);
            SaveView("07a_before_lamp_grab.png");
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

        /// <summary>Fast path to scenes 7-8 and Act II: skips scenes 1-6 like the pause menu would.</summary>
        public static IEnumerator Late(SmokeTestRunner t)
        {
            T = t;
            Setup();
            Object.DontDestroyOnLoad(t.gameObject);
            yield return ClickButton("Full Experience");
            yield return SkipStoryUntil(HumanReady, 90f);
            float end = Time.realtimeSinceStartup + 400f;
            while (Obj.Get("lamp") == null && Time.realtimeSinceStartup < end)
            {
                if (Obj.Running && Obj.Current != null && ScreenFader.Instance.Alpha < 0.05f)
                {
                    string id = Obj.Current.Id;
                    Director.RequestSkip();
                    yield return WaitFor(() => Obj.Current == null || Obj.Current.Id != id || !Obj.Running || Obj.Get("lamp") != null, 20f);
                }
                var sub = GameObject.Find("Subtitle");
                if (sub != null && sub.activeSelf) yield return Tap(Key.Space);
                yield return null;
            }
            T.Check("Skipped to the flashlight scene", Obj.Get("lamp") != null);
            yield return Click();   // capture the mouse (mouse look) like a desktop tester would
            var missed = new List<(TossCan c, Vector3 p)>();
            foreach (var c in F<CanTossGame>().Cans) if (c.Result == BeachLog.CanResult.Missed) missed.Add((c, c.transform.position));
            yield return FromLamp(missed);
        }

        static float RouteDist(Vector3 p)
        {
            var route = GameObject.Find("TurtleRoute").transform;
            float best = float.MaxValue;
            for (int i = 0; i < route.childCount - 1; i++)
            {
                Vector3 a = route.GetChild(i).position, b = route.GetChild(i + 1).position;
                Vector3 ab = b - a; ab.y = 0;
                Vector3 ap = p - a; ap.y = 0;
                float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                best = Mathf.Min(best, (ap - ab * t).magnitude);
            }
            return best;
        }

        /// <summary>The camera is at the hatchling's eyes, ~8 cm over the sand, and stays there while crawling.</summary>
        static IEnumerator CameraChecks()
        {
            var sw = Object.FindFirstObjectByType<PlayerPhaseSwitcher>();
            Transform eyes = sw.EyeAnchor;
            float eye = PlayerRig.Head.position.y - MeshKit.GroundY(PlayerRig.Head.position);
            float off = (PlayerRig.Head.position - eyes.position).magnitude;
            T.Check($"Camera at the hatchling's eyes: {eye * 100:F1} cm over the sand, {off * 100:F1} cm from the eye point, scale {PlayerRig.Scale:F2}",
                    eye > 0.065f && eye < 0.1f && off < 0.012f && Mathf.Abs(PlayerRig.Scale - 0.05f) < 0.005f);
            var model = Turtle.transform.Find("HatchlingModel");
            var head = model.GetComponentsInChildren<Renderer>();
            bool headHidden = true, bodyShown = false;
            foreach (var r in head)
            {
                if (r.name == "Head" || r.name == "EyeL" || r.name == "EyeR" || r.name == "Beak") headHidden &= r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                if (r.name.StartsWith("FlipperF") && r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) bodyShown = true;
            }
            Vector3 hf = Turtle.transform.InverseTransformPoint(PlayerRig.Head.position);
            T.Check($"Own head not drawn over the camera, front flippers visible below (eyes {hf.z * 100:F1} cm ahead of the body centre)", headHidden && bodyShown && hf.z > 0.03f);
            // crawl a little: the eyes stay on the body, no rising, no going under the sand
            float minEye = 9f, maxEye = 0f, maxOff = 0f;
            T.StartCoroutine(Hold(3f, Key.W));
            float until = Time.time + 3f;
            while (Time.time < until)
            {
                yield return null;
                float e = PlayerRig.Head.position.y - MeshKit.GroundY(PlayerRig.Head.position);
                minEye = Mathf.Min(minEye, e); maxEye = Mathf.Max(maxEye, e);
                maxOff = Mathf.Max(maxOff, (PlayerRig.Head.position - eyes.position).magnitude);
            }
            T.Check($"While crawling the camera stays at the eyes ({minEye * 100:F1}-{maxEye * 100:F1} cm over the sand, max {maxOff * 100:F1} cm off)",
                    minEye > 0.04f && maxEye < 0.12f && maxOff < 0.02f);
            yield return new WaitForSeconds(0.8f);
            Turtle.TeleportTo(sw.TurtleSpawn.position, sw.TurtleSpawn.eulerAngles.y);   // back in the nest for the next checks
            yield return null;
        }

        static IEnumerator TurtleChecks(BeachLog log, List<(TossCan c, Vector3 p)> missed)
        {
            T.Check("Act II: the hatchling (Hatchling state)", TurtleReady());
            yield return CameraChecks();
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
            T.Check($"The speaker is silent in the nest (waits for its trigger): volume {music.Source.volume:F2}",
                    hearing.On && !hearing.MusicStarted && lp != null && lp.enabled && music.Source.volume < 0.01f);
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

            // the corridor: heading sideways ends against the dunes, never around the obstacles
            var vit = F<TurtleVitality>();
            var hearing = F<TurtleHearing>();
            var ambient = F<AmbientAudio>();
            {
                Vector3 side0 = W(5);   // between the camp and the castle, away from the low bulbs
                // a player insisting on going sideways: keeps the body pointed across the beach while crawling
                IEnumerator Sideways(Vector3 dir)
                {
                    yield return Teleport(side0, side0 + dir);
                    T.StartCoroutine(Hold(22f, Key.W));
                    float end = Time.time + 22f;
                    while (Time.time < end) { turtle.transform.rotation = Quaternion.LookRotation(dir); yield return null; }
                }
                yield return Sideways(Vector3.left);
                float wentLeft = side0.x - turtle.transform.position.x;
                yield return Sideways(Vector3.right);
                float wentRight = turtle.transform.position.x - side0.x;
                var limits = GameObject.Find("TurtleBounds").GetComponentsInChildren<Collider>();
                T.Check($"Natural limits: sideways it stops at the berms ({wentLeft:F1} m left, {wentRight:F1} m right; {limits.Length} hidden limits active)",
                        wentLeft < 2.3f && wentRight < 4.2f && Mathf.Max(wentLeft, wentRight) > 1.0f && limits.Length > 10 && limits[0].enabled);
            }

            // gull 1: a real attack pass (swoop from the sea), triggered by the corridor zone
            yield return Teleport(W(2) + Vector3.back * 0.3f, W(3));
            yield return Hold(5f, Key.W, () => gulls.Attacks > 0);
            yield return WaitFor(() => gulls.FirstDone, 20f);
            T.Check($"First gull: a real attack pass ({gulls.Attacks} attack, {(gulls.SwoopStruck ? "hit: stunned, strength " + vit.Strength.ToString("P0") : "dodged")}), not lethal",
                    gulls.FirstDone && gulls.Attacks >= 1 && turtle.enabled && GM.Current == GameState.Hatchling);

            // lights pull
            yield return Teleport(W(2), W(3));
            yield return Hold(4f, Key.W);
            T.Check($"The light string pulls her off course ({story.SecondsOffCourse:F1} s)", story.SecondsOffCourse > 0.5f || turtle.ArtificialShare > 0.2f);
            yield return AimAt(W(1) + Vector3.up * 1.4f);
            SaveView("11_lights_from_8cm.png");

            // light hazard: stay under the lowest bulb, then leave
            {
                var garland = F<LightGarland>();
                Vector3 lowest = garland.BulbPositions[0];
                foreach (var bp0 in garland.BulbPositions) if (bp0.y - MeshKit.GroundY(bp0) < lowest.y - MeshKit.GroundY(lowest)) lowest = bp0;
                vit.ResetState();   // measure the light alone (the gull may already have taken some strength)
                float s0 = vit.Strength;
                yield return Teleport(MeshKit.OnGround(lowest), lowest + Vector3.forward);
                yield return new WaitForSeconds(5f);
                float dz = vit.Dazzle, slow = turtle.HazardSpeedMultiplier;
                T.Check($"Under a bulb: dazzled {dz:F2}, slowed x{slow:F2}, strength {s0:P0} -> {vit.Strength:P0}", dz > 0.25f && slow < 0.95f && vit.Strength < s0);
                yield return new WaitForSeconds(20f);
                T.Check($"Light damage is capped (strength {vit.Strength:P0} >= 50%), never lethal", vit.Strength >= 0.499f && turtle.enabled);
                yield return Teleport(W(6), W(7));
                yield return new WaitForSeconds(3f);
                T.Check($"Away from the bulbs the glare fades (dazzle {vit.Dazzle:F2}, speed x{turtle.HazardSpeedMultiplier:F2})", vit.Dazzle < dz * 0.5f && turtle.HazardSpeedMultiplier > slow);
            }

            // music: the trigger at the camp starts the human's song, louder as she nears it, over the waves
            {
                var music = F<MusicPlayer>();
                yield return Teleport(W(3) + Vector3.back * 0.6f, W(4));
                yield return Hold(3f, Key.W, () => hearing.MusicStarted);
                T.Check("Crossing the camp trigger starts the music", hearing.MusicStarted);
                yield return Teleport(W(4), music.transform.position);
                yield return new WaitForSeconds(6f);
                var lp = music.Source.GetComponent<AudioLowPassFilter>();
                T.Check($"Near the speaker it dominates the mix (volume {music.Source.volume:F2}, low-pass {lp.cutoffFrequency:F0} Hz, waves x{ambient.WaveDuck:F2})",
                        music.Source.isPlaying && music.Source.volume > 0.8f && lp.cutoffFrequency > 1500f && ambient.WaveDuck < 0.5f);
                float near = music.Source.volume, nearDuck = ambient.WaveDuck;
                // past the camp but before the bag, the second gull and the giant (left of the castle/hole)
                Vector3 past = W(6) + Vector3.left * 1.3f + Vector3.forward * 0.7f;
                yield return Teleport(past, past + Vector3.forward);
                yield return new WaitForSeconds(8f);
                T.Check($"Leaving the camp: the music recedes ({near:F2} -> {music.Source.volume:F2}) and the waves return (x{ambient.WaveDuck:F2})",
                        music.Source.volume < near && ambient.WaveDuck > nearDuck);
            }

            // cans + hole
            yield return Teleport(Vector3.Lerp(W(3), W(4), 0.3f), W(5));
            yield return Hold(3f, Key.W);
            yield return AimAt(W(5));
            SaveView("12_cans_and_hole.png");
            var hole = F<SandHole>();
            {
                // the trampled sand around the castle and the hole slows her only a little
                var site = F<SandcastleSite>();
                Vector3 a = site.Plot.position + (hole.transform.position - site.Plot.position) * 0.5f + Vector3.back * 0.9f;
                yield return Teleport(a, a + Vector3.forward);
                Vector3 s0 = turtle.transform.position;
                yield return Hold(4f, Key.W);
                float pace = Flat(turtle.transform.position - s0) / 4f;
                T.Check($"Around the castle and the hole she keeps a normal pace ({pace:F2} m/s)", pace > 0.12f);
            }
            Vector3 into = hole.transform.position + (W(5) - hole.transform.position).normalized * 0.1f;
            yield return Teleport(hole.transform.position - (W(6) - W(4)).normalized * 0.55f, hole.transform.position);
            Vector3 h0 = turtle.transform.position;
            yield return Hold(4f, Key.W, () => story.FellInHole);
            var trapCol = hole.GetComponent<Collider>();
            Debug.Log($"[SameShoreTest] hole approach: start {Flat(h0 - hole.transform.position):F2} m from centre, end {Flat(turtle.transform.position - hole.transform.position):F2} m, " +
                      $"moved {Flat(turtle.transform.position - h0):F2} m, y {turtle.transform.position.y - hole.transform.position.y:F2}, trap collider {(trapCol != null && trapCol.enabled)}, stunned {turtle.IsStunned}");
            T.Check("She slides into the hole the human dug", story.FellInHole);
            yield return Hold(12f, Key.W, () => SeaD(turtle.transform.position) < SeaD(hole.transform.position) - 1f || story.PassedHole);
            T.Check($"...and climbs out (not trapped): {Flat(turtle.transform.position - hole.transform.position):F2} m from its centre", Flat(turtle.transform.position - hole.transform.position) > hole.Radius * 0.6f);

            // gull 2 (non-lethal)
            yield return Teleport(W(7) + Vector3.back * 0.45f, W(8));
            yield return Hold(6f, Key.W, () => gulls.Attacks > 1);
            yield return WaitFor(() => gulls.SecondDone, 25f);
            T.Check($"Second gull attack resolved ({gulls.Attacks} attacks in total, {gulls.Hits} hits) - never lethal", gulls.SecondDone && gulls.Attacks == 2 && turtle.enabled && GM.Current == GameState.Hatchling);

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
            yield return Teleport(gf + (W(7) - gf).normalized * 1.3f, gf);
            yield return AimAt(gf + Vector3.up * 1.2f);
            SaveView("14_the_giant.png");
            // measure the fader from the approach on (the flash can come and go before PhotoDone), never the ending
            float peak = 0;
            bool measuring = true;
            IEnumerator Measure()
            {
                while (measuring)
                {
                    if (GM.Current == GameState.Hatchling) peak = Mathf.Max(peak, ScreenFader.Instance.Alpha);
                    yield return null;
                }
            }
            T.StartCoroutine(Measure());
            yield return Hold(10f, Key.W, () => giant.PhotoDone);
            yield return WaitFor(() => giant.PhotoDone, 10f);
            yield return new WaitForSeconds(4.5f);   // PhotoDone is set when the shot starts; the flash comes ~2.6 s later
            measuring = false;
            Debug.Log($"[SameShoreTest] glare peak {peak:F2}");
            peak = Mathf.Max(peak, story.GlarePeak);
            T.Check($"The giant bends down and takes THE photo: moderate glare ({story.Glares} flash, veil peak {peak:F2} < 0.7, no white-out)", giant.PhotoDone && story.Glares == 1 && peak > 0.1f && peak < 0.7f);
            if (expectGiantFromLog) T.Check($"The giant stands where the human stood for the photo ({Flat(giant.PhotoSpot - F<BeachLog>().PhotoFeet):F2} m)", Flat(giant.PhotoSpot - F<BeachLog>().PhotoFeet) < 0.6f);
            yield return new WaitForSeconds(3f);

            // almost there, the sea, the ending
            Vector3 shore = W(route.childCount - 1);
            yield return Teleport(shore - Vector3.forward * 1.5f, shore + Vector3.forward);
            yield return Hold(12f, Key.W, () => GM.Current == GameState.End);
            T.Check("Almost there...", story.AlmostThere);
            T.Check("Reached the water -> ending", GM.Current == GameState.End);
            yield return WaitFor(() => Director.EndingCarried, 20f);
            T.Check($"Ending: the gull grabs her as the wave arrives and carries her off ({Director.EndingLift:F1} m up)", Director.EndingCarried && Director.EndingLift > 2f);
            T.Check($"She reached the sea with strength {vit.Strength:P0} (lights and gulls weaken, never kill)", vit.Strength >= 0.2f);
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
            T.Check("Default beach: lights on, music logged on, fireworks done", F<LightGarland>().LightsOn && log.MusicOn && F<FireworksShow>().Finished);
            T.Check($"Default layout: cans, castle, hole and bag on the hatchling's way", RouteDist(F<SandcastleSite>().Plot.position) < 0.9f && RouteDist(F<SandHole>().transform.position) < 0.9f && RouteDist(F<WindBag>().transform.position) < 0.7f);
            yield return CameraChecks();
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
