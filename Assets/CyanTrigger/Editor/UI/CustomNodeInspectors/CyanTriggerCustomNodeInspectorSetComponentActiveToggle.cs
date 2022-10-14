using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeInspectorSetComponentActiveToggle : ICyanTriggerCustomNodeInspector
    {
        public string GetNodeDefinitionName()
        {
            return CyanTriggerCustomNodeSetComponentActiveToggle.FullName;
        }
        
        public string GetCustomActionGuid()
        {
            return "";
        }

        public bool HasCustomHeight(CyanTriggerActionInstanceRenderData actionInstanceRenderData)
        {
            return false;
        }

        public float GetHeightForInspector(CyanTriggerActionInstanceRenderData actionInstanceRenderData)
        {
            throw new NotImplementedException();
        }

        public void RenderInspector(
            CyanTriggerActionInstanceRenderData actionInstanceRenderData,
            CyanTriggerActionVariableDefinition[] variableDefinitions, 
            Func<Type, List<CyanTriggerEditorVariableOption>> getVariableOptionsForType, 
            Rect rect, 
            bool layout)
        {
            CyanTriggerCustomNodeInspectorSetComponentActive.RenderInspector(
                actionInstanceRenderData, 
                variableDefinitions, 
                getVariableOptionsForType,
                rect, 
                layout,
                true);
        }
    }
}