using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace SameShore
{
    /// <summary>
    /// Desktop Testing mode: lets the whole experience be played in the Unity Editor without a headset.
    /// Activates automatically in the Editor when no XR device is running; on Quest (or with a headset
    /// over Link) it stays off and nothing here runs, so the XR controls are untouched.
    ///
    /// It reuses the existing rig instead of adding a second camera: the OVRCameraRig's TrackingSpace
    /// is raised to eye height and rotated with the mouse (the HMD would normally drive the anchors
    /// inside it). The hatchling's 0.05 rig scale therefore also applies on desktop (eyes at ~8 cm).
    ///
    /// Controls: WASD move/crawl · mouse look · LMB or E grab/drop · RMB or F use (flashlight, tip bucket)
    /// · Space burst · hold Enter end human phase · Esc release cursor · click to capture it again.
    /// Overrides: env SAMESHORE_DESKTOP=1/0, command line -sameshore-desktop / -sameshore-xr.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class DesktopTestingMode : MonoBehaviour
    {
        public enum Activation { Auto, ForceDesktop, ForceXR }

        [SerializeField] private Activation activation = Activation.Auto;
        [SerializeField] private float eyeHeight = 1.65f;
        [Tooltip("Degrees per mouse count.")]
        [SerializeField] private float mouseSensitivity = 0.12f;
        [SerializeField] private float fieldOfView = 75f;
        [SerializeField] private bool showHelp = true;

        public static bool Active { get; private set; }
        public static DesktopTestingMode Instance { get; private set; }
        public float MouseSensitivity => mouseSensitivity;
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public EventSystem EventSystem { get; private set; }

        Transform _trackingSpace;
        bool _ignoreClickUntilRelease;
        bool _menuWasOpen;

        void Awake()
        {
            Instance = this;
            Active = Decide();
        }

        void OnDestroy()
        {
            if (Instance == this) { Instance = null; Active = false; }
            if (Active) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }

        bool Decide()
        {
            string env = Environment.GetEnvironmentVariable("SAMESHORE_DESKTOP");
            if (env == "1") return true;
            if (env == "0") return false;
            foreach (string a in Environment.GetCommandLineArgs())
            {
                if (a == "-sameshore-desktop") return true;
                if (a == "-sameshore-xr") return false;
            }
            switch (activation)
            {
                case Activation.ForceDesktop: return true;
                case Activation.ForceXR: return false;
                default:
                    // Editor only, and only when no headset is actually rendering
                    return Application.isEditor && !XRSettings.isDeviceActive && !OVRManager.isHmdPresent;
            }
        }

        void Start()
        {
            if (!Active) { enabled = false; return; }
            Debug.Log("[SameShore] Desktop Testing mode ON (no XR device). WASD + mouse, LMB/E grab, RMB/F use, Esc frees the cursor.");
            SetupCamera();
            SetupEventSystem();
            if (GetComponent<DesktopInteractor>() == null) gameObject.AddComponent<DesktopInteractor>();
        }

        void SetupCamera()
        {
            OVRCameraRig rig = PlayerRig.Rig;
            if (rig == null) return;
            _trackingSpace = rig.trackingSpace;
            // the headset emulator also grabs the mouse; desktop mode replaces it
            foreach (var emu in FindObjectsByType<OVRHeadsetEmulator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                emu.enabled = false;
            Camera cam = PlayerRig.HeadCamera;
            if (cam != null)
            {
                cam.fieldOfView = fieldOfView;
                cam.stereoTargetEye = StereoTargetEyeMask.None;
                var raycaster = cam.GetComponent<PhysicsRaycaster>();
                if (raycaster == null) raycaster = cam.gameObject.AddComponent<PhysicsRaycaster>();
                raycaster.eventMask = 1 << UIButton.UILayer;
                cam.cullingMask |= 1 << UIButton.UILayer;
            }
            Yaw = 0f;
            Pitch = 0f;
            ApplyHead();
        }

        /// <summary>
        /// Finds (or creates) the scene's EventSystem and makes sure it is driven by the Input System
        /// UI module; incompatible/legacy modules are switched off (desktop mode only).
        /// </summary>
        void SetupEventSystem()
        {
            EventSystem[] all = FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            EventSystem es = null;
            foreach (var e in all) if (e.isActiveAndEnabled) { es = e; break; }
            if (es == null && all.Length > 0) { es = all[0]; es.gameObject.SetActive(true); es.enabled = true; }
            if (es == null) es = new GameObject("EventSystem (Desktop)").AddComponent<EventSystem>();
            foreach (var e in all) if (e != es) e.gameObject.SetActive(false);

#if ENABLE_INPUT_SYSTEM
            var module = es.GetComponent<InputSystemUIInputModule>();
            if (module == null) module = es.gameObject.AddComponent<InputSystemUIInputModule>();
            if (module.actionsAsset == null) module.AssignDefaultActions();
            module.enabled = true;
            foreach (var m in es.GetComponents<BaseInputModule>())
                if (m != module) m.enabled = false;
#endif
            EventSystem.current = es;
            EventSystem = es;
            Debug.Log($"[SameShore] Desktop EventSystem '{es.name}' with {es.GetComponent<BaseInputModule>()?.GetType().Name}; PhysicsRaycaster on {PlayerRig.HeadCamera?.name}.");
        }

        /// <summary>
        /// Mouse captured for looking/interacting. Tracked here rather than read back from
        /// Cursor.lockState, which Unity resets when the Game view loses focus (and never changes
        /// without a window, e.g. in batch tests).
        /// </summary>
        public bool Captured { get; private set; }

        void Update()
        {
            UpdateCursor();
            if (Captured) Look();
            ApplyHead();
        }

        void SetCaptured(bool on)
        {
            Captured = on;
            Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !on;
        }

        void UpdateCursor()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool menuOpen = UIPanel.AnyInteractiveVisible;
            if (menuOpen && !_menuWasOpen)
            {
                // panels open at eye level: level the view so the buttons are on screen
                Pitch = 0f;
                ApplyHead();
                foreach (var p in UIPanel.Visible) if (p != null && p.Interactive) p.Follow.Snap();
            }
            _menuWasOpen = menuOpen;
            if (menuOpen || (kb != null && kb.escapeKey.wasPressedThisFrame))
            {
                if (Captured || Cursor.lockState != CursorLockMode.None) SetCaptured(false);
                if (menuOpen) _ignoreClickUntilRelease = true;
                return;
            }
            if (mouse != null && _ignoreClickUntilRelease && !mouse.leftButton.isPressed) _ignoreClickUntilRelease = false;
            if (!Captured && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                // the click that captures the cursor is not an interaction
                SetCaptured(true);
                _ignoreClickUntilRelease = true;
            }
            else if (Captured && Application.isFocused && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked; // Game view refocused
            }
#endif
        }

        /// <summary>True on the frame a click should act on the world (cursor captured, not the capturing click).</summary>
        public bool WorldClickDown
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                return Captured && mouse != null && mouse.leftButton.wasPressedThisFrame && !_ignoreClickUntilRelease;
#else
                return false;
#endif
            }
        }

        void Look()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 d = mouse.delta.ReadValue();
            Yaw += d.x * mouseSensitivity;
            Pitch = Mathf.Clamp(Pitch - d.y * mouseSensitivity, -85f, 85f);
