using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using VRC.Udon;
using VRC.SDKBase;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Compiler.Compilers;

namespace Cyan.CT.Editor
{
    public static class CyanTriggerAssemblyDataConsts
    {
        private static readonly List<CyanTriggerEditorVariableOption> ConstVariables;
        public static readonly CyanTriggerEditorVariableOption ThisGameObject;
        public static readonly CyanTriggerEditorVariableOption ThisTransform;
        public static readonly CyanTriggerEditorVariableOption ThisCyanTrigger;
        public static readonly CyanTriggerEditorVariableOption ThisUdonBehaviour;
        public static readonly CyanTriggerEditorVariableOption LocalPlayer;
        public static readonly CyanTriggerEditorVariableOption NullObject;
        
        
        static CyanTriggerAssemblyDataConsts()
        {
            ConstVariables = new List<CyanTriggerEditorVariableOption>();
            NullObject = AddConstVariable("null", "__null", typeof(object));
            ThisGameObject = AddConstVariable("This GameObject", "_this_gameobject", typeof(GameObject));
            ThisTransform = AddConstVariable("This Transform", "_this_transform", typeof(Transform));
            ThisCyanTrigger = AddConstVariable("This CyanTrigger", "_this_cyantrigger", typeof(CyanTrigger));
            ThisUdonBehaviour = AddConstVariable("This UdonBehaviour", "_this_udonbehaviour", typeof(UdonBehaviour));
            LocalPlayer = AddConstVariable("Local Player", "_this_local_player", typeof(VRCPlayerApi));
        }

        private static CyanTriggerEditorVariableOption AddConstVariable(string name, string id, Type type)
        {
            var variable = new CyanTriggerEditorVariableOption
            {
                Name = name,
                ID = id,
                Type = type,
                IsReadOnly = true,
            };
            ConstVariables.Add(variable);
            return variable;
        }
        
        public static IEnumerable<CyanTriggerEditorVariableOption> GetConstVariables()
        {
            return ConstVariables;
        }

        // TODO find a better way to scale this.
        public static bool IsIdThis(string id, Type type)
        {
            if (type == typeof(UdonBehaviour))
            {
                return id == ThisUdonBehaviour.ID;
            }
            if (type == typeof(CyanTrigger))
            {
                return id == ThisCyanTrigger.ID;
            }
            if (type == typeof(Transform))
            {
                return  id == ThisTransform.ID;
            }
            if (type == typeof(GameObject))
            {
                return  id == ThisGameObject.ID;
            }

            return false;
        }
    }

    public class CyanTriggerAssemblyData
    {
        public const string JumpReturnVariableName = "__jump_return_";
        public const string CtEventArgPrefix = "_arg_";
        public const string CtInternalPostfix = "_ct_intern";

        private static readonly Dictionary<string, List<(string, Type)>> EventsToEventVariables =
            new Dictionary<string, List<(string, Type)>>();

        private static readonly HashSet<string> SpecialVariableNames = new HashSet<string>();
        private static readonly HashSet<string> SpecialCustomActionVariableNames = new HashSet<string>();

        private readonly List<string> _orderedVariables = new List<string>();

        private readonly Dictionary<string, CyanTriggerAssemblyDataType> _variables =
            new Dictionary<string, CyanTriggerAssemblyDataType>();

        private readonly Dictionary<Type, Dictionary<object, CyanTriggerAssemblyDataType>> _variableConstants =
            new Dictionary<Type, Dictionary<object, CyanTriggerAssemblyDataType>>();

        private readonly Dictionary<Type, Queue<CyanTriggerAssemblyDataType>> _tempVariables =
            new Dictionary<Type, Queue<CyanTriggerAssemblyDataType>>();

        private readonly Dictionary<string, string> _userDefinedVariables = new Dictionary<string, string>();

        private readonly List<(CyanTriggerAssemblyDataType, CyanTriggerAssemblyInstruction)> _jumpReturnVariables =
            new List<(CyanTriggerAssemblyDataType, CyanTriggerAssemblyInstruction)>();

        private readonly Dictionary<Type, CyanTriggerAssemblyDataType> _thisConstsByType =
            new Dictionary<Type, CyanTriggerAssemblyDataType>();

        private readonly Dictionary<string, CyanTriggerAssemblyDataType> _thisConstsById =
            new Dictionary<string, CyanTriggerAssemblyDataType>();

        private readonly Dictionary<string, CyanTriggerAssemblyDataType> _uniqueInternalVars =
            new Dictionary<string, CyanTriggerAssemblyDataType>();

