using System;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeFailIfFalse :
        CyanTriggerCustomUdonActionNodeDefinition, 
        ICyanTriggerCustomNodeCustomVariableSettings
    {
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "ConditionFailIfFalse",
            "CyanTriggerSpecial_ConditionFailIfFalse",
            typeof(CyanTrigger),
            new []
            {
                new UdonNodeParameter
                {
                    name = "bool",
                    type = typeof(bool),
                    parameterType = UdonNodeParameter.ParameterType.IN,
                }
            },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<object>(),
            true
        );

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
            return CyanTriggerDocumentationLinks.FailIfFalseNodeDocumentation;
        }
        
        public static readonly CyanTriggerActionVariableDefinition[] VariableDefinitions =
        {
            new CyanTriggerActionVariableDefinition
            {
                type = new CyanTriggerSerializableType(typeof(bool)),
                udonName = "bool",
                displayName = "Should fail", 
                description = "If the input provided is false, then the entire condition will fail, skipping the rest of the actions in the Condition and skipping the ConditionBody.",
                variableType = CyanTriggerActionVariableTypeDefinition.VariableInput
            },
        };

        public override UdonNodeDefinition GetNodeDefinition()
        {
            return NodeDefinition;
        }
        
        public CyanTriggerActionVariableDefinition[] GetCustomVariableSettings()
        {
            return VariableDefinitions;
        }

        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            var actionInstance = compileState.ActionInstance;
            var actionMethod = compileState.ActionMethod;
            var program = compileState.Program;
            var scopeData = compileState.ScopeData;

            foreach (var scopeFrame in scopeData.ScopeStack)
            {
                if (scopeFrame.Definition is CyanTriggerCustomNodeCondition)
                {
                    var variable = actionInstance.inputs[0];
                    
                    // Push constant false for if the jump is successful.
                    actionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(
                        program.Data.GetOrCreateVariableConstant(typeof(bool), false)));
                    
                    // Check if the value was false and we should jump to the end of the condition
                    actionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(
                        compileState.GetDataFromVariableInstance(-1, 0, variable, typeof(bool), false)));
                    actionMethod.AddAction(CyanTriggerAssemblyInstruction.JumpIfFalse(scopeFrame.EndNop));
                    
                    // Pop off the constant false since we did not jump.
                    actionMethod.AddAction(CyanTriggerAssemblyInstruction.Pop());
                    return;
                }
            }
            
            compileState.LogError("FailIfFalse statement not included in a condition!");
        }
    }
}
