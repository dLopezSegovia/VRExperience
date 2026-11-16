using UnityEngine;

/// <summary>
/// Si un objeto se cae del mapa o sale volando demasiado lejos, vuelve a su sitio inicial.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class RespawnIfOutOfBounds : MonoBehaviour
{
    [SerializeField] private float minY = -1f;
    [SerializeField] private float maxDistanceFromStart = 10f;

    private Rigidbody _rb;
    private Vector3 _startPosition;
    private Quaternion _startRotation;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _startPosition = transform.position;
        _startRotation = transform.rotation;
    }

    private void FixedUpdate()
    {
        bool fell = transform.position.y < minY;
        bool tooFar = (transform.position - _startPosition).sqrMagnitude
                      > maxDistanceFromStart * maxDistanceFromStart;
        if (!fell && !tooFar) return;

        // En Unity 6 "velocity" da un aviso de obsoleto (linearVelocity); funciona igual.
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.position = _startPosition;
        _rb.rotation = _startRotation;
    }
}
