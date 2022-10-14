using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.Udon;

namespace Cyan.CT.Editor
{
    public static class CyanTriggerCustomNodeInspectorUtil
    {
        private static readonly HashSet<string> VariableNameBlacklist = new HashSet<string>();

        private static readonly CyanTriggerCustomNodeInspectorUtilCommonOptionCache<GameObject, Type> ComponentCache =
            new CyanTriggerCustomNodeInspectorUtilCommonOptionCache<GameObject, Type>();
        private static readonly CyanTriggerCustomNodeInspectorUtilCommonOptionCache<UdonBehaviour, (string, Type)> UdonVariableCache =
            new CyanTriggerCustomNodeInspectorUtilCommonOptionCache<UdonBehaviour, (string, Type)>();
        private static readonly CyanTriggerCustomNodeInspectorUtilCommonOptionCache<UdonBehaviour, CyanTriggerEventArgData> UdonEventCache =
            new CyanTriggerCustomNodeInspectorUtilCommonOptionCache<UdonBehaviour, CyanTriggerEventArgData>();
        private static readonly CyanTriggerCustomNodeInspectorUtilCommonOptionCache<Animator, AnimatorControllerParameter> AnimatorParameterCache =
            new CyanTriggerCustomNodeInspectorUtilCommonOptionCache<Animator, AnimatorControllerParameter>();
        
        static CyanTriggerCustomNodeInspectorUtil()
        {
            foreach (var constVar in CyanTriggerAssemblyDataConsts.GetConstVariables())
            {
                VariableNameBlacklist.Add(constVar.ID);
            }
        }

        public static void ClearCache()
        {
            ComponentCache.ClearCache();
            UdonVariableCache.ClearCache();
            UdonEventCache.ClearCache();
        }

        private static Dictionary<string, T> GetIdToObjs<T>(
            CyanTriggerVariable[] variables,
            UdonBehaviour udonBehaviour)
        {
            Type tType = typeof(T);
            Dictionary<string, T> idToObj = new Dictionary<string, T>();
            foreach (var variable in variables)
            {
                if (variable.IsDisplayOnly())
                {
                    continue;
                }
                if (variable.type.Type == tType)
                {
                    // Check UdonBehaviour's public variables first for current value. 
                    if (udonBehaviour != null 
                        && udonBehaviour.publicVariables != null
                        && udonBehaviour.publicVariables.TryGetVariableValue(variable.name, out T obj) && obj != null)
                    {
                        idToObj.Add(variable.variableID, obj);
                    }
                    // TODO check if CyanTrigger or CyanTriggerAsset exists on udon and try get variable that way?
                    // Check default variables for current value. 
                    else if (variable.data.Obj is T tObj)
                    {
                        idToObj.Add(variable.variableID, tObj);
                    }
                }
            }

            return idToObj;
        }

        public static T GetTypeFromInput<T>(
            SerializedProperty inputProperty,
            CyanTriggerVariable[] variables,
            UdonBehaviour udonBehaviour,
            out bool containsSelf)
        {
            Dictionary<string, T> idToObj = GetIdToObjs<T>(variables, udonBehaviour);
            GetTypeFromInput(inputProperty, idToObj, out containsSelf, out T data);
            return data;
        }
        
        private static bool GetTypeFromInput<T>(
            SerializedProperty inputProperty,
            Dictionary<string, T> idToObj,
            out bool containsSelf,
            out T data)
        {
            containsSelf = false;
            data = default;
            
            var isVarProp = inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
            // If is a constant object
            if (!isVarProp.boolValue)
            {
                var dataProp = inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                var objData = CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataProp);
                if (objData is T obj)
                {
                    data = obj;
                    return true;
                }
                return false;
            }

            var varIdProp = inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
            string id = varIdProp.stringValue;

            // Check if variable is global
            if (idToObj.TryGetValue(id, out var objVar))
            {
                data = objVar;
                return true;
            }

            containsSelf |= CyanTriggerAssemblyDataConsts.IsIdThis(id, typeof(T));

            return false;
        }

