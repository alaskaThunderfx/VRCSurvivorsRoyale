using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEngine;
using VRC.Udon;
using Object = UnityEngine.Object;

namespace Cyan.CT.Editor
{
    [CustomEditor(typeof(CyanTriggerAsset))]
    [CanEditMultipleObjects]
    public class CyanTriggerAssetEditor : UnityEditor.Editor
    {
        private bool _hasNullUdon;
        private bool _allSameProgram;
        private CyanTriggerEditableProgramAsset _programAsset;
        
        // Asset properties
        private SerializedProperty _ctProgramProperty;
        private SerializedProperty _udonProperty;
        private SerializedProperty _variableDataProperty;
        private SerializedProperty _variableGuidsProperty;
        private SerializedProperty _expandVariablesProperty;
        private SerializedProperty _interactProximityProperty;
        private SerializedProperty _interactTextProperty;
        private SerializedProperty _expandInteractProperty;
        
        // UdonBehaviour properties
        private SerializedObject _udonSerializedObject;
        private SerializedProperty _programSourceProperty;
        private SerializedProperty _serializedProgramAssetProperty;
        
        private bool _isPlaying = false;
        private CyanTriggerVariableValueTreeView _variableTreeView;
        private UdonBehaviour[] _udonBehaviours;
        private CyanTriggerAsset[] _ctAssets;

        private CyanTriggerProgramAssetBaseEditor _triggerEditor;

        private bool _showEventOptions = false;

        private void OnEnable()
        {
            SerializedProperty assetInstanceProperty = serializedObject.FindProperty(nameof(CyanTriggerAsset.assetInstance));

            _ctProgramProperty = assetInstanceProperty.FindPropertyRelative(nameof(CyanTriggerAssetSerializableInstance.cyanTriggerProgram));
            _udonProperty = assetInstanceProperty.FindPropertyRelative(nameof(CyanTriggerAssetSerializableInstance.udonBehaviour));
            
            _expandVariablesProperty = assetInstanceProperty.FindPropertyRelative(nameof(CyanTriggerAssetSerializableInstance.expandVariables));
            
            _interactProximityProperty = assetInstanceProperty.FindPropertyRelative(nameof(CyanTriggerAssetSerializableInstance.proximity));
            _interactTextProperty = assetInstanceProperty.FindPropertyRelative(nameof(CyanTriggerAssetSerializableInstance.interactText));
            _expandInteractProperty = assetInstanceProperty.FindPropertyRelative(nameof(CyanTriggerAssetSerializableInstance.expandInteractSettings));
                

            _hasNullUdon = false;
            _allSameProgram = true;
            Object[] targetUdons = new Object[targets.Length];
            _udonBehaviours = new UdonBehaviour[targets.Length];
            _ctAssets = new CyanTriggerAsset[targets.Length];
            for (var index = 0; index < targets.Length; ++index)
            {
                var ctAsset = (CyanTriggerAsset)targets[index];
                _ctAssets[index] = ctAsset;
                var assetInstance = ctAsset.assetInstance;
                
                if (assetInstance != null && assetInstance.udonBehaviour != null)
                {
                    targetUdons[index] = _udonBehaviours[index] = assetInstance.udonBehaviour;
                }
                _hasNullUdon |= _udonBehaviours[index] == null;
                
                if (assetInstance != null)
                {
                    if (!_allSameProgram)
                    {
                        continue;
                    }
                    
                    var program = (CyanTriggerEditableProgramAsset)assetInstance.cyanTriggerProgram;
                    if (index == 0)
                    {
                        _programAsset = program;
                    }

                    if (_programAsset != program)
                    {
                        _programAsset = null;
                        _allSameProgram = false;
                    }
                }
                else
                {
                    _allSameProgram = false;
                    _programAsset = null;
                }
            }

            if (!_hasNullUdon)
            {
                _udonSerializedObject = new SerializedObject(targetUdons);
                _programSourceProperty = _udonSerializedObject.FindProperty("programSource");
                _serializedProgramAssetProperty = _udonSerializedObject.FindProperty("serializedProgramAsset");

                // Force all UdonBehaviours to have the same program if any differ or are missing.
                if (_allSameProgram && _programAsset != _programSourceProperty.objectReferenceValue)
                {
                    _programSourceProperty.objectReferenceValue = _programAsset;
                    if (_programAsset == null)
                    {
                        _serializedProgramAssetProperty.objectReferenceValue = null;
                    }
                    else
                    {
                        _serializedProgramAssetProperty.objectReferenceValue = _programAsset.SerializedProgramAsset;
                    }

                    _udonSerializedObject?.ApplyModifiedProperties();
                }
            }

            UpdateInspector();
        }

