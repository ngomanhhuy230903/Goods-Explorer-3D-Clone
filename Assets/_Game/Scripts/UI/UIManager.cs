using UnityEngine;
using DG.Tweening;
using System;
using FoodMatch.Level;
using FoodMatch.Managers;
using FoodMatch.Core;

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
    [SerializeField] private GameObject popupNoHP;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        SetPanel(panelLoading, false);
        SetPanel(panelLogo, false);
        SetPanel(panelHome, false);
        SetPanel(panelGame, false);
    }

    private void OnEnable()
    {
        GameManager.OnGameStateChanged += HandleStateChange;
        EventBus.OnHPEmpty += HandleHPEmpty;
    }

    private void OnDisable()
    {
        GameManager.OnGameStateChanged -= HandleStateChange;
        EventBus.OnHPEmpty -= HandleHPEmpty;
    }

    // ─── Shop triggers ────────────────────────────────────────────────────────

    private void HandleInsufficientCoins(long shortfall)
    {
        ShopManager.Instance.OpenShop();
    }

    private void HandleHPEmpty()
    {
        // Nếu đang có popupNoHP riêng thì dùng nó, ngược lại mở shop luôn
        if (popupNoHP != null)
            ShowNoHPPopup();
        else
            ShopManager.Instance.OpenShop();
    }
    // ─── State handling ───────────────────────────────────────────────────────

    private void HandleStateChange(GameState state)
    {
        switch (state)
        {
            case GameState.Init:
                ShowBootSequence(() => GameManager.Instance.ChangeState(GameState.Menu));
                break;

            case GameState.Menu:
                panelGame.DOKill();
                panelGame.DOFade(0f, 0.3f).OnComplete(() => SetPanel(panelGame, false));
                ShowHome();
                break;

            case GameState.LoadLevel:
            case GameState.Play:
                ShowGame();
                break;

            case GameState.Win:
            case GameState.Lose:
                break;
        }
    }

    // ─── Panel transitions ────────────────────────────────────────────────────

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
            btnPlay.DOKill();
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
        if (cg == null) return;
        cg.alpha = active ? 1f : 0f;
        cg.interactable = active;
        cg.blocksRaycasts = active;
    }

    // ─── Button callbacks ─────────────────────────────────────────────────────

    public void OnClickPlayButton()
    {
        if (HPManager.Instance != null && !HPManager.Instance.HasHPToPlay())
        {
            HandleHPEmpty();
            return;
        }

        if (btnPlay != null) btnPlay.DOKill();
        int levelToLoad = SaveManager.CurrentLevel;
        LevelManager.Instance.RequestLoadLevel(levelToLoad);
    }

    /// <summary>Nút + coin / icon shop trên HUD game gọi thẳng cái này.</summary>
    public void OnClickOpenShop()
    {
        ShopManager.Instance.OpenShop();
    }

    private void ShowNoHPPopup()
    {
        if (popupNoHP != null)
        {
            popupNoHP.SetActive(true);
            return;
        }
        Debug.Log("[UIManager] Hết HP, không thể chơi!");
    }
}