        public static List<T> GetTypeFromMultiInput<T>(
            SerializedProperty multiInputListProperty, 
            CyanTriggerVariable[] variables,
            UdonBehaviour udonBehaviour,
            out bool containsSelf)
        {
            Dictionary<string, T> idToObj = GetIdToObjs<T>(variables, udonBehaviour);
            List<T> output = new List<T>();
            
            int size = multiInputListProperty.arraySize;
            containsSelf = false;
            for (int i = 0; i < size; ++i)
            {
                var inputProp = multiInputListProperty.GetArrayElementAtIndex(i);
                if (GetTypeFromInput(inputProp, idToObj, out bool localContainsSelf, out T data))
                {
                    output.Add(data);
                }
                containsSelf |= localContainsSelf;
            }

            return output;
        }

        public static T GetTypeFromInput<T>(
            CyanTriggerActionVariableInstance input,
            CyanTriggerVariable[] variables,
            UdonBehaviour udonBehaviour,
            out bool containsSelf)
        {
            Dictionary<string, T> idToObj = GetIdToObjs<T>(variables, udonBehaviour);
            GetTypeFromInput(input, idToObj, out containsSelf, out T data);
            return data;
        }
        
        private static bool GetTypeFromInput<T>(
            CyanTriggerActionVariableInstance input,
            Dictionary<string, T> idToObj,
            out bool containsSelf,
            out T data)
        {
            containsSelf = false;
            data = default;
            
            // If is a constant object
            if (!input.isVariable)
            {
                if (input.data.Obj is T obj)
                {
                    data = obj;
                    return true;
                }
                return false;
            }

            string id = input.variableID;
            
            // Check if variable is global
            if (idToObj.TryGetValue(id, out var objVar))
            {
                data = objVar;
                return true;
            }

            containsSelf |= CyanTriggerAssemblyDataConsts.IsIdThis(id, typeof(T));

            return false;
        }
        
        public static List<T> GetTypeFromMultiInput<T>(
            CyanTriggerActionVariableInstance[] multiInput, 
            CyanTriggerVariable[] variables,
            UdonBehaviour udonBehaviour,
            out bool containsSelf)
        {
            Dictionary<string, T> idToObj = GetIdToObjs<T>(variables, udonBehaviour);
            List<T> output = new List<T>();
            
            int size = multiInput.Length;
            containsSelf = false;
            for (int index = 0; index < size; ++index)
            {
                if (GetTypeFromInput(multiInput[index], idToObj, out bool localContainsSelf, out T data))
                {
                    output.Add(data);
                }
                containsSelf |= localContainsSelf;
            }

            return output;
        }

        public static CyanTriggerDataInstance GetDataInstanceFromCyanTrigger(
            CyanTriggerProgramAsset ctProgramAsset,
            UdonBehaviour udonBehaviour)
        {
            CyanTriggerDataInstance dataInstance = ctProgramAsset.GetCyanTriggerData();
            if (udonBehaviour != null && !(ctProgramAsset is CyanTriggerEditableProgramAsset))
            {
                // Get the CyanTrigger directly to ensure up to date data in case it hasn't been compiled yet. 
                CyanTrigger ct = udonBehaviour.GetComponent<CyanTrigger>();
                if (ct != null)
                {
                    dataInstance = ct.triggerInstance?.triggerDataInstance;
                }
            }

            return dataInstance;
        }
        
        public static List<(string, Type)> GetVariableOptionsFromCyanTrigger(
            CyanTriggerProgramAsset ctProgramAsset,
            UdonBehaviour udonBehaviour)
        {
            CyanTriggerDataInstance dataInstance = GetDataInstanceFromCyanTrigger(ctProgramAsset, udonBehaviour);
            if (dataInstance == null)
            {
                return GetVariableOptionsFromUdonProgram(ctProgramAsset);
            }

            return GetVariableOptionsFromCyanTrigger(dataInstance);
        }
        
        public static List<(string, Type)> GetVariableOptionsFromCyanTrigger(CyanTriggerDataInstance dataInstance)
        {
            List<(string, Type)> variables = new List<(string, Type)>();
            foreach (var variable in dataInstance.variables)
            {
                if (variable.IsDisplayOnly() 
                    || variable.type.Type.IsAssignableFrom(typeof(ICyanTriggerCustomTypeNoValueEditor)))   
                {
                    continue;
                }
                variables.Add((variable.name, variable.type.Type));
            }

            return variables;
        }
        
