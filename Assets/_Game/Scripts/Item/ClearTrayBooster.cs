using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using FoodMatch.Food;
using FoodMatch.Tray;

namespace FoodMatch.Items
{
    [Booster]
    public class ClearTrayBooster : IBooster
    {
        public string BoosterName => "ClearTray";

        private BackupTray _backupTray;
        private FoodBuffer _foodBuffer;
        private MonoBehaviour _runner;

        private const float FlyDuration = 0.4f;
        private const float JumpPower = 1.5f;
        private const float StaggerDelay = 0.08f;

        public void Initialize(BoosterContext ctx)
        {
            _backupTray = ctx.BackupTray;
            _foodBuffer = ctx.FoodBuffer;
            _runner = ctx.CoroutineRunner;
        }

        public bool CanExecute()
        {
            if (_backupTray == null || _foodBuffer == null) return false;
            return _backupTray.OccupiedCount > 0;
        }

        public void Execute() => _runner.StartCoroutine(ClearRoutine());

        private IEnumerator ClearRoutine()
        {
            var foods = _backupTray.GetAllFoods();
            if (foods.Count == 0)
            {
                BoosterManager.Instance?.NotifyBoosterCompleted(BoosterName);
                yield break;
            }

            Vector3 bufferPos = _foodBuffer.transform.position;

            for (int i = 0; i < foods.Count; i++)
            {
                var food = foods[i];
                if (food == null) continue;

                _backupTray.TryRemoveFood(food);
                var capturedFood = food;
                capturedFood.transform
                    .DOJump(bufferPos, JumpPower, 1, FlyDuration)
                    .SetDelay(i * StaggerDelay)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() => _foodBuffer.AddFood(capturedFood));
            }

            // Chờ animation cuối cùng hoàn thành
            float totalWait = (foods.Count - 1) * StaggerDelay + FlyDuration + 0.1f;
            yield return new WaitForSeconds(totalWait);

            // ── Tất cả food đã bay vào buffer → release lock ──────────────────
            BoosterManager.Instance?.NotifyBoosterCompleted(BoosterName);
            Debug.Log("[ClearTray] Done.");
        }
    }
}