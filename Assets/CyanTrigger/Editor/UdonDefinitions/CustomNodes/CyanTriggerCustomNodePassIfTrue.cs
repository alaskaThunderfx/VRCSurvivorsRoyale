using System;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodePassIfTrue :
        CyanTriggerCustomUdonActionNodeDefinition, 
        ICyanTriggerCustomNodeCustomVariableSettings
    {
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "ConditionPassIfTrue",
            "CyanTriggerSpecial_ConditionPassIfTrue",
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
            return CyanTriggerDocumentationLinks.PassIfTrueNodeDocumentation;
        }
        
        public static readonly CyanTriggerActionVariableDefinition[] VariableDefinitions =
        {
            new CyanTriggerActionVariableDefinition
            {
                type = new CyanTriggerSerializableType(typeof(bool)),
                udonName = "bool",
                displayName = "Should pass", 
                description = "If the input provided is true, then the entire condition will pass, skipping the rest of the actions in the Condition.",
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
                    CyanTriggerAssemblyDataType tempBool = program.Data.RequestTempVariable(typeof(bool));
                    CyanTriggerAssemblyInstruction pushTempBool = CyanTriggerAssemblyInstruction.PushVariable(tempBool);
                    
                    // Get the variable and invert it for jump if false
                    actionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(
                        compileState.GetDataFromVariableInstance(-1, 0, variable, typeof(bool), false)));
                    actionMethod.AddAction(pushTempBool);
                    actionMethod.AddAction(CyanTriggerAssemblyInstruction.CreateExtern(
                        CyanTriggerDefinitionResolver.GetPrimitiveOperationSignature(
                            typeof(bool), 
                            PrimitiveOperation.UnaryNegation)));
                    
                    // Push constant true for if the jump is successful.
                    actionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(
                        program.Data.GetOrCreateVariableConstant(typeof(bool), true)));
                    
                    // Check if the value was true and we should jump to the end of the condition
                    actionMethod.AddAction(pushTempBool);
                    actionMethod.AddAction(CyanTriggerAssemblyInstruction.JumpIfFalse(scopeFrame.EndNop));
                    
                    // Pop off the constant true since we did not jump.
                    actionMethod.AddAction(CyanTriggerAssemblyInstruction.Pop());
                    
                    program.Data.ReleaseTempVariable(tempBool);
                    return;
                }
            }
            
            compileState.LogError("PassIfTrue statement not included in a condition!");
        }
    }
}