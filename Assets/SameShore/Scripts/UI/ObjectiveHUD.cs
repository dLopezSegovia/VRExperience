using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SameShore
{
    /// <summary>
    /// World-space Canvas listing the objectives in the upper-left of the view. It follows the head
    /// lazily (yaw only, smoothed) so it stays readable without being head-locked, scales with the
    /// player (human / hatchling) and never covers the centre of vision.
    /// Toggle: left thumbstick click (Quest) or H (desktop).
    /// </summary>
    public class ObjectiveHUD : MonoBehaviour
    {
        // layout is written in "pixels"; U converts them to canvas units of 1 dm. TextMeshProUGUI encodes the
        // world scale for its SDF, and at hatchling size (x0.05) a 1 mm canvas unit made the text vanish.
        const float U = 0.01f;
        const float PixelsPerMetre = 10f;
        const float Width = 430f;
        const float RowHeight = 44f;

        public Vector3 offset = new Vector3(-0.42f, 0.2f, 1.15f);   // metres, in the head's yaw frame
        public float followSpeed = 4f;

        public bool Visible
        {
            get => _visible;
            set { _visible = value; ApplyVisibility(); }
        }

        /// <summary>Temporarily hidden by cinematics/menus without changing the player's choice.</summary>
        public bool Suppressed
        {
            get => _suppressed;
            set { _suppressed = value; ApplyVisibility(); }
        }

        class Row
        {
            public Objective O;
            public RectTransform Root;
            public Image Box, Mark, Arrow;
            public TextMeshProUGUI Label, Progress;
            public float Pulse;
        }

        Canvas _canvas;
        RectTransform _panel;
        TextMeshProUGUI _title, _footer;
        readonly List<Row> _rows = new List<Row>();
        bool _visible = true, _suppressed;
        CanvasGroup _group;
        float _alpha;
        float _yaw;
        Objective _current;
        Vector3? _currentTarget;

        static Sprite _box, _check, _cross, _arrow, _rounded;

        void Awake()
        {
            BuildSprites();
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 50;
            gameObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4;
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(Width, 200) * U;
            _panel = NewRect("Panel", transform);
            var bg = _panel.gameObject.AddComponent<Image>();
            bg.sprite = _rounded;
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = 200f;
            bg.color = new Color(0.02f, 0.035f, 0.06f, 0.62f);
            _title = Text("Title", _panel, 25, FontStyles.Bold, new Color(0.85f, 0.93f, 1f));
            _title.characterSpacing = 8;
            _footer = Text("Footer", _panel, 15, FontStyles.Normal, new Color(0.65f, 0.72f, 0.8f, 0.8f));
            _footer.text = DesktopTestingMode.Active ? "H: hide / show" : "Left stick click: hide / show";
            gameObject.SetActive(false);
        }

        public void Build(string title, IReadOnlyList<Objective> list)
        {
            foreach (var r in _rows) Destroy(r.Root.gameObject);
            _rows.Clear();
            _title.text = title;
            float h = 58 + list.Count * RowHeight + 26;
            ((RectTransform)transform).sizeDelta = new Vector2(Width, h) * U;
            Place(_panel, 0, 0, Width, h);
            Place(_title.rectTransform, 18, 14, Width - 36, 30);
            Place(_footer.rectTransform, 18, h - 24, Width - 36, 18);
            for (int i = 0; i < list.Count; i++)
            {
                var row = new Row { O = list[i] };
                row.Root = NewRect("Row" + i, _panel);
                Place(row.Root, 14, 52 + i * RowHeight, Width - 28, RowHeight);
                row.Box = Img("Box", row.Root, _box, Color.white);
                Place(row.Box.rectTransform, 4, 9, 22, 22);
                row.Mark = Img("Mark", row.Box.rectTransform, _check, new Color(0.55f, 0.95f, 0.7f));
                Place(row.Mark.rectTransform, -2, -4, 28, 28);
                row.Label = Text("Label", row.Root, 21, FontStyles.Normal, Color.white);
                Place(row.Label.rectTransform, 36, 6, Width - 130, 30);
                row.Progress = Text("Progress", row.Root, 17, FontStyles.Normal, new Color(0.75f, 0.82f, 0.9f));
                row.Progress.alignment = TextAlignmentOptions.MidlineRight;
                Place(row.Progress.rectTransform, Width - 130, 6, 70, 30);
                row.Arrow = Img("Arrow", row.Root, _arrow, new Color(1f, 0.85f, 0.45f));
                Place(row.Arrow.rectTransform, Width - 56, 10, 20, 20);
                _rows.Add(row);
            }
            Refresh(list);
            Snap();
        }

        public void SetCurrent(Objective current, Vector3? target)
        {
            _current = current;
            _currentTarget = target;
        }

        public void Refresh(IReadOnlyList<Objective> list)
        {
            foreach (var r in _rows)
            {
                var o = r.O;
                bool isCurrent = o == _current;
                string progress = o.Progress != null && o.State != ObjectiveState.Done ? o.Progress() : "";
                r.Progress.text = progress;
                switch (o.State)
                {
                    case ObjectiveState.Done:
                        r.Label.text = $"<color=#9fb3a8>{o.Title}</color>";
                        r.Mark.sprite = _check;
                        r.Mark.color = new Color(0.55f, 0.95f, 0.7f);
                        r.Mark.enabled = true;
                        r.Box.color = new Color(0.55f, 0.95f, 0.7f, 0.9f);
                        break;
                    case ObjectiveState.Left:
                        r.Label.text = $"<color=#d9a46a>{o.Title}</color>  <size=75%><color=#b88a5c>left behind</color></size>";
                        r.Mark.sprite = _cross;
                        r.Mark.color = new Color(0.9f, 0.6f, 0.35f);
                        r.Mark.enabled = true;
                        r.Box.color = new Color(0.9f, 0.6f, 0.35f, 0.9f);
                        break;
                    default:
                        r.Label.text = isCurrent ? $"<b>{o.Title}</b>" : $"<color=#c9d3dc>{o.Title}</color>";
                        r.Mark.enabled = false;
                        r.Box.color = isCurrent ? Color.white : new Color(1, 1, 1, 0.6f);
                        break;
                }
                r.Arrow.enabled = isCurrent && _currentTarget.HasValue;
                if (r.Arrow.enabled && PlayerRig.Head != null)
                {
                    Vector3 d = _currentTarget.Value - PlayerRig.Head.position;
                    d.y = 0;
                    float a = Vector3.SignedAngle(PlayerRig.FlatForward, d, Vector3.up);
                    r.Arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, -a);
                }
                // confirmation pulse
                float k = Mathf.Clamp01((Time.time - o.StateChangedAt) / 0.6f);
                float s = o.State == ObjectiveState.Pending ? 1f : 1f + 0.35f * Mathf.Sin(k * Mathf.PI) * (1 - k);
                r.Box.rectTransform.localScale = Vector3.one * s;
                r.Label.rectTransform.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(k * Mathf.PI) * (1 - k));
            }
        }

        public void Animate(Objective o)
        {
            // the pulse is computed in Refresh from Objective.StateChangedAt
        }

        void Update()
        {
            if (XRInput.KeyDown(XRInput.KeyId.H) || OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick))
                Visible = !Visible;
        }

        void LateUpdate()
        {
            Transform head = PlayerRig.Head;
            if (head == null) return;
            float targetYaw = head.eulerAngles.y;
            _yaw = Mathf.LerpAngle(_yaw, targetYaw, 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime));
            float s = PlayerRig.Scale;
            Quaternion yawRot = Quaternion.Euler(0, _yaw, 0);
            Vector3 pos = head.position + yawRot * offset * s;
            transform.position = pos;
            Vector3 look = pos - head.position;
            transform.rotation = Quaternion.LookRotation(look, Vector3.up);
            transform.localScale = Vector3.one * (s / PixelsPerMetre);
            // fade in/out
            float target = _visible && !_suppressed ? 1f : 0f;
            _alpha = Mathf.MoveTowards(_alpha, target, Time.unscaledDeltaTime * 3f);
            _group.alpha = _alpha;
        }

        void Snap()
        {
            if (PlayerRig.Head != null) _yaw = PlayerRig.Head.eulerAngles.y;
        }

        void ApplyVisibility()
        {
            if (_rows.Count > 0 && !gameObject.activeSelf) { gameObject.SetActive(true); Snap(); }
        }

        // ------------------------------------------------------------------ building helpers
        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            return rt;
        }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y) * U;
            rt.sizeDelta = new Vector2(w, h) * U;
            if (rt.parent is RectTransform p && p.GetComponent<Canvas>() != null)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            }
        }

        static TextMeshProUGUI Text(string name, Transform parent, float size, FontStyles style, Color color)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (SameShoreAssets.I.font != null) t.font = SameShoreAssets.I.font;
            t.fontSize = size * U;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            return t;
        }

        static Image Img(string name, Transform parent, Sprite sprite, Color color)
        {
            var rt = NewRect(name, parent);
            var i = rt.gameObject.AddComponent<Image>();
            i.sprite = sprite;
            i.color = color;
            i.raycastTarget = false;
            return i;
        }

        static void BuildSprites()
        {
            if (_box != null) return;
            _box = Draw(64, (x, y) =>
            {
                float d = Mathf.Max(Mathf.Abs(x - 31.5f), Mathf.Abs(y - 31.5f));
                return Mathf.Clamp01(1 - Mathf.Abs(d - 27f) / 2.5f);
            });
            _check = Draw(64, (x, y) =>
            {
                float d1 = DistSeg(x, y, 12, 33, 26, 18), d2 = DistSeg(x, y, 26, 18, 53, 48);
                return Mathf.Clamp01(1 - (Mathf.Min(d1, d2) - 3.5f) / 1.5f);
            });
            _cross = Draw(64, (x, y) =>
            {
                float d = DistSeg(x, y, 16, 32, 48, 32);
                return Mathf.Clamp01(1 - (d - 3.5f) / 1.5f);
            });
            _arrow = Draw(64, (x, y) =>
            {
                // upward chevron
                float d1 = DistSeg(x, y, 14, 22, 32, 46), d2 = DistSeg(x, y, 32, 46, 50, 22);
                return Mathf.Clamp01(1 - (Mathf.Min(d1, d2) - 4f) / 1.5f);
            });
            Texture2D rr = SameShoreAssets.I.roundedRect;
            if (rr != null)
                _rounded = Sprite.Create(rr, new Rect(0, 0, rr.width, rr.height), new Vector2(0.5f, 0.5f), 100, 0,
                                         SpriteMeshType.FullRect, new Vector4(30, 30, 30, 30));
        }

        static float DistSeg(float px, float py, float ax, float ay, float bx, float by)
        {
            Vector2 p = new Vector2(px, py), a = new Vector2(ax, ay), b = new Vector2(bx, by);
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        static Sprite Draw(int size, System.Func<float, float, float> alpha)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                t.SetPixel(x, y, new Color(1, 1, 1, alpha(x, y)));
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
        }
    }
}
