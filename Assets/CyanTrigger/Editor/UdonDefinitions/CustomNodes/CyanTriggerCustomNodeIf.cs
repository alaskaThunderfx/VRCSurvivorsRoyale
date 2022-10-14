using System;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeIf : 
        CyanTriggerCustomUdonActionNodeDefinition, 
        ICyanTriggerCustomNodeScope,
        ICyanTriggerCustomNodeDependency,
        ICyanTriggerCustomNodeIf
    {
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "If",
            "CyanTriggerSpecial_If",
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
            return CyanTriggerDocumentationLinks.IfNodeDocumentation;
        }

        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            // Do nothing here
        }
        
        public void HandleEndScope(CyanTriggerCompileState compileState)
        {
            // Do nothing here
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