using System;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeElse : 
        CyanTriggerCustomUdonActionNodeDefinition, 
        ICyanTriggerCustomNodeScope,
        ICyanTriggerCustomNodeDependency
    {
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "Else",
            "CyanTriggerSpecial_Else",
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
            return CyanTriggerDocumentationLinks.ElseNodeDocumentation;
        }

        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            var actionMethod = compileState.ActionMethod;
            var scopeData = compileState.ScopeData;

            // Verify previous node is If or Else_If
            if (!(scopeData.PreviousScopeDefinition is ICyanTriggerCustomNodeIf))
            {
                compileState.LogError($"{GetDisplayName()} did not come after an If or ElseIf Action!");
                return;
            }

            var endNop = CyanTriggerAssemblyInstruction.Nop();
            scopeData.ScopeStack.Peek().EndNop = endNop;
            
            var lastAction = actionMethod.Actions[actionMethod.Actions.Count - 1];
            if (lastAction.GetInstructionType() != CyanTriggerInstructionType.NOP)
            {
                compileState.LogError($"Else expected last instruction to be of type variable Nop! {lastAction.GetInstructionType()}");
                return;
            }

            var jumpToNop = CyanTriggerAssemblyInstruction.Jump(endNop);
            actionMethod.Actions.Insert(actionMethod.Actions.Count - 1, jumpToNop);
        }
        
        public virtual void HandleEndScope(CyanTriggerCompileState compileState)
        {
            var scopeFrame = compileState.ScopeData.ScopeStack.Peek();
            compileState.ActionMethod.AddAction(scopeFrame.EndNop);
        }
        
        public virtual UdonNodeDefinition[] GetDependentNodes()
        {
            return new[]
            {
                CyanTriggerCustomNodeBlockEnd.NodeDefinition
            };
        }
    }
}