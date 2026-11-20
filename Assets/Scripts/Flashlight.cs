using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Linterna para Meta Interaction SDK. Mientras está agarrada, el gatillo (índice)
/// de cualquiera de los dos mandos la enciende o apaga.
/// Sustituye a la versión anterior de Flashlight.cs.
/// </summary>
public class Flashlight : MonoBehaviour
{
    [SerializeField] private Grabbable grabbable;        // el Grabbable de este objeto
    [SerializeField] private Light beam;                 // el Spot Light hijo
    [SerializeField] private Renderer lensRenderer;      // opcional
    [SerializeField] private Material lensOnMaterial;    // opcional
    [SerializeField] private Material lensOffMaterial;   // opcional
    [SerializeField] private AudioSource clickSound;     // opcional
    [SerializeField] private bool startOn = true;

    public bool IsOn => beam != null && beam.enabled;

    private void Reset() => grabbable = GetComponent<Grabbable>();

    private void Start() => Apply(startOn);

    private void Update()
    {
        if (grabbable == null || grabbable.SelectingPointsCount == 0) return;

        // Gatillo o botón A/X mientras se sostiene (por si el agarre usa el gatillo)
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger) ||
            OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger) ||
            OVRInput.GetDown(OVRInput.Button.One) ||
            OVRInput.GetDown(OVRInput.Button.Three))
        {
            Toggle();
            SameShore.XRInput.HapticBoth(0.3f, 0.04f);
        }
    }

    public void Toggle()
    {
        Apply(!IsOn);
        if (clickSound != null) clickSound.Play();
    }

    private void Apply(bool on)
    {
        if (beam != null) beam.enabled = on;
        if (lensRenderer != null && lensOnMaterial != null && lensOffMaterial != null)
            lensRenderer.sharedMaterial = on ? lensOnMaterial : lensOffMaterial;
    }
}
