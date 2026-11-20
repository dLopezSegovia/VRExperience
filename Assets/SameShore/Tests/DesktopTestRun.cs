using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;

namespace SameShore.Tests
{
    /// <summary>
    /// Desktop Testing mode walkthrough driven ONLY by simulated mouse and keyboard devices:
    /// menu buttons are hovered and clicked through the EventSystem, the camera is aimed with mouse
    /// deltas, objects are grabbed with E / used with F, the human walks with W and the hatchling
    /// crawls with W. It also returns to the main menu (scene reload) and picks another mode.
    /// </summary>
    public static class DesktopTestRun
    {
        static SmokeTestRunner _t;
        static Keyboard _kb;
        static Mouse _mouse;
        static readonly System.Collections.Generic.List<Key> _keys = new System.Collections.Generic.List<Key>();
        static Vector2 _mousePos;
        static bool _lmb, _rmb;

        public static IEnumerator Run(SmokeTestRunner t)
        {
            _t = t;
            Object.DontDestroyOnLoad(t.gameObject);   // survives "Main menu" (scene reload)
            Time.timeScale = 1f;
            SetupDevices();
            yield return null;

            // ---------------------------------------------------------------- detection & EventSystem
            t.Check("Desktop mode auto-activated in the Editor without an XR device", DesktopTestingMode.Active);
            var es = EventSystem.current;
            var module = es != null ? es.GetComponent<InputSystemUIInputModule>() : null;
            var cam = PlayerRig.HeadCamera;
            t.Check($"EventSystem found/created ({(es != null ? es.name : "none")}) with an enabled InputSystemUIInputModule",
                    es != null && module != null && module.enabled && es.currentInputModule == module);
            t.Check("PhysicsRaycaster on the head camera", cam != null && cam.GetComponent<PhysicsRaycaster>() != null);
            t.Check($"Desktop camera at human eye height ({PlayerRig.Head.position.y - PlayerRig.Root.position.y:F2} m)",
                    Mathf.Abs(PlayerRig.Head.position.y - PlayerRig.Root.position.y - 1.65f) < 0.05f);
            t.Check("Cursor free while the menu is open", !DesktopTestingMode.Instance.Captured);

            // ---------------------------------------------------------------- menu: hover + click
            UIButton full = FindButton("Full Experience");
            yield return MouseTo(full);
            t.Check("Mouse hover highlights 'Full Experience'", full != null && full.IsHovered);
            UIButton other = FindButton("Turtle Experience");
            t.Check("Other buttons are not hovered", other != null && !other.IsHovered);
            yield return ClickLeft();
            var gm = GameManager.Instance;
            yield return WaitFor(() => Object.FindFirstObjectByType<SmoothLocomotion>().MovementEnabled, 60f);
            t.Check("Mouse click on 'Full Experience' starts the human phase", gm.Current == GameState.Human);

            // capture the mouse with a click, then look around
            yield return ClickLeft();
            t.Check("Click in the view captures the cursor", DesktopTestingMode.Instance.Captured);
            var dtm = DesktopTestingMode.Instance;
            float yaw0 = dtm.Yaw;
            yield return MouseDelta(new Vector2(200, 0));
            float expected = 200 * dtm.MouseSensitivity;
            t.Check($"Mouse look turns the head ({dtm.Yaw - yaw0:F1} deg, expected {expected:F1})", Mathf.Abs(dtm.Yaw - yaw0 - expected) < 1f);
            yield return Press(Key.Escape);
            yield return Release();
            t.Check("Esc releases the cursor", !DesktopTestingMode.Instance.Captured);
            yield return ClickLeft();
            t.Check("Clicking the view captures it again", DesktopTestingMode.Instance.Captured);

            // ---------------------------------------------------------------- human: walk, grab, bin, flashlight, hole
            var interactor = Object.FindFirstObjectByType<DesktopInteractor>();
            Vector3 p0 = PlayerRig.Head.position;
            yield return Hold(1.5f, Key.W);
            float walked = Flat(PlayerRig.Head.position - p0);
            t.Check($"WASD walks the human ({walked:F2} m in 1.5 s)", walked > 1.6f);

            TrashItem bottle = TrashItem.All.Find(x => x.Label == "bottle" && !x.Cleared);
            yield return WalkTo(bottle.transform.position, 1.2f);
            yield return AimAt(Center(bottle.gameObject));
            t.Check($"Crosshair targets the bottle ({(interactor.Target != null ? interactor.Target.name : "nothing")})",
                    interactor.Target != null && interactor.Target.attachedRigidbody == bottle.GetComponent<Rigidbody>());
            yield return Tap(Key.E);
            t.Check("E picks the bottle up", interactor.Held == bottle.GetComponent<Rigidbody>());
            var bin = Object.FindFirstObjectByType<TrashBin>();
            yield return WalkTo(bin.transform.position, 1.6f);
            yield return AimAt(bin.transform.position + Vector3.up * 0.4f);
            yield return null;
            yield return ClickLeft();       // left click = drop
            t.Check("Left click drops it", interactor.Held == null);
            yield return new WaitForSeconds(2f);
            t.Check("The bottle fell into the litter bin (removed)", bottle.InBin && bottle.Cleared);

            var lamp = Object.FindFirstObjectByType<Flashlight>();
            yield return WalkTo(lamp.transform.position, 1.3f);
            yield return AimAt(lamp.transform.position);
            t.Check("Flashlight under the crosshair", interactor.Target != null && interactor.Target.GetComponentInParent<Flashlight>() == lamp);
            yield return Tap(Key.F);
            t.Check("F switches the flashlight OFF", !lamp.IsOn);

            var hole = Object.FindFirstObjectByType<SandHole>();
            SandBucket bucket = NearestBucket(hole.transform.position);
            yield return WalkTo(bucket.transform.position, 1.2f);
            yield return AimAt(Center(bucket.gameObject));
            yield return Tap(Key.E);
            t.Check("E picks up the sand bucket", interactor.Held != null && interactor.Held.GetComponent<SandBucket>() == bucket);
            yield return WalkTo(hole.transform.position, 1.6f);
            yield return AimAt(hole.transform.position);
            yield return HoldMouseRight(2.6f);
            t.Check($"Holding RMB tips the bucket over the hole (hole {hole.Fill01 * 100:F0}% filled, bucket {bucket.Sand:F2})",
                    hole.Fill01 >= 0.45f && bucket.Sand < 0.05f);
            Vector3 sand = hole.transform.position + PlayerRig.FlatForward * -0.2f + Vector3.Cross(Vector3.up, PlayerRig.FlatForward) * 1.1f;
            yield return AimAt(sand);
            yield return new WaitForSeconds(0.6f);
            t.Check($"Aiming the bucket at the sand scoops a new load (bucket {bucket.Sand:F2})", bucket.Sand > 0.95f);
            yield return AimAt(hole.transform.position);
            yield return Hold(2.6f, Key.F);
            t.Check("Second bucket fills the hole", hole.IsFilled);
            yield return Tap(Key.E);

            // ---------------------------------------------------------------- end the night -> hatchling
            yield return Hold(2.2f, Key.Enter);
            yield return WaitFor(() => gm.Current == GameState.Hatchling && ScreenFader.Instance.Alpha < 0.05f, 120f);
            t.Check($"Holding Enter ends the night -> hatchling (scale {PlayerRig.Scale:F3}, eyes {(PlayerRig.Head.position.y - Object.FindFirstObjectByType<TurtleController>().transform.position.y) * 100:F1} cm)",
                    gm.Current == GameState.Hatchling && Mathf.Abs(PlayerRig.Scale - 0.05f) < 0.001f);
            t.Check("PERSIST (desktop): bottle in the bin, flashlight off, hole filled", bottle.InBin && !lamp.IsOn && hole.IsFilled);

            var turtle = Object.FindFirstObjectByType<TurtleController>();
            Vector3 tp = turtle.transform.position;
            yield return Hold(4f, Key.W);
            t.Check($"W makes the hatchling crawl ({Flat(turtle.transform.position - tp):F2} m)", Flat(turtle.transform.position - tp) > 0.4f);
            float ty = turtle.transform.eulerAngles.y;
            yield return Hold(1.5f, Key.D);
            t.Check($"D turns the hatchling body ({Mathf.DeltaAngle(ty, turtle.transform.eulerAngles.y):F0} deg)", Mathf.DeltaAngle(ty, turtle.transform.eulerAngles.y) > 20f);
            int strokes = turtle.Strokes;
            yield return Tap(Key.Space);
            yield return new WaitForSeconds(0.3f);
            t.Check("Space triggers a burst", turtle.Strokes > strokes && turtle.StrokeIsBurst);

            // crawl the last metres into the surf with real input
            Vector3 sea = GameObject.Find("SeaPoint").transform.position;
            turtle.TeleportTo(new Vector3(sea.x, Ground(sea - Vector3.forward * 1.2f) + 0.02f, sea.z - 1.2f), 0f);
            yield return Hold(12f, Key.W, () => gm.Current == GameState.End);
            yield return WaitFor(() => FindButton("Main menu") != null, 20f);
            var title = GameObject.Find("EndScreen")?.GetComponentInChildren<TextMeshPro>();
            t.Check($"Crawling into the sea shows '{(title != null ? title.text : "-")}'", gm.Current == GameState.End && title != null && title.text == "Great Job");
            t.Check("Cursor freed for the end screen", !DesktopTestingMode.Instance.Captured);

            // ---------------------------------------------------------------- Main menu (scene reload) -> Turtle Experience
            UIButton menuBtn = FindButton("Main menu");
            yield return MouseTo(menuBtn);
            if (!menuBtn.IsHovered) LogRaycast(menuBtn);
            t.Check("Hover on 'Main menu'", menuBtn.IsHovered);
            yield return ClickLeft();
            yield return WaitFor(() => GameObject.Find("StartMenu") != null && GameManager.Instance != null && GameManager.Instance != gm, 60f);
            yield return new WaitForSeconds(1f);
            t.Check("'Main menu' reloads the scene and shows the start menu",
                    GameManager.Instance != gm && GameManager.Instance.Current == GameState.Intro && GameObject.Find("StartMenu") != null);
            t.Check("Fresh scene: beach reset (flashlight on again)", Object.FindFirstObjectByType<Flashlight>().IsOn);
            t.Check("Desktop mode and EventSystem re-initialised after reload",
                    DesktopTestingMode.Active && EventSystem.current != null && EventSystem.current.currentInputModule is InputSystemUIInputModule);
            UIButton turtleBtn = FindButton("Turtle Experience");
            if (turtleBtn == null) { t.Check("Start menu has a 'Turtle Experience' button", false); t.Finish(); yield break; }
            yield return MouseTo(turtleBtn);
            t.Check("Hover on 'Turtle Experience'", turtleBtn.IsHovered);
            yield return ClickLeft();
            var gm2 = GameManager.Instance;
            yield return WaitFor(() => gm2.Current == GameState.Hatchling && ScreenFader.Instance.Alpha < 0.05f, 90f);
            t.Check("Mouse click on 'Turtle Experience' starts as a hatchling in the nest", gm2.Current == GameState.Hatchling &&
                    (Object.FindFirstObjectByType<TurtleController>().transform.position - GameObject.Find("TurtleSpawn").transform.position).magnitude < 0.3f);

            t.Finish();
        }

