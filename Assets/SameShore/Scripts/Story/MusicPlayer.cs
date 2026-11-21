using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// The portable speaker: play/pause and volume up buttons. The same AudioSource keeps playing into the
    /// night, so the hatchling hears exactly the same song (through its own perception, see TurtleHearing).
    /// </summary>
    public class MusicPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private PokeButton playButton;
        [SerializeField] private PokeButton volumeButton;
        [SerializeField] private Renderer statusLed;
        [SerializeField] private float[] volumeLevels = { 0.22f, 0.36f, 0.5f };

        public bool IsOn => source != null && source.isPlaying && !_paused;
        public int VolumeLevel { get; private set; }
        public AudioSource Source => source;
        public float BaseVolume => volumeLevels[Mathf.Clamp(VolumeLevel, 0, volumeLevels.Length - 1)];
        /// <summary>Extra gain applied by the hatchling's perception (kept moderate).</summary>
        public float PerceptionGain { get; set; } = 1f;
        /// <summary>When set, the hatchling's hearing drives the source directly (Act II mix).</summary>
        public bool Overridden { get; set; }
        /// <summary>The player's own state (play pressed), even while the hatchling's mix holds the source.</summary>
        public bool Wanted => !_paused;

        bool _paused = true;
        MaterialPropertyBlock _mpb;

        void Start()
        {
            if (playButton != null) playButton.Pressed += TogglePlay;
            if (volumeButton != null) volumeButton.Pressed += VolumeUp;
            if (source != null) { source.loop = true; source.playOnAwake = false; source.volume = 0; }
            UpdateLed();
        }

        public void TogglePlay()
        {
            if (source == null) return;
            if (!source.isPlaying) source.Play();
            _paused = !_paused;
            if (_paused) source.Pause(); else source.UnPause();
            Log();
            UpdateLed();
        }

        public void SetOn(bool on)
        {
            if (on != IsOn) TogglePlay();
        }

        public void VolumeUp()
        {
            if (!IsOn) { SetOn(true); return; }
            VolumeLevel = Mathf.Min(volumeLevels.Length - 1, VolumeLevel + 1);
            Log();
        }

        public void SetVolumeLevel(int level)
        {
            VolumeLevel = Mathf.Clamp(level, 0, volumeLevels.Length - 1);
            Log();
        }

        void Log()
        {
            if (BeachLog.I == null) return;
            BeachLog.I.MusicOn = IsOn;
            BeachLog.I.MusicVolumeLevel = VolumeLevel + 1;
            BeachLog.I.MusicTrack = source != null && source.clip != null ? source.clip.name : "";
            BeachLog.I.Record(IsOn ? $"music on (volume {VolumeLevel + 1})" : "music paused", transform.position);
        }

        void Update()
        {
            if (source == null || Overridden) return;
            float target = IsOn ? BaseVolume * PerceptionGain : 0f;
            source.volume = Mathf.MoveTowards(source.volume, Mathf.Min(target, 0.85f), Time.deltaTime * 0.6f);
        }

        void UpdateLed()
        {
            if (statusLed == null) return;
            _mpb ??= new MaterialPropertyBlock();
            _mpb.SetColor("_BaseColor", IsOn ? new Color(0.2f, 1f, 0.4f) : new Color(0.25f, 0.05f, 0.05f));
            _mpb.SetColor("_EmissionColor", IsOn ? new Color(0.2f, 1.6f, 0.5f) : Color.black);
            statusLed.SetPropertyBlock(_mpb);
        }
    }
}
