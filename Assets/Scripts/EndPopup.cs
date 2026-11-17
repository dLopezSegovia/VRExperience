using UnityEngine;

/// <summary>
/// Muestra el cartel "Great Job" delante de la cabeza cuando el estado pasa a End.
/// El cartel es un Canvas en World Space, desactivado y fuera de la jerarquía del player.
/// Se adapta a la escala del rig (la tortuga mide 0.05).
/// </summary>
public class EndPopup : MonoBehaviour
{
    [SerializeField] private Transform popup;            // raíz del Canvas (World Space)
    [Tooltip("Vacío = Camera.main")]
    [SerializeField] private Transform head;
    [SerializeField] private float distance = 1.5f;      // metros "del jugador" (se multiplica por su escala)

    private Vector3 _baseScale;

    private void Awake()
    {
        if (popup == null)
        {
            Debug.LogError("[EndPopup] Falta asignar el cartel.");
            enabled = false;
            return;
        }
        _baseScale = popup.localScale;
        popup.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged += OnStateChanged;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= OnStateChanged;
    }

    private void OnStateChanged(GameState previous, GameState next)
    {
        if (next == GameState.End) Show();
        else popup.gameObject.SetActive(false);
    }

    private void Show()
    {
        if (head == null) return;

        float s = head.lossyScale.x; // 1 para el humano, 0.05 para la tortuga
        popup.position = head.position + head.forward * distance * s;
        popup.rotation = Quaternion.LookRotation(popup.position - head.position);
        popup.localScale = _baseScale * s;
        popup.gameObject.SetActive(true);
    }
}
