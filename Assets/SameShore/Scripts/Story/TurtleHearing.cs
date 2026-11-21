using System;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// How the hatchling hears the human's world. The speaker on the camp table is silent when the act begins;
    /// when the hatchling crosses the music trigger (the corridor at the camp) the human turns the same song back
    /// on, and it becomes the loudest thing on the beach for it: a booming, smeared, too-close human noise.
    ///
    /// The mix is built with spatial audio, filtering and EQ, not with loudness: the source volume is capped at
    /// 0.8; closer to the speaker the low-pass opens (harsher, more present highs), the distortion and the bass
    /// pulses in the controllers grow; the sea is ducked a little (slowly) while the music dominates and comes
    /// back as the hatchling leaves the camp behind. No sudden changes: every parameter is slewed.
    /// </summary>
    public class TurtleHearing : MonoBehaviour
    {
        [SerializeField] private MusicPlayer music;
        [SerializeField] private AmbientAudio ambient;
        [SerializeField] private StoryTrigger musicZone;
        [SerializeField] private float bpm = 96f;
        [SerializeField] private float maxVolume = 0.8f;
        [SerializeField] private float nearDistance = 1.5f;      // world metres from the speaker (~30 m for the hatchling)
        [SerializeField] private float farDistance = 12f;
        [SerializeField] private float leaveDistance = 3.2f;      // past the camp, towards the sea, the music recedes
        [SerializeField] private float waveDuck = 0.55f;

        AudioLowPassFilter _lp;
        AudioDistortionFilter _dist;
        AudioReverbFilter _rev;
        AudioChorusFilter _chorus;
        bool _on;
        float _nextBeat;
        float _minDist0, _maxDist0, _pitch0;
        float _zoneGain, _proximity;
        Transform _listener;

        public bool On => _on;
        public bool MusicStarted { get; private set; }
        /// <summary>0..1 how strongly the music dominates the hatchling's mix right now.</summary>
        public float Presence => _zoneGain * Mathf.Lerp(0.35f, 1f, _proximity);
        public event Action<string, float> Message;

        public void Enable(bool on, Transform listener = null)
        {
            _on = on;
            _listener = listener;
            MusicStarted = false;
            _zoneGain = 0f;
            if (music == null || music.Source == null) return;
            var src = music.Source;
            if (_lp == null)
            {
                _lp = src.gameObject.AddComponent<AudioLowPassFilter>();
                _dist = src.gameObject.AddComponent<AudioDistortionFilter>();
                _chorus = src.gameObject.AddComponent<AudioChorusFilter>();
                _rev = src.gameObject.AddComponent<AudioReverbFilter>();
                _minDist0 = src.minDistance;
                _maxDist0 = src.maxDistance;
                _pitch0 = src.pitch;
            }
            _lp.enabled = _dist.enabled = _rev.enabled = _chorus.enabled = on;
            _lp.lowpassResonanceQ = 1.8f;
            _rev.reverbPreset = AudioReverbPreset.Cave;
            _chorus.dryMix = 0.7f; _chorus.wetMix1 = 0.35f; _chorus.wetMix2 = 0.25f; _chorus.depth = 0.25f; _chorus.rate = 0.6f;
            src.minDistance = on ? 2.2f : _minDist0;
            src.maxDistance = on ? 40f : _maxDist0;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.pitch = on ? 0.94f : _pitch0;
            music.Overridden = on;
            if (on)
            {
                // silent until the trigger: the act starts in the quiet of the nest
                src.volume = 0f;
                if (src.isPlaying) src.Pause();
                if (musicZone != null)
                {
                    musicZone.Arm(listener);
                    musicZone.Entered -= OnZone;
                    musicZone.Entered += OnZone;
                }
            }
            else
            {
                if (musicZone != null) musicZone.Disarm();
                if (ambient != null) ambient.DuckWaves(1f);
                // hand the source back to the speaker exactly as the human left it
                if (music.Wanted && !src.isPlaying) { src.Play(); src.UnPause(); }
            }
        }

        void OnZone(StoryTrigger t) => StartMusic();

        /// <summary>The human turns the music back on (narrative trigger, or the fallback by distance).</summary>
        public void StartMusic()
        {
            if (!_on || MusicStarted || music == null || music.Source == null) return;
            MusicStarted = true;
            var src = music.Source;
            if (!src.isPlaying) src.Play();
            src.UnPause();
            AudioOneShot.PlayAt(SameShoreAssets.I.buttonClick, music.transform.position, 0.5f, 1f, 10f);
            XRInput.HapticBoth(0.25f, 0.25f, 0.2f);
            Message?.Invoke("A click... then a boom shakes the sand.\nThe giants' music. It is everywhere at once.", 6f);
            BeachLog.I?.Record("the hatchling heard the music start", music.transform.position);
        }

        void Update()
        {
            if (!_on || music == null || music.Source == null) return;
            var src = music.Source;
            Vector3 ear = _listener != null ? _listener.position : PlayerRig.Head != null ? PlayerRig.Head.position : transform.position;
            float dist = (music.transform.position - ear).magnitude;
            _proximity = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(nearDistance, farDistance, dist));
            // fallback: if the trigger was skipped (checkpoint), start when close to the camp
            if (!MusicStarted && dist < 5f) StartMusic();

            // after the camp, towards the sea, the music recedes behind the waves
            bool behind = MusicStarted && dist > leaveDistance && IsSeaward(ear);
            float target = MusicStarted ? (behind ? 0.45f : 1f) : 0f;
            _zoneGain = Mathf.MoveTowards(_zoneGain, target, Time.deltaTime * (target > _zoneGain ? 0.3f : 0.18f));

            src.volume = Mathf.Min(maxVolume, maxVolume * _zoneGain);
            // EQ by distance: far = a muffled boom; near = harsher, brighter, distorted (slewed)
            float cutoff = Mathf.Lerp(600f, 3400f, _proximity * _proximity);
            _lp.cutoffFrequency = Mathf.Lerp(_lp.cutoffFrequency, cutoff, Time.deltaTime * 2f);
            _dist.distortionLevel = Mathf.Lerp(0.22f, 0.5f, _proximity);
            _rev.dryLevel = Mathf.Lerp(-600f, 0f, _proximity);
            if (ambient != null) ambient.DuckWaves(Mathf.Lerp(1f, waveDuck, Presence));

            if (!MusicStarted || Time.time < _nextBeat) return;
            _nextBeat = Time.time + 60f / (bpm * 0.94f);
            float amp = Mathf.Lerp(0.02f, 0.16f, _proximity) * _zoneGain;
            if (amp > 0.015f) XRInput.HapticBoth(amp, 0.05f, 0.15f);
        }

        bool IsSeaward(Vector3 ear)
        {
            // the speaker faces the corridor; "past" = closer to the sea than the speaker (sea is +z)
            return ear.z > music.transform.position.z;
        }
    }
}
