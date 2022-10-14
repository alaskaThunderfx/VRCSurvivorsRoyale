using System;
using System.Collections.Generic;
using UnityEditor;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeLoopForEach :
        CyanTriggerCustomUdonActionNodeDefinition,
        ICyanTriggerCustomNodeLoop, 
        ICyanTriggerCustomNodeScope,
        ICyanTriggerCustomNodeDependency,
        ICyanTriggerCustomNodeCustomVariableInitialization
    {
        public const string FullName = "CyanTriggerSpecial_ForEach";
        
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "ForEach",
            FullName,
            typeof(CyanTrigger),
            new []
            {
                new UdonNodeParameter
                {
                    name = "Array",
                    type = typeof(Array),
                    parameterType = UdonNodeParameter.ParameterType.IN
                },
                new UdonNodeParameter
                {
                    name = "index",
                    type = typeof(int),
                    parameterType = UdonNodeParameter.ParameterType.OUT
                },
                new UdonNodeParameter
                {
                    name = "value",
                    type = typeof(object),
                    parameterType = UdonNodeParameter.ParameterType.OUT
                }
            },
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
            return CyanTriggerDocumentationLinks.ForeachNodeDocumentation;
        }

        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            var actionInstance = compileState.ActionInstance;
            var program = compileState.Program;
            var data = program.Data;
            var actions = compileState.ActionMethod;
            
            var scopeFrame = compileState.ScopeData.ScopeStack.Peek();
            scopeFrame.EndNop = CyanTriggerAssemblyInstruction.Nop();
            scopeFrame.StartNop = CyanTriggerAssemblyInstruction.Nop();
            
            Type intType = typeof(int);
            
            var startInput = data.GetOrCreateVariableConstant(intType, 0);
            var stepInput = data.GetOrCreateVariableConstant(intType, 1);

            var arrayInput =
                compileState.GetDataFromVariableInstance(-1, 0, actionInstance.inputs[0], typeof(Array), false);
            var endInput = data.RequestTempVariable(intType); // Purposefully do not release temp var
            
            var indexVariable = compileState.GetDataFromVariableInstance(-1, 1, actionInstance.inputs[1], intType, false);
            var objVariable = compileState.GetDataFromVariableInstance(-1, 2, actionInstance.inputs[2], typeof(object), false);

            actions.AddAction(CyanTriggerAssemblyInstruction.PushVariable(arrayInput));
            actions.AddAction(CyanTriggerAssemblyInstruction.PushVariable(endInput));
            actions.AddAction(CyanTriggerAssemblyInstruction.CreateExtern(
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    // ReSharper disable once PossibleNullReferenceException
                    typeof(Array).GetProperty(nameof(Array.Length)).GetGetMethod())));

            List<CyanTriggerAssemblyInstruction> UpdateObjectVariable()
            {
                List<CyanTriggerAssemblyInstruction> getVarActions = new List<CyanTriggerAssemblyInstruction>();
                
                getVarActions.Add(CyanTriggerAssemblyInstruction.PushVariable(arrayInput));
                getVarActions.Add(CyanTriggerAssemblyInstruction.PushVariable(indexVariable));
                getVarActions.Add(CyanTriggerAssemblyInstruction.PushVariable(objVariable));
                getVarActions.Add(CyanTriggerAssemblyInstruction.CreateExtern(
                    CyanTriggerDefinitionResolver.GetMethodSignature(
                        typeof(Array).GetMethod(nameof(Array.GetValue), new [] {typeof (int)}))));
                
                // Check if object's value changes.
                var changedVariables = new List<CyanTriggerAssemblyDataType> { objVariable };
                getVarActions.AddRange(compileState.GetVariableChangedActions(changedVariables));
                
                return getVarActions;
            }
            
            actions.AddActions(CyanTriggerCustomNodeLoopFor.BeginForLoop(
                program, 
                startInput, 
                endInput, 
                stepInput, 
                indexVariable, 
                scopeFrame.StartNop, 
                scopeFrame.EndNop, 
                compileState.GetVariableChangedActions,
                UpdateObjectVariable));
        }
        
        public void HandleEndScope(CyanTriggerCompileState compileState)
        {
            var actionMethod = compileState.ActionMethod;
            var scopeFrame = compileState.ScopeData.ScopeStack.Peek();
            actionMethod.AddActions(CyanTriggerCustomNodeLoopFor.EndForLoop(scopeFrame.StartNop, scopeFrame.EndNop));
        }

        public UdonNodeDefinition[] GetDependentNodes()
        {
            return new[]
            {
                CyanTriggerCustomNodeBlockEnd.NodeDefinition
            };
        }
        
        public void InitializeVariableProperties(
            SerializedProperty inputsProperty, 
            SerializedProperty multiInputsProperty)
        {
            // Array input left uninitialized
            
            // index variable initialized with name
            {
                SerializedProperty inputProperty = inputsProperty.GetArrayElementAtIndex(1);
                SerializedProperty nameDataProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                CyanTriggerSerializableObject.UpdateSerializedProperty(nameDataProperty, "index_int");
                
                SerializedProperty idProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
                idProperty.stringValue = Guid.NewGuid().ToString();
                
                SerializedProperty isVariableProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
                isVariableProperty.boolValue = true;
            }
            
            // value object variable initialized with name
            {
                SerializedProperty inputProperty = inputsProperty.GetArrayElementAtIndex(2);
                SerializedProperty nameDataProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                CyanTriggerSerializableObject.UpdateSerializedProperty(nameDataProperty, "value_object");
                
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