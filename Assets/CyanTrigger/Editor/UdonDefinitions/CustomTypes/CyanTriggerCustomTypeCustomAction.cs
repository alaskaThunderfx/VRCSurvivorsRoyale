using System;
using Cyan.CT.Editor;
using UnityEngine;

namespace Cyan.CT
{
    [Serializable]
    public class CyanTriggerCustomTypeCustomAction : 
        ICyanTriggerCustomType,
        ICyanTriggerCustomTypeNoValueEditor
    {
        [SerializeField]
        private CyanTriggerActionGroupDefinition actionGroup;
        public CyanTriggerActionGroupDefinition ActionGroup => actionGroup;

        public CyanTriggerCustomTypeCustomAction(CyanTriggerActionGroupDefinition actionGroup)
        {
            this.actionGroup = actionGroup;
        }
        
        public Type GetBaseType()
        {
            return typeof(CyanTriggerActionGroupDefinition);
        }

        public string GetTypeDisplayName()
        {
            if (actionGroup)
            {
                string nameSpace = $"{actionGroup.GetNamespace()}*";
                if (!actionGroup.isMultiInstance)
                {
                    return $"Invalid ({nameSpace})";
                }
                return nameSpace;
            }
            return "Invalid";
        }

        // No data needs to be changed here as this isn't referencing scene objects.
        public ICyanTriggerCustomType Clone()
        {
            return this;
        }

        public GUIContent GetDocumentationContent()
        {
            return CyanTriggerEditorUtils.GetDocumentationAction((CyanTriggerActionGroupDefinitionUdonAsset)actionGroup).Item1;
        }

        public Action GetDocumentationAction()
        {
            return CyanTriggerEditorUtils.GetDocumentationAction((CyanTriggerActionGroupDefinitionUdonAsset)actionGroup).Item2;
        }
    }
}