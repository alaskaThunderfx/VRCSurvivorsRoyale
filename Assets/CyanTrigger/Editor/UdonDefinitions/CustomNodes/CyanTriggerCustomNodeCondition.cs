using System;
using UnityEditor;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeCondition :
        CyanTriggerCustomUdonActionNodeDefinition,
        ICyanTriggerCustomNodeScope,
        ICyanTriggerCustomNodeDependency,
        ICyanTriggerCustomNodeCustomHash,
        ICyanTriggerCustomNodeCustomVariableInitialization
    {
        public const string FullName = "CyanTriggerSpecial_Condition";
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "Condition",
            FullName,
            typeof(CyanTrigger),
            new []
            {
                new UdonNodeParameter
                {
                    name = "condition",
                    type = typeof(bool),
                    parameterType = UdonNodeParameter.ParameterType.OUT
                }
            },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<object>(),
            true
        );

        public override UdonNodeDefinition GetNodeDefinition()
        {
            return NodeDefinition;
        }

        public override CyanTriggerNodeDefinition.UdonDefinitionType GetDefinitionType()
        {
            return CyanTriggerNodeDefinition.UdonDefinitionType.CyanTriggerSpecial;
        }

        public override string GetDisplayName()
        {
            return NodeDefinition.name;
        }
        
        public override string GetDocumentationLink()
        {
            return CyanTriggerDocumentationLinks.ConditionNodeDocumentation;
        }

        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            var scopeFrame = compileState.ScopeData.ScopeStack.Peek();
            scopeFrame.EndNop = CyanTriggerAssemblyInstruction.Nop();
            
            // If new variable defined, initialize it to false every condition check
            var actionInstance = compileState.ActionInstance;
            if (HasNewVariable(actionInstance))
            {
                var actionMethod = compileState.ActionMethod;
                var program = compileState.Program;
                
                var userVariable = compileState.GetDataFromVariableInstance(-1, 0, actionInstance.inputs[0], typeof(bool), false);
                var constFalse = program.Data.GetOrCreateVariableConstant(typeof(bool), false);
                actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.CopyVariables(constFalse, userVariable));
            }
        }
        
        public void HandleEndScope(CyanTriggerCompileState compileState)
        {
            var actionInstance = compileState.ActionInstance;
            var actionMethod = compileState.ActionMethod;

            var userVariable = compileState.GetDataFromVariableInstance(-1, 0, actionInstance.inputs[0], typeof(bool), false);
                        
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(userVariable));
            
            // Push jump point for Pass or Fail checks. 
            var scopeFrame = compileState.ScopeData.ScopeStack.Peek();
            actionMethod.AddAction(scopeFrame.EndNop);
        }
        
        public UdonNodeDefinition[] GetDependentNodes()
        {
            return new[] {CyanTriggerCustomNodeBlockEnd.NodeDefinition};
        }

        public void InitializeVariableProperties(
            SerializedProperty inputsProperty, 
            SerializedProperty multiInputsProperty)
        {
            // condition variable initialized with name
            {
                SerializedProperty inputProperty = inputsProperty.GetArrayElementAtIndex(0);
                SerializedProperty nameDataProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                CyanTriggerSerializableObject.UpdateSerializedProperty(nameDataProperty, "condition_bool");
                
                SerializedProperty idProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
                idProperty.stringValue = Guid.NewGuid().ToString();
                
                SerializedProperty isVariableProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
                isVariableProperty.boolValue = true;
            }

            inputsProperty.serializedObject.ApplyModifiedProperties();
        }

        private bool HasNewVariable(CyanTriggerActionInstance actionInstance)
        {
            var firstInput = actionInstance.inputs[0];
            return string.IsNullOrEmpty(firstInput.name) && !string.IsNullOrEmpty(firstInput.variableID);
        }
        
        public string GetCustomHash(CyanTriggerActionInstance actionInstance)
        {
            return HasNewVariable(actionInstance) ? "Condition new var" : "Condition reuse var";
        }
    }
}