        public enum CyanTriggerSpecialVariableName
        {
            ReturnAddress,
            EndAddress,
            BroadcastCount,
            ProgramName,
            ProgramHash,
            TimerQueue,
            ReturnValue,
            
            // Only add Custom Action Specials after this point.
            // Index will be used for determining if it is custom action related.
            ActionJumpAddress, 
            ActionInstanceEventJumpAddress,
            ActionInstanceEventDelayFramesJumpAddress,
            ActionInstanceEventDelaySecondsJumpAddress,
            ActionInstanceEventNetworkedJumpAddress,

            ActionInstanceEventName,
            ActionInstanceEventDelayFrames,
            ActionInstanceEventDelaySeconds,
            ActionInstanceEventDelayTiming,
            ActionInstanceEventNetworkTarget,
        }

        // Do not change these variable names as it will break CustomActions if no fallback is set.
        private static readonly (string, Type)[] UdonSpecialVariableNames =
        {
            ("__0_ra_SystemUInt32", typeof(uint)), // Return Address
            ("__1_ea_SystemUInt32", typeof(uint)), // End Address
            ("__2_bc_SystemUInt32", typeof(uint)), // Broadcast Count
            ("__CyanTrigger_ProgramName_SystemString", typeof(string)), // Program Name
            ("__CyanTrigger_ProgramHash_SystemString", typeof(string)), // Program Hash, used for checking if it should be recompiled...
            ("__timer_queue", typeof(UdonBehaviour)),
            (UdonBehaviour.ReturnVariableName, typeof(object)),
            
            // This is bad and should be rewritten somehow.
            // Only used during compiling Custom Actions but never output
            ("__aja_SystemUInt32", typeof(uint)), // Action Jump Address
            
            ("__aieja_SystemUInt32", typeof(uint)), // Jump Address for Custom Action Instances to jump to SendCustomEvent
            ("__aiedfja_SystemUInt32", typeof(uint)), // Jump Address for Custom Action Instances to jump to SendCustomEventDelayedFrames
            ("__aiedsja_SystemUInt32", typeof(uint)), // Jump Address for Custom Action Instances to jump to SendCustomEventDelayedSeconds
            ("__aienja_SystemUInt32", typeof(uint)), // Jump Address for Custom Action Instances to jump to SendCustomNetworkedEvent
            
            ("__aien_SystemString", typeof(string)), // Variable to store the name of the custom event
            ("__aiedv_SystemInt32", typeof(int)), // Variable to store Delay Frames count
            ("__aiedv_SystemSingle", typeof(float)), // Variable to store Delay Seconds
            ("__aiedt_VRCEventTiming", typeof(VRC.Udon.Common.Enums.EventTiming)), // Variable to store Delay Timing
            ("__aient_VRCNetworkEventTarget", typeof(NetworkEventTarget)), // Variable to store Network Event Target
        };

        // Unchanging data
        static CyanTriggerAssemblyData()
        {
            foreach (var eventDefinition in CyanTriggerNodeDefinitionManager.Instance.GetEventDefinitions())
            {
                List<(string, Type)> eventVariables = eventDefinition.GetEventVariables();
                EventsToEventVariables.Add(eventDefinition.GetEventName(), eventVariables);

                // Add each variable name to special variable names list
                foreach (var variable in eventVariables)
                {
                    SpecialVariableNames.Add(variable.Item1);
                }
            }

            // Add predefined special variable names
            for (var index = 0; index < UdonSpecialVariableNames.Length; index++)
            {
                string name = UdonSpecialVariableNames[index].Item1;
                SpecialVariableNames.Add(name);

                if (index >= (int)CyanTriggerSpecialVariableName.ActionJumpAddress)
                {
                    SpecialCustomActionVariableNames.Add(name);
                }
            }

            // Add all consts to ensure that CustomActions will ignore modifying these variables.
            foreach (var constVar in CyanTriggerAssemblyDataConsts.GetConstVariables())
            {
                SpecialVariableNames.Add(constVar.ID);
            }
        }

