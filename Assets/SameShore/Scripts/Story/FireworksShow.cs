using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SameShore
{
    // =====================================================================================================
    // Scene 5: fireworks
    // =====================================================================================================

    /// <summary>
    /// A small fireworks battery in a roped-off safe zone, fired with the button on its remote. Shells rise
    /// over the sea and burst (pooled particle systems), each with a short light flash and a streak of
    /// reflected light on the water. Sound travels at the speed of sound. Spent tubes keep smoking.
    /// </summary>
    public class FireworksShow : MonoBehaviour
    {
        [SerializeField] private PokeButton launchButton;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Transform seaCenter;
        [SerializeField] private int shells = 9;
        [SerializeField] private float showSeconds = 16f;
        [SerializeField] private GameObject spentVisual;
        [SerializeField] private Material particleMaterial;
        [SerializeField] private Material reflectionMaterial;
        [SerializeField] private float waterLevel = 0.31f;

        public bool Started { get; private set; }
        public bool Finished { get; private set; }
        public int Launched { get; private set; }
        public Transform Button => launchButton != null ? launchButton.transform : transform;

        public event Action<Vector3> Burst;

        static readonly Color[] Palette =
        {
            new Color(1f, 0.45f, 0.25f), new Color(1f, 0.85f, 0.35f), new Color(0.45f, 0.75f, 1f),
            new Color(0.95f, 0.4f, 0.85f), new Color(0.5f, 1f, 0.6f), new Color(1f, 1f, 0.95f)
        };

        readonly List<ParticleSystem> _bursts = new List<ParticleSystem>();
        int _next;
        ParticleSystem _smoke;
        Light _flash;
        GameObject _reflection;

        void Start()
        {
            if (launchButton != null) launchButton.Pressed += () => { if (!Started) StartCoroutine(Show()); };
            for (int i = 0; i < 4; i++) _bursts.Add(MakeBurst());
            _flash = new GameObject("FireworkFlash").AddComponent<Light>();
            _flash.transform.SetParent(transform, false);
            _flash.type = LightType.Point;
            _flash.range = 70f;
            _flash.intensity = 0;
            _flash.shadows = LightShadows.None;
            _flash.enabled = false;
            if (spentVisual != null) spentVisual.SetActive(false);
        }

        public void Launch()
        {
            if (!Started) StartCoroutine(Show());
        }

        IEnumerator Show()
        {
            Started = true;
            if (launchButton != null) launchButton.interactable = false;
            BeachLog.I?.Record("fireworks show started", transform.position);
            if (BeachLog.I != null) BeachLog.I.FireworksPosition = transform.position;
            yield return new WaitForSeconds(0.8f);
            for (int i = 0; i < shells; i++)
            {
                StartCoroutine(Shell(i));
                yield return new WaitForSeconds(showSeconds / shells * UnityEngine.Random.Range(0.6f, 1.25f));
            }
            yield return new WaitForSeconds(3.5f);
            Finished = true;
            if (spentVisual != null) spentVisual.SetActive(true);
            StartSmoke();
            BeachLog.I?.Record("fireworks finished (spent battery left smoking)", transform.position);
        }

        IEnumerator Shell(int i)
        {
            Launched++;
            if (BeachLog.I != null) BeachLog.I.FireworksLaunched = Launched;
            Vector3 from = muzzle != null ? muzzle.position : transform.position + Vector3.up * 0.3f;
            Vector3 sea = seaCenter != null ? seaCenter.position : from + Vector3.forward * 25f;
            Vector3 target = sea + new Vector3(UnityEngine.Random.Range(-14f, 14f), 0, UnityEngine.Random.Range(-4f, 8f));
            target.y = from.y + UnityEngine.Random.Range(22f, 34f);
            AudioOneShot.PlayAt(SameShoreAssets.I.Clip("firework_launch"), from, 0.9f, 2f, 60f);
            // rising spark
            var spark = new GameObject("Spark");
            var tr = spark.AddComponent<TrailRenderer>();
            tr.time = 0.5f;
            tr.startWidth = 0.18f;
            tr.endWidth = 0f;
            tr.sharedMaterial = particleMaterial;
            tr.startColor = new Color(1f, 0.85f, 0.6f, 1f);
            tr.endColor = new Color(1f, 0.5f, 0.2f, 0f);
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            float rise = UnityEngine.Random.Range(1.3f, 1.7f);
            Vector3 mid = Vector3.Lerp(from, target, 0.5f) + Vector3.up * 4f;
            for (float t = 0; t < rise; t += Time.deltaTime)
            {
                float k = t / rise;
                float e = 1 - (1 - k) * (1 - k);
                Vector3 a = Vector3.Lerp(from, mid, e), b = Vector3.Lerp(mid, target, e);
                spark.transform.position = Vector3.Lerp(a, b, e);
                yield return null;
            }
            Destroy(spark, 0.6f);
            Explode(target, Palette[(i * 2 + UnityEngine.Random.Range(0, 2)) % Palette.Length]);
        }

        void Explode(Vector3 p, Color c)
        {
            var ps = _bursts[_next++ % _bursts.Count];
            ps.transform.position = p;
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(c, Color.Lerp(c, Color.white, 0.5f));
            ps.Emit(140);
            StartCoroutine(FlashLight(p, c));
            StartCoroutine(Reflect(p, c));
            Burst?.Invoke(p);
            // sound arrives later: speed of sound from the burst to the listener
            float delay = PlayerRig.Head != null ? (p - PlayerRig.Head.position).magnitude / 343f : 0.1f;
            StartCoroutine(Delayed(delay, () => AudioOneShot.PlayAt(SameShoreAssets.I.Clip("firework_burst"), p, 1f, 25f, 400f)));
        }

        IEnumerator Delayed(float s, Action a)
        {
            yield return new WaitForSeconds(s);
            a();
        }

        IEnumerator FlashLight(Vector3 p, Color c)
        {
            _flash.transform.position = p;
            _flash.color = c;
            _flash.enabled = true;
            for (float t = 0; t < 1.1f; t += Time.deltaTime)
            {
                _flash.intensity = 6f * Mathf.Exp(-t * 4f);
                yield return null;
            }
            _flash.enabled = false;
        }

        IEnumerator Reflect(Vector3 p, Color c)
        {
            if (reflectionMaterial == null) yield break;
            if (_reflection == null)
            {
                _reflection = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(_reflection.GetComponent<Collider>());
                _reflection.name = "FireworkReflection";
                _reflection.GetComponent<MeshRenderer>().sharedMaterial = new Material(reflectionMaterial);
                _reflection.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var mat = _reflection.GetComponent<MeshRenderer>().sharedMaterial;
            Vector3 eye = PlayerRig.Head != null ? PlayerRig.Head.position : p - Vector3.forward * 20f;
            Vector3 onWater = new Vector3(p.x, waterLevel + 0.03f, p.z);
            Vector3 toEye = Vector3.ProjectOnPlane(eye - onWater, Vector3.up).normalized;
            // a streak on the water from below the burst towards the viewer
            _reflection.transform.position = onWater + toEye * 7f;
            _reflection.transform.rotation = Quaternion.LookRotation(Vector3.down, toEye);
            _reflection.transform.localScale = new Vector3(3.5f, 16f, 1f);
            _reflection.SetActive(true);
            for (float t = 0; t < 1.4f; t += Time.deltaTime)
            {
                Color k = c * (1.6f * Mathf.Exp(-t * 2.6f));
                k.a = Mathf.Exp(-t * 2.6f);
                mat.SetColor("_BaseColor", k);
                yield return null;
            }
            _reflection.SetActive(false);
        }

        ParticleSystem MakeBurst()
        {
            var go = new GameObject("FireworkBurst");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(7f, 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.gravityModifier = 0.25f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;
            main.playOnAwake = false;
            var em = ps.emission;
            em.rateOverTime = 0;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;
            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 2.2f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.9f, 0.5f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var trails = ps.trails;
            trails.enabled = true;
            trails.lifetime = 0.25f;
            trails.widthOverTrail = 0.35f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = particleMaterial;
            r.trailMaterial = particleMaterial;
            ps.Play();
            return ps;
        }

        void StartSmoke()
        {
            var go = new GameObject("SpentSmoke");
            go.transform.SetParent(transform, false);
            go.transform.position = (muzzle != null ? muzzle.position : transform.position) + Vector3.up * 0.05f;
            go.transform.rotation = Quaternion.Euler(-90, 0, 0);
            _smoke = go.AddComponent<ParticleSystem>();
            _smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _smoke.main;
            main.startLifetime = 4f;
            main.startSpeed = 0.15f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startColor = new Color(0.55f, 0.55f, 0.55f, 0.18f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;
            var em = _smoke.emission;
            em.rateOverTime = 4f;
            var shape = _smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.06f;
            var sz = _smoke.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 2.2f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = SameShoreAssets.I.sandParticle;
            _smoke.Play();
        }

        /// <summary>Skip / defaults: the show happened (spent battery on the sand).</summary>
        public void ForceComplete()
        {
            StopAllCoroutines();
            Started = Finished = true;
            Launched = Mathf.Max(Launched, shells);
            if (BeachLog.I != null) { BeachLog.I.FireworksLaunched = Launched; BeachLog.I.FireworksPosition = transform.position; }
            if (spentVisual != null) spentVisual.SetActive(true);
            if (launchButton != null) launchButton.interactable = false;
            if (_smoke == null) StartSmoke();
        }
    }
}
