using System;
using UnityEditor;
using UnityEngine;
using VRC.Udon.Editor.ProgramSources;
using Object = UnityEngine.Object;

namespace Cyan.CT.Editor
{
    [CustomEditor(typeof(CyanTriggerProgramAsset), true)]
    public class CyanTriggerProgramAssetEditor : UdonAssemblyProgramAssetEditor
    {
        private CyanTriggerProgramAssetBaseEditor _programEditor;
        
        private void OnEnable()
        {
            _programEditor = new CyanTriggerProgramAssetBaseEditor(this, (CyanTriggerProgramAsset)target, false, null, null);
        }

        private void OnDisable()
        {
            _programEditor.OnDisable();
        }

        public override void OnInspectorGUI()
        {
            _programEditor.OnInspectorGUI();
            
            CyanTriggerEditorUtils.DrawHeader("CyanTrigger", true);

            base.OnInspectorGUI();
        }
    }
    
    public class CyanTriggerProgramAssetBaseEditor : ICyanTriggerBaseEditor
    {
        public const double TimeDelayBeforeAutoCompile = 2.0;

        private readonly CyanTriggerProgramAsset _cyanTriggerProgramAsset;
        private readonly CyanTriggerEditableProgramAsset _cyanTriggerEditableProgramAsset;
        private CyanTriggerSerializableInstanceEditor _editor;
        private readonly CyanTriggerAsset[] _ctAssets;
        
        private SerializedObject _serializedCyanTrigger;
        private CyanTriggerScriptableObject _cyanTrigger;

        private bool _anyChanges = false;
        private bool _disposed = false;
        private bool _initialized = false;
        private double _lastChangeTime;
        
        private readonly bool _isEditable = false;
        private readonly bool _allowExpandAndEdit = false;
        private bool _isPlaying = false;

        private readonly UnityEditor.Editor _baseEditor;
        private readonly Action _onChange;

        public CyanTriggerProgramAssetBaseEditor(
            UnityEditor.Editor baseEditor,
            CyanTriggerProgramAsset target,
            bool showExpandInInspector,
            CyanTriggerAsset[] ctAssets,
            Action onChange)
        {
            _baseEditor = baseEditor;
            _allowExpandAndEdit = showExpandInInspector;
            _ctAssets = ctAssets;
            _onChange = onChange;

            _lastChangeTime = -1;
            _cyanTriggerProgramAsset = target;

            if (_cyanTriggerProgramAsset is CyanTriggerEditableProgramAsset editableProgram)
            {
                _isEditable = true;
                _cyanTriggerEditableProgramAsset = editableProgram;

                if (_cyanTriggerEditableProgramAsset.TryVerifyAndMigrate())
                {
                    AssetDatabase.SaveAssets();
                }
            }

            if (!EditorApplication.isPlaying)
            {
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            }

            CreateEditor();
        }
        
        private void CreateEditor() 
        {
            if (_cyanTrigger)
            {
                Object.DestroyImmediate(_cyanTrigger);
            }
            
            _cyanTrigger = ScriptableObject.CreateInstance<CyanTriggerScriptableObject>();
            _cyanTrigger.triggerInstance.triggerDataInstance = _cyanTriggerProgramAsset.GetCyanTriggerData();

            if (_ctAssets != null && _ctAssets.Length > 0)
            {
                _cyanTrigger.triggerInstance.udonBehaviour = _ctAssets[0].assetInstance.udonBehaviour;
            }

            _serializedCyanTrigger = new SerializedObject(_cyanTrigger);
            var instanceProperty = _serializedCyanTrigger.FindProperty(nameof(CyanTriggerScriptableObject.triggerInstance));

            _editor?.Dispose();
            _editor = new CyanTriggerSerializableInstanceEditor(instanceProperty, _cyanTrigger.triggerInstance, this);
        }

        private bool CanEdit()
        {
#if !CYAN_TRIGGER_DEBUG
            if (_cyanTriggerEditableProgramAsset != null && _cyanTriggerEditableProgramAsset.isLocked)
            {
                return false;
            }
#endif
            
            if (!_allowExpandAndEdit)
            {
                return true;
            }

            if (_isPlaying)
            {
                return false;
            }

            if (_cyanTriggerEditableProgramAsset != null)
            {
                return _cyanTriggerEditableProgramAsset.allowEditingInInspector && !_cyanTriggerEditableProgramAsset.isLocked;
            }
            
            return false;
        }

        public void SetIdStartIndex(int index)
        {
            if (_editor != null)
            {
                _editor.IdStartIndex = index;
            }
        }
        
