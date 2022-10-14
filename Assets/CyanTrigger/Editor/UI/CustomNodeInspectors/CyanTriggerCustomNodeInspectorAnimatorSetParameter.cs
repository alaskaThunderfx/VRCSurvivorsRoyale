using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeInspectorAnimatorSetParameter : ICyanTriggerCustomNodeInspector
    {
        private static readonly Dictionary<PrimitiveOperation, string[]> OpToGuid = new Dictionary<PrimitiveOperation, string[]>()
        {
            {PrimitiveOperation.UnaryNegation , new [] {"4d341da3-b2f3-4607-8c27-5426906fc8f5", "", ""}},
            {PrimitiveOperation.Addition , new [] {"", "2f608dd1-b833-467c-8208-404c7486ce8c", "98cea39e-2b2b-4380-b471-ae01be1e9465"}},
            {PrimitiveOperation.Subtraction , new [] {"", "6fa78d94-7239-4fec-8473-2dc8564473a8", "2c2793e2-9798-453f-803f-c4cfde2659ac"}},
            {PrimitiveOperation.Multiplication , new [] {"", "e41b0b6a-0382-480b-ac67-33042a54b709", "fc11ae15-ef01-41fa-a722-8408e8a9b242"}},
            {PrimitiveOperation.Division , new [] {"", "01aedc29-ba91-4a89-8fa3-368a90f1101e", "a80a6a62-0fbd-4626-a364-325d9560bb3f"}},
            {PrimitiveOperation.Remainder , new [] {"", "3c3fd026-1e9c-4930-b8ec-92b3775739f7", "3ac46420-641b-46f6-a6e1-9c9700cc5cbd"}}
        };
        
        private readonly Type _type;
        private readonly PrimitiveOperation _operation;
        private readonly bool _isTrigger = false;
        private readonly bool _isSetTrigger = false;
        private readonly bool _noInputs = false;

        public CyanTriggerCustomNodeInspectorAnimatorSetParameter(Type type, PrimitiveOperation operation)
        {
            _type = type;
            _operation = operation;

            _noInputs = _type == typeof(bool) && _operation == PrimitiveOperation.UnaryNegation;
        }
        
        public CyanTriggerCustomNodeInspectorAnimatorSetParameter(bool isSetTrigger)
        {
            _type = typeof(bool);
            _operation = PrimitiveOperation.None;

            _isTrigger = true;
            _isSetTrigger = isSetTrigger;
            _noInputs = true;
        }
        
        public string GetNodeDefinitionName()
        {
            if (_operation != PrimitiveOperation.None)
            {
                return "";
            }

            if (_isTrigger)
            {
                if (_isSetTrigger)
                {
                    return "UnityEngineAnimator.__SetTrigger__SystemString__SystemVoid";
                }
                return "UnityEngineAnimator.__ResetTrigger__SystemString__SystemVoid";
            }

            if (_type == typeof(bool))
            {
                return "UnityEngineAnimator.__SetBool__SystemString_SystemBoolean__SystemVoid";
            }

            if (_type == typeof(int))
            {
                return "UnityEngineAnimator.__SetInteger__SystemString_SystemInt32__SystemVoid";
            }

            if (_type == typeof(float))
            {
                return "UnityEngineAnimator.__SetFloat__SystemString_SystemSingle__SystemVoid";
            }
            
            return "";
        }

        public string GetCustomActionGuid()
        {
            if (_operation == PrimitiveOperation.None || _isTrigger)
            {
                return "";
            }

            int index = -1;
            if (_type == typeof(bool))
            {
                index = 0;
            }
            else if (_type == typeof(int))
            {
                index = 1;
            }
            else if (_type == typeof(float))
            {
                index = 2;
            }

            if (index == -1)
            {
                return "";
            }

            if (OpToGuid.TryGetValue(_operation, out var guids))
            {
                return guids[index];
            }

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
            var actionProperty = actionInstanceRenderData.Property;
            var inputListProperty = actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
            
            var multiVarDef = variableDefinitions[0];
            var multiInputListProperty = 
                actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.multiInput));
            
            Debug.Assert((multiVarDef.variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0,
                "Animator Inspector does not properly support multiple variables!");
            
            Rect inputRect = new Rect(rect);
            
            // Render Animator multi-input editor
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
            SerializedProperty parameterNameInputProperty = inputListProperty.GetArrayElementAtIndex(1);
            SerializedProperty parameterIsVariableProperty =
                parameterNameInputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));

            // Go through all Animator input in multi-input to get a list of parameter options to pick from.
            if (!parameterIsVariableProperty.boolValue)
            {
                var animators = CyanTriggerCustomNodeInspectorUtil.GetTypeFromMultiInput<Animator>(
                    multiInputListProperty,
                    actionInstanceRenderData.DataInstance.variables,
                    actionInstanceRenderData.UdonBehaviour,
                    out bool containsSelf);

                // This shouldn't happen since there is no "this animator" parameter.
                // if (containsSelf && actionInstanceRenderData.UdonBehaviour != null)
                // {
                //     animators.Add(actionInstanceRenderData.UdonBehaviour.GetComponent<Animator>());
                // }

                var parameters = CyanTriggerCustomNodeInspectorUtil.GetAnimatorParameterOptions(animators, _type);
                if (parameters.Count > 0)
                {
                    List<string> optionsSorted = new List<string>();
                    foreach (var parameter in parameters)
                    {
                        optionsSorted.Add(parameter.name);
                    }
                    optionsSorted.Sort();
                    var optionContent = new List<(GUIContent, object)>();
                    foreach (var variable in optionsSorted)
                    {
                        optionContent.Add((new GUIContent(variable), variable));
                    }
                    getOptionsInput = () => optionContent;
                }
            }

            inputRect = new Rect(rect);
            
            // Render parameter options input editor.
            int inputIndex = 1;
            CyanTriggerPropertyEditor.DrawActionVariableInstanceInputEditor(
                actionInstanceRenderData,
                inputIndex,
                parameterNameInputProperty, 
                variableDefinitions[inputIndex],
                getVariableOptionsForType, 
                ref inputRect,
                layout,
                getOptionsInput);
            
            
            if (_noInputs)
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