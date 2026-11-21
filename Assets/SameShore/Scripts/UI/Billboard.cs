using UnityEngine;

namespace SameShore
{
    /// <summary>Keeps a quad facing the player's head (hook markers, small labels).</summary>
    public class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            Transform h = PlayerRig.Head;
            if (h == null) return;
            Vector3 d = transform.position - h.position;
            if (d.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(d, Vector3.up);
        }
    }
}