        public static void MergeData(
            CyanTriggerAssemblyData baseData, 
            Dictionary<CyanTriggerAssemblyDataType, CyanTriggerAssemblyDataType> variableMapping,
            params CyanTriggerAssemblyData[] dataToMerge)
        {
            foreach (var data in dataToMerge)
            {
                baseData._jumpReturnVariables.AddRange(data._jumpReturnVariables);

                // TODO
                //baseData.userDefinedVariables.

                foreach (var variable in data._variables.Values)
                {
                    string varName = variable.Name;
                    if (SpecialVariableNames.Contains(varName) && baseData._variables.ContainsKey(varName))
                    {
                        continue;
                    }

                    // Ensure that unique internal variables are actually unique and remapped over.
                    if (IsVarNameCtInternal(varName))
                    {
                        string internalName = GetInternalNameFromVarName(varName);
                        if (!string.IsNullOrEmpty(internalName))
                        {
                            var internalVariable = baseData.GetOrCreateUniqueInternalVariable(
                                internalName, 
                                variable.Type, 
                                variable.Export, 
                                variable.DefaultValue);
                            
                            // Ensure all other properties are copied too
                            variable.CopyTo(internalVariable);
                            
                            variableMapping.Add(variable, internalVariable);
                            
                            continue;
                        }
                    }

                    if (baseData._variables.ContainsKey(varName))
                    {
                        Debug.LogWarning($"Base data already contains variable named {varName}");
                        continue;
                    }

                    baseData._variables.Add(varName, variable);
                    baseData._orderedVariables.Add(varName);
                }
            }
        }

        public static List<(string, Type)> GetEventVariableTypes(string eventName)
        {
            EventsToEventVariables.TryGetValue(eventName, out List<(string, Type)> variableData);
            return variableData;
        }

        public void AddThisVariables()
        {
            foreach (var varConst in CyanTriggerAssemblyDataConsts.GetConstVariables())
            {
                // Skip this CyanTrigger since it is a fake variable; handled below for This Udon
                if (varConst == CyanTriggerAssemblyDataConsts.ThisCyanTrigger)
                {
                    continue;
                }

                var variable = AddNamedVariable(varConst.ID, varConst.Type, false);
                _thisConstsByType.Add(varConst.Type, variable);
                _thisConstsById.Add(varConst.ID, variable);

                if (varConst == CyanTriggerAssemblyDataConsts.ThisUdonBehaviour)
                {
                    _thisConstsByType.Add(typeof(CyanTrigger), variable);
                    _thisConstsById.Add(CyanTriggerAssemblyDataConsts.ThisCyanTrigger.ID, variable);
                }
            }
        }

        public static bool IsIdThisVariable(string varId)
        {
            return varId.StartsWith("_this_");
        }

        public void CreateSpecialAddressVariables()
        {
            GetSpecialVariable(CyanTriggerSpecialVariableName.ReturnAddress);
            CyanTriggerAssemblyDataType endAddress = GetSpecialVariable(CyanTriggerSpecialVariableName.EndAddress);
            endAddress.DefaultValue = 0xFFFFF0u;
        }

        public void CreateProgramNameVariable(string hash)
        {
            CyanTriggerAssemblyDataType programName = GetSpecialVariable(CyanTriggerSpecialVariableName.ProgramName);
            programName.DefaultValue = "CyanTrigger";
            programName.Export = true;
            
            CyanTriggerAssemblyDataType programHash = GetSpecialVariable(CyanTriggerSpecialVariableName.ProgramHash);
            programHash.DefaultValue = hash;
        }

        public int GetVariableCount()
        {
            return _variables.Count;
        }

        public IEnumerable<CyanTriggerAssemblyDataType> GetVariables()
        {
            foreach (var variableName in _orderedVariables)
            {
                yield return _variables[variableName];
            }
        }

        // TODO make more generic or a constant.
        public static bool IsVarNameArg(string varName)
        {
            if (string.IsNullOrEmpty(varName))
            {
                return false;
            }

            return varName.StartsWith(CtEventArgPrefix);
        }

        public static string CreateCustomEventArgName(string eventName, string varName)
        {
            return $"{CtEventArgPrefix}{varName}_{eventName}";
        }

        public static bool IsVarNameCtInternal(string varName)
        {
            if (string.IsNullOrEmpty(varName))
            {
                return false;
            }

            return varName.EndsWith(CtInternalPostfix);
        }

        public string CreateVariableName(string name, Type type, bool ctInternal)
        {
            string sanitizedType = CyanTriggerNameHelpers.GetSanitizedTypeName(type);
            return $"{UdonGraphCompiler.INTERNAL_VARIABLE_PREFIX}{_variables.Count}_{name}_{sanitizedType}{(ctInternal ? CtInternalPostfix: "")}";
        }
        
