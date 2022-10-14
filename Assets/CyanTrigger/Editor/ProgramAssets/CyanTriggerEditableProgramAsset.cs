using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Editor.ProgramSources.Attributes;

[assembly: UdonProgramSourceNewMenu(typeof(Cyan.CT.Editor.CyanTriggerEditableProgramAsset), "CyanTrigger Program Asset")]

namespace Cyan.CT.Editor
{
    [CreateAssetMenu(menuName = "CyanTrigger/CyanTrigger Program Asset", fileName = "New CyanTrigger Program Asset", order = 6)]
    [HelpURL(CyanTriggerDocumentationLinks.EditableProgramAsset)]
    public class CyanTriggerEditableProgramAsset : CyanTriggerProgramAsset
    {
        // Methods mainly for CyanTriggerAsset's inspector
        public bool allowEditingInInspector;
        public bool expandInInspector;

#if !CYAN_TRIGGER_DEBUG
        [HideInInspector]
#endif
        public bool isLocked;
        
        protected override void DrawProgramSourceGUI(UdonBehaviour udonBehaviour, ref bool dirty)
        {
            ApplyUdonDataProperties(ctDataInstance, udonBehaviour, ref dirty);

            var ctAsset = GetMatchingCyanTriggerAsset(udonBehaviour);

            // Only show CTAsset field if this is for a GameObject rather than viewing the program directly.
            if (udonBehaviour != null)
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField("CyanTriggerAsset", ctAsset, typeof(CyanTriggerAsset), true);
                EditorGUI.EndDisabledGroup();
            }
            
            if (ctAsset != null)
            {
                VerifyAssetVariables(ctAsset);
                ApplyToUdon(ctAsset, udonBehaviour, ref dirty);
            }
            else if (udonBehaviour != null)
            {
                // Draw info box and button to add CyanTriggerAsset
                EditorGUILayout.HelpBox("Add CyanTriggerAsset for better inspector", MessageType.Warning);
                if (GUILayout.Button("Add CyanTriggerAsset"))
                {
                    CyanTriggerAsset.AddFromUdonBehaviour(udonBehaviour);
                    return;
                }
                DrawPublicVariables(udonBehaviour, ref dirty);
            }
            
            ShowGenericInspectorGUI(udonBehaviour, ref dirty, true);
            
            ShowDebugInformation(udonBehaviour, ref dirty);
        }

        private static CyanTriggerAsset GetMatchingCyanTriggerAsset(UdonBehaviour udonBehaviour)
        {
            if (udonBehaviour == null)
            {
                return null;
            }

            foreach (var ctAsset in udonBehaviour.GetComponents<CyanTriggerAsset>())
            {
                if (ctAsset.assetInstance?.udonBehaviour == udonBehaviour)
                {
                    return ctAsset;
                }
            }

            return null;
        }

        public void ApplyToUdon(CyanTriggerAsset ctAsset, UdonBehaviour udonBehaviour, ref bool dirty)
        {
            if (HasUncompiledChanges)
            {
                return;
            }

            var assetInstance = ctAsset.assetInstance;
            VerifyAssetVariableValuesMatchType(ctAsset, ctDataInstance?.variables);
            UpdatePublicVariables(ctDataInstance, udonBehaviour, ref dirty, assetInstance.variableData);
            
            if (!Mathf.Approximately(assetInstance.proximity, udonBehaviour.proximity))
            {
                udonBehaviour.proximity = assetInstance.proximity;
                dirty = true;
            }

            if (assetInstance.interactText != udonBehaviour.interactText)
            {
                udonBehaviour.interactText = assetInstance.interactText;
                dirty = true;
            }
        }

