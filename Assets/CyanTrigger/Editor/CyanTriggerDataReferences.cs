using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Serialization;
using VRC.Udon;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Serialization.OdinSerializer;
using Object = UnityEngine.Object;

namespace Cyan.CT.Editor
{
    [Serializable]
    public class CyanTriggerActionDataReferenceIndex
    {
        // ReSharper disable once InconsistentNaming
        // Old Odin Serialized version that is deprecated. 
        [NonSerialized, OdinSerialize, Obsolete]
        public Type symbolType = null;

        public string symbolName;
        public CyanTriggerSerializableType type;
        public int eventIndex;
        public int actionIndex;
        public int variableIndex;
        public int multiVariableIndex;

        public Type GetSymbolType()
        {
            if (type != null)
            {
                return type.Type;
            }
            
#pragma warning disable CS0612
            return symbolType;
#pragma warning restore CS0612
        }

        public override string ToString()
        {
            return $"Symbol: {GetSymbolType()} {symbolName} event[{eventIndex}].action[{actionIndex}].var[{multiVariableIndex}, {variableIndex}]";
        }
    }
    
    [Serializable]
    public class CyanTriggerDataReferences
    {
        [SerializeField]
        private CyanTriggerActionDataReferenceIndex[] actionDataIndices;
        [SerializeField]
        private string[] userVariableNames;
        [SerializeField]
        private CyanTriggerSerializableType[] userVariableTypes;
        
        #region Deprecated Data
        
        // ReSharper disable InconsistentNaming, Unity.RedundantFormerlySerializedAsAttribute
        [NonSerialized, OdinSerialize, Obsolete, FormerlySerializedAs("ActionDataIndices")]
        public List<CyanTriggerActionDataReferenceIndex> actionDataIndicesOdin;
        [NonSerialized, OdinSerialize, Obsolete, FormerlySerializedAs("userVariables")]
        public Dictionary<string, Type> userVariablesOdin = null;
        // ReSharper restore InconsistentNaming, Unity.RedundantFormerlySerializedAsAttribute

        #endregion

        private Dictionary<string, Type> _userVariablesMap;

        public CyanTriggerDataReferences(
            List<CyanTriggerActionDataReferenceIndex> actionDataIndicesList,
            Dictionary<string, Type> userVariablesMap)
        {
            actionDataIndices = actionDataIndicesList.ToArray();

            int size = userVariablesMap.Count;
            userVariableNames = new string[size];
            userVariableTypes = new CyanTriggerSerializableType[size];
            _userVariablesMap = new Dictionary<string, Type>();

            int count = 0;
            foreach (var variable in userVariablesMap)
            {
                string name = variable.Key;
                Type type = variable.Value;
                userVariableNames[count] = name;
                userVariableTypes[count] = new CyanTriggerSerializableType(type);
                ++count;
                
                _userVariablesMap.Add(name, type);
            }

#pragma warning disable CS0612
            actionDataIndicesOdin = null;
            userVariablesOdin = null;
#pragma warning restore CS0612
        }

        private void UpdateUserVariableCache()
        {
            _userVariablesMap = new Dictionary<string, Type>();
            for (int index = 0; index < userVariableNames.Length; ++index)
            {
                _userVariablesMap.Add(userVariableNames[index], userVariableTypes[index].Type);
            }
        }

        private Dictionary<string, Type> GetUserVariableMap()
        {
            if (_userVariablesMap == null && userVariableNames != null)
            {
                UpdateUserVariableCache();
            }
            if (_userVariablesMap != null)
            {
                return _userVariablesMap;
            }
            
#pragma warning disable CS0612
            return userVariablesOdin;
#pragma warning restore CS0612
        }

        public IList<CyanTriggerActionDataReferenceIndex> GetActionDataIndices()
        {
            if (actionDataIndices != null)
            {
                return actionDataIndices;
            }
            
#pragma warning disable CS0612
            return actionDataIndicesOdin;
#pragma warning restore CS0612
        }

