using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// The fenced nest. When the human comes close the first hatchlings break the surface
    /// ("User can see the nest hatchlings start to hatch"); in the hatchling phase the
    /// siblings set off for the sea together with the player.
    /// </summary>
    public class NestController : MonoBehaviour
    {
        [SerializeField] private HatchlingSibling[] siblings;
        [SerializeField] private float humanHatchRadius = 3f;
        [SerializeField] private int hatchOnHumanVisit = 2;

        bool _humanHatched, _crawling;

        void Start()
        {
            if (GameManager.Instance != null) GameManager.Instance.OnStateChanged += OnState;
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= OnState;
        }

        void OnState(GameState prev, GameState next)
        {
            if (next == GameState.Hatchling && !_crawling)
            {
                _crawling = true;
                for (int i = 0; i < siblings.Length; i++)
                    if (siblings[i] != null) siblings[i].StartCrawl(1.5f + i * Random.Range(1.2f, 2.5f));
            }
        }

        void Update()
        {
            if (_humanHatched || GameManager.Instance == null || GameManager.Instance.Current != GameState.Human) return;
            Transform head = PlayerRig.Head;
            if (head == null) return;
            Vector3 d = head.position - transform.position;
            d.y = 0;
            if (d.magnitude > humanHatchRadius) return;
            _humanHatched = true;
            for (int i = 0; i < Mathf.Min(hatchOnHumanVisit, siblings.Length); i++)
                if (siblings[i] != null) siblings[i].Emerge(i * 1.5f);
        }
    }
}
