using System;
using System.Collections;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// Ambient mix for each body. Human: waves, wind, distant hotel music. Hatchling: louder,
    /// closer surf, the music and human voices muffled (low-pass), wind lower.
    /// Every channel is a spatial AudioSource in the scene; this only rebalances them.
    /// </summary>
    public class AmbientAudio : MonoBehaviour
    {
        [Serializable]
        public class Channel
        {
            public string name;
            public AudioSource source;
            public float humanVolume = 1f;
            public float hatchlingVolume = 1f;
            public float humanLowpass = 22000f;
            public float hatchlingLowpass = 22000f;
            [NonSerialized] public AudioLowPassFilter filter;
        }

        [SerializeField] private Channel[] channels;
        [SerializeField] private float crossfadeSeconds = 2.5f;

        float[] _base;
        float _waveDuck = 1f, _waveDuckTarget = 1f;

        /// <summary>Lowers the sea (channels whose name starts with "Waves") while the human's music dominates.</summary>
        public void DuckWaves(float factor) => _waveDuckTarget = Mathf.Clamp01(factor);
        public float WaveDuck => _waveDuck;

        void LateUpdate()
        {
            if (_base == null) return;
            _waveDuck = Mathf.MoveTowards(_waveDuck, _waveDuckTarget, Time.unscaledDeltaTime * 0.25f);   // slow, never abrupt
            for (int i = 0; i < channels.Length; i++)
            {
                var c = channels[i];
                if (c.source == null) continue;
                bool wave = c.name != null && c.name.StartsWith("Waves");
                c.source.volume = _base[i] * (wave ? _waveDuck : 1f);
            }
        }

        void Awake()
        {
            _base = new float[channels.Length];
            foreach (var c in channels)
            {
                if (c.source == null) continue;
                c.filter = c.source.GetComponent<AudioLowPassFilter>();
                if (c.filter == null) c.filter = c.source.gameObject.AddComponent<AudioLowPassFilter>();
            }
            Apply(Perception.Human, 0f);
        }

        public void Apply(Perception p, float seconds = -1f)
        {
            StopAllCoroutines();
            StartCoroutine(Fade(p == Perception.Hatchling, seconds < 0 ? crossfadeSeconds : seconds));
        }

        /// <summary>Dip everything (used under the black transition).</summary>
        public void Duck(float factor, float seconds)
        {
            StartCoroutine(DuckRoutine(factor, seconds));
        }

        IEnumerator DuckRoutine(float factor, float seconds)
        {
            float t0 = AudioListener.volume;
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                AudioListener.volume = Mathf.Lerp(t0, factor, t / seconds);
                yield return null;
            }
            AudioListener.volume = factor;
        }

        IEnumerator Fade(bool hatch, float seconds)
        {
            var startVol = new float[channels.Length];
            var startLp = new float[channels.Length];
            for (int i = 0; i < channels.Length; i++)
            {
                if (channels[i].source == null) continue;
                startVol[i] = _base != null ? _base[i] : channels[i].source.volume;
                startLp[i] = channels[i].filter.cutoffFrequency;
                if (!channels[i].source.isPlaying && channels[i].source.clip != null) channels[i].source.Play();
            }
            for (float t = 0; t <= seconds; t += Time.unscaledDeltaTime)
            {
                float k = seconds <= 0 ? 1 : Mathf.SmoothStep(0, 1, t / seconds);
                Set(k, hatch, startVol, startLp);
                yield return null;
            }
            Set(1, hatch, startVol, startLp);
        }

        void Set(float k, bool hatch, float[] v0, float[] lp0)
        {
            for (int i = 0; i < channels.Length; i++)
            {
                var c = channels[i];
                if (c.source == null) continue;
                _base[i] = Mathf.Lerp(v0[i], hatch ? c.hatchlingVolume : c.humanVolume, k);
                // interpolate the cutoff logarithmically so the muffling sounds natural
                float target = hatch ? c.hatchlingLowpass : c.humanLowpass;
                c.filter.cutoffFrequency = Mathf.Exp(Mathf.Lerp(Mathf.Log(Mathf.Max(10, lp0[i])), Mathf.Log(target), k));
            }
        }
    }
}
