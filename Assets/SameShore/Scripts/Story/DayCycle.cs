using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace SameShore
{
    /// <summary>
    /// Afternoon -> sunset -> night, driven by one value (0 = sunny afternoon, 1 = sunset, 2 = night).
    /// Interpolates the sun/moon light (direction, colour, intensity, shadows), the trilight ambient, the
    /// fog, the procedural sky (SameShore/SkyCycle), the ocean colours and the moon billboard.
    /// The camera is never touched: the world changes around the player.
    /// </summary>
    public class DayCycle : MonoBehaviour
    {
        public static DayCycle Instance { get; private set; }

        [SerializeField] private Light sun;
        [SerializeField] private Material skyMaterial;
        [SerializeField] private Renderer ocean;
        [SerializeField] private Renderer swash;
        [SerializeField] private Renderer moon;
        [SerializeField] private Transform sunArc;          // optional: sun billboard pivot (unused)
        [SerializeField] private Renderer[] hotel;           // distant hotel: windows lit only after sunset
        [Range(0, 2)] [SerializeField] private float cycle;

        [Header("Light directions (yaw is towards the sea, from the beach)")]
        [SerializeField] private float azimuth = -24f;
        [SerializeField] private float dayElevation = 42f;
        [SerializeField] private float sunsetElevation = 5f;
        [SerializeField] private float moonElevation = 36f;

        public float Cycle => cycle;
        public bool IsNight => cycle > 1.6f;

        struct Key
        {
            public Color light; public float intensity;
            public Color sky, equator, ground;
            public Color fog; public float fogDensity;
            public Color deep, shallow, skyRefl, foam;
            public float glitter;
        }

        static readonly Key Day = new Key
        {
            light = new Color(1f, 0.95f, 0.86f), intensity = 1.35f,
            sky = new Color(0.52f, 0.64f, 0.82f), equator = new Color(0.62f, 0.64f, 0.62f), ground = new Color(0.38f, 0.34f, 0.29f),
            fog = new Color(0.72f, 0.8f, 0.9f), fogDensity = 0.0032f,
            deep = new Color(0.02f, 0.2f, 0.3f), shallow = new Color(0.12f, 0.48f, 0.5f), skyRefl = new Color(0.5f, 0.68f, 0.88f), foam = new Color(0.95f, 0.97f, 0.98f),
            glitter = 1.2f
        };

        static readonly Key Sunset = new Key
        {
            light = new Color(1f, 0.56f, 0.3f), intensity = 1.0f,
            sky = new Color(0.42f, 0.36f, 0.48f), equator = new Color(0.7f, 0.45f, 0.32f), ground = new Color(0.2f, 0.15f, 0.12f),
            fog = new Color(0.86f, 0.56f, 0.42f), fogDensity = 0.0055f,
            deep = new Color(0.05f, 0.07f, 0.14f), shallow = new Color(0.22f, 0.17f, 0.2f), skyRefl = new Color(0.95f, 0.58f, 0.42f), foam = new Color(0.95f, 0.8f, 0.72f),
            glitter = 3.5f
        };

        static readonly Key Night = new Key
        {
            light = new Color(0.62f, 0.72f, 1f), intensity = 0.55f,
            sky = new Color(0.07f, 0.09f, 0.16f), equator = new Color(0.045f, 0.055f, 0.085f), ground = new Color(0.02f, 0.02f, 0.025f),
            fog = new Color(0.03f, 0.045f, 0.075f), fogDensity = 0.012f,
            deep = new Color(0.01f, 0.03f, 0.06f), shallow = new Color(0.03f, 0.09f, 0.11f), skyRefl = new Color(0.07f, 0.1f, 0.18f), foam = new Color(0.55f, 0.6f, 0.68f),
            glitter = 3f
        };

        Material _ocean, _moon;
        Color _moonColor;
        Material[] _hotel;
        Color[] _hotelColor;

        void Awake()
        {
            Instance = this;
            if (ocean != null) _ocean = ocean.material;
            if (moon != null) { _moon = moon.material; _moonColor = _moon.GetColor("_Color"); }
            if (skyMaterial != null) RenderSettings.skybox = skyMaterial;
            if (hotel != null)
            {
                _hotel = new Material[hotel.Length];
                _hotelColor = new Color[hotel.Length];
                for (int i = 0; i < hotel.Length; i++)
                    if (hotel[i] != null) { _hotel[i] = hotel[i].material; _hotelColor[i] = _hotel[i].GetColor("_BaseColor"); }
            }
        }

        // edit-mode previews (SameShorePreview) work on the shared materials
        Material OceanMat => _ocean != null ? _ocean : (!Application.isPlaying && ocean != null ? ocean.sharedMaterial : null);

        void Start() => Apply();

        public void Set(float c)
        {
            cycle = Mathf.Clamp(c, 0, 2);
            Apply();
        }

        public Coroutine TransitionTo(float target, float seconds) => StartCoroutine(Run(target, seconds));

        IEnumerator Run(float target, float seconds)
        {
            float from = cycle;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                Set(Mathf.Lerp(from, target, Mathf.SmoothStep(0, 1, t / seconds)));
                yield return null;
            }
            Set(target);
        }

        static Key Lerp(Key a, Key b, float k)
        {
            return new Key
            {
                light = Color.Lerp(a.light, b.light, k), intensity = Mathf.Lerp(a.intensity, b.intensity, k),
                sky = Color.Lerp(a.sky, b.sky, k), equator = Color.Lerp(a.equator, b.equator, k), ground = Color.Lerp(a.ground, b.ground, k),
                fog = Color.Lerp(a.fog, b.fog, k), fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, k),
                deep = Color.Lerp(a.deep, b.deep, k), shallow = Color.Lerp(a.shallow, b.shallow, k),
                skyRefl = Color.Lerp(a.skyRefl, b.skyRefl, k), foam = Color.Lerp(a.foam, b.foam, k),
                glitter = Mathf.Lerp(a.glitter, b.glitter, k)
            };
        }

        void Apply()
        {
            float c = cycle;
            Key k = c <= 1 ? Lerp(Day, Sunset, c) : Lerp(Sunset, Night, c - 1);
            // sun goes down to the horizon, the light fades out at dusk, then the moonlight rises
            float lightScale = 1f;
            Vector3 dir;
            if (c <= 1.35f)
            {
                float elev = c <= 1 ? Mathf.Lerp(dayElevation, sunsetElevation, c) : Mathf.Lerp(sunsetElevation, -3f, (c - 1) / 0.35f);
                dir = Quaternion.Euler(-elev, azimuth, 0) * Vector3.forward;     // towards the sun
                if (c > 1) lightScale = 1f - Mathf.SmoothStep(0, 1, (c - 1) / 0.35f);
            }
            else
            {
                dir = Quaternion.Euler(-moonElevation, azimuth, 0) * Vector3.forward;
                lightScale = Mathf.SmoothStep(0, 1, (c - 1.35f) / 0.4f);
            }
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.LookRotation(-dir);
                sun.color = k.light;
                sun.intensity = k.intensity * lightScale;
                sun.shadowStrength = Mathf.Lerp(0.85f, 0.75f, Mathf.Clamp01(c - 1));
                RenderSettings.sun = sun;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = k.sky;
            RenderSettings.ambientEquatorColor = k.equator;
            RenderSettings.ambientGroundColor = k.ground;
            if (PerceptionController.Instance == null || PerceptionController.Instance.Current == Perception.Human)
            {
                RenderSettings.fogColor = k.fog;
                RenderSettings.fogDensity = k.fogDensity;
            }
            if (skyMaterial != null)
            {
                skyMaterial.SetFloat("_Cycle", c);
                Vector3 sunDir = c <= 1.35f ? dir : Quaternion.Euler(8f, azimuth, 0) * Vector3.forward;
                skyMaterial.SetVector("_SunDir", sunDir);
                skyMaterial.SetColor("_SunColor", c <= 1 ? Color.Lerp(new Color(1f, 0.95f, 0.85f), new Color(1f, 0.55f, 0.25f), c) : new Color(1f, 0.45f, 0.2f) * (1 - Mathf.Clamp01((c - 1) / 0.4f)));
            }
            var om = OceanMat;
            if (om != null)
            {
                om.SetColor("_DeepColor", k.deep);
                om.SetColor("_ShallowColor", k.shallow);
                om.SetColor("_SkyColor", k.skyRefl);
                om.SetColor("_FoamColor", k.foam);
                om.SetFloat("_Glitter", k.glitter);
            }
            if (_hotel != null)
            {
                // by day a pale facade with dark windows; the windows light up at dusk
                float lit = Mathf.SmoothStep(0, 1, (c - 0.9f) / 0.6f);
                for (int i = 0; i < _hotel.Length; i++)
                    if (_hotel[i] != null) _hotel[i].SetColor("_BaseColor", Color.Lerp(new Color(0.62f, 0.6f, 0.58f) * 0.6f, _hotelColor[i], lit));
            }
            if (moon != null && _moon == null && !Application.isPlaying) moon.enabled = c > 1.3f;
            if (_moon != null)
            {
                Color m = _moonColor;
                float vis = Mathf.SmoothStep(0, 1, (c - 1.2f) / 0.6f);
                _moon.SetColor("_Color", new Color(m.r * vis, m.g * vis, m.b * vis, m.a * vis));
                moon.enabled = vis > 0.01f;
            }
        }

        void OnValidate()
        {
            if (Application.isPlaying) Apply();
        }
    }
}
