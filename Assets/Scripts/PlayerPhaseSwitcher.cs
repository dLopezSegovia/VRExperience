using System.Collections.Generic;
using Oculus.Interaction.HandGrab;
using UnityEngine;

/// <summary>
/// Cambia al jugador entre humano (VS1) y tortuga (VS2) SIN modificar el player:
///  - Humano -> Tortuga: desactiva (temporalmente) la locomoción, los colliders y el agarre
///    humanos, escala el rig a 0.05, lo cuelga de TurtleBody y lo coloca en el nido.
///  - Tortuga -> Humano (solo para probar): lo deja todo como estaba.
/// Va en el mismo objeto que el GameManager (o en uno nuevo).
/// </summary>
public class PlayerPhaseSwitcher : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private TurtleController turtle;
    [SerializeField] private Transform turtleSpawn;
    [Tooltip("Vacío = se detecta solo (la raíz del OVRCameraRig). Mira el log al empezar.")]
    [SerializeField] private Transform playerRoot;
    [Tooltip("Vacío = el CenterEyeAnchor del OVRCameraRig.")]
    [SerializeField] private Transform head;

    [Header("Fase tortuga")]
    [SerializeField] private float turtleScale = 0.05f; // 1.65 m -> ~8 cm de ojos
    [Tooltip("Punto entre los ojos del modelo de la tortuga (hijo de TurtleBody). La cámara se calibra aquí.")]
    [SerializeField] private Transform eyeAnchor;
    [Tooltip("Si la cabeza real se aleja de los ojos más que esto (metros de mundo) durante un rato, se recalibra con un fundido corto.")]
    [SerializeField] private float recalibrateDistance = 0.035f;

    [Header("Se desactiva en la fase tortuga")]
    [Tooltip("Cualquier componente del player cuyo nombre contenga alguno de estos textos.")]
    [SerializeField] private string[] humanOnlyTypeKeywords = { "Locomot", "Teleport", "SnapTurn" };
    [Tooltip("Componentes concretos, si el filtro por nombre no encuentra el tuyo.")]
    [SerializeField] private MonoBehaviour[] humanOnlyBehaviours;
    [Tooltip("Objetos enteros: por ejemplo SeaBarrier (el muro del mar). NO pongas los muros laterales.")]
    [SerializeField] private GameObject[] humanOnlyObjects;
    [SerializeField] private bool disableHandGrabbing = true;

    private Transform _origParent;
    private Vector3 _origPos;
    private Quaternion _origRot;
    private Vector3 _origScale;
    private bool _inTurtle;

    private readonly List<MonoBehaviour> _disabledBehaviours = new List<MonoBehaviour>();
    private readonly List<Collider> _disabledColliders = new List<Collider>();
    private readonly List<GameObject> _disabledObjects = new List<GameObject>();

    private void Awake()
    {
        if (playerRoot == null || head == null)
        {
            OVRCameraRig rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig != null)
            {
                if (playerRoot == null) playerRoot = rig.transform.root;
                if (head == null) head = rig.centerEyeAnchor;
            }
        }
        if (head == null && Camera.main != null) head = Camera.main.transform;
    }

    private void Start()
    {
        if (playerRoot == null || head == null || turtle == null || turtleSpawn == null)
        {
            Debug.LogError("[PlayerPhaseSwitcher] Faltan referencias (turtle, turtleSpawn, playerRoot o head).");
            enabled = false;
            return;
        }

        Debug.Log($"[PlayerPhaseSwitcher] playerRoot = '{playerRoot.name}', head = '{head.name}'");

        _origParent = playerRoot.parent;
        _origPos = playerRoot.localPosition;
        _origRot = playerRoot.localRotation;
        _origScale = playerRoot.localScale;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged += OnStateChanged;
            if (GameManager.Instance.Current == GameState.Hatchling) EnterTurtle();
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= OnStateChanged;
    }

    private void OnStateChanged(GameState previous, GameState next)
    {
        if (next == GameState.Hatchling) EnterTurtle();
        else if (next == GameState.Human) ExitTurtle();
    }

    // Atajos para probar sin recorrer todo el flujo: clic derecho en el componente (en Play).
    [ContextMenu("Probar fase tortuga")]
    private void DebugTurtle() => GameManager.Instance.SetState(GameState.Hatchling);

    [ContextMenu("Volver a humano")]
    private void DebugHuman() => GameManager.Instance.SetState(GameState.Human);

    private void EnterTurtle()
    {
        if (_inTurtle) return;
        _inTurtle = true;

        DisableHumanStuff();

        // 1) Colocar el cuerpo de la tortuga en el nido, mirando al mar
        CharacterController cc = turtle.GetComponent<CharacterController>();
        cc.enabled = false;
        turtle.transform.SetPositionAndRotation(
            turtleSpawn.position, Quaternion.Euler(0f, turtleSpawn.eulerAngles.y, 0f));
        cc.enabled = true;

        // 2) Colgar el rig del cuerpo y reducirlo
        playerRoot.SetParent(turtle.transform, false);
        playerRoot.localPosition = Vector3.zero;
        playerRoot.localRotation = Quaternion.identity;
        playerRoot.localScale = Vector3.one * turtleScale;

        // 3) Que la cabeza quede sobre el cuerpo y mirando en su dirección
        float headYaw = Yaw(head.forward);
        float bodyYaw = Yaw(turtle.transform.forward);
        playerRoot.RotateAround(head.position, Vector3.up, Mathf.DeltaAngle(headYaw, bodyYaw));

        AlignHeadToEyes();
        _driftTime = 0f;

        turtle.Begin();
        TurtleEntered?.Invoke();
    }

    float _driftTime;
    bool _recalibrating;

    /// <summary>
    /// Calibración: desplaza el rig (nunca la cámara) para que la posición ACTUAL de la cabeza coincida con los ojos
    /// del modelo. El seguimiento del visor sigue siendo 1:1 (a escala 1:20): mirar, inclinarse o agacharse se
    /// respeta; solo se corrige el desfase de partida (altura real del jugador, sentado o de pie, centro del guardián).
    /// </summary>
    public void AlignHeadToEyes()
    {
        if (playerRoot == null || head == null) return;
        if (eyeAnchor != null)
        {
            playerRoot.position += eyeAnchor.position - head.position;
            return;
        }
        Vector3 offset = turtle.transform.position - head.position;
        offset.y = 0f;
        playerRoot.position += offset;
    }

    public Transform EyeAnchor => eyeAnchor;
    public Transform Head => head;

    private void OnEnable()
    {
        if (OVRManager.display != null) OVRManager.display.RecenteredPose += OnRecentered;
    }

    private void OnDisable()
    {
        if (OVRManager.display != null) OVRManager.display.RecenteredPose -= OnRecentered;
    }

    private void OnRecentered()
    {
        if (_inTurtle) AlignHeadToEyes();
    }

    private void LateUpdate()
    {
        if (!_inTurtle || eyeAnchor == null || head == null || _recalibrating) return;
        // the player walked away in the real room (or stood up / sat down): re-seat the eyes after a moment
        Vector3 err = eyeAnchor.position - head.position;
        bool far = new Vector2(err.x, err.z).magnitude > recalibrateDistance || Mathf.Abs(err.y) > recalibrateDistance;
        _driftTime = far ? _driftTime + Time.deltaTime : 0f;
        if (_driftTime > 0.8f)
        {
            Debug.Log($"[PlayerPhaseSwitcher] head drifted {err.magnitude * 100f:F1} cm from the eyes: recalibrating");
            StartCoroutine(Recalibrate());
        }
    }

    private System.Collections.IEnumerator Recalibrate()
    {
        _recalibrating = true;
        var fader = SameShore.ScreenFader.Instance;
        if (fader != null) yield return fader.FadeTo(1f, 0.2f);
        AlignHeadToEyes();
        _driftTime = 0f;
        if (fader != null) yield return fader.FadeTo(0f, 0.3f);
        _recalibrating = false;
    }

    private void ExitTurtle()
    {
        if (!_inTurtle) return;
        _inTurtle = false;

        turtle.enabled = false;

        playerRoot.SetParent(_origParent, false);
        playerRoot.localPosition = _origPos;
        playerRoot.localRotation = _origRot;
        playerRoot.localScale = _origScale;

        RestoreHumanStuff();
    }

    /// <summary>Se lanza al terminar de convertir al jugador en tortuga.</summary>
    public event System.Action TurtleEntered;

    public bool InTurtle => _inTurtle;
    public Transform TurtleSpawn => turtleSpawn;

    /// <summary>Vuelve a poner la tortuga en el nido (reintentar) sin tocar el resto de la playa.</summary>
    public void ResetTurtle()
    {
        if (!_inTurtle) { EnterTurtle(); return; }
        turtle.TeleportTo(turtleSpawn.position, turtleSpawn.eulerAngles.y);
        turtle.Begin();
    }

    private static float Yaw(Vector3 forward)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) return 0f;
        return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
    }

    private void DisableHumanStuff()
    {
        List<string> names = new List<string>();

        // Componentes del player por nombre (locomoción, teletransporte, giro...)
        foreach (MonoBehaviour mb in playerRoot.GetComponentsInChildren<MonoBehaviour>(false))
        {
            if (mb == null || !mb.enabled) continue;
            string typeName = mb.GetType().Name;
            foreach (string keyword in humanOnlyTypeKeywords)
            {
                if (string.IsNullOrEmpty(keyword) || !typeName.Contains(keyword)) continue;
                DisableBehaviour(mb, names);
                break;
            }
        }

        // Componentes indicados a mano
        foreach (MonoBehaviour mb in humanOnlyBehaviours)
            if (mb != null && mb.enabled) DisableBehaviour(mb, names);

        // Colliders sólidos del player (incluye un posible CharacterController humano)
        foreach (Collider col in playerRoot.GetComponentsInChildren<Collider>(false))
        {
            if (col == null || !col.enabled || col.isTrigger) continue;
            col.enabled = false;
            _disabledColliders.Add(col);
            names.Add($"{col.GetType().Name} ({col.name})");
        }

        // Objetos enteros (SeaBarrier...)
        foreach (GameObject go in humanOnlyObjects)
        {
            if (go == null || !go.activeSelf) continue;
            go.SetActive(false);
            _disabledObjects.Add(go);
            names.Add($"objeto '{go.name}'");
        }

        // Sin agarre en la fase tortuga
        if (disableHandGrabbing)
        {
            foreach (HandGrabInteractable grab in FindObjectsByType<HandGrabInteractable>(FindObjectsSortMode.None))
            {
                if (!grab.enabled) continue;
                DisableBehaviour(grab, null);
            }
        }

        Debug.Log("[PlayerPhaseSwitcher] Desactivado en fase tortuga: " +
                  (names.Count > 0 ? string.Join(", ", names) : "(nada)"));
    }

    private void DisableBehaviour(MonoBehaviour mb, List<string> names)
    {
        mb.enabled = false;
        _disabledBehaviours.Add(mb);
        names?.Add($"{mb.GetType().Name} ({mb.name})");
    }

    private void RestoreHumanStuff()
    {
        foreach (MonoBehaviour mb in _disabledBehaviours) if (mb != null) mb.enabled = true;
        foreach (Collider col in _disabledColliders) if (col != null) col.enabled = true;
        foreach (GameObject go in _disabledObjects) if (go != null) go.SetActive(true);

        _disabledBehaviours.Clear();
        _disabledColliders.Clear();
        _disabledObjects.Clear();
    }
}
