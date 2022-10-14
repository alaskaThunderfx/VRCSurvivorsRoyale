using System;
using System.Reflection;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeClearReplay : 
        CyanTriggerCustomUdonActionNodeDefinition,
        ICyanTriggerCustomNodeValidator,
        ICyanTriggerCustomNodeCustomHash,
        ICyanTriggerCustomNodeCustomVariableSettings
    {
        public const string FullName = "CyanTriggerSpecial_ClearReplay";
        
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "ClearReplay",
            FullName,
            typeof(CyanTrigger),
            new []
            {
                new UdonNodeParameter
                {
                    name = "Event Replay",
                    type = typeof(string),
                    parameterType = UdonNodeParameter.ParameterType.IN,
                }
            },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<object>(),
            true
        );
        
        public static readonly CyanTriggerActionVariableDefinition[] VariableDefinitions =
        {
            new CyanTriggerActionVariableDefinition
            {
                type = new CyanTriggerSerializableType(typeof(string)),
                udonName = "EventReplay",
                displayName = "Event Replay", 
                description = "Event to clear Replay count. The event will act for late joiners as if it was never executed.",
                variableType = CyanTriggerActionVariableTypeDefinition.Constant
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

        public override CyanTriggerNodeDefinition.UdonDefinitionType GetDefinitionType()
        {
            return CyanTriggerNodeDefinition.UdonDefinitionType.CyanTriggerSpecial;
        }

        public override string GetDisplayName()
        {
            return "ClearReplay";
        }
        
        public override string GetDocumentationLink()
        {
            return CyanTriggerDocumentationLinks.ClearReplayNodeDocumentation;
        }

        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            object data = compileState.ActionInstance.inputs[0].data.Obj;
            if (!(data is string eventValue))
            {
                compileState.LogError($"Invalid event value: {data}");
                return;
            }

            var actionMethod = compileState.ActionMethod;
            var replayData = compileState.ReplayData;
            var replayVar = replayData.GetEventReplayVariable(eventValue);
            actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.ReplayClearEventCount(
                compileState.Program,
                replayVar,
                replayData));
        }

        public CyanTriggerErrorType Validate(
            CyanTriggerActionInstance actionInstance, 
            CyanTriggerDataInstance triggerData,
            ref string message)
        {
            object data = actionInstance.inputs[0].data.Obj;

            if (!(data is string eventValue))
            {
                message = $"Data is not valid event info: {data}";
                return CyanTriggerErrorType.Error;
            }

            var events = triggerData.events;
            
            int index;
            for (index = 0; index < events.Length; ++index)
            {
                if (events[index].eventId == eventValue)
                {
                    break;
                }
            }

            if (index == events.Length)
            {
                message = $"EventId could not be found: eventId: {eventValue}";
                return CyanTriggerErrorType.Error;
            }

            var eventOptions = events[index].eventOptions;
            if (eventOptions.broadcast != CyanTriggerBroadcast.All || eventOptions.replay == CyanTriggerReplay.None)
            {
                message = "Event to clear does not have Replay";
                return CyanTriggerErrorType.Error;
            }
            
            return CyanTriggerErrorType.None;
        }

        public string GetCustomHash(CyanTriggerActionInstance actionInstance)
        {
            object data = actionInstance.inputs[0].data.Obj;
            if (data is string eventValue)
            {
                return $"ClearEvent: {eventValue}";
            }

            return "";
        }
    }
}