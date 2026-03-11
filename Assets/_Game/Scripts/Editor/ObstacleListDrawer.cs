// Editor/ObstacleListDrawer.cs
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using FoodMatch.Data;

namespace FoodMatch.Editor
{
    /// <summary>
    /// Vẽ lại phần "obstacles" trong LevelConfig Inspector:
    /// - Nút [+ Lock] [+ Tube] [+ Conveyor] để thêm nhanh
    /// - Nút [Apply Preset] để copy từ ObstaclePreset SO
    /// - Nút [✕] để xóa từng obstacle
    /// - Toggle enable/disable inline
    /// </summary>
    [CustomEditor(typeof(LevelConfig))]
    public class LevelConfigEditor : UnityEditor.Editor
    {
        // Registry: thêm obstacle type mới vào đây là xong
        private static readonly List<(string label, Type type)> ObstacleTypes = new()
        {
            ("🔒 Lock", typeof(LockObstacleData)),
            ("🧪 Tube", typeof(TubeObstacleData)),
            ("🎢 Conveyor", typeof(ConveyorObstacleData)),
            // ("💣 Bomb",   typeof(BombObstacleData)),  ← thêm sau này 1 dòng
        };

        private SerializedProperty _obstaclesProp;
        private SerializedProperty _presetProp;

        private void OnEnable()
        {
            _obstaclesProp = serializedObject.FindProperty("obstacles");
            _presetProp = serializedObject.FindProperty("obstaclePreset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Vẽ tất cả field bình thường TRỪ obstacles và obstaclePreset
            DrawPropertiesExcluding(serializedObject, "obstacles", "obstaclePreset");

            EditorGUILayout.Space(8);
            DrawObstacleSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawObstacleSection()
        {
            // ── Header ──────────────────────────────────────────────────────
            EditorGUILayout.LabelField("─── Obstacles ───────────────────────",
                EditorStyles.boldLabel);

            // ── Preset apply ────────────────────────────────────────────────
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(_presetProp,
                new GUIContent("Preset", "Chọn preset rồi bấm Apply"));
            GUI.enabled = _presetProp.objectReferenceValue != null;
            if (GUILayout.Button("Apply", GUILayout.Width(60)))
                ApplyPreset();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // ── Danh sách obstacle hiện tại ─────────────────────────────────
            var config = (LevelConfig)target;
            if (config.obstacles == null) config.obstacles = new();

            for (int i = 0; i < _obstaclesProp.arraySize; i++)
            {
                var element = _obstaclesProp.GetArrayElementAtIndex(i);
                var obstacle = config.obstacles[i];
                if (obstacle == null) continue;

                // Box mỗi obstacle
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Header row: toggle + tên + xóa
                EditorGUILayout.BeginHorizontal();
                obstacle.isEnabled = EditorGUILayout.Toggle(obstacle.isEnabled,
                    GUILayout.Width(16));
                EditorGUILayout.LabelField(
                    $"{obstacle.ObstacleName}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("✕", GUILayout.Width(24)))
                {
                    config.obstacles.RemoveAt(i);
                    EditorUtility.SetDirty(target);
                    break;
                }
                EditorGUILayout.EndHorizontal();

                // Fields của obstacle
                if (obstacle.isEnabled)
                    EditorGUILayout.PropertyField(element, GUIContent.none, true);

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            // ── Nút thêm obstacle ────────────────────────────────────────────
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Add:", GUILayout.Width(32));
            foreach (var (label, type) in ObstacleTypes)
            {
                // Disable nếu đã có obstacle kiểu này rồi
                bool alreadyHas = config.obstacles.Any(o => o?.GetType() == type);
                GUI.enabled = !alreadyHas;
                if (GUILayout.Button(label))
                {
                    var instance = (ObstacleData)Activator.CreateInstance(type);
                    config.obstacles.Add(instance);
                    EditorUtility.SetDirty(target);
                }
                GUI.enabled = true;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void ApplyPreset()
        {
            var config = (LevelConfig)target;
            var preset = (ObstaclePreset)_presetProp.objectReferenceValue;
            if (preset == null) return;

            if (EditorUtility.DisplayDialog(
                "Apply Preset",
                $"Ghi đè obstacles của Level {config.levelIndex} bằng preset '{preset.name}'?",
                "OK", "Cancel"))
            {
                config.obstacles = preset.CloneObstacles();
                EditorUtility.SetDirty(target);
                serializedObject.Update();
                Debug.Log($"[LevelConfig] Applied preset '{preset.name}' → Level {config.levelIndex}");
            }
        }
    }
}
#endif