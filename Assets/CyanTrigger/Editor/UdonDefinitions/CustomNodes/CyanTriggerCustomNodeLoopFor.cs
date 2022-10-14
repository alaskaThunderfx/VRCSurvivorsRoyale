using System;
using System.Collections.Generic;
using UnityEditor;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeLoopFor :
        CyanTriggerCustomUdonActionNodeDefinition,
        ICyanTriggerCustomNodeLoop, 
        ICyanTriggerCustomNodeScope,
        ICyanTriggerCustomNodeDependency,
        ICyanTriggerCustomNodeValidator,
        ICyanTriggerCustomNodeCustomVariableInitialization
    {
        public const string FullName = "CyanTriggerSpecial_For";
        
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "For",
            FullName,
            typeof(CyanTrigger),
            new []
            {
                new UdonNodeParameter
                {
                    name = "start",
                    type = typeof(int),
                    parameterType = UdonNodeParameter.ParameterType.IN
                },
                new UdonNodeParameter
                {
                    name = "end",
                    type = typeof(int),
                    parameterType = UdonNodeParameter.ParameterType.IN
                },
                new UdonNodeParameter
                {
                    name = "step",
                    type = typeof(int),
                    parameterType = UdonNodeParameter.ParameterType.IN
                },
                new UdonNodeParameter
                {
                    name = "index",
                    type = typeof(int),
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
            return CyanTriggerDocumentationLinks.ForNodeDocumentation;
        }

        public CyanTriggerErrorType Validate(
            CyanTriggerActionInstance actionInstance, 
            CyanTriggerDataInstance triggerData, 
            ref string message)
        {
            if (!actionInstance.inputs[2].isVariable && ((int) actionInstance.inputs[2].data.Obj) == 0)
            {
                message = "For loop has step value of 0";
                return CyanTriggerErrorType.Error;
            }
            if (actionInstance.inputs[2].isVariable && 
                string.IsNullOrEmpty(actionInstance.inputs[2].name) && 
                string.IsNullOrEmpty(actionInstance.inputs[2].variableID))
            {
                message = "For loop has empty variable for step value";
                return CyanTriggerErrorType.Error;
            }
            
            return CyanTriggerErrorType.None;
        }

        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            var actionInstance = compileState.ActionInstance;
            var program = compileState.Program;
            
            var scopeFrame = compileState.ScopeData.ScopeStack.Peek();
            scopeFrame.EndNop = CyanTriggerAssemblyInstruction.Nop();
            scopeFrame.StartNop = CyanTriggerAssemblyInstruction.Nop();
            
            if (!actionInstance.inputs[2].isVariable && ((int) actionInstance.inputs[2].data.Obj) == 0)
            {
                compileState.LogError("For loop has step value of 0!");
            }
            if (actionInstance.inputs[2].isVariable && 
                string.IsNullOrEmpty(actionInstance.inputs[2].name) && 
                string.IsNullOrEmpty(actionInstance.inputs[2].variableID))
            {
                compileState.LogError("For loop has empty variable for step value!");
            }

            Type intType = typeof(int);
            var startInput = compileState.GetDataFromVariableInstance(-1, 0, actionInstance.inputs[0], intType, false);
            var endInput = compileState.GetDataFromVariableInstance(-1, 1, actionInstance.inputs[1], intType, false);
            var stepInput = compileState.GetDataFromVariableInstance(-1, 2, actionInstance.inputs[2], intType, false);
            
            var indexVariable = compileState.GetDataFromVariableInstance(-1, 3, actionInstance.inputs[3], intType, false);
            
            compileState.ActionMethod.AddActions(BeginForLoop(
                program, 
                startInput, 
                endInput, 
                stepInput, 
                indexVariable, 
                scopeFrame.StartNop, 
                scopeFrame.EndNop,
                compileState.GetVariableChangedActions));
        }
        
        public void HandleEndScope(CyanTriggerCompileState compileState)
        {
            var actionMethod = compileState.ActionMethod;
            var scopeFrame = compileState.ScopeData.ScopeStack.Peek();
            actionMethod.AddActions(EndForLoop(scopeFrame.StartNop, scopeFrame.EndNop));
        }

        public static List<CyanTriggerAssemblyInstruction> BeginForLoop(
            CyanTriggerAssemblyProgram program,
            CyanTriggerAssemblyDataType startInput,
            CyanTriggerAssemblyDataType endInput,
            CyanTriggerAssemblyDataType stepInput,
            CyanTriggerAssemblyDataType indexVariable,
            CyanTriggerAssemblyInstruction startNop,
            CyanTriggerAssemblyInstruction endNop,
            Func<List<CyanTriggerAssemblyDataType>, List<CyanTriggerAssemblyInstruction>> getVariableChangedActions = null,
            Func<List<CyanTriggerAssemblyInstruction>> bodyBeginActions = null)
        {
            var data = program.Data;

            var conditionStartNop = CyanTriggerAssemblyInstruction.Nop();
            var conditionEndNop = CyanTriggerAssemblyInstruction.Nop();
            var conditionNegativeNop = CyanTriggerAssemblyInstruction.Nop();

            Type intType = typeof(int);
            Type boolType = typeof(bool);
            var step = data.RequestTempVariable(intType);
            var end = data.RequestTempVariable(intType);
            var stepIsPositive = data.RequestTempVariable(boolType);
            var conditionBool = data.RequestTempVariable(boolType);
            
            var pushIndex = CyanTriggerAssemblyInstruction.PushVariable(indexVariable);
            var pushStep = CyanTriggerAssemblyInstruction.PushVariable(step);
            var pushEnd = CyanTriggerAssemblyInstruction.PushVariable(end);
            var pushStepIsPositive = CyanTriggerAssemblyInstruction.PushVariable(stepIsPositive);
            var pushConditionBool = CyanTriggerAssemblyInstruction.PushVariable(conditionBool);

            var copyInstruction = CyanTriggerAssemblyInstruction.Copy();
            
            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();

            // Initialize the index to the start value
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(startInput));
            actions.Add(pushIndex);
            actions.Add(copyInstruction);
            
            // Copy end value
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(endInput));
            actions.Add(pushEnd);
            actions.Add(copyInstruction);
            
            // Copy step value
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(stepInput));
            actions.Add(pushStep);
            actions.Add(copyInstruction);
            
            // Check if step is positive. This will be used for comparing index with end.
            actions.Add(pushStep);
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(data.GetOrCreateVariableConstant(intType, 0, false)));
            actions.Add(pushStepIsPositive);
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(
                CyanTriggerDefinitionResolver.GetPrimitiveOperationSignature(intType,
                    PrimitiveOperation.GreaterThanOrEqual)));

            // Jump to condition start
            actions.Add(CyanTriggerAssemblyInstruction.Jump(conditionStartNop));
            
            // Start of for loop. Update value and check condition again.
            actions.Add(startNop);
            
            // Update index
            actions.Add(pushIndex);
            actions.Add(pushStep);
            actions.Add(pushIndex);
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(
                CyanTriggerDefinitionResolver.GetPrimitiveOperationSignature(intType,
                    PrimitiveOperation.Addition)));
            
            // Check if the condition is valid
            actions.Add(conditionStartNop);
            
            // push comparison variables early
            actions.Add(pushIndex);
            actions.Add(pushEnd);
            actions.Add(pushConditionBool);
            
            // Jump to negative compare if not positive
            actions.Add(pushStepIsPositive);
            actions.Add(CyanTriggerAssemblyInstruction.JumpIfFalse(conditionNegativeNop));
            
            // Step is positive, check if index is still than end
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(
                CyanTriggerDefinitionResolver.GetPrimitiveOperationSignature(intType,
                    PrimitiveOperation.LessThan)));

            actions.Add(CyanTriggerAssemblyInstruction.Jump(conditionEndNop));
            actions.Add(conditionNegativeNop);
            
            // Step is negative, check if index is still greater than end
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(
                CyanTriggerDefinitionResolver.GetPrimitiveOperationSignature(intType,
                    PrimitiveOperation.GreaterThan)));
            
            actions.Add(conditionEndNop);
            
            if (getVariableChangedActions != null)
            {
                // Check if index changes.
                var changedVariables = new List<CyanTriggerAssemblyDataType> { indexVariable };
                actions.AddRange(getVariableChangedActions(changedVariables));
            }
            
            // Push condition variable and jump to end if false
            actions.Add(pushConditionBool);
            actions.Add(CyanTriggerAssemblyInstruction.JumpIfFalse(endNop));
            
            if (bodyBeginActions != null)
            {
                actions.AddRange(bodyBeginActions());
            }
            
            return actions;
        }

        public static List<CyanTriggerAssemblyInstruction> EndForLoop(
            CyanTriggerAssemblyInstruction startNop,
            CyanTriggerAssemblyInstruction endNop)
        {
            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();

            actions.Add(CyanTriggerAssemblyInstruction.Jump(startNop));
            actions.Add(endNop);
            
            return actions;
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
            // start and end can be left 0
            
            // step value initialized to 1
            {
                SerializedProperty inputProperty = inputsProperty.GetArrayElementAtIndex(2);
                SerializedProperty nameDataProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                CyanTriggerSerializableObject.UpdateSerializedProperty(nameDataProperty, 1);
            }
            
            // index variable initialized with name
            {
                SerializedProperty inputProperty = inputsProperty.GetArrayElementAtIndex(3);
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

            inputsProperty.serializedObject.ApplyModifiedProperties();
        }
    }
}
