using System.Collections;
using UnityEngine;
using FoodMatch.Tray;

namespace FoodMatch.Items
{
    [Booster]
    public class ExtraTrayBooster : IBooster
    {
        public string BoosterName => "ExtraTray";

        private BackupTray _backupTray;
        private BackupTraySpawner _spawner;
        private MonoBehaviour _runner;

        private const int MaxSlots = 7;

        /// <summary>
        /// Flag chống dùng lại trong cùng 1 game session.
        /// Consume xảy ra ngay khi UseBooster() pass guard (trong BoosterManager),
        /// flag này đảm bảo CanExecute() = false ngay sau lần dùng đầu tiên.
        /// Reset về false khi Initialize() được gọi lại (level mới / game mới).
        /// </summary>
        private bool _usedThisGame = false;

        public void Initialize(BoosterContext ctx)
        {
            _backupTray = ctx.BackupTray;
            _spawner = ctx.BackupTraySpawner;
            _runner = ctx.CoroutineRunner;

            // Reset khi game/level mới bắt đầu
            _usedThisGame = false;
        }

        public bool CanExecute()
        {
            if (_backupTray == null || _spawner == null) return false;
            if (_usedThisGame) return false;                    // đã dùng rồi → block
            return _backupTray.Capacity < MaxSlots;
        }

        public void Execute()
        {
            // Đánh dấu NGAY LẬP TỨC — trước khi animation chạy.
            // Dù người dùng spam click thêm, CanExecute() sẽ trả false từ frame này.
            _usedThisGame = true;

            Debug.Log($"[ExtraTray] Executing. Capacity hiện tại: {_backupTray.Capacity}");

            // Chạy coroutine — NotifyBoosterCompleted sẽ được gọi SAU KHI animation xong.
            _runner.StartCoroutine(ExecuteRoutine());
        }

        private IEnumerator ExecuteRoutine()
        {
            // Chờ AddExtraSlotRoutine hoàn thành (nó tự xử lý InputBlocker bên trong)
            bool done = false;
            _spawner.AddExtraSlot(onComplete: () => done = true);

            yield return new WaitUntil(() => done);

            Debug.Log($"[ExtraTray] Done. Capacity mới: {_backupTray.Capacity}");

            // Release lock SAU KHI animation xong — consumed=true vì đã mark _usedThisGame
            BoosterManager.Instance?.NotifyBoosterCompleted(BoosterName, consumed: true);
        }
    }
}