using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// How the hatchling hears the human's world: the same song from the same speaker, but low-passed,
    /// slightly slowed, with a rough edge and a cavernous echo; the bass is felt as faint pulses in the
    /// controllers. The effect comes from filtering and spatialisation, never from loudness
    /// (the speaker's volume is capped well below 1).
    /// </summary>
    public class TurtleHearing : MonoBehaviour
    {
        [SerializeField] private MusicPlayer music;
        [SerializeField] private float bpm = 96f;

        AudioLowPassFilter _lp;
        AudioDistortionFilter _dist;
        AudioReverbFilter _rev;
        bool _on;
        float _nextBeat;
        float _minDist0, _pitch0;

        public bool On => _on;

        public void Enable(bool on)
        {
            _on = on;
            if (music == null || music.Source == null) return;
            var src = music.Source;
            if (_lp == null)
            {
                _lp = src.gameObject.AddComponent<AudioLowPassFilter>();
                _dist = src.gameObject.AddComponent<AudioDistortionFilter>();
                _rev = src.gameObject.AddComponent<AudioReverbFilter>();
                _minDist0 = src.minDistance;
                _pitch0 = src.pitch;
            }
            _lp.enabled = on;
            _lp.cutoffFrequency = 650f;
            _lp.lowpassResonanceQ = 1.6f;
            _dist.enabled = on;
            _dist.distortionLevel = 0.35f;
            _rev.enabled = on;
            _rev.reverbPreset = AudioReverbPreset.Cave;
            src.minDistance = on ? 6f : _minDist0;
            src.pitch = on ? 0.94f : _pitch0;
            music.PerceptionGain = on ? 1.5f : 1f;   // still capped at 0.85 by the player
        }

        void Update()
        {
            if (!_on || music == null || !music.IsOn || PlayerRig.Head == null) return;
            if (Time.time < _nextBeat) return;
            _nextBeat = Time.time + 60f / (bpm * 0.94f);
            float dist = (music.transform.position - PlayerRig.Head.position).magnitude;
            float amp = Mathf.Clamp01(1f - dist / 14f) * 0.12f;
            if (amp > 0.01f) XRInput.HapticBoth(amp, 0.05f, 0.15f);
        }
    }
}
