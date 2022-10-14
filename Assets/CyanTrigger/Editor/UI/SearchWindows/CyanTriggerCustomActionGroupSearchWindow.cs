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
    // Currently only displays Multi-Instance Custom Action groups
    internal class CyanTriggerCustomActionGroupSearchWindow : CyanTriggerSearchWindowProvider
    {
        private static List<SearchTreeEntry> _registryCache;
        
        public Action<CyanTriggerActionGroupDefinition> OnDefinitionSelected;
        
        public static void ResetCache()
        {
            _registryCache = null;
        }
        
        #region ISearchWindowProvider

        public override bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            if (entry.userData is CyanTriggerActionGroupDefinition customAction && OnDefinitionSelected != null)
            {
                if (CyanTriggerSearchWindow.WasEventRightClick)
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Add Custom Action"), false, () =>
                    {
                        OnDefinitionSelected.Invoke(customAction);
                    });
                    AddDocumentationRightClickOption(menu, customAction);
                
                    menu.ShowAsContext();
                    return false;
                }
                OnDefinitionSelected.Invoke(customAction);
                return true;
            }

            return false;
        }

        public override List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            if (_registryCache != null && _registryCache.Count > 0)
            {
                return _registryCache;
            }

            // Ensuring that items are initialized before creating the list. 
            // Initialization this will clear the cache.
            var instance = CyanTriggerActionGroupDefinitionUtil.Instance;
            
            _registryCache = new List<SearchTreeEntry>();
            
            _registryCache.Add(new SearchTreeGroupEntry(new GUIContent("Custom Actions Search"), 0));

            Texture2D customTypeIcon = CyanTriggerImageResources.CyanTriggerCustomActionIcon;

            foreach (var actionGroup in instance.GetInstancedCustomActions())
            {
                _registryCache.Add(new SearchTreeEntry(new GUIContent(actionGroup.GetNamespace(), customTypeIcon))
                    {level = 1, userData = actionGroup});
            }

            return _registryCache;
        }
        
        #endregion
    }
}