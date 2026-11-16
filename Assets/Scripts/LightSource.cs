using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marca una luz como "atractiva" para la tortuga (linterna, luna, luces del hotel...).
/// La tortuga (día 2) consulta GetInfluence / GetStrongestSource con su posición.
/// Si es un foco (Spot), solo influye si la tortuga está dentro del cono:
/// una linterna tirada que apunta a otro lado no afecta a la tortuga.
/// </summary>
[RequireComponent(typeof(Light))]
public class LightSource : MonoBehaviour
{
    public static readonly List<LightSource> All = new List<LightSource>();

    [SerializeField, Range(0f, 2f)] private float strength = 1f;

    private Light _light;

    public bool IsOn => _light != null && _light.enabled && isActiveAndEnabled;

    private void Awake() => _light = GetComponent<Light>();
    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    /// <summary>0 = no influye. Valores mayores = más atracción.</summary>
    public float GetInfluence(Vector3 targetPosition)
    {
        if (!IsOn) return 0f;

        Vector3 toTarget = targetPosition - transform.position;
        float distance = toTarget.magnitude;
        if (distance > _light.range) return 0f;

        float falloff = 1f - distance / _light.range;
        float cone = 1f;

        if (_light.type == LightType.Spot)
        {
            float halfAngle = _light.spotAngle * 0.5f;
            float angle = Vector3.Angle(transform.forward, toTarget);
            if (angle > halfAngle) return 0f;                 // fuera del cono
            cone = Mathf.Clamp01((1f - angle / halfAngle) * 2f); // borde suave
        }

        return strength * falloff * cone;
    }

    /// <summary>Devuelve la fuente con más influencia sobre una posición (o null).</summary>
    public static LightSource GetStrongestSource(Vector3 position, out float influence)
    {
        LightSource best = null;
        influence = 0f;
        foreach (LightSource source in All)
        {
            float value = source.GetInfluence(position);
            if (value > influence)
            {
                influence = value;
                best = source;
            }
        }
        return best;
    }
}
