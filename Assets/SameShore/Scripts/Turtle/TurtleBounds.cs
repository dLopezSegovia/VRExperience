using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// Safety limits of the hatchling's corridor. They sit inside the crests of the dune berms (under the grass,
    /// rocks and driftwood that line the way), so they are never seen; and they are only solid in the hatchling's
    /// act, so the human walks over the low berms freely during the day.
    /// </summary>
    public class TurtleBounds : MonoBehaviour
    {
        Collider[] _cols;
        bool _on = true;

        void Awake()
        {
            _cols = GetComponentsInChildren<Collider>(true);
            Set(false);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            Set(gm != null && gm.Current == GameState.Hatchling);
        }

        void Set(bool on)
        {
            if (on == _on) return;
            _on = on;
            foreach (var c in _cols) if (c != null) c.enabled = on;
        }
    }
}
