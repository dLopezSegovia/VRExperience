using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// The gulls, as controlled tension: exactly two attacks before the ending, both real, both announced
    /// (call -> shadow on the sand -> the bird approaching from in front, low enough to be seen from 8 cm), both
    /// dodgeable, neither lethal.
    ///   1) the swoop (corridor trigger at the light string): the gull comes in from the sea, homes on where the
    ///      hatchling is going, commits ~1 s before impact and strafes it. Changing pace or direction (or a burst,
    ///      or hiding beside something) makes it miss. A hit = short stun, a small push, a little strength lost.
    ///   2) the dive (corridor trigger after the castle): it circles, hovers and screams; a ring follows the
    ///      hatchling and freezes 1 s before the dive; leaving the ring (or hiding) avoids it.
    /// Locomotion is never blocked for more than a second; death is reserved for the ending.
    /// </summary>
    public class GullEncounters : MonoBehaviour
    {
        [SerializeField] private Seagull gull;
        [SerializeField] private Transform sea;
        [SerializeField] private Material shadowMaterial;
        [SerializeField] private Material ringMaterial;
        [SerializeField] private float ringRadius = 0.3f;
        [SerializeField] private float warningSeconds = 2.6f;
        [SerializeField] private float swoopHitRadius = 0.22f;
        [SerializeField] private TurtleVitality vitality;
        [SerializeField] private float strikeDamage = 0.15f;

        public bool FirstDone { get; private set; }
        public bool SecondDone { get; private set; }
        public bool Struck { get; private set; }
        public bool Dodged { get; private set; }
        /// <summary>Attack passes actually flown at the hatchling (dives/swoops), and how many connected.</summary>
        public int Attacks { get; private set; }
        public int Hits { get; private set; }
        public bool SwoopStruck { get; private set; }
        public event Action<string, float> Message;
        public Seagull Gull => gull;

        GameObject _shadow, _ring;
        Coroutine _run;

        void Say(string s, float t) => Message?.Invoke(s, t);

        public void ResetEncounters()
        {
            Stop();
            FirstDone = SecondDone = Struck = Dodged = SwoopStruck = false;
            Attacks = Hits = 0;
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

        public void StartPass(TurtleController t) => StartSwoop(t);

        public void StartSwoop(TurtleController t)
        {
            if (FirstDone || gull == null) return;
            EnsureVisuals();
            if (_run != null) StopCoroutine(_run);
            _run = StartCoroutine(Swoop(t));
        }

        IEnumerator Swoop(TurtleController t)
        {
            Vector3 p = t.transform.position;
            Vector3 seaDir = Vector3.ProjectOnPlane(sea.position - p, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, seaDir);
            // 1) heard first: a call from over the water, in front of the hatchling
            gull.TeleportTo(p + seaDir * 16f + side * 4f + Vector3.up * 7f);
            gull.Call(0.9f);
            yield return new WaitForSeconds(0.8f);
            // 2) it comes in low, straight up the beach: visible ahead, its shadow racing over the sand
            gull.FlyTo(p + seaDir * 6f + side * 0.8f + Vector3.up * 2.2f, false);
            _shadow.SetActive(true);
            Say("A cry over the waves... a shadow skims the sand.\nIt is coming straight at you!", 4f);
            XRInput.HapticBoth(0.15f, 0.3f, 0.2f);
            for (float k = 0; k < 6f; k += Time.deltaTime)
            {
                PlaceShadow(gull.transform.position, 0.45f);
                if (Flat(gull.transform.position - t.transform.position) < 6.5f) break;
                yield return null;
            }
            // 3) the attack: homing on where the hatchling is heading, committing ~1 s before impact
            gull.Screech();
            Attacks++;
            Vector3 strike = Lead(t, 1f);
            bool arrived = false;
            gull.Dive(strike + Vector3.up * 0.06f, () => arrived = true);
            AudioOneShot.Play2D(SameShoreAssets.I.Clip("wings_fast"), 0.55f);
            bool committed = false;
            for (float k = 0; k < 4f && !arrived; k += Time.deltaTime)
            {
                float dist = (gull.transform.position - strike).magnitude;
                if (!committed)
                {
                    strike = Lead(t, Mathf.Clamp(dist / 3.5f, 0.3f, 1.2f));
                    gull.Retarget(strike + Vector3.up * 0.06f);
                    if (dist < 3.5f) committed = true;   // from here on it cannot correct: dodge now
                }
                PlaceShadow(gull.transform.position, 0.5f);
                yield return null;
            }
            Vector3 tp = t.transform.position;
            bool sheltered = Sheltered(tp);
            if (Flat(tp - strike) < swoopHitRadius && !sheltered)
            {
                SwoopStruck = true;
                Land(t, strike, seaDir, "Talons scrape your shell - it lost its grip!\nKeep going before it turns.");
            }
            else
            {
                Dodged = true;
                Say(sheltered ? "Pressed against something big, the gull can't reach you." : "It missed! Wings thunder over your head.", 4f);
            }
            // 4) it pulls up over the dunes and wheels away (it will be back)
            gull.Leave(-seaDir + side * 0.5f);
            for (float k = 0; k < 3f; k += Time.deltaTime) { PlaceShadow(gull.transform.position, 0.5f); yield return null; }
            _shadow.SetActive(false);
            FirstDone = true;
            _run = null;
        }

        /// <summary>Where the hatchling will be in 'seconds' if it keeps going (the gull aims there).</summary>
        static Vector3 Lead(TurtleController t, float seconds)
        {
            Vector3 v = t.transform.forward * t.CurrentSpeed;
            return t.transform.position + new Vector3(v.x, 0f, v.z) * seconds;
        }

        void Land(TurtleController t, Vector3 strike, Vector3 seaDir, string line)
        {
            Struck = true;
            Hits++;
            AudioOneShot.Play2D(SameShoreAssets.I.Clip("gull_strike"), 0.7f);
            // a soft dark-red pulse at the edges (no white flash), a short haptic thump
            if (ScreenFader.Instance != null) ScreenFader.Instance.Flash(new Color(0.25f, 0.02f, 0.02f), 0.25f, 0.6f);
            XRInput.HapticBoth(0.6f, 0.15f);
            t.Stun(0.8f);
            Vector3 tp = t.transform.position;
            Vector3 push = Vector3.ProjectOnPlane(tp - strike, Vector3.up);
            if (push.sqrMagnitude < 1e-4f) push = seaDir;
            t.Nudge(push.normalized * 0.1f);
            if (vitality != null) vitality.Hurt(strikeDamage);
            BeachLog.I?.Record("a gull struck the hatchling", tp);
            Say(line, 5f);
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

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
            // warning: it hangs in the wind in front of the hatchling, screams; a ring follows it on the sand and
            // freezes one second before the dive: that is the moment to get out of it
            Vector3 strike = t.transform.position;
            gull.Hover(strike + seaDir * 1.2f + Vector3.up * 2.4f);
            gull.Screech();
            Attacks++;
            _ring.SetActive(true);
            _ring.transform.rotation = Quaternion.LookRotation(Vector3.down);
            Say("It is diving at you!\nWhen it screams again and the ring jumps ahead, change course - or hide close to something.", Mathf.Max(3f, warningSeconds + 1f));
            XRInput.HapticBoth(0.3f, 0.25f, 0.6f);
            bool frozen = false;
            for (float k = 0; k < warningSeconds + 0.7f; k += Time.deltaTime)
            {
                if (k < warningSeconds) { strike = t.transform.position; gull.Retarget(strike + seaDir * 1.2f + Vector3.up * 2.4f); }
                else if (!frozen)
                {
                    // it picks its spot: where the hatchling will be if it keeps crawling the same way
                    frozen = true;
                    strike = Lead(t, 1.4f);
                    gull.Screech();
                }
                _ring.transform.position = MeshKit.OnGround(strike, 0.012f);
                float pulse = 1f + (k < warningSeconds ? 0.06f : 0.16f) * Mathf.Sin(k * (k < warningSeconds ? 6f : 14f));
                _ring.transform.localScale = Vector3.one * ringRadius * 2.4f * pulse;
                PlaceShadow(gull.transform.position, 0.5f + k * 0.12f);
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
            if (inside && !hidden) Land(t, strike, seaDir, "The beak grazes your shell - but it can't hold on.\nKeep going!");
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