        private void OnDisable()
        {
            _triggerEditor?.OnDisable();
            _triggerEditor = null;

            if (EditorApplication.isPlaying)
            {
                EditorApplication.update -= Repaint;
            }
        }

        private void UpdateTriggerEditor()
        {
            if (_programAsset == null)
            {
                _triggerEditor?.OnDisable();
                _triggerEditor = null;
            }
            else if (_allSameProgram)
            {
                _triggerEditor = new CyanTriggerProgramAssetBaseEditor(this, _programAsset, true, _ctAssets, OnProgramChanged);
            }
        }

        private void OnProgramChanged()
        {
            VerifyProgramAssetVariables();
        }

        private void UpdateInspector()
        {
            if (!_allSameProgram)
            {
                return;
            }
            
            SerializedProperty assetInstanceProperty = serializedObject.FindProperty(nameof(CyanTriggerAsset.assetInstance));
            _variableGuidsProperty = assetInstanceProperty.FindPropertyRelative(nameof(CyanTriggerAssetSerializableInstance.variableGuids));
            _variableDataProperty = assetInstanceProperty.FindPropertyRelative(nameof(CyanTriggerAssetSerializableInstance.variableData));
            
            UpdateVariables();

            UpdateTriggerEditor();
        }
        
        private void UpdateVariables()
        {
            if (_programAsset == null || !_allSameProgram)
            {
                if (EditorApplication.isPlaying)
                {
                    EditorApplication.update -= Repaint;
                }
                _variableTreeView = null;
                return;
            }
            
            CyanTriggerVariable[] variables = _programAsset.GetCyanTriggerData()?.variables;
            _variableTreeView = new CyanTriggerVariableValueTreeView(_variableDataProperty, variables, _udonBehaviours);
            _triggerEditor?.SetIdStartIndex(_variableTreeView.Size);
            
            if (EditorApplication.isPlaying)
            {
                EditorApplication.update += Repaint;
            }
        }

        private void ResetVariables()
        {
            if (!_allSameProgram)
            {
                return;
            }
            if (_programAsset == null)
            {
                _variableDataProperty.ClearArray();
                _variableGuidsProperty.ClearArray();
                return;
            }
            
            CyanTriggerVariable[] variables = _programAsset.GetCyanTriggerData()?.variables;

            int length = variables?.Length ?? 0;
            _variableDataProperty.arraySize = length;
            _variableGuidsProperty.arraySize = length;
            
            if (variables != null)
            {
                for (var index = 0; index < variables.Length; ++index)
                {
                    var variable = variables[index];
                    SerializedProperty dataProp = _variableDataProperty.GetArrayElementAtIndex(index);
                    CyanTriggerSerializableObject.UpdateSerializedProperty(
                        dataProp, 
                        variable.data.Obj);

                    SerializedProperty guidProp = _variableGuidsProperty.GetArrayElementAtIndex(index);
                    guidProp.stringValue = variable.variableID;
                }
            }

            CyanTriggerEditableProgramAsset.ForceGuidPrefabOverride(
                _ctAssets, _variableGuidsProperty, _variableDataProperty);
        }
        
        private void VerifyProgramAssetVariables()
        {
            if (!_allSameProgram)
            {
                return;
            }
            
            if (_programAsset == null)
            {
                return;
            }

            if (_programAsset.VerifyAssetVariables(_ctAssets))
            {
                UpdateVariables();
            }
        }

        public override void OnInspectorGUI()
        {
            _isPlaying = EditorApplication.isPlaying;

            VerifyProgramAssetVariables();

            if (_allSameProgram)
            {
                serializedObject.UpdateIfRequiredOrScript();
            }
            
            DisplayEditors();

            if (_allSameProgram)
            {
                serializedObject.ApplyModifiedProperties();
            }
            
            CyanTriggerEditorUtils.DrawHeader("CyanTrigger", true);
        }

