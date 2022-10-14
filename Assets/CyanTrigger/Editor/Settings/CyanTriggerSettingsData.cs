using UnityEditor;
using UnityEngine;

namespace Cyan.CT.Editor
{
    [ExcludeFromPresetAttribute]
    public class CyanTriggerSettingsData : ScriptableObject
    {
        // Scene Compile Settings
        public bool compileSceneTriggersOnSave = true;
        public bool compileSceneTriggersOnPlay = true;
        public bool compileSceneTriggersOnBuild = true;

        // public bool compileSceneTriggerStats = false;
        
        // Asset Compile Settings
        public bool compileAssetTriggersOnBuild = false;
        public bool compileAssetTriggersOnEdit = true;
        public bool compileAssetTriggersOnClose = true;
        
        // UI settings
        public bool actionDetailedView = true;
        public bool useColorThemes = true;

        public CyanTriggerSettingsColor colorThemeDark = CyanTriggerSettingsColor.Clone(CyanTriggerSettingsColor.GetDarkTheme());
        public CyanTriggerSettingsColor colorThemeLight = CyanTriggerSettingsColor.Clone(CyanTriggerSettingsColor.GetLightTheme());
        
        // ==== Hidden information ====
        
        // Object containing the directory for CyanTrigger's data path.
        public Object directoryPath = null;
        
        // Used to know if all prefabs in the project should be migrated
        public int lastMigratedDataVersion = -1;
        
        // Used to know if new version of Examples should be imported. Only applies to packages
        public string lastExampleVersionImported;
        
        
        public CyanTriggerSettingsColor GetColorTheme()
        {
            if (useColorThemes)
            {
                return EditorGUIUtility.isProSkin ? colorThemeDark : colorThemeLight;
            }
            return EditorGUIUtility.isProSkin 
                ? CyanTriggerSettingsColor.DarkThemeNoColor 
                : CyanTriggerSettingsColor.LightThemeNoColor;
        }

        public bool CheckDefaultSettings()
        {
            bool changes = false;
            if (colorThemeDark == null)
            {
                colorThemeDark = CyanTriggerSettingsColor.Clone(CyanTriggerSettingsColor.GetDarkTheme());
                changes = true;
            }
            if (colorThemeLight == null)
            {
                colorThemeLight = CyanTriggerSettingsColor.Clone(CyanTriggerSettingsColor.GetLightTheme());
                changes = true;
            }

            return changes;
        }
    }
}

