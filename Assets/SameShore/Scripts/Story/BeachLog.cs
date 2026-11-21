using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// Shared memory of the day, written by the human's activities and read by the hatchling's act.
    /// Both acts share one scene, so every object physically stays where the human left it (cans, the
    /// hole, the bag, the light string). This log adds what cannot be seen afterwards: the timeline of
    /// events, the music, the fireworks, and the human's movements at night (replayed by the "giant").
    /// </summary>
    public class BeachLog : MonoBehaviour
    {
        public static BeachLog I { get; private set; }

        public enum CanResult { Unthrown, Scored, Missed }

        public class CanRecord
        {
            public string id;
            public CanResult result;
            public Vector3 restPosition;
            public Transform can;
        }

        public struct PathSample
        {
            public float t;
            public Vector3 feet;
            public float yaw;
            public bool flashlightOn;
            public Vector3 lightDir;
        }

        public struct Event
        {
            public float t;
            public string what;
            public Vector3 where;
        }

        // ---- the day
        public readonly List<CanRecord> Cans = new List<CanRecord>();
        public bool CastleBuilt;
        public Vector3 CastlePosition;
        public Vector3 HolePosition;
        public float HoleDepth01;
        public bool BagLost;
        public Vector3 BagPosition;
        public bool MusicOn;
        public int MusicVolumeLevel;
        public string MusicTrack = "";
        public int FireworksLaunched;
        public Vector3 FireworksPosition;
        public bool LightsInstalled;
        public readonly List<Vector3> LightAnchors = new List<Vector3>();
        public bool FlashlightOn;
        public Vector3 FlashlightPosition;

        // ---- the night (replayed in the hatchling's act)
        public readonly List<PathSample> NightPath = new List<PathSample>();
        public bool PhotoTaken;
        public Vector3 PhotoFeet;
        public float PhotoYaw;
        public Vector3 PhotoPhone;
        public Vector3 PhotoSubject;
        public bool PhotoFlash;
        public bool UsedDefaults;

        public readonly List<Event> Timeline = new List<Event>();

        void Awake() => I = this;

        public void Record(string what, Vector3 where)
        {
            Timeline.Add(new Event { t = Time.time, what = what, where = where });
            Debug.Log($"[SameShore] beach log @{Time.time:F1}s: {what} at {where}");
        }

        public int CansMissed
        {
            get { int n = 0; foreach (var c in Cans) if (c.result == CanResult.Missed) n++; return n; }
        }

        public int CansScored
        {
            get { int n = 0; foreach (var c in Cans) if (c.result == CanResult.Scored) n++; return n; }
        }

        // ---- night path recording
        bool _recording;
        float _recStart, _nextSample;

        public void StartNightRecording()
        {
            NightPath.Clear();
            _recording = true;
            _recStart = Time.time;
            _nextSample = 0;
        }

        public void StopNightRecording() => _recording = false;

        void Update()
        {
            if (!_recording || PlayerRig.Head == null || Time.time < _nextSample) return;
            _nextSample = Time.time + 0.2f;
            Transform h = PlayerRig.Head;
            var lamp = Object.FindFirstObjectByType<Flashlight>();
            Vector3 lightDir = lamp != null ? lamp.transform.forward : h.forward;
            NightPath.Add(new PathSample
            {
                t = Time.time - _recStart,
                feet = MeshKit.OnGround(h.position),
                yaw = h.eulerAngles.y,
                flashlightOn = lamp != null && lamp.IsOn && lamp.GetComponent<VRGrabbable>() != null && lamp.GetComponent<VRGrabbable>().IsHeld,
                lightDir = lightDir
            });
        }

        /// <summary>One-paragraph summary used at the transition and for debugging.</summary>
        public string Summary()
        {
            var sb = new StringBuilder();
            if (Cans.Count > 0) sb.AppendLine($"Cans: {CansScored} in the bin, {CansMissed} left on the sand.");
            if (CastleBuilt) sb.AppendLine($"A sandcastle, and the hole dug for it ({Mathf.RoundToInt(HoleDepth01 * 100)}% deep).");
            if (BagLost) sb.AppendLine("A plastic bag, carried by the wind to the wet sand.");
            if (FireworksLaunched > 0) sb.AppendLine($"{FireworksLaunched} fireworks over the sea.");
            if (LightsInstalled) sb.AppendLine($"A string of lights across {LightAnchors.Count - 1} spans, still on.");
            if (MusicOn) sb.AppendLine($"Music playing, volume {MusicVolumeLevel}.");
            if (PhotoTaken) sb.AppendLine("A photo, with flash.");
            return sb.ToString();
        }
    }
}
