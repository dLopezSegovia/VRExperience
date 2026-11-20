using System.Collections;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// The ending: the hatchling reaches the foam, a wave builds, rushes up the sand and washes over
    /// its eyes; sound turns muffled and underwater, the view sinks into the sea and fades.
    /// The camera is never moved: the water moves, the player is inside it.
    /// </summary>
    public class FinalWave : MonoBehaviour
    {
        [SerializeField] private Material waterMaterial;
        [SerializeField] private Material foamMaterial;
        [SerializeField] private float width = 5f;
        [SerializeField] private float crestHeight = 0.16f;
        [SerializeField] private float travelSeconds = 2.6f;

        GameObject _wave;
        AudioSource _under;

        public IEnumerator Play(Transform turtleBody, Transform sea, AmbientAudio ambient)
        {
            Vector3 p = turtleBody.position;
            Vector3 toLand = sea != null ? Vector3.ProjectOnPlane(p - sea.position, Vector3.up) : Vector3.back;
            toLand.y = 0;
            toLand = toLand.sqrMagnitude > 0.01f ? new Vector3(0, 0, Mathf.Sign(toLand.z)) : Vector3.back;
            Quaternion rot = Quaternion.LookRotation(toLand, Vector3.up);

            _wave = new GameObject("FinalWave");
            _wave.AddComponent<MeshFilter>().sharedMesh = BuildMesh(width, crestHeight);
            var r = _wave.AddComponent<MeshRenderer>();
            r.sharedMaterials = new[] { waterMaterial, foamMaterial };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            AudioOneShot.Play2D(SameShoreAssets.I.swashWash, 0.8f);
            AudioOneShot.Play2D(SameShoreAssets.I.waveCrashBig, 1f);
            Vector3 start = p - toLand * 2.4f, end = p + toLand * 0.8f;
            for (float t = 0; t < travelSeconds; t += Time.deltaTime)
            {
                float k = t / travelSeconds;
                float ease = k * k * (3 - 2 * k);
                Vector3 pos = Vector3.Lerp(start, end, ease);
                pos.y = Ground(pos) - 0.01f;
                _wave.transform.SetPositionAndRotation(pos, rot);
                _wave.transform.localScale = new Vector3(1, Mathf.Lerp(0.35f, 1.15f, Mathf.Sin(k * Mathf.PI * 0.6f)), 1);
                yield return null;
            }

            // under the water
            ambient.Duck(0.35f, 0.8f);
            _under = gameObject.AddComponent<AudioSource>();
            _under.clip = SameShoreAssets.I.underwater;
            _under.loop = true;
            _under.spatialBlend = 0;
            _under.volume = 0.9f;
            _under.ignoreListenerVolume = true;
            _under.Play();
            var fader = ScreenFader.Instance;
            fader.SetColor(new Color(0.02f, 0.13f, 0.16f));
            yield return fader.FadeTo(0.82f, 0.9f);
            yield return new WaitForSeconds(1.6f);
            fader.SetColor(new Color(0.0f, 0.03f, 0.05f));
            yield return fader.FadeTo(1f, 1.6f);
            Destroy(_wave);
            fader.SetColor(Color.black);
        }

        public void StopUnderwater()
        {
            if (_under != null) { Destroy(_under); _under = null; }
        }

        static float Ground(Vector3 p) => TurtleJourney.Ground(p).y;

        /// <summary>Wave sheet: water body rising into a rounded crest that curls forward, foam on the lip.</summary>
        static Mesh BuildMesh(float width, float crest)
        {
            const int cols = 24;
            // profile along local +Z (landward) : (z, y) points from the back to the lip
            Vector2[] prof =
            {
                new Vector2(-1.6f, -0.02f), new Vector2(-1.1f, 0.25f), new Vector2(-0.6f, 0.6f), new Vector2(-0.25f, 0.88f),
                new Vector2(0f, 1f), new Vector2(0.12f, 0.97f), new Vector2(0.2f, 0.85f), new Vector2(0.22f, 0.7f),
                new Vector2(0.18f, 0.55f), new Vector2(0.3f, 0.2f), new Vector2(0.55f, 0.02f)
            };
            int rows = prof.Length;
            var v = new Vector3[(cols + 1) * rows];
            var uv = new Vector2[v.Length];
            for (int r = 0; r < rows; r++)
            for (int c = 0; c <= cols; c++)
            {
                float x = (c / (float)cols - 0.5f) * width;
                float wob = 0.12f * Mathf.Sin(c * 0.9f) * (prof[r].y);
                v[r * (cols + 1) + c] = new Vector3(x, prof[r].y * crest, prof[r].x * crest * 4f + wob * crest);
                uv[r * (cols + 1) + c] = new Vector2(c / (float)cols, r / (float)(rows - 1));
            }
            var water = new System.Collections.Generic.List<int>();
            var foam = new System.Collections.Generic.List<int>();
            for (int r = 0; r < rows - 1; r++)
            for (int c = 0; c < cols; c++)
            {
                int i = r * (cols + 1) + c;
                var list = r >= 3 ? foam : water;
                list.AddRange(new[] { i, i + cols + 1, i + 1, i + 1, i + cols + 1, i + cols + 2 });
            }
            var m = new Mesh { name = "FinalWave" };
            m.vertices = v;
            m.uv = uv;
            m.subMeshCount = 2;
            m.SetTriangles(water, 0);
            m.SetTriangles(foam, 1);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