        public void ApplyPublicVariableData(
            CyanTriggerVariable[] variables,
            CyanTriggerSerializableObject[] variableData,
            CyanTriggerEvent[] events,
            string programName,
            UdonBehaviour udonBehaviour,
            IUdonSymbolTable symbolTable,
            ref bool dirty)
        {
            IUdonVariableTable publicVariables = udonBehaviour.publicVariables;
            if (publicVariables == null)
            {
                Debug.LogError("Cannot set public variables when VariableTable is null");
                return;
            }
            
            // Remove non-exported public variables
            foreach(string publicVariableSymbol in new List<string>(publicVariables.VariableSymbols))
            {
                // Symbol table doesn't have the variable name 
                // The type for the symbol doesn't match the type currently in the public variable table.
                if(!symbolTable.HasExportedSymbol(publicVariableSymbol))
                {
                    //Debug.Log($"Removing Reference: {publicVariableSymbol}");
                    publicVariables.RemoveVariable(publicVariableSymbol);
                    dirty = true;
                }
                
                if((publicVariables.TryGetVariableType(publicVariableSymbol, out var type) && 
                    type != symbolTable.GetSymbolType(publicVariableSymbol)))
                {
                    //Debug.Log($"Removing Reference: {publicVariableSymbol}, type {type}, expected: {symbolTable.GetSymbolType(publicVariableSymbol)}");
                    publicVariables.RemoveVariable(publicVariableSymbol);
                    dirty = true;
                }
            }

            GameObject udonObj = udonBehaviour.gameObject;

            HashSet<string> usedVariables = new HashSet<string>();

            // TODO figure out a generic way to handle external data like TimerQueue
            
            // Set the constant value for the program name
            string programNameVariableName = CyanTriggerAssemblyData.GetSpecialVariableName(
                CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ProgramName);
            usedVariables.Add(programNameVariableName);
            SetUdonVariable(
                udonBehaviour, 
                publicVariables, 
                programNameVariableName, 
                typeof(string), 
                programName, 
                ref dirty);

            Dictionary<string, Type> userVariablesMap = GetUserVariableMap();
            if (variables != null && userVariablesMap != null)
            {
                for (var index = 0; index < variables.Length; ++index)
                {
                    var variable = variables[index];
                    if (variable.IsDisplayOnly())
                    {
                        continue;
                    }
                    
                    // Check if variable was valid based on last compile
                    // Hidden variables will be skipped here as they will not be included in the userVariables map.
                    if (!userVariablesMap.TryGetValue(variable.name, out var type) || type != variable.type.Type)
                    {
                        // Debug.LogWarning($"Variables does not contain {variable.name} {variable.type.Type}");
                        continue;
                    }

                    usedVariables.Add(variable.name);

                    object value = variableData != null && index < variableData.Length
                        ? variableData[index].Obj
                        : variable.data.Obj;
                    
                    object messageValue = value;
                    value = VerifyVariableData(type, value,
                        () =>
                            $"Global variable \"{variable.name}\" contains invalid data for type {type.Name}. Data: {messageValue}. Replacing with default value. Please check the CyanTrigger on object {VRC.Tools.GetGameObjectPath(udonObj)}");

                    SetUdonVariable(
                        udonBehaviour,
                        publicVariables,
                        variable.name,
                        type,
                        value,
                        ref dirty);

                    // Variable had a callback. Ensure that previous value is equal to default value.
                    string prevVarName = CyanTriggerCustomNodeOnVariableChanged.GetOldVariableName(variable.name);
                    if (symbolTable.HasExportedSymbol(prevVarName))
                    {
                        usedVariables.Add(prevVarName);
                        SetUdonVariable(
                            udonBehaviour,
                            publicVariables,
                            prevVarName,
                            type,
                            value,
                            ref dirty);
                    }
                }
            }

            var dataIndices = GetActionDataIndices();
            if (events != null && dataIndices != null)
            {
                foreach (var publicVar in dataIndices)
                {
                    object data = GetDataForReferenceIndex(publicVar, events, udonObj, out Type type);

#if CYAN_TRIGGER_DEBUG
                    Type expectedType = symbolTable.GetSymbolType(publicVar.symbolName);
                    if (expectedType != type && !type.IsAssignableFrom(expectedType))
                    {
                        Debug.LogWarning($"Type for symbol does not match public variable type. {expectedType}, {type}. Please check the CyanTrigger on object {VRC.Tools.GetGameObjectPath(udonObj)}");
                    }
#endif
                
                    usedVariables.Add(publicVar.symbolName);
                    
                    SetUdonVariable(
                        udonBehaviour, 
                        publicVariables, 
                        publicVar.symbolName, 
                        type, 
                        data,
                        ref dirty);
                }
            }
            
#if CYAN_TRIGGER_DEBUG
            // Used for debug purposes to see if a public variable was missed.
            foreach (string publicVariableSymbol in new List<string>(publicVariables.VariableSymbols))
            {
                if(!usedVariables.Contains(publicVariableSymbol))
                {
                    Debug.LogWarning($"[CyanTrigger][Debug] Variable was unused: {publicVariableSymbol}");
                }
            }
#endif
        }

