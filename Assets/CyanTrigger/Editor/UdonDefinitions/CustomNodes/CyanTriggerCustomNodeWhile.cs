using System;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeWhile : 
        CyanTriggerCustomUdonActionNodeDefinition, 
        ICyanTriggerCustomNodeLoop, 
        ICyanTriggerCustomNodeScope,
        ICyanTriggerCustomNodeDependency
    {
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "While",
            "CyanTriggerSpecial_While",
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
            return CyanTriggerDocumentationLinks.WhileNodeDocumentation;
        }
        
        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            var scopeFrame = compileState.ScopeData.ScopeStack.Peek();
            scopeFrame.EndNop = CyanTriggerAssemblyInstruction.Nop();
            scopeFrame.StartNop = CyanTriggerAssemblyInstruction.Nop();
            
            compileState.ActionMethod.AddAction(scopeFrame.StartNop);
        }
        
        public void HandleEndScope(CyanTriggerCompileState compileState)
        {
            var actionMethod = compileState.ActionMethod;

            var lastAction = actionMethod.Actions[actionMethod.Actions.Count - 1];
            if (lastAction.GetInstructionType() != CyanTriggerInstructionType.NOP)
            {
                compileState.LogError($"While expected last instruction to be of type variable Nop! {lastAction.GetInstructionType()}");
            }

            var scopeFrame = compileState.ScopeData.ScopeStack.Peek();
            var jumpToNop = CyanTriggerAssemblyInstruction.Jump(scopeFrame.StartNop);
            actionMethod.Actions.Insert(actionMethod.Actions.Count - 1, jumpToNop);
            
            actionMethod.AddAction(scopeFrame.EndNop);
        }
        
        public UdonNodeDefinition[] GetDependentNodes()
        {
            return new[]
            {
                CyanTriggerCustomNodeCondition.NodeDefinition,
                CyanTriggerCustomNodeConditionBody.NodeDefinition,
                CyanTriggerCustomNodeBlockEnd.NodeDefinition
            };
        }
    }
}
