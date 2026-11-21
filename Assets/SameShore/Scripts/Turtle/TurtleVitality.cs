using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SameShore
{
    /// <summary>
    /// The hatchling's strength (0..1) and the harm of the human's lights.
    ///
    /// Light hazard: within <see cref="hazardRadius"/> of any bulb of the festoon the hatchling is dazzled. The effect
    /// grows smoothly with closeness and time spent there: slower strokes, a soft dark vignette and less contrast,
    /// a little strength lost. It fades as soon as it moves away. Light damage alone can never take strength below
    /// <see cref="lightFloor"/> and gull strikes never below <see cref="absoluteFloor"/>: nothing here can kill the
    /// hatchling or stop it finishing. No flashes, no camera motion (the vignette is a post-processing weight).
    /// </summary>
    public class TurtleVitality : MonoBehaviour
    {
        [SerializeField] private TurtleController turtle;
        [SerializeField] private LightGarland garland;
        [SerializeField] private float hazardRadius = 0.75f;      // horizontal distance to a bulb
        [SerializeField] private float lightDrainPerSecond = 0.07f;
        [SerializeField] private float lightFloor = 0.5f;
        [SerializeField] private float absoluteFloor = 0.2f;
        [SerializeField] private float regenPerSecond = 0.025f;
        [SerializeField] private float maxSlowdown = 0.45f;       // speed x0.55 at the worst
        [SerializeField] private float dazzleRise = 0.8f;         // per second
        [SerializeField] private float dazzleFall = 0.6f;

        public float Strength { get; private set; } = 1f;
        /// <summary>Smoothed light exposure 0..1 (drives the vignette and the slowdown).</summary>
        public float Dazzle { get; private set; }
        public float RawExposure { get; private set; }
        public float LightDamageTaken { get; private set; }
        public float SecondsDazzled { get; private set; }
        public bool Active { get; set; }

        public event Action<string, float> Message;

        Volume _volume;
        Vignette _vig;
        ColorAdjustments _ca;
        bool _warned, _warnedAgain;
        float _hurtCooldown;

        public void ResetState()
        {
            Strength = 1f;
            Dazzle = RawExposure = LightDamageTaken = SecondsDazzled = 0f;
            _warned = _warnedAgain = false;
            if (turtle != null) turtle.HazardSpeedMultiplier = 1f;
            if (_volume != null) _volume.weight = 0f;
        }

        /// <summary>Gull strikes and other knocks. Never below the absolute floor.</summary>
        public void Hurt(float amount)
        {
            Strength = Mathf.Max(absoluteFloor, Strength - amount);
        }

        void Start()
        {
            // a local post-processing layer for the dazzle: dark warm vignette + washed-out contrast
            var go = new GameObject("~LightDazzleVolume");
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 30;
            _volume.weight = 0f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _vig = profile.Add<Vignette>(true);
            _vig.color.Override(new Color(0.12f, 0.06f, 0.02f));
            _vig.intensity.Override(0.42f);
            _vig.smoothness.Override(0.85f);
            _ca = profile.Add<ColorAdjustments>(true);
            _ca.contrast.Override(-28f);
            _ca.saturation.Override(-18f);
            _ca.postExposure.Override(0.15f);
            _volume.sharedProfile = profile;
        }

        /// <summary>
        /// Exposure from the bulbs: strong only right under the low middle of a span (a bulb ~1 m over the sand),
        /// weak near the posts where the string is high. Crossing a span close to a post avoids most of it.
        /// </summary>
        float Exposure(Vector3 eye)
        {
            if (garland == null || !garland.LightsOn) return 0f;
            float e = 0f;
            var bulbs = garland.BulbPositions;
            for (int i = 0; i < bulbs.Count; i++)
            {
                Vector3 b = bulbs[i];
                float hd = new Vector2(b.x - eye.x, b.z - eye.z).magnitude;
                if (hd >= hazardRadius) continue;
                float height = b.y - MeshKit.GroundY(b);
                float low = Mathf.Clamp01(Mathf.InverseLerp(1.7f, 0.9f, height));
                float k = 1f - hd / hazardRadius;
                e += k * k * low;
            }
            return Mathf.Clamp01(e * 1.5f);
        }

        void Update()
        {
            bool running = Active && turtle != null && turtle.enabled && GameManager.Instance != null && GameManager.Instance.Current == GameState.Hatchling;
            Vector3 eye = turtle != null ? turtle.transform.position : Vector3.zero;
            RawExposure = running ? Exposure(eye) : 0f;
            Dazzle = Mathf.MoveTowards(Dazzle, RawExposure, Time.deltaTime * (RawExposure > Dazzle ? dazzleRise : dazzleFall));

            if (running)
            {
                if (Dazzle > 0.05f)
                {
                    SecondsDazzled += Time.deltaTime;
                    // light can weaken you, but only down to the floor: standing under a bulb never accumulates more
                    if (Strength > lightFloor)
                    {
                        float loss = Mathf.Min(Strength - lightFloor, lightDrainPerSecond * Dazzle * Time.deltaTime);
                        Strength -= loss;
                        LightDamageTaken += loss;
                    }
                    if (!_warned && Dazzle > 0.3f)
                    {
                        _warned = true;
                        Message?.Invoke("Too close to the light... it burns your eyes.\nYour flippers feel heavy. Crawl away from it - into the dark.", 6f);
                        XRInput.HapticBoth(0.15f, 0.3f, 0.5f);
                    }
                    else if (_warned && !_warnedAgain && SecondsDazzled > 9f)
                    {
                        _warnedAgain = true;
                        Message?.Invoke("The light is draining you. The sea is the dim glow ahead, not the bulbs.", 5f);
                    }
                    _hurtCooldown -= Time.deltaTime;
                    if (_hurtCooldown <= 0f && Dazzle > 0.4f)
                    {
                        _hurtCooldown = 1.6f;
                        XRInput.HapticBoth(0.08f * Dazzle, 0.12f, 0.4f);
                    }
                }
                else if (Strength < 1f) Strength = Mathf.Min(1f, Strength + regenPerSecond * Time.deltaTime);
            }
            if (turtle != null) turtle.HazardSpeedMultiplier = 1f - maxSlowdown * Dazzle;
            if (_volume != null) _volume.weight = Mathf.SmoothStep(0f, 1f, Dazzle);
        }

        /// <summary>Short status for the HUD ("strength 80%").</summary>
        public string StatusText()
        {
            string s = $"strength {Mathf.RoundToInt(Strength * 100f)}%";
            if (Dazzle > 0.2f) s += "  <color=#FFB060>dazzled</color>";
            return s;
        }
    }
}
