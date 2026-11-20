using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SameShore
{
    /// <summary>
    /// Keeps a world-space panel comfortably in front of the player: it only re-centres when the
    /// player looks away (lazy follow), and scales with the player (human 1, hatchling 0.05).
    /// </summary>
    public class LazyFollow : MonoBehaviour
    {
        public float distance = 1.4f;
        public float heightOffset = -0.05f;
        public float followAngle = 28f;
        public float smoothTime = 0.35f;
        public float baseScale = 1f;
        public bool follow = true;

        Vector3 _vel;
        Vector3 _target;
        bool _moving;

        void OnEnable() => Snap();

        public void Snap()
        {
            Transform head = PlayerRig.Head;
            if (head == null) return;
            _target = TargetPos(head);
            transform.position = _target;
            Face(head);
        }

        Vector3 TargetPos(Transform head)
        {
            float s = PlayerRig.Scale;
            return head.position + PlayerRig.FlatForward * distance * s + Vector3.up * heightOffset * s;
        }

        void LateUpdate()
        {
            Transform head = PlayerRig.Head;
            if (head == null) return;
            float s = PlayerRig.Scale;
            transform.localScale = Vector3.one * baseScale * s;
            if (follow)
            {
                Vector3 toPanel = transform.position - head.position;
                float angle = Vector3.Angle(Vector3.ProjectOnPlane(toPanel, Vector3.up), PlayerRig.FlatForward);
                float dist = toPanel.magnitude / Mathf.Max(s, 1e-4f);
                if (angle > followAngle || Mathf.Abs(dist - distance) > 0.6f) _moving = true;
                if (_moving)
                {
                    _target = TargetPos(head);
                    transform.position = Vector3.SmoothDamp(transform.position, _target, ref _vel, smoothTime);
                    if ((transform.position - _target).magnitude < 0.01f * s) _moving = false;
                }
            }
            Face(head);
        }

        void Face(Transform head)
        {
            Vector3 d = transform.position - head.position;
            d.y *= 0.5f;
            if (d.sqrMagnitude > 1e-8f) transform.rotation = Quaternion.LookRotation(d, Vector3.up);
        }
    }

    /// <summary>World-space panel built from code (quad background + TextMeshPro + buttons).</summary>
    public class UIPanel : MonoBehaviour
    {
        public static readonly List<UIPanel> Visible = new List<UIPanel>();
        static Material _textMat;

        public LazyFollow Follow { get; private set; }
        readonly List<UIButton> _buttons = new List<UIButton>();

        /// <summary>Panel with buttons (menu, end screen): the desktop cursor is freed while one is open.</summary>
        public bool Interactive => _buttons.Count > 0;
        public IReadOnlyList<UIButton> Buttons => _buttons;

        public static bool AnyInteractiveVisible
        {
            get
            {
                foreach (var p in Visible) if (p != null && p.Interactive) return true;
                return false;
            }
        }

        public static UIPanel Create(string name, Vector2 size, float distance = 1.4f, Color? background = null)
        {
            var go = new GameObject(name);
            var panel = go.AddComponent<UIPanel>();
            panel.Follow = go.AddComponent<LazyFollow>();
            panel.Follow.distance = distance;
            var bg = Quad("Background", go.transform, Vector3.zero, size, SameShoreAssets.I.uiPanel,
                          background ?? new Color(0.03f, 0.05f, 0.09f, 0.82f), 0);
            bg.transform.localPosition = new Vector3(0, 0, 0.002f);
            go.SetActive(false);
            return panel;
        }

        void OnEnable() { if (!Visible.Contains(this)) Visible.Add(this); }
        void OnDisable() => Visible.Remove(this);

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);

        public static GameObject Quad(string name, Transform parent, Vector3 localPos, Vector2 size, Material mat, Color color, int queueOffset)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.localPosition = localPos;
            q.transform.localScale = new Vector3(size.x, size.y, 1);
            // the quad (and TMP text) face -Z, i.e. towards the head, because LazyFollow points +Z away from it
            var r = q.GetComponent<MeshRenderer>();
            var m = new Material(mat) { color = color };
            m.renderQueue = mat.renderQueue + queueOffset;
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return q;
        }

        public TextMeshPro AddText(string text, float size, Vector2 pos, Vector2 box, Color color,
                                   FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(pos.x, pos.y, -0.001f);
            var t = go.AddComponent<TextMeshPro>();
            if (SameShoreAssets.I.font != null) t.font = SameShoreAssets.I.font;
            t.fontSharedMaterial = TextMaterial(t.font);
            t.rectTransform.sizeDelta = box;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.text = text;
            return t;
        }

        static Material TextMaterial(TMP_FontAsset font)
        {
            if (_textMat == null && font != null)
            {
                _textMat = new Material(font.material);
                _textMat.SetFloat("unity_GUIZTestMode", (float)UnityEngine.Rendering.CompareFunction.Always);
                _textMat.renderQueue = 4300;
            }
            return _textMat;
        }

        public UIButton AddButton(string label, string subLabel, Vector2 pos, Vector2 size, Action onClick,
                                  XRInput.KeyId? hotkey = null)
        {
            var go = new GameObject("Button_" + label);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(pos.x, pos.y, -0.004f);
            var b = go.AddComponent<UIButton>();
            b.Build(this, label, subLabel, size, onClick, hotkey);
            _buttons.Add(b);
            return b;
        }

        public void ClearButtons()
        {
            foreach (var b in _buttons) if (b != null) Destroy(b.gameObject);
            _buttons.Clear();
        }
    }

    /// <summary>
    /// World-space button. Clicked by the XR controller laser (VRPointer) or, in Desktop Testing mode,
    /// by the mouse through the EventSystem (PhysicsRaycaster + Input System UI module).
    /// </summary>
    public class UIButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        static readonly Color Normal = new Color(0.12f, 0.2f, 0.3f, 0.95f);
        static readonly Color Hover = new Color(0.22f, 0.5f, 0.62f, 1f);

        public static int UILayer => LayerMask.NameToLayer("UI") >= 0 ? LayerMask.NameToLayer("UI") : 5;
        public string Label { get; private set; }
        public bool IsHovered => Time.unscaledTime < _hoverUntil || _pointerOver;
        bool _pointerOver;

        public void OnPointerEnter(PointerEventData e) => _pointerOver = true;
        public void OnPointerExit(PointerEventData e) => _pointerOver = false;

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) Click();
        }

        void OnDisable() => _pointerOver = false;

        Action _onClick;
        XRInput.KeyId? _hotkey;
        Material _mat;
        float _hoverUntil;
        Vector3 _baseScale;

        public void Build(UIPanel panel, string label, string sub, Vector2 size, Action onClick, XRInput.KeyId? hotkey)
        {
            _onClick = onClick;
            _hotkey = hotkey;
            Label = label;
            // built-in "UI" layer: the desktop PhysicsRaycaster only looks at buttons, so world triggers
            // in front of a panel (e.g. the sea goal at hatchling scale) never steal the pointer
            gameObject.layer = UILayer;
            var bg = UIPanel.Quad("Bg", transform, Vector3.zero, size, SameShoreAssets.I.uiPanel, Normal, 10);
            _mat = bg.GetComponent<MeshRenderer>().sharedMaterial;
            var col = gameObject.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(size.x, size.y, 0.02f);
            float y = string.IsNullOrEmpty(sub) ? 0 : size.y * 0.16f;
            var t = panel.AddText(label, 0.62f, Vector2.zero, new Vector2(size.x * 0.95f, size.y * 0.5f), Color.white, FontStyles.Bold);
            t.transform.SetParent(transform, false);
            t.transform.localPosition = new Vector3(0, y, -0.002f);
            if (!string.IsNullOrEmpty(sub))
            {
                var s = panel.AddText(sub, 0.34f, Vector2.zero, new Vector2(size.x * 0.92f, size.y * 0.4f), new Color(0.75f, 0.85f, 0.92f));
                s.transform.SetParent(transform, false);
                s.transform.localPosition = new Vector3(0, -size.y * 0.22f, -0.002f);
            }
            _baseScale = transform.localScale;
        }

        public void SetHover() => _hoverUntil = Time.unscaledTime + 0.05f;

        void Update()
        {
            bool hover = IsHovered;
            _mat.color = Color.Lerp(_mat.color, hover ? Hover : Normal, Time.unscaledDeltaTime * 14f);
            transform.localScale = Vector3.Lerp(transform.localScale, _baseScale * (hover ? 1.04f : 1f), Time.unscaledDeltaTime * 14f);
            if (_hotkey.HasValue && XRInput.KeyDown(_hotkey.Value)) Click();
        }

        public void Click()
        {
            AudioOneShot.Play2D(SameShoreAssets.I.click, 0.6f);
            _onClick?.Invoke();
        }
    }

    /// <summary>Controller laser for the world-space menus. Trigger = click. Only visible when a panel with buttons is open.</summary>
    public class VRPointerImpl : MonoBehaviour
    {
        public Hand hand = Hand.Right;
        public float maxDistance = 6f;
        LineRenderer _line;
        Transform _dot;

        protected void Awake()
        {
            _line = gameObject.AddComponent<LineRenderer>();
            _line.positionCount = 2;
            _line.useWorldSpace = true;
            _line.sharedMaterial = SameShoreAssets.I.uiLine;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.numCapVertices = 2;
            _line.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(new Color(0.7f, 0.9f, 1f), 0), new GradientColorKey(new Color(0.7f, 0.9f, 1f), 1) },
                alphaKeys = new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0.0f, 1) }
            };
            _line.enabled = false;
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(dot.GetComponent<Collider>());
            dot.GetComponent<MeshRenderer>().sharedMaterial = SameShoreAssets.I.uiLine;
            _dot = dot.transform;
            _dot.SetParent(transform, false);
            _dot.gameObject.SetActive(false);
        }

        protected void Update()
        {
            Transform anchor = PlayerRig.Hand(hand);
            bool active = anchor != null && UIPanel.Visible.Count > 0 && OVRInput.IsControllerConnected(
                hand == Hand.Left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch);
            _line.enabled = active;
            _dot.gameObject.SetActive(false);
            if (!active) return;

            float s = PlayerRig.Scale;
            Vector3 o = anchor.position;
            Vector3 d = anchor.forward;
            float len = maxDistance * s;
            UIButton best = null;
            RaycastHit[] hits = Physics.RaycastAll(o, d, len, ~0, QueryTriggerInteraction.Collide);
            float bestDist = len;
            foreach (var h in hits)
            {
                var b = h.collider.GetComponent<UIButton>();
                if (b != null && h.distance < bestDist) { best = b; bestDist = h.distance; }
            }
            _line.widthMultiplier = 0.004f * s;
            _line.SetPosition(0, o);
            _line.SetPosition(1, o + d * (best != null ? bestDist : len * 0.35f));
            if (best != null)
            {
                best.SetHover();
                _dot.gameObject.SetActive(true);
                _dot.position = o + d * bestDist;
                _dot.localScale = Vector3.one * 0.015f * s / Mathf.Max(transform.lossyScale.x, 1e-5f);
                if (XRInput.TriggerDown(hand))
                {
                    XRInput.Haptic(hand, 0.4f, 0.05f);
                    best.Click();
                }
            }
        }
    }

    /// <summary>Fire-and-forget audio helpers.</summary>
    public static class AudioOneShot
    {
        public static void Play2D(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            var go = new GameObject("~sfx2D");
            var a = go.AddComponent<AudioSource>();
            a.clip = clip;
            a.volume = volume;
            a.spatialBlend = 0;
            a.Play();
            UnityEngine.Object.Destroy(go, clip.length + 0.1f);
        }

        public static void PlayAt(AudioClip clip, Vector3 pos, float volume = 1f, float minDistance = 1f, float maxDistance = 30f, float pitch = 1f)
        {
            if (clip == null) return;
            var go = new GameObject("~sfx3D");
            go.transform.position = pos;
            var a = go.AddComponent<AudioSource>();
            a.clip = clip;
            a.volume = volume;
            a.pitch = pitch;
            a.spatialBlend = 1;
            a.rolloffMode = AudioRolloffMode.Logarithmic;
            a.minDistance = minDistance;
            a.maxDistance = maxDistance;
            a.dopplerLevel = 0;
            a.Play();
            UnityEngine.Object.Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
        }
    }
}
