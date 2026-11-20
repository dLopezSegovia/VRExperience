using System;
using SameShore;
using UnityEngine;

/// <summary>
/// Controlador de la cría de tortuga (VS2). Va en el objeto "TurtleBody", que lleva un
/// CharacterController. PlayerPhaseSwitcher cuelga el rig del jugador de este objeto,
/// así que moviendo/rotando TurtleBody se mueve el jugador (el visor sigue siendo 1:1).
///
/// - Stick (eje Y): brazadas. Mantener hacia delante = brazadas rítmicas (pulsos);
///   hacia atrás = reptar marcha atrás, más despacio. No hay desplazamiento lateral.
/// - Stick (eje X): gira el cuerpo despacio (25°/s, diseño VS2).
/// - A / X (o gatillo): impulso súbito en una dirección (con recarga).
/// - Luz: cada brazada se desvía hacia la atracción neta de las luces (mar, linterna, hotel).
/// - Objetos ligeros se empujan con las aletas; los pesados bloquean.
/// - Zonas: Slow/Trap/Tangle frenan (el hoyo atrapa un rato), Goal termina la partida.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class TurtleController : MonoBehaviour
{
    [Header("Brazada")]
    [SerializeField] private float strokeDistance = 0.13f;     // metros de mundo por brazada
    [SerializeField] private float strokeDuration = 0.6f;      // segundos que dura la ráfaga
    [SerializeField] private float strokeGap = 0.15f;          // pausa entre brazadas al mantener el stick
    [SerializeField] private float strokeThreshold = 0.55f;    // stick Y para dar la brazada
    [SerializeField] private float rearmThreshold = 0.3f;      // (compatibilidad) umbral de reposo
    [SerializeField, Range(0f, 1f)] private float backwardFactor = 0.4f;

    [Header("Impulso súbito")]
    [SerializeField] private float burstDistance = 0.32f;
    [SerializeField] private float burstDuration = 0.55f;
    [SerializeField] private float burstCooldown = 2.5f;

    [Header("Giro")]
    [SerializeField] private float turnSpeed = 25f;            // grados/s (diseño VS2)
    [SerializeField] private float stickDeadZone = 0.15f;

    [Header("Atracción de la luz")]
    [SerializeField, Range(0f, 1f)] private float lightSteer = 0.55f; // cuánto se desvía la brazada hacia la luz
    [SerializeField] private float maxSteerAngle = 70f;
    [Tooltip("Giro automático del cuerpo hacia la luz (°/s a atracción 1), solo mientras repta. 0 = desactivado.")]
    [SerializeField] private float bodyTurnTowardLight = 8f;
    [SerializeField] private float minLightDistance = 0.1f;

    [Header("Empuje de objetos")]
    [SerializeField] private float maxPushMass = 1f;           // más pesado que esto = bloquea
    [SerializeField, Range(0f, 1f)] private float pushStrength = 0.3f; // diseño: 1:0.3
    [SerializeField] private float pushGain = 25f;

    [Header("Trampas y bloqueos")]
    [Tooltip("Segundos de esfuerzo dentro del hoyo antes de poder trepar.")]
    [SerializeField] private float trapEscapeTime = 5f;
    [SerializeField] private float stuckMessageInterval = 15f;

    [Header("Física")]
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float zoneProbeRadius = 0.05f;

    [Header("Audio")]
    [SerializeField] private AudioSource strokeSource;
    [SerializeField] private AudioSource dragSource;

    public float CurrentSpeed { get; private set; }
    /// <summary>0..1 mientras dura una brazada, -1 en reposo (lo usa la animación de aletas).</summary>
    public float StrokePhase => _strokeTime < _strokeLen ? _strokeTime / _strokeLen : -1f;
    public bool StrokeIsBurst { get; private set; }
    public bool StrokeIsBackward { get; private set; }
    public Vector3 LightPull { get; private set; }
    public float ArtificialShare { get; private set; }
    public float BurstReady => Mathf.Clamp01((Time.time - _lastBurst) / burstCooldown);
    public bool IsStunned => Time.time < _stunnedUntil;

    // estadísticas para la pantalla final
    public float ElapsedTime { get; private set; }
    public int Strokes { get; private set; }
    public int TimesStuck { get; private set; }
    public float SecondsPulledByArtificialLight { get; private set; }

    public event Action ReachedGoal;
    public event Action<string> Message;
    /// <summary>First frame inside a zone (hole, plastic bag, footprints...).</summary>
    public event Action<TurtleZone> ZoneEntered;
    /// <summary>Ran into something solid (litter, chair, rock...). Throttled.</summary>
    public event Action<Collider> Bumped;

    private static readonly Collider[] Buffer = new Collider[16];

    private CharacterController _cc;
    private float _strokeTime = 10f, _strokeLen = 1f, _strokeDist;
    private float _nextStrokeAllowed;
    private Vector3 _strokeDir;
    private float _vertical;
    private float _speedMultiplier = 1f;
    private float _lastBurst = -99f;
    private float _stunnedUntil;
    private float _trapTime;
    private bool _inTrap;
    private float _defaultSlope, _defaultStep;
    private Vector3 _strokeStartPos;
    private int _lowProgressStrokes;
    private float _lastStuckMessage = -99f;
    private readonly System.Collections.Generic.HashSet<TurtleZone> _announced = new System.Collections.Generic.HashSet<TurtleZone>();
    private readonly System.Collections.Generic.HashSet<TurtleZone> _inside = new System.Collections.Generic.HashSet<TurtleZone>();
    private readonly System.Collections.Generic.List<TurtleZone> _insideNow = new System.Collections.Generic.List<TurtleZone>();
    private float _lastBump;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _defaultSlope = _cc.slopeLimit;
        _defaultStep = _cc.stepOffset;
        enabled = false; // lo activa PlayerPhaseSwitcher con Begin()
    }

    public void Begin()
    {
        _strokeTime = _strokeLen = 1f;
        _vertical = 0f;
        CurrentSpeed = 0f;
        ElapsedTime = 0f;
        Strokes = TimesStuck = 0;
        SecondsPulledByArtificialLight = 0f;
        _stunnedUntil = 0f;
        _trapTime = 0f;
        _announced.Clear();
        _inside.Clear();
        enabled = true;
    }

    /// <summary>Continue after a pause/respawn without resetting the run statistics.</summary>
    public void Resume()
    {
        _strokeTime = _strokeLen = 1f;
        _vertical = 0f;
        _stunnedUntil = 0f;
        enabled = true;
    }

    public void Stun(float seconds)
    {
        _stunnedUntil = Mathf.Max(_stunnedUntil, Time.time + seconds);
        _strokeTime = _strokeLen; // corta la brazada en curso
        XRInput.HapticBoth(0.9f, 0.35f, 0.8f);
    }

    private void OnDisable()
    {
        CurrentSpeed = 0f;
        if (dragSource != null) dragSource.volume = 0f;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        ElapsedTime += dt;
        Vector2 stick = XRInput.AnyStick;

        CheckZones();
        if (!enabled) return; // la zona Goal nos puede haber apagado

        UpdateLightPull(dt);
        bool stunned = IsStunned;

        // Giro lento del cuerpo
        if (!stunned && Mathf.Abs(stick.x) > stickDeadZone)
            transform.Rotate(0f, Mathf.Sign(stick.x) * Mathf.InverseLerp(stickDeadZone, 1f, Mathf.Abs(stick.x)) * turnSpeed * dt, 0f);

        // Brazadas rítmicas mientras se mantiene el stick; impulso con A/X
        bool strokeDone = _strokeTime >= _strokeLen;
        if (!stunned && strokeDone && Time.time >= _nextStrokeAllowed)
        {
            if (XRInput.BurstDown && BurstReady >= 1f) StartStroke(true, false);
            else if (stick.y > strokeThreshold) StartStroke(false, false);
            else if (stick.y < -strokeThreshold) StartStroke(false, true);
        }

        Vector3 motion = Vector3.zero;
        CurrentSpeed = 0f;
        if (_strokeTime < _strokeLen)
        {
            float t = _strokeTime / _strokeLen;
            // pulso suave (sin²): el área bajo la curva = distancia de la brazada
            float profile = 2f * Mathf.Sin(Mathf.PI * t) * Mathf.Sin(Mathf.PI * t);
            CurrentSpeed = _strokeDist / _strokeLen * profile * _speedMultiplier;
            motion = _strokeDir * CurrentSpeed;
            _strokeTime += dt;
            if (_strokeTime >= _strokeLen) EndStroke();

            // la luz también gira poco a poco el cuerpo mientras repta (desactivable)
            if (bodyTurnTowardLight > 0f && LightPull.sqrMagnitude > 0.0001f && !StrokeIsBackward)
            {
                float angle = Vector3.SignedAngle(Flat(transform.forward), LightPull.normalized, Vector3.up);
                float maxStep = bodyTurnTowardLight * Mathf.Clamp01(LightPull.magnitude) * dt;
                transform.Rotate(0f, Mathf.Clamp(angle, -maxStep, maxStep), 0f);
            }
        }

        if (_cc.isGrounded && _vertical < 0f) _vertical = -0.5f;
        _vertical += gravity * dt;
        motion.y = _vertical;
        _cc.Move(motion * dt);

        if (dragSource != null)
            dragSource.volume = Mathf.MoveTowards(dragSource.volume, Mathf.Clamp01(CurrentSpeed * 4f) * 0.7f, dt * 3f);
    }

    private void UpdateLightPull(float dt)
    {
        LightPull = LightSource.GetNetPull(transform.position, out float artificialShare);
        ArtificialShare = artificialShare;
        if (CurrentSpeed > 0.01f && artificialShare > 0.5f) SecondsPulledByArtificialLight += dt;
        if (PerceptionController.Instance != null)
        {
            Vector3 dir = LightPull.sqrMagnitude > 0.0001f ? LightPull.normalized : transform.forward;
            PerceptionController.Instance.PullDirection = (dir + Vector3.up * 0.15f).normalized;
            PerceptionController.Instance.PullStrength = Mathf.Clamp01(LightPull.magnitude * 0.6f);
        }
    }

    private void StartStroke(bool burst, bool backward)
    {
        StrokeIsBurst = burst;
        StrokeIsBackward = backward;
        _strokeTime = 0f;
        _strokeLen = burst ? burstDuration : strokeDuration;
        _strokeDist = burst ? burstDistance : strokeDistance * (backward ? backwardFactor : 1f);
        _strokeDir = ComputeStrokeDirection(backward);
        _strokeStartPos = transform.position;
        if (burst) _lastBurst = Time.time;
        if (_inTrap && _trapTime > trapEscapeTime)
        {
            // tras unos segundos de esfuerzo, la tortuga consigue trepar fuera del hoyo
            _cc.slopeLimit = 75f;
            _cc.stepOffset = Mathf.Max(_defaultStep, 0.06f);
        }
        Strokes++;
        if (strokeSource != null)
        {
            strokeSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f) * (burst ? 0.85f : 1f);
            strokeSource.PlayOneShot(SameShoreAssets.Pick(SameShoreAssets.I.flipperStrokes), burst ? 1f : 0.7f);
        }
        XRInput.HapticBoth(burst ? 0.5f : 0.18f, burst ? 0.12f : 0.06f);
    }

    private void EndStroke()
    {
        _nextStrokeAllowed = Time.time + strokeGap;
        float expected = _strokeDist * 0.5f;
        float moved = Flat(transform.position - _strokeStartPos).magnitude;
        if (moved < expected * 0.2f && !_inTrap) _lowProgressStrokes++;
        else _lowProgressStrokes = 0;

        if (_lowProgressStrokes >= 3)
        {
            _lowProgressStrokes = 0;
            TimesStuck++;
            if (Time.time - _lastStuckMessage > stuckMessageInterval)
            {
                _lastStuckMessage = Time.time;
                Message?.Invoke("Something blocks your way.\nTurn your body to go around it.");
            }
        }
    }

    private Vector3 ComputeStrokeDirection(bool backward)
    {
        Vector3 dir = Flat(transform.forward).normalized;
        if (backward) return -dir;

        if (LightPull.sqrMagnitude > minLightDistance * minLightDistance * 0.01f)
        {
            float bias = Mathf.Clamp01(LightPull.magnitude * lightSteer);
            float angle = Vector3.SignedAngle(dir, LightPull.normalized, Vector3.up);
            angle = Mathf.Clamp(angle * bias, -maxSteerAngle, maxSteerAngle);
            dir = Quaternion.Euler(0f, angle, 0f) * dir;
        }
        return dir;
    }

    private void CheckZones()
    {
        _speedMultiplier = 1f;
        bool inTrap = false;
        _insideNow.Clear();
        Vector3 center = transform.position + Vector3.up * 0.03f;
        int count = Physics.OverlapSphereNonAlloc(center, zoneProbeRadius, Buffer, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            if (!Buffer[i].TryGetComponent(out TurtleZone zone) || !zone.isActiveAndEnabled) continue;

            if (zone.Type == TurtleZoneType.Goal)
            {
                ReachGoal();
                return;
            }
            _insideNow.Add(zone);
            if (!_inside.Contains(zone)) ZoneEntered?.Invoke(zone);
            if (zone.Type == TurtleZoneType.Trap) inTrap = true;
            _speedMultiplier = Mathf.Min(_speedMultiplier, zone.SpeedMultiplier);

            if (!string.IsNullOrEmpty(zone.FirstEnterMessage) && _announced.Add(zone))
                Message?.Invoke(zone.FirstEnterMessage);
        }

        _inside.Clear();
        foreach (var z in _insideNow) _inside.Add(z);

        if (inTrap)
        {
            if (!_inTrap) TimesStuck++;
            if (_strokeTime < _strokeLen || Mathf.Abs(XRInput.AnyStick.y) > strokeThreshold) _trapTime += Time.deltaTime;
            if (_trapTime > trapEscapeTime) _speedMultiplier = Mathf.Max(_speedMultiplier, 0.8f);
        }
        else if (_inTrap)
        {
            _trapTime = 0f;
            _cc.slopeLimit = _defaultSlope;
            _cc.stepOffset = _defaultStep;
        }
        _inTrap = inTrap;
    }

    private void ReachGoal()
    {
        enabled = false;
        ReachedGoal?.Invoke();
        if (GameManager.Instance != null) GameManager.Instance.SetState(GameState.End);
    }

    /// <summary>Recoloca la tortuga (para reintentar). El rig va colgado, así que viaja con ella.</summary>
    public void TeleportTo(Vector3 position, float yaw)
    {
        _cc.enabled = false;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        _cc.enabled = true;
        _cc.slopeLimit = _defaultSlope;
        _cc.stepOffset = _defaultStep;
        _inTrap = false;
        _trapTime = 0f;
    }

    // Los objetos ligeros se empujan un poco con las aletas; los pesados simplemente bloquean.
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!(hit.collider is TerrainCollider) && Time.time - _lastBump > 0.5f && CurrentSpeed > 0.01f)
        {
            _lastBump = Time.time;
            Bumped?.Invoke(hit.collider);
        }
        Rigidbody rb = hit.collider.attachedRigidbody;
        if (rb == null || rb.isKinematic || rb.mass > maxPushMass) return;
        if (hit.moveDirection.y < -0.3f) return;

        Vector3 dir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
        if (dir.sqrMagnitude < 0.0001f) return;

        rb.AddForceAtPosition(dir.normalized * (CurrentSpeed * pushStrength * pushGain), hit.point, ForceMode.Acceleration);
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }
}
