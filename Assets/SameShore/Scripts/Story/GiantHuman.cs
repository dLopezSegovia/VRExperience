using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// "The giant": replays the human's night (recorded path, flashlight on/off) for the hatchling, then
    /// stands at the exact spot where the photo was taken and takes it again, when the hatchling arrives.
    /// </summary>
    public class GiantHuman : MonoBehaviour
    {
        [SerializeField] private HumanPuppet puppet;
        [SerializeField] private Light lampBeam;
        [SerializeField] private Light phoneFlash;
        [SerializeField] private Collider[] standingColliders;
        [SerializeField] private AudioSource steps;

        public bool Playing { get; private set; }
        public bool AtPhotoSpot { get; private set; }
        public bool PhotoDone { get; private set; }
        public Vector3 Feet => puppet.Feet;
        public float TimeScale { get; set; } = 1f;
        public event System.Action Flashed;

        List<BeachLog.PathSample> _path;
        float _t;
        float _stepAccum;
        Vector3 _last;
        Vector3 _photoFeet;
        float _photoYaw;

        void Awake()
        {
            SetVisible(false);
            if (phoneFlash != null) phoneFlash.enabled = false;
            if (lampBeam != null) lampBeam.enabled = false;
        }

        void SetVisible(bool on)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = on;
            foreach (var c in standingColliders) if (c != null) c.enabled = false;
            if (!on && lampBeam != null) lampBeam.enabled = false;
        }

        public void Hide()
        {
            StopAllCoroutines();
            Playing = false;
            SetVisible(false);
        }

        /// <summary>Starts the replay of the recorded night (or a default walk) at the beginning of the hatchling's act.</summary>
        public void Play(List<BeachLog.PathSample> path, Vector3 photoFeet, float photoYaw)
        {
            _path = path;
            _photoFeet = photoFeet;
            _photoYaw = photoYaw;
            _t = 0;
            PhotoDone = false;
            AtPhotoSpot = false;
            Playing = true;
            SetVisible(true);
            if (_path.Count > 0) { puppet.Feet = _path[0].feet; puppet.Yaw = _path[0].yaw; }
            _last = puppet.Feet;
            puppet.Bend = 0;
            puppet.PhoneRaise = 0;
        }

        void Update()
        {
            if (!Playing || _path == null) return;
            if (!AtPhotoSpot)
            {
                _t += Time.deltaTime * TimeScale;
                if (_path.Count < 2 || _t >= _path[_path.Count - 1].t) { ArriveAtPhotoSpot(); return; }
                int i = 0;
                while (i < _path.Count - 2 && _path[i + 1].t < _t) i++;
                var a = _path[i];
                var b = _path[i + 1];
                float k = Mathf.InverseLerp(a.t, b.t, _t);
                Vector3 feet = Vector3.Lerp(a.feet, b.feet, k);
                feet.y = MeshKit.GroundY(feet);
                Vector3 delta = feet - _last;
                delta.y = 0;
                float speed = delta.magnitude / Mathf.Max(Time.deltaTime, 1e-4f);
                // body faces the walking direction, or where the head looked when standing
                float yaw = speed > 0.25f ? Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg : Mathf.LerpAngle(a.yaw, b.yaw, k);
                puppet.Yaw = Mathf.MoveTowardsAngle(puppet.Yaw, yaw, 160f * Time.deltaTime);
                puppet.Feet = feet;
                puppet.WalkAmount = Mathf.MoveTowards(puppet.WalkAmount, Mathf.Clamp01(speed / 1.1f), Time.deltaTime * 3f);
                puppet.WalkPhase += speed / 0.6f * Mathf.PI * Time.deltaTime;
                if (lampBeam != null) lampBeam.enabled = a.flashlightOn;
                puppet.LightAim = a.flashlightOn ? 1f : 0f;
                Steps(delta.magnitude);
                _last = feet;
            }
        }

        void Steps(float moved)
        {
            _stepAccum += moved;
            if (_stepAccum < 0.7f || steps == null) return;
            _stepAccum = 0;
            steps.pitch = UnityEngine.Random.Range(0.55f, 0.7f);  // heavier, slower thuds for the hatchling
            steps.PlayOneShot(SameShoreAssets.Pick(SameShoreAssets.I.footsteps), 1f);
            XRInput.HapticBoth(0.12f, 0.05f, 0.2f);
        }

        void ArriveAtPhotoSpot()
        {
            if (AtPhotoSpot) return;
            AtPhotoSpot = true;
            puppet.Feet = MeshKit.OnGround(_photoFeet);
            puppet.Yaw = _photoYaw;
            puppet.WalkAmount = 0;
            foreach (var c in standingColliders) if (c != null) c.enabled = true;
        }

        /// <summary>Skip the rest of the walk (the hatchling arrived first).</summary>
        public void HurryToPhotoSpot()
        {
            if (!AtPhotoSpot) TimeScale = 4f;
        }

        /// <summary>The photo, seen from below: the giant leans over, the phone comes out, flash.</summary>
        public IEnumerator TakePhoto(Vector3 subject)
        {
            if (PhotoDone) yield break;
            while (!AtPhotoSpot) { HurryToPhotoSpot(); yield return null; }
            PhotoDone = true;
            Vector3 to = Vector3.ProjectOnPlane(subject - puppet.Feet, Vector3.up);
            float targetYaw = to.sqrMagnitude > 0.01f ? Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg : puppet.Yaw;
            if (lampBeam != null) lampBeam.enabled = false;
            for (float t = 0; t < 1.6f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / 1.6f);
                puppet.Yaw = Mathf.LerpAngle(puppet.Yaw, targetYaw, Time.deltaTime * 3f);
                puppet.Bend = k * 0.85f;
                puppet.LightAim = 1 - k;
                puppet.PhoneRaise = Mathf.SmoothStep(0, 1, (t - 0.4f) / 1.0f);
                yield return null;
            }
            yield return new WaitForSeconds(0.7f);
            AudioOneShot.PlayAt(SameShoreAssets.I.Clip("flash_whine"), puppet.phone != null ? puppet.phone.position : puppet.HeadPosition, 0.5f, 0.1f, 3f, 0.8f);
            yield return new WaitForSeconds(0.3f);
            AudioOneShot.PlayAt(SameShoreAssets.I.Clip("camera_shutter"), puppet.phone != null ? puppet.phone.position : puppet.HeadPosition, 0.9f, 0.1f, 4f, 0.75f);
            if (phoneFlash != null) StartCoroutine(FlashRoutine());
            Flashed?.Invoke();
            yield return new WaitForSeconds(1.5f);
            for (float t = 0; t < 1.4f; t += Time.deltaTime)
            {
                float k = 1 - Mathf.SmoothStep(0, 1, t / 1.4f);
                puppet.Bend = k * 0.85f;
                puppet.PhoneRaise = k;
                yield return null;
            }
            // the person walks back to the towel
            Vector3 dir = Quaternion.Euler(0, puppet.Yaw + 150f, 0) * Vector3.forward;
            for (float t = 0; t < 4f; t += Time.deltaTime)
            {
                puppet.Yaw = Mathf.MoveTowardsAngle(puppet.Yaw, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 120f * Time.deltaTime);
                puppet.WalkAmount = Mathf.MoveTowards(puppet.WalkAmount, 1f, Time.deltaTime * 2f);
                puppet.WalkPhase += 1.1f / 0.6f * Mathf.PI * Time.deltaTime;
                Vector3 f = puppet.Feet + dir * 1.1f * Time.deltaTime;
                puppet.Feet = MeshKit.OnGround(f);
                Steps(1.1f * Time.deltaTime);
                if (t > 0.5f) foreach (var c in standingColliders) if (c != null) c.enabled = false;
                yield return null;
            }
            Hide();
        }

        IEnumerator FlashRoutine()
        {
            phoneFlash.enabled = true;
            for (float t = 0; t < 0.25f; t += Time.deltaTime)
            {
                phoneFlash.intensity = 10f * (1f - t / 0.25f);
                yield return null;
            }
            phoneFlash.enabled = false;
        }
    }
}
