using UnityEngine;
using DG.Tweening;
using System;
using FoodMatch.Core;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public GameState CurrentState { get; private set; } = GameState.None; // ← sentinel

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
        ChangeState(GameState.Init); // CurrentState = None → không bị block
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

    public void ChangeState(GameState newState)
    {
        if (CurrentState == newState) return;

        CurrentState = newState;
        Debug.Log($"[GameManager] State → {newState}");
        OnGameStateChanged?.Invoke(newState);
    }
}