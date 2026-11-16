using System;
using UnityEngine;

public enum GameState { Intro, Human, Hatching, Transition, Hatchling, End }

/// <summary>
/// Máquina de estados global. Los demás scripts se suscriben a OnStateChanged
/// (hazlo en Start, no en OnEnable, para evitar problemas de orden de ejecución).
/// </summary>
[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private GameState startState = GameState.Human;

    public GameState Current { get; private set; }

    /// <summary>(estado anterior, estado nuevo)</summary>
    public event Action<GameState, GameState> OnStateChanged;

    private bool _initialized;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start() => SetState(startState);

    public void SetState(GameState next)
    {
        if (_initialized && next == Current) return;

        GameState previous = Current;
        Current = next;
        _initialized = true;

        Debug.Log($"[GameManager] {previous} -> {next}");
        OnStateChanged?.Invoke(previous, next);
    }

    // Para probar sin tener todo el flujo hecho: clic derecho en el componente (en Play).
    [ContextMenu("Siguiente estado")]
    public void NextState()
    {
        int next = Mathf.Min((int)Current + 1, (int)GameState.End);
        SetState((GameState)next);
    }
}
