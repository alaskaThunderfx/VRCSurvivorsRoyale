using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cyan.CT.Editor
{
    public static class CyanTriggerSettings
    {
        private const string SettingsFolderName = "Settings";
        private const string SettingsName = "CyanTriggerSettings.asset";
        
        private static CyanTriggerSettingsData _instance;
        public static CyanTriggerSettingsData Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = LoadSettings();
                }
                return _instance;
            }
        }

        public static string GetSettingsDirectoryPath()
        {
            string dataPath = CyanTriggerResourceManager.Instance.GetDataPath();
            string settingsPath = Path.Combine(dataPath, SettingsFolderName);
            if (!AssetDatabase.IsValidFolder(settingsPath))
            {
                AssetDatabase.CreateFolder(dataPath, SettingsFolderName);
            }
            return settingsPath;
        }
        
        public static CyanTriggerSettingsData LoadSettings(string directory = "")
        {
            string settingsDirectory;
            if (string.IsNullOrEmpty(directory))
            {
                settingsDirectory = GetSettingsDirectoryPath();
            }
            else
            {
                settingsDirectory = Path.Combine(directory, SettingsFolderName);
                if (!AssetDatabase.IsValidFolder(settingsDirectory))
                {
                    AssetDatabase.CreateFolder(directory, SettingsFolderName);
                }
            }
            string settingsPath = Path.Combine(settingsDirectory, SettingsName);
            
            CyanTriggerSettingsData settings = AssetDatabase.LoadAssetAtPath<CyanTriggerSettingsData>(settingsPath);
            if (settings == null)
            {
                settings = CreateAndImportSettings(settingsPath);
            }
            else if (settings.CheckDefaultSettings()) 
            {
                EditorUtility.SetDirty(settings);
            }
            
            return settings;
        }
        
        private static CyanTriggerSettingsData CreateAndImportSettings(string path)
        {
            CyanTriggerSettingsData settings = ScriptableObject.CreateInstance<CyanTriggerSettingsData>();
            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.ImportAsset(path);
            return settings;
        }
    }
}