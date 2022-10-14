using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeInspectorSendCustomEventDelayedSeconds : ICyanTriggerCustomNodeInspector
    {
        public string GetNodeDefinitionName()
        {
            return "VRCUdonCommonInterfacesIUdonEventReceiver.__SendCustomEventDelayedSeconds__SystemString_SystemSingle_VRCUdonCommonEnumsEventTiming__SystemVoid";
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
            CyanTriggerCustomNodeInspectorSendCustomEventUdon.RenderInspector(
                actionInstanceRenderData, 
                variableDefinitions, 
                getVariableOptionsForType, 
                rect, 
                layout, 
                1,
                false,
                false);
        }
    }
}