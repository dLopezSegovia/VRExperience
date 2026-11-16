using UnityEngine;

/// <summary>
/// Linterna agarrable. Conecta el evento Activated del XR Grab Interactable
/// (Interactable Events > Activate > Activated) a Flashlight.Toggle().
/// </summary>
public class Flashlight : MonoBehaviour
{
    [SerializeField] private Light beam;                 // el Spot Light hijo
    [SerializeField] private Renderer lensRenderer;      // opcional
    [SerializeField] private Material lensOnMaterial;    // opcional
    [SerializeField] private Material lensOffMaterial;   // opcional
    [SerializeField] private AudioSource clickSound;     // opcional
    [SerializeField] private bool startOn = true;

    public bool IsOn => beam != null && beam.enabled;

    private void Start() => Apply(startOn);

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
