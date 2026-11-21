using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SameShore
{
    // =====================================================================================================
    // Scene 8: the phone
    // =====================================================================================================

    /// <summary>
    /// The phone: held upright, its screen shows the live camera preview (low-res render texture, only while
    /// held). The trigger takes the photo: shutter, a short bright flash, the picture freezes on the screen.
    /// The moment (where the human stood, where the phone pointed) is stored for the hatchling's act.
    /// </summary>
    public class PhoneCamera : MonoBehaviour, IVRUsable
    {
        [SerializeField] private Camera previewCamera;
        [SerializeField] private Renderer screen;
        [SerializeField] private Light flash;
        [SerializeField] private Vector2Int resolution = new Vector2Int(240, 420);

        public bool PhotoTaken { get; private set; }
        public bool Armed { get; set; }
        public Transform Subject { get; set; }
        public event Action Shot;

        RenderTexture _rt;
        Texture2D _photo;
        VRGrabbable _g;
        Material _screenMat;
        int _frame;

        void Awake()
        {
            _g = GetComponent<VRGrabbable>();
            if (previewCamera != null) previewCamera.enabled = false;
            if (flash != null) flash.enabled = false;
            if (screen != null) { _screenMat = new Material(screen.sharedMaterial); screen.sharedMaterial = _screenMat; }
        }

        void Update()
        {
            bool live = _g != null && _g.IsHeld && !PhotoTaken && previewCamera != null;
            if (live && _rt == null)
            {
                _rt = new RenderTexture(resolution.x, resolution.y, 16) { name = "PhonePreview" };
                previewCamera.targetTexture = _rt;
                if (_screenMat != null) { _screenMat.SetTexture("_BaseMap", _rt); _screenMat.SetColor("_BaseColor", Color.white); }
            }
            // render the preview at half the frame rate (cheap on Quest)
            if (live && (_frame++ & 1) == 0) previewCamera.Render();
        }

        public void OnUse(VRHand hand)
        {
            if (!PhotoTaken && Armed) StartCoroutine(TakePhoto());
        }

        public void Shoot()
        {
            if (!PhotoTaken) StartCoroutine(TakePhoto());
        }

        IEnumerator TakePhoto()
        {
            PhotoTaken = true;
            AudioOneShot.PlayAt(SameShoreAssets.I.Clip("flash_whine"), transform.position, 0.4f, 0.2f, 3f);
            yield return new WaitForSeconds(0.25f);
            AudioOneShot.PlayAt(SameShoreAssets.I.Clip("camera_shutter"), transform.position, 0.8f, 0.2f, 4f);
            if (BeachLog.I != null)
            {
                Transform h = PlayerRig.Head;
                BeachLog.I.PhotoTaken = true;
                BeachLog.I.PhotoFlash = true;
                BeachLog.I.PhotoFeet = h != null ? MeshKit.OnGround(h.position) : transform.position;
                BeachLog.I.PhotoYaw = h != null ? h.eulerAngles.y : 0;
                BeachLog.I.PhotoPhone = transform.position;
                BeachLog.I.PhotoSubject = Subject != null ? Subject.position : transform.position + transform.forward;
                BeachLog.I.Record("photo taken with flash", transform.position);
            }
            if (flash != null) StartCoroutine(Flash());
            // a soft flash for the human (much milder than what the hatchling will get)
            if (ScreenFader.Instance != null) ScreenFader.Instance.Flash(new Color(1f, 1f, 0.97f), 0.22f, 0.3f);
            if (previewCamera != null && _rt != null)
            {
                previewCamera.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = _rt;
                _photo = new Texture2D(_rt.width, _rt.height, TextureFormat.RGB24, false);
                _photo.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
                _photo.Apply();
                RenderTexture.active = prev;
                if (_screenMat != null) _screenMat.SetTexture("_BaseMap", _photo);
            }
            Shot?.Invoke();
        }

        IEnumerator Flash()
        {
            flash.enabled = true;
            for (float t = 0; t < 0.22f; t += Time.deltaTime)
            {
                flash.intensity = 9f * (1f - t / 0.22f);
                yield return null;
            }
            flash.enabled = false;
        }

        void OnDestroy()
        {
            if (_rt != null) _rt.Release();
        }
    }
}
