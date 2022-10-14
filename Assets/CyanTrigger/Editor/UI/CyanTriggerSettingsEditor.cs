using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cyan.CT.Editor
{
    [CustomEditor(typeof(CyanTriggerSettingsData))]
    public class CyanTriggerSettingsEditor : UnityEditor.Editor
    {
        public static readonly GUIContent CompileSceneTriggersButtonLabel = new GUIContent("Compile all Scene CyanTrigger", "Compile all CyanTrigger components in the opened scene.");
        public static readonly GUIContent CompileAssetTriggersButtonLabel = new GUIContent("Compile all CyanTrigger Program Assets", "Compile all CyanTrigger Program Assets in the project. This may take some time.");
        private static readonly GUIContent ClearSerializedDataButtonLabel = new GUIContent("Clear Serialized Data", "Clear all serialized CyanTrigger data for the project. The serialized data is generated and reused between scenes, but sometimes can generate more than needed.");
        
        private static readonly GUIContent ActionDetailedViewLabel = new GUIContent("Show Action Parameters", "When enabled, all parameters in each CyanTrigger action will be displayed with the action type. This can clutter the UI, but gives enough detail to fully understand the action without expanding it.");
        private static readonly GUIContent ActionColorThemeLabel = new GUIContent("Use Color Theme", "When enabled, all actions will display color information for each item within the action display name.");

        // private static readonly GUIContent CompileSceneTriggersStatsLabel = new GUIContent("Create Scene Trigger Stats", "");
        private static readonly GUIContent CompileSceneTriggersOnSaveLabel = new GUIContent("Compile Scene Triggers On Save", "When enabled, saving a scene will compile all CyanTrigger components in the opened scene.");
        private static readonly GUIContent CompileSceneTriggersOnPlayLabel = new GUIContent("Compile Scene Triggers On Play", "When enabled, entering playmode will compile all CyanTrigger components in the opened scene.");
        private static readonly GUIContent CompileSceneTriggersOnBuildLabel = new GUIContent("Compile Scene Triggers On Build", "When enabled, building a world for VRChat will compile all CyanTrigger components in the opened scene.");
        
        private static readonly GUIContent CompileAssetTriggersOnBuildLabel = new GUIContent("Compile Asset Triggers On Build", "When enabled, building a world for VRChat will compile all CyanTrigger Program Assets in the project.");
        private static readonly GUIContent CompileAssetTriggersOnEditLabel = new GUIContent("Compile Asset Triggers On Edit", $"When enabled, making changes to a CyanTrigger Program Asset will auto compile after {CyanTriggerProgramAssetBaseEditor.TimeDelayBeforeAutoCompile:F1} seconds.");
        private static readonly GUIContent CompileAssetTriggersOnCloseLabel = new GUIContent("Compile Asset Triggers On Close", "When enabled, making changes to a CyanTrigger Program Asset will auto compile the program and its dependencies after closing the inspector.");

        
        private static GUIStyle _style;
        
        // UI settings
        private SerializedProperty _actionDetailedViewProperty;
        private SerializedProperty _actionColorThemesProperty;
        private SerializedProperty _colorThemeDarkProperty;
        private SerializedProperty _colorThemeLightProperty;
        
        // Scene Compile Settings
        private SerializedProperty _compileSceneTriggersOnSaveProperty;
        private SerializedProperty _compileSceneTriggersOnPlayProperty;
        private SerializedProperty _compileSceneTriggersOnBuildProperty;
        // private SerializedProperty _compileSceneTriggersStatsProperty;
        
        // Asset Compile Settings
        private SerializedProperty _compileAssetTriggersOnBuildProperty;
        private SerializedProperty _compileAssetTriggersOnEditProperty;
        private SerializedProperty _compileAssetTriggersOnCloseProperty;
        
        private bool _delayUpdateUI;
        
        private void OnEnable()
        {
            if (target == null)
            {
                DestroyImmediate(this);
                return;
            }
            
            _actionDetailedViewProperty = serializedObject.FindProperty(nameof(CyanTriggerSettingsData.actionDetailedView));
            _actionColorThemesProperty = serializedObject.FindProperty(nameof(CyanTriggerSettingsData.useColorThemes));
            _colorThemeLightProperty = serializedObject.FindProperty(nameof(CyanTriggerSettingsData.colorThemeLight));
            _colorThemeDarkProperty = serializedObject.FindProperty(nameof(CyanTriggerSettingsData.colorThemeDark));
        
            _compileSceneTriggersOnSaveProperty = serializedObject.FindProperty(nameof(CyanTriggerSettingsData.compileSceneTriggersOnSave));
            _compileSceneTriggersOnPlayProperty = serializedObject.FindProperty(nameof(CyanTriggerSettingsData.compileSceneTriggersOnPlay));
            _compileSceneTriggersOnBuildProperty = serializedObject.FindProperty(nameof(CyanTriggerSettingsData.compileSceneTriggersOnBuild));
            // _compileSceneTriggersStatsProperty = serializedObject.FindProperty(nameof(CyanTriggerSettingsData.compileSceneTriggerStats));
            
            _compileAssetTriggersOnBuildProperty = serializedObject.FindProperty(nameof(CyanTriggerSettingsData.compileAssetTriggersOnBuild));
            _compileAssetTriggersOnEditProperty = serializedObject.FindProperty(nameof(CyanTriggerSettingsData.compileAssetTriggersOnEdit));
            _compileAssetTriggersOnCloseProperty = serializedObject.FindProperty(nameof(CyanTriggerSettingsData.compileAssetTriggersOnClose));
        }

        public override void OnInspectorGUI()
        {
            _style = EditorStyles.helpBox;
            
            serializedObject.UpdateIfRequiredOrScript();

            float tempLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 250;
            
            DrawElements();
            
            EditorGUIUtility.labelWidth = tempLabelWidth;
            
            serializedObject.ApplyModifiedProperties();

            if (_delayUpdateUI)
            {
                _delayUpdateUI = false;
                CyanTriggerSerializableInstanceEditor.UpdateAllOpenSerializers();
                CyanTriggerImageResources.ClearThemeCache();
                CyanTriggerEditorGUIUtil.ClearThemeCache();
            }
        }

        private void DrawElements()
        {
            CyanTriggerEditorUtils.DrawHeader("CyanTrigger Settings", false);
            EditorGUILayout.Space();
            
            DrawActionSection();
            EditorGUILayout.Space();
            
            DrawSceneCompileSettings();
            EditorGUILayout.Space();

            DrawAssetCompileSettings();
            EditorGUILayout.Space();
            
            DrawUISettings();
            EditorGUILayout.Space();
        }
        
        private void DrawActionSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            
            if (GUILayout.Button(CompileSceneTriggersButtonLabel))
            {
                CyanTriggerSerializerManager.RecompileAllTriggers(true, true);
            }
            
            if (GUILayout.Button(CompileAssetTriggersButtonLabel))
            {
                CyanTriggerSerializedProgramManager.CompileAllCyanTriggerEditableAssets(true);
            }
            
            if (GUILayout.Button(ClearSerializedDataButtonLabel))
            {
                CyanTriggerSerializedProgramManager.Instance.ClearSerializedData();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawUISettings()
        {
            EditorGUILayout.BeginVertical(_style);
            
            EditorGUILayout.LabelField("UI Settings", EditorStyles.boldLabel);
            
            CyanTriggerEditorUtils.AddIndent();

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(_actionDetailedViewProperty, ActionDetailedViewLabel);
            EditorGUILayout.PropertyField(_actionColorThemesProperty, ActionColorThemeLabel);
            
            EditorGUI.BeginDisabledGroup(!_actionColorThemesProperty.boolValue);
            
            var settings = CyanTriggerSettings.Instance;

            DisplayThemeOptions(settings, true);
            EditorGUILayout.PropertyField(_colorThemeDarkProperty);

            EditorGUILayout.Space();
            DisplayThemeOptions(settings, false);
            EditorGUILayout.PropertyField(_colorThemeLightProperty);

            EditorGUI.EndDisabledGroup();
            
            if (EditorGUI.EndChangeCheck())
            {
                _delayUpdateUI = true;
            }
            
            CyanTriggerEditorUtils.RemoveIndent();

            EditorGUILayout.EndVertical();
        }

        private void DisplayThemeOptions(CyanTriggerSettingsData settings, bool isDarkTheme)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(30);

            if (GUILayout.Button("Reset"))
            {
                Undo.RecordObject(settings, "Reset Theme");
                if (isDarkTheme)
                {
                    settings.colorThemeDark = CyanTriggerSettingsColor.Clone(CyanTriggerSettingsColor.DarkThemeDefault);
                }
                else
                {
                    settings.colorThemeLight = CyanTriggerSettingsColor.Clone(CyanTriggerSettingsColor.LightThemeDefault);
                }
                _delayUpdateUI = true;
                serializedObject.Update();
            }
            if (GUILayout.Button("Export"))
            {
                string path = EditorUtility.SaveFilePanelInProject("Export Theme", "CyanTriggerColorTheme", "json", $"Saving CyanTrigger {(isDarkTheme ? "Dark" : "Light")} Theme to file");
                if (!string.IsNullOrEmpty(path))
                {
                    string themeAsJson = JsonUtility.ToJson(isDarkTheme ? settings.colorThemeDark : settings.colorThemeLight, true);
                    File.WriteAllText(path, themeAsJson);
                    AssetDatabase.Refresh();
                }
            }
            if (GUILayout.Button("Import"))
            {
                string path = EditorUtility.OpenFilePanel("Import Theme", "", "json");
                if (!string.IsNullOrEmpty(path))
                {
                    var fileContent = File.ReadAllText(path);
                    Undo.RecordObject(settings, "Import Theme");
                    CyanTriggerSettingsColor colors = JsonUtility.FromJson<CyanTriggerSettingsColor>(fileContent);
                    if (isDarkTheme)
                    {
                        settings.colorThemeDark = colors;
                    }
                    else
                    {
                        settings.colorThemeLight = colors;
                    }
                    _delayUpdateUI = true;
                    serializedObject.Update();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSceneCompileSettings()
        {
            EditorGUILayout.BeginVertical(_style);

            EditorGUILayout.LabelField("Scene Compile Settings", EditorStyles.boldLabel);

            CyanTriggerEditorUtils.AddIndent();

            // TODO
            // EditorGUILayout.PropertyField(_compileSceneTriggersStatsProperty, CompileSceneTriggersStatsLabel);
            EditorGUILayout.PropertyField(_compileSceneTriggersOnSaveProperty, CompileSceneTriggersOnSaveLabel);
            EditorGUILayout.PropertyField(_compileSceneTriggersOnPlayProperty, CompileSceneTriggersOnPlayLabel);
            EditorGUILayout.PropertyField(_compileSceneTriggersOnBuildProperty, CompileSceneTriggersOnBuildLabel);

            CyanTriggerEditorUtils.RemoveIndent();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawAssetCompileSettings()
        {
            EditorGUILayout.BeginVertical(_style);
            EditorGUILayout.LabelField("Program Asset Compile Settings", EditorStyles.boldLabel);
            
            CyanTriggerEditorUtils.AddIndent();
            
            EditorGUILayout.PropertyField(_compileAssetTriggersOnBuildProperty, CompileAssetTriggersOnBuildLabel);
            EditorGUILayout.PropertyField(_compileAssetTriggersOnEditProperty, CompileAssetTriggersOnEditLabel);
            EditorGUILayout.PropertyField(_compileAssetTriggersOnCloseProperty, CompileAssetTriggersOnCloseLabel);
            
            CyanTriggerEditorUtils.RemoveIndent();
            
            EditorGUILayout.EndVertical();
        }
    }
}