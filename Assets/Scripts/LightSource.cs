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

    [SerializeField, Range(0f, 3f)] private float strength = 1f;

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

    /// <summary>¿Es luz artificial (linterna, hotel)? La del horizonte marino es natural.</summary>
    public bool IsArtificial => artificial;
    [SerializeField] private bool artificial = true;

    public float Strength { get => strength; set => strength = value; }

    /// <summary>
    /// Suma de todas las fuentes: dirección (horizontal) ponderada por influencia.
    /// La magnitud indica cuánto tira la luz; artificialShare = parte que viene de luz artificial.
    /// </summary>
    public static Vector3 GetNetPull(Vector3 position, out float artificialShare)
    {
        Vector3 net = Vector3.zero;
        float total = 0f, art = 0f;
        foreach (LightSource source in All)
        {
            float value = source.GetInfluence(position);
            if (value <= 0f) continue;
            Vector3 dir = source.transform.position - position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) continue;
            // a light hanging right overhead has no direction for the hatchling: its pull fades out underneath
            // (otherwise it would circle below the bulb forever instead of being able to crawl away)
            value *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 1.4f, dir.magnitude));
            if (value <= 0f) continue;
            net += dir.normalized * value;
            total += value;
            if (source.artificial) art += value;
        }
        artificialShare = total > 0f ? art / total : 0f;
        return net;
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