        private void DisplayEditors()
        {
            DisplayMissingUdonSection();
            
            DisplayProgramSelector();

            if (!_allSameProgram)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("Selected Objects do not have the same CyanTrigger Programs.", MessageType.Warning);
                return;
            }
            
            if (_programAsset == null)
            {
                return;
            }
            
            // if (GUILayout.Button("Log overrides"))
            // {
            //     CyanTriggerEditableProgramAsset.LogOverrides(_ctAssets);
            // }
            CyanTriggerEditableProgramAsset.ForceGuidPrefabOverride(
                _ctAssets, _variableGuidsProperty, _variableDataProperty);

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            DisplayComment();
            
            DisplayInteractSettings();
            
            DisplayVariables();

            DisplayPlaymodeEventOptions();
            
            _triggerEditor.SetIdStartIndex(_variableTreeView.Size);
            _triggerEditor.OnInspectorGUI();
        }

        private void DisplayMissingUdonSection()
        {
            if (_hasNullUdon)
            {
                // Add large button to add missing Udon.
                EditorGUILayout.HelpBox("Missing Udon Behaviour! This program will not run.", MessageType.Error);

                // TODO make horizontal to prevent accidental clicks.
                if (GUILayout.Button("Add UdonBehaviour"))
                {
                    foreach (var ctTarget in targets)
                    {
                        CyanTriggerAsset ctAsset = (CyanTriggerAsset)ctTarget;
                        if (ctAsset.assetInstance?.udonBehaviour == null)
                        {
                            ctAsset.DelayedReset();
                        }
                    }
                    OnEnable();
                }
                if (GUILayout.Button("Remove CyanTriggerAsset"))
                {
                    foreach (var ctTarget in targets)
                    {
                        Undo.DestroyObjectImmediate(ctTarget);
                    }
                }
            }
        }
        
        private void DisplayProgramSelector()
        {
            EditorGUI.BeginDisabledGroup(_isPlaying);
            
            _udonSerializedObject?.UpdateIfRequiredOrScript();
            
            bool multi = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = _udonSerializedObject?.isEditingMultipleObjects ?? false;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(_udonProperty);
            }
            
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();

            EditorGUI.showMixedValue = !_allSameProgram;
            
            Object progValue = EditorGUILayout.ObjectField(
                "CyanTrigger Program",
                _ctProgramProperty.objectReferenceValue,
                typeof(CyanTriggerEditableProgramAsset), 
                false
            );
            
            EditorGUI.showMixedValue = multi;
            
            bool newValueSet = EditorGUI.EndChangeCheck();
            if (newValueSet)
            {
                _ctProgramProperty.objectReferenceValue = progValue;
                _allSameProgram = true;
                
                UpdateInspector();
            }

            // If no program, provide a way to create a new one.
            if (_ctProgramProperty.objectReferenceValue == null)
            {
                CyanTriggerAsset ctAsset = (CyanTriggerAsset)target;
                GameObject targetObj = ctAsset.gameObject;

                bool cannotSave = !CyanTriggerProgramAssetExporter.CanExportAsset(ctAsset);

                string tooltip = cannotSave
                    ? "Save the scene to create a new CyanTrigger program."
                    : "Create a new CyanTrigger program that will be saved in the same folder as this scene.";
                
                EditorGUI.BeginDisabledGroup(cannotSave);
                if (GUILayout.Button(new GUIContent("New Program", tooltip)))
                {
                    _ctProgramProperty.objectReferenceValue = 
                        CyanTriggerProgramAssetExporter.CreateUdonProgramSourceAsset(targetObj, ctAsset.name);
                }
                EditorGUI.EndDisabledGroup();
            }
            
            EditorGUILayout.EndHorizontal();

            _programAsset = (CyanTriggerEditableProgramAsset)_ctProgramProperty.objectReferenceValue;
            
