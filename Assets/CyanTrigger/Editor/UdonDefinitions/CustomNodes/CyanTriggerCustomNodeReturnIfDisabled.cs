using System;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeReturnIfDisabled : CyanTriggerCustomUdonActionNodeDefinition
    {
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "ReturnIfDisabled",
            "CyanTriggerSpecial_ReturnIfDisabled",
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
            return CyanTriggerDocumentationLinks.ReturnIfDisabledNodeDocumentation;
        }

        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            var actionMethod = compileState.ActionMethod;
            var data = compileState.Program.Data;

            var thisGameObject = data.GetThisConst(typeof(GameObject));
            var thisUdon = data.GetThisConst(typeof(IUdonEventReceiver));

            var tempBool = data.RequestTempVariable(typeof(bool));
            var pushTempBool = CyanTriggerAssemblyInstruction.PushVariable(tempBool);
            
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(thisGameObject));
            actionMethod.AddAction(pushTempBool);
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.CreateExtern(
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(GameObject).GetProperty(nameof(GameObject.activeInHierarchy)).GetGetMethod())));
            
            actionMethod.AddAction(pushTempBool);
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.JumpIfFalse(actionMethod.EndNop));
            
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(thisUdon));
            actionMethod.AddAction(pushTempBool);
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.CreateExtern(
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(UdonBehaviour).GetProperty(nameof(UdonBehaviour.enabled)).GetGetMethod())));
            
            actionMethod.AddAction(pushTempBool);
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.JumpIfFalse(actionMethod.EndNop));
        }
    }
}

