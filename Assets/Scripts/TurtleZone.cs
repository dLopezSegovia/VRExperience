using UnityEngine;

public enum TurtleZoneType
{
    Goal,   // el mar: al entrar termina la partida
    Slow    // hoyo, barro...: las brazadas rinden mucho menos (la tortuga se queda atrapada)
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

    public TurtleZoneType Type => type;
    public float SpeedMultiplier => speedMultiplier;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }
}
