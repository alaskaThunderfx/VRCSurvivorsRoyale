using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.Udon;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeInspectorGetProgramVariable : ICyanTriggerCustomNodeInspector
    {
        public string GetNodeDefinitionName()
        {
            return "VRCUdonCommonInterfacesIUdonEventReceiver.__GetProgramVariable__SystemString__SystemObject";
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
            RenderUdonInspector(
                actionInstanceRenderData, variableDefinitions, getVariableOptionsForType, rect, layout, true);
        }
        
        
        public static void RenderUdonInspector(
            CyanTriggerActionInstanceRenderData actionInstanceRenderData,
            CyanTriggerActionVariableDefinition[] variableDefinitions,
            Func<Type, List<CyanTriggerEditorVariableOption>> getVariableOptionsForType,
            Rect rect,
            bool layout,
            bool isGetVariable)
        {
            var actionProperty = actionInstanceRenderData.Property;
            var inputListProperty = actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
            
            Rect inputRect = new Rect(rect);
            
            // Render Udon input editor
            int inputIndex = 0;
            var udonInputVarDef = variableDefinitions[inputIndex];
            SerializedProperty udonInputProperty = inputListProperty.GetArrayElementAtIndex(inputIndex);
            CyanTriggerPropertyEditor.DrawActionVariableInstanceInputEditor(
                actionInstanceRenderData,
                inputIndex,
                udonInputProperty, 
                udonInputVarDef,
                getVariableOptionsForType, 
                ref inputRect,
                layout,
                null);
            
            rect.y += inputRect.height + 5;
            rect.height -= inputRect.height + 5;

            Func<List<(GUIContent, object)>> getOptionsInput = null;
            SerializedProperty variableNameInputProperty = inputListProperty.GetArrayElementAtIndex(1);
            SerializedProperty variableNameIsVariableProperty =
                variableNameInputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
            var variableValueInputVarDef = variableDefinitions[2];
            
            // Go through all Udon input in multi-input to get a list of Variable options to pick from.
            // If variable name option is set to variable, do not check udon for variables.
            if (!variableNameIsVariableProperty.boolValue)
            {
                var udonTarget = CyanTriggerCustomNodeInspectorUtil.GetTypeFromInput<UdonBehaviour>(
                    udonInputProperty,
                    actionInstanceRenderData.DataInstance.variables,
                    actionInstanceRenderData.UdonBehaviour,
                    out bool containsSelf);
                var udonTargets = new List<UdonBehaviour> { udonTarget };

                // Get list of variables from inputs.
                Dictionary<string, Type> variableOptions = CyanTriggerCustomNodeInspectorUtil.GetVariableOptions(
                    actionInstanceRenderData.DataInstance,
                    udonTargets,
                    containsSelf,
                    false);
                
                List<string> variableOptionsSorted = new List<string>(variableOptions.Keys);
                variableOptionsSorted.Sort();
                var optionContent = new List<(GUIContent, object)>();
                foreach (var variable in variableOptionsSorted)
                {
                    optionContent.Add((new GUIContent(variable), variable));
                }
                
                if (variableOptions.Count > 0)
                {
                    getOptionsInput = () => optionContent;
                }
                
                if (isGetVariable)
                {
                    // Find variable name and create new definition using that
                    variableValueInputVarDef = CyanTriggerCustomNodeInspectorUtil.GetUpdatedDefinitionFromSelectedVariable(
                        variableNameInputProperty,
                        variableValueInputVarDef,
                        variableOptions);
                }
            }

            inputRect = new Rect(rect);
            
            // Render variable name options input editor.
            inputIndex = 1;
            var variableNameInputVarDef = variableDefinitions[inputIndex];
            CyanTriggerPropertyEditor.DrawActionVariableInstanceInputEditor(
                actionInstanceRenderData,
                inputIndex,
                variableNameInputProperty, 
                variableNameInputVarDef,
                getVariableOptionsForType, 
                ref inputRect,
                layout,
                getOptionsInput);
            
            rect.y += inputRect.height + 5;
            rect.height -= inputRect.height + 5;
            inputRect = new Rect(rect);
            
            inputIndex = 2;
            SerializedProperty variableValueInputProperty = inputListProperty.GetArrayElementAtIndex(inputIndex);

            CyanTriggerPropertyEditor.DrawActionVariableInstanceInputEditor(
                actionInstanceRenderData,
                inputIndex,
                variableValueInputProperty, 
                variableValueInputVarDef,
                getVariableOptionsForType, 
                ref inputRect,
                layout,
                null);
        }
    }
}