using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SameShore.Tests
{
    /// <summary>Simulated keyboard + mouse (Input System devices) for play-mode tests.</summary>
    public static class TestInput
    {
        static Keyboard _kb;
        static Mouse _mouse;
        static readonly List<Key> Keys = new List<Key>();
        static Vector2 _pos;
        static bool _lmb, _rmb;

        public static void Setup()
        {
            if (_kb != null) return;
            var settings = Object.Instantiate(InputSystem.settings);
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = settings;
            _kb = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
            _kb.MakeCurrent();
            _mouse.MakeCurrent();
        }

        static void SendKeys() => InputSystem.QueueStateEvent(_kb, new KeyboardState(Keys.ToArray()));

        static void SendMouse(Vector2 delta)
        {
            var s = new MouseState { position = _pos, delta = delta }.WithButton(MouseButton.Left, _lmb).WithButton(MouseButton.Right, _rmb);
            InputSystem.QueueStateEvent(_mouse, s);
        }

        public static IEnumerator Press(params Key[] keys)
        {
            Keys.Clear();
            Keys.AddRange(keys);
            SendKeys();
            yield return null;
            yield return null;
        }

        public static IEnumerator Release()
        {
            Keys.Clear();
            SendKeys();
            yield return null;
            yield return null;
        }

        public static IEnumerator Tap(Key k)
        {
            yield return Press(k);
            yield return Release();
        }

        public static IEnumerator Hold(float gameSeconds, Key k, System.Func<bool> until = null)
        {
            yield return Press(k);
            float end = Time.time + gameSeconds;
            while (Time.time < end && (until == null || !until())) yield return null;
            yield return Release();
        }

        public static IEnumerator HoldRight(float gameSeconds)
        {
            _rmb = true;
            SendMouse(Vector2.zero);
            float end = Time.time + gameSeconds;
            while (Time.time < end) yield return null;
            _rmb = false;
            SendMouse(Vector2.zero);
            yield return null;
        }

        public static IEnumerator Click()
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

        public static IEnumerator MouseTo(UIButton b)
        {
            if (b == null) yield break;
            for (int i = 0; i < 3; i++) yield return null;   // let the panel settle
            Vector3 sp = PlayerRig.HeadCamera.WorldToScreenPoint(b.transform.position);
            _pos = new Vector2(sp.x, sp.y);
            SendMouse(Vector2.zero);
            for (int i = 0; i < 4; i++) yield return null;
        }

        /// <summary>Hover a button with the mouse and click it (through the EventSystem).</summary>
        public static IEnumerator ClickButton(string label)
        {
            var b = FindButton(label);
            yield return MouseTo(b);
            yield return Click();
        }

        public static IEnumerator MouseDelta(Vector2 d)
        {
            SendMouse(d);
            yield return null;
            SendMouse(Vector2.zero);
            yield return null;
            yield return null;
        }

        public static IEnumerator AimAt(Vector3 target)
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

        public static IEnumerator WalkTo(Vector3 target, float stopAt, float timeout = 25f)
        {
            yield return AimAt(new Vector3(target.x, PlayerRig.Head.position.y, target.z));
            if (Flat(PlayerRig.Head.position - target) <= stopAt) yield break;
            yield return Press(Key.W);
            float end = Time.time + timeout;
            while (Time.time < end && Flat(PlayerRig.Head.position - target) > stopAt)
            {
                if (Time.frameCount % 20 == 0) yield return AimAt(new Vector3(target.x, PlayerRig.Head.position.y, target.z));
                yield return null;
            }
            yield return Release();
        }

        public static IEnumerator WaitFor(System.Func<bool> cond, float timeoutReal)
        {
            float end = Time.realtimeSinceStartup + timeoutReal;
            while (!cond() && Time.realtimeSinceStartup < end) yield return null;
            yield return null;
        }

        /// <summary>Skips subtitles with Space (a real input) until the condition holds.</summary>
        public static IEnumerator SkipStoryUntil(System.Func<bool> cond, float timeoutReal)
        {
            float end = Time.realtimeSinceStartup + timeoutReal;
            while (!cond() && Time.realtimeSinceStartup < end)
            {
                var sub = GameObject.Find("Subtitle");
                if (sub != null && sub.activeSelf) yield return Tap(Key.Space);
                else yield return null;
            }
            yield return null;
        }

        public static UIButton FindButton(string label)
        {
            foreach (var b in Object.FindObjectsByType<UIButton>(FindObjectsSortMode.None))
                if (b.Label == label && b.isActiveAndEnabled) return b;
            return null;
        }

        public static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        public static Vector3 Center(GameObject go)
        {
            // aim at what the ray must hit: the solid colliders (renderer bounds would include e.g. the beam cone)
            bool any = false;
            Bounds b = default;
            foreach (var c in go.GetComponentsInChildren<Collider>())
            {
                if (!c.enabled || c.isTrigger) continue;
                if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
            }
            if (any) return b.center;
            var rs = go.GetComponentsInChildren<Renderer>();
            b = rs[0].bounds;
            foreach (var r in rs) if (r.enabled && !(r is ParticleSystemRenderer)) b.Encapsulate(r.bounds);
            return b.center;
        }

        public static void SaveView(string file)
        {
            var cam = PlayerRig.HeadCamera;
            var rt = new RenderTexture(960, 540, 24);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;
            RenderTexture.active = rt;
            var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Application.dataPath, "../Logs/Views"));
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "../Logs/Views/" + file), tex.EncodeToPNG());
            Object.Destroy(rt);
            Object.Destroy(tex);
        }
    }
}
