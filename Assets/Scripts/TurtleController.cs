using UnityEngine;

/// <summary>
/// Controlador de la cría de tortuga (VS2). Va en el objeto "TurtleBody", que lleva un
/// CharacterController. PlayerPhaseSwitcher cuelga el rig del jugador de este objeto,
/// así que no hace falta tocar el player: moviendo/rotando TurtleBody se mueve el jugador.
///
/// - Stick (eje Y): "brazadas". Cada empujón hacia delante = una ráfaga corta de movimiento.
///   Hay que soltar el stick y volver a empujar para dar otra. No hay strafe ni marcha atrás.
/// - Stick (eje X): gira el cuerpo despacio.
/// - Luz: la dirección de cada brazada se desvía hacia la fuente de luz con más influencia
///   (linterna encendida apuntando a la tortuga, brillo del mar...).
/// - Objetos ligeros se empujan (poco); los pesados bloquean.
/// - Zonas: Slow reduce las brazadas, Goal termina la partida.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class TurtleController : MonoBehaviour
{
    [Header("Brazada")]
    [SerializeField] private float strokeDistance = 0.25f;     // metros de mundo por brazada
    [SerializeField] private float strokeDuration = 0.6f;      // segundos que dura la ráfaga
    [SerializeField] private float strokeThreshold = 0.7f;     // stick Y para dar la brazada
    [SerializeField] private float rearmThreshold = 0.3f;      // stick Y por debajo del cual se rearma

    [Header("Giro")]
    [SerializeField] private float turnSpeed = 25f;            // grados/s (diseño VS2)
    [SerializeField] private float stickDeadZone = 0.15f;

    [Header("Atracción de la luz")]
    [SerializeField, Range(0f, 1f)] private float lightSteer = 0.8f; // cuánto se desvía la brazada hacia la luz
    [SerializeField] private float minLightDistance = 0.1f;

    [Header("Empuje de objetos")]
    [SerializeField] private float maxPushMass = 1f;           // más pesado que esto = bloquea
    [SerializeField, Range(0f, 1f)] private float pushStrength = 0.3f; // diseño: 1:0.3
    [SerializeField] private float pushGain = 10f;

    [Header("Física")]
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float zoneProbeRadius = 0.05f;

    public float CurrentSpeed { get; private set; }

    private static readonly Collider[] Buffer = new Collider[8];

    private CharacterController _cc;
    private float _strokeTime;
    private bool _armed;
    private Vector3 _strokeDir;
    private float _vertical;
    private float _speedMultiplier = 1f;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        enabled = false; // lo activa PlayerPhaseSwitcher con Begin()
    }

    public void Begin()
    {
        _strokeTime = strokeDuration; // sin brazada en curso
        _armed = false;               // hay que soltar el stick antes de la primera brazada
        _vertical = 0f;
        CurrentSpeed = 0f;
        enabled = true;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        Vector2 stick = ReadStick();

        CheckZones();
        if (!enabled) return; // la zona Goal nos puede haber apagado

        // Giro lento del cuerpo
        if (Mathf.Abs(stick.x) > stickDeadZone)
            transform.Rotate(0f, stick.x * turnSpeed * dt, 0f);

        // Brazadas
        if (_strokeTime >= strokeDuration)
        {
            if (stick.y < rearmThreshold) _armed = true;
            if (_armed && stick.y > strokeThreshold) StartStroke();
        }

        Vector3 motion = Vector3.zero;
        CurrentSpeed = 0f;
        if (_strokeTime < strokeDuration)
        {
            float t = _strokeTime / strokeDuration;
            // Ráfaga que arranca fuerte y se frena: el área bajo la curva = strokeDistance
            CurrentSpeed = 3f * strokeDistance / strokeDuration * (1f - t) * (1f - t) * _speedMultiplier;
            motion = _strokeDir * CurrentSpeed;
            _strokeTime += dt;
        }

        if (_cc.isGrounded && _vertical < 0f) _vertical = -0.5f;
        _vertical += gravity * dt;
        motion.y = _vertical;

        _cc.Move(motion * dt);
    }

    private Vector2 ReadStick()
    {
        Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
        if (stick.magnitude < 0.1f) stick = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);
        return stick;
    }

    private void StartStroke()
    {
        _armed = false;
        _strokeTime = 0f;
        _strokeDir = ComputeStrokeDirection();
    }

    private Vector3 ComputeStrokeDirection()
    {
        Vector3 dir = transform.forward;
        dir.y = 0f;
        dir.Normalize();

        LightSource source = LightSource.GetStrongestSource(transform.position, out float influence);
        if (source != null)
        {
            Vector3 toLight = source.transform.position - transform.position;
            toLight.y = 0f;
            if (toLight.magnitude > minLightDistance)
            {
                float bias = Mathf.Clamp01(influence * lightSteer);
                float angle = Vector3.SignedAngle(dir, toLight.normalized, Vector3.up);
                dir = Quaternion.Euler(0f, angle * bias, 0f) * dir;
            }
        }
        return dir;
    }

    private void CheckZones()
    {
        _speedMultiplier = 1f;
        Vector3 center = transform.position + Vector3.up * 0.05f;
        int count = Physics.OverlapSphereNonAlloc(center, zoneProbeRadius, Buffer, ~0,
                                                  QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            if (!Buffer[i].TryGetComponent(out TurtleZone zone)) continue;

            if (zone.Type == TurtleZoneType.Goal)
            {
                ReachGoal();
                return;
            }
            _speedMultiplier = Mathf.Min(_speedMultiplier, zone.SpeedMultiplier);
        }
    }

    private void ReachGoal()
    {
        enabled = false;
        if (GameManager.Instance != null) GameManager.Instance.SetState(GameState.End);
    }

    // Los objetos ligeros se empujan un poco; los pesados simplemente bloquean.
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody rb = hit.collider.attachedRigidbody;
        if (rb == null || rb.isKinematic || rb.mass > maxPushMass) return;
        if (hit.moveDirection.y < -0.3f) return;

        Vector3 dir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
        if (dir.sqrMagnitude < 0.0001f) return;

        rb.AddForce(dir.normalized * (CurrentSpeed * pushStrength * pushGain), ForceMode.Acceleration);
    }
}