#endif
        }

        /// <summary>Points the camera at a world position (used by the menu reset and the tests).</summary>
        public void SetLook(float yaw, float pitch)
        {
            Yaw = yaw;
            Pitch = Mathf.Clamp(pitch, -85f, 85f);
            ApplyHead();
        }

        void ApplyHead()
        {
            if (_trackingSpace == null) return;
            _trackingSpace.localPosition = new Vector3(0, eyeHeight, 0);
            _trackingSpace.localRotation = Quaternion.Euler(Pitch, Yaw, 0);
        }

        void OnGUI()
        {
            if (!showHelp) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            style.normal.textColor = new Color(1, 1, 1, 0.85f);
            bool locked = Captured;
            if (locked && !UIPanel.AnyInteractiveVisible)
            {
                var c = new GUIStyle(style) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(Screen.width / 2f - 20, Screen.height / 2f - 20, 40, 40), "+", c);
                var interactor = GetComponent<DesktopInteractor>();
                if (interactor != null && !string.IsNullOrEmpty(interactor.Prompt))
                {
                    var p = new GUIStyle(style) { alignment = TextAnchor.UpperCenter };
                    GUI.Label(new Rect(Screen.width / 2f - 200, Screen.height / 2f + 18, 400, 40), interactor.Prompt, p);
                }
            }
            string help = GameManager.Instance != null && GameManager.Instance.Current == GameState.Hatchling
                ? "DESKTOP TESTING · W/S crawl · A/D turn body · Space burst · mouse look · Esc free cursor"
                : "DESKTOP TESTING · WASD walk · mouse look · LMB/E grab-drop · RMB/F use (flashlight, tip bucket) · hold Enter: end night · Esc free cursor";
            if (!locked && !UIPanel.AnyInteractiveVisible) help += "   ·   CLICK THE VIEW TO CAPTURE THE MOUSE";
            GUI.Label(new Rect(10, Screen.height - 26, Screen.width - 20, 24), help, style);
        }
    }
}
