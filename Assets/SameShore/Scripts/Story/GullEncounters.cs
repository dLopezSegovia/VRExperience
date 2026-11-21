using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// The gulls, as controlled tension: at most two encounters, both announced by shadow, sound and movement.
    ///   1) a pass: a shadow sweeps over the sand, the gull calls and flies on (no attack).
    ///   2) an approach: it circles, hovers and screams, a ring marks where it will strike; crawling out of the
    ///      ring (or hiding next to something) is enough to avoid it. If it connects: a short stun and a push,
    ///      never death. Then it leaves for good.
    /// </summary>
    public class GullEncounters : MonoBehaviour
    {
        [SerializeField] private Seagull gull;
        [SerializeField] private Transform sea;
        [SerializeField] private Material shadowMaterial;
        [SerializeField] private Material ringMaterial;
        [SerializeField] private float ringRadius = 0.3f;
        [SerializeField] private float warningSeconds = 2.6f;

        public bool FirstDone { get; private set; }
        public bool SecondDone { get; private set; }
        public bool Struck { get; private set; }
        public bool Dodged { get; private set; }
        public event Action<string, float> Message;

        GameObject _shadow, _ring;
        Coroutine _run;

        void Say(string s, float t) => Message?.Invoke(s, t);

        public void ResetEncounters()
        {
            Stop();
            FirstDone = SecondDone = Struck = Dodged = false;
        }

        public void Stop()
        {
            if (_run != null) StopCoroutine(_run);
            _run = null;
            if (_shadow != null) _shadow.SetActive(false);
            if (_ring != null) _ring.SetActive(false);
        }

        void EnsureVisuals()
        {
            if (_shadow == null)
            {
                _shadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _shadow.name = "GullShadow";
                Destroy(_shadow.GetComponent<Collider>());
                _shadow.GetComponent<MeshRenderer>().sharedMaterial = new Material(shadowMaterial);
                _shadow.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _shadow.SetActive(false);
            }
            if (_ring == null)
            {
                _ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _ring.name = "GullWarningRing";
                Destroy(_ring.GetComponent<Collider>());
                var m = new Material(ringMaterial);
                if (SameShoreAssets.I.ringTexture != null) m.SetTexture("_MainTex", SameShoreAssets.I.ringTexture);
                _ring.GetComponent<MeshRenderer>().sharedMaterial = m;
                _ring.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _ring.SetActive(false);
            }
        }

        void PlaceShadow(Vector3 birdPos, float size)
        {
            Vector3 g = MeshKit.OnGround(birdPos, 0.006f);
            float h = Mathf.Max(0.3f, birdPos.y - g.y);
            _shadow.transform.position = g;
            Vector3 fwd = Vector3.ProjectOnPlane(gull.Velocity, Vector3.up);
            _shadow.transform.rotation = Quaternion.LookRotation(Vector3.down, fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward);
            float s = size * (1f + h * 0.08f);
            _shadow.transform.localScale = new Vector3(s * 1.7f, s, 1);
            var c = new Color(0, 0, 0, Mathf.Clamp(0.65f - h * 0.05f, 0.25f, 0.6f));
            _shadow.GetComponent<MeshRenderer>().sharedMaterial.SetColor("_BaseColor", c);
        }

        public void StartPass(TurtleController t)
        {
            if (FirstDone || gull == null) return;
            EnsureVisuals();
            _run = StartCoroutine(Pass(t));
        }

        IEnumerator Pass(TurtleController t)
        {
            Vector3 p = t.transform.position;
            Vector3 seaDir = Vector3.ProjectOnPlane(sea.position - p, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, seaDir);
            Vector3 from = p + seaDir * 18f + side * 6f + Vector3.up * 7f;
            Vector3 to = p - seaDir * 14f - side * 5f + Vector3.up * 6f;
            gull.TeleportTo(from);
            gull.FlyTo(to, false);
            gull.Call(0.9f);
            _shadow.SetActive(true);
            bool said = false;
            for (float t0 = 0; t0 < 7f; t0 += Time.deltaTime)
            {
                PlaceShadow(gull.transform.position, 0.45f);
                float dd = Vector2.Distance(new Vector2(gull.transform.position.x, gull.transform.position.z), new Vector2(t.transform.position.x, t.transform.position.z));
                if (!said && dd < 6f)
                {
                    said = true;
                    Say("A shadow sweeps across the sand.\nSomething is watching from above.", 5f);
                    XRInput.HapticBoth(0.15f, 0.3f, 0.2f);
                }
                yield return null;
            }
            _shadow.SetActive(false);
            gull.Leave(seaDir);
            FirstDone = true;
            _run = null;
        }

        public void StartAttack(TurtleController t)
        {
            if (SecondDone || gull == null) return;
            EnsureVisuals();
            if (_run != null) StopCoroutine(_run);
            _run = StartCoroutine(Attack(t));
        }

        IEnumerator Attack(TurtleController t)
        {
            Vector3 p = t.transform.position;
            Vector3 seaDir = Vector3.ProjectOnPlane(sea.position - p, Vector3.up).normalized;
            gull.TeleportTo(p + seaDir * 16f + Vector3.up * 8f);
            gull.Circle(t.transform, 3.5f, 4.5f);
            gull.Call(1f);
            _shadow.SetActive(true);
            Say("The gull is back. It circles lower and lower...", 4f);
            for (float k = 0; k < 4.5f; k += Time.deltaTime)
            {
                PlaceShadow(gull.transform.position, 0.5f);
                yield return null;
            }
            // warning: it hangs in the wind above the hatchling, screams; a ring marks the strike zone
            Vector3 strike = t.transform.position;
            gull.Hover(strike + Vector3.up * 2.6f);
            gull.Screech();
            _ring.SetActive(true);
            _ring.transform.position = MeshKit.OnGround(strike, 0.012f);
            _ring.transform.rotation = Quaternion.LookRotation(Vector3.down);
            Say("It is diving at you!\nCrawl out of the ring - or hide close to something.", Mathf.Max(3f, warningSeconds + 1f));
            XRInput.HapticBoth(0.3f, 0.25f, 0.6f);
            for (float k = 0; k < warningSeconds; k += Time.deltaTime)
            {
                float pulse = 1f + 0.12f * Mathf.Sin(k * 14f);
                _ring.transform.localScale = Vector3.one * ringRadius * 2.4f * pulse;
                PlaceShadow(gull.transform.position, 0.5f + k * 0.15f);
                yield return null;
            }
            bool arrived = false;
            gull.Dive(strike + Vector3.up * 0.08f, () => arrived = true);
            AudioOneShot.Play2D(SameShoreAssets.I.Clip("wings_fast"), 0.6f);
            for (float k = 0; k < 2.5f && !arrived; k += Time.deltaTime)
            {
                PlaceShadow(gull.transform.position, 0.7f);
                yield return null;
            }
            _ring.SetActive(false);
            Vector3 tp = t.transform.position;
            bool inside = new Vector2(tp.x - strike.x, tp.z - strike.z).magnitude < ringRadius;
            bool hidden = Sheltered(tp);
            if (inside && !hidden)
            {
                Struck = true;
                AudioOneShot.Play2D(SameShoreAssets.I.Clip("gull_strike"), 0.75f);
                if (ScreenFader.Instance != null) ScreenFader.Instance.Flash(new Color(0.25f, 0.02f, 0.02f), 0.3f, 0.5f);
                t.Stun(1.0f);
                Vector3 push = Vector3.ProjectOnPlane(tp - strike, Vector3.up);
                if (push.sqrMagnitude < 1e-4f) push = seaDir;
                t.Nudge(push.normalized * 0.12f);
                Say("The beak grazes your shell - but it can't hold on.\nKeep going!", 5f);
            }
            else
            {
                Dodged = true;
                Say(hidden ? "Hidden in the shadow of something big, the gull misses you." : "It missed you! Keep going.", 4f);
            }
            gull.Leave(seaDir);
            for (float k = 0; k < 3f; k += Time.deltaTime) { PlaceShadow(gull.transform.position, 0.5f); yield return null; }
            _shadow.SetActive(false);
            SecondDone = true;
            _run = null;
        }

        /// <summary>Something solid right above or tightly beside the hatchling (a can, the castle, a post).</summary>
        public static bool Sheltered(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 0.05f, Vector3.up, 1.2f, ~0, QueryTriggerInteraction.Ignore)) return true;
            int hits = 0;
            for (int i = 0; i < 8; i++)
            {
                Vector3 dir = Quaternion.Euler(0, i * 45f, 0) * Vector3.forward;
                if (Physics.Raycast(p + Vector3.up * 0.03f, dir, 0.09f, ~0, QueryTriggerInteraction.Ignore)) hits++;
            }
            return hits >= 2;
        }
    }
}