        public static List<(string, Type)> GetVariableOptionsFromUdonProgram(AbstractUdonProgramSource program)
        {
            var serializedProgram = program.SerializedProgramAsset;
            if (serializedProgram == null)
            {
                return null;
            }
            
            var udonProgram = serializedProgram.RetrieveProgram();
            if (udonProgram == null)
            {
                return null;
            }

            var results = new List<(string, Type)>();
            var symbolTable = udonProgram.SymbolTable;
            foreach (var variableName in symbolTable.GetExportedSymbols())
            {
                results.Add((variableName, symbolTable.GetSymbolType(variableName)));
            }
            
            return results;
        } 
        
        public static List<(string, Type)> GetVariableOptions(UdonBehaviour udonBehaviour)
        {
            if (udonBehaviour == null)
            {
                return null;
            }
            
            var program = udonBehaviour.programSource;
            if (program == null)
            {
                return null;
            }

            return GetVariableOptions(program, udonBehaviour);
        }
        
        public static List<(string, Type)> GetVariableOptions(
            AbstractUdonProgramSource program, 
            UdonBehaviour udonBehaviour)
        {
            if (program == null)
            {
                return null;
            }
            
            // Special case handling for CyanTrigger
            if (program is CyanTriggerProgramAsset ctProgramAsset)
            {
                return GetVariableOptionsFromCyanTrigger(ctProgramAsset, udonBehaviour);
            }
            
            // TODO UdonSharp Handling
// #if UDONSHARP
// #endif

            return GetVariableOptionsFromUdonProgram(program);
        }

        public static List<CyanTriggerEventArgData> GetEventOptionsFromCyanTrigger(
            CyanTriggerProgramAsset ctProgramAsset,
            UdonBehaviour udonBehaviour,
            // getAll is used for CyanTrigger for finding auto added events that are not directly added by the user.
            bool getAll = false)
        {
            CyanTriggerDataInstance dataInstance = GetDataInstanceFromCyanTrigger(ctProgramAsset, udonBehaviour);
            if (dataInstance == null)
            {
                return GetEventOptionsFromUdonProgram(ctProgramAsset);
            }

            return GetEventOptionsFromCyanTrigger(dataInstance, ctProgramAsset, getAll);
        }

        public static List<CyanTriggerEventArgData> GetEventOptionsFromCyanTrigger(
            CyanTriggerDataInstance dataInstance,
            CyanTriggerProgramAsset ctProgramAsset = null,
            bool getAll = false)
        {
            List<CyanTriggerEventArgData> results = new List<CyanTriggerEventArgData>();
            HashSet<string> uniqueNames = new HashSet<string>();
            
            foreach (var ctEvent in dataInstance.events)
            {
                CyanTriggerActionInfoHolder actionInfo = 
                    CyanTriggerActionInfoHolder.GetActionInfoHolder(ctEvent.eventInstance.actionType);
                
                string eventName = actionInfo.GetEventCompiledName(ctEvent);
                if (uniqueNames.Contains(eventName))
                {
                    continue;
                }
                uniqueNames.Add(eventName);
                
                results.Add(actionInfo.GetBaseEventArgData(ctEvent));
            }

            // Search for events that were auto added that are not custom.
            if (getAll && ctProgramAsset)
            {
                var instance = CyanTriggerNodeDefinitionManager.Instance;
                List<CyanTriggerEventArgData> allEvents = GetEventOptionsFromUdonProgram(ctProgramAsset);
                foreach (var otherEvent in allEvents)
                {
                    string eventName = otherEvent.eventName;
                    if (uniqueNames.Contains(eventName) || !instance.TryGetDefinitionFromCompiledName(eventName, out _))
                    {
                        continue;
                    }
                    
                    results.Add(otherEvent);
                }
            }
            
            return results;
        }

        public static List<CyanTriggerEventArgData> GetEventOptionsFromUdonProgram(AbstractUdonProgramSource program)
        {
            var serializedProgram = program.SerializedProgramAsset;
            if (serializedProgram == null)
            {
                return null;
            }
                
            var udonProgram = serializedProgram.RetrieveProgram();
            if (udonProgram == null)
            {
                return null;
            }

            var nodeDefinitionManager = CyanTriggerNodeDefinitionManager.Instance;
            List<CyanTriggerEventArgData> results = new List<CyanTriggerEventArgData>();
            foreach (var eventName in udonProgram.EntryPoints.GetExportedSymbols())
            {
                if (nodeDefinitionManager.TryGetDefinitionFromCompiledName(eventName, out var definition))
                {
                    var actionInfo = CyanTriggerActionInfoHolder.GetActionInfoHolder(definition);
                    results.Add(actionInfo.GetBaseEventArgData());
                }
                else
                {
                    results.Add(new CyanTriggerEventArgData
                    {
                        eventName = eventName
                    });
                }
            }

            return results;
        }