        public void ApplyToUdon(CyanTriggerAsset ctAsset)
        {
            if (HasUncompiledChanges)
            {
                return;
            }

            var assetInstance = ctAsset.assetInstance;
            UdonBehaviour udonBehaviour = assetInstance?.udonBehaviour;
            if (udonBehaviour == null)
            {
                return;
            }
            
            bool dirty = false;
            ApplyUdonDataProperties(ctDataInstance, udonBehaviour, ref dirty);
            ApplyToUdon(ctAsset, udonBehaviour, ref dirty);

            if (dirty)
            {
                EditorUtility.SetDirty(udonBehaviour);

                if (PrefabUtility.IsPartOfPrefabInstance(ctAsset))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(ctAsset);
                }
            }
        }
        
        public bool TryVerifyAndMigrate()
        {
            bool dirty = false;

            dirty |= TryMigrateTrigger();
            dirty |= TryVerify();

            return dirty;
        }

        public bool TryVerify()
        {
            bool dirty = false;
#if CYAN_TRIGGER_DEBUG
            string path = AssetDatabase.GetAssetPath(this);
            if (path.Contains("DefaultCustomActions"))
            {
                // Always lock custom actions.
                if (!isLocked)
                {
                    Debug.Log($"Program set locked: {path}");
                    isLocked = true;
                    dirty = true;
                    EditorUtility.SetDirty(this);
                }

                if (!ctDataInstance.ignoreEventWarnings)
                {
                    Debug.Log($"Program set to ignore event warnings: {path}");
                    ctDataInstance.ignoreEventWarnings = true;
                    dirty = true;
                    EditorUtility.SetDirty(this);
                }

                // Guarantee Default Actions are None when not synced and Manual when synced.
                if (ctDataInstance.autoSetSyncMode == false 
                    || ctDataInstance.programSyncMode != CyanTriggerProgramSyncMode.ManualWithAutoRequest)
                {
                    Debug.Log($"Program with wrong sync: {ctDataInstance.autoSetSyncMode} {ctDataInstance.programSyncMode}, {path}");
                    
                    ctDataInstance.autoSetSyncMode = true;
                    ctDataInstance.programSyncMode = CyanTriggerProgramSyncMode.ManualWithAutoRequest;
                    
                    dirty = true;
                    EditorUtility.SetDirty(this);
                }
            }
#endif
            
            bool verifyDirty = CyanTriggerUtil.ValidateTriggerData(ctDataInstance);
            if (verifyDirty)
            {
                dirty = true;
#if CYAN_TRIGGER_DEBUG
                Debug.Log($"Setting CyanTrigger Program dirty after verification: {AssetDatabase.GetAssetPath(this)}");
#endif
                EditorUtility.SetDirty(this);
            }
            
            return dirty;
        }
        
        public bool TryMigrateTrigger()
        {
            int prevVersion = ctDataInstance.version;
            if (CyanTriggerVersionMigrator.MigrateTrigger(ctDataInstance))
            {
#if CYAN_TRIGGER_DEBUG
                Debug.Log($"Migrated CyanTrigger Program from version {prevVersion} to version {ctDataInstance.version}, {AssetDatabase.GetAssetPath(this)}");
#endif
                EditorUtility.SetDirty(this);

                return true;
            }

            return false;
        }

        public override string GetDefaultCyanTriggerProgramName()
        {
            return name;
        }
        
        #region Variable Verification
        
        private static readonly HashSet<GameObject> VerifyVariablePrefabsProcessedCache = new HashSet<GameObject>();
        
        public void VerifyAndApply(List<CyanTriggerAsset> ctAssets)
        {
            VerifyAssetVariables(ctAssets);
            foreach (var ctAsset in ctAssets)
            {
                ApplyToUdon(ctAsset);
            }
        }

        public bool VerifyAssetVariables(IEnumerable<CyanTriggerAsset> ctAssets)
        {
            var variables = ctDataInstance.variables;

            VerifyVariablePrefabsProcessedCache.Clear();
            bool anyUpdated = false;
            foreach (var ctAsset in ctAssets)
            {
                anyUpdated |= VerifyAssetVariables(ctAsset, variables);
            }

            VerifyVariablePrefabsProcessedCache.Clear();

            return anyUpdated;
        }


        private void VerifyAssetVariables(CyanTriggerAsset ctAsset)
        {
            VerifyVariablePrefabsProcessedCache.Clear();
            var variables = ctDataInstance.variables;
            VerifyAssetVariables(ctAsset, variables);

            VerifyVariablePrefabsProcessedCache.Clear();
        }

        private static bool VerifyAssetVariables(CyanTriggerAsset ctAsset, CyanTriggerVariable[] variables)
        {
            if (!DoesAssetNeedUpdating(ctAsset, variables))
            {
                return false;
            }

            if (PrefabUtility.IsPartOfPrefabInstance(ctAsset))
            {
                VerifyPrefabAssetVariables(ctAsset, variables);
            }
            else
            {
                UpdateCyanTriggerAssetVariables(ctAsset, variables);
            }

            return true;
        }

        // Ensure that each data element contains data that corresponds to the proper type for the variable.
        private static bool VerifyAssetVariableValuesMatchType(
            CyanTriggerAsset ctAsset,
            CyanTriggerVariable[] variables)
        {
            var assetInstance = ctAsset.assetInstance;
            var varData = assetInstance.variableData;

            bool changes = false;
            if (varData == null)
            {
                varData = new CyanTriggerSerializableObject[variables.Length];
                changes = true;
            }

            if (varData.Length != variables.Length)
            {
                changes = true;
                Array.Resize(ref varData, variables.Length);
            }

            for (int index = 0; index < variables.Length; ++index)
            {
                var variable = variables[index];
                var type = variable.type.Type;
                if (varData[index] == null)
                {
                    varData[index] =
                        new CyanTriggerSerializableObject(CyanTriggerPropertyEditor.GetDefaultForType(type));
                    changes = true;
                    continue;
                }

                bool badData = false;
                var data = CyanTriggerPropertyEditor.CreateInitialValueForType(type, varData[index].Obj, ref badData);
                if (badData)
                {
                    varData[index].Obj = data;
                    changes = true;
                }
            }

            if (changes)
            {
                Undo.RecordObject(ctAsset, Undo.GetCurrentGroupName());
                assetInstance.variableData = varData;
                
                if (PrefabUtility.IsPartOfPrefabInstance(ctAsset))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(ctAsset);
                }
            }

            return changes;
        }

        private static bool DoesAssetNeedUpdating(CyanTriggerAsset ctAsset, CyanTriggerVariable[] variables)
        {
            // Verify data stored in the ct asset matches the program. 
            var assetInstance = ctAsset.assetInstance;
            var varData = assetInstance.variableData;
            var varGuids = assetInstance.variableGuids;

            if (varData == null
                || varGuids == null
                || varData.Length != varGuids.Length
                || varData.Length != variables.Length)
            {
                return true;
            }

            for (int index = 0; index < variables.Length; ++index)
            {
                if (variables[index].variableID != varGuids[index])
                {
                    return true;
                }
            }

            return false;
        }

        private static void VerifyPrefabAssetVariables(CyanTriggerAsset ctAsset, CyanTriggerVariable[] variables)
        {
            Dictionary<string, int> guidRemap = new Dictionary<string, int>();
            for (var index = 0; index < variables.Length; ++index)
            {
                var variable = variables[index];
                guidRemap.Add(variable.variableID, index);
            }
            
            int varSize = variables.Length;
            string sizeString = varSize.ToString();

            string assetInstancePath = nameof(CyanTriggerAsset.assetInstance);
            string variableDataPath =
                $"{assetInstancePath}.{nameof(CyanTriggerAssetSerializableInstance.variableData)}";
            string variableGuidsPath =
                $"{assetInstancePath}.{nameof(CyanTriggerAssetSerializableInstance.variableGuids)}";
            string guidArraySizePath = $"{variableGuidsPath}.Array.size";
            string dataArraySizePath = $"{variableDataPath}.Array.size";
            
            // Given a property path, find the first array index.
            int GetIndex(string path)
            {
                int openBrakLoc = path.IndexOf('[');
                int endBrakLoc = path.IndexOf(']');
                if (openBrakLoc == -1 || endBrakLoc == -1)
                {
                    return -1;
                }

                ++openBrakLoc;
                if (!int.TryParse(path.Substring(openBrakLoc, endBrakLoc - openBrakLoc), out int results))
                {
                    results = -1;
                }

                return results;
            }
            
            // Go through prefab modifications that match this CyanTriggerAsset program
            // for each data modification, find associated guid modification update data modification's index
            bool ProcessCyanTriggerAssetPrefabChanges(
                CyanTriggerAsset processingAsset,
                List<PropertyModification> ctMods,
                List<PropertyModification> allMods)
            {
                Dictionary<int, PropertyModification> guids = new Dictionary<int, PropertyModification>();
                // CyanTriggerSerializedObject encodes multiple items
                Dictionary<int, List<PropertyModification>> data = new Dictionary<int, List<PropertyModification>>();
                bool foundChanges = false;
                
                // Go through each modification for this CyanTriggerAsset
                // Map all data and guid mods based on array index
                foreach (var modification in ctMods)
                {
                    string propertyPath = modification.propertyPath;
                    if (propertyPath.StartsWith(variableDataPath))
                    {
                        int index = GetIndex(propertyPath);
                        if (index != -1)
                        {
                            if (!data.TryGetValue(index, out var mods))
                            {
                                mods = new List<PropertyModification>();
                                data.Add(index, mods);
                            }

                            mods.Add(modification);
                        }
                    }
                    else if (propertyPath.StartsWith(variableGuidsPath))
                    {
                        int index = GetIndex(propertyPath);
                        if (index != -1)
                        {
                            guids[index] = modification;

                            foundChanges |= index >= variables.Length ||
                                            variables[index].variableID != modification.value;
                        }
                    }
                    else
                    {
                        // Other fields should be added directly
                        allMods.Add(modification);
                    }
                }

                // If less guids found does not match the variable count, then we know we have changes. 
                foundChanges |= varSize != guids.Count;

                // Go through each data index and remap the PropertyModifications to the new index.
                foreach (var dataChanges in data)
                {
                    int index = dataChanges.Key;
                    var dataMods = dataChanges.Value;
                    
                    // If a guid doesn't have an index but data does, this is a problem.
                    // This shouldn't happen in normal situations as all Guids should be saved.
                    if (!guids.TryGetValue(index, out var guidMod))
                    {
                        Debug.LogWarning($"[CyanTriggerAsset] Missing guid for data[{index}] on remap.");
                        continue;
                    }

                    // Check if Guid still exists. If not, ignore these mods as they were removed.
                    if (!guidRemap.TryGetValue(guidMod.value, out int newIndex))
                    {
                        // Guid doesn't exist, so force changes. 
                        foundChanges = true;
                        continue;
                    }

                    // If new index is the same as the old, do nothing and just add the mods back with no changes.
                    if (newIndex == index)
                    {
                        foreach (var mod in dataMods)
                        {
                            allMods.Add(mod);
                        }

                        continue;
                    }

                    // We know data has a new index, update the paths with the new index.
                    foundChanges = true;

                    string GetUpdatedPath(string path)
                    {
                        int openBrakLoc = path.IndexOf('[');
                        int endBrakLoc = path.IndexOf(']');
                        string front = path.Substring(0, openBrakLoc + 1);
                        string end = path.Substring(endBrakLoc);
                        return $"{front}{newIndex}{end}";
                    }

                    foreach (var mod in dataMods)
                    {
                        string newPath = GetUpdatedPath(mod.propertyPath);
                        mod.propertyPath = newPath;
                        allMods.Add(mod);
                    }
                }
                
                allMods.Add(
                    new PropertyModification
                    {
                        target = processingAsset,
                        value = sizeString,
                        propertyPath = guidArraySizePath
                    }
                );
                allMods.Add(
                    new PropertyModification
                    {
                        target = processingAsset,
                        value = sizeString,
                        propertyPath = dataArraySizePath
                    }
                );

                // Add all guids back as property modifications.
                for (int index = 0; index < varSize; ++index)
                {
                    allMods.Add(
                        new PropertyModification
                        {
                            target = processingAsset,
                            value = variables[index].variableID,
                            propertyPath = $"{variableGuidsPath}.Array.data[{index}]",
                        }
                    );
                }
                
                // Update all non prefab changed serialized data.
                UpdateCyanTriggerAssetVariables(processingAsset, variables);
                
                return foundChanges;
            }

            // Gather all along with its prefab modifications, then process each individually, assuming no changes. 
            GameObject prefabInstanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(ctAsset);
            while (prefabInstanceRoot != null)
            {
                GameObject prefabRoot = prefabInstanceRoot;
                // Loop through all variants for this prefab.
                while (prefabRoot != null)
                {
                    // Ignore prefabs we have already processed and cached.
                    if (VerifyVariablePrefabsProcessedCache.Contains(prefabRoot))
                    {
                        prefabRoot = PrefabUtility.GetCorrespondingObjectFromSource(prefabRoot);
                        continue;
                    }
                    VerifyVariablePrefabsProcessedCache.Add(prefabRoot);

                    var prefabModifications = PrefabUtility.GetPropertyModifications(prefabRoot);
                    if (prefabModifications != null)
                    {
                        Dictionary<CyanTriggerAsset, List<PropertyModification>> modsPerAsset =
                            new Dictionary<CyanTriggerAsset, List<PropertyModification>>();
                        
                        List<PropertyModification> modifications = new List<PropertyModification>();

                        // Go through all modifications and save all CyanTriggerAsset mods based on the expected program.
                        foreach (var modification in prefabModifications)
                        {
                            // If target is a CyanTrigger asset with the same program as we are dealing with, save it.
                            if (modification.target is CyanTriggerAsset asset 
                                && CyanTriggerAsset.AssetsHaveSameProgram(asset, ctAsset))
                            {
                                if (!modsPerAsset.TryGetValue(asset, out var mods))
                                {
                                    mods = new List<PropertyModification>();
                                    modsPerAsset.Add(asset, mods);
                                }
                                mods.Add(modification);
                            }
                            else
                            {
                                modifications.Add(modification);
                            }
                        }
                        
                        bool changes = false;
                        foreach (var ctMods in modsPerAsset)
                        {
                            changes |= 
                                ProcessCyanTriggerAssetPrefabChanges(ctMods.Key, ctMods.Value, modifications);
                        }

                        if (changes)
                        {
                            PrefabUtility.SetPropertyModifications(prefabRoot, modifications.ToArray());
                        }
                    }
                    
                    prefabRoot = PrefabUtility.GetCorrespondingObjectFromSource(prefabRoot);
                }
            
                Transform nextPar = prefabInstanceRoot.transform.parent;
                prefabInstanceRoot = nextPar != null ? PrefabUtility.GetNearestPrefabInstanceRoot(nextPar) : null;
            }
        }

        // Go through all variable data and guids, pair them together, and then reorder them based on the provided variables
        private static void UpdateCyanTriggerAssetVariables(CyanTriggerAsset ctAsset, CyanTriggerVariable[] variables)
        {
            int variableCount = variables.Length;

            var assetInstance = ctAsset.assetInstance;
            var varGuids = assetInstance.variableGuids;

            Undo.RecordObject(ctAsset, Undo.GetCurrentGroupName());

            // This case shouldn't happen normally, but may happen when migrating old data. 
            // Assume the data is "correct" and only update guid order. 
            if (varGuids == null)
            {
                varGuids = assetInstance.variableGuids = new string[variableCount];
                for (int index = 0; index < variableCount; ++index)
                {
                    varGuids[index] = variables[index].variableID;
                }

                return;
            }
            
            var varData = assetInstance.variableData;
            
            // Data is miss-matched. Try to recreate align.
            Dictionary<string, CyanTriggerSerializableObject> idToData =
                new Dictionary<string, CyanTriggerSerializableObject>();

            if (varData == null)
            {
                varData = assetInstance.variableData = Array.Empty<CyanTriggerSerializableObject>();
            }

            // Match the data to each guid for easy look up.
            if (varData.Length == varGuids.Length)
            {
                for (int index = 0; index < varGuids.Length; ++index)
                {
                    string id = varGuids[index];
                    if (!string.IsNullOrEmpty(id))
                    {
                        idToData.Add(id, varData[index]);
                    }
                }
            }

            // Update the arrays to be the proper length given the actual variables. 
            Array.Resize(ref assetInstance.variableData, variableCount);
            Array.Resize(ref assetInstance.variableGuids, variableCount);

            varData = assetInstance.variableData;
            varGuids = assetInstance.variableGuids;

            // Rearrange data to match actual variable guid orders. 
            for (int index = 0; index < variableCount; ++index)
            {
                var variable = variables[index];
                string id = variable.variableID;
                varGuids[index] = id;
                if (!idToData.TryGetValue(id, out varData[index]))
                {
                    bool _ = false;
                    var data = CyanTriggerPropertyEditor.CreateInitialValueForType(variable.type.Type, variable.data.Obj, ref _);
                    varData[index] = new CyanTriggerSerializableObject(data);
                }
            }

            if (PrefabUtility.IsPartOfPrefabInstance(ctAsset))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(ctAsset);
            }
        }
        
        public static void ForceGuidPrefabOverride(
            CyanTriggerAsset[] ctAssets,
            SerializedProperty variableGuidsProperty, 
            SerializedProperty variableDataProperty)
        {
            if (!variableGuidsProperty.isInstantiatedPrefab 
                || (variableGuidsProperty.prefabOverride && variableDataProperty.prefabOverride))
            {
                return;
            }

            int size = variableGuidsProperty.arraySize;
            string sizeString = size.ToString();
            
            string guidArrayPropPath = variableGuidsProperty.propertyPath;
            string dataArrayPropPath = variableDataProperty.propertyPath;
            // The array itself doesnt actually hold the property.
            string guidArrayStartPath = $"{guidArrayPropPath}.Array.";
            string guidArraySizePath = $"{guidArrayStartPath}size";
            string dataArraySizePath = $"{dataArrayPropPath}.Array.size";
            
            foreach (var ctAsset in ctAssets)
            {
                CyanTriggerAsset prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(ctAsset);
                
                if (prefabAsset == null)
                {
                    continue;
                }
                
                List<PropertyModification> modifications = new List<PropertyModification>();
                
                // TODO make generic method for adding these properties and share it with the verify method above.
                modifications.Add(
                    new PropertyModification
                    {
                        target = prefabAsset,
                        value = sizeString,
                        propertyPath = guidArraySizePath
                    }
                );

                modifications.Add(
                    new PropertyModification
                    {
                        target = prefabAsset,
                        value = sizeString,
                        propertyPath = dataArraySizePath
                    }
                );
                
                for (int index = 0; index < size; ++index)
                {
                    SerializedProperty guidProp = variableGuidsProperty.GetArrayElementAtIndex(index);
                    modifications.Add(
                        new PropertyModification
                        {
                            target = prefabAsset,
                            value = guidProp.stringValue,
                            propertyPath = guidProp.propertyPath,
                        }
                    );
                }

                var overrides = PrefabUtility.GetPropertyModifications(ctAsset);
                if (overrides != null)
                {
                    foreach (var mod in overrides)
                    {
                        // Ignore non CyanTriggerAsset modifications
                        if (!(mod.target is CyanTriggerAsset modAsset))
                        {
                            modifications.Add(mod);
                            continue;
                        }

                        // Ignore CyanTriggerAsset prefab changes that are unrelated to the data or guid arrays.
                        if (modAsset == prefabAsset && 
                            (mod.propertyPath == dataArraySizePath || mod.propertyPath.StartsWith(guidArrayStartPath)))
                        {
                            continue;
                        }
                        modifications.Add(mod);
                    }
                }

                PrefabUtility.SetPropertyModifications(ctAsset, modifications.ToArray());
            }
        }

        // Used for debugging
        public static void LogOverrides(CyanTriggerAsset[] ctAssets)
        {
            foreach (var ctAsset in ctAssets)
            {
                var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(ctAsset);
                if (prefabAsset == null)
                {
                    continue;
                }
                
                var overrides = PrefabUtility.GetPropertyModifications(ctAsset);
                if (overrides != null)
                {
                    foreach (var mod in overrides)
                    {
                        if (mod.target is CyanTriggerAsset)
                        {
                            Debug.Log($"{mod.propertyPath}, {mod.value}, {mod.objectReference}");
                        }
                    }
                }
            }
        }

        #endregion
    }
}