using UnityEngine;

namespace SameShore
{
    /// <summary>Litter bin: anything with a TrashItem dropped inside counts as removed.</summary>
    [RequireComponent(typeof(Collider))]
    public class TrashBin : MonoBehaviour
    {
        void Reset() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            var item = other.GetComponentInParent<TrashItem>();
            if (item == null || item.InBin) return;
            item.InBin = true;
            AudioOneShot.PlayAt(SameShoreAssets.I.thud, transform.position, 0.8f, 0.5f, 10f, 1.3f);
            XRInput.HapticBoth(0.25f, 0.06f);
        }

        void OnTriggerExit(Collider other)
        {
            var item = other.GetComponentInParent<TrashItem>();
            if (item != null) item.InBin = false;
        }
    }
}