        public static string GetInternalNameFromVarName(string variableName)
        {
            int end = variableName.Length;
            if (IsVarNameCtInternal(variableName))
            {
                end -= CtInternalPostfix.Length;
            }

            // Find the start point after the variable count modifier.
            int start = 2;
            while (start < variableName.Length && char.IsDigit(variableName[start]))
            {
                ++start;
            }

            if (start >= variableName.Length || variableName[start] != '_')
            {
                return string.Empty;
            }

            int lastUnderscore = start;
            while (lastUnderscore != -1)
            {
                int nextUnder = variableName.IndexOf('_', lastUnderscore+1);
                if (nextUnder + 1 >= end || nextUnder == -1)
                {
                    break;
                }

                lastUnderscore = nextUnder;
            }
            
            // Skip initial underscore
            ++start;
            
            return variableName.Substring(start, lastUnderscore - start);
        }

        public CyanTriggerAssemblyDataType GetOrCreateUniqueInternalVariable(string name, Type type, bool export, object defaultValue = null)
        {
            if (!_uniqueInternalVars.TryGetValue(name, out var uniqueVar))
            {
                uniqueVar = AddVariableIntern(name, type, export, defaultValue, true);
                _uniqueInternalVars.Add(name, uniqueVar);
            }

            return uniqueVar;
        }

        public CyanTriggerAssemblyDataType AddVariable(string name, Type type, bool export, object defaultValue = null)
        {
            return AddVariableIntern(name, type, export, defaultValue, false);
        }
        
        private CyanTriggerAssemblyDataType AddVariableIntern(string name, Type type, bool export, object defaultValue, bool isInternal)
        {
            if (type.IsByRef)
            {
                type = type.GetElementType();
            }
            
            string variableName = CreateVariableName(name, type, isInternal);
            CyanTriggerAssemblyDataType var = new CyanTriggerAssemblyDataType(variableName, type, GetResolvedType(type), export);
            _variables.Add(var.Name, var);
            _orderedVariables.Add(var.Name);

            if (defaultValue != null)
            {
                var.DefaultValue = defaultValue;
            }

            return var;
        }

        public CyanTriggerAssemblyDataType AddNamedVariable(string name, Type type, bool export = false)
        {
            CyanTriggerAssemblyDataType var = new CyanTriggerAssemblyDataType(name, type, GetResolvedType(type), export);
            _variables.Add(var.Name, var);
            _orderedVariables.Add(var.Name);
            return var;
        }

        public CyanTriggerAssemblyDataType AddUserDefinedVariable(
            string name, 
            string guid,
            Type type, 
            CyanTriggerVariableSyncMode sync, 
            bool hasCallback,
            bool export = true)
        {
            if (type.IsByRef)
            {
                type = type.GetElementType();
            }
            
            _userDefinedVariables.Add(guid, name);
            CyanTriggerAssemblyDataType var = new CyanTriggerAssemblyDataType(name, type, GetResolvedType(type), export);
            var.Sync = sync;
            var.HasCallback = hasCallback;
            var.Guid = guid;

            _variables.Add(var.Name, var);
            _orderedVariables.Add(var.Name);

            if (hasCallback)
            {
                var.SetPreviousVariable(AddPreviousVariable(name, type, export));
            }

            return var;
        }

        public void SetVariableGuid(CyanTriggerAssemblyDataType variable, string guid)
        {
#if CYAN_TRIGGER_DEBUG
            if (!string.IsNullOrEmpty(variable.Guid) && variable.Guid != guid)
            {
                Debug.LogWarning($"Updating variable guid! {variable.Name} - {guid}");
            }
#endif
            variable.Guid = guid;
            _userDefinedVariables[guid] = variable.Name;
        }
        
        public CyanTriggerAssemblyDataType AddPreviousVariable(string varName, Type type, bool export)
        {
            string name = CyanTriggerCustomNodeOnVariableChanged.GetOldVariableName(varName);
            if (_variables.ContainsKey(name))
            {
                return _variables[name];
            }

            // Note that the previous variable should be exported as this is how the initial data is properly set.
            CyanTriggerAssemblyDataType var = new CyanTriggerAssemblyDataType(name, type, GetResolvedType(type), export);
            
            _variables.Add(var.Name, var);
            _orderedVariables.Add(var.Name);
            return var;
        }

        public void RemoveUserDefinedVariable(string guid)
        {
            if (!_userDefinedVariables.TryGetValue(guid, out _))
            {
                return;
            }

            _userDefinedVariables.Remove(guid);
            // Don't actually remove the variable from the variable list
            //_variables.Remove(variableName);
        }

        public void RemoveVariable(string variableName)
        {
            _variables.Remove(variableName);
            _orderedVariables.Remove(variableName);
        }

