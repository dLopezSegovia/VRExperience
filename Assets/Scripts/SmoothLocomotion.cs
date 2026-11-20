using SameShore;
using UnityEngine;

/// <summary>
/// Locomoción suave para Meta XR (OVRCameraRig), sin XR Interaction Toolkit.
/// Stick izquierdo: moverse relativo a la mirada (1.4 m/s, diseño VS1).
/// Stick derecho (eje X): girar (por saltos o suave, configurable).
/// Ponlo en el objeto raíz que contiene el OVRCameraRig y las manos/interactores
/// (el que, al moverlo, mueve cámara y manos juntas). Necesita un CharacterController.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class SmoothLocomotion : MonoBehaviour
{
    [SerializeField] private Transform head;            // CenterEyeAnchor (se busca solo si está vacío)
    [SerializeField] private float moveSpeed = 1.4f;    // m/s (diseño VS1)
    [SerializeField] private bool snapTurn = true;      // más cómodo en VR
    [SerializeField] private float snapAngle = 30f;
    [SerializeField] private float turnSpeed = 60f;     // grados/s (si snapTurn = false)
    [SerializeField] private float deadZone = 0.15f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float minHeight = 0.5f;
    [SerializeField] private float maxHeight = 2.2f;

    public bool MovementEnabled { get; set; } = true;
    public float DistanceWalked { get; private set; }

    private CharacterController _cc;
    private float _verticalVelocity;
    private bool _snapReady = true;

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
        float turn = XRInput.RightStick.x;
        if (snapTurn)
        {
            if (Mathf.Abs(turn) < 0.35f) { _snapReady = true; return; }
            if (!_snapReady || Mathf.Abs(turn) < 0.75f) return;
            _snapReady = false;
            Rotate(Mathf.Sign(turn) * snapAngle);
            return;
        }
        if (Mathf.Abs(turn) < deadZone) return;
        Rotate(turn * turnSpeed * Time.deltaTime);
    }

    private void Rotate(float degrees)
    {
        // gira alrededor de la cabeza para que el jugador no se desplace al girar
        Vector3 pivot = head.position;
        _cc.enabled = false;
        transform.RotateAround(pivot, Vector3.up, degrees);
        _cc.enabled = true;
    }

    private void Move()
    {
        Vector2 input = XRInput.LeftStick;
        input = input.magnitude < deadZone ? Vector2.zero : Vector2.ClampMagnitude(input, 1f);

        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) forward = transform.forward;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        Vector3 velocity = (forward * input.y + right * input.x) * moveSpeed;

        if (_cc.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
        _verticalVelocity += gravity * Time.deltaTime;
        velocity.y = _verticalVelocity;

        Vector3 before = transform.position;
        _cc.Move(velocity * Time.deltaTime);
        Vector3 delta = transform.position - before;
        delta.y = 0f;
        DistanceWalked += delta.magnitude;
    }

    /// <summary>Coloca al jugador con los pies en <paramref name="feet"/>, mirando hacia <paramref name="yaw"/>.</summary>
    public void PlaceAt(Vector3 feet, float yaw)
    {
        _cc.enabled = false;
        float headYaw = Vector3.SignedAngle(Vector3.forward, Vector3.ProjectOnPlane(head.forward, Vector3.up), Vector3.up);
        transform.RotateAround(head.position, Vector3.up, Mathf.DeltaAngle(headYaw, yaw));
        Vector3 offset = feet - new Vector3(head.position.x, transform.position.y, head.position.z);
        transform.position += offset;
        _cc.enabled = true;
        _verticalVelocity = 0f;
    }
}
