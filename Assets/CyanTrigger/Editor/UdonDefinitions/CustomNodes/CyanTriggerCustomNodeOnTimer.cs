using System;
using UnityEditor;
using UnityEngine;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeOnTimer : 
        CyanTriggerCustomUdonEventNodeDefinition,
        ICyanTriggerCustomNodeValidator,
        ICyanTriggerCustomNodeCustomHash
    {
        public const string OnTimerEventName = "Event_OnTimer";

        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "OnTimer",
            OnTimerEventName,
            typeof(void),
            new[]
            {
                new UdonNodeParameter
                {
                    name = "Repeat",
                    type = typeof(bool),
                    parameterType = UdonNodeParameter.ParameterType.IN
                },
                new UdonNodeParameter
                {
                    name = "ResetOnEnable",
                    type = typeof(bool),
                    parameterType = UdonNodeParameter.ParameterType.IN
                },
                new UdonNodeParameter
                {
                    name = "LowPeriodTime",
                    type = typeof(float),
                    parameterType = UdonNodeParameter.ParameterType.IN
                },
                new UdonNodeParameter
                {
                    name = "HighPeriodTime",
                    type = typeof(float),
                    parameterType = UdonNodeParameter.ParameterType.IN
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
            return CyanTriggerNodeDefinition.UdonDefinitionType.Event;
        }

        public override string GetDisplayName()
        {
            return NodeDefinition.name;
        }
        
        public override string GetDocumentationLink()
        {
            return CyanTriggerDocumentationLinks.OnTimerNodeDocumentation;
        }

        public override string GetBaseMethodName(SerializedProperty eventProperty)
        {
            SerializedProperty eventInstance =
                eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.eventInstance));
            SerializedProperty inputs = eventInstance.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
            SerializedProperty repeatProp = inputs.GetArrayElementAtIndex(0);
            SerializedProperty repeatData =
                repeatProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
            SerializedProperty resetProp = inputs.GetArrayElementAtIndex(1);
            SerializedProperty resetData =
                resetProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
            SerializedProperty lowProp = inputs.GetArrayElementAtIndex(2);
            SerializedProperty lowData =
                lowProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
            SerializedProperty highProp = inputs.GetArrayElementAtIndex(3);
            SerializedProperty highData =
                highProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));

            return GetTimerMethodName(
                (bool)CyanTriggerSerializableObject.ObjectFromSerializedProperty(repeatData),
                (bool)CyanTriggerSerializableObject.ObjectFromSerializedProperty(resetData),
                (float)CyanTriggerSerializableObject.ObjectFromSerializedProperty(lowData),
                (float)CyanTriggerSerializableObject.ObjectFromSerializedProperty(highData));
        }

        public override string GetBaseMethodName(CyanTriggerEvent evt)
        {
            var inputs = evt.eventInstance.inputs;
            return GetTimerMethodName(
                (bool)inputs[0].data.Obj, 
                (bool)inputs[1].data.Obj,
                (float)inputs[2].data.Obj,
                (float)inputs[3].data.Obj);
        }

        private string GetFloatAsString(float value)
        {
            return value.ToString("E5").Replace(".", "_").Replace("-", "n").Replace("+", "p");
        }
        
        private string GetTimerMethodName(bool repeat, bool resetOnEnable, float low, float high)
        {
            return CyanTriggerNameHelpers.SanitizeName($"_onTimer_{(repeat ? 1 : 0)}_{(resetOnEnable ? 1 : 0)}_l_{GetFloatAsString(low)}_h_{GetFloatAsString(high)}");
        }

        public override bool GetBaseMethod(
            CyanTriggerAssemblyProgram program, 
            CyanTriggerActionInstance actionInstance,
            out CyanTriggerAssemblyMethod method)
        {
            var inputs = actionInstance.inputs;

            bool repeat = (bool)inputs[0].data.Obj;
            bool resetOnEnable = (bool)inputs[1].data.Obj;
            float low = (float)inputs[2].data.Obj;
            float high = (float)inputs[3].data.Obj;
            string methodName = GetTimerMethodName(repeat, resetOnEnable, low, high);
            
            bool created = program.Code.GetOrCreateMethod(methodName, true, out method);
            if (created)
            {
                InitializeOnTimer(program, methodName, repeat, resetOnEnable, low, high);
            }
            return created;
        }
        
        public override CyanTriggerAssemblyMethod AddEventToProgram(CyanTriggerCompileState compileState)
        {
            // Everything is handled in the GetBaseMethod method as this is reused for multiple OnTimer events. 
            return CyanTriggerCompiler.AddDefaultEventToProgram(
                compileState.Program, 
                compileState.ActionMethod);
        }

        private void InitializeOnTimer(CyanTriggerAssemblyProgram program, string methodName, bool repeat, bool resetOnEnable, float low, float high)
        {
            var code = program.Code;
            var data = program.Data;
            
            string getTimeMethod =
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(Time).GetProperty(nameof(Time.timeSinceLevelLoad))
                        .GetGetMethod());
            string getRandomRangeMethod =
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(UnityEngine.Random).GetMethod(
                        nameof(UnityEngine.Random.Range),
                        new[] { typeof(float), typeof(float) }));

            string floatEqualityMethod =
                CyanTriggerDefinitionResolver.GetPrimitiveOperationSignature(
                    typeof(float),
                    PrimitiveOperation.Equality);
            string floatLess =
                CyanTriggerDefinitionResolver.GetPrimitiveOperationSignature(
                    typeof(float),
                    PrimitiveOperation.LessThan);
            string floatAdd =
                CyanTriggerDefinitionResolver.GetPrimitiveOperationSignature(
                    typeof(float),
                    PrimitiveOperation.Addition);

            // Does not need to be exported
            code.GetOrCreateMethod($"_{methodName}_reset", false, out var resetMethod);
            resetMethod.PushInitialEndVariable(data);

            if (code.GetOrCreateMethod("_onEnable", true, out var enableMethod))
            {
                enableMethod.PushInitialEndVariable(data);
            }
            if (code.GetOrCreateMethod("_update", true, out var updateMethod))
            {
                updateMethod.PushInitialEndVariable(data);
            }
            
            var timerVariable = data.AddVariable("timer", typeof(float), false, 0f);
            var constZero = data.GetOrCreateVariableConstant(typeof(float), 0f);
            var constInfinity = data.GetOrCreateVariableConstant(typeof(float), float.PositiveInfinity);
            
            var constLow = data.GetOrCreateVariableConstant(typeof(float), low);
            var constHigh = data.GetOrCreateVariableConstant(typeof(float), high);
            
            var pushTimerVar = CyanTriggerAssemblyInstruction.PushVariable(timerVariable);
            var pushConstZero = CyanTriggerAssemblyInstruction.PushVariable(constZero);
            var pushConstLow = CyanTriggerAssemblyInstruction.PushVariable(constLow);
            var pushConstHigh = CyanTriggerAssemblyInstruction.PushVariable(constHigh);
            
            var tempTimeVar = data.RequestTempVariable(typeof(float));
            var pushTempTimeVar = CyanTriggerAssemblyInstruction.PushVariable(tempTimeVar);
            var tempBoolVar = data.RequestTempVariable(typeof(bool));
            var pushTempBoolVar = CyanTriggerAssemblyInstruction.PushVariable(tempBoolVar);

            
            // ====== OnEnable actions ======
            // Add reset actions.
            var enableEndNop = CyanTriggerAssemblyInstruction.Nop();
            
            if (!resetOnEnable)
            {
                // Check if initialized and jump to end.
                enableMethod.AddAction(pushTimerVar);
                enableMethod.AddAction(pushConstZero);
                enableMethod.AddAction(pushTempBoolVar);
                enableMethod.AddAction(CyanTriggerAssemblyInstruction.CreateExtern(floatEqualityMethod));
                
                enableMethod.AddAction(pushTempBoolVar);
                enableMethod.AddAction(CyanTriggerAssemblyInstruction.JumpIfFalse(enableEndNop));
            }
            enableMethod.AddActions(CyanTriggerAssemblyActionsUtils.JumpToFunction(program, resetMethod.Name));
            
            enableMethod.AddAction(enableEndNop);
            
            
            // ====== Update actions ======
            var updateEndNop = CyanTriggerAssemblyInstruction.Nop();

            // Get current time
            updateMethod.AddAction(pushTempTimeVar);
            updateMethod.AddAction(CyanTriggerAssemblyInstruction.CreateExtern(getTimeMethod));
            
            // Compare time (timerVar < timeSinceLevelLoad)
            updateMethod.AddAction(pushTimerVar);
            updateMethod.AddAction(pushTempTimeVar);
            updateMethod.AddAction(pushTempBoolVar);
            updateMethod.AddAction(CyanTriggerAssemblyInstruction.CreateExtern(floatLess));
            
            updateMethod.AddAction(pushTempBoolVar);
            updateMethod.AddAction(CyanTriggerAssemblyInstruction.JumpIfFalse(updateEndNop));

            if (repeat)
            {
                updateMethod.AddActions(CyanTriggerAssemblyActionsUtils.JumpToFunction(program, resetMethod.Name));
            }
            else
            {
                // Set time to -1 to prevent it from firing again
                updateMethod.AddActions(CyanTriggerAssemblyActionsUtils.CopyVariables(constInfinity, timerVariable));
            }
            updateMethod.AddActions(CyanTriggerAssemblyActionsUtils.JumpToFunction(program, methodName));
            
            updateMethod.AddAction(updateEndNop);
            
            // ====== Reset Timer actions ======
            
            // Get current time
            resetMethod.AddAction(pushTimerVar);
            resetMethod.AddAction(CyanTriggerAssemblyInstruction.CreateExtern(getTimeMethod));
            
            // Get random range between low and high
            resetMethod.AddAction(pushConstLow);
            resetMethod.AddAction(pushConstHigh);
            resetMethod.AddAction(pushTempTimeVar);
            resetMethod.AddAction(CyanTriggerAssemblyInstruction.CreateExtern(getRandomRangeMethod));
            
            // Add time with random to get next random time
            resetMethod.AddAction(pushTimerVar);
            resetMethod.AddAction(pushTempTimeVar);
            resetMethod.AddAction(pushTimerVar);
            resetMethod.AddAction(CyanTriggerAssemblyInstruction.CreateExtern(floatAdd));
            
            // Done
            
            data.ReleaseTempVariable(tempTimeVar);
            data.ReleaseTempVariable(tempBoolVar);
        }

        public CyanTriggerErrorType Validate(
            CyanTriggerActionInstance actionInstance, 
            CyanTriggerDataInstance triggerData,
            ref string message)
        {
            var inputs = actionInstance.inputs;

            if (inputs.Length != 4)
            {
                message = "Invalid input size";
                return CyanTriggerErrorType.Error;
            }
            
            for (var index = 0; index < inputs.Length; index++)
            {
                var input = inputs[index];
                if (input.isVariable)
                {
                    message = "Input cannot be a variable";
                    return CyanTriggerErrorType.Error;
                }
            }

            if (!(inputs[0].data.Obj is bool) 
                || !(inputs[1].data.Obj is bool)
                || !(inputs[2].data.Obj is float low) 
                || !(inputs[3].data.Obj is float high))
            {
                message = "Input has invalid data!";
                return CyanTriggerErrorType.Error;
            }

            if (low < 0 || high < 0)
            {
                message = "Low and high period times must be non negative";
                return CyanTriggerErrorType.Error;
            }

            if (low > high)
            {
                message = "Low period time must be less than or equal to the high period time";
                return CyanTriggerErrorType.Error;
            }

            return CyanTriggerErrorType.None;
        }

        public string GetCustomHash(CyanTriggerActionInstance actionInstance)
        {
            var inputs = actionInstance.inputs;

            bool repeat = (bool)inputs[0].data.Obj;
            bool resetOnEnable = (bool)inputs[1].data.Obj;
            float low = (float)inputs[2].data.Obj;
            float high = (float)inputs[3].data.Obj;
            return $"OnTimer - repeat: {repeat}, reset: {resetOnEnable}, low: {low:E5}, high: {high:E5}";
        }
    }
}