using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// Human phase (VS1): smooth locomotion, footsteps that leave real footprints in the sand
    /// (they persist and become small traps for the hatchling), and "hold B/Y to end the night".
    /// Meta Interaction SDK teleport/turn locomotion is switched off so the stick only walks.
    /// </summary>
    public class HumanController : MonoBehaviour
    {
        [SerializeField] private SmoothLocomotion locomotion;
        [SerializeField] private FootprintSystem footprints;
        [SerializeField] private float stepLength = 0.72f;
        [SerializeField] private float finishHoldSeconds = 1.6f;
        [Tooltip("Objects that only make sense with the Meta teleport locomotion (e.g. Teleport Hotspot).")]
        [SerializeField] private GameObject[] disableObjects;
        [SerializeField] private string[] isdkLocomotionKeywords = { "Teleport", "Locomotion", "Locomotor", "Turner" };

        public bool Active { get; private set; }
        public string FinishLabel { get; set; } = "End the night and become a hatchling";
        public event Action FinishRequested;

        Vector3 _lastStepPos, _lastPrintPos;
        float _stepAccum;
        bool _leftFoot;
        float _hold;
        UIPanel _holdPanel;
        TextMeshPro _holdText;
        AudioSource _stepSource;
        readonly List<Behaviour> _isdk = new List<Behaviour>();

        void Awake()
        {
            _stepSource = gameObject.AddComponent<AudioSource>();
            _stepSource.spatialBlend = 0f;
            _stepSource.playOnAwake = false;
            _stepSource.volume = 0.5f;
        }

        void Start()
        {
            foreach (var go in disableObjects) if (go != null) go.SetActive(false);
            Transform root = PlayerRig.Root;
            if (root != null)
            {
                foreach (var b in root.GetComponentsInChildren<Behaviour>(true))
                {
                    if (b == null) continue;
                    Type t = b.GetType();
                    if (t.Namespace == null || !t.Namespace.StartsWith("Oculus.Interaction")) continue;
                    foreach (string k in isdkLocomotionKeywords)
                        if (t.Name.Contains(k)) { _isdk.Add(b); b.enabled = false; break; }
                }
                Debug.Log($"[SameShore] Disabled {_isdk.Count} Meta ISDK locomotion components (smooth stick walking is used instead).");

                // the body capsule must not collide with the rig's own colliders (ISDK locomotor, hands...)
                var body = root.GetComponent<CharacterController>();
                if (body != null)
                {
                    int n = 0;
                    foreach (var c in root.GetComponentsInChildren<Collider>(true))
                    {
                        if (c == body) continue;
                        Physics.IgnoreCollision(body, c, true);
                        n++;
                    }
                    Debug.Log($"[SameShore] Player capsule ignores {n} rig colliders.");
                }
            }
            SetActive(false);
        }

        public void SetActive(bool on)
        {
            Active = on;
            if (locomotion != null) locomotion.MovementEnabled = on;
            _hold = 0;
            if (PlayerRig.Head != null) _lastStepPos = _lastPrintPos = PlayerRig.Head.position;
            if (_holdPanel != null) _holdPanel.Hide();
        }

        public void PlaceAt(Transform spot)
        {
            if (locomotion != null) locomotion.PlaceAt(spot.position, spot.eulerAngles.y);
            if (PlayerRig.Head != null) _lastStepPos = _lastPrintPos = PlayerRig.Head.position;
        }

        void Update()
        {
            if (!Active || PlayerRig.Head == null) return;
            Footsteps();
            FinishHold();
        }

        void Footsteps()
        {
            Vector3 p = PlayerRig.Head.position;
            Vector3 d = p - _lastStepPos;
            d.y = 0;
            _stepAccum += d.magnitude;
            _lastStepPos = p;
            if (_stepAccum < stepLength) return;
            _stepAccum = 0;
            Vector3 stride = p - _lastPrintPos;
            stride.y = 0;
            _lastPrintPos = p;
            _leftFoot = !_leftFoot;
            _stepSource.pitch = UnityEngine.Random.Range(0.85f, 1.15f);
            _stepSource.PlayOneShot(SameShoreAssets.Pick(SameShoreAssets.I.footsteps), UnityEngine.Random.Range(0.6f, 0.9f));
            if (footprints != null && stride.sqrMagnitude > 0.01f)
            {
                Vector3 fwd = stride.normalized;
                Vector3 side = Vector3.Cross(Vector3.up, fwd) * (_leftFoot ? -0.11f : 0.11f);
                footprints.Add(p + side, fwd, _leftFoot);
            }
        }

        void FinishHold()
        {
            if (XRInput.FinishHeld)
            {
                if (_holdPanel == null) BuildHoldPanel();
                if (!_holdPanel.gameObject.activeSelf) _holdPanel.Show();
                _hold += Time.deltaTime;
                int pct = Mathf.RoundToInt(Mathf.Clamp01(_hold / finishHoldSeconds) * 100);
                _holdText.text = $"{FinishLabel}\n<size=70%>{new string('|', pct / 5)}{new string('.', 20 - pct / 5)}</size>";
                if (_hold >= finishHoldSeconds)
                {
                    _hold = 0;
                    _holdPanel.Hide();
                    XRInput.HapticBoth(0.4f, 0.15f);
                    FinishRequested?.Invoke();
                }
            }
            else if (_hold > 0)
            {
                _hold = 0;
                if (_holdPanel != null) _holdPanel.Hide();
            }
        }

        void BuildHoldPanel()
        {
            _holdPanel = UIPanel.Create("HoldToFinish", new Vector2(0.62f, 0.16f), 1.0f);
            _holdPanel.Follow.heightOffset = -0.25f;
            _holdText = _holdPanel.AddText("", 0.42f, Vector2.zero, new Vector2(0.58f, 0.14f), Color.white);
        }
    }
}