        public static object GetDataForReferenceIndex(
            CyanTriggerActionDataReferenceIndex publicVar, 
            CyanTriggerEvent[] events,
            GameObject gameObject,
            out Type type)
        {
            object data = null;
            type = publicVar.GetSymbolType();

            var eventInstance = events[publicVar.eventIndex];
            CyanTriggerActionInstance actionInstance;

            string message = $"Event[{publicVar.eventIndex}]";
            
            if (publicVar.actionIndex < 0)
            {
                // TODO figure out event organization here
                actionInstance = eventInstance.eventInstance;
            }
            else
            {
                actionInstance = eventInstance.actionInstances[publicVar.actionIndex];
                message += $".Action[{publicVar.actionIndex}]";
            }

            // TODO figure out a way to get modified data from custom udon node definitions.
            if (actionInstance != null)
            {
                CyanTriggerActionVariableInstance variableInstance;
                if (publicVar.multiVariableIndex != -1)
                {
                    variableInstance = actionInstance.multiInput[publicVar.multiVariableIndex];
                    message += $".Input[0][{publicVar.multiVariableIndex}]";
                }
                else
                {
                    variableInstance = actionInstance.inputs[publicVar.variableIndex];
                    message += $".Input[{publicVar.variableIndex}]";
                }

                data = variableInstance.data.Obj;
            }
            
            
            // Check is required for the case when a destroyed object is still saved, but shouldn't be. 
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (typeof(Object).IsAssignableFrom(type) 
                && data != null 
                && (!(data is Object dataObj) || dataObj == null))
            {
                data = null;
            }
            
            // TODO fix this. This is too hacky.
            if (type == typeof(CyanTrigger) || type == typeof(IUdonEventReceiver))
            {
                type = typeof(UdonBehaviour);
            }
            if (type == typeof(UdonBehaviour) && data is CyanTrigger trigger)
            {
                data = trigger.triggerInstance?.udonBehaviour;
            }

            // TODO find a better way here...
            if (publicVar.variableIndex == 1 && actionInstance != null && 
                (actionInstance.actionType.directEvent == CyanTriggerCustomNodeSetComponentActive.FullName 
                || actionInstance.actionType.directEvent == CyanTriggerCustomNodeSetComponentActiveToggle.FullName))
            {
                string varType = data as string;
                
                if (CyanTriggerNodeDefinitionManager.Instance.TryGetComponentType(varType, out var componentType))
                {
                    data = componentType.AssemblyQualifiedName;
                }
                else
                {
                    Debug.LogWarning($"{message} Could not find type for SetComponentActive: {varType}. {(gameObject == null ? "null" : VRC.Tools.GetGameObjectPath(gameObject))}");
                }
            }
            
            Type exportType = type;
            object messageData = data;
            data = VerifyVariableData(type, data, () => $"{message} contains invalid data for type {exportType.Name}. Data: {messageData}. Replacing with default value. Please check the CyanTrigger on object {(gameObject == null ? "null" : VRC.Tools.GetGameObjectPath(gameObject))}");

            return data;
        }

