using UnityEditor;
using UnityEngine;

namespace Cyan.CT.Editor
{
    public class CyanTriggerSettingsWindow : EditorWindow
    {
        private CyanTriggerSettingsEditor _settingsEditor;
        private CyanTriggerSettingsFavoriteListEditor _favoriteVariablesEditor;
        private CyanTriggerSettingsFavoriteListEditor _favoriteEventsEditor;
        private CyanTriggerSettingsFavoriteListEditor _favoriteActionsEditor;

        private bool _initialized = false;
        private Vector2 _scrollPosition;

        // [MenuItem ("Window/CyanTrigger/CyanTrigger Settings")]
        public static void ShowWindow()
        {
            var window = GetWindow<CyanTriggerSettingsWindow> ();
            window.titleContent = new GUIContent ("CyanTrigger Settings");
            window.Show();
        }

        private void OnEnable()
        {
            _initialized = false;
        }

        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }
            _initialized = true;
            
            _settingsEditor = (CyanTriggerSettingsEditor)
                UnityEditor.Editor.CreateEditor(CyanTriggerSettings.Instance);
            
            CyanTriggerSettingsFavoriteManager favoritesManager = CyanTriggerSettingsFavoriteManager.Instance;
            _favoriteVariablesEditor = (CyanTriggerSettingsFavoriteListEditor)
                UnityEditor.Editor.CreateEditor(favoritesManager.FavoriteVariables);
            _favoriteEventsEditor = (CyanTriggerSettingsFavoriteListEditor)
                UnityEditor.Editor.CreateEditor(favoritesManager.FavoriteEvents);
            _favoriteActionsEditor = (CyanTriggerSettingsFavoriteListEditor)
                UnityEditor.Editor.CreateEditor(favoritesManager.FavoriteActions);
        }

        private void OnDisable()
        {
            if (_settingsEditor)
            {
                DestroyImmediate(_settingsEditor);
                _settingsEditor = null;
            }
            if (_favoriteVariablesEditor)
            {
                DestroyImmediate(_favoriteVariablesEditor);
                _favoriteVariablesEditor = null;
            }
            if (_favoriteEventsEditor)
            {
                DestroyImmediate(_favoriteEventsEditor);
                _favoriteEventsEditor = null;
            }
            if (_favoriteActionsEditor)
            {
                DestroyImmediate(_favoriteActionsEditor);
                _favoriteActionsEditor = null;
            }
        }

        private void UpdateIdStartIndexes()
        {
            _favoriteVariablesEditor.SetIdStartIndex(0);
            _favoriteEventsEditor.SetIdStartIndex(_favoriteVariablesEditor.GetSize());
            _favoriteActionsEditor.SetIdStartIndex(_favoriteVariablesEditor.GetSize() + _favoriteEventsEditor.GetSize());
        }
        
        void OnGUI()
        {
            Initialize();
            
            UpdateIdStartIndexes();
            
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            _settingsEditor.OnInspectorGUI();
            EditorGUILayout.Space();
            
            _favoriteVariablesEditor.OnInspectorGUI();
            EditorGUILayout.Space();

            _favoriteEventsEditor.OnInspectorGUI();
            EditorGUILayout.Space();

            _favoriteActionsEditor.OnInspectorGUI();
            
            EditorGUILayout.EndScrollView();
        }
    }
}