        // ================================================================== input simulation
        static void SetupDevices()
        {
            var settings = Object.Instantiate(InputSystem.settings);
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = settings;
            _kb = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
            _kb.MakeCurrent();
            _mouse.MakeCurrent();
        }

        static void SendKeys() => InputSystem.QueueStateEvent(_kb, new KeyboardState(_keys.ToArray()));

        static void SendMouse(Vector2 delta)
        {
            var s = new MouseState { position = _mousePos, delta = delta };
            s = s.WithButton(MouseButton.Left, _lmb).WithButton(MouseButton.Right, _rmb);
            InputSystem.QueueStateEvent(_mouse, s);
        }

        static IEnumerator Press(params Key[] keys)
        {
            _keys.Clear();
            _keys.AddRange(keys);
            SendKeys();
            yield return null;
            yield return null;
        }

        static IEnumerator Release()
        {
            _keys.Clear();
            SendKeys();
            yield return null;
            yield return null;
        }

        static IEnumerator Tap(Key k)
        {
            yield return Press(k);
            yield return Release();
        }

        static IEnumerator Hold(float gameSeconds, Key k, System.Func<bool> until = null)
        {
            yield return Press(k);
            float end = Time.time + gameSeconds;
            while (Time.time < end && (until == null || !until())) yield return null;
            yield return Release();
        }

