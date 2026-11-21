using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// The plastic wrapper. Lies on the towel after the first bite; a gust then carries it along a
    /// deterministic, tumbling path (no physics, no glitches) to the wet sand next to the hatchlings' path,
    /// where it keeps breathing in the breeze like a small jellyfish.
    /// </summary>
    public class WindBag : MonoBehaviour
    {
        public enum Phase { Wrapped, OnTowel, Flying, Landed }

        [SerializeField] private Transform landing;
        [SerializeField] private Transform towelRest;
        [SerializeField] private float flightSeconds = 3.6f;
        [SerializeField] private Transform visual;

        public Phase State { get; private set; } = Phase.Wrapped;
        public Transform Landing => landing;
        public float Breath { get; set; } = 1f;   // flutter amplitude (the hatchling phase makes it pulse like a bell)
        public bool Bitten { get; set; }

        Vector3 _visualScale = Vector3.one;
        float _seed;

        void Awake()
        {
            _seed = UnityEngine.Random.value * 10f;
            if (visual != null) _visualScale = visual.localScale;
        }

        public void Unwrap(Transform sandwich)
        {
            if (State != Phase.Wrapped) return;
            State = Phase.OnTowel;
            transform.SetParent(null, true);
            Vector3 rest = towelRest != null ? towelRest.position : MeshKit.OnGround(sandwich.position, 0.01f);
            StartCoroutine(Drop(rest));
            AudioOneShot.PlayAt(SameShoreAssets.I.Clip("wrapper_rustle"), transform.position, 0.6f, 0.3f, 5f);
        }

        IEnumerator Drop(Vector3 rest)
        {
            Vector3 a = transform.position;
            Quaternion r0 = transform.rotation, r1 = Quaternion.Euler(0, UnityEngine.Random.Range(0, 360), 0);
            for (float t = 0; t < 0.8f; t += Time.deltaTime)
            {
                float k = t / 0.8f;
                transform.position = Vector3.Lerp(a, rest, k * k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.05f;
                transform.rotation = Quaternion.Slerp(r0, r1, k);
                yield return null;
            }
            transform.SetPositionAndRotation(rest, r1);
        }

        /// <summary>The gust: starts from wherever the bag is now and ends at the landing spot.</summary>
        public IEnumerator BlowAway()
        {
            if (State == Phase.Wrapped) Unwrap(transform);
            yield return new WaitForSeconds(0.85f);
            State = Phase.Flying;
            AudioOneShot.PlayAt(SameShoreAssets.I.Clip("wind_gust"), transform.position, 0.9f, 1f, 20f);
            AudioOneShot.PlayAt(SameShoreAssets.I.Clip("plastic_crinkle"), transform.position, 0.5f, 0.5f, 10f);
            Vector3 p0 = transform.position;
            Vector3 p3 = landing.position;
            Vector3 dir = Vector3.ProjectOnPlane(p3 - p0, Vector3.up);
            Vector3 side = Vector3.Cross(Vector3.up, dir.normalized);
            Vector3 p1 = p0 + Vector3.up * 1.6f + dir * 0.25f + side * 0.6f;
            Vector3 p2 = p3 + Vector3.up * 1.1f - dir * 0.3f - side * 0.4f;
            Quaternion r0 = transform.rotation;
            for (float t = 0; t < flightSeconds; t += Time.deltaTime)
            {
                float k = t / flightSeconds;
                float e = 1f - Mathf.Pow(1f - k, 1.6f);
                Vector3 p = Bezier(p0, p1, p2, p3, e);
                p += side * Mathf.Sin(k * 9f) * 0.12f * (1 - k) + Vector3.up * Mathf.Sin(k * 13f) * 0.06f * (1 - k);
                float gy = MeshKit.GroundY(p);
                if (p.y < gy + 0.02f) p.y = gy + 0.02f;
                transform.position = p;
                transform.rotation = r0 * Quaternion.Euler(k * 540f, k * 300f, Mathf.Sin(k * 7f) * 60f);
                yield return null;
            }
            // two little skips on the sand, then it settles
            Vector3 a = transform.position;
            for (int hop = 0; hop < 2; hop++)
            {
                Vector3 b = a + dir.normalized * (0.25f - hop * 0.1f);
                Quaternion ra = transform.rotation, rb = Quaternion.Euler(0, transform.eulerAngles.y + 40, 0);
                for (float t = 0; t < 0.45f; t += Time.deltaTime)
                {
                    float k = t / 0.45f;
                    Vector3 p = Vector3.Lerp(a, b, k);
                    p.y = MeshKit.GroundY(p) + 0.015f + Mathf.Sin(k * Mathf.PI) * (0.08f - hop * 0.04f);
                    transform.position = p;
                    transform.rotation = Quaternion.Slerp(ra, rb, k);
                    yield return null;
                }
                a = b;
            }
            transform.position = MeshKit.OnGround(a, 0.012f);
            landing.position = transform.position;
            State = Phase.Landed;
            if (BeachLog.I != null)
            {
                BeachLog.I.BagLost = true;
                BeachLog.I.BagPosition = transform.position;
                BeachLog.I.Record("plastic bag blown to the wet sand", transform.position);
            }
        }

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1 - t;
            return u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d;
        }

        public void ForceLanded()
        {
            StopAllCoroutines();
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(MeshKit.OnGround(landing.position, 0.012f), Quaternion.Euler(0, 35, 0));
            State = Phase.Landed;
            if (BeachLog.I != null) { BeachLog.I.BagLost = true; BeachLog.I.BagPosition = transform.position; }
        }

        void Update()
        {
            if (visual == null || State == Phase.Wrapped) return;
            // breathing in the breeze; stronger "bell" pulses when Breath > 1 (seen by the hatchling)
            float t = Time.time + _seed;
            float pulse = Mathf.Sin(t * 1.7f) * 0.5f + 0.5f;
            float b = State == Phase.Flying ? 1f : Breath;
            float up = 1f + (0.12f + 0.1f * pulse) * b * (State == Phase.Landed ? 1f : 0.5f);
            float wide = 1f - 0.06f * pulse * b;
            visual.localScale = new Vector3(_visualScale.x * wide, _visualScale.y * up, _visualScale.z * wide);
            visual.localRotation = Quaternion.Euler(Mathf.Sin(t * 2.3f) * 4f * b, 0, Mathf.Sin(t * 1.9f) * 5f * b);
        }
    }
}
