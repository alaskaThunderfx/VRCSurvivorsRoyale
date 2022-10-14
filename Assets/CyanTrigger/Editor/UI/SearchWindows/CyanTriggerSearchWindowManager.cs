using System;
using System.Collections.Generic;
using UnityEditor;
#if UNITY_2019_4_OR_NEWER
using UnityEditor.Experimental.GraphView;
#else
using UnityEditor.Experimental.UIElements.GraphView;
#endif
using UnityEngine;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerSearchWindowManager
    {
        private static CyanTriggerSearchWindowManager _instance;
        public static CyanTriggerSearchWindowManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new CyanTriggerSearchWindowManager();
                }
                return _instance;
            }
        }

        private const float WindowWidth = 400f;
        
        private readonly CyanTriggerVariableSearchWindow _variableSearchWindow;
        private readonly CyanTriggerActionSearchWindow _actionSearchWindow;
        private readonly CyanTriggerEventSearchWindow _eventSearchWindow;
        private readonly CyanTriggerFocusedSearchWindow _focusedSearchWindow;
        private readonly CyanTriggerFavoriteSearchWindow _favoritesSearchWindow;
        private readonly CyanTriggerCustomActionGroupSearchWindow _customActionsSearchWindow;
        
        // TODO make generic and take in the favorites list and auto populate every time.
        
        
        private Vector2 _searchWindowPosition;

        private CyanTriggerSearchWindowManager()
        {
            _variableSearchWindow = ScriptableObject.CreateInstance<CyanTriggerVariableSearchWindow>();
            _actionSearchWindow = ScriptableObject.CreateInstance<CyanTriggerActionSearchWindow>();
            _eventSearchWindow = ScriptableObject.CreateInstance<CyanTriggerEventSearchWindow>();
            _focusedSearchWindow = ScriptableObject.CreateInstance<CyanTriggerFocusedSearchWindow>();
            _favoritesSearchWindow = ScriptableObject.CreateInstance<CyanTriggerFavoriteSearchWindow>();
            _customActionsSearchWindow = ScriptableObject.CreateInstance<CyanTriggerCustomActionGroupSearchWindow>();
        }

        private Vector2 GetMousePos()
        {
            Vector2 pos = Vector2.zero;
            if (Event.current != null)
            {
                pos = Event.current.mousePosition;
            }

            return GUIUtility.GUIToScreenPoint(pos);
        }
        
        public void DisplayVariableSearchWindow(Action<UdonNodeDefinition> onSelect)
        {
            DisplayVariableSearchWindow(GetMousePos(), onSelect);
        }
        
        public void DisplayVariableSearchWindow(Vector2 pos, Action<UdonNodeDefinition> onSelect)
        {
            _variableSearchWindow.OnDefinitionSelected = onSelect;
            _variableSearchWindow.allowCustomTypes = false;
            _variableSearchWindow.OnCustomActionSelected = null;
            CyanTriggerSearchWindow.Open(new SearchWindowContext(pos, WindowWidth), _variableSearchWindow);
        }
        
        public void DisplayVariableSearchWindow(
            Action<UdonNodeDefinition> onSelectDefault, 
            Action<CyanTriggerActionGroupDefinition> onSelectCustom)
        {
            DisplayVariableSearchWindow(GetMousePos(), onSelectDefault, onSelectCustom);
        }
        
        public void DisplayVariableSearchWindow(
            Vector2 pos, 
            Action<UdonNodeDefinition> onSelectDefault, 
            Action<CyanTriggerActionGroupDefinition> onSelectCustom)
        {
            _variableSearchWindow.OnDefinitionSelected = onSelectDefault;
            _variableSearchWindow.allowCustomTypes = true;
            _variableSearchWindow.OnCustomActionSelected = onSelectCustom;
            CyanTriggerSearchWindow.Open(new SearchWindowContext(pos, WindowWidth), _variableSearchWindow);
        }
        
        public void DisplayCustomActionSearchWindow(Action<CyanTriggerActionGroupDefinition> onSelect)
        {
            DisplayCustomActionSearchWindow(GetMousePos(), onSelect);
        }
        
        public void DisplayCustomActionSearchWindow(Vector2 pos, Action<CyanTriggerActionGroupDefinition> onSelect)
        {
            _customActionsSearchWindow.OnDefinitionSelected = onSelect;
            CyanTriggerSearchWindow.Open(new SearchWindowContext(pos, WindowWidth), _customActionsSearchWindow);
        }
        
        public void DisplayActionSearchWindow(Action<CyanTriggerActionInfoHolder> onSelect)
        {
            DisplayActionSearchWindow(GetMousePos(), onSelect);
        }
        
        public void DisplayActionSearchWindow(Vector2 pos, Action<CyanTriggerActionInfoHolder> onSelect)
        {
            _actionSearchWindow.OnDefinitionSelected = onSelect;
            CyanTriggerSearchWindow.Open(new SearchWindowContext(pos, WindowWidth), _actionSearchWindow);
        }
        
        public void DisplayEventSearchWindow(Action<CyanTriggerActionInfoHolder> onSelect)
        {
            DisplayEventSearchWindow(GetMousePos(), onSelect);
        }
        
        public void DisplayEventSearchWindow(Vector2 pos, Action<CyanTriggerActionInfoHolder> onSelect)
        {
            _eventSearchWindow.OnDefinitionSelected = onSelect;
            CyanTriggerSearchWindow.Open(new SearchWindowContext(pos, WindowWidth), _eventSearchWindow);
        }
        
        public void DisplayFocusedSearchWindow(
            Vector2 pos, 
            Action<CyanTriggerActionInfoHolder> onSelect, 
            string title, 
            List<CyanTriggerActionInfoHolder> entries,
            Func<CyanTriggerActionInfoHolder, string> displayMethod = null)
        {
            _searchWindowPosition = pos;
            _focusedSearchWindow.OnDefinitionSelected = onSelect;
            _focusedSearchWindow.WindowTitle = title;
            _focusedSearchWindow.FocusedNodeDefinitions = entries;
            
            if (displayMethod == null)
            {
                _focusedSearchWindow.ResetDisplayMethod();
            }
            else
            {
                _focusedSearchWindow.GetDisplayString = displayMethod;
            }
            
            EditorApplication.update += TryOpenFocusedSearch;
        }
        
        private void TryOpenFocusedSearch()
        {
            if (CyanTriggerSearchWindow.Open(new SearchWindowContext(_searchWindowPosition, WindowWidth), _focusedSearchWindow))
            {
                EditorApplication.update -= TryOpenFocusedSearch;
            }
        }
        
        
        public void DisplayVariableFavoritesSearchWindow(Action<CyanTriggerSettingsFavoriteItem> onSelect)
        {
            DisplayVariableFavoritesSearchWindow(GetMousePos(), onSelect);
        }

        public void DisplayVariableFavoritesSearchWindow(Vector2 pos, Action<CyanTriggerSettingsFavoriteItem> onSelect)
        {
            _favoritesSearchWindow.OnDefinitionSelected = onSelect;
            _favoritesSearchWindow.WindowTitle = "Favorite Variables";
            _favoritesSearchWindow.FavoriteList =
                CyanTriggerSettingsFavoriteManager.Instance.FavoriteVariables.favoriteItems;
            
            CyanTriggerSearchWindow.Open(new SearchWindowContext(pos, WindowWidth), _favoritesSearchWindow);
        }
        
        public void DisplayEventsFavoritesSearchWindow(Action<CyanTriggerSettingsFavoriteItem> onSelect, bool displayAll = false)
        {
            DisplayEventsFavoritesSearchWindow(GetMousePos(), onSelect, displayAll);
        }
        
        public void DisplayEventsFavoritesSearchWindow(Vector2 pos, Action<CyanTriggerSettingsFavoriteItem> onSelect, bool displayAll = false)
        {
            _favoritesSearchWindow.OnDefinitionSelected = onSelect;
            _favoritesSearchWindow.WindowTitle = "Favorite Events";

            if (displayAll)
            {
                _favoritesSearchWindow.FavoriteList = CyanTriggerEventSearchWindow.GetAllEventsAsFavorites();
            }
            else
            {
                _favoritesSearchWindow.FavoriteList =
                    CyanTriggerSettingsFavoriteManager.Instance.FavoriteEvents.favoriteItems;
            }
            
            CyanTriggerSearchWindow.Open(new SearchWindowContext(pos, WindowWidth), _favoritesSearchWindow);
        }
        
        public void DisplayActionFavoritesSearchWindow(Action<CyanTriggerSettingsFavoriteItem> onSelect)
        {
            DisplayActionFavoritesSearchWindow(GetMousePos(), onSelect);
        }
        
        public void DisplayActionFavoritesSearchWindow(Vector2 pos, Action<CyanTriggerSettingsFavoriteItem> onSelect)
        {
            _favoritesSearchWindow.OnDefinitionSelected = onSelect;
            _favoritesSearchWindow.WindowTitle = "Favorite Actions";
            _favoritesSearchWindow.FavoriteList = 
                CyanTriggerSettingsFavoriteManager.Instance.FavoriteActions.favoriteItems;
            
            CyanTriggerSearchWindow.Open(new SearchWindowContext(pos, WindowWidth), _favoritesSearchWindow);
        }
        
        public void DisplaySDK2ActionFavoritesSearchWindow(Action<CyanTriggerSettingsFavoriteItem> onSelect)
        {
            DisplaySDK2ActionFavoritesSearchWindow(GetMousePos(), onSelect);
        }
        
        public void DisplaySDK2ActionFavoritesSearchWindow(Vector2 pos, Action<CyanTriggerSettingsFavoriteItem> onSelect)
        {
            _favoritesSearchWindow.OnDefinitionSelected = onSelect;
            _favoritesSearchWindow.WindowTitle = "SDK2 Actions";
            _favoritesSearchWindow.FavoriteList = 
                CyanTriggerSettingsFavoriteManager.Instance.Sdk2Actions.favoriteItems;
            
            CyanTriggerSearchWindow.Open(new SearchWindowContext(pos, WindowWidth), _favoritesSearchWindow);
        }
    }
}
