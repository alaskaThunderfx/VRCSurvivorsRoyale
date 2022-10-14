using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace Cyan.CT.Editor
{
    public class CyanTriggerSerializedProgramManager : UnityEditor.AssetModificationProcessor
    {
        public const string SerializedUdonAssetNamePrefix = "CyanTrigger_";
        public const string SerializedUdonPath = "CyanTriggerSerialized";
        public const string DefaultProgramAssetGuid = "Empty";
        
        private static readonly object Lock = new object();
        
        private static CyanTriggerSerializedProgramManager _instance;
        public static CyanTriggerSerializedProgramManager Instance
        {
            get
            {
                lock (Lock)
                {
                    if (_instance == null)
                    {
                        _instance = new CyanTriggerSerializedProgramManager();
                    }

                    return _instance;
                }
            }
        }

        private readonly Dictionary<string, CyanTriggerProgramAsset> _programAssets =
            new Dictionary<string, CyanTriggerProgramAsset>();

        private CyanTriggerProgramAsset _defaultProgramAsset;
        public CyanTriggerProgramAsset DefaultProgramAsset
        {
            get
            {
                if (_defaultProgramAsset == null)
                {
                    LoadSerializedData();
                }
                return _defaultProgramAsset;
            }
        }


        /*
        // TODO enable this when updates are needed. Compare performance with using OnEnable instead
        [InitializeOnLoadMethod] 
        private static void InitializeItems()
        {
            if (Application.isPlaying)
            {
                return;
            }
            
            // On every assembly refresh, find all CyanTrigger editable assets and check if they need to be migrated.
            // This will catch the case where a CyanTrigger update requires these to be migrated.
            TryMigrateAllEditableCyanTriggerProgramAssets();
        }
        */
        
        private static string GetExpectedProgramName(string guid)
        {
            return $"{SerializedUdonAssetNamePrefix}{guid}.asset";
        }

        public static bool IsDefaultEmptyProgram(CyanTriggerProgramAsset program)
        {
            if (_instance != null && _instance._defaultProgramAsset)
            {
                return _instance._defaultProgramAsset == program;
            }
            
            return program.triggerHash == DefaultProgramAssetGuid
                   && GetExpectedProgramName(DefaultProgramAssetGuid) != program.name;
        }

        public static void VerifySerializedUdonDirectory()
        {
            string newPath = GetSerializedUdonDirectoryPath();
            if (AssetDatabase.IsValidFolder(newPath))
            {
                return;
            }

            string oldPath = Path.Combine("Assets", SerializedUdonPath);
            if (AssetDatabase.IsValidFolder(oldPath))
            {
                string res = AssetDatabase.MoveAsset(oldPath, newPath);
                if (!string.IsNullOrEmpty(res))
                {
                    Debug.LogError($"Failed to move serialized udon path! {res}");
                }
            }
            else
            {
                string dataPath = CyanTriggerResourceManager.Instance.GetDataPath();
                AssetDatabase.CreateFolder(dataPath, SerializedUdonPath);
            }
        }
        
        private static string GetSerializedUdonDirectoryPath()
        {
            return Path.Combine(CyanTriggerResourceManager.Instance.GetDataPath(), SerializedUdonPath);
        }

        private CyanTriggerProgramAsset CreateTriggerProgramAsset(string guid)
        {
            string assetPath = Path.Combine(GetSerializedUdonDirectoryPath(), GetExpectedProgramName(guid));
            var programAsset = ScriptableObject.CreateInstance<CyanTriggerProgramAsset>();
            
            AssetDatabase.CreateAsset(programAsset, assetPath);
            AssetDatabase.ImportAsset(assetPath);
            return AssetDatabase.LoadAssetAtPath<CyanTriggerProgramAsset>(assetPath);
        }
        
        private static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
        {
            // Skip, since there is nothing to update
            if (_instance == null || _instance._programAssets.Count == 0)
            {
                return AssetDeleteResult.DidNotDelete;
            }
            
            string path = Path.Combine(GetSerializedUdonDirectoryPath(), SerializedUdonAssetNamePrefix);
            if (assetPath.Contains(path) && assetPath.EndsWith(".asset"))
            {
                int startIndex = assetPath.IndexOf(path, StringComparison.Ordinal) + path.Length;
                int len = assetPath.Length - 6 - startIndex;
                string guid = assetPath.Substring(startIndex, len);
                
                // TODO verify this actually removes the asset as the guid in the name may not be what it is stored under.
                if (_instance._programAssets.ContainsKey(guid))
                {
                    _instance._programAssets.Remove(guid);
                }
            }
            
            return AssetDeleteResult.DidNotDelete;
        }
        
        private CyanTriggerSerializedProgramManager()
        {
            if (EditorApplication.isPlaying)
            {
                return;
            }
            
            VerifySerializedUdonDirectory();
            LoadSerializedData();
        }

        private string[] GetAllSerializedCyanTriggerAssetGuids()
        {
            return AssetDatabase.FindAssets(
                $"t:{nameof(CyanTriggerProgramAsset)}",
                new[] { GetSerializedUdonDirectoryPath() });
        }
        
        private void LoadSerializedData()
        {
            string defaultAsset = GetExpectedProgramName(DefaultProgramAssetGuid);
            
            foreach (var guid in GetAllSerializedCyanTriggerAssetGuids())
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var serializedTrigger = AssetDatabase.LoadAssetAtPath<CyanTriggerProgramAsset>(path);
                if (serializedTrigger == null)
                {
                    Debug.LogWarning($"File was not a proper CyanTriggerProgramAsset: {path}");
                    continue;
                }

                if (serializedTrigger is CyanTriggerEditableProgramAsset)
                {
                    Debug.LogWarning($"File is an editable CyanTriggerProgramAsset: {path}");
                }

                if (serializedTrigger.name == defaultAsset)
                {
                    _defaultProgramAsset = serializedTrigger;
                    continue;
                }

                // Handle cases where duplicates exist, eg collab
                if (_programAssets.ContainsKey(serializedTrigger.triggerHash))
                {
                    serializedTrigger.InvalidateData();
                }
                
                _programAssets.Add(serializedTrigger.triggerHash, serializedTrigger);
            }

            if (_defaultProgramAsset == null)
            {
                _defaultProgramAsset = CreateTriggerProgramAsset(DefaultProgramAssetGuid);
            }
            _defaultProgramAsset.SetCyanTriggerData(null, DefaultProgramAssetGuid);
        }

        public void ClearSerializedData()
        {
            _programAssets.Clear();

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var guid in GetAllSerializedCyanTriggerAssetGuids())
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var serializedTrigger = AssetDatabase.LoadAssetAtPath<CyanTriggerProgramAsset>(path);
                    if (serializedTrigger == null || IsDefaultEmptyProgram(serializedTrigger))
                    {
                        continue;
                    }

                    AssetDatabase.DeleteAsset(path);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        public void ApplyTriggerPrograms(List<CyanTrigger> triggers, bool force = false)
        {
            if (EditorApplication.isPlaying)
            {
                throw new Exception("Cannot compile CyanTrigger while in playmode.");
            }
            
            Profiler.BeginSample("CyanTriggerSerializedProgramManager.ApplyTriggerPrograms");

            Dictionary<string, List<CyanTrigger>> hashToTriggers = new Dictionary<string, List<CyanTrigger>>();
            foreach (var trigger in triggers)
            {
                try
                {
                    string hash = CyanTriggerInstanceDataHash.HashCyanTriggerInstanceData(
                        trigger.triggerInstance.triggerDataInstance);

                    // Debug.Log("Trigger hash "+ hash +" " +VRC.Tools.GetGameObjectPath(trigger.gameObject));
                    if (!hashToTriggers.TryGetValue(hash, out var triggerList))
                    {
                        triggerList = new List<CyanTrigger>();
                        hashToTriggers.Add(hash, triggerList);
                    }

                    triggerList.Add(trigger);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to hash CyanTrigger on object: {VRC.Tools.GetGameObjectPath(trigger.gameObject)}");
                    Debug.LogException(e);
                }
            }

            foreach (string key in new List<string>(_programAssets.Keys))
            {
                var programAsset = _programAssets[key];
                if (programAsset == null)
                {
                    _programAssets.Remove(key);
                    continue;
                }

                // Ensure each asset is stored at the proper key
                if (programAsset.triggerHash != key)
                {
                    _programAssets.Remove(key);
                    _programAssets.Remove(programAsset.triggerHash);
                    _programAssets.Add(programAsset.triggerHash, programAsset);
                }
            }
            
            Queue<CyanTriggerProgramAsset> unusedAssets = new Queue<CyanTriggerProgramAsset>();
            foreach (var programAssetPair in _programAssets)
            {
                // Never work with default program
                if (programAssetPair.Value == DefaultProgramAsset)
                {
                    continue;
                }
                
                string hash = programAssetPair.Value.triggerHash;
                if (!hashToTriggers.ContainsKey(hash))
                {
                    unusedAssets.Enqueue(programAssetPair.Value);
                }
            }
            
            // Match trigger hashes to programs and collect programs that need to be recompiled.
            List<CyanTriggerProgramAsset> programsToCompile = new List<CyanTriggerProgramAsset>();
            foreach (var triggerHash in hashToTriggers.Keys)
            {
                List<CyanTrigger> curTriggers = hashToTriggers[triggerHash];
                if (curTriggers.Count == 0)
                {
                    continue;
                }

                bool recompile = force;

                if (!_programAssets.TryGetValue(triggerHash, out var programAsset))
                {
                    // Pull an asset from Unused, or create a new one.
                    if (unusedAssets.Count > 0)
                    {
                        programAsset = unusedAssets.Dequeue();
                        _programAssets.Remove(programAsset.triggerHash);
                    }
                    else
                    {
                        // TODO figure out a better method here since guid is pretty arbitrary
                        programAsset = CreateTriggerProgramAsset(Guid.NewGuid().ToString());
                    }

                    _programAssets.Add(triggerHash, programAsset);
                    recompile = true;
                }

                if (programAsset == DefaultProgramAsset)
                {
                    Debug.LogError("Trying to use default program asset for CyanTrigger!");
                    continue;
                }

                if (recompile)
                {
                    try
                    {
                        var firstTrigger = curTriggers[0];
                        programAsset.SetCyanTriggerData(firstTrigger.triggerInstance.triggerDataInstance, triggerHash);
                        programsToCompile.Add(programAsset);
                    }
                    catch (Exception e)
                    {
                        PrintError("Failed to copy data and compile CyanTrigger on objects: ", curTriggers);
                        Debug.LogException(e);
                    }
                }
            }

            string GetTriggerHash(CyanTriggerProgramAsset program)
            {
                return program.triggerHash;
            }
            
            // Batch compile and get results
            Dictionary<string, bool> results = CyanTriggerCompiler.BatchCompile(programsToCompile, GetTriggerHash);
            if (results == null)
            {
                Debug.LogError("[CyanTrigger] Compiling failed due to errors.");
                Profiler.EndSample();
                return;
            }
            
            // Apply triggers to compiled programs.
            foreach (var triggerHash in hashToTriggers.Keys)
            {
                List<CyanTrigger> curTriggers = hashToTriggers[triggerHash];
                if (curTriggers.Count == 0)
                {
                    continue;
                }

                if (!_programAssets.TryGetValue(triggerHash, out var programAsset))
                {
                    // Log error?
                    continue;
                }
                
                // Get results if hash had to recompile
                if (results.TryGetValue(triggerHash, out bool success))
                {
                    if (!success)
                    {
                        programAsset.PrintErrorsAndWarnings();
                        PrintError("Failed to compile CyanTrigger on objects: ", curTriggers);

                        _programAssets.Remove(programAsset.triggerHash);
                        programAsset.InvalidateData();
                        _programAssets.Add(programAsset.triggerHash, programAsset);
                        
                        foreach (var trigger in curTriggers)
                        {
                            PairTriggerToProgram(trigger, programAsset, false);
                        }
                    
                        continue;
                    }
                    
                    if (programAsset.warningMessages.Length > 0)
                    {
                        programAsset.PrintErrorsAndWarnings();
                        PrintWarning("CyanTriggers compiled with warnings: ", curTriggers);
                    }
                }

                if (triggerHash != programAsset.triggerHash)
                {
                    PrintError($"CyanTrigger hash was not the expected hash after compiling! \"{triggerHash}\" vs \"{programAsset.triggerHash}\" for objects: ", curTriggers);
                    continue;
                }
                
                foreach (var trigger in curTriggers)
                {
                    PairTriggerToProgram(trigger, programAsset);
                }
            }
            
            Profiler.EndSample();
        }

        private static void PairTriggerToProgram(CyanTrigger trigger, CyanTriggerProgramAsset programAsset, bool shouldApply = true)
        {
            try
            {
                var data = trigger.triggerInstance;
                var udon = trigger.triggerInstance.udonBehaviour;

                if (data == null || udon == null || data.triggerDataInstance == null)
                {
                    Debug.LogError($"Could not apply program for CyanTrigger: {VRC.Tools.GetGameObjectPath(trigger.gameObject)}");
                    return;
                }

                bool dirty = false;
                if (udon.programSource != programAsset)
                {
                    udon.programSource = programAsset;
                    dirty = true;
                }

                if (shouldApply)
                {
                    programAsset.ApplyCyanTriggerToUdon(data, udon, ref dirty);
                }
                else
                {
                    // Clear all public variables on the udon behaviour
                    CyanTriggerProgramAsset.ClearPublicUdonVariables(udon, ref dirty);
                }

                if (dirty)
                {
                    // TODO check for prefab applying?
                    // Debug.Log("Setting dirty after pairing Udon/Trigger");
                    EditorUtility.SetDirty(udon);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to apply program for CyanTrigger: {VRC.Tools.GetGameObjectPath(trigger.gameObject)}");
                Debug.LogException(e);
            }
        }

        private static void PrintError(string message, List<CyanTrigger> triggers)
        {
            Debug.LogError(message + ObjectPathsString(triggers));
        }
        
        private static void PrintWarning(string message, List<CyanTrigger> triggers)
        {
            Debug.LogWarning(message + ObjectPathsString(triggers));
        }

        private static string ObjectPathsString(List<CyanTrigger> triggers)
        {
            StringBuilder objectPaths = new StringBuilder();
            if (triggers.Count > 1)
            {
                objectPaths.AppendLine("<View full message to see all objects>");
            }
            
            foreach (var trigger in triggers)
            {
                objectPaths.AppendLine(VRC.Tools.GetGameObjectPath(trigger.gameObject));
            }

            return objectPaths.ToString();
        }

        private static void TryMigrateAllEditableCyanTriggerProgramAssets()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(CyanTriggerEditableProgramAsset)}");
            bool anyMigrated = false;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var cyanTriggerAsset = AssetDatabase.LoadAssetAtPath<CyanTriggerEditableProgramAsset>(path);
                if (cyanTriggerAsset == null)
                {
                    continue;
                }

                anyMigrated |= cyanTriggerAsset.TryVerifyAndMigrate();
            }

            if (anyMigrated)
            {
                AssetDatabase.SaveAssets();
            }
        }

        public static void CompileAllCyanTriggerEditableAssets(bool force = false)
        {
            Profiler.BeginSample("CyanTriggerSerializedProgramManager.CompileAllCyanTriggerEditableAssets");

            string[] guids = AssetDatabase.FindAssets($"t:{nameof(CyanTriggerEditableProgramAsset)}");
            
            // Gather programs to compile.
            List<CyanTriggerProgramAsset> programsToCompile = new List<CyanTriggerProgramAsset>();
            Dictionary<CyanTriggerProgramAsset, string> programsToPath =
                new Dictionary<CyanTriggerProgramAsset, string>();
            
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var cyanTriggerAsset = AssetDatabase.LoadAssetAtPath<CyanTriggerEditableProgramAsset>(path);
                if (cyanTriggerAsset == null)
                {
                    continue;
                }
                
                // Only add program to recompile if we aren't forcing all recompile or if the program's hash changed.
                if (!force && !cyanTriggerAsset.Rehash())
                {
                    continue;
                }

                cyanTriggerAsset.TryVerifyAndMigrate();
                programsToCompile.Add(cyanTriggerAsset);
                programsToPath.Add(cyanTriggerAsset, path);
            }

            if (programsToCompile.Count == 0)
            {
                Profiler.EndSample();
                return;
            }

            try
            {
                AssetDatabase.StartAssetEditing();

                string GetPathForProgram(CyanTriggerProgramAsset program)
                {
                    return programsToPath[program];
                }
                
                var results = CyanTriggerCompiler.BatchCompile(programsToCompile, GetPathForProgram);
                if (results == null)
                {
                    Debug.LogError("[CyanTrigger] Compiling failed due to errors.");
                    return;
                }
                
                foreach (var program in programsToCompile)
                {
                    string path = GetPathForProgram(program);
                    if (results.TryGetValue(path, out var success) && !success)
                    {
                        program.PrintErrorsAndWarnings();
                        Debug.LogError($"Failed to compile program: {path}");
                    }
                    else if (program.warningMessages.Length > 0)
                    {
                        program.PrintErrorsAndWarnings();
                        Debug.LogWarning($"Program compiled with warnings: {path}");
                    }
                }
                
                AssetDatabase.SaveAssets();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            
            Profiler.EndSample();
        }

        public static void CompileCyanTriggerEditableAssetsAndDependencies(CyanTriggerEditableProgramAsset asset)
        {
            CompileCyanTriggerEditableAssetsAndDependencies(new List<CyanTriggerEditableProgramAsset> { asset });
        }
        
        public static void CompileCyanTriggerEditableAssetsAndDependencies(List<CyanTriggerEditableProgramAsset> assets)
        {
            if (assets.Count == 0)
            {
                return;
            }
            
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(CyanTriggerActionGroupDefinitionUdonAsset)}");

            List<CyanTriggerProgramAsset> ctPrograms = new List<CyanTriggerProgramAsset>();
            
            Dictionary<CyanTriggerProgramAsset, string> programsToPath =
                new Dictionary<CyanTriggerProgramAsset, string>();
            
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var groupDefinition = AssetDatabase.LoadAssetAtPath<CyanTriggerActionGroupDefinitionUdonAsset>(path);
                if (groupDefinition == null)
                {
                    continue;
                }

                var udonAsset = groupDefinition.udonProgramAsset;
                if (!(udonAsset is CyanTriggerProgramAsset ctProgram))
                {
                    continue;
                }
                
                // Multiple Custom Actions can reference the same program.
                if (programsToPath.ContainsKey(ctProgram))
                {
                    continue;
                }

                if (ctProgram.GetCyanTriggerData() == null)
                {
                    continue;
                }
                
                if (ctProgram is CyanTriggerEditableProgramAsset editableAsset)
                {
                    editableAsset.TryVerifyAndMigrate();
                }

                ctPrograms.Add(ctProgram);
                programsToPath.Add(ctProgram, path);
            }

            // Check if any program is part of a custom action.
            bool anyCustomActions = false;
            foreach (var asset in assets)
            {
                asset.TryVerifyAndMigrate();
                
                if (programsToPath.ContainsKey(asset))
                {
                    anyCustomActions = true;
                }
                else
                {
                    programsToPath[asset] = AssetDatabase.GetAssetPath(asset);
                }
            }

            List<CyanTriggerProgramAsset> programsToCompile = new List<CyanTriggerProgramAsset>();
            if (anyCustomActions)
            {
                // Create a dependency graph for all programs based on which CustomActions each one uses. 
                Dictionary<CyanTriggerProgramAsset, List<CyanTriggerProgramAsset>> programDependencies =
                    new Dictionary<CyanTriggerProgramAsset, List<CyanTriggerProgramAsset>>();

                foreach (var ctProgram in ctPrograms)
                {
                    foreach (var actionGroups in
                             CyanTriggerUtil.GetCustomActionDependencies(ctProgram.GetCyanTriggerData()))
                    {
                        if (!(actionGroups is CyanTriggerActionGroupDefinitionUdonAsset groupDefinition))
                        {
                            continue;
                        }

                        var udonAsset = groupDefinition.udonProgramAsset;
                        if (!(udonAsset is CyanTriggerProgramAsset ctDepProgram))
                        {
                            continue;
                        }
                        
                        if (!programDependencies.TryGetValue(ctDepProgram, out var depList))
                        {
                            depList = new List<CyanTriggerProgramAsset>();
                            programDependencies.Add(ctDepProgram, depList);
                        }
                        depList.Add(ctProgram);
                    }
                }

                // Go through and find all dependencies of the original program, following the edges
                HashSet<CyanTriggerProgramAsset> dependencies = new HashSet<CyanTriggerProgramAsset>();
                Queue<CyanTriggerProgramAsset> queue = new Queue<CyanTriggerProgramAsset>();

                foreach (var asset in assets)
                {
                    queue.Enqueue(asset);
                    dependencies.Add(asset);
                }

                while (queue.Count > 0)
                {
                    var program = queue.Dequeue();
                    if (programDependencies.TryGetValue(program, out var depProgs))
                    {
                        foreach (var depProg in depProgs)
                        {
                            if (!dependencies.Contains(depProg))
                            {
                                dependencies.Add(depProg);
                                queue.Enqueue(depProg);
                            }
                        }
                    }
                }
            }
            else
            {
                programsToCompile.AddRange(assets);
            }

            try
            {
                AssetDatabase.StartAssetEditing();
                
                string GetPathForProgram(CyanTriggerProgramAsset program)
                {
                    return programsToPath[program];
                }
                
                var results = 
                    CyanTriggerCompiler.BatchCompile(programsToCompile, GetPathForProgram);
                if (results == null)
                {
                    Debug.LogError("[CyanTrigger] Compiling failed due to errors.");
                    return;
                }
                
                foreach (var program in programsToCompile)
                {
                    string path = GetPathForProgram(program);
                    if (results.TryGetValue(path, out var success) && !success)
                    {
                        program.PrintErrorsAndWarnings();
                        Debug.LogError($"Failed to compile program: {path}");
                    }
                    else if (program.warningMessages.Length > 0)
                    {
                        program.PrintErrorsAndWarnings();
                        Debug.LogWarning($"Program compiled with warnings: {program}");
                    }
                }
                
                AssetDatabase.SaveAssets();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

#if CYAN_TRIGGER_DEBUG
        [MenuItem("Window/CyanTrigger/Verify Event Names")]
        // Helper method to ensure that all CustomActions do not introduce non underscored event names. 
        private static void VerifyEventNames()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(CyanTriggerActionGroupDefinitionUdonAsset)}");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var groupDefinition = AssetDatabase.LoadAssetAtPath<CyanTriggerActionGroupDefinitionUdonAsset>(path);
                if (groupDefinition == null)
                {
                    continue;
                }

                var udonAsset = groupDefinition.udonProgramAsset;
                if (!(udonAsset is CyanTriggerProgramAsset ctProgram))
                {
                    continue;
                }

                Dictionary<string, List<int>> improperEventNames = new Dictionary<string, List<int>>();
                for (var index = 0; index < groupDefinition.exposedActions.Length; index++)
                {
                    var actions = groupDefinition.exposedActions[index];
                    if (actions.eventEntry[0] != '_')
                    {
                        if (!improperEventNames.TryGetValue(actions.eventEntry, out var indices))
                        {
                            indices = new List<int>();
                            improperEventNames.Add(actions.eventEntry, indices);
                        }
                        indices.Add(index);
                    }
                }

                if (improperEventNames.Count == 0)
                {
                    continue;
                }
                
                Debug.LogWarning($"{path} - EventNames: {string.Join(", ", improperEventNames.Keys)}", ctProgram);
                
                // Prevent breaking other people's programs
                if (!path.Contains("DefaultCustomActions"))
                {
                    continue;
                }
                
                var ctData = ctProgram.GetCyanTriggerData();
                if (ctData == null || ctData.events == null)
                {
                    continue;
                }
                
                foreach (var ctEvent in ctData.events)
                {
                    string name = ctEvent.name;
                    if (!improperEventNames.TryGetValue(name, out List<int> indices))
                    {
                        continue;
                    }

                    name = $"_{name}";
                    ctEvent.name = name;
                    foreach (var index in indices)
                    {
                        groupDefinition.exposedActions[index].eventEntry = name;
                    }
                }
                
                Debug.Log($"Modifying program: {AssetDatabase.GetAssetPath(ctProgram)}", ctProgram);
                EditorUtility.SetDirty(ctProgram);
                EditorUtility.SetDirty(groupDefinition);
            }
            
            AssetDatabase.SaveAssets();
        }
#endif
    }
}

