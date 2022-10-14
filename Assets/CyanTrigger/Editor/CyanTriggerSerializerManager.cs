using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using VRC.Udon;
using Object = UnityEngine.Object;

namespace Cyan.CT.Editor
{
    [InitializeOnLoad]
    public class CyanTriggerSerializerManager : UnityEditor.AssetModificationProcessor
    {
        private static readonly List<PrefabStage> OpenedPrefabStages = new List<PrefabStage>();
        private static bool _enteredEditMode;
        
        static CyanTriggerSerializerManager()
        {
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorSceneManager.sceneOpened += SceneOpened;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            
            PrefabStage.prefabSaving += PrefabStageOnPrefabSaving;
            PrefabStage.prefabStageOpened += PrefabStageOnPrefabStageOpened;
            PrefabStage.prefabStageClosing += PrefabStageOnPrefabStageClosing;
            
            // TODO handle when assemblies reload and OpenedPrefabStages is lost
        }
        
        private static string[] OnWillSaveAssets(string[] paths)
        {
            // Check settings to verify saving should trigger recompile scene triggers.
            if (!CyanTriggerSettings.Instance.compileSceneTriggersOnSave)
            {
                return paths;
            }
            
            Profiler.BeginSample("CyanTrigger.OnWillSaveAssets");
            bool isSavingScene = false;
        
            // TODO check prefab saving?
            foreach (string path in paths)
            {
                if (Path.GetExtension(path).Equals(".unity"))
                {
                    isSavingScene = true;
                    break;
                }
            }
        
            if (isSavingScene)
            {
                RecompileAllTriggers(false, true);
            }
            
            Profiler.EndSample();
        
            return paths;
        }

        private static void SceneOpened(Scene scene, OpenSceneMode mode)
        {
            RecompileAllTriggers(true, true);
        }
        
        private static void PrefabStageOnPrefabSaving(GameObject obj)
        {
            VerifyPrefabScene();

            // TODO this is not enough to cover all possible prefab situations.
            // Prefab variants and nested prefabs may not get proper compilation. This needs another method for that.
            //CyanTriggerSerializedProgramManager.Instance.ApplyTriggerPrograms(triggers);
        }

        private static void PrefabStageOnPrefabStageOpened(PrefabStage prefabStage)
        {
            OpenedPrefabStages.Add(prefabStage);

            VerifyPrefabScene();
        }
        
        private static void PrefabStageOnPrefabStageClosing(PrefabStage prefabStage)
        {
            OpenedPrefabStages.Remove(prefabStage);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Exiting edit mode to ensure that everything is compiled before play.
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                // Check settings to verify switching to playmode should trigger recompile scene triggers.
                if (CyanTriggerSettings.Instance.compileSceneTriggersOnPlay)
                {
                    RecompileAllTriggers(true, true);
                }
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                _enteredEditMode = true;
            }
        }

        private static void OnHierarchyChanged()
        {
            if (IsPlaying())
            {
                return;
            }
            
            // Prevent verification right after exiting playmode.
            if (_enteredEditMode)
            {
                _enteredEditMode = false;
                return;
            }
            
            Profiler.BeginSample("CyanTrigger.OnHierarchyChanged");
            
            if (OpenedPrefabStages.Count > 0)
            {
                VerifyPrefabScene();
            }
            else
            {
                VerifyScene(false, false);
            }
            
            Profiler.EndSample();
        }

