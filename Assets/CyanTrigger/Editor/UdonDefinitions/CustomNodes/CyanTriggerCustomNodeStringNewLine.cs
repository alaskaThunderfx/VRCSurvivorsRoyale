using System;
using System.Collections.Generic;
using UnityEditor;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeStringNewLine : 
        CyanTriggerCustomUdonActionNodeDefinition, 
        ICyanTriggerCustomNodeCustomVariableInitialization
    {
        public const string FullName = "SystemString.__get_newLine__SystemString";
        
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "string Get newLine",
            FullName,
            typeof(string),
            new[]
            {
                new UdonNodeParameter()
                {
                    name = "", 
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
            return "Get newLine";
        }
        
        public override string GetDocumentationLink()
        {
            return CyanTriggerDocumentationLinks.StringNewLineNodeDocumentation;
        }

        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            var newLineVariable = compileState.Program.Data.GetOrCreateVariableConstant(typeof(string), "\n");
            var variable = compileState.ActionInstance.inputs[0];
            var stringObj = compileState.GetDataFromVariableInstance(-1, 0, variable, typeof(string), true);
            
            compileState.ActionMethod.AddActions(
                CyanTriggerAssemblyActionsUtils.CopyVariables(newLineVariable, stringObj));
            
            var changedVariables = new List<CyanTriggerAssemblyDataType> { stringObj };
            compileState.CheckVariableChanged(compileState.ActionMethod, changedVariables);
        }
        
        public void InitializeVariableProperties(
            SerializedProperty inputsProperty, 
            SerializedProperty multiInputsProperty)
        {
            // type variable initialized with name
            {
                SerializedProperty inputProperty = inputsProperty.GetArrayElementAtIndex(0);
                SerializedProperty nameDataProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                CyanTriggerSerializableObject.UpdateSerializedProperty(nameDataProperty, "newLine");
                
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