            if (newValueSet
                || (_udonSerializedObject != null && _programAsset != _programSourceProperty.objectReferenceValue))
            {
                if (_udonSerializedObject != null)
                {
                    _programSourceProperty.objectReferenceValue = _programAsset;
                    if (_programAsset == null)
                    {
                        _serializedProgramAssetProperty.objectReferenceValue = null;
                    }
                    else
                    {
                        _serializedProgramAssetProperty.objectReferenceValue = _programAsset.SerializedProgramAsset;
                    }

                    _udonSerializedObject?.ApplyModifiedProperties();
                }

                ResetVariables();
                UpdateVariables();
                UpdateTriggerEditor();
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DisplayComment()
        {
            var triggerData = _programAsset.GetCyanTriggerData();
            string comment = triggerData?.comment?.comment;

            if (string.IsNullOrEmpty(comment))
            {
                return;
            } 
            
            EditorGUILayout.BeginVertical(CyanTriggerEditorGUIUtil.HelpBoxStyle);
            
            EditorGUILayout.LabelField($"// {comment}".Colorize(CyanTriggerColorTheme.Comment, true), CyanTriggerEditorGUIUtil.CommentStyle);
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
        }
        
        private void DisplayInteractSettings()
        {
            bool renderInteractSettings = false;
            foreach (var eventType in _programAsset.GetCyanTriggerData().events)
            {
                var actionType = eventType.eventInstance.actionType;
                string directEvent = actionType.directEvent;
                if (string.IsNullOrEmpty(directEvent))
                {
                    if (!CyanTriggerActionGroupDefinitionUtil.Instance.TryGetActionDefinition(actionType.guid, out var definition))
                    {
                        continue;
                    }
                    directEvent = definition.baseEventName;
                }
                    
                if (directEvent.Equals("Event_Interact"))
                {
                    renderInteractSettings = true;
                }
            }

            if (!renderInteractSettings)
            {
                return;
            }
            
            EditorGUILayout.BeginVertical(CyanTriggerEditorGUIUtil.HelpBoxStyle);

            bool showInteract = _expandInteractProperty.boolValue;
            
            EditorGUI.BeginDisabledGroup(_isPlaying);
            
            CyanTriggerPropertyEditor.DrawFoldoutListHeader(
                new GUIContent("Interact Settings"),
                ref showInteract,
                false,
                0,
                null,
                false,
                null,
                false,
                false
            );
            _expandInteractProperty.boolValue = showInteract;
            
            EditorGUI.EndDisabledGroup();
            
            if (showInteract)
            {
                CyanTriggerSerializableInstanceEditor.RenderInteractSettings(_interactTextProperty, _interactProximityProperty);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
        }

        private void DisplayVariables()
        {
            EditorGUILayout.BeginVertical(CyanTriggerEditorGUIUtil.HelpBoxStyle);

            bool showVariables = _expandVariablesProperty.boolValue;
            
            EditorGUI.BeginDisabledGroup(_isPlaying);
            
            CyanTriggerPropertyEditor.DrawFoldoutListHeader(
                new GUIContent("Public Variables"),
                ref showVariables,
                false,
                0,
                null,
                false,
                null,
                false,
                false
            );
            _expandVariablesProperty.boolValue = showVariables;
            
            EditorGUI.EndDisabledGroup();

            if (showVariables)
            {
                _variableTreeView.DoLayoutTree();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
        }

        private void DisplayPlaymodeEventOptions()
        {
            if (!_isPlaying || _hasNullUdon)
            {
                return;
            }
            
            // Prevent showing event selection for prefabs not in a scene.
            foreach (var udon in _udonBehaviours)
            {
                GameObject obj = udon.gameObject;
                if (PrefabUtility.IsPartOfPrefabAsset(obj)
                    || PrefabStageUtility.GetPrefabStage(obj) != null)
                {
                    return;
                }
            }
            
            EditorGUILayout.BeginVertical(CyanTriggerEditorGUIUtil.HelpBoxStyle);
            
            CyanTriggerPropertyEditor.DrawFoldoutListHeader(
                new GUIContent("Events"),
                ref _showEventOptions,
                false,
                0,
                null,
                false,
                null,
                false,
                false
            );
            
            if (_showEventOptions)
            {
                var events =
                    CyanTriggerCustomNodeInspectorUtil.GetEventOptionsFromCyanTrigger(_programAsset, null, true);

                foreach (var eventArgs in events)
                {
                    if (GUILayout.Button(eventArgs.eventName))
                    {
                        foreach (var udon in _udonBehaviours)
                        {
                            // TODO show input options for events
                            udon.SendCustomEvent(eventArgs.eventName);
                        }
                    }
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
        }
    }
}