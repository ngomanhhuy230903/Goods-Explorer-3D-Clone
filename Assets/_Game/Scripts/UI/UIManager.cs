using UnityEngine;
using DG.Tweening;
using System;
using FoodMatch.Level;
using FoodMatch.Managers;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Panels")]
    [SerializeField] private CanvasGroup panelLoading;
    [SerializeField] private CanvasGroup panelLogo;
    [SerializeField] private CanvasGroup panelHome;
    [SerializeField] private CanvasGroup panelGame;

    [Header("Home UI")]
    [SerializeField] private RectTransform btnPlay;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        SetPanel(panelLoading, false);
        SetPanel(panelLogo, false);
        SetPanel(panelHome, false);
        SetPanel(panelGame, false);
    }

    // Lắng nghe Event từ GameManager
    private void OnEnable() => GameManager.OnGameStateChanged += HandleStateChange;
    private void OnDisable() => GameManager.OnGameStateChanged -= HandleStateChange;

    private void HandleStateChange(GameState state)
    {
        switch (state)
        {
            case GameState.Init:
                // Tự động chạy chuỗi Boot, xong thì báo GameManager chuyển Menu
                ShowBootSequence(() => GameManager.Instance.ChangeState(GameState.Menu));
                break;
            case GameState.Menu:
                ShowHome();
                break;
            case GameState.LoadLevel:
            case GameState.Play:
                ShowGame();
                break;
        }
    }

    private void ShowBootSequence(Action onComplete)
    {
        SetPanel(panelLoading, true);
        panelLoading.alpha = 0;
        panelLoading.DOFade(1f, 0.5f).OnComplete(() =>
        {
            DOVirtual.DelayedCall(1.5f, () =>
            {
                panelLoading.DOFade(0f, 0.4f).OnComplete(() => SetPanel(panelLoading, false));
                ShowLogo(onComplete);
            });
        });
    }

    private void ShowLogo(Action onComplete)
    {
        SetPanel(panelLogo, true);
        panelLogo.alpha = 0;
        panelLogo.transform.localScale = Vector3.one * 0.8f;

        Sequence seq = DOTween.Sequence();
        seq.Append(panelLogo.DOFade(1f, 0.4f));
        seq.Join(panelLogo.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack));
        seq.AppendInterval(1f);
        seq.Append(panelLogo.DOFade(0f, 0.4f));
        seq.OnComplete(() =>
        {
            SetPanel(panelLogo, false);
            onComplete?.Invoke();
        });
    }

    private void ShowHome()
    {
        SetPanel(panelHome, true);
        panelHome.alpha = 0;
        panelHome.DOFade(1f, 0.5f);

        if (btnPlay != null)
        {
            btnPlay.localScale = Vector3.one * 0.9f;
            btnPlay.DOScale(1f, 0.6f).SetEase(Ease.OutBack).OnComplete(() =>
            {
                btnPlay.DOScale(1.05f, 0.7f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
            });
        }
    }

    private void ShowGame()
    {
        panelHome.DOFade(0f, 0.3f).OnComplete(() => SetPanel(panelHome, false));
        SetPanel(panelGame, true);
        panelGame.alpha = 0;
        panelGame.DOFade(1f, 0.4f);
    }

    private void SetPanel(CanvasGroup cg, bool active)
    {
        cg.alpha = active ? 1f : 0f;
        cg.interactable = active;
        cg.blocksRaycasts = active;
    }
    public void OnClickPlayButton()
    {
        // 1. Dừng animation nảy của nút Play để tránh lỗi khi chuyển màn
        if (btnPlay != null)
        {
            btnPlay.DOKill();
        }

        // 2. Báo cho GameManager biết để đổi State sang Play (hoặc LoadLevel)
        int levelToLoad = SaveManager.CurrentLevel;
        LevelManager.Instance.RequestLoadLevel(levelToLoad);
    }
}