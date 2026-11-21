using System;
using System.Collections.Generic;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// A physical button (speaker play/volume, the fireworks remote). Pressed by touching it with a
    /// fingertip (either controller), by pulling the trigger with the hand close to it, or in Desktop
    /// Testing mode with E / left click while aiming at it. The cap moves in and clicks.
    /// </summary>
    public class PokeButton : MonoBehaviour
    {
        public static readonly List<PokeButton> All = new List<PokeButton>();
        public static PokeButton DesktopAimed { get; private set; }

        public string Label = "button";
        [SerializeField] private Transform cap;
        [SerializeField] private float radius = 0.035f;
        [SerializeField] private float travel = 0.006f;
        [SerializeField] private float cooldown = 0.45f;
        public bool interactable = true;

        public event Action Pressed;
        public int Presses { get; private set; }

        Vector3 _capRest;
        float _last = -9f;
        readonly bool[] _inside = new bool[2];

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Awake()
        {
            if (cap != null) _capRest = cap.localPosition;
        }

        public void SetCap(Transform c) { cap = c; _capRest = c.localPosition; }

        void Update()
        {
            if (cap != null)
                cap.localPosition = Vector3.Lerp(cap.localPosition, _capRest, Time.deltaTime * 12f);
            var gm = GameManager.Instance;
            if (!interactable || gm == null || gm.Current != GameState.Human || !VRHand.Enabled) return;

            if (DesktopTestingMode.Active)
            {
                if (VRHand.Right != null && VRHand.Right.Held != null) return;
                Transform h = PlayerRig.Head;
                if (h == null) return;
                bool aimed = false;
                float best = float.MaxValue;
                Transform root = PlayerRig.Root;
                foreach (var x in Physics.RaycastAll(h.position, h.forward, 3.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                {
                    if (x.distance >= best || (root != null && x.collider.transform.IsChildOf(root))) continue;
                    if (x.collider.isTrigger && x.collider.GetComponent<PokeButton>() == null) continue;
                    best = x.distance;
                    aimed = x.collider.transform == transform || x.collider.transform.IsChildOf(transform);
                }
                if (aimed) DesktopAimed = this;
                else if (DesktopAimed == this) DesktopAimed = null;
                if (aimed && (XRInput.KeyDown(XRInput.KeyId.E) || (DesktopTestingMode.Instance != null && DesktopTestingMode.Instance.WorldClickDown)))
                    Press(null);
                return;
            }
            for (int i = 0; i < 2; i++)
            {
                VRHand hand = i == 0 ? VRHand.Left : VRHand.Right;
                if (hand == null || !hand.Tracked) continue;
                bool inside = (hand.Fingertip - transform.position).magnitude < radius;
                bool near = (hand.Position - transform.position).magnitude < radius + 0.1f;
                if ((inside && !_inside[i]) || (near && hand.Held == null && hand.UseDown)) Press(hand);
                _inside[i] = inside;
            }
        }

        public void Press(VRHand hand)
        {
            if (Time.time - _last < cooldown) return;
            _last = Time.time;
            Presses++;
            if (cap != null) cap.localPosition = _capRest - cap.parent.InverseTransformDirection(transform.up) * travel * (1f / Mathf.Max(1e-4f, cap.parent.lossyScale.y));
            AudioOneShot.PlayAt(SameShoreAssets.I.buttonClick, transform.position, 0.6f, 0.3f, 5f);
            if (hand != null) XRInput.Haptic(hand.hand, 0.35f, 0.04f);
            Pressed?.Invoke();
        }
    }
}
