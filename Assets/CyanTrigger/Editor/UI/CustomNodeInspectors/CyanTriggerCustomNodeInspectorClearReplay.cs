using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeInspectorClearReplay : 
        ICyanTriggerCustomNodeInspector,
        ICyanTriggerCustomNodeInspectorDisplayText
    {
        public string GetNodeDefinitionName()
        {
            return CyanTriggerCustomNodeClearReplay.FullName;
        }
        
        public string GetCustomActionGuid()
        {
            return "";
        }

        public bool HasCustomHeight(CyanTriggerActionInstanceRenderData actionInstanceRenderData)
        {
            return false;
        }

        public float GetHeightForInspector(CyanTriggerActionInstanceRenderData actionInstanceRenderData)
        {
            throw new NotImplementedException();
        }

        private static List<(string, string)> GetEventNames(CyanTriggerEvent[] events)
        {
            List<(string, string)> options = new List<(string, string)>();
            Dictionary<string, int> nameCount = new Dictionary<string, int>();
            for (var index = 0; index < events.Length; index++)
            {
                var evt = events[index];
                CyanTriggerActionInfoHolder info =
                    CyanTriggerActionInfoHolder.GetActionInfoHolder(evt.eventInstance.actionType);
                string name = info.GetEventCompiledName(evt);
                if (!nameCount.TryGetValue(name, out int count))
                {
                    count = 0;
                }
                ++count;
                nameCount[name] = count;

                var eventOptions = evt.eventOptions;
                if (eventOptions.broadcast == CyanTriggerBroadcast.All && eventOptions.replay != CyanTriggerReplay.None)
                {
                    string displayText = $"\"{name}\" ({count}) - {eventOptions.replay}";
                    options.Add((displayText, evt.eventId));
                }
            }

            return options;
        }
        
        public void RenderInspector(
            CyanTriggerActionInstanceRenderData actionInstanceRenderData,
            CyanTriggerActionVariableDefinition[] variableDefinitions, 
            Func<Type, List<CyanTriggerEditorVariableOption>> getVariableOptionsForType, 
            Rect rect, 
            bool layout)
        {
            var actionProperty = actionInstanceRenderData.Property;
            var inputListProperty = actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));

            List<(GUIContent, object)> content = new List<(GUIContent, object)>();

            foreach (var option in GetEventNames(actionInstanceRenderData.DataInstance.events))
            {
                content.Add((new GUIContent(option.Item1), option.Item2));
            }

            int actionIndex = 0;
            SerializedProperty inputProperty = inputListProperty.GetArrayElementAtIndex(actionIndex);
            CyanTriggerPropertyEditor.DrawActionVariableInstanceInputEditor(
                actionInstanceRenderData,
                actionIndex,
                inputProperty, 
                variableDefinitions[actionIndex],
                getVariableOptionsForType, 
                ref rect,
                layout,
                () => content);
        }

        public string GetCustomDisplayText(
            CyanTriggerActionInfoHolder actionInfo, 
            SerializedProperty actionProperty, 
            CyanTriggerDataInstance triggerData,
            bool withColor)
        {
            string eventName = "<Invalid>".Colorize(CyanTriggerColorTheme.Error, withColor);
            SerializedProperty inputsProperty =
                actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
            if (inputsProperty.arraySize > 0)
            {
                SerializedProperty eventInputProperty = inputsProperty.GetArrayElementAtIndex(0);
                SerializedProperty dataProperty =
                    eventInputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                var data = CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataProperty);

                if (data is string eventId)
                {
                    foreach (var options in GetEventNames(triggerData.events))
                    {
                        if (options.Item2 == eventId)
                        {
                            eventName = options.Item1.Colorize(CyanTriggerColorTheme.ValueLiteral, withColor);
                            break;
                        }
                    }
                }
            }
            return $"{actionInfo.GetActionRenderingDisplayName(withColor)}({eventName})";
        }
    }
}