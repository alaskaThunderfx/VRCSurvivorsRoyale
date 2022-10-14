using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Editor.ProgramSources;
using Object = UnityEngine.Object;

namespace Cyan.CT.Editor
{
    [CreateAssetMenu(menuName = "CyanTrigger/CyanTrigger Custom Action", fileName = "New CyanTrigger Custom Action", order = 6)]
    [HelpURL(CyanTriggerDocumentationLinks.CustomAction)]
    public class CyanTriggerActionGroupDefinitionUdonAsset : CyanTriggerActionGroupDefinition
    {
        public UdonProgramAsset udonProgramAsset;
        
        [SerializeField] 
        private string assetGuid;
        [SerializeField]
        private string thisGuid;

        private CyanTriggerEventArgData[] _eventNames;
        private (string, Type)[] _variables;


        // Verify this asset's guid to know if it is new or a duplicate
        // and if all actions on this should generate new guids.
        public bool VerifyThisGuid()
        {
            bool dirty = false;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(this, out string newGuid, out long _);
            if (string.IsNullOrEmpty(thisGuid))
            {
                thisGuid = newGuid;
                dirty = true;
            }
            else if (thisGuid != newGuid)
            {
                thisGuid = newGuid;
                dirty = true;
                
#if CYAN_TRIGGER_DEBUG
                Debug.LogWarning($"Found CustomAction \"{name}\" with non matching guid. Updating all Action Guids.");
#endif
                // Go through and generate new guids for all actions
                foreach (var action in exposedActions)
                {
                    action.guid = Guid.NewGuid().ToString();
                }
            }

            return dirty;
        }
        
        private bool VerifyAsset()
        {
            bool dirty = false;

            dirty |= VerifyThisGuid();

            // The UdonAsset was not properly loaded, but there is still a reference. Unity will say it is null,
            // but the object still exists. Try loading the asset based on the guid and re-save the asset.
            if (((object) udonProgramAsset) != null && udonProgramAsset == null && !string.IsNullOrEmpty(assetGuid))
            {
                string path = AssetDatabase.GUIDToAssetPath(assetGuid);
                if (!string.IsNullOrEmpty(path))
                {
                    udonProgramAsset = AssetDatabase.LoadAssetAtPath<UdonProgramAsset>(path);
                    dirty = true;
                }
            }
            
            if (udonProgramAsset)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(udonProgramAsset, out string newGuid, out long _);
                dirty |= assetGuid != newGuid;
                assetGuid = newGuid;
            }
            else if (!string.IsNullOrEmpty(assetGuid))
            {
                assetGuid = "";
                dirty = true;
            }

            return dirty;
        }

        private void VerifyAndSave()
        {
            if (VerifyAsset())
            {
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
            }
        }
        
        public override void Initialize()
        {
            VerifyAndSave();
            if (!udonProgramAsset)
            {
                _eventNames = Array.Empty<CyanTriggerEventArgData>();
                _variables = Array.Empty<(string, Type)>();
                return;
            }
            
            _eventNames = CyanTriggerCustomNodeInspectorUtil.GetEventOptions(udonProgramAsset, null, true).ToArray();
            _variables = CyanTriggerCustomNodeInspectorUtil.GetVariableOptions(udonProgramAsset, null).ToArray();
        }
        
        public override CyanTriggerAssemblyProgram GetCyanTriggerAssemblyProgram()
        {
            VerifyAndSave();
            if (!udonProgramAsset)
            {
#if CYAN_TRIGGER_DEBUG
                Debug.LogError($"ProgramAsset is null: {name}");
#endif
                return null;
            }
            
            IUdonProgram program = udonProgramAsset.SerializedProgramAsset.RetrieveProgram();
            
            // Verify program has actions expected?
            string message = "";
            if (!VerifyProgramActions(program, ref message))
            {
                udonProgramAsset.RefreshProgram();
                program = udonProgramAsset.SerializedProgramAsset.RetrieveProgram();
                if (!VerifyProgramActions(program, ref message))
                {
                    string path = AssetDatabase.GetAssetPath(this);
                    Debug.LogError($"CyanTrigger Custom Action Definition is invalid! \"{name}\" {message} {path}");
                    return null;
                }
            }

            if (!VerifyCustomAction(ref message))
            {
                string path = AssetDatabase.GetAssetPath(this);
                Debug.LogError($"CyanTrigger Custom Action Definition is invalid! \"{name}\" {message} {path}");
                return null;
            }
            
            return CyanTriggerAssemblyProgramUtil.CreateProgram(program);
        }