        public CyanTriggerAssemblyDataType CreateReferenceVariable(Type type)
        {
            if (type.IsByRef)
            {
                type = type.GetElementType();
            }
            
            if (type == typeof(CyanTrigger))
            {
                type = typeof(UdonBehaviour);
            }
            else if (type == typeof(IUdonEventReceiver))
            {
                type = typeof(UdonBehaviour);
            }
            
            return AddVariable("ref", type, true);
        }

        private string GetResolvedType(Type type)
        {
            // TODO find a better way here... UdonGameObjectComponentHeapReference
            return typeof(UdonBehaviour) == type ? "VRCUdonUdonBehaviour" : CyanTriggerDefinitionResolver.GetTypeSignature(type);
        }


        public CyanTriggerAssemblyDataType GetOrCreateVariableConstant(Type type, object value, bool export = false)
        {
            if (type.IsByRef)
            {
                type = type.GetElementType();
            }
            
            if (type.IsSubclassOf(typeof(UnityEngine.Object)) || type == typeof(UnityEngine.Object))
            {
                throw new Exception($"Cannot create const {type.Name} variable as it is a unity object!");
            }
            if (value == null)
            {
                throw new Exception($"Cannot create const {type.Name} variable for null!");
            }
            
            
            if (!_variableConstants.TryGetValue(type, out Dictionary<object, CyanTriggerAssemblyDataType> objs))
            {
                objs = new Dictionary<object, CyanTriggerAssemblyDataType>();
                _variableConstants.Add(type, objs);
            }
            
            // TODO force initialize
            

            if (!objs.TryGetValue(value, out CyanTriggerAssemblyDataType variable))
            {
                variable = AddVariable("const", type, export, value);
                objs.Add(value, variable);
            }

            return objs[value];
        }

        public bool TryGetVariableNamed(string name, out CyanTriggerAssemblyDataType variable)
        {
            return _variables.TryGetValue(name, out variable);
        }
        
        public CyanTriggerAssemblyDataType GetVariableNamed(string name)
        {
            if (_variables.TryGetValue(name, out CyanTriggerAssemblyDataType variable))
            {
                return variable;
            }
            
#if CYAN_TRIGGER_DEBUG
            Debug.LogError($"[AssemblyData] Failed to find variable with name \"{name}\"");
#endif
            return null;
        }

        public CyanTriggerAssemblyDataType GetThisConst(Type type, string name = null)
        {
            if (!string.IsNullOrEmpty(name) && _thisConstsById.TryGetValue(name, out var variable))
            {
                return variable;
            }
            
            // TODO generic way to convert types.
            if (type == typeof(CyanTrigger))
            {
                type = typeof(UdonBehaviour);
            }
            else if (type == typeof(IUdonEventReceiver))
            {
                type = typeof(UdonBehaviour);
            }
            
            if (_thisConstsByType.TryGetValue(type, out variable) && name != null)
            {
                return variable;
            }

            if (variable == null)
            {
                Debug.LogError($"Could not find const. Type: {type}, Name: {name}");
            }
            return variable;
        }
        
        public CyanTriggerAssemblyDataType GetUserDefinedVariable(string guid)
        {
            CyanTriggerAssemblyDataType var;
            if (!_userDefinedVariables.TryGetValue(guid, out string varName))
            {
                // Try using guid as variable name directly
                if (_variables.TryGetValue(guid, out var))
                {
                    return var;
                }
                
                // Try get variable name from guid tag
                varName = CyanTriggerAssemblyDataGuidTags.GetVariableName(guid);
                if (!string.IsNullOrEmpty(varName) && _variables.TryGetValue(varName, out var))
                {
                    return var;
                }
                
                Debug.LogError($"GUID does not exist in user defined variables! {guid}");
                return null;
            }

            if (!_variables.TryGetValue(varName, out var))
            {
                Debug.LogError($"User variable name is not a defined variable! {varName}");
                return null;
            }

            return var;
        }

        public CyanTriggerAssemblyDataType RequestTempVariable(Type type)
        {
            if (type == typeof(CyanTrigger))
            {
                type = typeof(UdonBehaviour);
            }
            else if (type == typeof(IUdonEventReceiver))
            {
                type = typeof(UdonBehaviour);
            }
            
            if (!_tempVariables.TryGetValue(type, out Queue<CyanTriggerAssemblyDataType> tempQueue) 
                || tempQueue.Count == 0)
            {
                return AddVariable("temp", type, false);
            }

            return tempQueue.Dequeue();
        }

