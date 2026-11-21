using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// The little turtle the human photographs (Act I, scene 8). Crawls towards the sea in short strokes;
    /// freezes when the human comes close (as hatchlings do in a light beam).
    /// </summary>
    public class PhotoHatchling : MonoBehaviour
    {
        [SerializeField] private HatchlingAvatar avatar;
        [SerializeField] private Transform sea;
        [SerializeField] private float strokeDistance = 0.07f;
        [SerializeField] private float strokeDuration = 0.7f;
        [SerializeField] private float pause = 0.5f;

        bool _crawl, _frozen;
        float _t;

        public void StartCrawl(float delay)
        {
            _crawl = true;
            _frozen = false;
            _t = -delay;
        }

        public void Freeze(bool on) => _frozen = on;

        void Update()
        {
            if (!_crawl || _frozen) { if (avatar != null) avatar.Phase = -1f; return; }
            _t += Time.deltaTime;
            if (_t < 0) return;
            float cycle = strokeDuration + pause;
            float ph = (_t % cycle) / strokeDuration;
            if (avatar != null) avatar.Phase = ph <= 1f ? ph : -1f;
            if (ph > 1f || sea == null) return;
            Vector3 to = Vector3.ProjectOnPlane(sea.position - transform.position, Vector3.up).normalized;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to, Vector3.up), Time.deltaTime * 1.5f);
            float speed = strokeDistance / strokeDuration * 2f * Mathf.Sin(Mathf.PI * ph) * Mathf.Sin(Mathf.PI * ph);
            Vector3 p = transform.position + transform.forward * speed * Time.deltaTime;
            transform.position = MeshKit.OnGround(p);
        }
    }
}
