using System;
using System.Collections.Generic;
using UnityEditor;
using VRC.Udon;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeGetCyanTriggerProgramName : 
        CyanTriggerCustomUdonActionNodeDefinition, 
        ICyanTriggerCustomNodeCustomVariableInitialization
    {
        public const string FullName = "VRCUdonCommonInterfacesIUdonEventReceiver.__GetCyanTriggerProgramName__SystemString";
        
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "UdonBehaviour GetCyanTriggerProgramName",
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
                    name = "Program Name", 
                    type = typeof(string),
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
            return "GetCyanTriggerProgramName";
        }
        
        public override string GetDocumentationLink()
        {
            return CyanTriggerDocumentationLinks.GetCyanTriggerProgramNameNodeDocumentation;
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
                compileState.GetDataFromVariableInstance(-1, 1, compileState.ActionInstance.inputs[1], typeof(string), true);

            
            var isCyanTriggerBoolVariable = compileState.Program.Data.RequestTempVariable(typeof(bool));

            compileState.ActionMethod.AddActions(CyanTriggerAssemblyActionsUtils.UdonHasNamedVariable(
                compileState.Program.Data,
                udonVariable,
                programNameVariable,
                isCyanTriggerBoolVariable));
            
            // If isCyanTriggerBoolVariable
            //   get program variable
            // else
            //   set output to null
            var elseNop = CyanTriggerAssemblyInstruction.Nop();
            var endNop = CyanTriggerAssemblyInstruction.Nop();
            
            compileState.ActionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(isCyanTriggerBoolVariable));
            compileState.ActionMethod.AddAction(CyanTriggerAssemblyInstruction.JumpIfFalse(elseNop));
            
            compileState.ActionMethod.AddActions(
                CyanTriggerAssemblyActionsUtils.GetProgramVariable(programNameVariable, outputVariable, udonVariable));
            compileState.ActionMethod.AddAction(CyanTriggerAssemblyInstruction.Jump(endNop));
            
            compileState.ActionMethod.AddAction(elseNop);

            var nullConst = compileState.Program.Data.GetThisConst(typeof(object));
            compileState.ActionMethod.AddActions(
                CyanTriggerAssemblyActionsUtils.CopyVariables(nullConst, outputVariable));
            
            compileState.ActionMethod.AddAction(endNop);
            
            
            compileState.Program.Data.ReleaseTempVariable(isCyanTriggerBoolVariable);
            
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
                CyanTriggerSerializableObject.UpdateSerializedProperty(nameDataProperty, "programName");
                
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