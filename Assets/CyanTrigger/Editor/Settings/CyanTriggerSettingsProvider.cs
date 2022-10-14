using System.Collections.Generic;
using UnityEditor;

namespace Cyan.CT.Editor
{
    public static class CyanTriggerSettingsProvider
    {
        public const string SettingsPath = "Project/CyanTrigger";
        private static CyanTriggerSettingsData GetSettings()
        {
            return CyanTriggerSettings.Instance;
        }
        
        private static CyanTriggerSettingsFavoriteList GetFavoriteVariables()
        {
            return CyanTriggerSettingsFavoriteManager.Instance.FavoriteVariables;
        }
        
        private static CyanTriggerSettingsFavoriteList GetFavoriteEvents()
        {
            return CyanTriggerSettingsFavoriteManager.Instance.FavoriteEvents;
        }
        
        private static CyanTriggerSettingsFavoriteList GetFavoriteActions()
        {
            return CyanTriggerSettingsFavoriteManager.Instance.FavoriteActions;
        }
        
        private static CyanTriggerSettingsFavoriteList GetFavoriteSdk2Items()
        {
            return CyanTriggerSettingsFavoriteManager.Instance.Sdk2Actions;
        }
        
        [SettingsProviderGroup]
        public static SettingsProvider[] CreateMyCustomSettingsProvider()
        {
            var providers = new List<SettingsProvider>
            {
                new AssetSettingsProvider(SettingsPath, GetSettings),
                new AssetSettingsProvider($"{SettingsPath}/Favorite Variables", GetFavoriteVariables),
                new AssetSettingsProvider($"{SettingsPath}/Favorite Events", GetFavoriteEvents),
                new AssetSettingsProvider($"{SettingsPath}/Favorite Actions", GetFavoriteActions),
                new AssetSettingsProvider($"{SettingsPath}/Favorite Actions SDK2", GetFavoriteSdk2Items),
            };
            
            return providers.ToArray();
        }
    }
}