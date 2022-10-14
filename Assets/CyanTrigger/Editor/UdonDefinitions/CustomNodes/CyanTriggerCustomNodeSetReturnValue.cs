using System;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeSetReturnValue : 
        CyanTriggerCustomUdonActionNodeDefinition,
        ICyanTriggerCustomNodeCustomVariableSettings
    {
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "SetReturnValue",
            "CyanTriggerSpecial_SetReturnValue",
            typeof(CyanTrigger),
            new []
            {
                new UdonNodeParameter
                {
                    name = "value",
                    type = typeof(object),
                    parameterType = UdonNodeParameter.ParameterType.IN
                },
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
            return CyanTriggerDocumentationLinks.SetReturnValueNodeDocumentation;
        }
        
        public static readonly CyanTriggerActionVariableDefinition[] VariableDefinitions =
        {
            new CyanTriggerActionVariableDefinition
            {
                type = new CyanTriggerSerializableType(typeof(object)),
                udonName = "value",
                displayName = "return value", 
                description = "Set the event return value to this object",
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
            var returnVariable = compileState.Program.Data.GetSpecialVariable(
                CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ReturnValue);

            var variable = compileState.ActionInstance.inputs[0];
            var valueObject = compileState.GetDataFromVariableInstance(-1, 0, variable, typeof(object), false);
            
            compileState.ActionMethod.AddActions(
                CyanTriggerAssemblyActionsUtils.CopyVariables(valueObject,returnVariable));
        }
    }
}