using System;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeConditionBody : 
        CyanTriggerCustomUdonActionNodeDefinition, 
        ICyanTriggerCustomNodeScope,
        ICyanTriggerCustomNodeDependency
    {
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "ConditionBody",
            "CyanTriggerSpecial_ConditionBody",
            typeof(CyanTrigger),
            Array.Empty<UdonNodeParameter>(),
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
            return CyanTriggerDocumentationLinks.ConditionBodyNodeDocumentation;
        }

        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            var actionMethod = compileState.ActionMethod;
            var scopeData = compileState.ScopeData;

            var scopeFrame = scopeData.ScopeStack.Peek();
            scopeFrame.EndNop = CyanTriggerAssemblyInstruction.Nop();
            
            // Verify previous node is Condition
            if (!(scopeData.PreviousScopeDefinition is CyanTriggerCustomNodeCondition))
            {
                compileState.LogError($"Condition body did not come after a Condition! {scopeData.PreviousScopeDefinition}");
            }

            var actions = actionMethod.Actions;
            if (actions.Count < 2)
            {
                compileState.LogError("Condition body expected at least two instructions.");
            }

            var pushAction = actions[actions.Count - 2];
            var nopAction = actions[actions.Count - 1];
            
            if (pushAction.GetInstructionType() != CyanTriggerInstructionType.PUSH)
            {
                compileState.LogError($"Condition body expected last instruction to be of type variable push! {pushAction.GetInstructionType()}");
            }
            
            if (nopAction.GetInstructionType() != CyanTriggerInstructionType.NOP)
            {
                compileState.LogError($"Condition body expected last instruction to be of type variable Nop! {nopAction.GetInstructionType()}");
            }
            
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.JumpIfFalse(scopeFrame.EndNop));
        }
        
        public void HandleEndScope(CyanTriggerCompileState compileState)
        {
            var scopeFrame = compileState.ScopeData.ScopeStack.Peek();
            compileState.ActionMethod.AddAction(scopeFrame.EndNop);
        }
        
        public UdonNodeDefinition[] GetDependentNodes()
        {
            return new[] {CyanTriggerCustomNodeBlockEnd.NodeDefinition};
        }
    }
}