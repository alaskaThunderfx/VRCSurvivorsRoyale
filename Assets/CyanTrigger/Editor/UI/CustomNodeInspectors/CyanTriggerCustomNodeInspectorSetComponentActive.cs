using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeInspectorSetComponentActive : ICyanTriggerCustomNodeInspector
    {
        public string GetNodeDefinitionName()
        {
            return CyanTriggerCustomNodeSetComponentActive.FullName;
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
            RenderInspector(
                actionInstanceRenderData, 
                variableDefinitions, 
                getVariableOptionsForType,
                rect, 
                layout,
                false);
        }
        
        public static void RenderInspector(
            CyanTriggerActionInstanceRenderData actionInstanceRenderData,
            CyanTriggerActionVariableDefinition[] variableDefinitions, 
            Func<Type, List<CyanTriggerEditorVariableOption>> getVariableOptionsForType, 
            Rect rect, 
            bool layout,
            bool isToggle)
        {
            var actionProperty = actionInstanceRenderData.Property;
            var inputListProperty = actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
            
            var multiVarDef = variableDefinitions[0];
            var multiInputListProperty = 
                actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.multiInput));
                    
            Rect inputRect = new Rect(rect);
            
            // Render GameObject multi-input editor
            CyanTriggerPropertyEditor.DrawActionVariableInstanceMultiInputEditor(
                actionInstanceRenderData,
                0,
                multiInputListProperty, 
                multiVarDef,
                getVariableOptionsForType, 
                ref inputRect,
                layout);
            
            rect.y += inputRect.height + 5;
            rect.height -= inputRect.height + 5;
            
            
            Func<List<(GUIContent, object)>> getOptionsInput = null;
            SerializedProperty componentNameInputProperty = inputListProperty.GetArrayElementAtIndex(1);
            
            // Go through all GameObject input in multi-input to get a list of component options to pick from.
            var gameObjects = CyanTriggerCustomNodeInspectorUtil.GetTypeFromMultiInput<GameObject>(
                multiInputListProperty,
                actionInstanceRenderData.DataInstance.variables,
                actionInstanceRenderData.UdonBehaviour,
                out bool containsSelf);

            if (containsSelf && actionInstanceRenderData.UdonBehaviour != null)
            {
                gameObjects.Add(actionInstanceRenderData.UdonBehaviour.gameObject);
            }

            List<Type> components = CyanTriggerCustomNodeInspectorUtil.GetComponentOptions(gameObjects);
            if (components.Count > 0)
            {
                List<string> componentOptionsSorted = new List<string>();
                foreach (var component in components)
                {
                    componentOptionsSorted.Add(component.Name);
                }
                componentOptionsSorted.Sort();
                var optionContent = new List<(GUIContent, object)>();
                foreach (var variable in componentOptionsSorted)
                {
                    optionContent.Add((new GUIContent(variable), variable));
                }
                getOptionsInput = () => optionContent;
            }

            inputRect = new Rect(rect);
            
            // Render component options input editor.
            int inputIndex = 1;
            var componentNameInputVarDef = variableDefinitions[inputIndex];
            CyanTriggerPropertyEditor.DrawActionVariableInstanceInputEditor(
                actionInstanceRenderData,
                inputIndex,
                componentNameInputProperty, 
                componentNameInputVarDef,
                getVariableOptionsForType, 
                ref inputRect,
                layout,
                getOptionsInput);
            
            // Toggle does not need to render the enabled bool value input
            if (isToggle)
            {
                return;
            }
            
            rect.y += inputRect.height + 5;
            rect.height -= inputRect.height + 5;
            inputRect = new Rect(rect);

            inputIndex = 2;
            CyanTriggerPropertyEditor.DrawActionVariableInstanceInputEditor(
                actionInstanceRenderData,
                inputIndex,
                inputListProperty.GetArrayElementAtIndex(inputIndex), 
                variableDefinitions[inputIndex],
                getVariableOptionsForType, 
                ref inputRect,
                layout,
                null);
        }
    }
}