        public void OnDisable()
        {
            _disposed = true;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            CheckNeedsRecompile();
            
            _editor?.Dispose();
            _editor = null;
            if (_cyanTrigger != null)
            {
                Object.DestroyImmediate(_cyanTrigger);
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange playModeStateChange)
        {
            if (playModeStateChange == PlayModeStateChange.ExitingEditMode)
            {
                CheckNeedsRecompile();
            }
        }

        private void CheckNeedsRecompile()
        {
            RemoveDelayCompile();
            
            if (!EditorApplication.isPlaying 
                && _anyChanges 
                && _isEditable
                && _cyanTriggerEditableProgramAsset 
                && CyanTriggerSettings.Instance.compileAssetTriggersOnClose)
            {
                // Only compile this and dependencies of this program
                CyanTriggerSerializedProgramManager.CompileCyanTriggerEditableAssetsAndDependencies(_cyanTriggerEditableProgramAsset);
            }
        }
        
        public void OnInspectorGUI()
        {
            if (_cyanTrigger == null)
            {
                CreateEditor();
            }
            
            _isPlaying = EditorApplication.isPlaying;

            if (_isEditable && _allowExpandAndEdit)
            {
                bool showProgram = _cyanTriggerEditableProgramAsset.expandInInspector;
                EditorGUILayout.BeginVertical(CyanTriggerEditorGUIUtil.HelpBoxStyle);
                
                CyanTriggerPropertyEditor.DrawFoldoutListHeader(
                    new GUIContent("CyanTrigger Program"),
                    ref showProgram,
                    false,
                    0,
                    null,
                    false,
                    null,
                    false,
                    false
                );
                
                if (_cyanTriggerEditableProgramAsset.expandInInspector != showProgram)
                {
                    Undo.RecordObject(_cyanTriggerEditableProgramAsset, "Set show program");
                    _cyanTriggerEditableProgramAsset.expandInInspector = showProgram;
                }

                if (!showProgram)
                {
                    EditorGUILayout.EndVertical();
                    return;
                }
                
                if (!_isPlaying)
                {
                    EditorGUILayout.Space();
                    
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(10);
                    
                    bool canEdit = _cyanTriggerEditableProgramAsset.allowEditingInInspector;
                    GUIContent lockIcon = canEdit 
                        ? new GUIContent(CyanTriggerImageResources.UnlockedIcon, "Stop Editing CyanTrigger Program") 
                        : new GUIContent(CyanTriggerImageResources.LockedIcon, "Edit CyanTrigger Program");
                    
                    if (GUILayout.Button(lockIcon, GUILayout.Width(30), GUILayout.Height(30)))
                    {
                        canEdit = !canEdit;
                        Undo.RecordObject(_cyanTriggerEditableProgramAsset, "Set allow editing");
                        _cyanTriggerEditableProgramAsset.allowEditingInInspector = canEdit;
                        
                        _editor.DelayUpdateActionDisplayNames();
                    }

                    EditorGUILayout.BeginVertical();
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField("Editing this program will affect all UdonBehaviours that use it!");
                    EditorGUILayout.EndVertical();
                    
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space();
                }
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.BeginVertical(CyanTriggerEditorGUIUtil.HelpBoxClearStyle);
            }
            
            if (_isPlaying && _isEditable)
            {
                EditorGUILayout.HelpBox("Exit Playmode to edit this CyanTrigger", MessageType.Warning);
            }
            else if (!_isEditable)
            {
                EditorGUILayout.HelpBox("This CyanTrigger represents a scene trigger and cannot be edited here.", MessageType.Warning);
            }
            
            if (_cyanTriggerEditableProgramAsset != null && _cyanTriggerEditableProgramAsset.isLocked)
            {
                EditorGUILayout.HelpBox("This content is locked and cannot be edited!", MessageType.Warning);
            }
            
            EditorGUI.BeginDisabledGroup(!_isEditable || !CanEdit());
            
            EditorGUI.BeginChangeCheck();
            
            _editor.OnInspectorGUI();
            
            if (EditorGUI.EndChangeCheck())
            {
                OnChange();
            }
            
            EditorGUI.BeginDisabledGroup(_isPlaying || !CanEdit());

            if (_isEditable)
            {
                CyanTriggerEditor.DisplayUtilitiesSection(DisplayUtilityActions);
            }

            _initialized = true;
            
            EditorGUI.EndDisabledGroup();
            EditorGUI.EndDisabledGroup();
            
            if (_isEditable && _allowExpandAndEdit)
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }
        }
        
