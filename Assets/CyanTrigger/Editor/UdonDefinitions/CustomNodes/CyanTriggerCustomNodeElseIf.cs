using System;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeElseIf : CyanTriggerCustomNodeElse, ICyanTriggerCustomNodeIf
    {
        public new static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "ElseIf",
            "CyanTriggerSpecial_ElseIf",
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
        
        public override string GetDisplayName()
        {
            return NodeDefinition.name;
        }
        
        public override string GetDocumentationLink()
        {
            return CyanTriggerDocumentationLinks.ElseIfNodeDocumentation;
        }
        
        // All logic is handled in the else code
        public override void HandleEndScope(CyanTriggerCompileState compileState)
        {
            var actionMethod = compileState.ActionMethod;

            var scopeFrame = compileState.ScopeData.ScopeStack.Peek();
            actionMethod.Actions.Insert(actionMethod.Actions.Count - 1, scopeFrame.EndNop);
        }
        
        public override UdonNodeDefinition[] GetDependentNodes()
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