        public static List<CyanTriggerEventArgData> GetEventOptions(UdonBehaviour udonBehaviour)
        {
            if (udonBehaviour == null)
            {
                return null;
            }
            
            var program = udonBehaviour.programSource;
            if (program == null)
            {
                return null;
            }

            return GetEventOptions(program, udonBehaviour);
        }

        public static List<CyanTriggerEventArgData> GetEventOptions(
            AbstractUdonProgramSource program,
            UdonBehaviour udonBehaviour,
            // getAll is used for CyanTrigger for finding auto added events that are not directly added by the user.
            bool getAll = false)
        {
            if (program == null)
            {
                return null;
            }
            
            // Special case handling for CyanTrigger
            if (program is CyanTriggerProgramAsset ctProgramAsset)
            {
                return GetEventOptionsFromCyanTrigger(ctProgramAsset, udonBehaviour, getAll);
            }
            
            // TODO UdonSharp Handling
// #if UDONSHARP
// #endif

            return GetEventOptionsFromUdonProgram(program);
        }
        
        public static Dictionary<string, Type> GetVariableOptions(
            CyanTriggerDataInstance dataInstance,
            List<UdonBehaviour> udonTargets,
            bool containsSelf,
            bool setVariable)
        {
            Dictionary<string, Type> variableOptions = new Dictionary<string, Type>();
            bool initialized = false;

            bool ShouldSkipVariable(string varName)
            {
                if (varName.StartsWith("__"))
                {
                    return true;
                }

                return setVariable && VariableNameBlacklist.Contains(varName);
            }
            
            void UpdateVariableOptions(IList<(string, Type)> otherVariables)
            {
                if (!initialized)
                {
                    foreach (var variable in otherVariables)
                    {
                        if (ShouldSkipVariable(variable.Item1))
                        {
                            continue;
                        }
                        
                        variableOptions.Add(variable.Item1, variable.Item2);
                    }
                    initialized = true;
                    return;
                }
                
                // Only add variables that both have. 
                HashSet<string> optionsToKeep = new HashSet<string>();
                foreach (var variable in otherVariables)
                {
                    if (ShouldSkipVariable(variable.Item1))
                    {
                        continue;
                    }
                    
                    if (!variableOptions.TryGetValue(variable.Item1, out var varType) || varType != variable.Item2)
                    {
                        continue;
                    }

                    optionsToKeep.Add(variable.Item1);
                }
                
                foreach (var evtName in new List<string>(variableOptions.Keys))
                {
                    if (!optionsToKeep.Contains(evtName))
                    {
                        variableOptions.Remove(evtName);
                    }
                }
            }

            // Go through all udonTargets and find all variables.
            foreach (var udon in udonTargets)
            {
                if (UdonVariableCache.TryGetFromCache(udon, out var cachedVarOptions))
                {
                    UpdateVariableOptions(cachedVarOptions);
                    continue;
                }
                
                List<(string, Type)> varOptions = GetVariableOptions(udon);
                if (varOptions == null)
                {
                    continue;
                }

                UdonVariableCache.AddToCache(udon, varOptions);
                UpdateVariableOptions(varOptions);
            }
            
            // If contains self, get all variables in the CyanTrigger
            if (containsSelf)
            {
                UpdateVariableOptions(GetVariableOptionsFromCyanTrigger(dataInstance));
            }
            
            return variableOptions;
        }