        public static bool RecompileAllTriggers(bool force, bool debugBuild)
        {
            if (IsPlaying())
            {
                return true;
            }

            bool ret = true;
            Profiler.BeginSample("CyanTrigger.RecompileAllTriggers");

            try
            {
                AssetDatabase.StartAssetEditing();

                CyanTriggerCompiler.DebugCompile = debugBuild;
                VerifyScene(true, force);
                CyanTriggerCompiler.DebugCompile = false;
            }
            catch (Exception e)
            {
                ret = false;
                Debug.LogError("Error while compiling all triggers");
                Debug.LogException(e);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            
            Profiler.EndSample();

            return ret;
        }

        private static void VerifyScene(bool recompile, bool force)
        {
            if (IsPlaying())
            {
                return;
            }

            // Remove CyanTriggerResources as the are currently not needed. 
            List<CyanTriggerResources> resources = GetAllOfTypeFromAllScenes<CyanTriggerResources>();
            foreach (var resource in resources)
            {
                if (resource != null && resource.gameObject != null)
                {
                    Object.DestroyImmediate(resource.gameObject);
                }
            }
            
            List<CyanTrigger> triggers = GetAllOfTypeFromAllScenes<CyanTrigger>();
            List<CyanTriggerAsset> triggerAssets = GetAllOfTypeFromAllScenes<CyanTriggerAsset>();
            List<UdonBehaviour> udonBehaviours = GetAllOfTypeFromAllScenes<UdonBehaviour>();

            VerifyItems(triggers, triggerAssets, udonBehaviours, recompile, force, false);
        }

        private static void VerifyPrefabScene()
        {
            if (IsPlaying())
            {
                return;
            }

            List<CyanTrigger> triggers = GetAllOfTypeFromPrefabScenes<CyanTrigger>();
            List<CyanTriggerAsset> triggerAssets = GetAllOfTypeFromPrefabScenes<CyanTriggerAsset>();
            List<UdonBehaviour> udonBehaviours = GetAllOfTypeFromPrefabScenes<UdonBehaviour>();

            VerifyItems(triggers, triggerAssets, udonBehaviours, false, false, true);
        }

        private static void VerifyItems(
            List<CyanTrigger> triggers,
            List<CyanTriggerAsset> triggerAssets,
            List<UdonBehaviour> udonBehaviours,
            bool recompile,
            bool force,
            bool isPrefab)
        {
            VerifySceneTriggers(triggers, recompile);
            VerifyCyanTriggerAssets(triggerAssets, recompile);
            
            // Should always be last
            VerifySceneUdon(udonBehaviours);
            
            if (!isPrefab && recompile)
            {
                CyanTriggerSerializedProgramManager.Instance.ApplyTriggerPrograms(triggers, force);
            }
        }

        private static void VerifySceneUdon(List<UdonBehaviour> udonBehaviours)
        {
            if (IsPlaying())
            {
                return;
            }

            foreach (var udon in udonBehaviours)
            {
                if (!(udon.programSource is CyanTriggerProgramAsset) 
                    || udon.programSource is CyanTriggerEditableProgramAsset)
                {
                    continue;
                }

                CyanTrigger trigger = udon.GetComponent<CyanTrigger>();
                if (trigger == null || trigger.triggerInstance.udonBehaviour != udon)
                {
                    //Debug.Log("Setting object dirty after deleting udon/trigger: {VRC.Tools.GetGameObjectPath(obj)}");
                    Undo.DestroyObjectImmediate(udon);
                }
            }
        }

        private static void VerifySceneTriggers(List<CyanTrigger> triggers, bool fullVerification)
        {
            if (IsPlaying())
            {
                return;
            }

            Profiler.BeginSample("CyanTrigger.VerifySceneTriggers");

            Object[] recordObjs = new Object[triggers.Count];
            for (int index = 0; index < recordObjs.Length; ++index)
            {
                recordObjs[index] = triggers[index];
            }
            Undo.RecordObjects(recordObjs, Undo.GetCurrentGroupName());
            
            foreach (var trigger in triggers)
            {
                VerifyTrigger(trigger, fullVerification, true);
            }
            
            Profiler.EndSample();
        }

        public static bool VerifyTrigger(CyanTrigger trigger, bool fullVerification, bool batch)
        {
            trigger.Verify();
            
            if (trigger.triggerInstance == null || trigger.triggerInstance.triggerDataInstance == null)
            {
                Debug.LogError($"Trigger data is null!: {VRC.Tools.GetGameObjectPath(trigger.gameObject)}");
                return false;
            }

            Profiler.BeginSample("CyanTrigger.VerifyTrigger");

            bool changes = false;
            // In batch verify, it is assumed the caller will group all undo.RecordObject calls
            if (!batch)
            {
                Undo.RecordObject(trigger, Undo.GetCurrentGroupName());
            }

            if ((trigger.hideFlags & HideFlags.DontSaveInBuild) == 0)
            {
                trigger.hideFlags |= HideFlags.DontSaveInBuild;
            }

            CyanTriggerSerializableInstance triggerInstance = trigger.triggerInstance;
            // Linked Udon is not on the same component
            if (triggerInstance.udonBehaviour != null && trigger.gameObject != triggerInstance.udonBehaviour.gameObject)
            {
                changes = true;
                triggerInstance.udonBehaviour = null;
#if CYAN_TRIGGER_DEBUG
                // Debug.Log($"Setting trigger dirty with wrong udon: {VRC.Tools.GetGameObjectPath(trigger.gameObject)}");
                Debug.LogWarning($"Trigger has UdonBehaviour on different object: {VRC.Tools.GetGameObjectPath(trigger.gameObject)}");
#endif
            }
            
            // Try getting UdonBehaviour if one already exists
            if (triggerInstance.udonBehaviour == null)
            {
#if CYAN_TRIGGER_DEBUG
                Debug.LogWarning($"Trigger missing UdonBehaviour: {VRC.Tools.GetGameObjectPath(trigger.gameObject)}");
#endif
                UdonBehaviour GetUdonFromTrigger(bool logWarning)
                {
                    // find anything that had proper name
                    UdonBehaviour[] udonBehaviours = trigger.GetComponents<UdonBehaviour>();
                    UdonBehaviour potentialUdon = null;
                    UdonBehaviour nullBehaviour = null;
                    bool warnedDuplicate = false;
                    foreach (var udonBehaviour in udonBehaviours)
                    {
                        AbstractUdonProgramSource abstractProgram = udonBehaviour.programSource;
                        if (abstractProgram is CyanTriggerProgramAsset 
                            && !(abstractProgram is CyanTriggerEditableProgramAsset)
                            && abstractProgram.name.StartsWith(CyanTriggerSerializedProgramManager.SerializedUdonAssetNamePrefix))
                        {
                            if (potentialUdon == null)
                            {
                                potentialUdon = udonBehaviour;
                            }
                            else if (logWarning && !warnedDuplicate)
                            {
#if CYAN_TRIGGER_DEBUG
                                Debug.LogWarning($"Multiple UdonBehaviours with CyanTrigger programs. {VRC.Tools.GetGameObjectPath(trigger.gameObject)}");
#endif
                                warnedDuplicate = true;
                            }
                        }

                        if (abstractProgram == null)
                        {
                            nullBehaviour = udonBehaviour;
                        }
                    }

                    if (potentialUdon == null && nullBehaviour != null)
                    {
                        potentialUdon = nullBehaviour;
                    }

                    return potentialUdon;
                }

                UdonBehaviour behaviour = GetUdonFromTrigger(true);
                if (behaviour != null)
                {
                    changes = true;
                    triggerInstance.udonBehaviour = behaviour;
                    
                    // Debug.Log($"Setting trigger dirty with new udon: {VRC.Tools.GetGameObjectPath(trigger.gameObject)}");
                }
                else
                {
                    bool prefabReverted = false;
                    
                    // If the trigger is part of a prefab, crawl the prefab hierarchy until finding the one that has the
                    // udon behaviour removed and try to revert the change.
                    if (PrefabUtility.IsPartOfPrefabInstance(trigger))
                    {
                        GameObject triggerObj = trigger.gameObject;
                        GameObject cur = triggerObj;
                        while (cur != null)
                        {
                            GameObject prefab = PrefabUtility.GetNearestPrefabInstanceRoot(cur);
                            if (prefab == null)
                            {
                                break;
                            }
                            var removed = PrefabUtility.GetRemovedComponents(prefab);
                            foreach (var item in removed)
                            {
                                if (item.containingInstanceGameObject == triggerObj 
                                    && item.assetComponent is UdonBehaviour udon 
                                    && udon.programSource is CyanTriggerProgramAsset)
                                {
                                    item.Revert();
                                    prefabReverted = true;
                                    break;
                                }
                            }

                            Transform parent = prefab.transform.parent;
                            cur = parent ? parent.gameObject : null;
                        }
                    }

                    if (!prefabReverted)
                    {
                        changes = true;
                        trigger.DelayedReset();
                    }
                    else
                    {
                        // Try to get udon component again
                        triggerInstance.udonBehaviour = GetUdonFromTrigger(false);
                        changes |= triggerInstance.udonBehaviour != null;
                    }
                }
            }
            
            Debug.Assert(triggerInstance.udonBehaviour != null, 
                $"CyanTrigger UdonBehaviour is still null! {VRC.Tools.GetGameObjectPath(trigger.gameObject)}");

            int prevVersion = triggerInstance.triggerDataInstance?.version ?? 0;
            if (CyanTriggerVersionMigrator.MigrateTrigger(triggerInstance.triggerDataInstance))
            {
                changes = true;
#if CYAN_TRIGGER_DEBUG
                int newVersion = triggerInstance.triggerDataInstance.version;
                string path = VRC.Tools.GetGameObjectPath(trigger.gameObject);
                Debug.Log($"Migrated object from version {prevVersion} to version {newVersion}, {path}");
#endif
                // Clear public variable symbol table
                UdonBehaviour udon = trigger.triggerInstance.udonBehaviour;
                if (udon != null)
                {
                    var publicVariables = udon.publicVariables;
                    if (publicVariables != null)
                    {
                        foreach (var symbol in new List<string>(publicVariables.VariableSymbols))
                        {
                            publicVariables.RemoveVariable(symbol);
                        }
                    }
                }
            }

            // TODO figure out other things to verify here
            if (fullVerification)
            {
                // Note that setting dirty is already handled in the Undo.RecordObject
                changes |= CyanTriggerUtil.ValidateTriggerData(trigger.triggerInstance?.triggerDataInstance);
            }
            
            if (changes && PrefabUtility.IsPartOfPrefabInstance(trigger))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(trigger);
            }
            
            // 
            if (triggerInstance.udonBehaviour == null)
            {
                Profiler.EndSample();
                return changes;
            }
            
            if (IsPlaying() || !trigger.gameObject.scene.IsValid())
            {
                Profiler.EndSample();
                return changes;
            }

            {
                UdonBehaviour udonBehaviour = triggerInstance.udonBehaviour;
                if (udonBehaviour.programSource == null
                    || !(udonBehaviour.programSource is CyanTriggerProgramAsset)
                    || udonBehaviour.programSource is CyanTriggerEditableProgramAsset)
                {
                    changes = true;
                    Undo.RecordObject(udonBehaviour, Undo.GetCurrentGroupName());
                    udonBehaviour.programSource = CyanTriggerSerializedProgramManager.Instance.DefaultProgramAsset;
                    //Debug.Log($"Setting udon dirty after setting default program: {VRC.Tools.GetGameObjectPath(udonBehaviour.gameObject)}");
                    
                    if (PrefabUtility.IsPartOfPrefabInstance(udonBehaviour))
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(udonBehaviour);
                    }
                }
            }

