using System.Text;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// Reads the state the human left the beach in. Nothing is copied: both phases share the same
    /// scene objects, so every change persists automatically. This only summarises it.
    /// </summary>
    public class BeachStateTracker : MonoBehaviour
    {
        public static BeachStateTracker Instance { get; private set; }

        [SerializeField] private Transform nest;
        [SerializeField] private Transform seaPoint;
        [Tooltip("Half width of the nest->sea corridor the hatchlings cross.")]
        [SerializeField] private float pathHalfWidth = 3.2f;
        [SerializeField] private Flashlight flashlight;
        [SerializeField] private SandHole hole;
        [SerializeField] private Transform chair;

        Vector3 _chairStart;

        void Awake()
        {
            Instance = this;
            if (chair != null) _chairStart = chair.position;
        }

        public static bool InPath(Vector3 p)
        {
            if (Instance == null || Instance.nest == null || Instance.seaPoint == null) return true;
            Vector3 a = Instance.nest.position, b = Instance.seaPoint.position;
            Vector2 a2 = new Vector2(a.x, a.z), b2 = new Vector2(b.x, b.z), p2 = new Vector2(p.x, p.z);
            Vector2 ab = b2 - a2;
            float t = Mathf.Clamp01(Vector2.Dot(p2 - a2, ab) / ab.sqrMagnitude);
            float dist = Vector2.Distance(p2, a2 + ab * t);
            return dist < Instance.pathHalfWidth && p.y > a.y - 30f;
        }

        public int TrashTotal => TrashItem.All.Count;

        public int TrashCleared
        {
            get
            {
                int n = 0;
                foreach (var t in TrashItem.All) if (t.Cleared) n++;
                return n;
            }
        }

        public bool FlashlightOn => flashlight != null && flashlight.IsOn;
        public bool HoleFilled => hole == null || hole.IsFilled;
        public bool ChairMoved => chair != null && (chair.position - _chairStart).magnitude > 0.5f;

        public string Summary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"You cleared <b>{TrashCleared} of {TrashTotal}</b> pieces of litter from the path to the sea.");
            sb.AppendLine(FlashlightOn ? "The flashlight is still <b>on</b>, glowing on the sand." : "You switched the flashlight <b>off</b>.");
            sb.AppendLine(HoleFilled ? "You <b>filled</b> the sandcastle hole." : "The sandcastle hole is still <b>open</b>.");
            if (ChairMoved) sb.AppendLine("You moved the beach chair.");
            return sb.ToString().TrimEnd();
        }
    }
}
