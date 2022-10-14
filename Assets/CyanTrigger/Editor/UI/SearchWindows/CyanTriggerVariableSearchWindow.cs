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
    internal class CyanTriggerVariableSearchWindow : CyanTriggerSearchWindowProvider
    {
        private static List<SearchTreeEntry> _registryFullCache;
        private static List<SearchTreeEntry> _registryDefaultCache;
        private static List<SearchTreeEntry> _defaultTypesCache;
        private static List<SearchTreeEntry> _customTypesCache;

        public Action<UdonNodeDefinition> OnDefinitionSelected;
        public Action<CyanTriggerActionGroupDefinition> OnCustomActionSelected;
        public bool allowCustomTypes;
        
        public static void ResetCache()
        {
            _registryFullCache?.Clear();
            _customTypesCache?.Clear();
        }
        
        #region ISearchWindowProvider
        
        public override bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            if (entry.userData is CyanTriggerActionInfoHolder actionInfoHolder && OnDefinitionSelected != null)
            {
                if (CyanTriggerSearchWindow.WasEventRightClick)
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Add Variable"), false, () =>
                    {
                        OnDefinitionSelected.Invoke(actionInfoHolder.Definition.Definition);
                    });
                    AddDocumentationRightClickOption(menu, actionInfoHolder);
                    
                    menu.ShowAsContext();
                    return false;
                }
                OnDefinitionSelected.Invoke(actionInfoHolder.Definition.Definition);
                return true;
            }
            if (allowCustomTypes && entry.userData is CyanTriggerActionGroupDefinition actionGroup && OnCustomActionSelected != null)
            {
                if (CyanTriggerSearchWindow.WasEventRightClick)
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Add Variable"), false, () =>
                    {
                        OnCustomActionSelected.Invoke(actionGroup);
                    });
                    AddDocumentationRightClickOption(menu, actionGroup);
                
                    menu.ShowAsContext();
                    return false;
                }
                OnCustomActionSelected.Invoke(actionGroup);
                return true;
            }

            return false;
        }
        
        public override List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            return allowCustomTypes ? CreateFullSearchTree() : CreateDefaultSearchTree();
        }

        #endregion

        private List<SearchTreeEntry> CreateFullSearchTree()
        {
            if (_registryFullCache != null && _registryFullCache.Count > 0)
            {
                return _registryFullCache;
            }

            // Initialize caches. This also prevents issues where Caches are cleared while creating them.
            GetCustomTypes();
            GetDefaultTypes();
            
            _registryFullCache = new List<SearchTreeEntry>();
            _registryFullCache.Add(new SearchTreeGroupEntry(new GUIContent("Variable Search"), 0));

            _registryFullCache.AddRange(GetCustomTypes());
            _registryFullCache.AddRange(GetDefaultTypes());
            
            return _registryFullCache;
        }
        
        private List<SearchTreeEntry> CreateDefaultSearchTree()
        {
            if (_registryDefaultCache != null && _registryDefaultCache.Count > 0)
            {
                return _registryDefaultCache;
            }
            
            _registryDefaultCache = new List<SearchTreeEntry>();
            _registryDefaultCache.Add(new SearchTreeGroupEntry(new GUIContent("Variable Search"), 0));

            _registryDefaultCache.AddRange(GetDefaultTypes());
            
            return _registryDefaultCache;
        }

        private List<SearchTreeEntry> GetDefaultTypes()
        {
            if (_defaultTypesCache != null)
            {
                return _defaultTypesCache;
            }

            Texture2D udonTypeIcon = CyanTriggerImageResources.ScriptIcon;

            _defaultTypesCache = new List<SearchTreeEntry>();
            
            List<CyanTriggerNodeDefinition> definitions = 
                new List<CyanTriggerNodeDefinition>(CyanTriggerNodeDefinitionManager.Instance.GetVariableDefinitions());
            
            // Sort so System variables are always first, everything else is alphabetical
            // TODO move to a generic place?
            definitions.Sort((d1, d2) =>
            {
                bool h1System = d1.FullName.StartsWith("CyanTriggerVariable_System");
                bool h2System = d2.FullName.StartsWith("CyanTriggerVariable_System");
                if (h1System == h2System)
                {
                    return String.Compare(d1.FullName, d2.FullName, StringComparison.Ordinal);
                }

                return (!h1System).CompareTo(!h2System);
            });
            
            foreach (var nodeDefinition in definitions)
            {
                _defaultTypesCache.Add(new SearchTreeEntry(new GUIContent(nodeDefinition.TypeFriendlyName, udonTypeIcon))
                    {level = 1, userData = CyanTriggerActionInfoHolder.GetActionInfoHolder(nodeDefinition)});
            }
            
            return _defaultTypesCache;
        }

        private List<SearchTreeEntry> GetCustomTypes()
        {
            if (_customTypesCache != null)
            {
                return _customTypesCache;
            }

            // Needs to be declared first as this will also clear the cache.
            var instance = CyanTriggerActionGroupDefinitionUtil.Instance;

            Texture2D customTypeIcon = CyanTriggerImageResources.CyanTriggerCustomActionIcon;

            _customTypesCache = new List<SearchTreeEntry>();
            _customTypesCache.Add(new SearchTreeGroupEntry(new GUIContent("Custom Types", "Types created through Custom Actions"), 1));
            
            List<CyanTriggerActionGroupDefinition> groups = 
                new List<CyanTriggerActionGroupDefinition>(instance.GetInstancedCustomActions());
            groups.Sort(CustomActionComparer);
            
            foreach (var actionGroup in groups)
            {
                _customTypesCache.Add(new SearchTreeEntry(new GUIContent($"{actionGroup.GetNamespace()} (Custom)", customTypeIcon))
                    {level = 2, userData = actionGroup});
            }
            
            return _customTypesCache;
        }

        private static int CustomActionComparer(
            CyanTriggerActionGroupDefinition actionGroup1, 
            CyanTriggerActionGroupDefinition actionGroup2)
        {
            return string.Compare(actionGroup1.GetNamespace(), actionGroup2.GetNamespace(), StringComparison.Ordinal);
        }
    }
}
