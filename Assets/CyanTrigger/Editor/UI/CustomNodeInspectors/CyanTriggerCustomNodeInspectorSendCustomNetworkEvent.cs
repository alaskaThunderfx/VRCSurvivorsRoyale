using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeInspectorSendCustomNetworkEvent : ICyanTriggerCustomNodeInspector
    {
        public string GetNodeDefinitionName()
        {
            return "VRCUdonCommonInterfacesIUdonEventReceiver.__SendCustomNetworkEvent__VRCUdonCommonInterfacesNetworkEventTarget_SystemString__SystemVoid";
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
                2,
                false,
                true);
        }
    }
}