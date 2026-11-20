using System.Collections.Generic;
using UnityEngine;

namespace SameShore
{
    /// <summary>A piece of litter. It counts as removed when it is in a bin or out of the hatchlings' path.</summary>
    public class TrashItem : MonoBehaviour
    {
        public static readonly List<TrashItem> All = new List<TrashItem>();

        [SerializeField] private string label = "bottle";

        public string Label => label;
        public bool InBin { get; set; }
        public bool Cleared => InBin || !BeachStateTracker.InPath(transform.position);

        void Start()
        {
            var rb = GetComponent<Rigidbody>();
            if (rb != null) rb.Sleep();
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);
    }
}
