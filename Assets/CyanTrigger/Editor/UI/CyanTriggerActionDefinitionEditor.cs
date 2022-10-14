using System;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Cyan.CT.Editor
{
    [CustomEditor(typeof(CyanTriggerActionGroupDefinition), true)]
    public class CyanTriggerActionDefinitionEditor : UnityEditor.Editor
    {
        private CyanTriggerActionGroupDefinition _definition;

        private SerializedProperty _exposedActionsProperty;
        private SerializedProperty _priorityProperty;
        private SerializedProperty _isMultiInstanceProperty;
        private SerializedProperty _namespaceProperty;
        
        
        private ReorderableList _exposedEventsList;
        private ReorderableList _variableList;
        private int _selectedEvent = -1;
        private int _selectedVariable = -1;

        private ReorderableList _variableArrayList;
        private bool _variableArrayExpand;

        private bool _isEditable;
        private bool _isMultiInstance = false;

        private bool _shouldUpdateGroupInfo = false;

        private bool _allowEditingProgramValues = false;
        private GUIStyle _helpBoxStyle;
        
        private void OnEnable()
        {
            // Ensure that the manager exists while working.
            var manager = CyanTriggerActionGroupDefinitionUtil.Instance;
            
            _definition = (CyanTriggerActionGroupDefinition)target;
            _definition.Initialize();
            
            _exposedActionsProperty = serializedObject.FindProperty(nameof(CyanTriggerActionGroupDefinition.exposedActions));
            _priorityProperty = serializedObject.FindProperty(nameof(CyanTriggerActionGroupDefinition.autoAddPriority));
            _isMultiInstanceProperty = serializedObject.FindProperty(nameof(CyanTriggerActionGroupDefinition.isMultiInstance));
            _namespaceProperty = serializedObject.FindProperty(nameof(CyanTriggerActionGroupDefinition.defaultNamespace));

            
            CreateActionList();
        }

        private void OnDisable()
        {
            UpdateActionGroup();
        }

        public override void OnInspectorGUI()
        {
            _helpBoxStyle = new GUIStyle(EditorStyles.helpBox);
            
            serializedObject.Update();
            
            _isEditable = _definition.IsEditorModifiable();

            EditorGUILayout.BeginVertical(_helpBoxStyle);
            
            if (GUILayout.Button("Custom Action Wiki"))
            {
                Application.OpenURL("https://github.com/CyanLaser/CyanTrigger/wiki/Custom-Actions");
            }
            
            EditorGUILayout.Space();
            
            EditorGUI.BeginDisabledGroup(!_isEditable);
            
            if (GUILayout.Button("Refresh Data"))
            {
                _definition.Initialize();
                CreateActionList();
                _shouldUpdateGroupInfo = true;
                UpdateActionGroup();
            }
            
            bool displayOptions = _definition.DisplayExtraEditorOptions(serializedObject);
            
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();

            _exposedEventsList.draggable = _exposedEventsList.displayAdd = _exposedEventsList.displayRemove = _isEditable;
            if (_variableList != null)
            {
                _variableList.draggable = _variableList.displayAdd = _variableList.displayRemove = _isEditable;
            }
            
            if (displayOptions)
            {
                DrawEvents();
            }

            UpdateActionGroup();
            if (_isEditable && (serializedObject.ApplyModifiedProperties() || GUI.changed))
            {
                _shouldUpdateGroupInfo = true;
            }
        }

        private void DelayApplyChanges()
        {
            serializedObject.ApplyModifiedProperties();
            _shouldUpdateGroupInfo = true;
        }

        private void UpdateActionGroup()
        {
            if (!_shouldUpdateGroupInfo || !_isEditable)
            {
                return;  
            }
            serializedObject.Update();
            CyanTriggerActionGroupDefinitionUtil.Instance.UpdateActionGroup(_definition);
            _shouldUpdateGroupInfo = false;
        }

        public void DrawEvents()
        {
            EditorGUILayout.BeginVertical(_helpBoxStyle);

            VerifyData(_exposedActionsProperty);
            
            string curNamespace = _namespaceProperty.stringValue;
            if (string.IsNullOrEmpty(curNamespace))
            {
                curNamespace = _definition.name;
            }
            
            EditorGUI.BeginDisabledGroup(!_isEditable);
            
            EditorGUI.BeginChangeCheck();
            string newNamespace = EditorGUILayout.TextField("Default Namespace", curNamespace);
            if (EditorGUI.EndChangeCheck())
            {
                _namespaceProperty.stringValue = newNamespace;

                if (curNamespace != newNamespace)
                {
                    // Go through all actions and update everything that matches.
                    int size = _exposedActionsProperty.arraySize;
                    for (int actionIndex = 0; actionIndex < size; ++actionIndex)
                    {
                        var curActionProp = _exposedActionsProperty.GetArrayElementAtIndex(actionIndex);
                        var namespaceProp = 
                            curActionProp.FindPropertyRelative(nameof(CyanTriggerActionDefinition.actionNamespace));
                        if (namespaceProp.stringValue == curNamespace)
                        {
                            namespaceProp.stringValue = newNamespace;
                        }
                    }
                }
            }

            EditorGUILayout.PropertyField(_priorityProperty, new GUIContent(_priorityProperty.displayName, "When events are set to auto add, how should these Custom Actions be prioritized? Lower numbers will be added first."));

            Rect instanceRect = EditorGUILayout.BeginHorizontal();
            GUILayout.Space(EditorGUIUtility.singleLineHeight);

            Rect labelRect = new Rect(instanceRect)
            {
                width = EditorGUIUtility.labelWidth
            };
            Rect instanceToggleRect = new Rect(instanceRect)
            {
                x = labelRect.xMax + 2,
                width = instanceRect.width - labelRect.width - 2
            };
            EditorGUI.LabelField(labelRect, "Instance Type");
            _isMultiInstance = _isMultiInstanceProperty.boolValue;
            int index = _isMultiInstance ? 1 : 0;
            GUIContent[] options = 
            {
                new GUIContent("Single-Instance", "Only one \"instance\" of this Custom Action per CyanTrigger. All actions will share the same data and can be added directly to a Trigger."),
                new GUIContent("Multi-Instance", "Multiple \"instances\" of this Custom Action can exist per CyanTrigger. Each instance will have its own data, but needs to be defined in the Variables section and each action will need to select the instance it is working with."),
            };
            int newIndex = GUI.Toolbar(instanceToggleRect, index, options);
            if (index != newIndex)
            {
                bool results = EditorUtility.DisplayDialog("Changing Instance Type", "Are you sure you want to change this Custom Action's instance type? Doing so will break all CyanTriggers using any of these actions! You will need to manually go back and edit these triggers.", "Yes", "No");
                if (results)
                {
                    _isMultiInstanceProperty.boolValue = _isMultiInstance = newIndex == 1;
                }
            }
            EditorGUILayout.EndHorizontal();

            _allowEditingProgramValues = EditorGUILayout.Toggle(new GUIContent("Edit Base Action Data", "When Actions and Variables are added, the base information is copied. These normally should not be edited to ensure the Custom Action works properly. In the case where the program itself is edited, then these values also need to be updated to match the changes."), _allowEditingProgramValues);
            if (_allowEditingProgramValues)
            {
                EditorGUILayout.HelpBox("Editing Base Action Data is enabled. Only modify data when the values have been changed in the program itself and need to be modified here to match!", MessageType.Warning);
            }
            
            _definition.DisplayExtraMethodOptions(serializedObject);
            
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.Space();

            _exposedEventsList.DoLayoutList();

            if (_selectedEvent >= _exposedEventsList.count)
            {
                _selectedEvent = -1;
            }

            if (_selectedEvent == -1 && _selectedVariable != -1)
            {
                _selectedVariable = -1;
                _variableList = null;

                _variableArrayExpand = true;
                _variableArrayList = null;
            }
            
            if (_selectedEvent != -1)
            {
                EditorGUILayout.Space();
                
                EditorGUILayout.BeginVertical(_helpBoxStyle);
                EditorGUI.BeginDisabledGroup(!_isEditable);
                
                SerializedProperty actionProperty = _exposedActionsProperty.GetArrayElementAtIndex(_selectedEvent);
                SerializedProperty nameProperty =
                    actionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.actionNamespace));
                SerializedProperty variantNameProperty =
                    actionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.actionVariantName));
                SerializedProperty descriptionProperty =
                    actionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.description));
                SerializedProperty baseEventProperty =
                    actionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.baseEventName));
                SerializedProperty entryEventProperty =
                    actionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.eventEntry));
                SerializedProperty variablesProperty =
                    actionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.variables));
                
                SerializedProperty autoAddProperty =
                    actionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.autoAdd));

                bool isVariableSet = entryEventProperty.stringValue ==
                                     CyanTriggerActionGroupDefinition.EmptyEntryEventName;

                if (isVariableSet)
                {
                    
                }
                
                // TODO check if this action has variable outputs and prevent if so. Otherwise, this should be fine to auto add.
                //EditorGUI.BeginDisabledGroup(isVariableSet);
                
                bool initialAutoAdd = autoAddProperty.boolValue;
                EditorGUILayout.PropertyField(autoAddProperty, 
                    new GUIContent("Auto Add Action", "Should this action be auto added when any actions in this group are used in a CyanTrigger? If auto add is set to true, this action cannot be selected in the action list and the default values will be used as input. Auto add should not be used if any input requires modifying variables."));
                bool shouldAutoAdd = autoAddProperty.boolValue;

                //EditorGUI.EndDisabledGroup();
                
                // When turning this to true, auto convert all inputs.
                if (shouldAutoAdd && initialAutoAdd != shouldAutoAdd && variablesProperty.arraySize > 0)
                {
                    bool results = EditorUtility.DisplayDialog("Set Action to Auto Add", "Are you sure you want to set this event to auto add? Doing so will convert all variables to hidden in the inspector. Make sure to update the default values.", "Yes", "No");
                    if (results)
                    {
                        ConvertVariablesToHidden(variablesProperty);
                    }
                    else
                    {
                        autoAddProperty.boolValue = false;
                    }
                }

                // TODO add warning if there are duplicate names
                EditorGUILayout.PropertyField(nameProperty);
                EditorGUILayout.PropertyField(variantNameProperty);
                EditorGUILayout.PropertyField(descriptionProperty);
                
                EditorGUI.BeginDisabledGroup(!_allowEditingProgramValues);
                if (_allowEditingProgramValues)
                {
                    EditorGUILayout.HelpBox("Ensure these values match the program's Event Type and Name!", MessageType.Warning);
                }
                EditorGUILayout.PropertyField(baseEventProperty);
                EditorGUILayout.PropertyField(entryEventProperty);
                EditorGUI.EndDisabledGroup();
                
                EditorGUI.EndDisabledGroup();
                
                if (_variableList == null)
                {
                    CreateVariableList(variablesProperty);
                }
                
                EditorGUILayout.Space();
                _variableList.DoLayoutList();
                
                if (_selectedVariable >= _variableList.count)
                {
                    _selectedVariable = -1;
                }
                
                if (_selectedVariable != -1)
                {
                    EditorGUILayout.Space();
                    
                    EditorGUILayout.BeginVertical(_helpBoxStyle);
                    EditorGUI.BeginDisabledGroup(!_isEditable);
                    
                    SerializedProperty variableProperty = variablesProperty.GetArrayElementAtIndex(_selectedVariable);
                    
                    SerializedProperty udonNameProperty =
                        variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.udonName));
                    SerializedProperty displayNameProperty =
                        variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.displayName));
                    SerializedProperty varDescriptionProperty =
                        variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.description));
                    SerializedProperty varTypeProperty =
                        variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.variableType));
                    SerializedProperty typeProperty =
                        variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.type));
                    SerializedProperty typeDefProperty =
                        typeProperty.FindPropertyRelative(nameof(CyanTriggerSerializableType.typeDef));
                    SerializedProperty defaultValueProperty =
                        variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.defaultValue));
                    
                    Type type = Type.GetType(typeDefProperty.stringValue);
                    bool isArgument = CyanTriggerAssemblyData.IsVarNameArg(udonNameProperty.stringValue);
                    
                    
                    EditorGUI.BeginDisabledGroup(!_allowEditingProgramValues);
                    if (_allowEditingProgramValues)
                    {
                        // TODO only show CyanTrigger warning for CyanTrigger programs.
                        EditorGUILayout.HelpBox("Ensure this value matches the program variable's name! For CyanTrigger arguments, it will be named \"_arg_(VariableName)_(EventName)\"", MessageType.Warning);
                    }
                    EditorGUILayout.PropertyField(udonNameProperty);
                    EditorGUI.EndDisabledGroup();
                    
                    EditorGUILayout.PropertyField(displayNameProperty);
                    EditorGUILayout.PropertyField(varDescriptionProperty);

                    bool isEvent = baseEventProperty.stringValue != CyanTriggerActionGroupDefinition.CustomEventName;
                    
                    CyanTriggerActionVariableTypeDefinition varTypes =
                        (CyanTriggerActionVariableTypeDefinition) varTypeProperty.intValue;

                    bool variableOutput = varTypes.HasFlag(CyanTriggerActionVariableTypeDefinition.VariableOutput);
                    bool hidden = varTypes.HasFlag(CyanTriggerActionVariableTypeDefinition.Hidden);
                    bool allowsMultiple = varTypes.HasFlag(CyanTriggerActionVariableTypeDefinition.AllowsMultiple);
                    
                    varTypes = CyanTriggerActionVariableTypeDefinition.None;

                    EditorGUI.BeginDisabledGroup(allowsMultiple || (variableOutput && isArgument) || shouldAutoAdd);
                    
                    hidden = EditorGUILayout.Toggle(
                        new GUIContent("Hidden in inspector",
                            "Check this if this variable will only ever use the default value. This is useful for making variants of an action using only different input parameters."), 
                        hidden || shouldAutoAdd);

                    EditorGUI.EndDisabledGroup();
                    
                    bool shouldDisableMultiInput = _selectedVariable != 0 || type.IsArray || (variableOutput && isEvent) || hidden;
                    allowsMultiple = allowsMultiple && !shouldDisableMultiInput;
                    
                    // Prevent showing multi input option when group action is multi instance.
                    if (!_isMultiInstance && _selectedVariable == 0)
                    {
                        EditorGUI.BeginDisabledGroup(shouldDisableMultiInput);

                        allowsMultiple = EditorGUILayout.Toggle(
                            new GUIContent("Repeat for Multiple Objects",
                                "This variable will be displayed as a list and the action will repeat itself for each item. This option is only available for the first variable slot and cannot be hidden. Array types are not supported at this time"),
                            allowsMultiple);

                        EditorGUI.EndDisabledGroup();
                    }

                    EditorGUI.BeginDisabledGroup(hidden || (allowsMultiple && isEvent) || (!_allowEditingProgramValues && isArgument));
                    
                    variableOutput = EditorGUILayout.Toggle(
                        new GUIContent("Modifies Variable",
                            "Check this if this variable will be modified in the action and the value stored into a user defined variable."), 
                        variableOutput);

                    EditorGUI.EndDisabledGroup();

                    // Constant and variable input value is now determined if variable output is checked.
                    bool constant;
                    bool variableInput;
                    
                    if (allowsMultiple)
                    {
                        hidden = false;
                    }
                    if (hidden)
                    {
                        constant = true;
                        variableInput = false;
                        variableOutput = false;
                        allowsMultiple = false;
                    }
                    else if (variableOutput)
                    {
                        constant = false;
                        variableInput = true;
                        allowsMultiple &= !isEvent; // Cannot have multiple while output.
                    }
                    else
                    {
                        constant = true;
                        variableInput = true;
                    }
                    
                    if (constant)
                    {
                        varTypes |= CyanTriggerActionVariableTypeDefinition.Constant;
                    }
                    if (variableInput)
                    {
                        varTypes |= CyanTriggerActionVariableTypeDefinition.VariableInput;
                    }
                    if (variableOutput)
                    {
                        varTypes |= CyanTriggerActionVariableTypeDefinition.VariableOutput;
                    }
                    if (hidden)
                    {
                        varTypes |= CyanTriggerActionVariableTypeDefinition.Hidden;
                    }
                    if (allowsMultiple)
                    {
                        varTypes |= CyanTriggerActionVariableTypeDefinition.AllowsMultiple;
                    }

                    varTypeProperty.intValue = (int) varTypes;
                    
                    EditorGUI.BeginDisabledGroup(!constant);
                    
                    if (type.IsArray)
                    {
                        CyanTriggerPropertyEditor.DrawArrayEditor(
                            defaultValueProperty, 
                            new GUIContent("Default Value"),
                            type, 
                            ref _variableArrayExpand, 
                            ref _variableArrayList);
                    }
                    else
                    {
                        CyanTriggerPropertyEditor.DrawEditor(
                            defaultValueProperty, 
                            Rect.zero, 
                            new GUIContent("Default Value"), 
                            type, 
                            true);
                    }
                    
                    EditorGUI.EndDisabledGroup();
                    
                    EditorGUI.EndDisabledGroup();
                    EditorGUILayout.EndVertical();
                }
                
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.EndVertical();
        }

        private void CreateActionList()
        {
            ReorderableList newList = new ReorderableList(serializedObject, _exposedActionsProperty, _isEditable, true, _isEditable, _isEditable);
            newList.drawHeaderCallback = (rect) => EditorGUI.LabelField(rect, "Actions");
            newList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                SerializedProperty actionProperty = _exposedActionsProperty.GetArrayElementAtIndex(index);
                SerializedProperty nameProperty =
                    actionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.actionNamespace));
                SerializedProperty variantNameProperty =
                    actionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.actionVariantName));
                SerializedProperty baseEventProperty =
                    actionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.baseEventName));
                SerializedProperty variablesProperty =
                    actionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.variables));

                string actionLabel = 
                    !baseEventProperty.stringValue.Equals("Event_Custom")
                    ? "Event"
                    : "Action";
                
                StringBuilder actionName = new StringBuilder();
                actionName.Append(actionLabel);
                actionName.Append(": ");
                actionName.Append(nameProperty.stringValue);
                actionName.Append(" .");
                actionName.Append(variantNameProperty.stringValue);
                actionName.Append(" (");

                int variableCount = variablesProperty.arraySize;
                for (int curVar = 0; curVar < variableCount; ++curVar)
                {
                    if (curVar > 0)
                    {
                        actionName.Append(", ");
                    }

                    SerializedProperty variableProperty = variablesProperty.GetArrayElementAtIndex(curVar);
                    string variableDisplayText = GetVariableDisplayText(variableProperty, false);
                    actionName.Append(variableDisplayText);
                }

                actionName.Append(")");

                string displayText = actionName.ToString();
                EditorGUI.LabelField(rect, new GUIContent(displayText, displayText));
                
                
                if (isActive)
                {
                    if (_selectedEvent != index)
                    {
                        _selectedEvent = index;
                        _selectedVariable = -1;
                        _variableList = null;

                        _variableArrayExpand = true;
                        _variableArrayList = null;
                    }
                }
                else if (_selectedEvent == index)
                {
                    _selectedEvent = -1;
                    
                    _selectedVariable = -1;
                    _variableList = null;

                    _variableArrayExpand = true;
                    _variableArrayList = null;
                }
            };
            newList.onAddCallback = (list) =>
            {
                _definition.AddNewEvent(_exposedActionsProperty, DelayApplyChanges);
            };
            newList.onReorderCallback = list =>
            {
                _shouldUpdateGroupInfo = true;
            };
            _exposedEventsList = newList;
        }

        private string GetVariableDisplayText(SerializedProperty variableProperty, bool longName = true)
        {
            SerializedProperty typeProperty =
                    variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.type));
            SerializedProperty typeDefProperty =
                typeProperty.FindPropertyRelative(nameof(CyanTriggerSerializableType.typeDef));
            SerializedProperty nameProperty =
                variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.displayName));

            SerializedProperty varTypeProperty =
                variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.variableType));
            CyanTriggerActionVariableTypeDefinition varTypes =
                (CyanTriggerActionVariableTypeDefinition)varTypeProperty.intValue;

            Type type = Type.GetType(typeDefProperty.stringValue);
            bool isOutput = (varTypes & CyanTriggerActionVariableTypeDefinition.VariableOutput) != 0;
            bool isHidden = (varTypes & CyanTriggerActionVariableTypeDefinition.Hidden) != 0;
            bool isMulti = (varTypes & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0;

            string typeName = CyanTriggerNameHelpers.GetTypeFriendlyName(type);
            string varName = nameProperty.stringValue;

            string multiText = isMulti 
                ? (longName ? "Multi " : "M ")
                : "";
            string hiddenText = isHidden 
                ? (longName ? "Hidden " : "H ")
                : "";
            string outputText = isOutput
                ? (longName ? "Out " : "O ")
                : "";

            return $"{multiText}{hiddenText}{outputText}{typeName} {varName}";
        }
        
        private void CreateVariableList(SerializedProperty variablesProperty)
        {
            ReorderableList newList = new ReorderableList(serializedObject, variablesProperty, _isEditable, true, _isEditable, _isEditable);
            newList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Variables");
            newList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                SerializedProperty variableProperty = variablesProperty.GetArrayElementAtIndex(index);

                string variableDisplayText = GetVariableDisplayText(variableProperty);
                EditorGUI.LabelField(rect, new GUIContent(variableDisplayText, variableDisplayText));
                
                if (isActive)
                {
                    if (_selectedVariable != index)
                    {
                        _selectedVariable = index;
                        _variableArrayExpand = true;
                        _variableArrayList = null;
                    }
                }
                else if (_selectedVariable == index)
                {
                    _selectedVariable = -1;
                    _variableArrayExpand = true;
                    _variableArrayList = null;
                }
            };
            newList.onAddCallback = list =>
            {
                _definition.AddNewVariable(_selectedEvent, variablesProperty, DelayApplyChanges);
                
                SerializedProperty actionProperty = _exposedActionsProperty.GetArrayElementAtIndex(_selectedEvent);
                SerializedProperty autoAddProperty =
                    actionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.autoAdd));
                if (autoAddProperty.boolValue)
                {
                    ConvertVariablesToHidden(variablesProperty);
                }
            };
            newList.onCanRemoveCallback = list =>
            {
                SerializedProperty variableProperty = variablesProperty.GetArrayElementAtIndex(list.index);
                SerializedProperty udonNameProperty =
                    variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.udonName));

                return _allowEditingProgramValues 
                       || !CyanTriggerAssemblyData.IsVarNameArg(udonNameProperty.stringValue);
            };
            newList.onReorderCallback = list =>
            {
                _shouldUpdateGroupInfo = true;
            };
            _variableList = newList;
        }

        private void ConvertVariablesToHidden(SerializedProperty variablesProperty)
        {
            int size = variablesProperty.arraySize;

            for (int index = 0; index < size; ++index)
            {
                SerializedProperty variableProperty = variablesProperty.GetArrayElementAtIndex(index);
                    
                SerializedProperty varTypeProperty =
                    variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.variableType));

                varTypeProperty.intValue = (int)(CyanTriggerActionVariableTypeDefinition.Hidden
                                                 | CyanTriggerActionVariableTypeDefinition.Constant);
            }
        }

        private void VerifyData(SerializedProperty exposedActionsProperty)
        {
            int autoAddCount = 0;
            int size = exposedActionsProperty.arraySize;
            for (int curEvent = 0; curEvent < size; ++curEvent)
            {
                SerializedProperty actionProperty = exposedActionsProperty.GetArrayElementAtIndex(curEvent);
                SerializedProperty autoAddProperty =
                    actionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.autoAdd));

                if (autoAddProperty.boolValue)
                {
                    ++autoAddCount;

                    SerializedProperty variablesProperty =
                        actionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.variables));

                    int varSize = variablesProperty.arraySize;
                    for (int curVar = 0; curVar < varSize; ++curVar)
                    {
                        SerializedProperty variableProperty = variablesProperty.GetArrayElementAtIndex(curVar);
                        SerializedProperty varTypeProperty =
                            variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.variableType));

                        CyanTriggerActionVariableTypeDefinition varTypeDef =
                            (CyanTriggerActionVariableTypeDefinition)varTypeProperty.intValue;

                        if (varTypeDef != (CyanTriggerActionVariableTypeDefinition.Hidden
                                            | CyanTriggerActionVariableTypeDefinition.Constant))
                        {
                            EditorGUILayout.HelpBox($"Event[{curEvent}] Variable[{curVar}]: Variable not set to hidden for auto added action", MessageType.Error);
                        }
                    }
                }
            }

            if (!_isMultiInstance && size > 0 && autoAddCount == size)
            {
                EditorGUILayout.HelpBox("All actions are set to Auto Add! Without an action that can be added by the user, no actions can ever be added.", MessageType.Error);
            }
        }
    }
}
