// Editor/ObstaclePresetEditor.cs
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using FoodMatch.Data;

namespace FoodMatch.Editor
{
    [CustomEditor(typeof(ObstaclePreset))]
    public class ObstaclePresetEditor : UnityEditor.Editor
    {
        private static readonly List<(string label, Type type)> ObstacleTypes = new()
        {
            ("🔒 Lock", typeof(LockObstacleData)),
            ("🧪 Tube", typeof(TubeObstacleData)),
            ("🎢 Conveyor", typeof(ConveyorObstacleData)),
        };

        private SerializedProperty _descProp;
        private SerializedProperty _obstaclesProp;

        private void OnEnable()
        {
            _descProp = serializedObject.FindProperty("description");
            _obstaclesProp = serializedObject.FindProperty("obstacles");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_descProp);
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("─── Obstacles ───────────────────────", EditorStyles.boldLabel);

            var preset = (ObstaclePreset)target;
            if (preset.obstacles == null) preset.obstacles = new();

            // Vẽ từng obstacle
            for (int i = 0; i < _obstaclesProp.arraySize; i++)
            {
                var element = _obstaclesProp.GetArrayElementAtIndex(i);
                var obstacle = preset.obstacles[i];
                if (obstacle == null) continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                obstacle.isEnabled = EditorGUILayout.Toggle(obstacle.isEnabled, GUILayout.Width(16));
                EditorGUILayout.LabelField(obstacle.ObstacleName, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("✕", GUILayout.Width(24)))
                {
                    preset.obstacles.RemoveAt(i);
                    EditorUtility.SetDirty(target);
                    break;
                }
                EditorGUILayout.EndHorizontal();

                if (obstacle.isEnabled)
                    EditorGUILayout.PropertyField(element, GUIContent.none, true);

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            // Nút thêm
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Add:", GUILayout.Width(32));
            foreach (var (label, type) in ObstacleTypes)
            {
                bool alreadyHas = preset.obstacles.Exists(o => o?.GetType() == type);
                GUI.enabled = !alreadyHas;
                if (GUILayout.Button(label))
                {
                    preset.obstacles.Add((ObstacleData)Activator.CreateInstance(type));
                    EditorUtility.SetDirty(target);
                }
                GUI.enabled = true;
            }
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif