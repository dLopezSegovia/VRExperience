using UnityEngine;

/// <summary>
/// Locomoción suave para Meta XR (OVRCameraRig), sin XR Interaction Toolkit.
/// Stick izquierdo: moverse relativo a la mirada. Stick derecho (eje X): girar.
/// Ponlo en el objeto raíz que contiene el OVRCameraRig y las manos/interactores
/// (el que, al moverlo, mueve cámara y manos juntas). Necesita un CharacterController.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class SmoothLocomotion : MonoBehaviour
{
    [SerializeField] private Transform head;            // CenterEyeAnchor (se busca solo si está vacío)
    [SerializeField] private float moveSpeed = 1.4f;    // m/s (diseño VS1)
    [SerializeField] private float turnSpeed = 60f;     // grados/s
    [SerializeField] private float deadZone = 0.15f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float minHeight = 0.5f;
    [SerializeField] private float maxHeight = 2.2f;

    public bool MovementEnabled { get; set; } = true;

    private CharacterController _cc;
    private float _verticalVelocity;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();

        if (head == null)
        {
            OVRCameraRig rig = GetComponentInChildren<OVRCameraRig>();
            if (rig != null) head = rig.centerEyeAnchor;
        }

        if (head == null)
            Debug.LogError("[SmoothLocomotion] No encuentro la cabeza. Asigna CenterEyeAnchor en el Inspector.");
    }

    private void Update()
    {
        if (head == null) return;

        SyncCapsuleToHead();
        if (!MovementEnabled) return;

        Turn();
        Move();
    }

    // La cápsula sigue a la cabeza para que, si el jugador camina físicamente
    // por su habitación, el cuerpo virtual también choque con las paredes.
    private void SyncCapsuleToHead()
    {
        Vector3 local = transform.InverseTransformPoint(head.position);
        float height = Mathf.Clamp(local.y, minHeight, maxHeight);
        _cc.height = height;
        _cc.center = new Vector3(local.x, height * 0.5f + _cc.skinWidth, local.z);
    }

    private void Turn()
    {
        float turn = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick).x;
        if (Mathf.Abs(turn) < deadZone) return;

        transform.RotateAround(head.position, Vector3.up, turn * turnSpeed * Time.deltaTime);
    }

    private void Move()
    {
        Vector2 input = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
        input = input.magnitude < deadZone ? Vector2.zero : Vector2.ClampMagnitude(input, 1f);

        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) forward = transform.forward;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        Vector3 velocity = (forward * input.y + right * input.x) * moveSpeed;

        if (_cc.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
        _verticalVelocity += gravity * Time.deltaTime;
        velocity.y = _verticalVelocity;

        _cc.Move(velocity * Time.deltaTime);
    }
}
