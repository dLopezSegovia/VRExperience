using UnityEngine;

public enum TurtleZoneType
{
    Goal,   // el mar: al entrar termina la partida
    Slow,   // huellas, arena removida...: las brazadas rinden menos
    Trap,   // hoyo: casi no se avanza y cuesta trepar (se libera tras unos segundos de esfuerzo)
    Tangle  // bolsa de plástico: enreda a la tortuga
}

/// <summary>
/// Zona (trigger) que solo afecta a la tortuga. La tortuga la detecta sola;
/// no hace falta ningún evento ni componente extra.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TurtleZone : MonoBehaviour
{
    [SerializeField] private TurtleZoneType type = TurtleZoneType.Goal;
    [SerializeField, Range(0.02f, 1f)] private float speedMultiplier = 0.15f;
    [Tooltip("Mensaje que se muestra la primera vez que la tortuga entra (opcional).")]
    [SerializeField, TextArea] private string firstEnterMessage;

    public TurtleZoneType Type { get => type; set => type = value; }
    public float SpeedMultiplier { get => speedMultiplier; set => speedMultiplier = value; }
    public string FirstEnterMessage { get => firstEnterMessage; set => firstEnterMessage = value; }

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }
}
