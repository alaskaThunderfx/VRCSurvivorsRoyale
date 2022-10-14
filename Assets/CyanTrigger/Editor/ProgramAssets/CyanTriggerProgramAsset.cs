
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Editor.ProgramSources;
using VRC.Udon.Serialization.OdinSerializer;

namespace Cyan.CT.Editor
{
    [HelpURL(CyanTriggerDocumentationLinks.ProgramAsset)]
    public class CyanTriggerProgramAsset : UdonAssemblyProgramAsset, ICyanTriggerProgramAsset
    {
        // On viewing a CyanTriggerProgram or an UdonBehaviour with a CyanTriggerProgram, this method will be called,
        // allowing other scripts to display extra debug information
        public static Action<CyanTriggerProgramAsset> ExtraDebugRendering;
        
        public string triggerHash;
        public bool shouldBeNetworked;

        public string[] warningMessages = Array.Empty<string>();
        public string[] errorMessages = Array.Empty<string>();

        [SerializeField]
        protected CyanTriggerDataInstance ctDataInstance = CyanTriggerDataInstance.CreateInitialized();
        [SerializeField]
        private CyanTriggerDataReferences publicVariableReferences;
        
        [SerializeField] 
        private bool ignoreOdinData;

        #region Deprecated Data
        
        // ReSharper disable InconsistentNaming, Unity.RedundantFormerlySerializedAsAttribute
        [NonSerialized, OdinSerialize, Obsolete, FormerlySerializedAs("cyanTriggerDataInstance")]
        protected CyanTriggerDataInstance ctDataInstanceOdin;
        [NonSerialized, OdinSerialize, Obsolete, FormerlySerializedAs("variableReferences")]
        private CyanTriggerDataReferences publicVariableReferencesOdin;
        // ReSharper restore InconsistentNaming, Unity.RedundantFormerlySerializedAsAttribute
        
        [SerializeField, Obsolete, FormerlySerializedAs("serializationData")]
        private SerializationData serializationDataOdin;

        #endregion

        private bool _showProgramUasm;
        private bool _showVariableReferences;
        private bool _showPublicVariables;
        private bool _showDefaultHeapValues;
        private bool _showHash;
        
        [NonSerialized]
        public bool HasUncompiledChanges;

        // Note that this will only apply to the non editable programs. 
        protected override void DrawProgramSourceGUI(UdonBehaviour udonBehaviour, ref bool dirty)
        {
            if (udonBehaviour != null)
            {
                CyanTrigger cyanTrigger = udonBehaviour.GetComponent<CyanTrigger>();
                
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField("CyanTrigger", cyanTrigger, typeof(CyanTrigger), true);
                EditorGUI.EndDisabledGroup();
                
                if (cyanTrigger != null)
                {
                    ApplyUdonProperties(cyanTrigger.triggerInstance, udonBehaviour, ref dirty);
                }
            }
            
            ShowGenericInspectorGUI(udonBehaviour, ref dirty, false);
            
            // TODO verify if valid, otherwise break out;

            ShowDebugInformation(udonBehaviour, ref dirty);
        }

        protected void ShowGenericInspectorGUI(UdonBehaviour udonBehaviour, ref bool dirty, bool isEditable)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                if (isEditable)
                {
                    EditorGUILayout.ObjectField("Program Source", this, typeof(CyanTriggerProgramAsset), false);
                }

                if (udonBehaviour == null)
                {
                    return;
                }

