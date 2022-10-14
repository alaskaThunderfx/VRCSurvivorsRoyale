
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cyan.CT.Editor
{
    public class CyanTriggerSettingsFavoriteManager
    {
        private const string FavoriteVariablesName = "Variables";
        private const string FavoriteEventsName = "Events";
        private const string FavoriteActionsName = "Actions";
        private const string SDK2ActionsName = "SDK2_Actions";
        private const string FavoritesPrefix = "CyanTriggerFavorite_";
        
        private static readonly object Lock = new object();
        
        private static CyanTriggerSettingsFavoriteManager _instance;
        public static CyanTriggerSettingsFavoriteManager Instance
        {
            get
            {
                lock (Lock)
                {
                    if (_instance == null)
                    {
                        _instance = new CyanTriggerSettingsFavoriteManager();
                    }
                    return _instance;
                }
            }
        }
        
        private CyanTriggerSettingsFavoriteList _favoriteVariables;
        public CyanTriggerSettingsFavoriteList FavoriteVariables
        {
            get
            {
                if (_favoriteVariables == null)
                {
                    _favoriteVariables = LoadOrCreateFavoriteList($"{FavoritesPrefix}{FavoriteVariablesName}");
                }
                return _favoriteVariables;
            }
        }
        
        private CyanTriggerSettingsFavoriteList _favoriteEvents;
        public CyanTriggerSettingsFavoriteList FavoriteEvents
        {
            get
            {
                if (_favoriteEvents == null)
                {
                    _favoriteEvents = LoadOrCreateFavoriteList($"{FavoritesPrefix}{FavoriteEventsName}");
                }
                return _favoriteEvents;
            }
        }

        private CyanTriggerSettingsFavoriteList _favoriteActions;
        public CyanTriggerSettingsFavoriteList FavoriteActions
        {
            get
            {
                if (_favoriteActions == null)
                {
                    _favoriteActions =  LoadOrCreateFavoriteList($"{FavoritesPrefix}{FavoriteActionsName}");
                }
                return _favoriteActions;
            }
        }

        
        private CyanTriggerSettingsFavoriteList _sdk2Actions;
        public CyanTriggerSettingsFavoriteList Sdk2Actions
        {
            get
            {
                if (_sdk2Actions == null)
                {
                    _sdk2Actions = LoadOrCreateFavoriteList($"{FavoritesPrefix}{SDK2ActionsName}");
                }
                return _sdk2Actions;
            }
        }

        private static CyanTriggerSettingsFavoriteList LoadOrCreateFavoriteList(string favoritesName)
        {
            string settingsFolderPath = CyanTriggerSettings.GetSettingsDirectoryPath();
            string assetPath = Path.Combine(settingsFolderPath, $"{favoritesName}.asset");
            var favoriteList = AssetDatabase.LoadAssetAtPath<CyanTriggerSettingsFavoriteList>(assetPath);
            if (favoriteList == null)
            {
                favoriteList = CreateFavoriteList(favoritesName, assetPath);
            }

            return favoriteList;
        }
        
        private static CyanTriggerSettingsFavoriteList CreateFavoriteList(string favoriteName, string assetPath)
        {
            CyanTriggerSettingsFavoriteList favoriteList =
                ScriptableObject.CreateInstance<CyanTriggerSettingsFavoriteList>();
            AssetDatabase.CreateAsset(favoriteList, assetPath);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            
            // Load from expected and copy over.
            CyanTriggerSettingsFavoriteList resourceFavorite = 
                Resources.Load<CyanTriggerSettingsFavoriteList>(Path.Combine("Settings", favoriteName));
            if (resourceFavorite == null)
            {
#if CYAN_TRIGGER_DEBUG
                Debug.LogWarning($"Could not find resources for favorite {favoriteName}");
#endif      
                return favoriteList;
            }

            favoriteList.favoriteType = resourceFavorite.favoriteType;
            CopyFavorites(resourceFavorite.favoriteItems, ref favoriteList.favoriteItems);
            EditorUtility.SetDirty(favoriteList);
            AssetDatabase.SaveAssets();
            
            return favoriteList;
        }

        private static void CopyFavorites(CyanTriggerSettingsFavoriteItem[] src, ref CyanTriggerSettingsFavoriteItem[] dest)
        {
            if (dest == null || dest.Length != src.Length)
            {
                dest = new CyanTriggerSettingsFavoriteItem[src.Length];
            }

            for (int cur = 0; cur < src.Length; ++cur)
            {
                dest[cur] = src[cur];
            }
        }
    }
}
