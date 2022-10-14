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
    internal class CyanTriggerFocusedSearchWindow : CyanTriggerSearchWindowProvider
    {
        // ReSharper disable once InconsistentNaming
        public string WindowTitle;
        public List<CyanTriggerActionInfoHolder> FocusedNodeDefinitions;
        public Action<CyanTriggerActionInfoHolder> OnDefinitionSelected;
        public Func<CyanTriggerActionInfoHolder, string> GetDisplayString;
        

        #region ISearchWindowProvider

        public override List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            List<SearchTreeEntry> nodeEntries = new List<SearchTreeEntry>();

            Texture2D udonTypeIcon = CyanTriggerImageResources.ScriptIcon;
            Texture2D customTypeIcon = CyanTriggerImageResources.CyanTriggerCustomActionIcon;

            nodeEntries.Add(new SearchTreeGroupEntry(new GUIContent($"{WindowTitle} Search"), 0));

            HashSet<string> usedNames = new HashSet<string>();
            foreach (var infoHolder in FocusedNodeDefinitions)
            {
                string infoName = GetDisplayString(infoHolder);
                if (usedNames.Contains(infoName))
                {
                    continue;
                }
                usedNames.Add(infoName);

                var icon = infoHolder.IsDefinition() ? udonTypeIcon : customTypeIcon;
                nodeEntries.Add(new SearchTreeEntry(new GUIContent(infoName, icon)) {level = 1, userData = infoHolder});    
            }

            return nodeEntries;
        }

        public override bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            if (entry.userData is CyanTriggerActionInfoHolder actionInfoHolder && OnDefinitionSelected != null)
            {
                if (CyanTriggerSearchWindow.WasEventRightClick)
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Add Item"), false, () =>
                    {
                        OnDefinitionSelected.Invoke(actionInfoHolder);
                    });
                    AddDocumentationRightClickOption(menu, actionInfoHolder);
                
                    menu.ShowAsContext();
                    return false;
                }
                
                // Debug to make setting up favorites easier...
                // foreach (var v in FocusedNodeDefinitions)
                // {
                //     OnDefinitionSelected.Invoke(v);
                // }
                OnDefinitionSelected.Invoke(actionInfoHolder);
                return true;
            }

            return false;
        }
        
        #endregion

        public void ResetDisplayMethod()
        {
            GetDisplayString = UseDisplayName;
        }

        public string UseDisplayName(CyanTriggerActionInfoHolder infoHolder)
        {
            return infoHolder.GetDisplayName();
        }
    }
}