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
