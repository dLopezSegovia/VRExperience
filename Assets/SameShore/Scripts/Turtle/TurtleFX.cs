using System.Collections.Generic;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// Makes the hatchling's scale and effort tangible:
    ///  - a crawl track pressed into the sand behind it (flipper marks + drag line),
    ///  - small sand puffs at the front flippers on every stroke,
    ///  - thousands of pebbles and shell grit around it (instanced): invisible to a standing human,
    ///    boulders at 8 cm eye height. Only drawn in the hatchling phase, only near the hatchling.
    /// </summary>
    public class TurtleFX : MonoBehaviour
    {
        [SerializeField] private TurtleController turtle;
        [SerializeField] private Transform flipperL, flipperR;
        [SerializeField] private float trackSpacing = 0.05f;
        [SerializeField] private int maxTracks = 400;
        [SerializeField] private float pebbleRadius = 3f;
        [SerializeField] private int pebbleCount = 2600;
        [SerializeField] private Vector2 pebbleSize = new Vector2(0.004f, 0.016f);
        [SerializeField] private Rect pebbleArea = new Rect(-6, -2, 12, 22);   // x offset, z from shore ... set by builder

        readonly Queue<Transform> _tracks = new Queue<Transform>();
        Vector3 _lastTrack;
        float _lastPhase = -1;
        ParticleSystem _puff;
        Material _trackMat;
        static Mesh _quad;

        List<Matrix4x4>[] _pebbleMatrices;
        Matrix4x4[] _batch = new Matrix4x4[1023];
        Vector3[] _pebblePos;
        int[] _pebbleMesh;
        Quaternion[] _pebbleRot;
        float[] _pebbleScale;
        bool _pebblesReady;

        public void SetPebbleArea(Rect worldXZ) => pebbleArea = worldXZ;

        void Start()
        {
            _trackMat = new Material(SameShoreAssets.I.groundDecal);
            if (_trackMat.HasProperty("_BaseMap")) _trackMat.SetTexture("_BaseMap", SameShoreAssets.I.turtleTrack);
            _trackMat.SetColor("_BaseColor", new Color(0.08f, 0.06f, 0.05f, 0.55f));
            BuildPuff();
            _lastTrack = turtle.transform.position;
        }

        bool Running => GameManager.Instance != null &&
                        (GameManager.Instance.Current == GameState.Hatchling || GameManager.Instance.Current == GameState.End);

        void Update()
        {
            if (!Running) return;
            if (!_pebblesReady) BuildPebbles();
            Tracks();
            Puffs();
            DrawPebbles();
        }

        // ------------------------------------------------------------------ track
        void Tracks()
        {
            Vector3 p = turtle.transform.position;
            Vector3 d = p - _lastTrack;
            d.y = 0;
            if (d.magnitude < trackSpacing) return;
            if (d.magnitude > 1f) { _lastTrack = p; return; } // teleport/respawn
            _lastTrack = p;
            if (!FootprintSystem.GroundHit(p + Vector3.up * 0.3f, out RaycastHit g)) return;
            if (_quad == null) _quad = FootprintSystem.BuildQuad();
            var go = new GameObject("CrawlTrack");
            go.transform.SetParent(transform, true);
            go.transform.SetPositionAndRotation(g.point + g.normal * 0.003f, Quaternion.LookRotation(Vector3.ProjectOnPlane(d, g.normal).normalized, g.normal));
            go.transform.localScale = new Vector3(0.11f, 1, trackSpacing * 1.3f);
            go.AddComponent<MeshFilter>().sharedMesh = _quad;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _trackMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _tracks.Enqueue(go.transform);
            while (_tracks.Count > maxTracks) Destroy(_tracks.Dequeue().gameObject);
        }

        public void ClearTracks()
        {
            while (_tracks.Count > 0) Destroy(_tracks.Dequeue().gameObject);
            _lastTrack = turtle.transform.position;
        }

        // ------------------------------------------------------------------ sand puffs
        void BuildPuff()
        {
            var go = new GameObject("SandPuff");
            go.transform.SetParent(transform, false);
            _puff = go.AddComponent<ParticleSystem>();
            _puff.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _puff.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.0015f, 0.004f);
            main.startColor = new Color(0.58f, 0.5f, 0.4f, 0.9f);
            main.gravityModifier = 0.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            main.playOnAwake = false;
            var em = _puff.emission;
            em.rateOverTime = 0;
            var shape = _puff.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.01f;
            var col = _puff.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.World;
            col.dampen = 0.6f;
            col.lifetimeLoss = 0.3f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = SameShoreAssets.I.sandParticle;
            _puff.Play();
        }

        void Puffs()
        {
            float phase = turtle.StrokePhase;
            // power stroke: flippers push sand back
            if (phase >= 0.3f && _lastPhase < 0.3f)
            {
                foreach (var f in new[] { flipperL, flipperR })
                {
                    if (f == null) continue;
                    _puff.transform.position = f.position + Vector3.up * 0.004f;
                    _puff.transform.rotation = Quaternion.LookRotation(-turtle.transform.forward + Vector3.up * 0.6f);
                    _puff.Emit(turtle.StrokeIsBurst ? 22 : 10);
                }
            }
            _lastPhase = phase;
        }

        // ------------------------------------------------------------------ pebbles (instanced)
        void BuildPebbles()
        {
            _pebblesReady = true;
            Mesh[] meshes = SameShoreAssets.I.pebbles;
            if (meshes == null || meshes.Length == 0 || SameShoreAssets.I.pebble == null) return;
            var rnd = new System.Random(5);
            _pebblePos = new Vector3[pebbleCount];
            _pebbleMesh = new int[pebbleCount];
            _pebbleRot = new Quaternion[pebbleCount];
            _pebbleScale = new float[pebbleCount];
            for (int i = 0; i < pebbleCount; i++)
            {
                float x = pebbleArea.xMin + (float)rnd.NextDouble() * pebbleArea.width;
                float z = pebbleArea.yMin + (float)rnd.NextDouble() * pebbleArea.height;
                Vector3 g = TurtleJourney.Ground(new Vector3(x, 0, z));
                float s = Mathf.Lerp(pebbleSize.x, pebbleSize.y, Mathf.Pow((float)rnd.NextDouble(), 2.5f));
                _pebblePos[i] = g + Vector3.up * s * 0.15f;
                _pebbleMesh[i] = rnd.Next(meshes.Length);
                _pebbleRot[i] = Quaternion.Euler((float)rnd.NextDouble() * 20 - 10, (float)rnd.NextDouble() * 360, (float)rnd.NextDouble() * 20 - 10);
                _pebbleScale[i] = s;
            }
            _pebbleMatrices = new List<Matrix4x4>[meshes.Length];
            for (int m = 0; m < meshes.Length; m++) _pebbleMatrices[m] = new List<Matrix4x4>(512);
        }

        void DrawPebbles()
        {
            if (_pebbleMatrices == null) return;
            Mesh[] meshes = SameShoreAssets.I.pebbles;
            Vector3 c = turtle.transform.position;
            float r2 = pebbleRadius * pebbleRadius;
            foreach (var l in _pebbleMatrices) l.Clear();
            for (int i = 0; i < _pebblePos.Length; i++)
            {
                Vector3 d = _pebblePos[i] - c;
                if (d.x * d.x + d.z * d.z > r2) continue;
                _pebbleMatrices[_pebbleMesh[i]].Add(Matrix4x4.TRS(_pebblePos[i], _pebbleRot[i], Vector3.one * _pebbleScale[i]));
            }
            var mat = SameShoreAssets.I.pebble;
            var rp = new RenderParams(mat) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = true };
            for (int m = 0; m < meshes.Length; m++)
            {
                var list = _pebbleMatrices[m];
                for (int start = 0; start < list.Count; start += 1023)
                {
                    int n = Mathf.Min(1023, list.Count - start);
                    list.CopyTo(start, _batch, 0, n);
                    Graphics.RenderMeshInstanced(rp, meshes[m], 0, _batch, n);
                }
            }
        }
    }
}