        private void DisplayUtilityActions()
        {
            if (!_isEditable)
            {
                return;
            }
            
            if (GUILayout.Button(new GUIContent("Compile Trigger", "Compile this CyanTrigger Program Asset.")))
            {
                if (!_cyanTriggerProgramAsset.RehashAndCompile())
                {
                    _cyanTriggerEditableProgramAsset.PrintErrorsAndWarnings();
                    string path = AssetDatabase.GetAssetPath(_cyanTriggerProgramAsset);
                    Debug.LogError($"Failed to compile program: {path}");
                }
                else if (_cyanTriggerProgramAsset.warningMessages?.Length > 0)
                {
                    _cyanTriggerProgramAsset.PrintErrorsAndWarnings();
                    Debug.LogWarning($"Program compiled with warnings: {_cyanTriggerProgramAsset}");
                }
                RemoveDelayCompile();
                AssetDatabase.SaveAssets();
            }

            if (GUILayout.Button(new GUIContent("Compile All Trigger Assets", CyanTriggerSettingsEditor.CompileAssetTriggersButtonLabel.tooltip)))
            {
                CyanTriggerSerializedProgramManager.CompileAllCyanTriggerEditableAssets(true);
                _anyChanges = false;
                RemoveDelayCompile();
            }
            
            // Add space to separate these actions.
            EditorGUILayout.Space();

            if (GUILayout.Button(new GUIContent("Create Custom Action", "Creating a Custom Action from this CyanTrigger Program allows you to call the Events in this program as Actions in any other CyanTrigger. This button will create a Custom Action Definition for the program with all Events exposed. See the Custom Action documentation for more details.")))
            {
                CyanTriggerActionGroupDefinitionUdonAsset.CreateCustomActionForProgramAsset(_cyanTriggerEditableProgramAsset);
            }

            // Only show if this is a CyanTriggerAsset with UdonBehaviour
            if (_ctAssets != null && _ctAssets.Length > 0)
            {
                // Prevent trying to add CyanTrigger when one already exists on the object. 
                bool hasCyanTriggerAlready = false;
                foreach (var ctAsset in _ctAssets)
                {
                    if (ctAsset.GetComponent<CyanTrigger>() != null)
                    {
                        hasCyanTriggerAlready = true;
                        break;
                    }
                }
                
                EditorGUI.BeginDisabledGroup(hasCyanTriggerAlready);
                if (GUILayout.Button(new GUIContent("Convert to Scene CyanTrigger", "Convert this CyanTriggerAsset into a CyanTrigger")))
                {
                    // TODO verify compiled properly before converting.
                    var defaultProgram = CyanTriggerSerializedProgramManager.Instance.DefaultProgramAsset;
                
                    foreach (var ctAsset in _ctAssets)
                    {
                        var udon = ctAsset.assetInstance.udonBehaviour;
                        CyanTrigger.AddFromCyanTriggerAsset(ctAsset);
                    
                        Undo.RecordObject(udon, Undo.GetCurrentGroupName());
                        udon.programSource = defaultProgram;
                    }

                    GUIUtility.ExitGUI();
                }
                EditorGUI.EndDisabledGroup();
            }
        
            if (GUILayout.Button(new GUIContent("Export to Assembly Asset", "Export this CyanTrigger Program Asset into an Udon Assembly Program Asset which can be used in any project without CyanTrigger imported. Note that both the Udon Assembly Program Asset and the Serialized Program Asset are needed for the program to run.")))
            {
                // TODO verify compiled properly before exporting.
                CyanTriggerProgramAssetExporter.ExportToAssemblyAsset(_cyanTriggerEditableProgramAsset);
            }
        }
        
        public bool IsSceneTrigger()
        {
            return !_isEditable;
        }

        public void Repaint()
        {
            _baseEditor.Repaint();
        }
        
        public Object GetTarget()
        {
            return _cyanTriggerProgramAsset;
        }
        
        public CyanTriggerProgramAsset GetProgram()
        {
            return _cyanTriggerProgramAsset;
        }

        public void OnChange()
        {
            _onChange?.Invoke();
            
            if (_disposed || !_initialized || !_isEditable || _isPlaying)
            {
                return;
            }

            _anyChanges = true;
            
            if (!CyanTriggerSettings.Instance.compileAssetTriggersOnEdit)
            {
                return;
            }

            // Ensure only one in queue.
            EditorApplication.update -= DelayHashChecker;
            EditorApplication.update += DelayHashChecker;
        }
        
        // Delay checking if item needs recompile as some actions perform multiple OnChange calls which may be before
        // the data is in a proper or valid state. Ex: Moving items doesn't remove variable references until later. 
        private void DelayHashChecker()
        {
            EditorApplication.update -= DelayHashChecker;
            
            // Rehash the trigger and check if it differs. Auto compile if so.
            string hash = CyanTriggerInstanceDataHash.HashCyanTriggerInstanceData(_cyanTrigger.triggerInstance.triggerDataInstance);
            if (hash != _cyanTriggerProgramAsset.triggerHash)
            {
                // Mark it as changed.
                _cyanTriggerProgramAsset.HasUncompiledChanges = true;
                
                // Flag for recompile rather than directly recompile here.
                _lastChangeTime = EditorApplication.timeSinceStartup;
                
                // Ensure only one in queue.
                EditorApplication.update -= DelayCompileChecker;
                EditorApplication.update += DelayCompileChecker;
            }
        }

        private void DelayCompileChecker()
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (!CyanTriggerSettings.Instance.compileAssetTriggersOnEdit || _lastChangeTime == -1)
            {
                RemoveDelayCompile();
                return;
            }
            
            if (_lastChangeTime + TimeDelayBeforeAutoCompile < EditorApplication.timeSinceStartup)
            {
                RemoveDelayCompile();
                _cyanTriggerProgramAsset.RehashAndCompile();
            }
        }

        private void RemoveDelayCompile()
        {
            _lastChangeTime = -1;
            EditorApplication.update -= DelayCompileChecker;
        }
    }
}