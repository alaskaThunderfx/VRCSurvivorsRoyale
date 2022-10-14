using System;
using System.Collections.Generic;
using UnityEditor;
#if UNITY_2019_4_OR_NEWER
using UnityEditor.Experimental.GraphView;
#else
using UnityEditor.Experimental.UIElements.GraphView;
#endif
using UnityEngine;

namespace Cyan.CT.Editor
{
    internal class CyanTriggerEventSearchWindow : CyanTriggerSearchWindowProvider
    {
        private static List<SearchTreeEntry> _vrcEventDefinitions;
        
        private static List<SearchTreeEntry> _registryCache;
        private static CyanTriggerSettingsFavoriteItem[] _otherFavoriteEvents;
        
        public Action<CyanTriggerActionInfoHolder> OnDefinitionSelected;


        public static void ResetCache()
        {
            _registryCache = null;
            _otherFavoriteEvents = null;
        }
        
        #region ISearchWindowProvider

        public override bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            if (entry.userData is List<CyanTriggerActionInfoHolder> actionInfos)
            {
                CyanTriggerSearchWindowManager.Instance.DisplayFocusedSearchWindow(
                    context.screenMousePosition, 
                    OnDefinitionSelected, 
                    entry.name, 
                    actionInfos);
                return true;
            }
            if (entry.userData is CyanTriggerActionInfoHolder actionInfoHolder && OnDefinitionSelected != null)
            {
                if (CyanTriggerSearchWindow.WasEventRightClick)
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Add Event"), false, () =>
                    {
                        OnDefinitionSelected.Invoke(actionInfoHolder);
                    });
                    AddDocumentationRightClickOption(menu, actionInfoHolder);
                
                    menu.ShowAsContext();
                    return false;
                }
                OnDefinitionSelected.Invoke(actionInfoHolder);
                return true;
            }

            return false;
        }
        
        public override List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            if (_vrcEventDefinitions == null)
            {
                GetVrcEventDefinitions();
            }
        
            if (_registryCache != null && _registryCache.Count > 0) return _registryCache;
            
            _registryCache = new List<SearchTreeEntry>();
            
            _registryCache.Add(new SearchTreeGroupEntry(new GUIContent("Event Search"), 0));
            
            // ReSharper disable once AssignNullToNotNullAttribute
            _registryCache.AddRange(_vrcEventDefinitions);
            
            _registryCache.Add(new SearchTreeGroupEntry(new GUIContent("Custom Events"), 1));
            _registryCache.AddRange(GetUserDefinedEvents());

            return _registryCache;
        }
        
        #endregion

        private static void GetVrcEventDefinitions()
        {
            _vrcEventDefinitions = new List<SearchTreeEntry>();
            _vrcEventDefinitions.Add(new SearchTreeGroupEntry(new GUIContent("VRC Events"), 1));

            var scriptIcon = CyanTriggerImageResources.ScriptIcon;
            foreach (var nodeDefinition in CyanTriggerNodeDefinitionManager.Instance.GetEventDefinitions())
            {
                _vrcEventDefinitions.Add(new SearchTreeEntry(new GUIContent(nodeDefinition.MethodName, scriptIcon))
                    {level = 2, userData = CyanTriggerActionInfoHolder.GetActionInfoHolder(nodeDefinition)});
            }
        }
        
        private static List<SearchTreeEntry> GetUserDefinedEvents()
        {
            List<SearchTreeEntry> results = new List<SearchTreeEntry>();
            HashSet<string> usedNames = new HashSet<string>();

            var customActionIcon = CyanTriggerImageResources.CyanTriggerCustomActionIcon;
            foreach (var actionInfo in CyanTriggerActionGroupDefinitionUtil.Instance.GetEventInfoHolders())
            {
                string actionName = actionInfo.GetActionRenderingDisplayName();
                if (usedNames.Contains(actionName))
                {
                    continue;
                }
                usedNames.Add(actionName);
                
                results.Add(new SearchTreeEntry(
                        new GUIContent(actionName, customActionIcon, actionInfo.Action.description))
                    {level = 2, userData = actionInfo});
            }

            return results;
        }

        private static (List<CyanTriggerSettingsFavoriteItem>, List<CyanTriggerSettingsFavoriteItem>)
            GetRemainingEvents(IEnumerable<CyanTriggerSettingsFavoriteItem> favoriteEvents)
        {
            List<CyanTriggerSettingsFavoriteItem> vrcEvents = new List<CyanTriggerSettingsFavoriteItem>();
            List<CyanTriggerSettingsFavoriteItem> customEvents = new List<CyanTriggerSettingsFavoriteItem>();

            HashSet<string> vrcDef = new HashSet<string>();
            HashSet<string> customGuid = new HashSet<string>();

            foreach (var favoriteItem in favoriteEvents)
            {
                var data = favoriteItem.data;
                if (!string.IsNullOrEmpty(data.directEvent))
                {
                    vrcDef.Add(data.directEvent);
                }
                if (!string.IsNullOrEmpty(data.guid))
                {
                    customGuid.Add(data.guid);
                }
            }

            foreach (var nodeDefinition in CyanTriggerNodeDefinitionManager.Instance.GetEventDefinitions())
            {
                if (vrcDef.Contains(nodeDefinition.FullName))
                {
                    continue;
                }

                var actionInfo = CyanTriggerActionInfoHolder.GetActionInfoHolder(nodeDefinition);
                vrcEvents.Add(new CyanTriggerSettingsFavoriteItem()
                {
                    item = actionInfo.GetActionRenderingDisplayName(),
                    data = new CyanTriggerActionType {directEvent = nodeDefinition.FullName}
                });
            }
            
            foreach (var actionInfo in CyanTriggerActionGroupDefinitionUtil.Instance.GetEventInfoHolders())
            {
                if (customGuid.Contains(actionInfo.Action.guid))
                {
                    continue;
                }

                customEvents.Add(new CyanTriggerSettingsFavoriteItem()
                {
                    item = actionInfo.GetActionRenderingDisplayName(),
                    data = new CyanTriggerActionType
                    {
                        guid = actionInfo.Action.guid,
                    }
                });
            }

            return (vrcEvents, customEvents);
        }

        public static CyanTriggerSettingsFavoriteItem[] GetAllEventsAsFavorites()
        {
            if (_otherFavoriteEvents != null)
            {
                return _otherFavoriteEvents;
            }

            var favoriteEvents =
                CyanTriggerSettingsFavoriteManager.Instance.FavoriteEvents.favoriteItems;
            
            List<CyanTriggerSettingsFavoriteItem> favoriteItems = new List<CyanTriggerSettingsFavoriteItem>();
            favoriteItems.AddRange(favoriteEvents);

            var (vrcEvents, customEvents) = 
                GetRemainingEvents(favoriteEvents);
                
            favoriteItems.Add(new CyanTriggerSettingsFavoriteItem{item = "Other VRC Events", scopeDelta = 1});
            favoriteItems.AddRange(vrcEvents);
            favoriteItems.Add(new CyanTriggerSettingsFavoriteItem{item = "End Other VRC Events", scopeDelta = -1});
                
            favoriteItems.Add(new CyanTriggerSettingsFavoriteItem{item = "Other Custom Events", scopeDelta = 1});
            favoriteItems.AddRange(customEvents);
            favoriteItems.Add(new CyanTriggerSettingsFavoriteItem{item = "End Other Custom Events", scopeDelta = -1});

            return _otherFavoriteEvents = favoriteItems.ToArray();
        }
    }
}
