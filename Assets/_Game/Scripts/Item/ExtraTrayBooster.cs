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

        private const int MaxSlots = 7;

        public void Initialize(BoosterContext ctx)
        {
            _backupTray = ctx.BackupTray;
            _spawner = ctx.BackupTraySpawner;
        }

        public bool CanExecute()
        {
            if (_backupTray == null || _spawner == null) return false;
            // Chặn nếu đã đạt max — BoosterManager._isBusy đã chặn spam từ bên ngoài
            return _backupTray.Capacity < MaxSlots;
        }

        public void Execute()
        {
            _spawner.AddExtraSlot();
            Debug.Log($"[ExtraTray] +1 slot. Capacity: {_backupTray.Capacity}");

            // AddExtraSlot là synchronous (không có animation chờ đợi)
            // → release lock ngay sau khi spawn xong
            BoosterManager.Instance?.NotifyBoosterCompleted(BoosterName);
        }
    }
}