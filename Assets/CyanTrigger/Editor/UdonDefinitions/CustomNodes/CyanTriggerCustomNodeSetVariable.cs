using System;
using System.Collections.Generic;
using UnityEditor;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeSetVariable : 
        CyanTriggerCustomUdonActionNodeDefinition,
        ICyanTriggerCustomNodeCustomVariableInitialization
    {
        private readonly Type _type;
        private readonly UdonNodeDefinition _definition;
        private readonly string _friendlyName;

        public CyanTriggerCustomNodeSetVariable(Type type)
        {
            if (type == typeof(UdonBehaviour))
            {
                type = typeof(IUdonEventReceiver);
            }
            
            _type = type;
            _friendlyName = CyanTriggerNameHelpers.GetTypeFriendlyName(_type);
            string fullName = GetFullnameForType(_type);
            
            _definition = new UdonNodeDefinition(
                $"{_friendlyName} Set",
                fullName,
                _type,
                new []
                {
                    new UdonNodeParameter
                    {
                        name = "input",
                        parameterType = UdonNodeParameter.ParameterType.IN,
                        type = _type
                    },
                    new UdonNodeParameter
                    {
                        name = "output",
                        parameterType = UdonNodeParameter.ParameterType.OUT,
                        type = _type
                    }
                },
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<object>(),
                true
            );
        }
        
        public static string GetFullnameForType(Type type)
        {
            if (type == typeof(UdonBehaviour))
            {
                type = typeof(IUdonEventReceiver);
            }
            
            string fullName = CyanTriggerNameHelpers.SanitizeName(type.FullName);
            if (type.IsArray)
            {
                fullName = $"{fullName}Array";
            }

            return $"{fullName}__.Set__{fullName}__{fullName}";
        }
        
        public override UdonNodeDefinition GetNodeDefinition()
        {
            return _definition;
        }
        
        public override CyanTriggerNodeDefinition.UdonDefinitionType GetDefinitionType()
        {
            return CyanTriggerNodeDefinition.UdonDefinitionType.Method;
        }

        public override string GetDisplayName()
        {
            return "Set";
        }
        
        public override string GetDocumentationLink()
        {
            return CyanTriggerDocumentationLinks.SetVariableNodeDocumentation;
        }

        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            var actionInstance = compileState.ActionInstance;
            var actionMethod = compileState.ActionMethod;
            
            var dataVar =
                compileState.GetDataFromVariableInstance(-1, 0, actionInstance.inputs[0], _type, false);
            var outputVar =
                compileState.GetDataFromVariableInstance(-1, 1, actionInstance.inputs[1], _type, true);
            
            actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.CopyVariables(dataVar, outputVar));
            
            var changedVariables = new List<CyanTriggerAssemblyDataType> { outputVar };
            compileState.CheckVariableChanged(actionMethod, changedVariables);
        }
        
        public void InitializeVariableProperties(
            SerializedProperty inputsProperty, 
            SerializedProperty multiInputsProperty)
        {
            // variable initialized with name
            {
                string displayName =
                    $"{CyanTriggerNameHelpers.SanitizeName(CyanTriggerNameHelpers.GetCamelCase(_friendlyName))}Var";
                
                SerializedProperty inputProperty = inputsProperty.GetArrayElementAtIndex(1);
                SerializedProperty nameDataProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                CyanTriggerSerializableObject.UpdateSerializedProperty(nameDataProperty, displayName);
                
                SerializedProperty idProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
                idProperty.stringValue = Guid.NewGuid().ToString();
                
                SerializedProperty isVariableProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
                isVariableProperty.boolValue = true;
            }

            inputsProperty.serializedObject.ApplyModifiedProperties();
        }
    }
}
