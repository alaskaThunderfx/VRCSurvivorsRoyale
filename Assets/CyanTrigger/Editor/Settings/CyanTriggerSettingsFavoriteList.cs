using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Cyan.CT.Editor
{
    public enum CyanTriggerFavoriteType 
    {
        None = 0,
        Variables = 1,
        Events = 2,
        Actions = 3,
    }
    
    [Serializable]
    public class CyanTriggerSettingsFavoriteItem
    {
        public string item;
        public CyanTriggerActionType data;
        public int scopeDelta;
    }

    [ExcludeFromPresetAttribute]
    public class CyanTriggerSettingsFavoriteList : ScriptableObject
    {
        [FormerlySerializedAs("FavoriteType")] 
        public CyanTriggerFavoriteType favoriteType;
        [FormerlySerializedAs("FavoriteItems")] 
        public CyanTriggerSettingsFavoriteItem[] favoriteItems = Array.Empty<CyanTriggerSettingsFavoriteItem>();
    }
}