        public void ReleaseTempVariable(CyanTriggerAssemblyDataType var)
        {
            if (!_tempVariables.TryGetValue(var.Type, out Queue<CyanTriggerAssemblyDataType> tempQueue))
            {
                tempQueue = new Queue<CyanTriggerAssemblyDataType>();
                _tempVariables.Add(var.Type, tempQueue);
            }

            tempQueue.Enqueue(var);
        }

        public CyanTriggerAssemblyDataType GetSpecialVariable(CyanTriggerSpecialVariableName udonSpecialVariableName)
        {
            (string, Type) varPair = UdonSpecialVariableNames[(int)udonSpecialVariableName];
            if (!ContainsName(varPair.Item1))
            {
                var variable = AddNamedVariable(varPair.Item1, varPair.Item2);
                
                // TODO do this in a more generic way...
                if (udonSpecialVariableName == CyanTriggerSpecialVariableName.TimerQueue)
                {
                    variable.Export = true;
                }
            }

            return _variables[varPair.Item1];
        }

        public static string GetSpecialVariableName(CyanTriggerSpecialVariableName udonSpecialVariableName)
        {
            return UdonSpecialVariableNames[(int)udonSpecialVariableName].Item1;
        }
        
        public static bool IsSpecialCustomActionVariableName(string name)
        {
            return SpecialCustomActionVariableNames.Contains(name);
        }

        public static IEnumerable<CyanTriggerSpecialVariableName> GetSpecialCustomActionVariables()
        {
            int start = (int)CyanTriggerSpecialVariableName.ActionJumpAddress;
            for (var index = start; index < UdonSpecialVariableNames.Length; ++index)
            {
                yield return (CyanTriggerSpecialVariableName)index;
            }
        }

        public bool ContainsSpecialVariable(CyanTriggerSpecialVariableName udonSpecialVariableName)
        {
            (string, Type) varPair = UdonSpecialVariableNames[(int)udonSpecialVariableName];
            return ContainsName(varPair.Item1);
        }

        public bool ContainsName(string name)
        {
            return _variables.ContainsKey(name);
        }
        
        public CyanTriggerAssemblyDataType CreateMethodReturnVar(CyanTriggerAssemblyInstruction afterInstruction)
        {
            CyanTriggerAssemblyDataType var = AddVariable($"{JumpReturnVariableName}{_jumpReturnVariables.Count}", typeof(uint), false);
            AddJumpReturnVariable(afterInstruction, var);
            return var;
        }
        
        public void AddJumpReturnVariable(CyanTriggerAssemblyInstruction afterInstruction, CyanTriggerAssemblyDataType var)
        {
            _jumpReturnVariables.Add((var, afterInstruction));
        }

        public void FinalizeJumpVariableAddresses()
        {
            foreach (var jumpReturn in _jumpReturnVariables)
            {
                jumpReturn.Item1.DefaultValue = jumpReturn.Item2.GetAddressAfterInstruction();
            }
        }

        public string Export()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(".data_start");

            foreach (var variableName in _orderedVariables)
            {
                var variable = _variables[variableName];
                if (variable.Export)
                {
                    sb.AppendLine($"  .export {variable.Name}");
                }
                if (variable.Sync != CyanTriggerVariableSyncMode.NotSynced)
                {
                    sb.AppendLine($"  .sync {variable.Name}, {GetSyncExportName(variable.Sync)}");
                }
            }

            foreach (var variable in _variables)
            {
                sb.AppendLine($"  {variable.Value}");
            }

            sb.AppendLine(".data_end");

            return sb.ToString();
        }
        
        public static string GetSyncExportName(CyanTriggerVariableSyncMode sync)
        {
            switch(sync)
            {
                case CyanTriggerVariableSyncMode.NotSynced:
                case CyanTriggerVariableSyncMode.Synced:
                    return "none";
                case CyanTriggerVariableSyncMode.SyncedLinear:
                    return "linear";
                case CyanTriggerVariableSyncMode.SyncedSmooth:
                    return "smooth";
                default:
                    throw new Exception($"Unexpected SyncMode {sync}");
            }
        }
        
        public static bool IsSpecialType(Type type)
        {
            return type == typeof(GameObject) || 
                   type == typeof(Transform) || 
                   type == typeof(UdonBehaviour) ||
                   type == typeof(IUdonEventReceiver) || 
                   type == typeof(UdonGameObjectComponentHeapReference);
        }

        public void SetVariableDefault(string name, object defaultValue)
        {
            _variables[name].DefaultValue = defaultValue;
        }

        public void ApplyAddresses()
        {
            uint address = 0;
            foreach (var variable in _variables)
            {
                CyanTriggerAssemblyDataType var = variable.Value;
                var.Address = address;
                address += 4;
            }
        }

