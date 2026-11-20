using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SameShore
{
    public enum Hand { Left, Right }

    /// <summary>
    /// Single entry point for controller input (Meta OVRInput) with a keyboard fallback so the
    /// experience can be exercised in the Editor without a headset.
    /// Keyboard: WASD = left stick, Q/E or arrows = right stick, Space = A (burst),
    /// Enter = B (hold to finish the human phase), F = trigger, G = grip.
    /// </summary>
    public static class XRInput
    {
        public static Vector2 LeftStick => Max(OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick), KeyStick(true));
        public static Vector2 RightStick => Max(OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick), KeyStick(false));

        /// <summary>Whichever stick is pushed further (the hatchling can be driven with either hand).</summary>
        public static Vector2 AnyStick
        {
            get
            {
                Vector2 l = LeftStick, r = RightStick;
                return l.sqrMagnitude >= r.sqrMagnitude ? l : r;
            }
        }

        public static bool TriggerDown(Hand h) =>
            OVRInput.GetDown(h == Hand.Left ? OVRInput.Button.PrimaryIndexTrigger : OVRInput.Button.SecondaryIndexTrigger)
            || (h == Hand.Right && KeyDown(KeyId.F));

        public static bool AnyTriggerDown => TriggerDown(Hand.Left) || TriggerDown(Hand.Right);

        /// <summary>A or X (face button, lower).</summary>
        public static bool BurstDown =>
            OVRInput.GetDown(OVRInput.Button.One) || OVRInput.GetDown(OVRInput.Button.Three) || KeyDown(KeyId.Space);

        /// <summary>B or Y (face button, upper) held.</summary>
        public static bool FinishHeld =>
            OVRInput.Get(OVRInput.Button.Two) || OVRInput.Get(OVRInput.Button.Four) || KeyHeld(KeyId.Enter);

        public static bool KeyDown(KeyId k)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && Key(kb, k).wasPressedThisFrame;
#else
            return false;
#endif
        }

        public static bool KeyHeld(KeyId k)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && Key(kb, k).isPressed;
#else
            return false;
#endif
        }

        public enum KeyId { Space, Enter, F, G, R, M, E, H, P, Escape, Digit1, Digit2, Digit3 }

#if ENABLE_INPUT_SYSTEM
        static UnityEngine.InputSystem.Controls.KeyControl Key(Keyboard kb, KeyId k)
        {
            switch (k)
            {
                case KeyId.Space: return kb.spaceKey;
                case KeyId.Enter: return kb.enterKey;
                case KeyId.F: return kb.fKey;
                case KeyId.G: return kb.gKey;
                case KeyId.R: return kb.rKey;
                case KeyId.M: return kb.mKey;
                case KeyId.E: return kb.eKey;
                case KeyId.H: return kb.hKey;
                case KeyId.P: return kb.pKey;
                case KeyId.Escape: return kb.escapeKey;
                case KeyId.Digit1: return kb.digit1Key;
                case KeyId.Digit2: return kb.digit2Key;
                default: return kb.digit3Key;
            }
        }
#endif

        static Vector2 KeyStick(bool left)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;
            Vector2 v = Vector2.zero;
            if (left)
            {
                if (kb.wKey.isPressed) v.y += 1;
                if (kb.sKey.isPressed) v.y -= 1;
                if (kb.dKey.isPressed) v.x += 1;
                if (kb.aKey.isPressed) v.x -= 1;
            }
            else
            {
                // in Desktop Testing mode the mouse turns and E is "interact", so Q/E are not used for turning
                bool qe = !DesktopTestingMode.Active;
                if ((qe && kb.eKey.isPressed) || kb.rightArrowKey.isPressed) v.x += 1;
                if ((qe && kb.qKey.isPressed) || kb.leftArrowKey.isPressed) v.x -= 1;
                if (kb.upArrowKey.isPressed) v.y += 1;
                if (kb.downArrowKey.isPressed) v.y -= 1;
            }
            return v;
#else
            return Vector2.zero;
#endif
        }

        static Vector2 Max(Vector2 a, Vector2 b) => a.sqrMagnitude >= b.sqrMagnitude ? a : b;

        // ------------------------------------------------------------------ haptics
        public static void Haptic(Hand h, float amplitude, float seconds, float frequency = 0.5f)
        {
            HapticsRunner.Instance.Pulse(h == Hand.Left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch,
                                         amplitude, seconds, frequency);
        }

        public static void HapticBoth(float amplitude, float seconds, float frequency = 0.5f)
        {
            Haptic(Hand.Left, amplitude, seconds, frequency);
            Haptic(Hand.Right, amplitude, seconds, frequency);
        }

        class HapticsRunner : MonoBehaviour
        {
            static HapticsRunner _instance;

            public static HapticsRunner Instance
            {
                get
                {
                    if (_instance == null)
                    {
                        var go = new GameObject("~Haptics") { hideFlags = HideFlags.HideAndDontSave };
                        _instance = go.AddComponent<HapticsRunner>();
                    }
                    return _instance;
                }
            }

            public void Pulse(OVRInput.Controller c, float amp, float seconds, float freq) =>
                StartCoroutine(Run(c, amp, seconds, freq));

            IEnumerator Run(OVRInput.Controller c, float amp, float seconds, float freq)
            {
                OVRInput.SetControllerVibration(freq, Mathf.Clamp01(amp), c);
                yield return new WaitForSecondsRealtime(seconds);
                OVRInput.SetControllerVibration(0, 0, c);
            }
        }
    }
}
