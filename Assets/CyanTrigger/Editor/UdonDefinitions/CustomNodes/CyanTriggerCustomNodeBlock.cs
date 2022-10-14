using System;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeBlock : 
        CyanTriggerCustomUdonActionNodeDefinition, 
        ICyanTriggerCustomNodeScope,
        ICyanTriggerCustomNodeDependency
    {
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "Block",
            "CyanTriggerSpecial_Block",
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
            return CyanTriggerDocumentationLinks.BlockNodeDocumentation;
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
            return new[] {CyanTriggerCustomNodeBlockEnd.NodeDefinition};
        }
    }
}
