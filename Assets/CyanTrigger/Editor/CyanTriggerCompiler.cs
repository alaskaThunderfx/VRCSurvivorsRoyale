using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.EditorBindings;
using VRC.Udon.UAssembly.Assembler;
using VRC.Udon.UAssembly.Interfaces;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCompiler
    {
        // TODO replace with Compile context to know what initiated compiling
        // - Scene trigger before play
        // - Asset trigger
        public static bool DebugCompile = false;
        
        private const string CustomActionNamespacePrefix = "__ag_";
        private const string CustomActionInstanceNamespacePrefix = "__agi_";

        private static readonly MethodInfo BuildTypeResolverGroupMethod =
            typeof(UdonEditorInterface).GetMethod("BuildTypeResolverGroup",
                BindingFlags.Static | BindingFlags.NonPublic);
        private static IUAssemblyTypeResolver buildTypeResolverGroup;
        private static CyanTriggerHeapFactory heapFactory;
        private static IUAssemblyAssembler assembler;
        
        private static readonly Dictionary<CyanTriggerActionGroupDefinition, CyanTriggerAssemblyProgram>
            ActionGroupsToPrograms = new Dictionary<CyanTriggerActionGroupDefinition, CyanTriggerAssemblyProgram>();

        private readonly CyanTriggerDataInstance _cyanTriggerDataInstance;
        private readonly string _triggerHash;
        private readonly bool _isSceneTrigger;
        private readonly bool _debugCompile;
        
        private readonly CyanTriggerAssemblyCode _code;
        private readonly CyanTriggerAssemblyData _data;
        private readonly CyanTriggerAssemblyProgram _program;

        private readonly HashSet<CyanTriggerActionGroupDefinition> _processedActionGroupDefinitions =
            new HashSet<CyanTriggerActionGroupDefinition>();
        private readonly HashSet<CyanTriggerActionGroupDefinition> _processedActionGroupDefinitionsForAutoAdd =
            new HashSet<CyanTriggerActionGroupDefinition>();
        private readonly Dictionary<CyanTriggerActionDefinition, CyanTriggerEventTranslation> _actionDefinitionTranslations =
            new Dictionary<CyanTriggerActionDefinition, CyanTriggerEventTranslation>();

        private readonly Dictionary<CyanTriggerActionGroupDefinition, CyanTriggerCustomActionInstanceTranslation> 
            _instanceActionGroupTranslation = 
                new Dictionary<CyanTriggerActionGroupDefinition, CyanTriggerCustomActionInstanceTranslation>();
        private readonly Dictionary<string, CyanTriggerCustomActionInstanceData> _instanceActionGroupGuidsToInstanceData =
            new Dictionary<string, CyanTriggerCustomActionInstanceData>();
        
        
        private readonly CyanTriggerProgramScopeData _programScopeData = new CyanTriggerProgramScopeData();

        private readonly Dictionary<Vector3Int, CyanTriggerAssemblyDataType> _refVariablesDataCache =
            new Dictionary<Vector3Int, CyanTriggerAssemblyDataType>();

        // Variable Reference data for CyanTriggerDataReferences
        private readonly List<CyanTriggerActionDataReferenceIndex> _actionDataIndices =
            new List<CyanTriggerActionDataReferenceIndex>();
        private readonly Dictionary<string, Type> _publicUserVariables = new Dictionary<string, Type>();

        // TODO add errors and warnings to udon behaviour
        private readonly List<string> _logWarningMessages = new List<string>();
        private readonly List<string> _logErrorMessages = new List<string>();

        private int _curVariable = -1;
        private int _curEvent = -1;
        private int _curAction = -1;

        private readonly CyanTriggerReplayData _replayData;
        private readonly CyanTriggerAssemblyInstruction _autoRequestSerializationNop;

        private bool _shouldNetwork = false;
        
        
        // TODO gather stats and print them. 
        public static Dictionary<string, bool> BatchCompile(
            List<CyanTriggerProgramAsset> programsToCompile,
            Func<CyanTriggerProgramAsset, string> resultHasher)
        {
            if (EditorApplication.isPlaying)
            {
                throw new Exception("Cannot compile CyanTrigger while in playmode.");
            }

            if (programsToCompile.Count == 0)
            {
                return new Dictionary<string, bool>();
            }
            
            ActionGroupsToPrograms.Clear();
            
            Profiler.BeginSample("CyanTriggerCompiler.BatchCompile");
            Profiler.BeginSample("CyanTriggerCompiler.BatchCompile.GetDependencies");
            
            CyanTriggerProgramDependency programDependency = new CyanTriggerProgramDependency(programsToCompile);
            List<CyanTriggerActionGroupDefinition> nonDependentProgramAssets = programDependency.ProcessPrograms();
            bool noCycles = programDependency.GetOrder(out var sortedItems, out var failedToSort);
            
            Profiler.EndSample();

            // Cache CustomActions that will not be compiled. 
            foreach (var actionGroupDefinition in nonDependentProgramAssets)
            {
                CyanTriggerAssemblyProgram program = actionGroupDefinition.GetCyanTriggerAssemblyProgram();
                
                // Don't continue compilation if a custom action is invalid.
                if (program == null)
                {
                    Debug.LogError($"Program is null for action group! {actionGroupDefinition.name}");
                    return null;
                }
                
                ActionGroupsToPrograms.Add(actionGroupDefinition, program);
            }
            
            Profiler.BeginSample("CyanTriggerCompiler.BatchCompile.CompileInOrder");
            
            Dictionary<string, bool> allResults = new Dictionary<string, bool>();
            foreach (var programData in sortedItems)
            {
                var program = programData.Program;
                // Debug.Log($"Processing {program.name}");
                
                string hash = resultHasher(program);
                bool results = program.RehashAndCompile();
                allResults.Add(hash, results);

                // Cache the compiled program.
                var actionGroup = programData.ActionGroupDefinition;
                if (results && actionGroup != null)
                {
                    ActionGroupsToPrograms.Add(actionGroup, actionGroup.GetCyanTriggerAssemblyProgram());
                }
            }
            
            // Check if everything was compiled, otherwise a cycle was detected. 
            if (!noCycles)
            {
                StringBuilder sb = new StringBuilder("Found Cycle in programs to compile: ");
                foreach (var programData in failedToSort)
                {
                    var program = programData.Program;
                    sb.Append($"{program.name}, ");
                    allResults.Add(program.triggerHash, false);
                }
                Debug.LogError(sb.ToString());
            }
            
            Profiler.EndSample();
            Profiler.EndSample();
            
            ActionGroupsToPrograms.Clear();
            
            return allResults;
        }
        

        public static bool CompileCyanTrigger(
            CyanTriggerDataInstance trigger,
            CyanTriggerProgramAsset triggerProgramAsset,
            string triggerHash = "")
        {
            if (EditorApplication.isPlaying)
            {
                throw new Exception("Cannot compile CyanTrigger while in playmode.");
            }
            
            // Don't try to compile the default program.
            if (triggerHash == CyanTriggerSerializedProgramManager.DefaultProgramAssetGuid)
            {
                triggerProgramAsset.SetCompiledData(triggerHash, "", null, null, null, null, null, null);
                return true;
            }
            
            List<string> logWarningMessages = new List<string>();
            List<string> logErrorMessages = new List<string>();
            
            try
            {
                if (trigger == null || trigger.variables == null || trigger.events == null)
                {
                    List<string> errors = new List<string>()
                    {
                        "Failed to compile because trigger, variables, or events were null.",
                    };
                    triggerProgramAsset.SetCompiledData(triggerHash, "", null, null, null, trigger, null, errors);
                    return false;
                }

                if (string.IsNullOrEmpty(triggerHash))
                {
                    triggerHash = CyanTriggerInstanceDataHash.HashCyanTriggerInstanceData(trigger);
                }

                bool isSceneTrigger = !(triggerProgramAsset is CyanTriggerEditableProgramAsset);
                CyanTriggerCompiler compiler = new CyanTriggerCompiler(trigger, triggerHash, isSceneTrigger, DebugCompile);
                
                // Catch errors in progress, but allow previous errors if they are the real cause.
                try
                {
                    compiler.Compile();
                }
                catch (Exception)
                {
                    logWarningMessages.AddRange(compiler._logWarningMessages);
                    logErrorMessages.AddRange(compiler._logErrorMessages);
                    throw;
                }

                if (!compiler.HasErrors())
                {
                    compiler.ApplyProgram(triggerProgramAsset);
                }

                if (compiler.HasErrors())
                {
                    triggerProgramAsset.SetCompiledData(triggerHash, "", null, null, null, trigger, 
                        compiler._logWarningMessages, compiler._logErrorMessages);
                    return false;
                }
                
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);

                if (logErrorMessages.Count > 0)
                {
                    logErrorMessages.Add("-- Please fix the above errors --");
                }
                
                logErrorMessages.Add($"Failed to compile due to error: {e.Message}");
                triggerProgramAsset.SetCompiledData(triggerHash, "", null, null, null, trigger,logWarningMessages, logErrorMessages);
                
                return false;
            }
        }

        private static IUdonProgram AssembleProgram(string programAssembly, uint heapSize)
        {
            if (heapFactory == null)
            {
                heapFactory = new CyanTriggerHeapFactory();
            }
            heapFactory.HeapSize = heapSize;

            if (assembler == null)
            {
                if (buildTypeResolverGroup == null)
                {
                    buildTypeResolverGroup = (IUAssemblyTypeResolver)BuildTypeResolverGroupMethod?.Invoke(null, null);
                }
                assembler = new UAssemblyAssembler(heapFactory, buildTypeResolverGroup);
            }

            return assembler.Assemble(programAssembly);
        }
        
        private CyanTriggerCompiler(CyanTriggerDataInstance trigger, string triggerHash, bool isSceneTrigger, bool debugCompile)
        {
            _cyanTriggerDataInstance = trigger;
            _triggerHash = string.IsNullOrEmpty(triggerHash)
                ? CyanTriggerInstanceDataHash.HashCyanTriggerInstanceData(_cyanTriggerDataInstance)
                : triggerHash;
            _isSceneTrigger = isSceneTrigger;
            _debugCompile = debugCompile;

            _code = new CyanTriggerAssemblyCode(trigger.updateOrder);
            _data = new CyanTriggerAssemblyData();
            _program = new CyanTriggerAssemblyProgram(_code, _data);

            _autoRequestSerializationNop = CyanTriggerAssemblyInstruction.Nop();
            _replayData = new CyanTriggerReplayData(_data);
        }
        
        public void Compile()
        {
            // Always create these variables first.
            AddSpecialVariables();
            
            AddUserDefinedVariables();
            if (HasErrors())
            {
                return;
            }

            AddExtraMethods();

            ProcessAllCustomEventsAndActions();
            if (HasErrors())
            {
                return;
            }
            
            AddCyanTriggerEvents();
            if (HasErrors())
            {
                return;
            }

            AddPostEvents();

            Finish();
        }

        private void Finish()
        {
            _program.Finish();

            CheckShouldBeNetworked();
            ProcessManualWithAutoRequest();
            CheckForSpecialVariableUsage();
            
            foreach (CyanTriggerAssemblyMethod method in _code.GetMethods())
            {
                method?.PushMethodEndReturnJump(_data);
            }

            try
            {
                _program.ApplyAddresses();
            }
            catch (CyanTriggerAssemblyMethod.MissingJumpLabelException e)
            {
                LogError($"Missing Custom Event name: \"{e.MissingLabel}\"");
            }
        }

        private void CheckShouldBeNetworked()
        {
            if (_shouldNetwork)
            {
                return;
            }
            
            // Search if there are any network synced variables
            foreach (var variable in _data.GetVariables())
            {
                if (variable.Sync != CyanTriggerVariableSyncMode.NotSynced)
                {
                    _shouldNetwork = true;
                    return;
                }
            }
            
            // Go through all instructions and see if there are any network related actions.
            string[] networkActions =
            {
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(Networking).GetMethod(nameof(Networking.GetOwner))),
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(Networking).GetMethod(nameof(Networking.IsOwner), new[] { typeof(GameObject) })),
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(Networking).GetMethod(nameof(Networking.IsOwner),
                        new[] { typeof(VRCPlayerApi), typeof(GameObject) })),
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(Networking).GetMethod(nameof(Networking.SetOwner))),
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(VRCPlayerApi).GetMethod(nameof(VRCPlayerApi.IsOwner))),
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(IUdonEventReceiver).GetMethod(nameof(IUdonEventReceiver.SendCustomNetworkEvent))),
            };
            int[] offsets =
            {
                0, 0, 1, 1, 1, 0
            };

            bool maybeNeedsNetwork = false;
            foreach (CyanTriggerAssemblyMethod method in _code.GetMethods())
            {
                for (var curInstr = 0; curInstr < method.Actions.Count; ++curInstr)
                {
                    CyanTriggerAssemblyInstruction instruction = method.Actions[curInstr];
                    if (instruction.GetInstructionType() != CyanTriggerInstructionType.EXTERN)
                    {
                        continue;
                    }

                    string instr = instruction.GetExternSignature();
                    int offset = -1;
                    for (int curNetAction = 0; curNetAction < networkActions.Length; ++curNetAction)
                    {
                        if (instr == networkActions[curNetAction])
                        {
                            offset = offsets[curNetAction];
                            break;
                        }
                    }

                    if (offset == -1)
                    {
                        continue;
                    }

                    maybeNeedsNetwork = true;

                    bool fail = false;
                    List<(CyanTriggerAssemblyInstruction, int)> methodInputs = new List<(CyanTriggerAssemblyInstruction, int)>();
                    CyanTriggerAssemblyProgramUtil.GetInstructionInputs(method.Actions, curInstr, methodInputs, ref fail);
                    if (fail)
                    {
                        continue;
                    }

                    CyanTriggerAssemblyInstruction pushAction = methodInputs[offset].Item1;
                    if (pushAction.GetInstructionType() != CyanTriggerInstructionType.PUSH)
                    {
#if CYAN_TRIGGER_DEBUG
                        Debug.LogWarning($"Invalid instruction type for checking networking: {pushAction.GetInstructionType()}");
#endif
                        continue;
                    }

                    var variable = pushAction.GetVariable();
                    if (variable.Type != typeof(GameObject) && variable.Type != typeof(UdonBehaviour))
                    {
                        continue;
                    }
                    
                    if ((variable.Type != typeof(GameObject) ||
                         variable.Name != CyanTriggerAssemblyDataConsts.ThisGameObject.ID) &&
                        (variable.Type != typeof(UdonBehaviour) ||
                         variable.Name != CyanTriggerAssemblyDataConsts.ThisUdonBehaviour.ID))
                    {
                        continue;
                    }
                    
                    _shouldNetwork = true;
                    return;
                }
            }

            if (maybeNeedsNetwork && !_shouldNetwork)
            {
                // TODO log since had network events but unsure if they target self.
            }
        }

        private void ProcessManualWithAutoRequest()
        {
            // TODO this seems bug prone.
            bool isManualWithRequests = 
                (_cyanTriggerDataInstance.autoSetSyncMode && _shouldNetwork)
                || (!_cyanTriggerDataInstance.autoSetSyncMode
                    && _cyanTriggerDataInstance.programSyncMode == CyanTriggerProgramSyncMode.ManualWithAutoRequest);
            
            if (!isManualWithRequests)
            {
                return;
            }

            // Automatic RequestSerialization
            var requestActions = CyanTriggerAssemblyActionsUtils.RequestSerialization(_program);
            foreach (CyanTriggerAssemblyMethod method in _code.GetMethods())
            {
                bool shouldAddRequestSerialization = false;
                foreach (CyanTriggerAssemblyInstruction instruction in method.Actions)
                {
                    if (instruction == _autoRequestSerializationNop)
                    {
                        shouldAddRequestSerialization = true;
                        break;
                    }
                }

                if (shouldAddRequestSerialization)
                {
                    method.AddActions(requestActions);
                }
            }
        }
        
        // Check if special variables are used. If not, remove the them.
        private void CheckForSpecialVariableUsage()
        {
            int localPlayerIndex = -1;
            List<string> specialVariableIds = new List<string>();
            foreach (var specialVariable in CyanTriggerAssemblyDataConsts.GetConstVariables())
            {
                if (specialVariable.ID == CyanTriggerAssemblyDataConsts.LocalPlayer.ID)
                {
                    localPlayerIndex = specialVariableIds.Count;
                }
                specialVariableIds.Add(specialVariable.ID);
            }
            
            int[] specialVariableUseCounts = new int[specialVariableIds.Count];
            CyanTriggerAssemblyDataType localPlayerInitVariable = null;
            
            // Go through 
            foreach (CyanTriggerAssemblyMethod method in _code.GetMethods())
            {
                int localPlayerCount = specialVariableUseCounts[localPlayerIndex];
                
                foreach (CyanTriggerAssemblyInstruction instruction in method.Actions)
                {
                    if (instruction.GetInstructionType() != CyanTriggerInstructionType.PUSH)
                    {
                        continue;
                    }

                    string variableName = instruction.GetVariableName();
                    for (int index = 0; index < specialVariableUseCounts.Length; ++index)
                    {
                        if (variableName == specialVariableIds[index])
                        {
                            ++specialVariableUseCounts[index];
                            break;
                        }
                    }
                }

                // This method uses the local player. Add initialization to beginning.
                // Ignore custom actions as these should already have been processed when they were compiled.
                if (!IsEventCustomAction(method.Name) && localPlayerCount != specialVariableUseCounts[localPlayerIndex])
                {
                    if (localPlayerInitVariable == null)
                    {
                        localPlayerInitVariable = _data.GetOrCreateUniqueInternalVariable("lp_init", typeof(bool), false, true);
                    }
                    method.AddActionsFirst(CyanTriggerAssemblyActionsUtils
                        .GetLocalPlayerOneTimeInitialization(_program, localPlayerInitVariable));
                }
            }
            
            // Go through and remove any variables that have zero uses.
            for (int index = 0; index < specialVariableUseCounts.Length; ++index)
            {
                if (specialVariableUseCounts[index] == 0)
                {
                    _data.RemoveVariable(specialVariableIds[index]);
                }
            }
        }

        public void ApplyProgram(CyanTriggerProgramAsset programAsset)
        {
            if (programAsset == null)
            {
                LogError("Cannot apply program for empty program asset");
                return;
            }

            // If is editable program, make all reference items private
            if (programAsset is CyanTriggerEditableProgramAsset)
            {
                var programName = 
                    _data.GetSpecialVariable(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ProgramName);
                programName.DefaultValue = programAsset.name;
                
                foreach (var varData in _actionDataIndices)
                {
                    var variable = _data.GetVariableNamed(varData.symbolName);
                    variable.Export = false;
                    // Apply default value from this CyanTrigger data
                    variable.DefaultValue = CyanTriggerDataReferences.GetDataForReferenceIndex(
                        varData, 
                        _cyanTriggerDataInstance?.events, 
                        null, 
                        out Type _);
                }
                _actionDataIndices.Clear();

                if (_cyanTriggerDataInstance != null)
                {
                    foreach (var userVar in _cyanTriggerDataInstance.variables)
                    {
                        if (userVar.IsDisplayOnly()
                            || typeof(ICyanTriggerCustomTypeNoValueEditor).IsAssignableFrom(userVar.type.Type))
                        {
                            continue;
                        }
                        
                        // TODO verify that this works for all cases.
                        var userVariable = _data.GetUserDefinedVariable(userVar.variableID);
                        userVariable.DefaultValue = userVar.data.Obj;
                        if (userVariable.PreviousVariable != null)
                        {
                            userVariable.PreviousVariable.DefaultValue = userVar.data.Obj;
                        }
                    }
                }
            }
            
            var variableReferences = new CyanTriggerDataReferences(_actionDataIndices, _publicUserVariables);

            IUdonProgram compiledUdonProgram = null;
            string assembly = _program.Export();
            try
            {
                // Assemble program here instead of default Udon Assembler to ensure valid heapsize
                // TODO replace this back once VRChat fixes the issue where heap is always 512
                compiledUdonProgram = AssembleProgram(assembly,  _program.GetHeapSize());
            }
            catch (Exception e)
            {
                compiledUdonProgram = null;
                assembly = null;
                _logErrorMessages.Add(e.Message);
                Debug.LogException(e);
            }
            
            programAsset.SetCompiledData(
                _triggerHash, 
                assembly, 
                compiledUdonProgram,
                _data.GetHeapDefaultValues(),
                variableReferences,
                _cyanTriggerDataInstance,
                _logWarningMessages,
                _logErrorMessages,
                _shouldNetwork);
        }

        private string ModifyLogMessage(string message)
        {
            if (_curVariable != -1)
            {
                return $"Variable {_curVariable}: {message}";
            }

            if (_curEvent != -1 && _curAction != -1)
            {
                return $"Event {_curEvent}, Action {_curAction}: {message}";
            }

            if (_curEvent != -1 && _curAction == -1)
            {
                return $"Event {_curEvent}: {message}";
            }

            return message;
        }
        
        // TODO save data along with the error to know how to display it better.
        // Variable index, event index, action index
        private void LogWarning(string warning)
        {
            warning = ModifyLogMessage(warning);
            _logWarningMessages.Add(warning);
        }

        private void LogError(string error)
        {
            error = ModifyLogMessage(error);
            _logErrorMessages.Add(error);
        }

        public bool HasErrors()
        {
            return _logErrorMessages.Count > 0;
        }

        private void AddSpecialVariables()
        {
            _data.CreateProgramNameVariable(_triggerHash);
            _data.CreateSpecialAddressVariables();
            _data.AddThisVariables();
            
            bool anyBroadcasts = false;
            foreach (var evt in _cyanTriggerDataInstance.events)
            {
                // Ensure event variables are added before processing any actions.
                CyanTriggerActionInfoHolder actionInfo =
                    CyanTriggerActionInfoHolder.GetActionInfoHolder(evt.eventInstance.actionType);
                _data.GetEventVariables(actionInfo.GetEventCompiledName(evt));
                
                if (evt.eventOptions.broadcast == CyanTriggerBroadcast.All)
                {
                    anyBroadcasts = true;
                }
            }

            if (anyBroadcasts)
            {
                _data.GetSpecialVariable(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.BroadcastCount);
            }
        }
        
        private void AddUserDefinedVariables()
        {
            HashSet<string> variablesWithCallbacks = CyanTriggerCustomNodeOnVariableChanged
                .GetVariablesWithOnChangedCallback(_cyanTriggerDataInstance.events, out bool allValid);

            if (!allValid)
            {
                throw new Exception("OnVariableChanged event does not have a selected variable!");
            }

            Stack<bool> parentVisibility = new Stack<bool>();
            parentVisibility.Push(true);
            for (var curVar = 0; curVar < _cyanTriggerDataInstance.variables.Length; ++curVar)
            {
                _curVariable = curVar;
                var variable = _cyanTriggerDataInstance.variables[curVar];

                bool visibility = parentVisibility.Peek() && variable.DisplayInInspector();
                if (variable.IsDisplayOnly())
                {
                    if (variable.typeInfo == CyanTriggerVariableType.SectionEnd)
                    {
                        parentVisibility.Pop();
                    }
                    if (variable.typeInfo == CyanTriggerVariableType.SectionStart)
                    {
                        parentVisibility.Push(visibility);
                    }
                    continue;
                }
                
                // TODO better handle custom variable types.
                if (typeof(ICyanTriggerCustomTypeNoValueEditor).IsAssignableFrom(variable.type.Type))
                {
                    continue;
                }
                
                if (string.IsNullOrEmpty(variable.name))
                {
                    LogError("Global Variable with no name!");
                    continue;
                }

                if (variable.name != CyanTriggerNameHelpers.SanitizeName(variable.name))
                {
                    LogError($"Global variable cannot be named \"{variable.name}\"");
                    continue;
                }

                _shouldNetwork |= variable.sync != CyanTriggerVariableSyncMode.NotSynced;

                // Force scene triggers to always be public
                visibility |= _isSceneTrigger;
                bool hasCallback = variablesWithCallbacks.Contains(variable.variableID);
                _data.AddUserDefinedVariable(
                    variable.name, 
                    variable.variableID, 
                    variable.type.Type, 
                    variable.sync,
                    hasCallback,
                    visibility);

                if (visibility)
                {
                    _publicUserVariables.Add(variable.name, variable.type.Type);
                }
            }

            _curVariable = -1;

            // Add CustomEvent parameters as variables and check for duplicate event names to log errors for each.
            var events = _cyanTriggerDataInstance.events;
            Dictionary<string, int> eventNames = new Dictionary<string, int>();
            CyanTriggerActionInfoHolder[] actionInfos = new CyanTriggerActionInfoHolder[events.Length];
            string[] eventNamesOrdered = new string[events.Length];
            
            // Gather all event names and the count for each name to verify if any custom with parameters has duplicate name
            for (var index = 0; index < events.Length; index++)
            {
                var ctEvent = events[index];
                var eventInfo = actionInfos[index] = 
                    CyanTriggerActionInfoHolder.GetActionInfoHolder(ctEvent.eventInstance.actionType);
                string eventName = eventNamesOrdered[index] = eventInfo.GetEventCompiledName(ctEvent);

                eventNames.TryGetValue(eventName, out int count);
                eventNames[eventName] = count + 1;
            }
            
            // Go through each custom with parameters and add the variables to the program
            for (var index = 0; index < _cyanTriggerDataInstance.events.Length; index++)
            {
                var eventInfo = actionInfos[index];
                if (!eventInfo.IsCustomEvent())
                {
                    continue;
                }

                string name = eventNamesOrdered[index];
                eventNames.TryGetValue(name, out int count);
                var parameters = eventInfo.GetCustomEventArgumentOptions(events[index], true);
                
                // Log Error for each event that has a duplicate name.
                if (parameters.Length > 0 && count > 1)
                {
                    _curEvent = index;
                    LogError($"Custom Events with Parameters must have a unique name! \"{name}\"");
                    continue;
                }

                // Dont try to add when there are already errors
                if (HasErrors())
                {
                    continue;
                }
                
                foreach (var variable in parameters)
                {
                    _data.AddUserDefinedVariable(
                        variable.UdonName,
                        variable.ID,
                        variable.Type,
                        CyanTriggerVariableSyncMode.NotSynced,
                        false, 
                        false);
                }
            }

            _curEvent = -1;
        }

        private void AddExtraMethods()
        {
            // Go through and find all replay events
            {
                for (var index = 0; index < _cyanTriggerDataInstance.events.Length; ++index)
                {
                    var trigEvent = _cyanTriggerDataInstance.events[index];
                    var eventOptions = trigEvent.eventOptions;
                    if (eventOptions.broadcast == CyanTriggerBroadcast.All
                        && eventOptions.replay != CyanTriggerReplay.None)
                    {
                        if (!_replayData.ShouldReplay)
                        {
                            // add special values
                            _replayData.CreateInitializedVariables();
                        }
                        
                        _replayData.AddEvent(trigEvent.eventId, eventOptions.replay);
                    }
                }

                if (_replayData.ShouldReplay)
                {
                    _shouldNetwork = true;
                    // OnPreSerialization, ensure that initialization data is set.
                    CyanTriggerAssemblyMethod preSerialization = GetOrAddMethod("_onPreSerialization");
                    var trueConst = _data.GetOrCreateVariableConstant(typeof(bool), true);
                    preSerialization.AddActions(CyanTriggerAssemblyActionsUtils.CopyVariables(
                        trueConst,
                        _replayData.LocalInitialized));
                    preSerialization.AddActions(CyanTriggerAssemblyActionsUtils.CopyVariables(
                        trueConst,
                        _replayData.SyncSetData));
                    
                    // Force request serialization on start.
                    CyanTriggerAssemblyMethod startMethod = GetOrAddMethod("_start");
                    startMethod.AddActions(CyanTriggerAssemblyActionsUtils.ReplayOwnerRequestSerialization(_program));
                    
                    // Force add deserialization to be above all other user methods.
                    GetOrAddMethod("_onDeserialization");
                }
            }
        }

        private void AddPostEvents()
        {
            if (_replayData.ShouldReplay)
            {
                CyanTriggerAssemblyMethod deserializationMethod = GetOrAddMethod("_onDeserialization");
                deserializationMethod.AddActionsFirst(
                    CyanTriggerAssemblyActionsUtils.ReplayAddVariableChecks(_program, _replayData));
            }
        }

        private void ProcessAllCustomEventsAndActions()
        {
            // Go through all variables first and process the action groups defined by them.
            // Save all the action groups to then later go through and auto add all instance actions
            // after variable data per action group has been saved.
            List<CyanTriggerActionGroupDefinition> customVariableActionGroups =
                new List<CyanTriggerActionGroupDefinition>();
            for (var curVar = 0; curVar < _cyanTriggerDataInstance.variables.Length; ++curVar)
            {
                _curVariable = curVar;
                var variable = _cyanTriggerDataInstance.variables[curVar];
                if (!typeof(CyanTriggerCustomTypeCustomAction).IsAssignableFrom(variable.type.Type))
                {
                    continue;
                }

                var value = variable.data.Obj;
                if (!(value is CyanTriggerCustomTypeCustomAction customType))
                {
                    continue;
                }

                var actionGroup = customType.ActionGroup;
                if (actionGroup == null || !actionGroup.isMultiInstance)
                {
                    string actionName = (actionGroup == null ? "null" : actionGroup.name);
                    LogError($"Variable {variable.name} is not a valid multi instance custom action: {actionName}");
                    continue;
                }
                
                ProcessActionDefinition(actionGroup, false);
                if (HasErrors())
                {
                    continue;
                }
                
                var actionGroupTranslation = _instanceActionGroupTranslation[actionGroup];
                List<string> actionVariableNames = actionGroupTranslation.VariableNames;
                List<CyanTriggerAssemblyDataType> instanceVariables = new List<CyanTriggerAssemblyDataType>();
                
                int instanceCount = _instanceActionGroupGuidsToInstanceData.Count;
                string instanceNamePrefix = $"{CustomActionInstanceNamespacePrefix}{instanceCount}";
                var copyToMethod = GetOrAddMethod($"{instanceNamePrefix}_set");
                var copyFromMethod = GetOrAddMethod($"{instanceNamePrefix}_get");
                
                void ProcessInstanceVariable(
                    CyanTriggerAssemblyDataType originalVariable,
                    CyanTriggerAssemblyDataType instanceVariable)
                {
                    // Sync for actionVariable will be removed at the end of all processing.
                    instanceVariable.Sync = originalVariable.Sync;
                    instanceVariable.Export = originalVariable.Export;
                    instanceVariable.DefaultValue = originalVariable.DefaultValue;
                        
                    instanceVariables.Add(instanceVariable);
                    
                    // Add copy methods
                    copyToMethod.AddActions(CyanTriggerAssemblyActionsUtils.CopyVariables(instanceVariable, originalVariable));
                    copyFromMethod.AddActions(CyanTriggerAssemblyActionsUtils.CopyVariables(originalVariable, instanceVariable));
                }
                
                // Create duplicate variables for this instance.
                // Also add copy instructions to copy methods. 
                foreach (var varName in actionVariableNames)
                {
                    var actionVariable = _data.GetVariableNamed(varName);
                    
                    // Ignore previous variables as they will be handled with the main variable.
                    if (actionVariable.IsPrevVar)
                    {
                        continue;
                    }
                    
                    var instanceVar = _data.AddVariable(
                        instanceNamePrefix, 
                        actionVariable.Type,
                        actionVariable.Export, 
                        actionVariable.DefaultValue);
                    ProcessInstanceVariable(actionVariable, instanceVar);

                    // Create prev variable for this variable.
                    if (actionVariable.PreviousVariable != null)
                    {
                        var actionPrevVar = actionVariable.PreviousVariable;
                        string prevVarName = CyanTriggerCustomNodeOnVariableChanged.GetOldVariableName(instanceVar.Name);
                        var instancePrevVar = _data.AddNamedVariable(prevVarName, actionVariable.Type);
                        ProcessInstanceVariable(actionPrevVar, instancePrevVar);
                        instanceVar.SetPreviousVariable(instancePrevVar);
                    }
                }

                // Create a generic method that checks if they have changed in the base program (Handled in processing event)
                // Add in OnDeserialization for each variable:
                // - CopyTo
                // - ChangeCheck
                // - CopyFrom
                if (actionGroupTranslation.SyncedVariableChangedCheck != null)
                {
                    var deserializationMethod = GetOrAddMethod("_onDeserialization");
                    
                    deserializationMethod.AddActions(CyanTriggerAssemblyActionsUtils.JumpToFunction(_program, copyToMethod.Name));
                    deserializationMethod.AddActions(CyanTriggerAssemblyActionsUtils.JumpToFunction(_program, actionGroupTranslation.SyncedVariableChangedCheck.Name));
                    deserializationMethod.AddActions(CyanTriggerAssemblyActionsUtils.JumpToFunction(_program, copyFromMethod.Name));
                }

                // Go through translated variables and check for SendCustomEvent items
                // If any exist:
                // - For each type, modify the event variable for the type (append prefix), push the special variables, and send the event.
                // - Go through all events and create a version that copy the variables, jump to the event, then copies back.
                CyanTriggerAssemblyData.CyanTriggerSpecialVariableName[] specialSendEventVariables =
                {
                    CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ActionInstanceEventDelaySecondsJumpAddress,
                    CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ActionInstanceEventDelayFramesJumpAddress,
                    CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ActionInstanceEventNetworkedJumpAddress,
                    //CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ActionInstanceEventJumpAddress
                };
                string[] specialMethodNamePostfix =
                {
                    "sce_delay_seconds",
                    "sce_delay_frames",
                    "sce_networked",
                    //"sce"
                };
                
                bool proxyEventsAdded = false;
                for (var index = 0; index < specialSendEventVariables.Length; ++index)
                {
                    var specialEventVariable = specialSendEventVariables[index];
                    if (!actionGroupTranslation.SpecialCustomActionVariableTranslations.TryGetValue(
                            specialEventVariable, out string jumpVariableName))
                    {
                        continue;
                    }

                    AddMethods();
                    
                    // Create event handler for this type of SendCustomEvent.
                    var newInstanceMethod = GetOrAddMethod($"{instanceNamePrefix}_{specialMethodNamePostfix[index]}");
                    
                    // Update event name, push variables, call event
                    newInstanceMethod.AddActions(CyanTriggerAssemblyActionsUtils.AddSendEventForCustomActionInstance(
                        _program,
                        instanceNamePrefix,
                        specialEventVariable, 
                        actionGroupTranslation.SpecialCustomActionVariableTranslations));
                    
                    var actionJumpLoc = _data.CreateMethodReturnVar(newInstanceMethod.Actions[0]);
                    var jumpVariable = _data.GetVariableNamed(jumpVariableName);
                    copyToMethod.AddActions(CyanTriggerAssemblyActionsUtils.CopyVariables(actionJumpLoc, jumpVariable));
                }
                
                // Create proxy version of each event for this instance so that the instance variables can be copied before calling the real method.
                void AddMethods()
                {
                    // Only add proxy events once
                    if (proxyEventsAdded)
                    {
                        return;
                    }
                    proxyEventsAdded = true;

                    // Go through each event and make a new one that copies variables, calls the event, then copies back.
                    foreach (var methodName in actionGroupTranslation.MethodNames)
                    {
                        if (methodName == CyanTriggerActionGroupDefinition.EmptyEntryEventName)
                        {
                            continue;
                        }
                        
                        var newInstanceMethod = GetOrAddMethod($"{methodName}{instanceNamePrefix}");
                        newInstanceMethod.AddActions(CyanTriggerAssemblyActionsUtils.JumpToFunction(_program, copyToMethod.Name));
                        newInstanceMethod.AddActions(CyanTriggerAssemblyActionsUtils.JumpToFunction(_program, methodName));
                        newInstanceMethod.AddActions(CyanTriggerAssemblyActionsUtils.JumpToFunction(_program, copyFromMethod.Name));
                    }
                }
                
                
                _instanceActionGroupGuidsToInstanceData[variable.variableID] =
                    new CyanTriggerCustomActionInstanceData
                    {
                        VariableGuid = variable.variableID,
                        InstanceVariables = instanceVariables,
                        ActionGroupDefinition = actionGroup,
                        CopyToMethod = copyToMethod,
                        CopyFromMethod = copyFromMethod,
                    };

                actionGroupTranslation.InstanceVariables.Add(variable.variableID);
                
                customVariableActionGroups.Add(actionGroup);
            }
            _curVariable = -1;
            
            if (HasErrors())
            {
                return;
            }

            var actionGroups = CyanTriggerUtil.GetCustomActionDependencies(_cyanTriggerDataInstance);
            // Merge lists before sorting. Note that duplicates should be auto handled in the respective processing methods.
            actionGroups.AddRange(customVariableActionGroups);
            actionGroups.Sort();

            HashSet<CyanTriggerActionGroupDefinition> processedGroups = new HashSet<CyanTriggerActionGroupDefinition>();
            // Go through all events and actions and merge unique programs in.
            foreach (CyanTriggerActionGroupDefinition actionGroupDefinition in actionGroups)
            {
                ProcessActionDefinition(actionGroupDefinition, false);
                ProcessActionDefinitionAutoAddActions(actionGroupDefinition);
                if (HasErrors())
                {
                    return;
                }
                
                if (!processedGroups.Contains(actionGroupDefinition))
                {
                    processedGroups.Add(actionGroupDefinition);
                    if (actionGroupDefinition.isMultiInstance)
                    {
                        // Ensure instance action groups do not have synced variables as instance variables are synced instead.
                        var actionGroupTranslation = _instanceActionGroupTranslation[actionGroupDefinition];
                        List<string> actionVariableNames = actionGroupTranslation.VariableNames;
                        foreach (var varName in actionVariableNames)
                        {
                            var actionVariable = _data.GetVariableNamed(varName);
                            actionVariable.Sync = CyanTriggerVariableSyncMode.NotSynced;
                        }
                    }
                }
            }
        }

        private CyanTriggerAssemblyMethod GetOrAddMethod(string baseEvent)
        {
            if (_code.GetOrCreateMethod(baseEvent, true, out var method))
            {
                _data.GetEventVariables(baseEvent);
                AddMethod(method);
            }

            return method;
        }
        
        private void AddMethod(CyanTriggerAssemblyMethod method)
        {
            method.PushInitialEndVariable(_data);
            _code.AddMethod(method);
        }

        private void AddCyanTriggerEvents()
        {
            var events = _cyanTriggerDataInstance.events;
            for (int curEvent = 0; curEvent < events.Length; ++curEvent)
            {
                _curEvent = curEvent;
                CyanTriggerEvent trigEvent = events[curEvent];
                CyanTriggerActionInstance eventAction = trigEvent.eventInstance;
                CyanTriggerActionInfoHolder info = CyanTriggerActionInfoHolder.GetActionInfoHolder(eventAction.actionType);
                
                // Log event warnings if not ignored.
                if (!_cyanTriggerDataInstance.ignoreEventWarnings)
                {
                    var warningMessages = info.GetEventWarnings();
                    if (warningMessages != null && warningMessages.Count > 0)
                    {
                        foreach (var message in warningMessages)
                        {
                            LogWarning(message);
                        }
                    }
                }

                if (info.IsCustomEvent())
                {
                    string eventName = trigEvent.name;
                    if (CyanTriggerNodeDefinitionManager.Instance.TryGetDefinitionFromCompiledName(eventName, 
                            out var node)
                        && node.CustomDefinition == null)
                    {
                        LogWarning($"Custom Event will act the same as {node.FullName}");
                    }
                }
                
                string invalidMessage = null;
                var results = CyanTriggerUtil.IsValid(eventAction, _cyanTriggerDataInstance, ref invalidMessage);
                if (results != CyanTriggerErrorType.None)
                {
                    string message = $"{info.GetActionRenderingDisplayName()} is invalid: \"{invalidMessage}\"";
                    if (results == CyanTriggerErrorType.Warning)
                    {
                        LogWarning(message);
                    }
                    else
                    {
                        LogError(message);
                    }
                    
                    continue;
                }
                
                // Add event itself to the scope stack. This way local variables can be added properly
                _programScopeData.SetNewEvent(trigEvent, _program);
                
                // TODO
                // if (!eventAction.active) 
                // {
                //     continue;
                // }

                // Get base action for event
                CyanTriggerAssemblyMethod udonMethod = GetOrCreateMethodForBaseAction(eventAction, trigEvent.name);

                CyanTriggerEventOptions eventOptions = trigEvent.eventOptions;

                // Only add special event gate if it is not anyone gating. 
                CyanTriggerAssemblyMethod gatedMethod = udonMethod;
                if (eventOptions.userGate != CyanTriggerUserGate.Anyone)
                {
                    gatedMethod =
                        new CyanTriggerAssemblyMethod($"__intern_event_{curEvent}_gated", false);
                    AddMethod(gatedMethod);
                
                    // add gate checks
                    udonMethod.AddActions(
                        CyanTriggerAssemblyActionsUtils.EventUserGate(
                            _program, 
                            gatedMethod.Name,
                            eventOptions.userGate, 
                            eventOptions.userGateExtraData));
                }
                
                // Handle creating event specific actions. 
                CyanTriggerAssemblyMethod actionsMethod = CallEventAction(curEvent, eventAction, gatedMethod);

                // For debugging purposes, add a new exported event here just to allow calling from Editor
                if (_isSceneTrigger && _debugCompile)
                {
                    CyanTriggerAssemblyMethod debugMethod = 
                        new CyanTriggerAssemblyMethod(GetDebugEventExecutionName(curEvent), true);
                    AddMethod(debugMethod);
                    
                    actionsMethod.AddActions(CyanTriggerAssemblyActionsUtils.JumpToFunction(_program, debugMethod.Name));
                    actionsMethod = debugMethod;
                }

                bool isReplay = false;
                // Add network call
                if (eventOptions.broadcast != CyanTriggerBroadcast.Local)
                {
                    _shouldNetwork = true;

                    CyanTriggerAssemblyMethod networkedActionsMethod =
                        new CyanTriggerAssemblyMethod($"intern_event_{curEvent}_networked_actions", true);
                    AddMethod(networkedActionsMethod);

                    if (eventOptions.broadcast == CyanTriggerBroadcast.All)
                    {
                        actionsMethod.AddActions(CyanTriggerAssemblyActionsUtils.CheckBroadcastCountAndLogError(_program, udonMethod.Name));
                    }
                    
                    actionsMethod.AddActions(CyanTriggerAssemblyActionsUtils.EventBroadcast(
                        _program,
                        networkedActionsMethod.Name,
                        eventOptions.broadcast));

                    // Replay (buffering) checks
                    if (eventOptions.broadcast == CyanTriggerBroadcast.All 
                        && eventOptions.replay != CyanTriggerReplay.None)
                    {
                        isReplay = true;
                        // In the broadcasted event, check if owner, and increment replay variable.
                        var replayVariable = _replayData.GetEventReplayVariable(trigEvent.eventId);
                        networkedActionsMethod.AddActions(
                            CyanTriggerAssemblyActionsUtils.ReplayUpdateEventCount(_program, replayVariable, _replayData));
                    }
                    
                    actionsMethod = networkedActionsMethod;
                }
                
                // add delay to action method
                if (eventOptions.delay > 0)
                {
                    CyanTriggerAssemblyMethod delayMethod =
                        new CyanTriggerAssemblyMethod($"__intern_event_{curEvent}_delayed_actions", true);
                    AddMethod(delayMethod);
                    
                    actionsMethod.AddActions(CyanTriggerAssemblyActionsUtils.DelayEvent(
                        _program,
                        delayMethod.Name,
                        eventOptions.delay));
                    
                    actionsMethod = delayMethod;
                }
                
                
                // Add check for is broadcast to current action method. 
                // Adding here after delay to ensure that delays are still considered as networked.
                if (eventOptions.broadcast == CyanTriggerBroadcast.All)
                {
                    CyanTriggerAssemblyActionsUtils.AddBroadcastCountToNetworkedEvent(_program, actionsMethod);
                }

                AddCyanTriggerEventsActionsInList(curEvent, trigEvent.actionInstances, actionsMethod);

                // Check if buffered and add jump to this action method
                if (isReplay)
                {
                    _replayData.SetEventMethodName(trigEvent.eventId, actionsMethod.Name);
                }
                
                actionsMethod.PushEndNopAndCreateNew();
            }
            _curEvent = -1;
        }

        private void AddCyanTriggerEventsActionsInList(
            int eventIndex,
            CyanTriggerActionInstance[] actionInstances, 
            CyanTriggerAssemblyMethod actionMethod)
        {
            for (int curAction = 0; curAction < actionInstances.Length; ++curAction)
            {
                _curAction = curAction;
                CyanTriggerActionInstance actionInstance = actionInstances[curAction];
                
                // TODO
                // if (!actionInstance.active) 
                // {
                //     continue;
                // }

                string invalidMessage = null;
                var results = CyanTriggerUtil.IsValid(actionInstance, _cyanTriggerDataInstance, ref invalidMessage);
                if (results != CyanTriggerErrorType.None)
                {
                    CyanTriggerActionInfoHolder info = CyanTriggerActionInfoHolder.GetActionInfoHolder(actionInstance.actionType);
                    string message = $"{info.GetActionRenderingDisplayName()} is invalid: \"{invalidMessage}\"";
                    if (results == CyanTriggerErrorType.Warning)
                    {
                        LogWarning(message);
                    }
                    else
                    {
                        LogError(message);
                    }
                    
                    continue;
                }
                
                CallAction(eventIndex, curAction, actionInstance, actionMethod);
            }
            _curAction = -1;
        }

        private CyanTriggerAssemblyMethod GetOrCreateMethodForBaseAction(CyanTriggerActionInstance action, string customName)
        {
            var actionType = action.actionType;
            if (CyanTriggerNodeDefinitionManager.Instance.TryGetCustomDefinition(actionType.directEvent, out var customDefinition))
            {
                if (customDefinition.GetBaseMethod(_program, action, out var customMethod))
                {
                    AddMethod(customMethod);
                }
                
                return customMethod;
            }
            
            string baseEvent = actionType.directEvent;
            if (!string.IsNullOrEmpty(actionType.guid))
            {
                if (!CyanTriggerActionGroupDefinitionUtil.Instance.TryGetActionDefinition(actionType.guid,
                    out CyanTriggerActionDefinition actionDefinition))
                {
                    LogError($"Action Definition GUID is not valid! {actionType.guid}");
                    return null;
                }

                baseEvent = actionDefinition.baseEventName;
                customName = actionDefinition.eventEntry;
            }
            
            CyanTriggerNodeDefinition nodeDefinition = CyanTriggerNodeDefinitionManager.Instance.GetDefinition(baseEvent);
            if (nodeDefinition == null)
            {
                LogError($"Base event is not a valid event! {baseEvent}");
                return null;
            }

            if (baseEvent == "Event_Custom")
            {
                baseEvent = customName;
                
                if (string.IsNullOrEmpty(customName))
                {
                    LogError("Custom Event with no name!");
                }
                else if (customName != CyanTriggerNameHelpers.SanitizeName(customName))
                {
                    LogError($"Custom Events cannot be named \"{customName}\"");
                }
            }
            else
            {
                baseEvent = $"_{char.ToLower(baseEvent[6])}{baseEvent.Substring(7)}";
            }

            return GetOrAddMethod(baseEvent);
        }

        private CyanTriggerAssemblyMethod CallEventAction(
            int eventIndex,
            CyanTriggerActionInstance actionInstance, 
            CyanTriggerAssemblyMethod eventMethod)
        {
            var actionType = actionInstance.actionType;
            if (!string.IsNullOrEmpty(actionType.directEvent))
            {
                return HandleDirectActionForEvents(eventIndex, actionInstance, eventMethod);
            }
            
            if (!CyanTriggerActionGroupDefinitionUtil.Instance.TryGetActionDefinition(
                actionType.guid, out CyanTriggerActionDefinition actionDefinition))
            {
                LogError($"Action Definition GUID is not valid! {actionType.guid}");
                return eventMethod;
            }
            
            if (!CyanTriggerActionGroupDefinitionUtil.Instance.TryGetActionGroupDefinition(
                    actionDefinition, out CyanTriggerActionGroupDefinition groupDefinition))
            {
                LogError($"Action Group Definition is not valid! {actionType.guid}");
                return eventMethod;
            }

            var actionTranslation = GetActionTranslation(actionType.guid, actionDefinition);
            if (actionTranslation == null)
            {
                LogError($"Action translation is missing! {actionDefinition.GetMethodName()}");
                return eventMethod;
            }
            
            CyanTriggerAssemblyMethod actionsMethod = 
                new CyanTriggerAssemblyMethod($"__intern_event_{eventIndex}_actions", false);
            AddMethod(actionsMethod);
            
            AddEventJumpToActionVariableCopy(eventMethod, actionsMethod, actionTranslation);

            var variableDefinitions = groupDefinition.GetVariablesForAction(actionDefinition);
            CallAction(eventIndex, -1, actionInstance, eventMethod, actionTranslation, variableDefinitions);
            
            // Add output variable copy to the top of the actions method since adding at the end of "CallAction" does not work for events.
            // The event needs to copy the variables before the actions. 
            {
                CheckVariablesChanged(-1, actionInstance, actionsMethod, variableDefinitions, actionTranslation);
            }

            ClearEventJumpToActionVariableCopy(eventMethod, actionTranslation);
            
            return actionsMethod;
        }

        private void CallAction(
            int eventIndex,
            int actionIndex,
            CyanTriggerActionInstance actionInstance,
            CyanTriggerAssemblyMethod actionMethod)
        {
            var actionType = actionInstance.actionType;
            if (!string.IsNullOrEmpty(actionType.directEvent))
            {
                HandleDirectAction(eventIndex, actionIndex, actionInstance, actionMethod);
                return;
            }
            _programScopeData.PreviousScopeDefinition = null;
            
            if (!CyanTriggerActionGroupDefinitionUtil.Instance.TryGetActionDefinition(
                actionType.guid, out CyanTriggerActionDefinition actionDefinition))
            {
                LogError($"Action Definition GUID is not valid! {actionType.guid}");
                return;
            }
            
            if (!CyanTriggerActionGroupDefinitionUtil.Instance.TryGetActionGroupDefinition(
                    actionDefinition, out CyanTriggerActionGroupDefinition groupDefinition))
            {
                LogError($"Action Group Definition is not valid! {actionType.guid}");
                return;
            }
            
            var actionTranslation = GetActionTranslation(actionType.guid, actionDefinition);
            if (actionTranslation == null)
            {
                LogError($"Action translation is missing! {actionDefinition.GetMethodName()}");
                return;
            }
            
            var variableDefinitions = groupDefinition.GetVariablesForAction(actionDefinition);
            CallAction(eventIndex, actionIndex, actionInstance, actionMethod, actionTranslation, variableDefinitions);
        }

        private void AddEventJumpToActionVariableCopy(
            CyanTriggerAssemblyMethod eventMethod,
            CyanTriggerAssemblyMethod actionMethod,
            CyanTriggerEventTranslation actionTranslation)
        {
            // Error is handled in processing.
            if (string.IsNullOrEmpty(actionTranslation.ActionJumpVariableName))
            {
                return;
            }
            
            // Set the action jump method
            var actionJumpLoc = _data.CreateMethodReturnVar(actionMethod.Actions[0]);
            eventMethod.AddActions(CyanTriggerAssemblyActionsUtils.CopyVariables(
                actionJumpLoc,
                _data.GetVariableNamed(actionTranslation.ActionJumpVariableName)));
        }

        private void ClearEventJumpToActionVariableCopy(
            CyanTriggerAssemblyMethod eventMethod,
            CyanTriggerEventTranslation actionTranslation)
        {
            // Error is handled in processing.
            if (string.IsNullOrEmpty(actionTranslation.ActionJumpVariableName))
            {
                return;
            }
            
            // Set the action jump method back to default value.
            var customActionJumpVariable = _data.GetVariableNamed(actionTranslation.ActionJumpVariableName);
            var constInitialValue =
                _data.GetOrCreateVariableConstant(customActionJumpVariable.Type, customActionJumpVariable.DefaultValue);
            
            eventMethod.AddActions(CyanTriggerAssemblyActionsUtils.CopyVariables(
                constInitialValue,
                customActionJumpVariable));
        }

        private CyanTriggerAssemblyMethod HandleDirectActionForEvents(
            int eventIndex,
            CyanTriggerActionInstance actionInstance, 
            CyanTriggerAssemblyMethod eventMethod)
        {
            var actionType = actionInstance.actionType;
            if (CyanTriggerNodeDefinitionManager.Instance.TryGetCustomDefinition(actionType.directEvent, out var customDefinition))
            {
                return customDefinition.AddEventToProgram(new CyanTriggerCompileState
                {
                    Program = _program,
                    ScopeData = _programScopeData,
                    ActionInstance = actionInstance,
                    EventInstance = (eventIndex == -1 ? null : _cyanTriggerDataInstance.events[eventIndex]),
                    ActionMethod = eventMethod,
                    
                    ReplayData = _replayData,
                    
                    GetDataFromVariableInstance = (multiVarIndex, varIndex, variableInstance, type, output) => 
                        GetDataFromVariableInstance(eventIndex, -1, multiVarIndex, varIndex, variableInstance, type, output),
                    
                    CheckVariableChanged = (method, variablesToCheckChanges) => 
                        CheckVariablesChanged(method, variablesToCheckChanges),
                    GetVariableChangedActions = variablesToCheckChanges => CheckVariablesChanged(variablesToCheckChanges),
                    RequestSerializationNop = _autoRequestSerializationNop,
                    
                    LogWarning = LogWarning,
                    LogError = LogError,
                });
            }
            
            return AddDefaultEventToProgram(_program, eventMethod);
        }
        
        public static CyanTriggerAssemblyMethod AddDefaultEventToProgram(
            CyanTriggerAssemblyProgram program,
            CyanTriggerAssemblyMethod eventMethod)
        {
            // Do nothing by default.
            return eventMethod;
        }
        
        private void HandleDirectAction(
            int eventIndex,
            int actionIndex,
            CyanTriggerActionInstance actionInstance,
            CyanTriggerAssemblyMethod actionMethod)
        {
            // Get out variables and add to scope
            _programScopeData.AddVariableOptions(_program, actionInstance);
            
            var actionType = actionInstance.actionType;
            if (CyanTriggerNodeDefinitionManager.Instance.TryGetCustomDefinition(actionType.directEvent, out var customDefinition))
            {
                if (customDefinition is ICyanTriggerCustomNodeScope scopedDefinition)
                {
                    var scopeFrame = new CyanTriggerScopeFrame(scopedDefinition, actionInstance);
                    _programScopeData.ScopeStack.Push(scopeFrame);
                }

                var compileState = new CyanTriggerCompileState
                {
                    Program = _program,
                    ScopeData = _programScopeData,
                    EventInstance = (eventIndex == -1 ? null : _cyanTriggerDataInstance.events[eventIndex]),
                    ActionInstance = actionInstance,
                    ActionMethod = actionMethod,
                    
                    ReplayData = _replayData,

                    GetDataFromVariableInstance = (multiVarIndex, varIndex, variableInstance, type, output) =>
                        GetDataFromVariableInstance(eventIndex, actionIndex, multiVarIndex, varIndex, variableInstance,
                            type, output),
                    
                    CheckVariableChanged = (method, variablesToCheckChanges) => 
                        CheckVariablesChanged(method, variablesToCheckChanges),
                    GetVariableChangedActions = variablesToCheckChanges => CheckVariablesChanged(variablesToCheckChanges),
                    RequestSerializationNop = _autoRequestSerializationNop,
                    
                    LogWarning = LogWarning,
                    LogError = LogError,
                };
                
                customDefinition.AddActionToProgram(compileState);
                
                // End scope, cleanup stack item
                if (customDefinition is CyanTriggerCustomNodeBlockEnd)
                {
                    var lastScope = _programScopeData.ScopeStack.Peek();
                    compileState.ActionInstance = lastScope.ActionInstance;
                    lastScope.Definition.HandleEndScope(compileState);
                    _programScopeData.PopScope(_program);
                    
                    // TODO verify next definition too? Needed for condition to expect condition body
                }
                
                return;
            }
            _programScopeData.PreviousScopeDefinition = null;
            
            CyanTriggerNodeDefinition nodeDef = CyanTriggerNodeDefinitionManager.Instance.GetDefinition(actionType.directEvent);
            if (nodeDef == null)
            {
                LogError($"No definition found for action name: {actionType.directEvent}");
                return;
            }
            
            if (nodeDef.VariableDefinitions.Length > 0 && 
                (nodeDef.VariableDefinitions[0].variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0)
            {
                for (int curInput = 0; curInput < actionInstance.multiInput.Length; ++curInput)
                {
                    HandleDirectActionSingle(eventIndex, actionIndex, curInput, actionInstance, actionMethod, nodeDef.VariableDefinitions);
                }
            }
            else
            {
                HandleDirectActionSingle(eventIndex, actionIndex, -1, actionInstance, actionMethod, nodeDef.VariableDefinitions);
            }
        }

        private void CallAction(
            int eventIndex,
            int actionIndex,
            CyanTriggerActionInstance actionInstance,
            CyanTriggerAssemblyMethod actionMethod,
            CyanTriggerEventTranslation actionTranslation,
            CyanTriggerActionVariableDefinition[] variableDefinitions)
        {
            if (string.IsNullOrEmpty(actionTranslation.TranslatedAction.TranslatedName))
            {
                LogError($"Event[{eventIndex}].Action[{actionIndex}] Translation name is null");
                return;
            }
            
            // Get out variables and add to scope
            _programScopeData.AddVariableOptions(_program, actionInstance);
            
            if (variableDefinitions.Length > 0 && 
                (variableDefinitions[0].variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0)
            {
                for (int curInput = 0; curInput < actionInstance.multiInput.Length; ++curInput)
                {
                    CallActionSingle(eventIndex, actionIndex, curInput, actionInstance, actionMethod, actionTranslation, variableDefinitions);
                }
            }
            else
            {
                CallActionSingle(eventIndex, actionIndex, -1, actionInstance, actionMethod, actionTranslation, variableDefinitions);
            }
        }

        private void HandleDirectActionSingle(
            int eventIndex,
            int actionIndex,
            int multiVarIndex,
            CyanTriggerActionInstance actionInstance,
            CyanTriggerAssemblyMethod actionMethod,
            CyanTriggerActionVariableDefinition[] variableDefinitions)
        {
            var actionType = actionInstance.actionType;
            for (int curVar = 0; curVar < actionInstance.inputs.Length; ++curVar)
            {
                var def = variableDefinitions[curVar];
                var input = (curVar == 0 && multiVarIndex != -1) ?
                    actionInstance.multiInput[multiVarIndex] :
                    actionInstance.inputs[curVar];

                var variable = GetDataFromVariableInstance(
                    eventIndex, 
                    actionIndex, 
                    multiVarIndex, 
                    curVar, 
                    input,
                    def.type.Type,
                    false);
                actionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(variable));
            }

            // TODO Remove now that "Set_" has been created?
            if (actionType.directEvent.StartsWith("Const_"))
            {
                actionMethod.AddAction(CyanTriggerAssemblyInstruction.Copy());
            }
            else
            {
                actionMethod.AddAction(CyanTriggerAssemblyInstruction.CreateExtern(actionType.directEvent));
            }
            
            CheckVariablesChanged(multiVarIndex, actionInstance, actionMethod, variableDefinitions, null);
        }

        private void CallActionSingle(
            int eventIndex,
            int actionIndex,
            int multiVarIndex,
            CyanTriggerActionInstance actionInstance,
            CyanTriggerAssemblyMethod actionMethod,
            CyanTriggerEventTranslation actionTranslation,
            CyanTriggerActionVariableDefinition[] variableDefinitions)
        {
            if (string.IsNullOrEmpty(actionTranslation.TranslatedAction.TranslatedName))
            {
                LogError($"Event[{eventIndex}].Action[{actionIndex}] Translation name is null");
                return;
            }

            CyanTriggerActionGroupDefinitionUtil.Instance.TryGetActionGroupDefinition(
                actionInstance.actionType.guid, out var actionGroupDefinition);
            
            // Check if item is custom action Instance type and copy all variables
            CyanTriggerCustomActionInstanceData instanceVariableData = null;
            if (actionGroupDefinition.isMultiInstance)
            {
                string guid = actionInstance.inputs[0].variableID;
                if (!_instanceActionGroupGuidsToInstanceData.TryGetValue(guid, out instanceVariableData))
                {
                    LogError($"Event[{eventIndex}].Action[{actionIndex}] Invalid Custom Action Instance");
                    return;
                }
                actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.JumpToFunction(_program, instanceVariableData.CopyToMethod.Name));
            }

            bool isVariableSetter = actionTranslation.TranslatedAction.TranslatedName.Equals(
                CyanTriggerActionGroupDefinition.EmptyEntryEventName);

            // Copy event specific variable data
            foreach (var variable in actionTranslation.EventTranslatedVariables)
            {
                CyanTriggerAssemblyDataType srcVariable = _data.GetVariableNamed(variable.BaseName);
                CyanTriggerAssemblyDataType destVariable = _data.GetVariableNamed(variable.TranslatedName);
                actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.CopyVariables(srcVariable, destVariable));
            }

            List<CyanTriggerAssemblyDataType> actionVariables = new List<CyanTriggerAssemblyDataType>();
            
            // Copy user variable data
            for (int curVar = 0; curVar < actionInstance.inputs.Length; ++curVar)
            {
                var def = variableDefinitions[curVar];
                
                // Always skip custom action instance variables.
                if (def.type.Type == typeof(CyanTriggerActionGroupDefinition))
                {
                    continue;
                }

                // Getting a variable from a variable setter should not copy the value first.
                if (isVariableSetter &&
                    (def.variableType & CyanTriggerActionVariableTypeDefinition.VariableOutput) != 0)
                {
                    continue;
                }
                
                var input = (curVar == 0 && multiVarIndex != -1) ?
                    actionInstance.multiInput[multiVarIndex] :
                    actionInstance.inputs[curVar];
                
                CyanTriggerAssemblyDataType srcVariable = GetDataFromVariableInstance(
                    eventIndex, 
                    actionIndex, 
                    multiVarIndex, 
                    curVar, 
                    input,
                    def.type.Type,
                    false);
                
                CyanTriggerAssemblyDataType destVariable =
                    _data.GetVariableNamed(actionTranslation.TranslatedVariables[curVar].TranslatedName);
                actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.CopyVariables(srcVariable, destVariable));
                
                actionVariables.Add(destVariable);
            }

            // Check if any of the input variables had variable change events and call them first before calling the event.
            CheckVariablesChanged(actionMethod, actionVariables);
            
            if (!isVariableSetter)
            {
                // Call method itself
                actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.JumpToFunction(_program,
                    actionTranslation.TranslatedAction.TranslatedName));
            }

            // Only add variable changed checks when not an Event based CustomAction.
            // For events, this is code is executed AFTER the actions and is too late.
            if (actionIndex != -1)
            {
                // Copy saved variables back
                CheckVariablesChanged(multiVarIndex, actionInstance, actionMethod, variableDefinitions, actionTranslation);
            }
            
            // If instance, copy all variables back
            if (actionGroupDefinition.isMultiInstance && instanceVariableData != null)
            {
                actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.JumpToFunction(_program, instanceVariableData.CopyFromMethod.Name));
            }
        }

        private void CheckVariablesChanged(
            int multiVarIndex,
            CyanTriggerActionInstance actionInstance,
            CyanTriggerAssemblyMethod actionMethod,
            CyanTriggerActionVariableDefinition[] variableDefinitions,
            CyanTriggerEventTranslation actionTranslation = null)
        {
            List<CyanTriggerAssemblyDataType> variablesToCheckChanges = new List<CyanTriggerAssemblyDataType>();
            List<CyanTriggerAssemblyDataType> translationVariables = new List<CyanTriggerAssemblyDataType>();
            for (int curVar = 0; curVar < actionInstance.inputs.Length; ++curVar)
            {
                var def = variableDefinitions[curVar];
                
                // Always skip custom action instance variables.
                if (def.type.Type == typeof(CyanTriggerActionGroupDefinition))
                {
                    continue;
                }
                
                if ((def.variableType & CyanTriggerActionVariableTypeDefinition.VariableOutput) == 0)
                {
                    continue;
                }

                var input = (curVar == 0 && multiVarIndex != -1) ?
                    actionInstance.multiInput[multiVarIndex] :
                    actionInstance.inputs[curVar];
                
                CyanTriggerAssemblyDataType dstVariable = GetOutputDataFromVariableInstance(_data, input);
                if (dstVariable == null)
                {
                    continue;
                }
                
                variablesToCheckChanges.Add(dstVariable);
                
                if (actionTranslation != null)
                {
                    CyanTriggerAssemblyDataType srcVariable =
                        _data.GetVariableNamed(actionTranslation.TranslatedVariables[curVar].TranslatedName);
                    translationVariables.Add(srcVariable);
                }
            }

            CheckVariablesChanged(actionMethod, variablesToCheckChanges, translationVariables);
        }

        private void CheckVariablesChanged(
            CyanTriggerAssemblyMethod actionMethod,
            List<CyanTriggerAssemblyDataType> variablesToCheckChanges,
            List<CyanTriggerAssemblyDataType> translationVariables = null)
        {
            actionMethod.AddActions(CheckVariablesChanged(variablesToCheckChanges, translationVariables));
        }

        private List<CyanTriggerAssemblyInstruction> CheckVariablesChanged(
            List<CyanTriggerAssemblyDataType> variablesToCheckChanges,
            List<CyanTriggerAssemblyDataType> translationVariables = null)
        {
            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();
            
            // Ensure copy actions happen before variable changed checks
            if (translationVariables != null && translationVariables.Count == variablesToCheckChanges.Count)
            {
                for (int cur = 0; cur < translationVariables.Count; ++cur)
                {
                    var srcVariable = translationVariables[cur];
                    var dstVariable = variablesToCheckChanges[cur];
                    actions.AddRange(CyanTriggerAssemblyActionsUtils.CopyVariables(srcVariable, dstVariable));
                }
            }

            bool isSynced = false;
            foreach (var dstVariable in variablesToCheckChanges)
            {
                if (dstVariable.Sync != CyanTriggerVariableSyncMode.NotSynced)
                {
                    isSynced = true;
                }
                actions.AddRange(CyanTriggerAssemblyActionsUtils.OnVariableChangedCheck(_program, dstVariable));
            }

            if (isSynced)
            {
                // Signal that we want to generate code to request serialization at the end of this method.
                actions.Add(_autoRequestSerializationNop);
            }

            return actions;
        }

        private CyanTriggerAssemblyDataType GetDataFromVariableInstance(
            int eventIndex,
            int actionIndex,
            int multiVarIndex,
            int varIndex,
            CyanTriggerActionVariableInstance input, 
            Type type, 
            bool outputOnly)
        {
            if (outputOnly)
            {
                var variable = GetOutputDataFromVariableInstance(_program.Data, input);
                if (variable == null)
                {
                    variable = _program.Data.RequestTempVariable(type);
                    _program.Data.ReleaseTempVariable(variable);
                }
                return variable;
            }

            if (input.isVariable)
            {
                return GetInputDataFromVariableInstance(_program.Data, input, type);
            }

            // Item is a constant and not a variable.

            // Check if item is auto added constant. In this case, data should be added directly without reference.
            if (eventIndex == -1)
            {
                return _program.Data.GetOrCreateVariableConstant(type, input.data.Obj);
            }
            
            // Create a reference for this variable so that data
            // is not in the code directly, allowing program reuse.
            
            bool isMulti = varIndex == 0 && multiVarIndex != -1;
            Vector3Int cacheIndex = new Vector3Int(eventIndex, actionIndex, varIndex);
            // Do not hash multi vars as those will never repeat.
            // Check cache first before creating a new reference
            if (!isMulti && _refVariablesDataCache.TryGetValue(cacheIndex, out var cachedData))
            {
                return cachedData;
            }
            
            // TODO do not pass in the data here. Ensure that public variables are properly updated in the program asset
            var varData = _program.Data.CreateReferenceVariable(type);

            // Add variable to the list of exported variables.
            _actionDataIndices.Add(new CyanTriggerActionDataReferenceIndex
            {
                eventIndex = eventIndex,
                actionIndex = actionIndex,
                multiVariableIndex = isMulti ? multiVarIndex : -1,
                variableIndex = varIndex,
                symbolName = varData.Name,
                type = new CyanTriggerSerializableType(type)
            });
            
            if (!isMulti)
            {
                _refVariablesDataCache.Add(cacheIndex, varData);
            }
                
            return varData;
        }
        
        public static CyanTriggerAssemblyDataType GetInputDataFromVariableInstance(
            CyanTriggerAssemblyData data,
            CyanTriggerActionVariableInstance input, 
            Type type)
        {
            if (!input.isVariable)
            {
                // Try to minimize the usage of this as this is defined in the program itself...
                return data.GetOrCreateVariableConstant(type, input.data.Obj, false);
            }

            // These methods should automatically verify if the variable exists.
            if (input.variableID != null && CyanTriggerAssemblyData.IsIdThisVariable(input.variableID))
            {
                return data.GetThisConst(type, input.variableID);
            }
            
            if (!string.IsNullOrEmpty(input.variableID))
            {
                return data.GetUserDefinedVariable(input.variableID);
            }

            if (!string.IsNullOrEmpty(input.name))
            {
                return data.GetVariableNamed(input.name);
            }
            
            // Variable is missing. Provide a temporary one to ignore the data.
            var variable = data.RequestTempVariable(type);
            data.ReleaseTempVariable(variable);
            return variable;
        }
        
        private CyanTriggerAssemblyDataType GetOutputDataFromVariableInstance(
            CyanTriggerAssemblyData data,
            CyanTriggerActionVariableInstance input)
        {
            if (!input.isVariable)
            {
                LogWarning("Trying to copy from a constant value");
                return null;
            }
            if (string.IsNullOrEmpty(input.variableID))
            {
                LogWarning("Output Variable is missing");
                return null;
            }

            if (CyanTriggerAssemblyData.IsIdThisVariable(input.variableID))
            {
                LogWarning("Cannot use this with output variables");
                return null;
            }
            
            // This should automatically verify if the variable exists.
            return data.GetUserDefinedVariable(input.variableID);
        }

        private CyanTriggerEventTranslation GetActionTranslation(
            string actionGuid,
            CyanTriggerActionDefinition actionDefinition)
        {
            if (!CyanTriggerActionGroupDefinitionUtil.Instance.TryGetActionGroupDefinition(
                actionGuid, out var actionGroupDefinition))
            {
                return null; 
            }

            ProcessActionDefinition(actionGroupDefinition, true);
            if (!_actionDefinitionTranslations.TryGetValue(actionDefinition, out var actionTranslation))
            {
                return null;
            }

            return actionTranslation;
        }

        public static bool IsEventCustomAction(string eventName)
        {
            return eventName.StartsWith(CustomActionNamespacePrefix) 
                   || eventName.StartsWith($"N{CustomActionNamespacePrefix}")
                   || eventName.StartsWith(CustomActionInstanceNamespacePrefix) ;
        }

        public static string GetDebugEventExecutionName(int eventIndex)
        {
            return $"__intern_event_{eventIndex}_debug_execute";
        }
        
        private void ProcessActionDefinition(CyanTriggerActionGroupDefinition actionGroupDefinition, bool handleAutoAdd)
        {
            if (actionGroupDefinition == null)
            {
                return;
            }

            if (_processedActionGroupDefinitions.Contains(actionGroupDefinition))
            {
                return;
            }

            try
            {
                // Try caching programs.
                if (!ActionGroupsToPrograms.TryGetValue(actionGroupDefinition, out CyanTriggerAssemblyProgram program))
                {
                    program = actionGroupDefinition.GetCyanTriggerAssemblyProgram();
                }
                if (program == null)
                {
                    throw new Exception($"Program is null for action group! {actionGroupDefinition.name}");
                }

                _processedActionGroupDefinitions.Add(actionGroupDefinition);

                CyanTriggerAssemblyProgram actionProgram = program.Clone();
                CyanTriggerAssemblyProgramUtil.ProcessProgramForCyanTriggers(actionProgram, actionGroupDefinition.isMultiInstance);

                string actionPrefix = $"{CustomActionNamespacePrefix}{_actionDefinitionTranslations.Count}";
                CyanTriggerProgramTranslation programTranslation =
                    CyanTriggerAssemblyProgramUtil.AddNamespace(actionProgram, actionPrefix);

                Dictionary<string, CyanTriggerItemTranslation> methodMap =
                    new Dictionary<string, CyanTriggerItemTranslation>();
                Dictionary<string, CyanTriggerItemTranslation> variableMap =
                    new Dictionary<string, CyanTriggerItemTranslation>();

                foreach (var method in programTranslation.TranslatedMethods)
                {
                    methodMap.Add(method.BaseName, method);
                }

                foreach (var variable in programTranslation.TranslatedVariables)
                {
                    variableMap.Add(variable.BaseName, variable);
                }

                _program.MergeProgram(actionProgram);

                var actionJumpVarName = CyanTriggerAssemblyData.GetSpecialVariableName(
                    CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ActionJumpAddress);
                string translatedActionJumpVarName = null;
                if (variableMap.TryGetValue(actionJumpVarName, out var actionJumpTranslation))
                {
                    translatedActionJumpVarName = actionJumpTranslation.TranslatedName;
                }

                bool hasAnyCustomEvents = false;
                foreach (var action in actionGroupDefinition.exposedActions)
                {
                    hasAnyCustomEvents |= action.IsEvent() && !action.autoAdd;
                    
                    CyanTriggerEventTranslation eventTranslation = new CyanTriggerEventTranslation();
                    _actionDefinitionTranslations.Add(action, eventTranslation);

                    if (!methodMap.TryGetValue(action.eventEntry, out eventTranslation.TranslatedAction))
                    {
                        throw new Exception($"Could not find event named \"{action.eventEntry}\"");
                    }
                    
                    var variableDefinitions = actionGroupDefinition.GetVariablesForAction(action);
                    eventTranslation.TranslatedVariables = new CyanTriggerItemTranslation[variableDefinitions.Length];

                    for (int cur = 0; cur < variableDefinitions.Length; ++cur)
                    {
                        var def = variableDefinitions[cur];
                        if (def.type.Type == typeof(CyanTriggerActionGroupDefinition))
                        {
                            continue;
                        }

                        if (!variableMap.TryGetValue(def.udonName, out var variableTranslation))
                        {
                            throw new Exception($"Could not find variable named \"{def.udonName}\"");
                        }
                        
                        eventTranslation.TranslatedVariables[cur] = variableTranslation;

                        var variable = _data.GetVariableNamed(variableTranslation.TranslatedName);
                        variable.IsModified = true;
                        variable.IsGlobalVariable = true;
                    }

                    eventTranslation.ActionJumpVariableName = translatedActionJumpVarName;

                    List<CyanTriggerItemTranslation> eventInputTranslation = new List<CyanTriggerItemTranslation>();
                    var eventVariables = CyanTriggerAssemblyData.GetEventVariableTypes(action.baseEventName);
                    if (eventVariables != null)
                    {
                        foreach (var variable in eventVariables)
                        {
                            eventInputTranslation.Add(variableMap[variable.Item1]);
                        }
                    }

                    eventTranslation.EventTranslatedVariables = eventInputTranslation.ToArray();
                }

                if (hasAnyCustomEvents && string.IsNullOrEmpty(translatedActionJumpVarName))
                {
                    throw new Exception("Custom Action defines events, but there is no blank SendCustomEvent to call actions for that event!");
                }
                
                // Must be after going through actions as actions can update variable modifications.
                // Check if instance type and save action specific variables.
                if (actionGroupDefinition.isMultiInstance)
                {
                    List<string> instanceVariables = new List<string>();
                    List<CyanTriggerAssemblyDataType> syncedCallbackVariables = new List<CyanTriggerAssemblyDataType>();
                    foreach (var variable in programTranslation.TranslatedVariables)
                    {
                        // Skip all special or event local variables.
                        // If a variable is used in multiple events, then we assume it is global and should have an instance version.
                        var variableData = actionProgram.Data.GetVariableNamed(variable.TranslatedName);
                        if (variable.TranslatedName == variable.BaseName 
                            || CyanTriggerAssemblyData.IsSpecialCustomActionVariableName(variable.BaseName)
                            || variable.BaseName.Contains(CyanTriggerAssemblyData.JumpReturnVariableName)
                            || !variableData.IsGlobalVariable)
                        {
                            continue;
                        }

                        instanceVariables.Add(variable.TranslatedName);

                        if (variableData.Sync != CyanTriggerVariableSyncMode.NotSynced && variableData.HasCallback)
                        {
                            syncedCallbackVariables.Add(variableData);
                        }
                    }

                    Dictionary<CyanTriggerAssemblyData.CyanTriggerSpecialVariableName, string> specialVariables =
                        new Dictionary<CyanTriggerAssemblyData.CyanTriggerSpecialVariableName, string>();

                    foreach (var specialVariable in CyanTriggerAssemblyData.GetSpecialCustomActionVariables())
                    {
                        string name = CyanTriggerAssemblyData.GetSpecialVariableName(specialVariable);
                        if (variableMap.TryGetValue(name, out var newName))
                        {
                            specialVariables.Add(specialVariable, newName.TranslatedName);
                        }
                    }

                    // TODO find what things need to be skipped here.
                    List<string> methodNames = new List<string>();
                    foreach (var method in programTranslation.TranslatedMethods)
                    {
                        methodNames.Add(method.TranslatedName);
                    }

                    // If there are any synced variables with OnVariableChange callbacks,
                    // create a new event to check all of them together.
                    CyanTriggerAssemblyMethod varChangedChecks = null;
                    if (syncedCallbackVariables.Count > 0)
                    {
                        varChangedChecks = GetOrAddMethod($"{actionPrefix}_{methodNames.Count}");
                        foreach (var syncedVar in syncedCallbackVariables)
                        {
                            varChangedChecks.AddActions(CyanTriggerAssemblyActionsUtils.OnVariableChangedCheck(_program, syncedVar));
                        }
                    }

                    CyanTriggerCustomActionInstanceTranslation instanceTranslation =
                        new CyanTriggerCustomActionInstanceTranslation
                        {
                            SyncedVariableChangedCheck = varChangedChecks,
                            MethodNames = methodNames,
                            VariableNames = instanceVariables,
                            SpecialCustomActionVariableTranslations = specialVariables,
                            InstanceVariables = new List<string>()
                        };
                    _instanceActionGroupTranslation.Add(actionGroupDefinition, instanceTranslation);
                }

                if (handleAutoAdd)
                {
                    ProcessActionDefinitionAutoAddActions(actionGroupDefinition);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to process custom action: {actionGroupDefinition.name}");
                Debug.LogException(e);
                LogError($"Failed to process custom action: {actionGroupDefinition.name}\n{e.Message}");
                _processedActionGroupDefinitions.Remove(actionGroupDefinition);
                _instanceActionGroupTranslation.Remove(actionGroupDefinition);
            }
        }

        private void ProcessActionDefinitionAutoAddActions(CyanTriggerActionGroupDefinition actionGroupDefinition)
        {
            if (HasErrors())
            {
                return;
            }
            
            if (_processedActionGroupDefinitionsForAutoAdd.Contains(actionGroupDefinition))
            {
                return;
            }
            _processedActionGroupDefinitionsForAutoAdd.Add(actionGroupDefinition);
            
            foreach (var action in actionGroupDefinition.exposedActions)
            {
                if (!action.autoAdd)
                {
                    continue;
                }

                var variableDefinitions = actionGroupDefinition.GetVariablesForAction(action);
                var instInputs = new CyanTriggerActionVariableInstance[variableDefinitions.Length];
                for (int index = 0; index < instInputs.Length; ++index)
                {
                    instInputs[index] = new CyanTriggerActionVariableInstance(variableDefinitions[index].defaultValue);
                }
                
                CyanTriggerActionInstance inst = new CyanTriggerActionInstance
                {
                    actionType = new CyanTriggerActionType{guid = action.guid},
                    inputs = instInputs
                };
                
                var eventTranslation = _actionDefinitionTranslations[action];
                
                var method = GetOrAddMethod(action.eventEntry);
                if (actionGroupDefinition.isMultiInstance)
                {
                    if (!_instanceActionGroupTranslation.TryGetValue(actionGroupDefinition,
                            out var actionGroupTranslation))
                    {
                        LogError($"Custom Action not properly processed! {actionGroupDefinition.name}");
                        return;
                    }
                    
                    foreach (var guid in actionGroupTranslation.InstanceVariables)
                    {
                        instInputs[0].isVariable = true;
                        instInputs[0].variableID = guid;
                        CallActionSingle(-1, -1, -1, inst, method, eventTranslation, variableDefinitions);
                    }
                }
                else
                {
                    CallActionSingle(-1, -1, -1, inst, method, eventTranslation, variableDefinitions);
                }
            }
        }
    }

    public class CyanTriggerScopeFrame
    {
        public CyanTriggerAssemblyInstruction StartNop;
        public CyanTriggerAssemblyInstruction EndNop;
        public readonly ICyanTriggerCustomNodeScope Definition;
        public readonly CyanTriggerActionInstance ActionInstance;
        public readonly bool IsLoop;
        public readonly List<CyanTriggerEditorVariableOption> ScopeVariables = 
            new List<CyanTriggerEditorVariableOption>();

        public CyanTriggerScopeFrame(
            ICyanTriggerCustomNodeScope definition,
            CyanTriggerActionInstance actionInstance)
        {
            Definition = definition;
            ActionInstance = actionInstance;

            IsLoop = definition is ICyanTriggerCustomNodeLoop;
        }

        public void AddVariables(CyanTriggerAssemblyProgram program, CyanTriggerEditorVariableOption[] variableOptions)
        {
            foreach (var variable in variableOptions)
            {
                program.Data.SetVariableGuid(program.Data.GetVariableNamed(variable.Name), variable.ID);
                ScopeVariables.Add(variable);
            }
        }
    }

    public class CyanTriggerProgramScopeData
    {
        public readonly Stack<CyanTriggerScopeFrame> ScopeStack = new Stack<CyanTriggerScopeFrame>();
        public ICyanTriggerCustomNodeScope PreviousScopeDefinition;

        public void SetNewEvent(CyanTriggerEvent triggerEvent, CyanTriggerAssemblyProgram program)
        {
            Clear();

            var eventInstance = triggerEvent.eventInstance;
            var eventFrame = new CyanTriggerScopeFrame(null, eventInstance);
            
            CyanTriggerActionInfoHolder infoHolder = CyanTriggerActionInfoHolder.GetActionInfoHolder(eventInstance.actionType);
            eventFrame.AddVariables(program, infoHolder.GetCustomEventArgumentOptions(triggerEvent, true));
            
            ScopeStack.Push(eventFrame);
        }
        
        public void Clear()
        {
            ScopeStack.Clear();
            PreviousScopeDefinition = null;
        }
        
        public bool VerifyPreviousScope()
        {
            // TODO
            return true;
        }

        public void AddVariableOptions(CyanTriggerAssemblyProgram program, CyanTriggerActionInstance actionInstance)
        {
            CyanTriggerActionInfoHolder info = CyanTriggerActionInfoHolder.GetActionInfoHolder(actionInstance.actionType);
            if (info == null)
            {
                return;
            }
            
            var scopeFrame = ScopeStack.Peek();
            scopeFrame.AddVariables(program, info.GetCustomEditorVariableOptions(program, actionInstance));
        }

        public CyanTriggerScopeFrame PopScope(CyanTriggerAssemblyProgram program)
        {
            var scopeFrame = ScopeStack.Pop();

            foreach (var variable in scopeFrame.ScopeVariables)
            {
                program.Data.RemoveUserDefinedVariable(variable.ID);
            }

            PreviousScopeDefinition = scopeFrame.Definition;
            return scopeFrame;
        }
    }
    
    public class CyanTriggerItemTranslation
    {
        public string BaseName;
        public string TranslatedName;
    }

    public class CyanTriggerEventTranslation
    {
        public string ActionJumpVariableName;
        public CyanTriggerItemTranslation TranslatedAction;
        public CyanTriggerItemTranslation[] TranslatedVariables;
        public CyanTriggerItemTranslation[] EventTranslatedVariables;
    }
    
    public class CyanTriggerProgramTranslation
    {
        public CyanTriggerItemTranslation[] TranslatedMethods;
        public CyanTriggerItemTranslation[] TranslatedVariables;
    }

    public class CyanTriggerCustomActionInstanceTranslation
    {
        public CyanTriggerAssemblyMethod SyncedVariableChangedCheck;
        public List<string> VariableNames;
        public List<string> MethodNames;
        public Dictionary<CyanTriggerAssemblyData.CyanTriggerSpecialVariableName, string>
            SpecialCustomActionVariableTranslations;

        public List<string> InstanceVariables;
    }

    public class CyanTriggerCustomActionInstanceData
    {
        public string VariableGuid;
        public List<CyanTriggerAssemblyDataType> InstanceVariables;
        public CyanTriggerActionGroupDefinition ActionGroupDefinition;
        public CyanTriggerAssemblyMethod CopyToMethod;
        public CyanTriggerAssemblyMethod CopyFromMethod;
    }

    public class CyanTriggerCompileState
    {
        public CyanTriggerAssemblyProgram Program;
        public CyanTriggerProgramScopeData ScopeData;
        public CyanTriggerEvent EventInstance; 
        public CyanTriggerActionInstance ActionInstance;
        public CyanTriggerAssemblyMethod ActionMethod;

        public CyanTriggerReplayData ReplayData;

        // multi index, variable index, variable instance, expected type
        public Func<int, int, CyanTriggerActionVariableInstance, Type, bool, CyanTriggerAssemblyDataType> 
            GetDataFromVariableInstance;
        
        public Action<CyanTriggerAssemblyMethod, List<CyanTriggerAssemblyDataType>> CheckVariableChanged;
        public Func<List<CyanTriggerAssemblyDataType>, List<CyanTriggerAssemblyInstruction>> GetVariableChangedActions;
        public CyanTriggerAssemblyInstruction RequestSerializationNop;

        public Action<string> LogWarning;
        public Action<string> LogError;
    }

    public class CyanTriggerEventReplayData
    {
        // string eventGuid
        public readonly string EventId;
        public readonly CyanTriggerAssemblyDataType Variable;
        public readonly CyanTriggerReplay ReplayType;
        public string MethodName;

        public CyanTriggerEventReplayData(
            string eventId, 
            CyanTriggerAssemblyDataType variable, 
            CyanTriggerReplay replayType)
        {
            EventId = eventId;
            Variable = variable;
            ReplayType = replayType;
        }
    }
    
    public class CyanTriggerReplayData
    {
        public bool ShouldReplay;
        public CyanTriggerAssemblyDataType SyncSetData; // Has data been set by master?
        public CyanTriggerAssemblyDataType LocalInitialized; // Has Replay been executed locally?
        public CyanTriggerAssemblyDataType NotExecutingReplay; // Is replay not currently executing?
        
        public readonly List<CyanTriggerEventReplayData> OrderedData = new List<CyanTriggerEventReplayData>();

        private readonly CyanTriggerAssemblyData _data;
        
        private readonly Dictionary<string, CyanTriggerEventReplayData> _eventIdToVariableAndAction =
            new Dictionary<string, CyanTriggerEventReplayData>();

        public CyanTriggerReplayData(CyanTriggerAssemblyData data)
        {
            _data = data;
        }
        
        public void CreateInitializedVariables()
        {
            ShouldReplay = true;
            SyncSetData = _data.GetOrCreateUniqueInternalVariable("replay_set", typeof(bool), false, false);
            SyncSetData.Sync = CyanTriggerVariableSyncMode.Synced;
            
            LocalInitialized = _data.AddVariable("replay_init", typeof(bool), false, false);
            NotExecutingReplay = _data.AddVariable("replay_ex", typeof(bool), false, true);
        }
        
        public void AddEvent(string eventId, CyanTriggerReplay replayType)
        {
            var variable = _data.AddVariable("replay", typeof(int), false, 0);
            variable.Sync = CyanTriggerVariableSyncMode.Synced;
            
            var replayData = new CyanTriggerEventReplayData(eventId, variable, replayType);
            _eventIdToVariableAndAction.Add(eventId, replayData);
            OrderedData.Add(replayData);
        }

        public CyanTriggerAssemblyDataType GetEventReplayVariable(string eventId)
        {
            if (!_eventIdToVariableAndAction.TryGetValue(eventId, out var replayData))
            {
#if CYAN_TRIGGER_DEBUG
                Debug.LogError($"Could not find event id for Replay. eventId: {eventId}");
#endif
                return null;
            }

            return replayData.Variable;
        }
        
        public void SetEventMethodName(string eventId, string methodName)
        {
            if (!_eventIdToVariableAndAction.TryGetValue(eventId, out var replayData))
            {
#if CYAN_TRIGGER_DEBUG
                Debug.LogError($"Could not find event id for Replay. method: {methodName}, eventId: {eventId}");
#endif
                return;
            }

            replayData.MethodName = methodName;
        }
    }
    
    public class CyanTriggerHeapFactory : IUdonHeapFactory
    {
        public uint HeapSize = 0u;
        
        public IUdonHeap ConstructUdonHeap()
        {
            return new UdonHeap(HeapSize);
        }

        public IUdonHeap ConstructUdonHeap(uint heapSize)
        {
            return new UdonHeap(HeapSize);
        }
    }
}