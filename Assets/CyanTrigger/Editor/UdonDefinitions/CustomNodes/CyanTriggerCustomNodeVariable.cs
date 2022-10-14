using System;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    // This node type is deprecated and should not be used in the future. Keeping for legacy reasons.
    [Obsolete("Use Set node instead of Variable Node.")]
    public class CyanTriggerCustomNodeVariable : CyanTriggerCustomNodeVariableProvider
    {
        public readonly Type Type;
        private readonly UdonNodeDefinition _definition;
        private readonly string _friendlyName;

        public CyanTriggerCustomNodeVariable(Type type)
        {
            Type = type;
            _friendlyName = CyanTriggerNameHelpers.GetTypeFriendlyName(Type);
            string fullName = GetFullnameForType(Type);
            
            // TODO verify this doesn't break anything for for Udon types.
            if (Type == typeof(IUdonEventReceiver))
            {
                Type = typeof(UdonBehaviour);
            }
            
            _definition = new UdonNodeDefinition(
                $"Variable {_friendlyName}",
                fullName,
                Type,
                new []
                {
                    new UdonNodeParameter
                    {
                        name = "Value",
                        type = Type,
                        parameterType = UdonNodeParameter.ParameterType.IN,
                    }
                },
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<object>(),
                true
            );
        }

        public static string GetFullnameForType(Type type)
        {
            string fullName = CyanTriggerNameHelpers.SanitizeName(type.FullName);
            return $"CyanTriggerVariable_{fullName}{(type.IsArray?"Array":"")}";
        }
        
        public override UdonNodeDefinition GetNodeDefinition()
        {
            return _definition;
        }
        
        public override CyanTriggerNodeDefinition.UdonDefinitionType GetDefinitionType()
        {
            return CyanTriggerNodeDefinition.UdonDefinitionType.CyanTriggerVariable;
        }

        public override string GetDisplayName()
        {
            return _friendlyName;
        }
        
        public override string GetDocumentationLink()
        {
            return UdonGraphExtensions.GetDocumentationLink(_definition);
        }

        public override bool HasDocumentation()
        {
            return UdonGraphExtensions.ShouldShowDocumentationLink(_definition);
        }

        protected override (string, Type, bool)[] GetVariables()
        {
            return new[]
            {
                ("Variable", Type, false)
            };
        }

        protected override bool ShowDefinedVariablesAtBeginning()
        {
            return false;
        }

        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            var actionInstance = compileState.ActionInstance;
            var actionMethod = compileState.ActionMethod;
            var program = compileState.Program;

            string variableGuid = GetVariableGuid(actionInstance, 0);

            actionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(
                compileState.GetDataFromVariableInstance(-1, 1, actionInstance.inputs[1], Type, false)));
            var userVariable = program.Data.GetUserDefinedVariable(variableGuid);
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(userVariable));
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.Copy());
        }

        protected override string GetVariableName(CyanTriggerAssemblyProgram program, Type type)
        {
            return program.Data.AddVariable("var", type, false).Name;
        }
    }
}