        public override bool DisplayExtraEditorOptions(SerializedObject obj)
        {
            if (IsLockedCyanTriggerEditableProgram())
            {
                EditorGUILayout.HelpBox("This content is locked and cannot be edited!", MessageType.Warning);
            }
            
            SerializedProperty udonAssetProperty = obj.FindProperty(nameof(udonProgramAsset));

            EditorGUI.BeginChangeCheck();
            
            EditorGUILayout.ObjectField(udonAssetProperty);
            
            obj.ApplyModifiedProperties();
            
            if (EditorGUI.EndChangeCheck())
            {
                Initialize();
            }

            // If udonProgram is missing, only display that
            if (!udonProgramAsset)
            {
                return false;
            }

            return true;
        }

        public override void DisplayExtraMethodOptions(SerializedObject obj)
        {
            if (GUILayout.Button(new GUIContent("Auto Add Non-Custom Events",
                    "Gather all events that are not custom that should be auto added. This is useful when handling specific events, such as networking.")))
            {
                GatherAutoAddEvents(obj.FindProperty(nameof(exposedActions)));
            }
        }

        private bool IsLockedCyanTriggerEditableProgram()
        {
            return udonProgramAsset != null 
                && udonProgramAsset is CyanTriggerEditableProgramAsset ctProgram
                && ctProgram.isLocked;
        }
        
        public override bool IsEditorModifiable()
        {
#if !CYAN_TRIGGER_DEBUG
            if (IsLockedCyanTriggerEditableProgram())
            {
                return false;
            }
#endif
            
            return true;
        }