        public static List<CyanTriggerEventArgData> GetEventOptions(
            CyanTriggerDataInstance dataInstance,
            List<UdonBehaviour> udonTargets,
            bool containsSelf,
            bool acceptsParameters,
            bool networkedOnly)
        {
            Dictionary<string, CyanTriggerEventArgData> eventNameToArgs =
                new Dictionary<string, CyanTriggerEventArgData>();

            bool initialized = false;
            
            bool ShouldSkipEvent(CyanTriggerEventArgData evt)
            {
                if (networkedOnly && evt.eventName.StartsWith("_"))
                {
                    return true;
                }

                if (!acceptsParameters && evt.variableNames.Length > 0)
                {
                    return true;
                }
                
                return false;
            }
            
            void UpdateEventOptions(IList<CyanTriggerEventArgData> otherEvents)
            {
                if (!initialized)
                {
                    foreach (var evt in otherEvents)
                    {
                        if (ShouldSkipEvent(evt))
                        {
                            continue;
                        }
                        eventNameToArgs.Add(evt.eventName, evt);
                    }
                    initialized = true;
                    return;
                }

                // Only add events that both have. 
                HashSet<string> optionsToKeep = new HashSet<string>();
                foreach (var evt in otherEvents)
                {
                    if (ShouldSkipEvent(evt))
                    {
                        continue;
                    }
                    
                    if (!eventNameToArgs.TryGetValue(evt.eventName, out var curEvent))
                    {
                        continue;
                    }

                    // TODO check if args match
                    optionsToKeep.Add(evt.eventName);
                }
                
                foreach (var evtName in new List<string>(eventNameToArgs.Keys))
                {
                    if (!optionsToKeep.Contains(evtName))
                    {
                        eventNameToArgs.Remove(evtName);
                    }
                }
            }

            // Go through all udonTargets and find all exported events.
            foreach (var udon in udonTargets)
            {
                if (UdonEventCache.TryGetFromCache(udon, out var cachedOptions))
                {
                    UpdateEventOptions(cachedOptions);
                    continue;
                }
                
                var options = GetEventOptions(udon);
                if (options == null)
                {
                    continue;
                }

                UdonEventCache.AddToCache(udon, options);
                UpdateEventOptions(options);
            }
            
            // If contains self, get all events in the CyanTrigger
            if (containsSelf)
            {
                UpdateEventOptions(GetEventOptionsFromCyanTrigger(dataInstance));
            }

            return new List<CyanTriggerEventArgData>(eventNameToArgs.Values);
        }

        public static bool HasEventOptions(
            CyanTriggerDataInstance dataInstance,
            List<UdonBehaviour> udonTargets,
            bool containsSelf,
            CyanTriggerEventArgData eventArgData,
            bool acceptsParameters,
            bool networkedOnly)
        {
            // TODO optimize
            var eventOptions = 
                GetEventOptions(dataInstance, udonTargets, containsSelf, acceptsParameters, networkedOnly);
            foreach (var eventArgs in eventOptions)
            {
                Debug.Log(eventArgs);
                if (eventArgs.Equals(eventArgData))
                {
                    return true;
                }
            }

            return false;
        }

        public static List<Type> GetComponentOptions(List<GameObject> gameObjects)
        {
            var defManager = CyanTriggerNodeDefinitionManager.Instance;
            Dictionary<Type, bool> validComponentType = new Dictionary<Type, bool>();
            
            bool IsValidComponent(Type component)
            {
                if (component == null)
                {
                    return false;
                }
                
                if (validComponentType.TryGetValue(component, out bool isValid))
                {
                    return isValid;
                }
                
                var setMethod = component.GetProperty(nameof(Behaviour.enabled))?.GetSetMethod();
                if (setMethod == null)
                {
                    validComponentType.Add(component, false);
                    return false;
                }
                var externSignature = CyanTriggerDefinitionResolver.GetMethodSignature(setMethod);
                bool valid = defManager.GetDefinition(externSignature) != null;
                validComponentType.Add(component, valid);
                return valid;
            }

            IList<Type> GetComponentTypes(GameObject gameObject)
            {
                if (ComponentCache.TryGetFromCache(gameObject, out var cachedTypes))
                {
                    return cachedTypes;
                }
                
                List<Type> componentTypes = new List<Type>();
                Component[] components = gameObject.GetComponents<Component>();

                foreach (var component in components)
                {
                    Type curType = component.GetType();
                    while (curType != null)
                    {
                        if (IsValidComponent(curType))
                        {
                            componentTypes.Add(curType);
                        }
                        curType = curType.BaseType;
                    }
                }
                
                ComponentCache.AddToCache(gameObject, componentTypes);
                return componentTypes;
            }

            return GetCommonOptions(gameObjects, GetComponentTypes, IsValidComponent);
        }

