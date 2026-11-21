using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SameShore
{
    /// <summary>
    /// Full-body puppet of the human, posed procedurally (same segment meshes and IK maths as the player's
    /// avatar). Used in the hatchling's act as "the giant": the same person, replaying what they did that
    /// night: walking back from the nest with the flashlight, then bending down to take the photo.
    /// </summary>
    public class HumanPuppet : MonoBehaviour
    {
        public Transform torso, head;
        public Transform upperArmL, forearmL, handL, upperArmR, forearmR, handR;
        public Transform thighL, shinL, footL, thighR, shinR, footR;
        public Transform phone, flashlight;

        public float torsoLength = 0.56f, upperArm = 0.29f, forearm = 0.26f, thigh = 0.44f, shin = 0.43f;
        public float shoulderHalfWidth = 0.18f, hipHalfWidth = 0.095f, hipHeight = 0.95f;

        /// <summary>Feet centre on the ground.</summary>
        public Vector3 Feet { get; set; }
        public float Yaw { get; set; }
        public float WalkPhase { get; set; }
        public float WalkAmount { get; set; }   // 0 standing .. 1 walking
        public float Bend { get; set; }         // 0 upright .. 1 bent over (photo of something on the ground)
        public float PhoneRaise { get; set; }   // 0 .. 1 phone in front of the face/chest
        public float LightAim { get; set; } = 1f; // flashlight carried pointing ahead/down

        public Vector3 HeadPosition { get; private set; }

        void LateUpdate() => Pose();

        public void Pose()
        {
            Quaternion yaw = Quaternion.Euler(0, Yaw, 0);
            Vector3 fwd = yaw * Vector3.forward, right = yaw * Vector3.right, up = Vector3.up;
            float bob = Mathf.Abs(Mathf.Sin(WalkPhase)) * 0.03f * WalkAmount;
            float crouch = Bend * 0.22f;
            Vector3 hips = Feet + up * (hipHeight - crouch + bob - 0.02f) - fwd * (Bend * 0.12f);
            // torso pitches forward when bending
            Vector3 spineDir = Quaternion.AngleAxis(Bend * 62f + WalkAmount * 4f, right) * up;
            Vector3 neck = hips + spineDir * torsoLength;
            Place(torso, hips, spineDir, fwd, 1f);
            if (head != null)
            {
                Vector3 headDir = Quaternion.AngleAxis(Bend * 25f, right) * spineDir;
                head.SetPositionAndRotation(neck + headDir * 0.02f, Quaternion.LookRotation(Vector3.ProjectOnPlane(fwd, headDir).normalized, headDir));
                HeadPosition = neck + headDir * 0.13f;
            }
            Vector3 chest = hips + spineDir * (torsoLength * 0.86f);
            Vector3 shL = chest - right * shoulderHalfWidth, shR = chest + right * shoulderHalfWidth;

            float swing = Mathf.Sin(WalkPhase) * 0.22f * WalkAmount;
            // left arm: swings, or helps hold the phone
            Vector3 wristL = Vector3.Lerp(shL - up * 0.5f + fwd * swing - right * 0.05f,
                                          chest + fwd * 0.33f - up * 0.1f - right * 0.04f + spineDir * 0.02f, PhoneRaise * 0.85f);
            Arm(shL, wristL, -right, fwd, upperArmL, forearmL, handL, fwd);
            // right arm: carries the flashlight, or holds the phone up
            Vector3 lampHand = hips + fwd * 0.32f + right * 0.2f + up * 0.12f - fwd * swing * 0.3f;
            Vector3 phoneHand = chest + fwd * 0.34f - up * 0.06f + right * 0.03f;
            Vector3 wristR = Vector3.Lerp(Vector3.Lerp(shR - up * 0.5f - fwd * swing + right * 0.05f, lampHand, LightAim), phoneHand, PhoneRaise);
            Vector3 aim = Vector3.Lerp((fwd * 1.6f - up * 1f).normalized, Quaternion.AngleAxis(Bend * 40f + 15f, right) * fwd, PhoneRaise);
            Arm(shR, wristR, right, fwd, upperArmR, forearmR, handR, aim);
            if (flashlight != null) flashlight.SetPositionAndRotation(wristR + aim * 0.06f, Quaternion.LookRotation(aim, up));
            if (phone != null)
            {
                Vector3 camDir = Quaternion.AngleAxis(Bend * 45f + 10f, right) * fwd;
                phone.SetPositionAndRotation(Vector3.Lerp(wristL, wristR, 0.5f) + spineDir * 0.05f, Quaternion.LookRotation(camDir, spineDir));
                phone.gameObject.SetActive(PhoneRaise > 0.05f);
            }

            // legs
            Vector3 hipL = hips - right * hipHalfWidth - up * 0.04f, hipR = hips + right * hipHalfWidth - up * 0.04f;
            Vector3 fl = FootPos(-1, fwd, right, 0f), fr = FootPos(1, fwd, right, Mathf.PI);
            Leg(hipL, fl, fwd, thighL, shinL, footL);
            Leg(hipR, fr, fwd, thighR, shinR, footR);
        }

        Vector3 FootPos(int side, Vector3 fwd, Vector3 right, float phaseOffset)
        {
            float ph = WalkPhase + phaseOffset;
            Vector3 p = Feet + right * (0.11f * side) + fwd * (Mathf.Sin(ph) * 0.3f * WalkAmount) - fwd * (Bend * 0.05f * side);
            p.y = MeshKit.GroundY(p) + Mathf.Max(0, Mathf.Cos(ph)) * 0.09f * WalkAmount;
            return p;
        }

        void Arm(Vector3 shoulder, Vector3 wrist, Vector3 outward, Vector3 fwd, Transform u, Transform f, Transform h, Vector3 handDir)
        {
            Vector3 pole = shoulder + Vector3.down * 0.4f + outward * 0.3f - fwd * 0.25f;
            Vector3 elbow = SolveIK(shoulder, wrist, upperArm, forearm, pole);
            Place(u, shoulder, elbow - shoulder, fwd, 1f);
            Place(f, elbow, wrist - elbow, fwd, 1f);
            Place(h, wrist, handDir, fwd, 1f);
        }

        void Leg(Vector3 hip, Vector3 footPos, Vector3 fwd, Transform th, Transform sh, Transform ft)
        {
            Vector3 ankle = footPos + Vector3.up * 0.075f;
            Vector3 knee = SolveIK(hip, ankle, thigh, shin, hip + fwd * 0.8f);
            Place(th, hip, knee - hip, fwd, 1f);
            Place(sh, knee, ankle - knee, fwd, 1f);
            if (ft != null) ft.SetPositionAndRotation(ankle, Quaternion.LookRotation(fwd, Vector3.up));
        }

        public static Vector3 SolveIK(Vector3 a, Vector3 target, float l1, float l2, Vector3 pole)
        {
            Vector3 ab = target - a;
            float d = Mathf.Clamp(ab.magnitude, 0.01f, l1 + l2 - 0.001f);
            Vector3 dir = ab.normalized;
            float x = (l1 * l1 - l2 * l2 + d * d) / (2 * d);
            float h = Mathf.Sqrt(Mathf.Max(0, l1 * l1 - x * x));
            Vector3 bend = Vector3.ProjectOnPlane(pole - a, dir);
            if (bend.sqrMagnitude < 1e-6f) bend = Vector3.ProjectOnPlane(Vector3.forward, dir);
            return a + dir * x + bend.normalized * h;
        }

        public static void Place(Transform t, Vector3 from, Vector3 along, Vector3 fwd, float stretch)
        {
            if (t == null || along.sqrMagnitude < 1e-8f) return;
            Vector3 upv = along.normalized;
            Vector3 f = Vector3.ProjectOnPlane(fwd, upv);
            if (f.sqrMagnitude < 1e-6f) f = Vector3.ProjectOnPlane(Vector3.forward, upv);
            if (f.sqrMagnitude < 1e-6f) f = Vector3.ProjectOnPlane(Vector3.right, upv);
            t.SetPositionAndRotation(from, Quaternion.LookRotation(f.normalized, upv));
            Vector3 s = t.localScale;
            s.y = stretch;
            t.localScale = s;
        }
    }
}
