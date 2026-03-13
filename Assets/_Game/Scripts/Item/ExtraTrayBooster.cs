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
        /// Reset về false mỗi khi ResetSession() được gọi (LoadLevel / Restart / GoHome).
        /// </summary>
        private bool _usedThisSession = false;

        public void Initialize(BoosterContext ctx)
        {
            _backupTray = ctx.BackupTray;
            _spawner = ctx.BackupTraySpawner;
            _runner = ctx.CoroutineRunner;
            // KHÔNG reset _usedThisSession ở đây — Initialize chỉ chạy 1 lần lúc AutoRegisterAll
        }

        /// <summary>
        /// Gọi từ BoosterManager.ResetAllBoosterSessions() mỗi khi level load/restart.
        /// </summary>
        public void ResetSession()
        {
            _usedThisSession = false;
        }

        public bool CanExecute()
        {
            if (_backupTray == null || _spawner == null) return false;
            if (_usedThisSession) return false;
            return _backupTray.Capacity < MaxSlots;
        }

        public void Execute()
        {
            _usedThisSession = true;
            Debug.Log($"[ExtraTray] Executing. Capacity: {_backupTray.Capacity}");
            _runner.StartCoroutine(ExecuteRoutine());
        }

        private IEnumerator ExecuteRoutine()
        {
            bool done = false;
            _spawner.AddExtraSlot(onComplete: () => done = true);
            yield return new WaitUntil(() => done);
            Debug.Log($"[ExtraTray] Done. Capacity mới: {_backupTray.Capacity}");
            BoosterManager.Instance?.NotifyBoosterCompleted(BoosterName, consumed: true);
        }
    }
}