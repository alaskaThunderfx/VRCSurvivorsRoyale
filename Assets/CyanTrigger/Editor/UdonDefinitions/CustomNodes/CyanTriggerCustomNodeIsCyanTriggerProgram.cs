using System;
using System.Collections.Generic;
using UnityEditor;
using VRC.Udon;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeIsCyanTriggerProgram : 
        CyanTriggerCustomUdonActionNodeDefinition, 
        ICyanTriggerCustomNodeCustomVariableInitialization
    {
        public const string FullName = "VRCUdonCommonInterfacesIUdonEventReceiver.__IsCyanTriggerProgram__SystemBoolean";
        
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "UdonBehaviour IsCyanTriggerProgram",
            FullName,
            typeof(UdonBehaviour),
            new[]
            {
                new UdonNodeParameter
                {
                    name = "instance", 
                    type = typeof(UdonBehaviour),
                    parameterType = UdonNodeParameter.ParameterType.IN
                },
                new UdonNodeParameter
                {
                    name = "Is CyanTrigger", 
                    type = typeof(bool),
                    parameterType = UdonNodeParameter.ParameterType.OUT
                },
            },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<object>(),
            false
        );
        
        public override UdonNodeDefinition GetNodeDefinition()
        {
            return NodeDefinition;
        }

        public override CyanTriggerNodeDefinition.UdonDefinitionType GetDefinitionType()
        {
            return CyanTriggerNodeDefinition.UdonDefinitionType.Method;
        }

        public override string GetDisplayName()
        {
            return "IsCyanTriggerProgram";
        }
        
        public override string GetDocumentationLink()
        {
            return CyanTriggerDocumentationLinks.IsCyanTriggerProgramNodeDocumentation;
        }

        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            string programNameVariableName = 
                CyanTriggerAssemblyData.GetSpecialVariableName(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName
                    .ProgramName);
            var programNameVariable = 
                compileState.Program.Data.GetOrCreateVariableConstant(typeof(string), programNameVariableName);
            
            var udonVariable = 
                compileState.GetDataFromVariableInstance(-1, 0, compileState.ActionInstance.inputs[0], typeof(UdonBehaviour), false);
            var outputVariable = 
                compileState.GetDataFromVariableInstance(-1, 1, compileState.ActionInstance.inputs[1], typeof(bool), true);

            compileState.ActionMethod.AddActions(CyanTriggerAssemblyActionsUtils.UdonHasNamedVariable(
                compileState.Program.Data,
                udonVariable,
                programNameVariable,
                outputVariable));

            var changedVariables = new List<CyanTriggerAssemblyDataType> { outputVariable };
            compileState.CheckVariableChanged(compileState.ActionMethod, changedVariables);
        }

        public void InitializeVariableProperties(
            SerializedProperty inputsProperty, 
            SerializedProperty multiInputsProperty)
        {
            // type variable initialized with name
            {
                SerializedProperty inputProperty = inputsProperty.GetArrayElementAtIndex(1);
                SerializedProperty nameDataProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                CyanTriggerSerializableObject.UpdateSerializedProperty(nameDataProperty, "isCyanTrigger");
                
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