using UnityEngine;
using FoodMatch.Tray;

namespace FoodMatch.Items
{
    [Booster] // ← Chỉ cần dòng này để tự động được đăng ký
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
            return _backupTray.Capacity < MaxSlots;
        }

        public void Execute()
        {
            _spawner.AddExtraSlot();
            Debug.Log($"[ExtraTray] +1 slot. Capacity: {_backupTray.Capacity}");
        }
    }
}