        public Dictionary<string, (object value, Type type)> GetHeapDefaultValues()
        {
            Dictionary<string, (object value, Type type)> heapDefaultValues = new Dictionary<string, (object value, Type type)>();
            foreach (var variable in _variables)
            {
                CyanTriggerAssemblyDataType var = variable.Value;
                // Skip over all variables that are not exported and have an empty default value.
                if (!var.Export && var.DefaultValue == null)
                {
                    continue;
                }
                heapDefaultValues.Add(var.Name, (var.DefaultValue, var.Type));
            }
            
            return heapDefaultValues;
        }

        private static readonly List<CyanTriggerAssemblyDataType> EmptyVariablesList = 
            new List<CyanTriggerAssemblyDataType>();
        public List<CyanTriggerAssemblyDataType> GetEventVariables(string eventName)
        {
            if (!EventsToEventVariables.TryGetValue(eventName, out List<(string, Type)> eventVariablePairs))
            {
                return EmptyVariablesList;
            }
            
            List<CyanTriggerAssemblyDataType> eventVariables = new List<CyanTriggerAssemblyDataType>();
            foreach (var eventVariable in eventVariablePairs)
            {
                if (!TryGetVariableNamed(eventVariable.Item1, out CyanTriggerAssemblyDataType variable))
                {
                    variable = AddNamedVariable(eventVariable.Item1, eventVariable.Item2);
                }
                eventVariables.Add(variable);
            }

            return eventVariables;
        }

        private static string PrefixVariableName(string prefix, CyanTriggerAssemblyDataType variable, int index)
        {
            if (variable.IsPrevVar)
            {
                throw new Exception("Cannot rename prev var");
            }

            string sanitizedTypeName = CyanTriggerNameHelpers.GetSanitizedTypeName(variable.Type);

            // Hack to force custom actions to update jump locations for this variable.
            if (variable.Name.Contains(JumpReturnVariableName))
            {
                return  $"{prefix}_{index}_{JumpReturnVariableName}{sanitizedTypeName}";
            }
            
            return $"{prefix}_{index}_{sanitizedTypeName}";
        }
        
        public CyanTriggerItemTranslation[] AddPrefixToAllVariables(string prefix)
        {
            List<CyanTriggerItemTranslation> translations = new List<CyanTriggerItemTranslation>();
            
            List<CyanTriggerAssemblyDataType> allOrderedVariables = new List<CyanTriggerAssemblyDataType>();
            List<CyanTriggerAssemblyDataType> variablesToRename = new List<CyanTriggerAssemblyDataType>();
            List<CyanTriggerAssemblyDataType> hasPrevVars = new List<CyanTriggerAssemblyDataType>();
            
            // Collect all variables in order before clearing the list. Clearing is needed as renaming needs to ensure
            // that no variable of the same name already exists in the dictionary that we haven't renamed yet.
            foreach (var variableName in _orderedVariables)
            {
                var variable = _variables[variableName];
                allOrderedVariables.Add(variable);
                
                // Do not try to rename special variables
                if ((SpecialVariableNames.Contains(variableName) &&
                     !SpecialCustomActionVariableNames.Contains(variableName))
                    || IsVarNameCtInternal(variableName))
                {
                    continue;
                }
                
                variablesToRename.Add(variable);
                _variables.Remove(variableName);
            }
            _orderedVariables.Clear();
            _variables.Clear();
            
            // Go through and rename all non prev variables. 
            for (var index = 0; index < variablesToRename.Count; ++index)
            {
                var variable = variablesToRename[index];
                
                if (variable.PreviousVariable != null)
                {
                    hasPrevVars.Add(variable);
                }
                if (variable.IsPrevVar)
                {
                    continue;
                }
                
                string prevName = variable.Name;
                RenameVariable(PrefixVariableName(prefix, variable, index), variable);
                translations.Add(new CyanTriggerItemTranslation
                    { BaseName = prevName, TranslatedName = variable.Name });
            }

            // Rename all prev variables
            foreach (CyanTriggerAssemblyDataType variable in hasPrevVars)
            {
                var prevVar = variable.PreviousVariable;
                string prevName = prevVar.Name;
                string newPrevName = UdonGraphCompiler.GetOldVariableName(variable.Name);
                RenameVariable(newPrevName, prevVar);
                translations.Add(new CyanTriggerItemTranslation { BaseName = prevName, TranslatedName = prevVar.Name });
            }
            
            // Put the variables back in the original added order.
            _variables.Clear();
            _orderedVariables.Clear();
            foreach (var variable in allOrderedVariables)
            {
                _variables.Add(variable.Name, variable);
                _orderedVariables.Add(variable.Name);
            }

            return translations.ToArray();
        }

