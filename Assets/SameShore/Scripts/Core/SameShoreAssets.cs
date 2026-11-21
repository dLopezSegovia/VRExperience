using TMPro;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// Shared runtime resources (materials, font, sounds). Lives in a Resources folder so it is
    /// always included in builds; generated and filled by the scene builder.
    /// </summary>
    [CreateAssetMenu(menuName = "Same Shore/Assets")]
    public class SameShoreAssets : ScriptableObject
    {
        [Header("Materials")]
        public Material fade;
        public Material turtleVision;
        public Material uiPanel;
        public Material uiLine;
        public Material sandParticle;
        public Texture2D roundedRect;
        public TMP_FontAsset font;

        [Header("Audio")]
        public AudioClip[] footsteps;
        public AudioClip[] flipperStrokes;
        public AudioClip sandDrag;
        public AudioClip[] gullCalls;
        public AudioClip gullFlap;
        public AudioClip click;
        public AudioClip thud;
        public AudioClip pickup;
        public AudioClip sandPour;
        public AudioClip heartbeat;
        public AudioClip success;
        public AudioClip transition;

        [Header("Audio - narrative redesign")]
        public AudioClip turtleBreath;
        public AudioClip nestScratching;
        public AudioClip swashWash;
        public AudioClip waveCrashBig;
        public AudioClip underwater;
        public AudioClip objectiveDone;
        public AudioClip objectiveLeft;
        public AudioClip uiTick;
        public AudioClip lightHum;
        public AudioClip gullScreech;
        public AudioClip gullDive;
        public AudioClip endingPad;

        [Header("Visual - narrative redesign")]
        public Material lightCone;
        public Material groundDecal;
        public Material warningRing;
        public Texture2D turtleTrack;
        public Mesh[] pebbles;
        public Material pebble;

        [Header("Storyboard v2")]
        public AudioClip[] clips;
        public Texture2D ringTexture;
        public Material bulbOn;
        public Material bulbOff;
        public Material cable;
        public Material fireworkParticle;
        public Material sandCastle;

        System.Collections.Generic.Dictionary<string, AudioClip> _byName;

        /// <summary>Clip by file name (Audio/&lt;name&gt;.wav), from the 'clips' list.</summary>
        public AudioClip Clip(string name)
        {
            if (_byName == null)
            {
                _byName = new System.Collections.Generic.Dictionary<string, AudioClip>();
                if (clips != null) foreach (var c in clips) if (c != null) _byName[c.name] = c;
            }
            if (_byName.TryGetValue(name, out var clip)) return clip;
            Debug.LogWarning("[SameShore] missing clip " + name);
            return null;
        }

        public AudioClip buttonClick => Clip("button_click");

        static SameShoreAssets _instance;

        public static SameShoreAssets I
        {
            get
            {
                if (_instance == null) _instance = Resources.Load<SameShoreAssets>("SameShoreAssets");
                if (_instance == null)
                {
                    Debug.LogError("[SameShore] Resources/SameShoreAssets missing. Run 'Same Shore/Build Scene'.");
                    _instance = CreateInstance<SameShoreAssets>();
                }
                return _instance;
            }
        }

        public static AudioClip Pick(AudioClip[] clips) =>
            clips == null || clips.Length == 0 ? null : clips[Random.Range(0, clips.Length)];
    }
}
