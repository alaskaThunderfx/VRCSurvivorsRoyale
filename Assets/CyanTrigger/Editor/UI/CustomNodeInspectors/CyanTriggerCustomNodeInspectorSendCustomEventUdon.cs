using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeInspectorSendCustomEventUdon : ICyanTriggerCustomNodeInspector
    {
        public string GetNodeDefinitionName()
        {
            return CyanTriggerCustomNodeSendCustomEventUdon.FullName;
        }
        
        public string GetCustomActionGuid()
        {
            return "";
        }

        public bool HasCustomHeight(CyanTriggerActionInstanceRenderData actionInstanceRenderData)
        {
            return true;
        }
        
        public float GetHeightForInspector(CyanTriggerActionInstanceRenderData actionInstanceRenderData)
        {
            bool shouldDisplayEditVariables = false;
            int variableCount = 0;
            
            if (actionInstanceRenderData.ActionInfo
                    .CustomDefinition is CyanTriggerCustomNodeSendCustomEventUdon udonSendEvent)
            {
                var actionProperty = actionInstanceRenderData.Property;
                var inputListProperty = actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
                variableCount = udonSendEvent.GetNodeDefinition().parameters.Count;

                if (inputListProperty.arraySize > variableCount)
                {
                    SerializedProperty extraVarDataProp = inputListProperty.GetArrayElementAtIndex(variableCount);
                    SerializedProperty editVarProp =
                        extraVarDataProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
                    shouldDisplayEditVariables = editVarProp.boolValue;
                }
            }

            // Provide height as it would have been normally
            if (!shouldDisplayEditVariables)
            {
                return CyanTriggerPropertyEditor.GetHeightForActionInstanceInputEditors(actionInstanceRenderData, false);
            }

            // Calculate height of multi-input and height of parameter editor
            float height = CyanTriggerPropertyEditor.GetHeightForActionVariableInstanceMultiInputEditor(
                actionInstanceRenderData.VariableDefinitions[0].type.Type,
                actionInstanceRenderData.ExpandedInputs[0],
                actionInstanceRenderData.InputLists[0]);

            if (actionInstanceRenderData.InputLists.Length > variableCount
                && actionInstanceRenderData.InputLists[variableCount] != null)
            {
                height += actionInstanceRenderData.InputLists[variableCount].GetHeight()
                          + actionInstanceRenderData.InputLists[variableCount].footerHeight;
            }
            
            return height;
        }

        public void RenderInspector(
            CyanTriggerActionInstanceRenderData actionInstanceRenderData,
            CyanTriggerActionVariableDefinition[] variableDefinitions,
            Func<Type, List<CyanTriggerEditorVariableOption>> getVariableOptionsForType,
            Rect rect,
            bool layout)
        {
            RenderInspector(
                actionInstanceRenderData, 
                variableDefinitions, 
                getVariableOptionsForType, 
                rect, 
                layout, 
                1,
                true,
                false);
        }


        public static void RenderInspector(
            CyanTriggerActionInstanceRenderData actionInstanceRenderData,
            CyanTriggerActionVariableDefinition[] variableDefinitions,
            Func<Type, List<CyanTriggerEditorVariableOption>> getVariableOptionsForType,
            Rect rect,
            bool layout,
            int eventsIndex,
            bool acceptsParameters,
            bool isNetworked)
        {
            var actionProperty = actionInstanceRenderData.Property;
            var inputListProperty = actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
            
            var multiVarDef = variableDefinitions[0];
            var multiInputListProperty = 
                actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.multiInput));
            
            Rect inputRect = new Rect(rect);
            
            // Render UdonBehaviour multi-input editor
            CyanTriggerPropertyEditor.DrawActionVariableInstanceMultiInputEditor(
                actionInstanceRenderData,
                0,
                multiInputListProperty, 
                multiVarDef,
                getVariableOptionsForType, 
                ref inputRect,
                layout);

            rect.y += inputRect.height + 5;
            rect.height -= inputRect.height + 5;
            
            Rect multiInputRect = inputRect;
            Rect afterMultiInputRect = rect;
            
            SerializedProperty eventProperty = inputListProperty.GetArrayElementAtIndex(eventsIndex);
            SerializedProperty eventIsVariableProperty =
                eventProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
            SerializedProperty eventDataProp =
                eventProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
            
            int variableCount = variableDefinitions.Length;
            bool displayEditVariables = false;
            if (actionInstanceRenderData.ActionInfo
                    .CustomDefinition is CyanTriggerCustomNodeSendCustomEventUdon udonSendEvent)
            {
                variableCount = udonSendEvent.GetNodeDefinition().parameters.Count;

                if (inputListProperty.arraySize > variableCount)
                {
                    SerializedProperty extraVarDataProp = inputListProperty.GetArrayElementAtIndex(variableCount);
                    SerializedProperty editVarProp =
                        extraVarDataProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
                    displayEditVariables = editVarProp.boolValue;
                }
            }
            else
            {
                udonSendEvent = null;
            }

            if (displayEditVariables)
            {
                ShowEditParametersButton(actionInstanceRenderData, true, multiInputRect, variableCount, inputListProperty);
                
                // TODO properly get and check this data.
                string eventNameData = (string)CyanTriggerSerializableObject.ObjectFromSerializedProperty(eventDataProp);
                
                ShowEditArguments(
                    actionInstanceRenderData, 
                    udonSendEvent, 
                    variableCount, 
                    actionProperty,
                    udonSendEvent.GetArgData(actionProperty), 
                    eventNameData,
                    afterMultiInputRect);
                return;
            }

            Dictionary<string, CyanTriggerEventArgData> eventsToArgs = ShowEventSelectorAndOptions(
                actionInstanceRenderData, 
                variableDefinitions, 
                getVariableOptionsForType,
                ref rect, 
                layout, 
                eventsIndex, 
                acceptsParameters, 
                isNetworked,
                variableCount,
                inputListProperty,
                multiInputListProperty,
                eventIsVariableProperty);

            if (!acceptsParameters)
            {
                return;
            }
            
            if (udonSendEvent == null)
            {
                return;
            }

            void ResetInputPropSize()
            {
                if (inputListProperty.arraySize == variableCount)
                {
                    return;
                }
                
                inputListProperty.arraySize = variableCount;
                actionInstanceRenderData.UpdateVariableSize();
                actionInstanceRenderData.NeedsRedraws = true;
            }
            
            if (eventIsVariableProperty.boolValue)
            {
                ResetInputPropSize();
                return;
            }

            // Event Name Data is invalid or empty
            object data = CyanTriggerSerializableObject.ObjectFromSerializedProperty(eventDataProp);
            if (!(data is string eventName) || string.IsNullOrEmpty(eventName))
            {
                ResetInputPropSize();
                return;
            }

            void UpdateSavedArgData(CyanTriggerEventArgData newData, CyanTriggerEventArgData oldData)
            {
                udonSendEvent.SetArgData(actionProperty, newData, oldData);
                actionInstanceRenderData.UpdateVariableSize();
                actionInstanceRenderData.NeedsRedraws = true;
                variableDefinitions = actionInstanceRenderData.VariableDefinitions;
            }
            
            // If current eventArgData is null, but previous data exists and we have the same event name,
            // use previous argData as this may be a program asset that can't get any event data
            var prevArgData = udonSendEvent.GetArgData(actionProperty);
            bool containsEventData = eventsToArgs.TryGetValue(eventName, out var eventArgData);
            bool usePrevEventData = false;
            if (!containsEventData && (prevArgData?.eventName == eventName || eventsToArgs.Count == 0))
            {
                containsEventData = true;
                usePrevEventData = true;
                eventArgData = prevArgData;

                // Ensure argument data always exists and has proper event name. 
                if (eventArgData == null)
                {
                    eventArgData = new CyanTriggerEventArgData
                    {
                        eventName = eventName
                    };
                    UpdateSavedArgData(eventArgData, eventArgData);
                }
                else if (eventArgData.eventName != eventName)
                {
                    eventArgData.eventName = eventName;
                    UpdateSavedArgData(eventArgData, eventArgData);
                }
                
                // We do not have information about the event. Provide the option to manually add parameters.
                ShowEditParametersButton(actionInstanceRenderData, false, multiInputRect, variableCount, inputListProperty);
            }
            
            // Couldn't find the event and previous did not match. 
            if (!containsEventData)
            {
                // Event list had data, but we didn't have a match. Reset the entire event data.
                if (eventsToArgs.Count != 0)
                {
                    ResetInputPropSize();
                }
                return;
            }

            // Check if event and inputs change at all and update set data as well as variable definitions
            if (!usePrevEventData && !eventArgData.Equals(prevArgData))
            {
                UpdateSavedArgData(eventArgData, prevArgData);
            }

            // Render parameter editors
            int argCount = eventArgData.variableNames.Length;
            for (int index = 0; index < argCount; ++index)
            {
                int curInput = variableDefinitions.Length - argCount + index;
                var variableDef = variableDefinitions[curInput];
                
                inputRect = new Rect(rect);

                SerializedProperty inputProperty = inputListProperty.GetArrayElementAtIndex(curInput);
                CyanTriggerPropertyEditor.DrawActionVariableInstanceInputEditor(
                    actionInstanceRenderData,
                    curInput,
                    inputProperty, 
                    variableDef,
                    getVariableOptionsForType, 
                    ref inputRect,
                    layout,
                    null);

                rect.y += inputRect.height + 5;
                rect.height -= inputRect.height + 5;
            }
        }

        private static Dictionary<string, CyanTriggerEventArgData> ShowEventSelectorAndOptions(
            CyanTriggerActionInstanceRenderData actionInstanceRenderData,
            CyanTriggerActionVariableDefinition[] variableDefinitions,
            Func<Type, List<CyanTriggerEditorVariableOption>> getVariableOptionsForType,
            ref Rect rect,
            bool layout,
            int eventsIndex,
            bool acceptsParameters,
            bool isNetworked,
            int variableCount,
            SerializedProperty inputListProperty,
            SerializedProperty multiInputListProperty,
            SerializedProperty eventIsVariableProperty)
        {
            var optionContent = new List<(GUIContent, object)>();
            Dictionary<string, CyanTriggerEventArgData> eventsToArgs = 
                new Dictionary<string, CyanTriggerEventArgData>();
            
            // Go through all Udon input in multi-input to get a list of Event Argument options to pick from.
            // If event is set to variable, do not check udon for event arguments.
            if (!eventIsVariableProperty.boolValue)
            {
                // TODO provide option to set program type instead of always searching?
                
                var udonTargets = CyanTriggerCustomNodeInspectorUtil.GetTypeFromMultiInput<UdonBehaviour>(
                    multiInputListProperty, 
                    actionInstanceRenderData.DataInstance.variables,  
                    actionInstanceRenderData.UdonBehaviour,
                    out bool containsSelf);
            
                // Get list of events from inputs.
                var options = CyanTriggerCustomNodeInspectorUtil.GetEventOptions(
                    actionInstanceRenderData.DataInstance, 
                    udonTargets, 
                    containsSelf,
                    acceptsParameters,
                    isNetworked);
            
                options.Sort((data1, data2) => String.Compare(data1.eventName, data2.eventName, StringComparison.Ordinal));
                
                foreach (var evt in options)
                {
                    optionContent.Add((new GUIContent(evt.GetUniqueString(false)), evt.eventName));
                    eventsToArgs.Add(evt.eventName, evt);
                }
            }

            for (int curInput = 1; curInput < variableCount; ++curInput)
            {
                CyanTriggerActionVariableDefinition variableDefinition = variableDefinitions[curInput];
                
                Rect inputRect = new Rect(rect);
                
                Func<List<(GUIContent, object)>> getOptionsInput = null;
                if (curInput == eventsIndex && optionContent.Count > 0)
                {
                    getOptionsInput = () => optionContent;
                }
                
                SerializedProperty inputProperty = inputListProperty.GetArrayElementAtIndex(curInput);
                CyanTriggerPropertyEditor.DrawActionVariableInstanceInputEditor(
                    actionInstanceRenderData,
                    curInput,
                    inputProperty, 
                    variableDefinition,
                    getVariableOptionsForType, 
                    ref inputRect,
                    layout,
                    getOptionsInput);

                rect.y += inputRect.height + 5;
                rect.height -= inputRect.height + 5;
            }

            return eventsToArgs;
        }

        private static void ShowEditParametersButton(
            CyanTriggerActionInstanceRenderData actionInstanceRenderData,
            bool isEditing, 
            Rect multiInputRect, 
            int variableCount, 
            SerializedProperty inputListProperty)
        {
            // Don't show Edit Parameters button while input list is not expanded.
            // This is to prevent covering the bar to blocking the ability to expand it again.
            if (!actionInstanceRenderData.ExpandedInputs[0])
            {
                return;
            }
            
            Rect editVarButtonRect = new Rect(
                multiInputRect.x,
                multiInputRect.yMax - EditorGUIUtility.singleLineHeight, 
                110, 
                EditorGUIUtility.singleLineHeight);
                
            if (GUI.Button(editVarButtonRect, isEditing ? "Finish Editing" : "Edit Parameters"))
            {
                SerializedProperty extraVarDataProp = inputListProperty.GetArrayElementAtIndex(variableCount);
                SerializedProperty editVarProp =
                    extraVarDataProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
                editVarProp.boolValue = !editVarProp.boolValue;
                actionInstanceRenderData.NeedsRedraws = true;
            }
        }
        
        private static void ShowEditArguments(
            CyanTriggerActionInstanceRenderData actionInstanceRenderData,
            CyanTriggerCustomNodeSendCustomEventUdon udonSendEvent,
            int variableCount,
            SerializedProperty actionProperty,
            CyanTriggerEventArgData eventArgData,
            string eventName,
            Rect rect)
        {
            void UpdateVariableData()
            {
                // update saved data
                udonSendEvent.SetArgData(actionProperty, eventArgData, eventArgData);
                actionInstanceRenderData.UpdateVariableSize();
                actionInstanceRenderData.NeedsRedraws = true;
                actionProperty.serializedObject.ApplyModifiedProperties();
            }

            ReorderableList list = actionInstanceRenderData.InputLists[variableCount];
            if (list == null)
            {
                list = new ReorderableList(eventArgData.variableNames, typeof(string), true, false, true, true);
                list.headerHeight = 1;
                list.onAddCallback = reorderableList =>
                {
                    void AddVariable(UdonNodeDefinition udonNodeDefinition)
                    {
                        int last = eventArgData.variableNames.Length;
                        int size = last + 1;
                        Array.Resize(ref eventArgData.variableNames, size);
                        Array.Resize(ref eventArgData.variableUdonNames, size);
                        Array.Resize(ref eventArgData.variableTypes, size);
                        Array.Resize(ref eventArgData.variableOuts, size);
                        
                        Type type = udonNodeDefinition.type;
                        string varName = eventArgData.variableNames[last] = CyanTriggerNameHelpers.GetTypeFriendlyName(type);
                        eventArgData.variableUdonNames[last] = CyanTriggerAssemblyData.CreateCustomEventArgName(eventName, varName);
                        eventArgData.variableTypes[last] = type;
                        eventArgData.variableOuts[last] = false;

                        UpdateVariableData();
                    }
                    CyanTriggerSearchWindowManager.Instance.DisplayVariableSearchWindow(AddVariable);
                };
                list.onRemoveCallback = reorderableList =>
                {
                    int size = eventArgData.variableNames.Length;
                    for (int index = reorderableList.index + 1; index < size; ++index)
                    {
                        eventArgData.variableNames[index - 1] = eventArgData.variableNames[index];
                        eventArgData.variableUdonNames[index- 1] = eventArgData.variableUdonNames[index];
                        eventArgData.variableTypes[index- 1] = eventArgData.variableTypes[index];
                        eventArgData.variableOuts[index- 1] = eventArgData.variableOuts[index];
                    }

                    --size;
                    Array.Resize(ref eventArgData.variableNames, size);
                    Array.Resize(ref eventArgData.variableUdonNames, size);
                    Array.Resize(ref eventArgData.variableTypes, size);
                    Array.Resize(ref eventArgData.variableOuts, size);
                    
                    UpdateVariableData();
                };
                list.onReorderCallbackWithDetails = (reorderableList, index, newIndex) =>
                {
                    (eventArgData.variableNames[index], eventArgData.variableNames[newIndex]) =
                        (eventArgData.variableNames[newIndex], eventArgData.variableNames[index]);
                    
                    (eventArgData.variableUdonNames[index], eventArgData.variableUdonNames[newIndex]) =
                        (eventArgData.variableUdonNames[newIndex], eventArgData.variableUdonNames[index]);
                    
                    (eventArgData.variableTypes[index], eventArgData.variableTypes[newIndex]) =
                        (eventArgData.variableTypes[newIndex], eventArgData.variableTypes[index]);
                        
                    (eventArgData.variableOuts[index], eventArgData.variableOuts[newIndex]) =
                        (eventArgData.variableOuts[newIndex], eventArgData.variableOuts[index]);
                    
                    UpdateVariableData();
                };
                list.drawElementCallback = (listRect, index, active, focused) =>
                {
                    float spaceBetween = 5;
                    
                    // type, name input, input/output toggle
                    Rect nameRect = new Rect(listRect);
                    nameRect.width /= 2;
                    listRect.xMin += nameRect.width;
                    nameRect.width -= spaceBetween;

                    Rect typeRect = new Rect(listRect);
                    typeRect.width /= 2;
                    listRect.xMin += typeRect.width;
                    typeRect.width -= spaceBetween;
                    
                    Rect inOutRect = new Rect(listRect);

                    bool changes = false;
                    // Render name
                    string nameOrig = eventArgData.variableNames[index];
                    string name = EditorGUI.TextField(nameRect, nameOrig);
                    name = CyanTriggerNameHelpers.SanitizeName(name);
                    string expectedUdonName = 
                        CyanTriggerAssemblyData.CreateCustomEventArgName(eventName, name);
                    if (nameOrig != name || expectedUdonName != eventArgData.variableUdonNames[index])
                    {
                        eventArgData.variableNames[index] = name;
                        eventArgData.variableUdonNames[index] = expectedUdonName;
                        changes = true;
                    }

                    // Render type
                    Type type = eventArgData.variableTypes[index];
                    string typeName = "<Invalid>";
                    string typeTooltip = $"Type is Invalid! {type}";
                    if (type != null)
                    {
                        typeName = CyanTriggerNameHelpers.GetTypeFriendlyName(type);
                        typeTooltip = type.FullName;
                    }
                    EditorGUI.LabelField(typeRect, new GUIContent(typeName, typeTooltip));
                    
                    // Input Output option
                    int selected = eventArgData.variableOuts[index] ? 1 : 0;
                    GUIContent[] options =
                    {
                        new GUIContent("Input", ""), 
                        new GUIContent("Output", "")
                    };
                    int newSelected = EditorGUI.Popup(inOutRect, GUIContent.none, selected, options);
                    if (newSelected != selected)
                    {
                        eventArgData.variableOuts[index] = 1 == newSelected;
                        changes = true;
                    }

                    if (changes)
                    {
                        UpdateVariableData();
                    }
                };
                
                actionInstanceRenderData.InputLists[variableCount] = list;
                actionInstanceRenderData.NeedsRedraws = true;
            }

            list.DoList(rect);
        }
    }
}