                var syncMethod = udonBehaviour.SyncMethod;
                EditorGUILayout.EnumPopup("Synchronization", syncMethod);
            }
        }

        protected void ShowDebugInformation(UdonBehaviour udonBehaviour, ref bool dirty)
        {
            DrawAssemblyErrorTextArea();
            
            _showProgramUasm = EditorGUILayout.Foldout(_showProgramUasm, "Compiled Trigger Assembly", true);
            if (_showProgramUasm)
            {
                DrawAssemblyTextArea(false, ref dirty);

                if (program != null)
                {
                    DrawProgramDisassembly();
                }
            }
            
#if CYAN_TRIGGER_DEBUG
            _showVariableReferences = EditorGUILayout.Foldout(_showVariableReferences, "Variable References", true);
            if (_showVariableReferences) 
            {
                if (publicVariableReferences == null)
                {
                    // Debug.LogError($"Variable references are null for program: {name}");
                    // CompileTrigger();
                    GUILayout.Label("No Variable References");
                }
                else
                {
                    foreach (var reference in publicVariableReferences.GetActionDataIndices())
                    {
                        GUILayout.Label(reference.ToString());
                    }
                }
            }

            if (udonBehaviour != null)
            {
                _showPublicVariables = EditorGUILayout.Foldout(_showPublicVariables, "Public Variables", true);
                if (_showPublicVariables)
                {
                    EditorGUI.BeginDisabledGroup(true);

                    DrawPublicVariables(udonBehaviour, ref dirty);

                    EditorGUI.EndDisabledGroup();
                }
            }

            _showDefaultHeapValues = EditorGUILayout.Foldout(_showDefaultHeapValues, "Heap Variables", true);
            if (_showDefaultHeapValues)
            {
                IUdonSymbolTable symbolTable = program?.SymbolTable;
                IUdonHeap heap = program?.Heap;
                if (symbolTable == null || heap == null)
                {
                    return;
                }

                GUILayout.Label("Heap Values:");
                foreach (var symbol in symbolTable.GetSymbols())
                {
                    uint address = symbolTable.GetAddressFromSymbol(symbol);
                    GUILayout.Label($"Symbol: {symbol}, type: {heap.GetHeapVariableType(address)}, obj: {heap.GetHeapVariable(address)}");
                }
            }

            _showHash = EditorGUILayout.Foldout(_showHash, "Trigger Hash", true);
            if (_showHash)
            {
                EditorGUI.BeginDisabledGroup(true);

                EditorGUILayout.TextArea(triggerHash);
                EditorGUILayout.TextArea(CyanTriggerInstanceDataHash.HashCyanTriggerInstanceData(ctDataInstance));
                EditorGUILayout.TextArea(CyanTriggerInstanceDataHash.GetProgramUniqueStringForCyanTrigger(ctDataInstance));

                EditorGUI.EndDisabledGroup();
            }
#endif
            
            ExtraDebugRendering?.Invoke(this);
        }

        public static void ClearPublicUdonVariables(UdonBehaviour udonBehaviour, ref bool dirty)
        {
            IUdonVariableTable publicVariables = udonBehaviour.publicVariables;
            if (publicVariables == null)
            {
                return;
            }
            
            foreach(string publicVariableSymbol in new List<string>(publicVariables.VariableSymbols))
            {
                publicVariables.RemoveVariable(publicVariableSymbol);
                dirty = true;
            }
        }

        public void ApplyCyanTriggerToUdon(
            CyanTriggerSerializableInstance triggerInstance, 
            UdonBehaviour udonBehaviour,
            ref bool dirty)
        {
            if (publicVariableReferences == null)
            {
#if CYAN_TRIGGER_DEBUG
                // TODO figure out why serialization is failing here.
                Debug.LogError($"Variable references are null for program: {name}");
#endif
                CompileTrigger();
            }
            
            UpdatePublicVariables(triggerInstance.triggerDataInstance, udonBehaviour, ref dirty);

            ApplyUdonProperties(triggerInstance, udonBehaviour, ref dirty);
        }

        protected void UpdatePublicVariables(
            CyanTriggerDataInstance triggerDataInstance, 
            UdonBehaviour udonBehaviour, 
            ref bool dirty,
            CyanTriggerSerializableObject[] variableData = null)
        {
            if (EditorApplication.isPlaying || HasErrors())
            {
                return;
            }

            if (program == null && serializedUdonProgramAsset != null)
            {
                program = serializedUdonProgramAsset.RetrieveProgram();
                dirty = true;
            }

            if (program == null)
            {
#if CYAN_TRIGGER_DEBUG
                Debug.LogWarning($"CyanTrigger program is null: {name}");
#endif
                return;
            }

            publicVariableReferences?.ApplyPublicVariableData(
                triggerDataInstance?.variables,
                variableData,
                triggerDataInstance?.events,
                GetCyanTriggerProgramName(),
                udonBehaviour,
                program.SymbolTable,
                ref dirty);
        }

        private void ApplyUdonProperties(
            CyanTriggerSerializableInstance triggerInstance, 
            UdonBehaviour udonBehaviour, 
            ref bool dirty)
        {
            if (!Mathf.Approximately(triggerInstance.proximity, udonBehaviour.proximity))
            {
                // Debug.Log("Dirty after updating interact proximity");
                udonBehaviour.proximity = triggerInstance.proximity;
                dirty = true;
            }

            if (triggerInstance.interactText != udonBehaviour.interactText)
            {
                // Debug.Log("Dirty after updating interact text");
                udonBehaviour.interactText = triggerInstance.interactText;
                dirty = true;
            }

            ApplyUdonDataProperties(triggerInstance.triggerDataInstance, udonBehaviour, ref dirty);
        }

        protected void ApplyUdonDataProperties(
            CyanTriggerDataInstance triggerInstance,
            UdonBehaviour udonBehaviour, 
            ref bool dirty)
        {
            if (!udonBehaviour)
            {
                return;
            }
            
            // TODO remove when dropping 2018 support.
#if !UNITY_2019_4_OR_NEWER
            bool reliableSync = CyanTriggerUtil.GetSyncMode(triggerInstance, udonBehaviour);

            if (udonBehaviour.Reliable != reliableSync)
            {
                udonBehaviour.Reliable = reliableSync;
                dirty = true;
            }
#else
            Networking.SyncType syncType = 
                CyanTriggerUtil.GetSyncMode(triggerInstance, udonBehaviour, shouldBeNetworked);

            if (udonBehaviour.SyncMethod != syncType)
            {
                // Debug.Log("Dirty after applying sync mode");
                udonBehaviour.SyncMethod = syncType;
                dirty = true;
            }
#endif
        }
        
        protected override void RefreshProgramImpl()
        {
            RehashAndCompile();
        }
        
        private void ApplyDefaultValuesToHeap(Dictionary<string, (object value, Type type)> heapDefaultValues)
        {
            if (heapDefaultValues == null)
            {
                return;
            }
            
            IUdonSymbolTable symbolTable = program?.SymbolTable;
            IUdonHeap heap = program?.Heap;
            if (symbolTable == null || heap == null)
            {
                return;
            }

            foreach (var defaultValue in heapDefaultValues)
            {
                if (!symbolTable.HasAddressForSymbol(defaultValue.Key))
                {
                    continue;
                }

                uint symbolAddress = symbolTable.GetAddressFromSymbol(defaultValue.Key);
                (object value, Type declaredType) = defaultValue.Value;
                if (value is UdonGameObjectComponentHeapReference)
                {
                    declaredType = typeof(UdonGameObjectComponentHeapReference);
                }
                if (typeof(UnityEngine.Object).IsAssignableFrom(declaredType))
                {
                    if (value != null && !declaredType.IsInstanceOfType(value))
                    {
                        heap.SetHeapVariable(symbolAddress, null, declaredType);
                        continue;
                    }

                    if ((UnityEngine.Object)value == null)
                    {
                        heap.SetHeapVariable(symbolAddress, null, declaredType);
                        continue;
                    }
                }

                if (value != null)
                {
                    if (!declaredType.IsInstanceOfType(value))
                    {
                        value = declaredType.IsValueType ? Activator.CreateInstance(declaredType) : null;
                    }
                }

                heap.SetHeapVariable(symbolAddress, value, declaredType);
            }
        }

        public virtual string GetDefaultCyanTriggerProgramName()
        {
            return "CyanTrigger";
        }
        
        public string GetCyanTriggerProgramName()
        {
            string programName = ctDataInstance?.programName;
            if (string.IsNullOrEmpty(programName))
            {
                return GetDefaultCyanTriggerProgramName();
            }

            return programName;
        }

        public bool HasErrors()
        {
            return string.IsNullOrEmpty(udonAssembly)
                   || (errorMessages != null && errorMessages.Length > 0);
        }
        
        // Used to know if importing a Program asset should be recompiled.
        // Udon stores compiled programs in another file that is not imported,
        // thus when importing CT programs, they will have differing data until recompiled.
        public bool SerializedProgramHashMatchesExpectedHash()
        {
            if (program == null && serializedUdonProgramAsset != null)
            {
                program = serializedUdonProgramAsset.RetrieveProgram();
            }
            
            var heap = program?.Heap;
            var symbolTable = program?.SymbolTable;
            if (heap == null || symbolTable == null)
            {
                return false;
            }

            string hashVariableName = CyanTriggerAssemblyData.GetSpecialVariableName(
                CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ProgramHash);
            if (!symbolTable.TryGetAddressFromSymbol(hashVariableName, out uint hashAddress))
            {
                return false;
            }

            if (!heap.TryGetHeapVariable(hashAddress, out string compiledHash))
            {
                return false;
            }
            
            return compiledHash == triggerHash;
        }
        
        public void SetCyanTriggerData(CyanTriggerDataInstance dataInstance, string hash)
        {
            triggerHash = hash;
            ctDataInstance = CyanTriggerCopyUtil.CopyCyanTriggerDataInstance(dataInstance, false);
        }

        public CyanTriggerDataInstance GetCyanTriggerData()
        {
            return ctDataInstance;
        }

        public CyanTriggerDataInstance GetCopyOfCyanTriggerData()
        {
            return CyanTriggerCopyUtil.CopyCyanTriggerDataInstance(ctDataInstance, false);
        }

        public (CyanTriggerSerializableObject[], string[]) GetDefaultVariableData()
        {
            var variableData = ctDataInstance.variables;
            if (variableData == null)
            {
                return (Array.Empty<CyanTriggerSerializableObject>(), Array.Empty<string>());
            }

            var varData = new CyanTriggerSerializableObject[variableData.Length];
            var varGuid = new string[variableData.Length];
            for (int index = 0; index < varData.Length; ++index)
            {
                var variable = variableData[index];
                bool dirty = false;
                object data = CyanTriggerPropertyEditor.CreateInitialValueForType(
                    variable.type.Type, 
                    variable.data.Obj, 
                    ref dirty);
                varData[index] = new CyanTriggerSerializableObject(data);
                varGuid[index] = variable.variableID;
            }

            return (varData, varGuid);
        }

        public string GetUdonAssembly()
        {
            return udonAssembly;
        }

        public IUdonProgram GetUdonProgram()
        {
            return program;
        }

        public void InvalidateData()
        {
            triggerHash = name;
            ctDataInstance = null;
        }

        public bool Rehash()
        {
            if (CyanTriggerSerializedProgramManager.IsDefaultEmptyProgram(this))
            {
                return false;
            }
            
            string hash = CyanTriggerInstanceDataHash.HashCyanTriggerInstanceData(ctDataInstance);
            bool hashDiffers = hash != triggerHash;
            if (hashDiffers)
            {
                triggerHash = hash;
                EditorUtility.SetDirty(this);
            }

            return hashDiffers;
        }
        
        public bool RehashAndCompile()
        {
            Rehash();
            return CompileTrigger();
        }
        
        public bool CompileTrigger()
        {
            HasUncompiledChanges = false;
            return CyanTriggerCompiler.CompileCyanTrigger(ctDataInstance, this, triggerHash);
        }

        public void PrintErrorsAndWarnings()
        {
            if (warningMessages != null && warningMessages.Length > 0)
            {
                foreach (var warning in warningMessages)
                {
                    Debug.LogWarning(warning);
                }
            }

            if (errorMessages != null && errorMessages.Length > 0)
            {
                foreach (var error in errorMessages)
                {
                    Debug.LogError(error);
                }
            }
        }

        public void SetCompiledData(
            string hash, 
            string assembly,
            IUdonProgram compiledUdonProgram,
            Dictionary<string, (object value, Type type)> variables,
            CyanTriggerDataReferences varReferences,
            CyanTriggerDataInstance dataInstance,
            List<string> warnings,
            List<string> errors,
            bool shouldNetwork = false)
        {
            triggerHash = hash;
            udonAssembly = assembly;
            program = compiledUdonProgram;
            ctDataInstance = dataInstance;
            shouldBeNetworked = shouldNetwork;

#pragma warning disable CS0612
            publicVariableReferencesOdin = null;
#pragma warning restore CS0612
            publicVariableReferences = varReferences;

            warningMessages = warnings?.ToArray();
            errorMessages = errors?.ToArray();
            
            ApplyDefaultValuesToHeap(variables);
            
            SerializedProgramAsset.StoreProgram(compiledUdonProgram);
            
            UpdateUdonErrors();
            
            EditorUtility.SetDirty(this);

            HasUncompiledChanges = false;
        }

        private void UpdateUdonErrors()
        {
            if (errorMessages != null && errorMessages.Length > 0)
            {
                assemblyError = string.Join("\n", errorMessages);
            }
            else
            {
                assemblyError = null;
            }
        }


        protected override void OnBeforeSerialize()
        {
#pragma warning disable CS0612
            if (ctDataInstanceOdin != null)
            {
                ctDataInstance = ctDataInstanceOdin;
                ctDataInstanceOdin = null;
            }
            if (publicVariableReferencesOdin != null)
            {
                publicVariableReferences = new CyanTriggerDataReferences(
                    publicVariableReferencesOdin.actionDataIndicesOdin, 
                    publicVariableReferencesOdin.userVariablesOdin);
                publicVariableReferencesOdin = null;
            }
            if (!ignoreOdinData)
            {
                ignoreOdinData = true;
                serializationDataOdin = new SerializationData();
            }
#pragma warning restore CS0612
            
            // Reference to how odin data was serialized before
            // UnitySerializationUtility.SerializeUnityObject(this, ref serializationData);
            base.OnBeforeSerialize();
        }
        
        protected override void OnAfterDeserialize()
        {
            base.OnAfterDeserialize();
            
#pragma warning disable CS0612 
            if (!ignoreOdinData)
            {
                UnitySerializationUtility.DeserializeUnityObject(this, ref serializationDataOdin);
            }
            
            if (ctDataInstanceOdin != null)
            {
                ctDataInstance = ctDataInstanceOdin;
                ctDataInstanceOdin = null;
            }
            if (publicVariableReferencesOdin != null)
            {
                publicVariableReferences = new CyanTriggerDataReferences(
                    publicVariableReferencesOdin.actionDataIndicesOdin, 
                    publicVariableReferencesOdin.userVariablesOdin);
                publicVariableReferencesOdin = null;
            }
#pragma warning restore CS0612
        }
    }
}