        static IEnumerator HoldMouseRight(float gameSeconds)
        {
            _rmb = true;
            SendMouse(Vector2.zero);
            float end = Time.time + gameSeconds;
            while (Time.time < end) yield return null;
            _rmb = false;
            SendMouse(Vector2.zero);
            yield return null;
        }

        static IEnumerator ClickLeft()
        {
            _lmb = true;
            SendMouse(Vector2.zero);
            yield return null;
            yield return null;
            _lmb = false;
            SendMouse(Vector2.zero);
            yield return null;
            yield return null;
        }

        static IEnumerator MouseTo(UIButton b)
        {
            if (b == null) yield break;
            Vector3 sp = PlayerRig.HeadCamera.WorldToScreenPoint(b.transform.position);
            _mousePos = new Vector2(sp.x, sp.y);
            SendMouse(Vector2.zero);
            for (int i = 0; i < 4; i++) yield return null;
        }

        static IEnumerator MouseDelta(Vector2 d)
        {
            SendMouse(d);
            yield return null;
            SendMouse(Vector2.zero);
            yield return null;
            yield return null;
        }

        /// <summary>Turn the view towards a world point with a mouse movement (iteratively, like a person correcting).</summary>
        static IEnumerator AimAt(Vector3 target)
        {
            var dtm = DesktopTestingMode.Instance;
            for (int i = 0; i < 4; i++)
            {
                Transform h = PlayerRig.Head;
                Vector3 dir = target - h.position;
                float wantYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                float wantPitch = -Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg;
                float dYaw = Mathf.DeltaAngle(h.eulerAngles.y, wantYaw);
                float dPitch = wantPitch - dtm.Pitch;
                if (Mathf.Abs(dYaw) < 0.5f && Mathf.Abs(dPitch) < 0.5f) break;
                yield return MouseDelta(new Vector2(dYaw / dtm.MouseSensitivity, -dPitch / dtm.MouseSensitivity));
            }
        }

