using System;
using UnityEditor;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeSendCustomEvent : 
        CyanTriggerCustomUdonActionNodeDefinition, 
        ICyanTriggerCustomNodeCustomVariableSettings,
        ICyanTriggerCustomNodeCustomVariableInitialization,
        ICyanTriggerCustomNodeCustomHash,
        ICyanTriggerCustomNodeValidator
    {
        public const string FullName = "CyanTrigger.__SendCustomEvent__CyanTrigger__SystemString";
        
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "CyanTrigger SendCustomEvent",
            FullName,
            typeof(CyanTrigger),
            new[]
            {
                new UdonNodeParameter()
                {
                    name = "instance", 
                    type = typeof(CyanTrigger),
                    parameterType = UdonNodeParameter.ParameterType.IN
                },
                new UdonNodeParameter()
                {
                    name = "name", 
                    type = typeof(string),
                    parameterType = UdonNodeParameter.ParameterType.IN
                }
            },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<object>(),
            true
        );

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
        
        public string GetCustomHash(CyanTriggerActionInstance actionInstance)
        {
            bool local = false;
            foreach (var var in actionInstance.multiInput)
            {
                if (var.isVariable && var.variableID == CyanTriggerAssemblyDataConsts.ThisCyanTrigger.ID)
                {
                    local = true;
                    break;
                }
            }
            
            if (local)
            {
                return $"CustomNamed: {actionInstance.inputs[1].data.Obj}";
            }

            return "";
        }

        public CyanTriggerErrorType Validate(
            CyanTriggerActionInstance actionInstance, 
            CyanTriggerDataInstance triggerData, 
            ref string message)
        {
            string eventName = actionInstance.inputs[1].data?.Obj as string;
            if (string.IsNullOrEmpty(eventName))
            {
                message = "Event is empty";
                return CyanTriggerErrorType.Warning;
            }

            // Check if event exists on the cyantrigger.
            bool local = false;
            foreach (var var in actionInstance.multiInput)
            {
                if (var.isVariable && var.variableID == CyanTriggerAssemblyDataConsts.ThisCyanTrigger.ID)
                {
                    local = true;
                    break;
                }
            }

            if (!local)
            {
                return CyanTriggerErrorType.None;
            }

            foreach (var evt in triggerData.events)
            {
                var eventInfo = CyanTriggerActionInfoHolder.GetActionInfoHolder(evt.eventInstance.actionType);
                if (eventInfo.GetEventCompiledName(evt) == eventName)
                {
                    return CyanTriggerErrorType.None;
                }
            }
            
            message = $"This CyanTrigger does not have the event \"{eventName}\"";
            return CyanTriggerErrorType.Error;
        }
        
        public static readonly CyanTriggerActionVariableDefinition[] VariableDefinitions =
        {
            new CyanTriggerActionVariableDefinition
            {
                type = new CyanTriggerSerializableType(typeof(CyanTrigger)),
                udonName = "instance",
                displayName = "CyanTrigger", 
                variableType = CyanTriggerActionVariableTypeDefinition.Constant |
                               CyanTriggerActionVariableTypeDefinition.VariableInput |
                               CyanTriggerActionVariableTypeDefinition.AllowsMultiple
            },
            new CyanTriggerActionVariableDefinition
            {
                type = new CyanTriggerSerializableType(typeof(string)),
                udonName = "name",
                displayName = "Custom Name", 
                variableType = CyanTriggerActionVariableTypeDefinition.Constant
            }
        };
        
        public override UdonNodeDefinition GetNodeDefinition()
        {
            return NodeDefinition;
        }
        
        public CyanTriggerActionVariableDefinition[] GetCustomVariableSettings()
        {
            return VariableDefinitions;
        }
        
        public void InitializeVariableProperties(
            SerializedProperty inputsProperty, 
            SerializedProperty multiInputsProperty)
        {
            var thisCyanTrigger = CyanTriggerAssemblyDataConsts.ThisCyanTrigger;
            
            multiInputsProperty.arraySize = 1;
            SerializedProperty inputNameProperty = multiInputsProperty.GetArrayElementAtIndex(0);
            SerializedProperty nameProperty =
                inputNameProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));
            nameProperty.stringValue = thisCyanTrigger.Name;
            SerializedProperty guidProperty =
                inputNameProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
            guidProperty.stringValue = thisCyanTrigger.ID;
            SerializedProperty isVariableProperty =
                inputNameProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
            isVariableProperty.boolValue = true;
                
            multiInputsProperty.serializedObject.ApplyModifiedProperties();
        }

        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            var actionInstance = compileState.ActionInstance;
            var actionMethod = compileState.ActionMethod;
            var program = compileState.Program;
            
            string eventName = actionInstance.inputs[1].data?.Obj as string;
            if (string.IsNullOrEmpty(eventName))
            {
                compileState.LogWarning("CyanTrigger.SendCustomEvent cannot have an empty event!");
                return;
            }
            
            var eventNameVariable =
                compileState.GetDataFromVariableInstance(-1, 1, actionInstance.inputs[1], typeof(string), false);
            
            for (int curMulti = 0; curMulti < actionInstance.multiInput.Length; ++curMulti)
            {
                var variable = actionInstance.multiInput[curMulti];

                // Jump to self. Optimize and jump directly to the method
                if (variable.isVariable && variable.variableID == CyanTriggerAssemblyDataConsts.ThisCyanTrigger.ID)
                {
                    actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.JumpToFunction(
                        program, 
                        eventName));
                    continue;
                }
                
                actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.SendCustomEvent(
                    program,
                    compileState.GetDataFromVariableInstance(curMulti, 0, variable, typeof(CyanTrigger), false),
                    eventNameVariable));
            }
        }
    }
}