        public bool RenameVariable(string newName, CyanTriggerAssemblyDataType variable)
        {
            if (SpecialVariableNames.Contains(variable.Name) && 
                !SpecialCustomActionVariableNames.Contains(variable.Name))
            {
                return false;
            }

            _variables.Remove(variable.Name);
            _orderedVariables.Remove(variable.Name);
            variable.Name = newName;
            _variables.Add(variable.Name, variable);
            _orderedVariables.Add(variable.Name);

            return true;
        }
        
        public CyanTriggerAssemblyData Clone(
            Dictionary<CyanTriggerAssemblyDataType, CyanTriggerAssemblyDataType> variableMapping)
        {
            CyanTriggerAssemblyData data = new CyanTriggerAssemblyData();

            foreach (var variable in _userDefinedVariables)
            {
                data._userDefinedVariables.Add(variable.Key, variable.Value);
            }

            List<CyanTriggerAssemblyDataType> withPrevVars = new List<CyanTriggerAssemblyDataType>();
            
            foreach (var variablePair in _variables)
            {
                var clone = variablePair.Value.Clone();
                data._variables.Add(variablePair.Key, clone);
                data._orderedVariables.Add(variablePair.Key);
                
                variableMapping.Add(variablePair.Value, clone);

                if (clone.PreviousVariable != null)
                {
                    withPrevVars.Add(clone);
                }
            }

            foreach (var variable in withPrevVars)
            {
                variable.SetPreviousVariable(variableMapping[variable.PreviousVariable]);
            }
            
            foreach (var type in _variableConstants)
            {
                Dictionary<object, CyanTriggerAssemblyDataType> dict =
                    new Dictionary<object, CyanTriggerAssemblyDataType>();
                data._variableConstants.Add(type.Key, dict);
                foreach (var obj in type.Value)
                {
                    dict.Add(obj.Key, data._variables[obj.Value.Name]);
                }
            }
            
            foreach (var temp in _tempVariables)
            {
                Queue<CyanTriggerAssemblyDataType> queue = new Queue<CyanTriggerAssemblyDataType>();
                data._tempVariables.Add(temp.Key, queue);
                foreach (var variable in temp.Value)
                {
                    queue.Enqueue(data._variables[variable.Name]);
                }
            }

            foreach (var variablePair in _jumpReturnVariables)
            {
                data._jumpReturnVariables.Add((data._variables[variablePair.Item1.Name], variablePair.Item2));
            }

            return data;
        }

        public void UpdateJumpInstructions(
            Dictionary<CyanTriggerAssemblyInstruction, CyanTriggerAssemblyInstruction> mapping)
        {
            for (int cur = 0; cur < _jumpReturnVariables.Count; ++cur)
            {
                var pair = _jumpReturnVariables[cur];
                _jumpReturnVariables[cur] = (pair.Item1, mapping[pair.Item2]);
            }
        }
    }

    public static class CyanTriggerAssemblyDataGuidTags
    {
        public const string VariableNameTag = "VariableName";
        public const string VariableIdTag = "VariableId";

        // This is stupidly hacky.
        private const char GuidTagSeparator = ',';
        private const char GuidTagDataSeparator = ':';

        public static string AddVariableGuidTag(string tag, string data, string guid = null)
        {
            string nTag = $"{tag}{GuidTagDataSeparator}{data}";
            if (string.IsNullOrEmpty(guid))
            {
                return nTag;
            }

            return $"{guid}{GuidTagSeparator}{nTag}";
        }

        // TODO optimize?
        public static string GetVariableGuidTag(string guid, string tag)
        {
            foreach (var tagPair in guid.Split(GuidTagSeparator))
            {
                if (tagPair.StartsWith($"{tag}{GuidTagDataSeparator}"))
                {
                    return tagPair.Substring(tag.Length + 1);
                }
            }

            return null;
        }

        public static string AddVariableIdTag(string id, string guid = null)
        {
            return AddVariableGuidTag(VariableIdTag, id, guid);
        }

        public static string GetVariableId(string guid)
        {
            return GetVariableGuidTag(guid, VariableIdTag);
        }
        
        public static string AddVariableNameTag(string name, string guid = null)
        {
            return AddVariableGuidTag(VariableNameTag, name, guid);
        }

        public static string GetVariableName(string guid)
        {
            return GetVariableGuidTag(guid, VariableNameTag);
        }
    }
}