        private static object VerifyVariableData(
            Type symbolType,
            object value,
            Func<string> getMessage)
        {
            bool badData = false;
            object other = CyanTriggerPropertyEditor.CreateInitialValueForType(symbolType, value, ref badData);
            if (badData)
            {
                Debug.LogError(getMessage());
                value = other;
            }

            return value;
        }

        private static void SetUdonVariable(
            UdonBehaviour udonBehaviour, 
            IUdonVariableTable publicVariables, 
            string exportedSymbol, 
            Type symbolType, 
            object value, 
            ref bool dirty)
        {
            object messageValue = value;
            value = VerifyVariableData(symbolType, value, () => $"Variable \"{exportedSymbol}\" contains invalid data for type {symbolType.Name}. Data: {messageValue}. Replacing with default value. Please check the CyanTrigger on object {VRC.Tools.GetGameObjectPath(udonBehaviour.gameObject)}");
            
            bool hasVariable = publicVariables.TryGetVariableValue(exportedSymbol, out object variableValue);

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            // Unity objects override null and may return different results. 
            if (value == null || (value is Object unityValue && unityValue == null))
            {
                if (hasVariable && variableValue != null)
                {
                    dirty = true;
                    // Debug.Log(exportedSymbol +" was changed! " + variableValue +" to " +value);
                    // Debug.Log("Setting object dirty after removing variable: " + VRC.Tools.GetGameObjectPath(udonBehaviour.gameObject) +" " +exportedSymbol);
                    EditorUtility.SetDirty(udonBehaviour);
 
                    Undo.RecordObject(udonBehaviour, "Modify Public Variable");

                    publicVariables.RemoveVariable(exportedSymbol);

                    EditorSceneManager.MarkSceneDirty(udonBehaviour.gameObject.scene);

                    if (PrefabUtility.IsPartOfPrefabInstance(udonBehaviour))
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(udonBehaviour);
                    }
                }
                
                return;
            }

            // This is jank
            bool DeepEquals(object o1, object o2)
            {
                if (o1 == null ^ o2 == null)
                {
                    return false;
                }
                
                if (Equals(o1, o2))
                {
                    return true;
                }
                
                Type t1 = o1.GetType();
                if (t1 != o2.GetType())
                {
                    return false;
                }

                if (t1.IsArray)
                {
                    Array a1 = o1 as Array;
                    Array a2 = o2 as Array;

                    int l1 = a1.Length;
                    if (l1 != a2.Length)
                    {
                        return false;
                    }

                    for (int index = 0; index < l1; ++index)
                    {
                        if (!DeepEquals(a1.GetValue(index), a2.GetValue(index)))
                        {
                            return false;
                        }
                    }

                    return true;
                }

                return false;
            }
            
            if (!hasVariable || !DeepEquals(value, variableValue))
            {
                dirty = true;
                // Debug.Log(exportedSymbol +" was changed! " + variableValue +" to " +value);
                // Debug.Log("Setting object dirty after updating variable: " + VRC.Tools.GetGameObjectPath(udonBehaviour.gameObject));
                EditorUtility.SetDirty(udonBehaviour);
 
                Undo.RecordObject(udonBehaviour, "Modify Public Variable");

                if (!publicVariables.TrySetVariableValue(exportedSymbol, value))
                {
                    if (!publicVariables.TryAddVariable(CreateUdonVariable(exportedSymbol, value, symbolType)))
                    {
                        Debug.LogError($"Failed to set public variable '{exportedSymbol}' value.");
                    }
                }

                EditorSceneManager.MarkSceneDirty(udonBehaviour.gameObject.scene);

                if (PrefabUtility.IsPartOfPrefabInstance(udonBehaviour))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(udonBehaviour);
                }
            }
        }
        
        public static IUdonVariable CreateUdonVariable(string symbolName, object value, Type declaredType)
        {
            try
            {
                Type udonVariableType = typeof(UdonVariable<>).MakeGenericType(declaredType);
                return (IUdonVariable) Activator.CreateInstance(udonVariableType, symbolName, value);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to create UdonVariable for symbol: {symbolName}, type: {declaredType}, object: {value}");
                Debug.LogException(e);
                throw;
            }
        }
    }
}
