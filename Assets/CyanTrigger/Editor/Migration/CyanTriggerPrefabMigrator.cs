using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cyan.CT.Editor
{
    public class CyanTriggerPrefabMigrator : AssetPostprocessor
    {
        private static bool delayedMigrationStarted = false;
        private static bool migrationInProgress = false;

        private static List<string> _skippedImportedAssets = null;
        
        public static void MigrateAllPrefabs()
        {
            if (delayedMigrationStarted)
            {
                return;
            }
            
            EditorApplication.update += DelayedMigrateAllPrefabs;
        }

        private static void DelayedMigrateAllPrefabs()
        {
            if (migrationInProgress)
            {
                return;
            }
            
            EditorApplication.update -= DelayedMigrateAllPrefabs;
            
            if (delayedMigrationStarted)
            {
                return;
            }

            var settings = CyanTriggerSettings.Instance;
            
            // If settings assumes current project migration version is equal to the data version,
            // then no need to check all prefabs for migration. 
            // This doesn't catch all cases, but will at least get the majority of the prefabs in a project. 
            // Migration will still happen on importing a prefab or opening a scene or prefab manually.
            if (settings.lastMigratedDataVersion >= CyanTriggerDataInstance.DataVersion)
            {
                delayedMigrationStarted = true;
                // Check for any assets not processed on original import
                if (_skippedImportedAssets != null)
                {
                    string[] importedAssets = _skippedImportedAssets.ToArray();
                    _skippedImportedAssets = null;
                    OnPostprocessAllAssets(importedAssets, null, null, null);
                }
                return;
            }

            Debug.Log($"[CyanTrigger] Migrating project Prefabs and Programs to current data version: {CyanTriggerDataInstance.DataVersion}");
            CyanTriggerSerializedProgramManager.CompileAllCyanTriggerEditableAssets(true);
            MigratePrefabs(CyanTriggerPrefabDependency.GetValidPrefabPaths());

            settings.lastMigratedDataVersion = CyanTriggerDataInstance.DataVersion;
            EditorUtility.SetDirty(settings);
            
            delayedMigrationStarted = true;
            _skippedImportedAssets = null;
        }
        
        // Verify prefabs and Programs on import.
        private static void OnPostprocessAllAssets(
            string[] importedAssets, 
            string[] deletedAssets, 
            string[] movedAssets, 
            string[] movedFromAssetPaths)
        {
            if (migrationInProgress)
            {
                return;
            }

            if (!delayedMigrationStarted)
            {
                // Skip this import check but save in the case where project does not need full migration.
                if (_skippedImportedAssets == null)
                {
                    _skippedImportedAssets = new List<string>();
                }
                _skippedImportedAssets.AddRange(importedAssets);
                return;
            }
            
            VerifyImportedProgramAssets(importedAssets);
            MigratePrefabs(importedAssets);
        }
        
        // TODO make coroutine to prevent locking the main thread. 
        private static void MigratePrefabs(IList<string> paths)
        {
            migrationInProgress = true;

            CyanTriggerPrefabDependency dependencies = new CyanTriggerPrefabDependency();
            foreach (var path in paths)
            {
                dependencies.AddAsset(path, true);
            }

            var sortedOrder = dependencies.GetOrder();

            if (sortedOrder.Count == 0)
            {
                migrationInProgress = false;
                return;
            }
            
            try
            {
                AssetDatabase.StartAssetEditing();
                
                foreach (var prefabData in sortedOrder)
                {
                    string path = prefabData.Path;

                    // This is really hacky, but speedups processing considerably :upsidedown:
                    // Basically checks if the file has a CyanTrigger component in it and ignores files that do not.
                    if (!File.ReadAllText(Path.GetFullPath(path))
                            .Contains("m_Script: {fileID: 11500000, guid: 3dd4a7956009f7d429a09b8371329c82, type: 3}"))
                    {
                        continue;
                    }
                    
                    GameObject prefab = PrefabUtility.LoadPrefabContents(path);
                    if (PrefabUtility.IsPartOfImmutablePrefab(prefab))
                    {
                        PrefabUtility.UnloadPrefabContents(prefab);
                        continue;
                    }
                    
                    var triggers = prefab.GetComponentsInChildren<CyanTrigger>();
                    if (triggers.Length > 0)
                    {
#if CYAN_TRIGGER_DEBUG
                        //Debug.Log($"Version: {triggers[0].triggerInstance.triggerDataInstance.version}, Path: {path}");
#endif
                        
                        Object[] recordObjs = new Object[triggers.Length];
                        for (int index = 0; index < recordObjs.Length; ++index)
                        {
                            recordObjs[index] = triggers[index];
                        }
                        Undo.RecordObjects(recordObjs, Undo.GetCurrentGroupName());

                        bool changes = false;
                        foreach (var trigger in triggers)
                        {
                            changes |= CyanTriggerSerializerManager.VerifyTrigger(trigger, true, true);
                        }

                        if (changes)
                        {
                            PrefabUtility.SaveAsPrefabAsset(prefab, path);
                        }
                    }

                    PrefabUtility.UnloadPrefabContents(prefab);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            
            migrationInProgress = false;
        }

        // When importing CyanTrigger Programs, the compiled program is not imported and will contain old data
        // This check goes through all new imported programs to verify if the compiled version matches or if it needs to be recompiled. 
        private static void VerifyImportedProgramAssets(string[] importedAssets)
        {
            migrationInProgress = true;
            
            List<CyanTriggerEditableProgramAsset> programToCompile = new List<CyanTriggerEditableProgramAsset>();
            foreach (string path in importedAssets)
            {
                CyanTriggerEditableProgramAsset program =
                    AssetDatabase.LoadAssetAtPath<CyanTriggerEditableProgramAsset>(path);
                if (program != null 
                    && !program.HasErrors() 
                    && (program.TryVerifyAndMigrate() || !program.SerializedProgramHashMatchesExpectedHash()))
                {
                    programToCompile.Add(program);
                }
            }

            if (programToCompile.Count > 0)
            {
                CyanTriggerSerializedProgramManager.CompileCyanTriggerEditableAssetsAndDependencies(programToCompile);
            }
            
            migrationInProgress = false;
        }
    }
}