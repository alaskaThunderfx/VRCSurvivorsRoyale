using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Editor.ProgramSources;
using Object = UnityEngine.Object;

namespace Cyan.CT.Editor
{
    public static class CyanTriggerProgramAssetExporter
    {
        public enum ConvertOptions
        {
            Unknown,
            Direct,
            TryMerge,
        }

        [MenuItem("CONTEXT/CyanTriggerEditableProgramAsset/Export to Assembly Asset")]
        private static void ExportToAssemblyAsset(MenuCommand command)
        {
            ExportToAssemblyAsset((CyanTriggerEditableProgramAsset)command.context);
        }
        
        public static UdonAssemblyProgramAsset ExportToAssemblyAsset(CyanTriggerEditableProgramAsset programAsset)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit playmode before exporting assembly assets.");
                return null;
            }

            if (programAsset == null)
            {
                return null;
            }

            return ExportAssemblyProgram(programAsset, Path.GetDirectoryName(AssetDatabase.GetAssetPath(programAsset)));
        }
        
        public static CyanTriggerEditableProgramAsset ExportToCyanTriggerEditableProgramAsset(
            CyanTrigger cyanTrigger,
            out Dictionary<string, Object> updatedVariableReferences,
            ConvertOptions convertOptions = ConvertOptions.Unknown)
        {
            updatedVariableReferences = null;
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit playmode before exporting CyanTrigger assets.");
                return null;
            }

            if (cyanTrigger == null)
            {
                return null;
            }
            
            CyanTriggerDataInstance dataInstance = cyanTrigger.triggerInstance?.triggerDataInstance;
            if (dataInstance == null)
            {
                return null;
            }

            if (convertOptions == ConvertOptions.Unknown)
            {
                // Give dialog relating to handling of UnityObject consts
                int option = EditorUtility.DisplayDialogComplex("Export CyanTrigger",
                    "Exported CyanTrigger programs cannot have constant references to Unity Objects. Exporting provides two options: 1) All references that use the same object will use the same variable. 2) Every reference will get its own variable.",
                    "Match References",
                    "Cancel",
                    "Variable Per Reference");

                switch (option)
                {
                    case 0: // Match Reference
                        convertOptions = ConvertOptions.TryMerge;
                        break;
                    case 2: // Variable per object
                        convertOptions = ConvertOptions.Direct;
                        break;
                    default:
                        return null;
                }
            }
            
            string saveLocation = GetSavePath(cyanTrigger.gameObject, cyanTrigger.name, true);
            return CreateProgramAssetFromTriggerInstance(dataInstance, saveLocation, convertOptions, out updatedVariableReferences);
        }
        
        private static UdonAssemblyProgramAsset ExportAssemblyProgram(CyanTriggerProgramAsset programAsset, string directory)
        {
            string folderName = $"{programAsset.name}_Assembly";
            string folderPath = Path.Combine(directory, folderName);
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder(directory, folderName);
            }

            string path = Path.Combine(folderPath, $"{programAsset.name}.asset");
            
            UdonAssemblyProgramAsset assemblyProgramAsset = ScriptableObject.CreateInstance<UdonAssemblyProgramAsset>();
            
            // Create asset early to ensure it has GUID for serialized program
            AssetDatabase.CreateAsset(assemblyProgramAsset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            
            assemblyProgramAsset = AssetDatabase.LoadAssetAtPath<UdonAssemblyProgramAsset>(path);
            // Get serialized program early to ensure it has been created and tried to assemble an empty program.
            var serializedProgram = assemblyProgramAsset.SerializedProgramAsset;
            
            FieldInfo assemblyField = typeof(UdonAssemblyProgramAsset).GetField("udonAssembly", BindingFlags.NonPublic | BindingFlags.Instance);
            assemblyField.SetValue(assemblyProgramAsset, programAsset.GetUdonAssembly());

            bool failedToAssemble = false;
            void OnAssemble(bool success, string udonAssembly)
            {
                failedToAssemble = !success;
            }
            
            assemblyProgramAsset.OnAssemble += OnAssemble;
            // Force assembling of the program.
            assemblyProgramAsset.RefreshProgram();
            assemblyProgramAsset.OnAssemble -= OnAssemble;

            if (failedToAssemble)
            {
                Debug.LogError("Failed to assembly program.");
                AssetDatabase.DeleteAsset(folderPath);
                return null;
            }
            
            FieldInfo assemblyProgramField = typeof(UdonProgramAsset).GetField("program", BindingFlags.NonPublic | BindingFlags.Instance);
            IUdonProgram assemblyProgram = (IUdonProgram)assemblyProgramField.GetValue(assemblyProgramAsset);
            IUdonProgram ctProgram = programAsset.GetUdonProgram();

            if (ctProgram == null || assemblyProgram == null)
            {
                // TODO log?
                AssetDatabase.DeleteAsset(folderPath);
                return null;
            }

            // Copy heap values from one to the other.
            var ctSymbolTable = ctProgram.SymbolTable;
            IUdonHeap ctHeap = ctProgram.Heap;
            var assemblySymbolTable = assemblyProgram.SymbolTable;
            IUdonHeap assemblyHeap = assemblyProgram.Heap;
            
            foreach (string symbol in ctSymbolTable.GetSymbols())
            {
                uint symbolAddress = ctSymbolTable.GetAddressFromSymbol(symbol);
                Type symbolType = ctHeap.GetHeapVariableType(symbolAddress);
                object symbolValue = ctHeap.GetHeapVariable(symbolAddress);
                assemblyHeap.SetHeapVariable(assemblySymbolTable.GetAddressFromSymbol(symbol), symbolValue, symbolType);
            }

            EditorUtility.SetDirty(assemblyProgramAsset);

            serializedProgram.StoreProgram(assemblyProgram);
            EditorUtility.SetDirty(serializedProgram);

            AssetDatabase.SaveAssets();

            // Move the serialized asset to the same folder since this is what stores the heap values.
            string serializedProgramPath = AssetDatabase.GetAssetPath(serializedProgram);
            string newSerializedProgramPath = Path.Combine(folderPath, Path.GetFileName(serializedProgramPath));
            AssetDatabase.MoveAsset(serializedProgramPath, newSerializedProgramPath);

            Selection.SetActiveObjectWithContext(assemblyProgramAsset, null);
            
            // TODO notify user that both items are needed when sharing
            return assemblyProgramAsset;
        }

        public static CyanTriggerEditableProgramAsset CreateProgramAssetFromTriggerInstance(
            CyanTriggerDataInstance dataInstance,
            string assetPath,
            ConvertOptions convertOptions,
            out Dictionary<string, Object> updatedVariableReferences)
        {
            CyanTriggerEditableProgramAsset ctProgram = CreateUdonProgramSourceAsset(assetPath);
            
            CyanTriggerDataInstance processedData = ProcessAndRemoveUnityObjects(
                dataInstance, 
                convertOptions, 
                out updatedVariableReferences);
            
            ctProgram.SetCyanTriggerData(processedData, null);
            ctProgram.RehashAndCompile();
            
            EditorUtility.SetDirty(ctProgram);
            AssetDatabase.SaveAssets();
            
            return ctProgram;
        }

        private static CyanTriggerDataInstance ProcessAndRemoveUnityObjects(
            CyanTriggerDataInstance dataInstance,
            ConvertOptions convertOptions,
            out Dictionary<string, Object> updatedVariableReferences)
        {
            if (convertOptions == ConvertOptions.Unknown)
            {
                throw new NotSupportedException("Cannot convert CyanTrigger data with unknown convert option");
            }
            
            CyanTriggerDataInstance processedData = CyanTriggerCopyUtil.CopyCyanTriggerDataInstance(dataInstance, true);
            
            Dictionary<string, Object> localUpdatedVariableReferences = new Dictionary<string, Object>();
            updatedVariableReferences = localUpdatedVariableReferences;

            List<CyanTriggerVariable> variables = new List<CyanTriggerVariable>(processedData.variables);

            Dictionary<Object, CyanTriggerVariable> objectsToVariable = new Dictionary<Object, CyanTriggerVariable>();
            Dictionary<Type, int> referenceCount = new Dictionary<Type, int>();
            
            var nullVar = CyanTriggerAssemblyDataConsts.NullObject;
            
            void ProcessInput(CyanTriggerActionVariableInstance input)
            {
                if (input == null)
                {
                    return;
                }

                if (input.isVariable)
                {
                    return;
                }

                object data = input.data.Obj;
                // If data is null, set it to the null variable directly. 
                if (data == null)
                {
                    input.isVariable = true;
                    input.name = nullVar.Name;
                    input.variableID = nullVar.ID;
                    return;
                }

                // If object type isn't a unity object, nothing to handle. Skip it. 
                if (!(data is Object unityObj))
                {
                    return;
                }

                CyanTriggerVariable var;
                if (convertOptions == ConvertOptions.Direct)
                {
                    Type type = unityObj.GetType();
                    if (!referenceCount.TryGetValue(type, out int value))
                    {
                        value = 0;
                    }
                    referenceCount[type] = value + 1;

                    var = new CyanTriggerVariable
                    {
                        name = CyanTriggerNameHelpers.SanitizeName($"_{type.Name}_{value}"),
                        type = new CyanTriggerSerializableType(type),
                        variableID = Guid.NewGuid().ToString(),
                    };
                    variables.Add(var);
                    
                    localUpdatedVariableReferences.Add(var.variableID, unityObj);
                }
                else if (convertOptions == ConvertOptions.TryMerge)
                {
                    if (!objectsToVariable.TryGetValue(unityObj, out var))
                    {
                        Type type = unityObj.GetType();
                        var = new CyanTriggerVariable
                        {
                            name = CyanTriggerNameHelpers.SanitizeName($"_{unityObj.name}_{type.Name}"),
                            type = new CyanTriggerSerializableType(type),
                            variableID = Guid.NewGuid().ToString(),
                        };
                        variables.Add(var);

                        objectsToVariable[unityObj] = var;
                        localUpdatedVariableReferences.Add(var.variableID, unityObj);
                    }
                }
                else
                {
                    var = null;
                }
                
                input.data.Obj = null;
                input.isVariable = true;
                input.name = var.name;
                input.variableID = var.variableID;
            }
            
            void ProcessActionInstance(CyanTriggerActionInstance action)
            {
                if (action == null)
                {
                    return;
                }
                
                foreach (var input in action.multiInput)
                {
                    ProcessInput(input);
                }

                foreach (var input in action.inputs)
                {
                    ProcessInput(input);
                }
            }

            foreach (var evt in processedData.events)
            {
                ProcessActionInstance(evt.eventInstance);
                foreach (var action in evt.actionInstances)
                {
                    ProcessActionInstance(action);
                }
            }

            processedData.variables = variables.ToArray();

            return processedData;
        }

        public static bool CanExportAsset(Component component)
        {
            GameObject targetObject = component.gameObject;
            return !string.IsNullOrEmpty(targetObject.scene.path)
                   || PrefabUtility.IsPartOfAnyPrefab(component)
                   || PrefabStageUtility.GetPrefabStage(targetObject) != null;
        }
        
        public static string GetSavePath(GameObject sourceObject, string objectName, bool createFolder)
        {
            Scene scene = sourceObject.scene;
            string sourcePath = scene.path;
            string folderPath;
            
            // Assume scene is saved and has a path. 
            if (!string.IsNullOrEmpty(sourcePath))
            {
                sourcePath = Path.GetDirectoryName(sourcePath);
                
                string folderName = $"{scene.name}_CyanTriggerPrograms";
                folderPath = Path.Combine(sourcePath, folderName);
            
                if (createFolder && !AssetDatabase.IsValidFolder(folderPath))
                {
                    AssetDatabase.CreateFolder(sourcePath, folderName);
                }
            }
            // Scene does not have a path. Get prefab asset path as location to save.
            else
            {
                sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(sourceObject);
                if (string.IsNullOrEmpty(sourcePath))
                {
                    sourcePath = Path.GetDirectoryName(PrefabStageUtility.GetCurrentPrefabStage().prefabAssetPath);
                }
                else
                {
                    sourcePath = Path.GetDirectoryName(sourcePath);
                }
                folderPath = sourcePath;
            }

            string assetPath = Path.Combine(folderPath, $"{objectName} CyanTriggerProgram.asset");
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            
            return assetPath;
        }

        public static CyanTriggerEditableProgramAsset CreateUdonProgramSourceAsset(GameObject sourceObject, string objectName)
        {
            string assetPath = GetSavePath(sourceObject, objectName, true);
            return CreateUdonProgramSourceAsset(assetPath);
        }
        
        public static CyanTriggerEditableProgramAsset CreateUdonProgramSourceAsset(string assetPath)
        {
            CyanTriggerEditableProgramAsset asset = ScriptableObject.CreateInstance<CyanTriggerEditableProgramAsset>();
            asset.expandInInspector = true;
            asset.allowEditingInInspector = true;
            
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return asset;
        }
    }
}