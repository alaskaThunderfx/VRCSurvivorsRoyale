using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cyan.CT.Editor
{
    public interface ICyanTriggerCustomNodeInspector
    {
        string GetNodeDefinitionName();
        string GetCustomActionGuid();
        bool HasCustomHeight(CyanTriggerActionInstanceRenderData actionInstanceRenderData);
        float GetHeightForInspector(CyanTriggerActionInstanceRenderData actionInstanceRenderData);
        void RenderInspector(
            CyanTriggerActionInstanceRenderData actionInstanceRenderData,
            CyanTriggerActionVariableDefinition[] variableDefinitions,
            Func<Type, List<CyanTriggerEditorVariableOption>> getVariableOptionsForType,
            Rect rect,
            bool layout);
    }
}