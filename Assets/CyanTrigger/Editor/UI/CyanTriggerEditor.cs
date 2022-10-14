using System;
using UnityEditor;
using UnityEngine;
using VRC.Udon;
using Object = UnityEngine.Object;

namespace Cyan.CT.Editor
{
    [CustomEditor(typeof(CyanTrigger))]
    public class CyanTriggerEditor : UnityEditor.Editor, ICyanTriggerBaseEditor
    {
        private CyanTrigger _cyanTrigger;
        private CyanTriggerSerializableInstanceEditor _editor;

#if CYAN_TRIGGER_DEBUG
        private bool _showHash;
#endif
        
        private void OnEnable()
        {
            _cyanTrigger = (CyanTrigger)target;
            _cyanTrigger.Verify();
            CreateEditor();
        }
        
        private void OnDisable()
        {
            DisposeEditor();
        }

        private void CreateEditor()
        {
            DisposeEditor();

            var triggerInstance = _cyanTrigger.triggerInstance;
            var instanceProperty = serializedObject.FindProperty(nameof(CyanTrigger.triggerInstance));

            _editor = new CyanTriggerSerializableInstanceEditor(instanceProperty, triggerInstance, this);

            // Ensure that variables are updated.
            if (EditorApplication.isPlaying)
            {
                EditorApplication.update += Repaint;
            }
        }

        private void DisposeEditor()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.update -= Repaint;
            }
            
            _editor?.Dispose();
            _editor = null;
        }
        
        public override void OnInspectorGUI()
        {
            bool isPlaying = EditorApplication.isPlaying;
            if (isPlaying)
            {
                EditorGUILayout.HelpBox("Exit Playmode to edit this CyanTrigger", MessageType.Warning);
            }

            if (_editor == null)
            {
                CreateEditor();
            }
            
            _editor.OnInspectorGUI();
            
            EditorGUI.BeginDisabledGroup(isPlaying);
            
            DisplayUtilitiesSection(DisplayUtilityActions);
            
            
            CyanTriggerEditorUtils.DrawHeader("CyanTrigger", true);
         
#if CYAN_TRIGGER_DEBUG   
            _showHash = EditorGUILayout.Foldout(_showHash, "Trigger Hash", true);
            if (_showHash)
            {
                EditorGUI.BeginDisabledGroup(true);

                EditorGUILayout.TextArea(
                    CyanTriggerInstanceDataHash.HashCyanTriggerInstanceData(_cyanTrigger.triggerInstance
                        .triggerDataInstance));
                EditorGUILayout.TextArea(
                    CyanTriggerInstanceDataHash.GetProgramUniqueStringForCyanTrigger(_cyanTrigger.triggerInstance
                        .triggerDataInstance));

                EditorGUI.EndDisabledGroup();
            }
#endif
            
            
            EditorGUI.EndDisabledGroup();
        }

        private void DisplayUtilityActions()
        {
            if (GUILayout.Button(new GUIContent("Compile Scene Triggers", CyanTriggerSettingsEditor.CompileSceneTriggersButtonLabel.tooltip)))
            {
                CyanTriggerSerializerManager.RecompileAllTriggers(true, true);
            }
            
            // Add space to separate these actions.
            EditorGUILayout.Space();
            
            bool cannotSave = !CyanTriggerProgramAssetExporter.CanExportAsset(_cyanTrigger);
            
            EditorGUI.BeginDisabledGroup(cannotSave);
            string tooltip = cannotSave
                ? "Save the scene to enable converting to CyanTriggerAsset."
                : "Export this CyanTrigger into a CyanTrigger Program Asset and convert the CyanTrigger into a CyanTriggerAsset.";
            if (GUILayout.Button(new GUIContent("Convert to CyanTriggerAsset", tooltip)))
            {
                var program = CyanTriggerProgramAssetExporter.ExportToCyanTriggerEditableProgramAsset(
                    _cyanTrigger,
                    out var updatedVariableReferences);

                if (program)
                {
                    var udonBehaviour = _cyanTrigger.triggerInstance.udonBehaviour;

                    foreach (var variable in _cyanTrigger.triggerInstance.triggerDataInstance.variables)
                    {
                        if (variable.data.Obj is Object unityObject)
                        {
                            updatedVariableReferences[variable.variableID] = unityObject;
                        }
                    }
                    
                    // TODO this does not undo properly.
                    Undo.RecordObject(udonBehaviour, Undo.GetCurrentGroupName());
                    udonBehaviour.programSource = program;
                    
                    CyanTriggerAsset ctAsset = CyanTriggerAsset.AddFromUdonBehaviour(udonBehaviour);
                    Undo.RecordObject(ctAsset, Undo.GetCurrentGroupName());

                    var programVarData = program.GetCyanTriggerData().variables;
                    var variableData = ctAsset.assetInstance.variableData;
                    for (int index = 0; index < programVarData.Length; ++index)
                    {
                        if (updatedVariableReferences.TryGetValue(programVarData[index].variableID, out Object obj))
                        {
                            variableData[index].Obj = obj;
                        }
                    }

                    Undo.DestroyObjectImmediate(_cyanTrigger);
                }
            }
            // TODO convert entire scene to CyanTriggerAsset or all of same program type?
            
            tooltip = cannotSave
                ? "Save the scene to enable exporting this CyanTrigger to a program asset."
                : "Export this CyanTrigger into a CyanTrigger Program Asset.";
            if (GUILayout.Button(new GUIContent("Export CyanTrigger Program", tooltip)))
            {
                var program = 
                    CyanTriggerProgramAssetExporter.ExportToCyanTriggerEditableProgramAsset(_cyanTrigger, out _);
                if (program)
                {
                    Selection.SetActiveObjectWithContext(program, null);
                }
            }

            EditorGUI.EndDisabledGroup();
        }
        
        public static void DisplayUtilitiesSection(Action drawActions)
        {
            EditorGUILayout.BeginVertical(CyanTriggerEditorGUIUtil.HelpBoxStyle);

            bool showUtilities = CyanTriggerSessionState.GetBool(CyanTriggerSessionState.ShowUtilityActions);
            bool newShowUtilities = showUtilities;
            
            CyanTriggerPropertyEditor.DrawFoldoutListHeader(
                new GUIContent("Utilities"),
                ref newShowUtilities,
                false,
                0,
                null,
                false,
                null,
                false,
                false
            );

            if (newShowUtilities != showUtilities)
            {
                CyanTriggerSessionState.SetBool(CyanTriggerSessionState.ShowUtilityActions, newShowUtilities);
                showUtilities = newShowUtilities;
            }

            if (!showUtilities)
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
                return;
            }
            
            drawActions?.Invoke();
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }
        
        public void OnChange()
        {
            // TODO?
        }

        public Object GetTarget()
        {
            return target;
        }

        public bool IsSceneTrigger()
        {
            return true;
        }

        public CyanTriggerProgramAsset GetProgram()
        {
            UdonBehaviour udon = _cyanTrigger.triggerInstance?.udonBehaviour;
            if (udon == null)
            {
                return null;
            }
            return udon.programSource as CyanTriggerProgramAsset;
        }
    }
}