        /// <summary>Walk with W towards a point (aiming the view at it with the mouse first).</summary>
        static IEnumerator WalkTo(Vector3 target, float stopAt)
        {
            Vector3 eyeLevel = new Vector3(target.x, PlayerRig.Head.position.y, target.z);
            yield return AimAt(eyeLevel);
            if (Flat(PlayerRig.Head.position - target) <= stopAt) yield break;
            yield return Press(Key.W);
            float end = Time.time + 25f;
            while (Time.time < end && Flat(PlayerRig.Head.position - target) > stopAt)
            {
                if (Time.frameCount % 20 == 0) yield return AimAt(new Vector3(target.x, PlayerRig.Head.position.y, target.z));
                yield return null;
            }
            yield return Release();
        }

        static IEnumerator WaitFor(System.Func<bool> cond, float timeoutReal)
        {
            float end = Time.realtimeSinceStartup + timeoutReal;
            while (!cond() && Time.realtimeSinceStartup < end) yield return null;
            yield return null;
        }

        // ================================================================== helpers
        static void LogRaycast(UIButton b)
        {
            var cam = PlayerRig.HeadCamera;
            var ped = new PointerEventData(EventSystem.current) { position = _mousePos };
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(ped, results);
            Debug.Log($"[SameShoreTest] DIAG mouse={_mousePos} screen={Screen.width}x{Screen.height} camRect={cam.pixelRect} near={cam.nearClipPlane} " +
                      $"btn={b.transform.position} head={cam.transform.position} hits={results.Count} first={(results.Count > 0 ? results[0].gameObject.name : "-")} " +
                      $"pointerMouse={Mouse.current?.position.ReadValue()}");
            foreach (var hit in Physics.RaycastAll(cam.ScreenPointToRay(_mousePos), 10f, ~0, QueryTriggerInteraction.Collide))
                Debug.Log($"[SameShoreTest] DIAG physics hit {hit.collider.name} d={hit.distance:F4}");
        }
        static UIButton FindButton(string label)
        {
            foreach (var b in Object.FindObjectsByType<UIButton>(FindObjectsSortMode.None))
                if (b.Label == label && b.isActiveAndEnabled) return b;
            return null;
        }

        static SandBucket NearestBucket(Vector3 p)
        {
            SandBucket best = null;
            foreach (var b in Object.FindObjectsByType<SandBucket>(FindObjectsSortMode.None))
                if (best == null || (b.transform.position - p).sqrMagnitude < (best.transform.position - p).sqrMagnitude) best = b;
            return best;
        }

        static Vector3 Center(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            Bounds b = rs[0].bounds;
            foreach (var r in rs) if (!(r is ParticleSystemRenderer)) b.Encapsulate(r.bounds);
            return b.center;
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        static float Ground(Vector3 p)
        {
            foreach (Terrain t in Terrain.activeTerrains)
            {
                Vector3 o = t.transform.position, s = t.terrainData.size;
                if (p.x >= o.x && p.z >= o.z && p.x <= o.x + s.x && p.z <= o.z + s.z) return t.SampleHeight(p) + o.y;
            }
            return 0;
        }
    }
}
