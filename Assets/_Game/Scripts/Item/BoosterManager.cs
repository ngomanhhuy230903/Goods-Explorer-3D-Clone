using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using FoodMatch.Core;

namespace FoodMatch.Items
{
    /// <summary>
    /// Singleton — tự động scan assembly tìm class có [Booster],
    /// tạo instance, inject context, đăng ký vào registry.
    /// Thêm booster mới: chỉ tạo file + [Booster] attribute.
    /// </summary>
    public class BoosterManager : MonoBehaviour
    {
        public static BoosterManager Instance { get; private set; }

        private readonly Dictionary<string, IBooster> _registry
            = new Dictionary<string, IBooster>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>
        /// Gọi 1 lần từ BoosterInstaller.Start() sau khi có đủ references.
        /// </summary>
        public void AutoRegisterAll(BoosterContext context)
        {
            _registry.Clear();

            var boosterType = typeof(IBooster);
            var assembly = Assembly.GetExecutingAssembly();

            foreach (var type in assembly.GetTypes())
            {
                // Chỉ lấy class có [Booster] attribute
                if (type.GetCustomAttribute<BoosterAttribute>() == null) continue;
                if (!boosterType.IsAssignableFrom(type)) continue;
                if (type.IsAbstract || type.IsInterface) continue;

                // Tạo instance qua parameterless constructor
                if (Activator.CreateInstance(type) is not IBooster booster)
                {
                    Debug.LogWarning($"[BoosterManager] Không tạo được instance: {type.Name}");
                    continue;
                }

                // Inject dependencies
                booster.Initialize(context);

                // Đăng ký vào registry
                if (_registry.ContainsKey(booster.BoosterName))
                {
                    Debug.LogWarning($"[BoosterManager] Trùng tên: '{booster.BoosterName}'");
                    continue;
                }

                _registry[booster.BoosterName] = booster;
                Debug.Log($"[BoosterManager] Auto-registered: {booster.BoosterName}");
            }

            Debug.Log($"[BoosterManager] Tổng: {_registry.Count} boosters.");
        }

        /// <summary>Gọi từ BoosterButtonUI.</summary>
        public void UseBooster(string boosterName)
        {
            if (!_registry.TryGetValue(boosterName, out var booster))
            {
                Debug.LogError($"[BoosterManager] Không tìm thấy: '{boosterName}'");
                return;
            }

            if (!booster.CanExecute())
            {
                Debug.Log($"[BoosterManager] '{boosterName}' không thể dùng.");
                return;
            }

            booster.Execute();
            EventBus.RaiseBoosterActivated(boosterName);
        }

        public void UnregisterAll() => _registry.Clear();
    }
}