            Profiler.EndSample();
            return changes;
        }

        private static void VerifyCyanTriggerAssets(List<CyanTriggerAsset> triggerAssets, bool fullVerification)
        {
            if (IsPlaying())
            {
                return;
            }

            Object[] recordObjs = new Object[triggerAssets.Count];
            for (int index = 0; index < recordObjs.Length; ++index)
            {
                recordObjs[index] = triggerAssets[index];
            }
            Undo.RecordObjects(recordObjs, Undo.GetCurrentGroupName());

            
            // Collect all Programs to CyanTriggerAssets to batch verify variables.
            Dictionary<CyanTriggerEditableProgramAsset, List<CyanTriggerAsset>> programs =
                new Dictionary<CyanTriggerEditableProgramAsset, List<CyanTriggerAsset>>();
            foreach (var triggerAsset in triggerAssets)
            {
                var program = triggerAsset.assetInstance?.cyanTriggerProgram;
                if (program == null || !(program is CyanTriggerEditableProgramAsset ctProgram))
                {
                    continue;
                }
                
                if (!programs.TryGetValue(ctProgram, out List<CyanTriggerAsset> ctAssets))
                {
                    ctAssets = new List<CyanTriggerAsset>();
                    programs.Add(ctProgram, ctAssets);
                }
                ctAssets.Add(triggerAsset);
            }
            
            // Verify CyanTriggerAssets. This mainly handles missing Udon checks.
            foreach (var triggerAsset in triggerAssets)
            {
                VerifyCyanTriggerAsset(triggerAsset, fullVerification, true);
            }

            List<CyanTriggerEditableProgramAsset> programsToRecompile = new List<CyanTriggerEditableProgramAsset>();
            bool anyChanges = false;
            foreach (var programPair in programs)
            {
                var program = programPair.Key;
                if (fullVerification)
                {
                    anyChanges |= program.TryVerifyAndMigrate();
                }

                if (!program.SerializedProgramHashMatchesExpectedHash())
                {
#if CYAN_TRIGGER_DEBUG
                    Debug.LogWarning($"Program {program.name} has invalid hash! Will be recompiled.");
#endif
                    programsToRecompile.Add(program);
                }
            }

            if (programsToRecompile.Count > 0)
            {
                CyanTriggerSerializedProgramManager.CompileCyanTriggerEditableAssetsAndDependencies(programsToRecompile);
            }
            
            foreach (var programPair in programs)
            {
                programPair.Key.VerifyAndApply(programPair.Value);
            }

            if (fullVerification && anyChanges)
            {
                AssetDatabase.SaveAssets();
            }
        }
        
        private static bool VerifyCyanTriggerAsset(CyanTriggerAsset triggerAsset, bool fullVerification, bool batch) 
        {
            var assetInstance = triggerAsset.assetInstance;
            if (assetInstance == null)
            {
                Debug.LogError($"CyanTriggerAsset's assetInstance is null: {VRC.Tools.GetGameObjectPath(triggerAsset.gameObject)}");
                return false;
            }

            bool changes = false;
            if (!batch)
            {
                Undo.RecordObject(triggerAsset, Undo.GetCurrentGroupName());
            }
            
            if ((triggerAsset.hideFlags & HideFlags.DontSaveInBuild) == 0)
            {
                triggerAsset.hideFlags |= HideFlags.DontSaveInBuild;
            }

            var udonBehaviour = assetInstance.udonBehaviour;

            // If the UdonBehaviour is on a different object, set udon reference to null.
            if (udonBehaviour != null && udonBehaviour.gameObject != triggerAsset.gameObject)
            {
                changes = true;
                udonBehaviour = assetInstance.udonBehaviour = null;
            }
            
            if (changes && PrefabUtility.IsPartOfPrefabInstance(triggerAsset))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(triggerAsset);
            }
            
            if (udonBehaviour == null)
            {
                // TODO Try to match with existing udon on the object?
                // Get all CTAssets and all UdonBehaviours
                // Try to pair them and see if remaining ones match with this.
                // If not, check for prefab changes?
                if (fullVerification)
                {
                    Debug.LogWarning($"CyanTriggerAsset without an UdonBehaviour: {VRC.Tools.GetGameObjectPath(triggerAsset.gameObject)}");
                }
                return changes;
            }
            
            // When udon program is not the expected CyanTriggerAsset program, set it back. 
            if (udonBehaviour.programSource != assetInstance.cyanTriggerProgram)
            {
                changes = true;
                Undo.RecordObject(udonBehaviour, Undo.GetCurrentGroupName());
                udonBehaviour.programSource = assetInstance.cyanTriggerProgram;
                
                if (PrefabUtility.IsPartOfPrefabInstance(udonBehaviour))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(udonBehaviour);
                }
            }

            return changes;
        }
        
        private static bool IsPlaying()
        {
            return EditorApplication.isPlaying;
        }


        private static List<T> GetAllOfTypeFromAllScenes<T>()
        {
            Profiler.BeginSample("CyanTrigger.GetAllOfTypeFromAllScenes");

            List<Scene> scenes = new List<Scene>();
            
            int countLoaded = SceneManager.sceneCount;
            for (int i = 0; i < countLoaded; ++i)
            {
                // TODO Verify scene is not prefab scene? 
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid())
                {
                    continue;
                }

                scenes.Add(scene);
            }

            var components = GetAllOfTypeFromScenes<T>(scenes);
            
            Profiler.EndSample();
            
            return components;
        }
        
        private static List<T> GetAllOfTypeFromPrefabScenes<T>()
        {
            Profiler.BeginSample("CyanTrigger.GetAllOfTypeFromPrefabScenes");

            List<Scene> scenes = new List<Scene>();
            
            foreach (var prefabStage in OpenedPrefabStages)
            {
                Scene scene = prefabStage.scene;
                if (!scene.IsValid())
                {
                    continue;
                }

                scenes.Add(scene);
            }

            var components = GetAllOfTypeFromScenes<T>(scenes);
            
            Profiler.EndSample();
            
            return components;
        }

        private static List<T> GetAllOfTypeFromScenes<T>(IEnumerable<Scene> scenes)
        {
            List<T> components = new List<T>();

            foreach (var scene in scenes)
            {
                if (!scene.IsValid())
                {
                    continue;
                }

                GetAllOfTypeFromScene(scene, ref components);
            }
            
            return components;
        }

        private static void GetAllOfTypeFromScene<T>(Scene scene, ref List<T> components)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }
            
            List<GameObject> sceneObjects = new List<GameObject>();
            scene.GetRootGameObjects(sceneObjects);
            
            foreach (var obj in sceneObjects)
            {
                components.AddRange(obj.GetComponentsInChildren<T>(true));
            }
        }
    }
}

