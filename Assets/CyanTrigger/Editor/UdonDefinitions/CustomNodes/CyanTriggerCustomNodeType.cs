
using System;
using System.Collections.Generic;
using UnityEditor;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeType : 
        CyanTriggerCustomUdonActionNodeDefinition, 
        ICyanTriggerCustomNodeCustomVariableInitialization
    {
        private readonly Type _type;
        private readonly UdonNodeDefinition _definition;
        private readonly string _friendlyName;

        public CyanTriggerCustomNodeType(UdonNodeDefinition typeDefinition)
        {
            _definition = typeDefinition;
            _type = CyanTriggerNodeDefinition.GetFixedType(typeDefinition);
            _friendlyName = CyanTriggerNameHelpers.GetTypeFriendlyName(_type);
        }
        
        public override UdonNodeDefinition GetNodeDefinition()
        {
            return _definition;
        }
        
        public override CyanTriggerNodeDefinition.UdonDefinitionType GetDefinitionType()
        {
            return CyanTriggerNodeDefinition.UdonDefinitionType.Type;
        }

        public Type GetBaseType()
        {
            return _type;
        }

        public override string GetDisplayName()
        {
            return _friendlyName;
        }
        
        public override string GetDocumentationLink()
        {
            return CyanTriggerDocumentationLinks.TypeNodeDocumentation;
        }

        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            var actionInstance = compileState.ActionInstance;
            var actionMethod = compileState.ActionMethod;
            var program = compileState.Program;

            var constTypeVar = program.Data.GetOrCreateVariableConstant(typeof(Type), _type, false);
            var outputVar = compileState.GetDataFromVariableInstance(-1, 0, actionInstance.inputs[0], _type, true);
            
            actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.CopyVariables(constTypeVar, outputVar));

            var changedVariables = new List<CyanTriggerAssemblyDataType> { outputVar };
            compileState.CheckVariableChanged(actionMethod, changedVariables);
        }
        
        public void InitializeVariableProperties(
            SerializedProperty inputsProperty, 
            SerializedProperty multiInputsProperty)
        {
            // type variable initialized with name
            {
                string displayName = $"{CyanTriggerNameHelpers.SanitizeName(CyanTriggerNameHelpers.GetCamelCase(_friendlyName))}Type";
                
                SerializedProperty inputProperty = inputsProperty.GetArrayElementAtIndex(0);
                SerializedProperty nameDataProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                CyanTriggerSerializableObject.UpdateSerializedProperty(nameDataProperty, displayName);

                SerializedProperty idProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
                idProperty.stringValue = Guid.NewGuid().ToString();
                
                SerializedProperty isVariableProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
                isVariableProperty.boolValue = true;
            }

            inputsProperty.serializedObject.ApplyModifiedProperties();
        }
    }
}
