using System.Collections;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// The fenced nest. When the human returns to it the clutch starts to hatch: scratching under the
    /// sand, the surface trembles and sheds grains, and a first hatchling pushes its head out
    /// ("User can see the nest hatchlings start to hatch"). In the hatchling phase the siblings set
    /// off for the sea together with the player, with the same attraction to light.
    /// </summary>
    public class NestController : MonoBehaviour
    {
        [SerializeField] private HatchlingSibling[] siblings;
        [SerializeField] private Transform mound;
        [SerializeField] private AudioSource scratchSource;

        bool _hatched, _crawling;
        ParticleSystem _grains;

        void Start()
        {
            if (GameManager.Instance != null) GameManager.Instance.OnStateChanged += OnState;
            BuildGrains();
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
                    if (siblings[i] != null) siblings[i].StartCrawl(4f + i * Random.Range(1.5f, 3f));
            }
        }

        /// <summary>The hatching moment seen by the human (about 5 seconds).</summary>
        public IEnumerator HatchSequence()
        {
            if (scratchSource != null && SameShoreAssets.I.nestScratching != null)
            {
                scratchSource.clip = SameShoreAssets.I.nestScratching;
                scratchSource.Play();
            }
            Vector3 baseScale = mound != null ? mound.localScale : Vector3.one;
            for (float t = 0; t < 2.2f; t += Time.deltaTime)
            {
                if (mound != null) mound.localScale = new Vector3(baseScale.x, baseScale.y * (1f + 0.06f * Mathf.Sin(t * 23f) * Mathf.Sin(t * 1.4f)), baseScale.z);
                if (Random.value < 0.25f) _grains.Emit(3);
                yield return null;
            }
            if (mound != null) mound.localScale = baseScale;
            _grains.Emit(40);
            if (!_hatched)
            {
                _hatched = true;
                for (int i = 0; i < Mathf.Min(2, siblings.Length); i++)
                    if (siblings[i] != null) siblings[i].Emerge(i * 1.2f);
            }
            yield return new WaitForSeconds(2.8f);
        }

        void BuildGrains()
        {
            var go = new GameObject("NestGrains");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 0.03f;
            _grains = go.AddComponent<ParticleSystem>();
            _grains.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _grains.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.012f);
            main.startColor = new Color(0.62f, 0.54f, 0.42f);
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            main.playOnAwake = false;
            var em = _grains.emission;
            em.rateOverTime = 0;
            var shape = _grains.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.12f;
            go.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var col = _grains.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.World;
            col.dampen = 0.7f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = SameShoreAssets.I.sandParticle;
            _grains.Play();
        }
    }
}