        private void AddEvent(
            SerializedProperty eventListProperty, 
            CyanTriggerEventArgData selectedEvent, 
            Action onAdded)
        {
            string eventName = selectedEvent.eventName;
            string baseEvent = "Event_Custom";
            bool isCustom = true;

            if (CyanTriggerNodeDefinitionManager.Instance.TryGetDefinitionFromCompiledName(eventName,
                    out var node)
                && node.CustomDefinition == null)
            {
                baseEvent = node.FullName;
                isCustom = false;
            }

            string actionNamespace = GetNamespace();
            
            SerializedProperty actionProperty = 
                AddNewEvent(eventListProperty, actionNamespace, eventName, baseEvent, eventName);
            
            SerializedProperty variablesProperty =
                actionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.variables));

            // Prevent adding event variables and only add variables for custom defined items.
            if (isCustom)
            {
                // Go through and add event parameters along with the event. 
                for (int curVar = 0; curVar < selectedEvent.variableTypes.Length; ++curVar)
                {
                    AddNewVariable(
                        variablesProperty,
                        selectedEvent.variableUdonNames[curVar],
                        selectedEvent.variableNames[curVar],
                        selectedEvent.variableTypes[curVar],
                        "",
                        CyanTriggerActionVariableTypeDefinition.VariableInput 
                        | (selectedEvent.variableOuts[curVar]
                            ? CyanTriggerActionVariableTypeDefinition.VariableOutput
                            : CyanTriggerActionVariableTypeDefinition.Constant)
                    );
                }
            }

            onAdded?.Invoke();
        }

        public override void AddNewEvent(SerializedProperty eventListProperty, Action onAdded)
        {
            GenericMenu menu = new GenericMenu();

            foreach (var selectedEvent in _eventNames)
            {
                menu.AddItem(new GUIContent(selectedEvent.eventName), false, (item) =>
                {
                    AddEvent(eventListProperty, (CyanTriggerEventArgData)item, onAdded);
                }, selectedEvent);
            }
            
            menu.AddItem(new GUIContent("Variable Setter"), false, (i) =>
            {
                AddNewEvent(eventListProperty, GetNamespace(), "SetVar", CustomEventName, EmptyEntryEventName);
                onAdded?.Invoke();
            }, null);

            menu.ShowAsContext();
        }

        public override void AddNewVariable(int actionIndex, SerializedProperty variableListProperty, Action onAdded)
        {
            GenericMenu menu = new GenericMenu();

            // Go though all already added variables and save the udon assembly name.
            HashSet<string> usedVariables = new HashSet<string>();
            for (int cur = 0; cur < variableListProperty.arraySize; ++cur)
            {
                SerializedProperty variable = variableListProperty.GetArrayElementAtIndex(cur);
                SerializedProperty varName =
                    variable.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.udonName));
                usedVariables.Add(varName.stringValue);
            }

            void AddVariableOption(string variableName, string udonVariableName, Type variableType, bool output)
            {
                // If variable has already been added to this list, skip it. 
                if (usedVariables.Contains(udonVariableName))
                {
                    return;
                }

                menu.AddItem(new GUIContent(variableName), false, (t) =>
                {
                    var varTypeInfo = CyanTriggerActionVariableTypeDefinition.VariableInput
                                      | (output 
                                          ? CyanTriggerActionVariableTypeDefinition.VariableOutput
                                          : CyanTriggerActionVariableTypeDefinition.Constant);
                    AddNewVariable(variableListProperty, udonVariableName, variableName, variableType, "", varTypeInfo);
                    onAdded?.Invoke();
                }, null);
            }

            // Check if the current exposed action has parameters and add those to the list. 
            var action = exposedActions[actionIndex];
            if (action.baseEventName == CustomEventName)
            {
                string eventName = action.eventEntry;
                foreach (var eventData in _eventNames)
                {
                    if (eventData.eventName == eventName)
                    {
                        for (int index = 0; index < eventData.variableNames.Length; ++index)
                        {
                            AddVariableOption(
                                eventData.variableNames[index],
                                eventData.variableUdonNames[index], 
                                eventData.variableTypes[index],
                                eventData.variableOuts[index]);
                        }
                    }
                }
            }

            // Go through all scanned variables and add those to the list. 
            for (int cur = 0; cur < _variables.Length; ++cur)
            {
                (string varName, Type varType) = _variables[cur];
                AddVariableOption(varName, varName, varType, false);
            }

            menu.ShowAsContext();
        }
        
        private void GatherAutoAddEvents(SerializedProperty eventListProperty)
        {
            // Gather events that have already been added. 
            HashSet<string> existingEvents = new HashSet<string>();
            foreach (var action in exposedActions)
            {
                if (action.baseEventName == CustomEventName || action.baseEventName == EmptyEntryEventName)
                {
                    continue;
                }

                existingEvents.Add(action.eventEntry);
            }

            // Go through all events, even if it is a CyanTrigger.
            // This way, Event Replay's Start, OnPreSerialization and OnDeserialization items will found.
            var allEventOptions = CyanTriggerCustomNodeInspectorUtil.GetEventOptionsFromUdonProgram(udonProgramAsset);
            foreach (var selectedEvent in allEventOptions)
            {
                string eventName = selectedEvent.eventName;

                // Ignore already added events.
                if (existingEvents.Contains(eventName))
                {
                    continue;
                }
                
                // Ignore any events that do not have an actual Node representation.
                if (!CyanTriggerNodeDefinitionManager.Instance.TryGetDefinitionFromCompiledName(eventName,
                        out var node))
                {
                    continue;
                }
                
                string baseEvent = node.FullName;
                string actionNamespace = GetNamespace();
                
                SerializedProperty actionProperty = 
                    AddNewEvent(eventListProperty, actionNamespace, eventName, baseEvent, eventName);
                
                SerializedProperty autoAddProperty =
                    actionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.autoAdd));
                autoAddProperty.boolValue = true;
            }
        }
        
        public static CyanTriggerActionGroupDefinitionUdonAsset CreateCustomActionForProgramAsset(
            CyanTriggerEditableProgramAsset programAsset)
        {
            string path = AssetDatabase.GetAssetPath(programAsset);
            string filename = Path.Combine(Path.GetDirectoryName(path), $"{programAsset.name}Actions.asset");
            
            var customAction = CreateInstance<CyanTriggerActionGroupDefinitionUdonAsset>();

            customAction.udonProgramAsset = programAsset;
            customAction.defaultNamespace = programAsset.name;
            customAction.autoAddPriority = 0;
            customAction.isMultiInstance = false;
            
            customAction.Initialize();

            SerializedObject serializedObject = new SerializedObject(customAction);
            var eventsProp = serializedObject.FindProperty(nameof(exposedActions));
            
            foreach (var selectedEvent in customAction._eventNames)
            {
                customAction.AddEvent(eventsProp, selectedEvent, null);
            }

            serializedObject.ApplyModifiedProperties();
            
            AssetDatabase.CreateAsset(customAction, filename);
            AssetDatabase.SaveAssets();

            Selection.objects = new Object[] { customAction };
            
            return customAction;
        }
    }
}