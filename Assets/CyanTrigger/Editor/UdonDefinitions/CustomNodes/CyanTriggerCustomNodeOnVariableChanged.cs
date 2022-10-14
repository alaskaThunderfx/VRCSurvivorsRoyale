using System;
using System.Collections.Generic;
using UnityEditor;
using VRC.Udon.Compiler.Compilers;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeOnVariableChanged : 
        CyanTriggerCustomUdonEventNodeDefinition,
        ICyanTriggerCustomNodeCustomVariableOptions
    {
        public const string OnVariableChangedEventName = "Event_OnVariableChanged";
        private const string OldVariableDisplayName = "oldValue";

        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "OnVariableChanged",
            OnVariableChangedEventName,
            typeof(void),
            new[]
            {
                new UdonNodeParameter()
                {
                    name = "variable",
                    type = typeof(CyanTriggerVariable),
                    parameterType = UdonNodeParameter.ParameterType.IN
                },
                new UdonNodeParameter()
                {
                    name = OldVariableDisplayName,
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
            return CyanTriggerNodeDefinition.UdonDefinitionType.Event;
        }

        public override string GetDisplayName()
        {
            return NodeDefinition.name;
        }
        
        public override string GetDocumentationLink()
        {
            return CyanTriggerDocumentationLinks.OnVariableChangeNodeDocumentation;
        }
        
        public CyanTriggerEditorVariableOption[] GetCustomEditorVariableOptions(
            SerializedProperty variableProperties)
        {
            if (variableProperties.arraySize == 0)
            {
                return Array.Empty<CyanTriggerEditorVariableOption>();
            }

            CyanTriggerEditorVariableOption ret = 
                GetPrevVariableOptionFromData(variableProperties.GetArrayElementAtIndex(0));

            if (ret == null)
            {
                return Array.Empty<CyanTriggerEditorVariableOption>();
            }

            return new[]
            {
                ret
            };
        }
        
        // Used to help with editor and not needed to be exact :eyes:
        public override string GetBaseMethodName(SerializedProperty eventProperty)
        {
            SerializedProperty eventInstance =
                eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.eventInstance));
            SerializedProperty inputs = eventInstance.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
            SerializedProperty varProperty = inputs.GetArrayElementAtIndex(0);
            SerializedProperty varName =
                varProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));
            return GetVariableChangeEventName(varName.stringValue);
        }
        
        public override string GetBaseMethodName(CyanTriggerEvent evt)
        {
            return GetVariableChangeEventName(evt.eventInstance.inputs[0].name);
        }
        
        public override bool GetBaseMethod(
            CyanTriggerAssemblyProgram program,
            CyanTriggerActionInstance actionInstance,
            out CyanTriggerAssemblyMethod method)
        {
            var variable = program.Data.GetUserDefinedVariable(actionInstance.inputs[0].variableID);
            string methodName = GetVariableChangeEventName(variable.Name);
            bool created = program.Code.GetOrCreateMethod(methodName, true, out method);
            if (created)
            {
                method.AddActionsLast(
                    CyanTriggerAssemblyActionsUtils.CopyVariables(variable, variable.PreviousVariable));
            }
            return created;
        }

        public override CyanTriggerAssemblyMethod AddEventToProgram(CyanTriggerCompileState compileState)
        {
            return CyanTriggerCompiler.AddDefaultEventToProgram(
                compileState.Program, 
                compileState.ActionMethod);
        }
        
        public static HashSet<string> GetVariablesWithOnChangedCallback(CyanTriggerEvent[] events, out bool allValid)
        {
            allValid = true;
            HashSet<string> variablesWithCallbacks = new HashSet<string>();
            foreach (var trigEvent in events)
            {
                var eventInstance = trigEvent.eventInstance;
                if (eventInstance.actionType.directEvent == OnVariableChangedEventName)
                {
                    string varId = eventInstance.inputs[0].variableID;
                    if (string.IsNullOrEmpty(varId))
                    {
                        allValid = false;
                    }
                    variablesWithCallbacks.Add(varId);
                }
            }

            return variablesWithCallbacks;
        }

        public static string GetOldVariableName(string varName)
        {
            return UdonGraphCompiler.GetOldVariableName(varName);
        }

        public static string GetVariableChangeEventName(string varName)
        {
            return UdonGraphCompiler.GetVariableChangeEventName(varName);
        }

        
        // TODO fix all of this extra data, as it is way too hacky...
        public static void SetVariableExtraData(SerializedProperty eventInstance, CyanTriggerVariable[] variables)
        {
            SerializedProperty eventInputs =
                eventInstance.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
            SerializedProperty variableInstance = eventInputs.GetArrayElementAtIndex(0);

            if (variableInstance == null)
            {
                return;
            }

            SerializedProperty dataProperty =
                variableInstance.FindPropertyRelative(nameof(CyanTriggerVariable.data));
            
            string varName = 
                variableInstance.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name)).stringValue;
            
            string varId = 
                variableInstance.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID)).stringValue;
            
            string[] data = GetDataForVariable(varName, varId, variables);
            CyanTriggerSerializableObject.UpdateSerializedProperty(dataProperty, data);
        }

        private static string[] GetDataForVariable(string varName, string varId, CyanTriggerVariable[] variables)
        {
            string[] data = null;
            foreach (var variable in variables)
            {
                if (variable.IsDisplayOnly())
                {
                    continue;
                }
                
                if (variable.name == varName)
                {
                    string prevVar = GetOldVariableName(varName);
                    data = new[] {prevVar, variable.type.typeDef, varId};
                    break;
                }
            }

            return data;
        }

        public static CyanTriggerEditorVariableOption GetPrevVariableOptionFromData(SerializedProperty variableInstance)
        {
            SerializedProperty dataProperty =
                variableInstance.FindPropertyRelative(nameof(CyanTriggerVariable.data));

            string[] data = (string[]) CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataProperty);

            if (data == null)
            {
                return null;
            }

            Type varType = Type.GetType(data[1]);
            string oldVarName = data[0];
            string actualId = data[2];

            string guid = GetPrevVariableGuid(oldVarName, actualId);
            
            return new CyanTriggerEditorVariableOption
            {
                Name = OldVariableDisplayName,
                ID = guid,
                Type = varType,
                IsReadOnly = true,
            };
        }

        private const string IsPrevVariableTag = "IsPrev";
        public static string GetPrevVariableGuid(string oldVarName, string actualId)
        {
            string guid = CyanTriggerAssemblyDataGuidTags.AddVariableNameTag(oldVarName);
            guid = CyanTriggerAssemblyDataGuidTags.AddVariableIdTag(actualId, guid);
            guid = CyanTriggerAssemblyDataGuidTags.AddVariableGuidTag(IsPrevVariableTag, "true", guid);
            return guid;
        }
        
        public static bool IsPrevVariable(string name, string guid)
        {
            return name == OldVariableDisplayName && 
                   !string.IsNullOrEmpty(CyanTriggerAssemblyDataGuidTags.GetVariableGuidTag(guid, IsPrevVariableTag));
        }

        public static string GetMainVariableId(string guid)
        {
            return CyanTriggerAssemblyDataGuidTags.GetVariableId(guid);
        }

        public static void MigrateEvent(CyanTriggerActionInstance eventAction, CyanTriggerVariable[] variables)
        {
            if (eventAction.actionType.directEvent != OnVariableChangedEventName)
            {
                return;
            }

            if (eventAction.inputs.Length == 0)
            {
                return;
            }

            var input = eventAction.inputs[0];
            string[] data = GetDataForVariable(input.name, input.variableID, variables);
            eventAction.inputs[0].data = new CyanTriggerSerializableObject(data);
        }
    }
}
