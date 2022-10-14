using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Editor;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeSendCustomEventUdon : 
        CyanTriggerCustomUdonActionNodeDefinition,
        ICyanTriggerCustomNodeCustomVariableInitialization,
        ICyanTriggerCustomNodeCustomHash,
        ICyanTriggerCustomNodeValidator,
        ICyanTriggerCustomNodeCustomVariableInputSize
    {
        public const string FullName =
            "VRCUdonCommonInterfacesIUdonEventReceiver.__SendCustomEvent__SystemString__SystemVoid";
        private readonly UdonNodeDefinition _nodeDef;

        public CyanTriggerCustomNodeSendCustomEventUdon()
        {
            _nodeDef = UdonEditorManager.Instance.GetNodeDefinition(FullName);
        }
        
        public override UdonNodeDefinition GetNodeDefinition()
        {
            return _nodeDef;
        }
        
        public override CyanTriggerNodeDefinition.UdonDefinitionType GetDefinitionType()
        {
            return CyanTriggerNodeDefinition.UdonDefinitionType.Method;
        }

        public override string GetDisplayName()
        {
            return "SendCustomEvent";
        }
        
        public override string GetDocumentationLink()
        {
            return CyanTriggerDocumentationLinks.SendCustomEventNodeDocumentation;
        }

        public void InitializeVariableProperties(
            SerializedProperty inputProperties, 
            SerializedProperty multiInputProperties)
        {
            var thisUdon = CyanTriggerAssemblyDataConsts.ThisUdonBehaviour;
            
            multiInputProperties.arraySize = 1;
            SerializedProperty inputNameProperty = multiInputProperties.GetArrayElementAtIndex(0);
            SerializedProperty nameProperty =
                inputNameProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));
            nameProperty.stringValue = thisUdon.Name;
            SerializedProperty guidProperty =
                inputNameProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
            guidProperty.stringValue = thisUdon.ID;
            SerializedProperty isVariableProperty =
                inputNameProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
            isVariableProperty.boolValue = true;
                
            multiInputProperties.serializedObject.ApplyModifiedProperties();
        }

        public string GetCustomHash(CyanTriggerActionInstance actionInstance)
        {
            bool local = false;
            foreach (var var in actionInstance.multiInput)
            {
                if (var.isVariable && var.variableID == CyanTriggerAssemblyDataConsts.ThisUdonBehaviour.ID)
                {
                    local = true;
                    break;
                }
            }

            StringBuilder sb = new StringBuilder();
            if (local && !actionInstance.inputs[1].isVariable)
            {
                sb.Append("UdonCustomNamed: ");
                sb.Append(actionInstance.inputs[1].data.Obj);
            }

            var argData = GetArgData(actionInstance);
            if (argData != null)
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }
                
                sb.Append("ArgData: ");
                sb.Append(argData.GetUniqueString(true));
            }

            return sb.ToString();
        }

        public CyanTriggerErrorType Validate(
            CyanTriggerActionInstance actionInstance,
            CyanTriggerDataInstance triggerData,
            ref string message)
        {
            if (actionInstance.inputs[1].isVariable)
            {
                return CyanTriggerErrorType.None;
            }

            string eventName = actionInstance.inputs[1].data?.Obj as string;
            // TODO this is required for CustomActions, and shouldn't cause warnings.
            if (string.IsNullOrEmpty(eventName))
            {
                message = "Event is empty";
                return CyanTriggerErrorType.None;
            }

            // Check if event exists on the CyanTrigger.
            bool local = false;
            foreach (var var in actionInstance.multiInput)
            {
                if (var.isVariable && var.variableID == CyanTriggerAssemblyDataConsts.ThisUdonBehaviour.ID)
                {
                    local = true;
                    break;
                }
            }

            // TODO this won't work with the current design of verification due to programs not having data on
            // unity objects to even get what udon behaviour targets exist.

            // Get arg data and verify if program has expected parameter names
            // var argData = GetArgData(actionInstance);
            // if (argData != null)
            // {
            //     var udonTargets = CyanTriggerCustomNodeInspectorUtil.GetTypeFromMultiInput<UdonBehaviour>(
            //         actionInstance.multiInput,
            //         triggerData.variables,
            //         null,
            //         out bool containsSelf);
            //     
            //     bool isValidEventArgData = CyanTriggerCustomNodeInspectorUtil.HasEventOptions(
            //         triggerData, 
            //         udonTargets, 
            //         containsSelf,
            //         argData,
            //         true,
            //         false);
            //
            //     if (!isValidEventArgData)
            //     {
            //         message = $"Event parameters are invalid for \"{eventName}\"";
            //         return CyanTriggerErrorType.Error;
            //     }
            // }

            if (!local)
            {
                return CyanTriggerErrorType.None;
            }

            var argData = GetArgData(actionInstance);
            if (argData != null)
            {
                bool found = false;
                foreach (var evt in CyanTriggerCustomNodeInspectorUtil.GetEventOptionsFromCyanTrigger(triggerData))
                {
                    if (evt.eventName == argData.eventName)
                    {
                        found = true;
                        if (!argData.Equals(evt))
                        {
                            message = $"Parameter info does not match expected for Event \"{eventName}\"";
                            return CyanTriggerErrorType.Error;
                        }
                    }
                }

                if (found)
                {
                    return CyanTriggerErrorType.None;
                }
            }
            else
            {
                foreach (var evt in triggerData.events)
                {
                    var eventInfo = CyanTriggerActionInfoHolder.GetActionInfoHolder(evt.eventInstance.actionType);
                
                    if (eventInfo.GetEventCompiledName(evt) == eventName)
                    {
                        return CyanTriggerErrorType.None;
                    }
                }
            }
            
            message = $"This CyanTrigger does not have the event \"{eventName}\"";
            return CyanTriggerErrorType.Error;
        }

        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            var actionInstance = compileState.ActionInstance;
            var actionMethod = compileState.ActionMethod;
            var program = compileState.Program;
            var data = program.Data;
            
            bool isVariableEvent = actionInstance.inputs[1].isVariable;
            string eventName = actionInstance.inputs[1].data?.Obj as string;
            bool isEmptyEvent = !isVariableEvent && string.IsNullOrEmpty(eventName); 
            // TODO this is required for CustomActions, and shouldn't change compiling.
            // if (!isVariableEvent && string.IsNullOrEmpty(eventName))
            // {
            //     compileState.LogWarning("UdonBehaviour.SendCustomEvent Event is empty!");
            //     return;
            // }
            
            var thisUdonId = CyanTriggerAssemblyDataConsts.ThisUdonBehaviour.ID;
            var eventNameVariable =
                compileState.GetDataFromVariableInstance(-1, 1, actionInstance.inputs[1], typeof(string), false);
            
            // Get extra variables
            var argData = GetArgData(actionInstance);
            List<CyanTriggerAssemblyDataType> outputs = new List<CyanTriggerAssemblyDataType>();
            List<(CyanTriggerAssemblyDataType, string)> inputToDest = new List<(CyanTriggerAssemblyDataType, string)>();
            List<(CyanTriggerAssemblyDataType, string)> outputToDest = new List<(CyanTriggerAssemblyDataType, string)>();

            if (argData != null)
            {
                // Go through the argument data and gather the input and output parameters.
                for (int index = 0; index < argData.variableNames.Length; ++index)
                {
                    int argInd = 3 + index;
                    Type type = argData.variableTypes[index];
                    var inputData = actionInstance.inputs[argInd];
                    var inputVar = compileState.GetDataFromVariableInstance(-1, argInd, inputData, type, false);

                    string dest = argData.variableUdonNames[index];
                    var variableData = (inputVar, dest);
                    if (argData.variableOuts[index])
                    {
                        outputToDest.Add(variableData);
                        outputs.Add(inputVar);
                    }
                    inputToDest.Add(variableData);
                }
            }

            
            for (int curMulti = 0; curMulti < actionInstance.multiInput.Length; ++curMulti)
            {
                var variable = actionInstance.multiInput[curMulti];

                // Jump to self. Optimize and jump directly to the method
                if (!isEmptyEvent && !isVariableEvent && variable.isVariable && variable.variableID == thisUdonId)
                {
                    // Go through extra variables and copy inputs to local version
                    foreach (var input in inputToDest)
                    {
                        var dest = data.GetVariableNamed(input.Item2);
                        actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.CopyVariables(input.Item1, dest));
                    }
                    
                    actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.JumpToFunction(
                        program, 
                        eventName));
                    
                    // Go through extra variables and copy outputs back
                    foreach (var input in outputToDest)
                    {
                        var dest = data.GetVariableNamed(input.Item2);
                        actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.CopyVariables(dest, input.Item1));
                    }
                    
                    if (outputs.Count > 0)
                    {
                        // Check for change events on extra output variables
                        compileState.CheckVariableChanged(actionMethod, outputs);
                    }
                    
                    continue;
                }

                var udonVariable =
                    compileState.GetDataFromVariableInstance(curMulti, 0, variable, typeof(IUdonEventReceiver), false);
                
                // Go through extra variables and SetProgramVariable for inputs
                foreach (var input in inputToDest)
                {
                    var destVarName = data.GetOrCreateVariableConstant(typeof(string), input.Item2);
                    actionMethod.AddActions(
                        CyanTriggerAssemblyActionsUtils.SetProgramVariable(destVarName, input.Item1, udonVariable));
                }
                
                actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.SendCustomEvent(
                    program,
                    udonVariable,
                    eventNameVariable));
                
                // Go through extra variables and GetProgramVariable outputs back
                foreach (var output in outputToDest)
                {
                    var destVarName = data.GetOrCreateVariableConstant(typeof(string), output.Item2);
                    actionMethod.AddActions(
                        CyanTriggerAssemblyActionsUtils.GetProgramVariable(destVarName, output.Item1, udonVariable));
                }
                
                if (outputs.Count > 0)
                {
                    // Check for change events on extra output variables
                    compileState.CheckVariableChanged(actionMethod, outputs);
                }
            }
        }

        public void SetArgData(
            SerializedProperty actionProperty, 
            CyanTriggerEventArgData argData,
            CyanTriggerEventArgData prevArgData)
        {
            var inputsProperty = actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
            
            var eventProp = inputsProperty.GetArrayElementAtIndex(1);
            var propIsEventVariableProp =
                eventProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
            
            int defaultParamCount = _nodeDef.parameters.Count;
            int expectedSize = defaultParamCount;
            int argCount = 0;
            // If event input is a variable, there is no arg data, or no variables for arg data
            //   Then input size should not include arg data options
            // Else, increase input size to match expected arguments + 1. First custom input holds the arg data itself.
            if (!propIsEventVariableProp.boolValue && argData != null)
            {
                argCount = argData.variableNames.Length;
                expectedSize += 1 + argCount;
            }
            
            if (inputsProperty.arraySize != expectedSize)
            {
                inputsProperty.arraySize = expectedSize;
            }

            if (expectedSize == defaultParamCount)
            {
                return;
            }

            var extraVarDataProp = inputsProperty.GetArrayElementAtIndex(defaultParamCount);

            var dataProp = extraVarDataProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
            CyanTriggerSerializableObject.UpdateSerializedProperty(dataProp, argData);
            
            // Update input properties
            for (int index = 0; index < argCount; ++index)
            {
                // Do not clear data for items that have the same type.
                if (prevArgData != null
                    && argData != null
                    && index < prevArgData.variableTypes.Length
                    && prevArgData.variableTypes[index] == argData.variableTypes[index])
                {
                    continue;
                }
                
                // Clear data for current input as type doesn't match.
                int propIndex = index + defaultParamCount + 1;
                var argDataInputProp = inputsProperty.GetArrayElementAtIndex(propIndex);
                var argDataProp = argDataInputProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                CyanTriggerSerializableObject.UpdateSerializedProperty(argDataProp, null);
                
                var nameProp = argDataInputProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));
                nameProp.stringValue = "";
                
                var idProp = argDataInputProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
                idProp.stringValue = "";
                
                var isVariableProp = argDataInputProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
                isVariableProp.boolValue = false;
            }
        }

        public CyanTriggerEventArgData GetArgData(CyanTriggerActionInstance actionInstance)
        {
            int expectedSize = _nodeDef.parameters.Count;
            if (actionInstance.inputs.Length <= expectedSize || actionInstance.inputs[1].isVariable)
            {
                return null;
            }

            var argDataInput = actionInstance.inputs[expectedSize];
            object argDataObj = argDataInput.data.Obj;
            if (argDataObj is CyanTriggerEventArgData argData)
            {
                return argData;
            }
            return null;
        }
        
        public CyanTriggerEventArgData GetArgData(SerializedProperty actionProperty)
        {
            var inputsProperty = actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
            int expectedSize = _nodeDef.parameters.Count;
            if (inputsProperty.arraySize <= expectedSize)
            {
                return null;
            }
            
            var eventProp = inputsProperty.GetArrayElementAtIndex(1);
            var propIsEventVariableProp =
                eventProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
            if (propIsEventVariableProp.boolValue)
            {
                return null;
            }
            
            var extraVarDataProp = inputsProperty.GetArrayElementAtIndex(expectedSize);
            var dataProp = extraVarDataProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
            object argDataObj = CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataProp);
            if (argDataObj is CyanTriggerEventArgData argData)
            {
                return argData;
            }
            return null;
        }

        public CyanTriggerActionVariableDefinition[] GetExtraVariables(
            SerializedProperty actionProperty,
            bool includeEventVariables)
        {
            CyanTriggerActionInfoHolder actionInfo = CyanTriggerActionInfoHolder.GetActionInfoHolder(_nodeDef);
            List<CyanTriggerActionVariableDefinition> variables =
                new List<CyanTriggerActionVariableDefinition>(actionInfo.GetBaseActionVariables(includeEventVariables));

            var argData = GetArgData(actionProperty);
            if (argData != null)
            {
                GetVariablesFromEventArgs(argData, variables);
            }

            return variables.ToArray();
        }

        public CyanTriggerActionVariableDefinition[] GetExtraVariables(
            CyanTriggerActionInstance actionInstance, 
            bool includeEventVariables)
        {
            CyanTriggerActionInfoHolder actionInfo = CyanTriggerActionInfoHolder.GetActionInfoHolder(_nodeDef);
            List<CyanTriggerActionVariableDefinition> variables =
                new List<CyanTriggerActionVariableDefinition>(actionInfo.GetBaseActionVariables(includeEventVariables));
            
            var argData = GetArgData(actionInstance);
            if (argData != null)
            {
                GetVariablesFromEventArgs(argData, variables);
            }

            return variables.ToArray();
        }

        private static void GetVariablesFromEventArgs(
            CyanTriggerEventArgData argData,
            List<CyanTriggerActionVariableDefinition> variables)
        {
            // Dummy just to get size correct.
            variables.Add(null);
            
            for (int index = 0; index < argData.variableNames.Length; ++index)
            {
                Type type = argData.variableTypes[index];
                CyanTriggerActionVariableDefinition variableDefinition = new CyanTriggerActionVariableDefinition
                {
                    type = new CyanTriggerSerializableType(type),
                    variableType =
                        CyanTriggerActionVariableTypeDefinition.VariableInput |
                        (argData.variableOuts[index]
                            ? CyanTriggerActionVariableTypeDefinition.VariableOutput
                            : CyanTriggerActionVariableTypeDefinition.Constant),
                    displayName = argData.variableNames[index],
                    udonName = argData.variableUdonNames[index],
                    defaultValue = new CyanTriggerSerializableObject(CyanTriggerPropertyEditor.GetDefaultForType(type))
                };
                
                variables.Add(variableDefinition);
            }
        }

        public static List<CyanTriggerActionVariableDefinition> GetVariablesFromEventArgs(CyanTriggerEventArgData argData)
        {
            List<CyanTriggerActionVariableDefinition> ret = new List<CyanTriggerActionVariableDefinition>();
            GetVariablesFromEventArgs(argData, ret);
            return ret;
        }
    }
}