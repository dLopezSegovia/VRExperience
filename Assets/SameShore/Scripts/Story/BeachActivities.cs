using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SameShore
{
    /// <summary>Keeps a scored can inside the bin (it rattles but does not jump out).</summary>
    public class StayInBin : MonoBehaviour
    {
        void Start()
        {
            var g = GetComponent<VRGrabbable>();
            if (g != null) g.grabbingEnabled = false;
        }
    }

    /// <summary>Shared one-shot sand puff.</summary>
    public static class SandFX
    {
        public static ParticleSystem CreateBurst(Transform parent, Material mat)
        {
            var go = new GameObject("SandBurst");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.018f);
            main.startColor = new Color(0.78f, 0.68f, 0.52f);
            main.gravityModifier = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            main.playOnAwake = false;
            var em = ps.emission;
            em.rateOverTime = 0;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.05f;
            go.transform.rotation = Quaternion.Euler(-90, 0, 0);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
            ps.Play();
            return ps;
        }
    }
}
