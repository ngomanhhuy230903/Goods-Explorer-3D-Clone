using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace FoodMatch.UI
{
    /// <summary>
    /// Gắn vào GameObject "QuitWarningPopup".
    /// Hiện khi người chơi nhấn Quit trong Settings.
    /// Cảnh báo: thoát sẽ mất 1 HP.
    /// </summary>
    public class QuitWarningPopup : MonoBehaviour
    {
        [Header("─── Buttons ─────────────────────────────")]
        [SerializeField] private Button btnConfirm;   // "Xác nhận thoát / Yes"
        [SerializeField] private Button btnCancel;    // "Hủy / No"

        [Header("─── Ref ────────────────────────────────")]
        [SerializeField] private SettingsUI settingsUI;

        [Header("─── Animation ───────────────────────────")]
        [SerializeField] private float scaleDuration = 0.3f;
        [SerializeField] private Ease openEase = Ease.OutBack;
        [SerializeField] private Ease closeEase = Ease.InBack;

        private void Awake()
        {
            btnConfirm?.onClick.AddListener(OnConfirm);
            btnCancel?.onClick.AddListener(OnCancel);
            gameObject.SetActive(false);
        }

        private void OnConfirm()
        {
            AnimateClose(() => settingsUI?.ConfirmQuit());
        }

        private void OnCancel()
        {
            AnimateClose(() => settingsUI?.CancelQuit());
        }

        private void AnimateClose(System.Action onComplete)
        {
            transform
                .DOScale(Vector3.zero, scaleDuration * 0.8f)
                .SetEase(closeEase)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    gameObject.SetActive(false);
                    onComplete?.Invoke();
                });
        }
    }
}