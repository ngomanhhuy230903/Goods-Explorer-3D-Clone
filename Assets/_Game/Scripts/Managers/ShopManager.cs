using UnityEngine;
using DG.Tweening;
using FoodMatch.Core;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    [Header("Shop Panel")]
    [SerializeField] private GameObject panelShopRoot;
    [SerializeField] private RectTransform panelShopPanel;

    [Header("Animation")]
    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private float scaleDuration = 0.3f;
    [SerializeField] private Ease scaleEaseIn = Ease.OutBack;
    [SerializeField] private Ease scaleEaseOut = Ease.InBack;

    // Canvas riêng — sort order cao nhất, che cả 3D objects
    private Canvas _shopCanvas;
    private CanvasGroup _shopCanvasGroup;
    private bool _isOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        BuildShopCanvas();
    }

    private void OnEnable()
    {
        EventBus.OnOpenShop += HandleOpenViaEvent;
        EventBus.OnCloseShop += CloseShop;
    }

    private void OnDisable()
    {
        EventBus.OnOpenShop -= HandleOpenViaEvent;
        EventBus.OnCloseShop -= CloseShop;
    }

    // ─── Build Canvas (giống GameResultUI.BuildPopupCanvas) ──────────────────

    private void BuildShopCanvas()
    {
        var go = new GameObject("ShopCanvas");
        DontDestroyOnLoad(go);

        _shopCanvas = go.AddComponent<Canvas>();
        _shopCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _shopCanvas.sortingOrder = 1000;

        var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Reparent trước
        ReparentToShopCanvas(panelShopRoot);

        // CanvasGroup đặt trên panelShopRoot, không phải canvas
        // để Inspector của Panel_Shop phản ánh đúng
        if (panelShopRoot != null)
        {
            _shopCanvasGroup = panelShopRoot.GetComponent<CanvasGroup>();
            if (_shopCanvasGroup == null)
                _shopCanvasGroup = panelShopRoot.AddComponent<CanvasGroup>();

            _shopCanvasGroup.alpha = 0f;
            _shopCanvasGroup.interactable = false;
            _shopCanvasGroup.blocksRaycasts = false;

            panelShopRoot.SetActive(false);
        }

        Debug.Log("[ShopManager] ShopCanvas built, sortingOrder=1000");
    }

    private void ReparentToShopCanvas(GameObject target)
    {
        if (target == null || _shopCanvas == null) return;
        var rt = target.GetComponent<RectTransform>();
        if (rt == null) return;

        // Giữ nguyên layout
        var anchorMin = rt.anchorMin;
        var anchorMax = rt.anchorMax;
        var offsetMin = rt.offsetMin;
        var offsetMax = rt.offsetMax;
        var anchoredPos = rt.anchoredPosition;
        var sizeDelta = rt.sizeDelta;
        var localScale = rt.localScale;

        rt.SetParent(_shopCanvas.transform, false);

        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        rt.localScale = localScale;
    }

    // ─── Open ─────────────────────────────────────────────────────────────────

    public void OpenShop()
    {
        // Reset _isOpen để cho phép mở lại ngay sau khi popup khác đóng
        _isOpen = false;

        if (_isOpen) return;
        _isOpen = true;

        if (panelShopRoot == null) { Debug.LogError("[ShopManager] panelShopRoot NULL!"); return; }
        if (_shopCanvasGroup == null) { Debug.LogError("[ShopManager] _shopCanvasGroup NULL!"); return; }

        panelShopRoot.SetActive(true);

        _shopCanvasGroup.DOKill();
        _shopCanvasGroup.alpha = 0f;
        _shopCanvasGroup.interactable = false;
        _shopCanvasGroup.blocksRaycasts = false;

        if (panelShopPanel != null)
        {
            panelShopPanel.DOKill();
            panelShopPanel.localScale = Vector3.one * 0.9f;
            panelShopPanel.DOScale(Vector3.one, scaleDuration).SetEase(scaleEaseIn);
        }

        _shopCanvasGroup.DOFade(1f, fadeDuration).OnComplete(() =>
        {
            _shopCanvasGroup.interactable = true;
            _shopCanvasGroup.blocksRaycasts = true;
            Debug.Log("[ShopManager] OpenShop() hoàn tất.");
        });
    }

    private void HandleOpenViaEvent() => OpenShop();

    // ─── Close ────────────────────────────────────────────────────────────────

    public void CloseShop()
    {
        if (!_isOpen) return;
        _isOpen = false;

        if (_shopCanvasGroup == null) return;

        _shopCanvasGroup.interactable = false;
        _shopCanvasGroup.blocksRaycasts = false;
        _shopCanvasGroup.DOKill();
        _shopCanvasGroup.DOFade(0f, fadeDuration).OnComplete(() =>
        {
            if (panelShopRoot != null) panelShopRoot.SetActive(false);
        });
    }

    // Button Close trong panelShop gán vào đây
    public void OnClickClose() => CloseShop();

    private void OnDestroy()
    {
        if (_shopCanvas != null) Destroy(_shopCanvas.gameObject);
    }
}