        public static List<AnimatorControllerParameter> GetAnimatorParameterOptions(List<Animator> animators, Type type)
        {
            // Only pick the types that match this Inspector
            bool IsValidParameter(AnimatorControllerParameter parameter)
            {
                if (type == typeof(bool))
                {
                    return parameter.type == AnimatorControllerParameterType.Bool
                           || parameter.type == AnimatorControllerParameterType.Trigger;
                }
                if (type == typeof(int))
                {
                    return parameter.type == AnimatorControllerParameterType.Int;
                }
                if (type == typeof(float))
                {
                    return parameter.type == AnimatorControllerParameterType.Float;
                }

                return false;
            }

            // Note that going through the controller is needed as updating the controller file will reset the
            // Animator.parameters value to zero length array.
            IList<AnimatorControllerParameter> GetParameters(Animator animator)
            {
                if (AnimatorParameterCache.TryGetFromCache(animator, out var options))
                {
                    return options;
                }
                
                RuntimeAnimatorController controller = animator.runtimeAnimatorController;
                
                // If an override controller, keep getting the override controller until we find
                // something that isn't another override
                while (controller != null && controller is AnimatorOverrideController overrideController)
                {
                    controller = overrideController.runtimeAnimatorController;
                }

                // Get parameters from the controller directly
                if (controller is AnimatorController animatorController)
                {
                    var results = animatorController.parameters;
                    AnimatorParameterCache.AddToCache(animator, results);
                    return results;
                }

                // if animator.runtimeAnimatorController is null, warnings will spam the log.
                if (controller != null)
                {
                    // Fallback to animator parameters
                    var results = animator.parameters;
                    AnimatorParameterCache.AddToCache(animator, results);
                    return results;
                }
                
                var empty = Array.Empty<AnimatorControllerParameter>();
                AnimatorParameterCache.AddToCache(animator, empty);
                return empty;
            }

            return GetCommonOptions(animators, GetParameters, IsValidParameter);
        }

        public static List<TReturnType> GetCommonOptions<TReturnType, TInputType> (
            List<TInputType> inputObjects,
            Func<TInputType, IList<TReturnType>> getOptionsFromInput,
            Func<TReturnType, bool> isValidReturn)
        {
            HashSet<TReturnType> options = new HashSet<TReturnType>();
            bool initialized = false;

            void UpdateOptions(IList<TReturnType> inputOptions)
            {
                if (!initialized)
                {
                    foreach (var option in inputOptions)
                    {
                        if (isValidReturn(option))
                        {
                            options.Add(option);
                        }
                    }
                    initialized = true;
                    return;
                }
                
                // Only add options that both have. 
                HashSet<TReturnType> optionsToKeep = new HashSet<TReturnType>();
                foreach (var option in inputOptions)
                {
                    if (isValidReturn(option) && options.Contains(option))
                    {
                        optionsToKeep.Add(option);
                    }
                }

                options = optionsToKeep;
            }

            // Go through all inputs and find all outputs.
            foreach (var input in inputObjects)
            {
                UpdateOptions(getOptionsFromInput(input));
            }
            
            return new List<TReturnType>(options);
        }

        public static CyanTriggerActionVariableDefinition GetUpdatedDefinitionFromSelectedVariable(
            SerializedProperty variableNameInputProperty,
            CyanTriggerActionVariableDefinition variableValueInputVarDef,
            Dictionary<string, Type> variableNamesToTypes)
        {
            var isVariableProp =
                variableNameInputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
            if (isVariableProp.boolValue)
            {
                return variableValueInputVarDef;
            }
            
            var dataProp =
                variableNameInputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
            var varNameObj = CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataProp);
            if (!(varNameObj is string varName))
            {
                return variableValueInputVarDef;
            }

            if (variableNamesToTypes.TryGetValue(varName, out Type varType) && varType != null)
            {
                return new CyanTriggerActionVariableDefinition
                {
                    type = new CyanTriggerSerializableType(varType),
                            
                    description = variableValueInputVarDef.description,
                    defaultValue = variableValueInputVarDef.defaultValue,
                    displayName = variableValueInputVarDef.displayName,
                    udonName = variableValueInputVarDef.udonName,
                    variableType = variableValueInputVarDef.variableType
                };
            }

            return variableValueInputVarDef;
        }
    }
}