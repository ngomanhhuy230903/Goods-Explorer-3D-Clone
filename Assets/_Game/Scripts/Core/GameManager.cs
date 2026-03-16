using UnityEngine;
using DG.Tweening;
using System;
using FoodMatch.Core;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public GameState CurrentState { get; private set; } = GameState.None;
    public static event Action<GameState> OnGameStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        DOTween.Init(true, true, LogBehaviour.Verbose).SetCapacity(200, 10);
    }

    private void Start()
    {
        ChangeState(GameState.Init);
    }

    private void OnEnable()
    {
        EventBus.OnAllOrdersCompleted += HandleWin;
        EventBus.OnBackupTrayFull += HandleLose;
    }

    private void OnDisable()
    {
        EventBus.OnAllOrdersCompleted -= HandleWin;
        EventBus.OnBackupTrayFull -= HandleLose;
    }

    private void HandleWin() => ChangeState(GameState.Win);
    private void HandleLose() => ChangeState(GameState.Lose);

    /// <summary>
    /// Đổi state bình thường — có guard chống gọi trùng state.
    /// </summary>
    public void ChangeState(GameState newState)
    {
        if (CurrentState == newState) return;
        CurrentState = newState;
        Debug.Log($"[GameManager] State → {newState}");
        OnGameStateChanged?.Invoke(newState);
    }

    /// <summary>
    /// Force đổi state kể cả khi CurrentState == newState.
    /// Dùng khi cần đảm bảo state được set và event được fire,
    /// ví dụ: Settings mở lại sau khi state kẹt ở Pause do
    /// ChangeState(Pause) bị guard block.
    /// </summary>
    public void ForceChangeState(GameState newState)
    {
        CurrentState = newState;
        Debug.Log($"[GameManager] ForceState → {newState}");
        OnGameStateChanged?.Invoke(newState);
    }
}