
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
#if UNITY_2019_4_OR_NEWER
using UnityEditor.Experimental.GraphView;
#else
using UnityEditor.Experimental.UIElements.GraphView;
#endif

namespace Cyan.CT.Editor
{
    internal abstract class CyanTriggerSearchWindowProvider : ScriptableObject, ISearchWindowProvider
    {
        public abstract List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context);
        public abstract bool OnSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context);

        protected void AddDocumentationRightClickOption(GenericMenu menu, CyanTriggerActionInfoHolder actionInfo)
        {
            (GUIContent content, Action onClick) = CyanTriggerEditorUtils.GetDocumentationAction(actionInfo);
            if (content != null && onClick != null)
            {
                // TODO fix this
                content.text = content.tooltip.Substring(0, content.tooltip.IndexOf(':'));
                menu.AddItem(content, false, () => OnMenuItemSelected(onClick));
            }
        }
        
        protected void AddDocumentationRightClickOption(GenericMenu menu, CyanTriggerActionGroupDefinition customAction)
        {
            string tooltip = $"Open Custom Action Definition";
            GUIContent content = new GUIContent(tooltip, CyanTriggerImageResources.CyanTriggerCustomActionIcon, tooltip);
            menu.AddItem(content, false,
                () => OnMenuItemSelected(() => Selection.SetActiveObjectWithContext(customAction, null)));
        }

        private void OnMenuItemSelected(Action action)
        {
            action?.Invoke();
            CyanTriggerSearchWindow.CloseWindow();
        }
    }
}