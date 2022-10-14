using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Compiler.Compilers;
using VRC.Udon.Editor;

namespace Cyan.CT.Editor
{
    public static class CyanTriggerAssemblyProgramUtil
    {

        public static CyanTriggerAssemblyProgram MergePrograms(params CyanTriggerAssemblyProgram[] programs)
        {
            CyanTriggerAssemblyCode code = new CyanTriggerAssemblyCode();
            CyanTriggerAssemblyData data = new CyanTriggerAssemblyData();
            CyanTriggerAssemblyProgram program = new CyanTriggerAssemblyProgram(code, data);

            foreach (var programToMerge in programs)
            {
                program.MergeProgram(programToMerge);
            }

            return program;
        }

        // Given a list of instructions and an index, find the push variable input instructions for the given instruction.
        // This will crawl back to find the push variables and avoid other instructions if interleaved.
        // Note that these will fail in some weirdly formatted assembly cases.
        //  Ex: U# and CT handling of internal method calls will push a variable and then jump.
        //      The push may not be handled properly as it will be used in the jump
        public static int GetInstructionInputs(
            List<CyanTriggerAssemblyInstruction> actions,
            int index, 
            List<(CyanTriggerAssemblyInstruction, int)> inputs,
            ref bool fail)
        {
            CyanTriggerAssemblyInstruction cur = actions[index];

            int inputCount = GetUdonInstructionInputCount(cur);
            if (inputs == null && cur.GetInstructionType().IsJump())
            {
                fail = true;
                return -1;
            }

            --index;
            for (int i = 0; i < inputCount; ++i)
            {
                var maybeInput = actions[index];
                var type = maybeInput.GetInstructionType();
                if (type == CyanTriggerInstructionType.PUSH)
                {
                    inputs?.Add((maybeInput, index));
                    --index;
                    continue;
                }

                index = GetInstructionInputs(actions, index, null, ref fail);
                --i;
                if (fail)
                {
                    return -1;
                }
            }

            inputs?.Reverse();

            return index;
        }

        public static int GetUdonInstructionInputCount(CyanTriggerAssemblyInstruction instruction)
        {
            var type = instruction.GetInstructionType();
            if (type != CyanTriggerInstructionType.EXTERN)
            {
                return CyanTriggerAssemblyInstruction.GetUdonInstructionInputCount(type);
            }
            
            string externMethod = instruction.GetExternSignature();
            var definition = CyanTriggerNodeDefinitionManager.Instance.GetDefinition(externMethod);
            if (definition == null)
            {
                return 0;
            }

            return definition.VariableDefinitions.Length;
        }
        
        
        public static CyanTriggerAssemblyProgram CreateProgram(IUdonProgram udonProgram)
        {
            CyanTriggerAssemblyCode code = new CyanTriggerAssemblyCode();
            CyanTriggerAssemblyData data = new CyanTriggerAssemblyData();
            CyanTriggerAssemblyProgram program = new CyanTriggerAssemblyProgram(code, data);

            Dictionary<uint, string> variableLocs = new Dictionary<uint, string>();
            Dictionary<uint, string> methodLocs = new Dictionary<uint, string>();
            Dictionary<uint, CyanTriggerAssemblyInstruction> instructionLocs = new Dictionary<uint, CyanTriggerAssemblyInstruction>();

            List<(CyanTriggerAssemblyInstruction, CyanTriggerAssemblyInstruction)> unresolvedJumps = new List<(CyanTriggerAssemblyInstruction, CyanTriggerAssemblyInstruction)>();

            var symbolTable = udonProgram.SymbolTable;
            // Get variables
            {
                foreach (var symbol in symbolTable.GetSymbols())
                {
                    uint i = symbolTable.GetAddressFromSymbol(symbol);
                    Type type = symbolTable.GetSymbolType(symbol);

                    CyanTriggerAssemblyDataType variable = data.AddNamedVariable(symbol, type);
                    variable.DefaultValue = udonProgram.Heap.GetHeapVariable(i);

                    variableLocs.Add(i, symbol);
                }

                foreach (var symbol in symbolTable.GetSymbols())
                {
                    string prevVarName = UdonGraphCompiler.GetOldVariableName(symbol);
                    if (data.TryGetVariableNamed(prevVarName, out var prevVar))
                    {
                        data.GetVariableNamed(symbol).SetPreviousVariable(prevVar);
                    }
                }
                
                foreach (var metadata in udonProgram.SyncMetadataTable.GetAllSyncMetadata())
                {
                    var variable = data.GetVariableNamed(metadata.Name);
                    foreach (var prop in metadata.Properties)
                    {
                        switch (prop.InterpolationAlgorithm)
                        {
                            case UdonSyncInterpolationMethod.None:
                                variable.Sync = CyanTriggerVariableSyncMode.Synced;
                                break;
                            case UdonSyncInterpolationMethod.Linear:
                                variable.Sync = CyanTriggerVariableSyncMode.SyncedLinear;
                                break;
                            case UdonSyncInterpolationMethod.Smooth:
                                variable.Sync = CyanTriggerVariableSyncMode.SyncedSmooth;
                                break;
                        }
                    }
                }

                foreach (var exportedVariables in symbolTable.GetExportedSymbols())
                {
                    var variable = data.GetVariableNamed(exportedVariables);
                    variable.IsModified = true;
                    variable.IsGlobalVariable = true;
                }
            }

            // Get assembly
            {
                foreach (var symbol in udonProgram.EntryPoints.GetSymbols())
                {
                    uint i = udonProgram.EntryPoints.GetAddressFromSymbol(symbol);
                    methodLocs.Add(i, symbol);
                }

                string[] assembly = UdonEditorManager.Instance.DisassembleProgram(udonProgram);

                CyanTriggerAssemblyMethod method = null;

                for (int i = 0; i < assembly.Length; ++i)
                {
                    CyanTriggerAssemblyInstruction instruction = null;

                    int index = assembly[i].IndexOf(':');
                    string addressString = assembly[i].Substring(0, index).Trim();
                    uint address = Convert.ToUInt32(addressString, 16);

                    assembly[i] = assembly[i].Substring(index + 2);

                    string[] split = assembly[i].Split(',');
                    if (split[0].StartsWith("PUSH"))
                    {
                        CyanTriggerAssemblyDataType variable = null;
                        uint variableAddress = Convert.ToUInt32(split[1].Trim(), 16);
                        if (variableLocs.TryGetValue(variableAddress, out string varName))
                        {
                            variable = data.GetVariableNamed(varName);
                            Debug.Assert(variable != null, $"Could not find variable named {varName}");
                        }
                        else
                        {
                            Debug.LogError($"unknown variable? {variableAddress}");
                            break;
                        }

                        instruction = CyanTriggerAssemblyInstruction.PushVariable(variable);
                    }
                    else if (split[0].StartsWith("JUMP_INDIRECT"))
                    {
                        string varName = split[1].Trim();
                        CyanTriggerAssemblyDataType variable = data.GetVariableNamed(varName);
                        Debug.Assert(variable != null, $"Could not find variable named {varName}");
                        instruction = CyanTriggerAssemblyInstruction.JumpIndirect(variable);
                    }
                    else if (split[0].StartsWith("JUMP_IF_FALSE") || split[0].Equals("JUMP"))
                    {
                        string add = split[1].Trim();
                        if (add.StartsWith("0x"))
                        {
                            uint methodAddress = Convert.ToUInt32(add, 16);

                            CyanTriggerAssemblyInstruction nop = CyanTriggerAssemblyInstruction.Nop();
                            nop.SetAddress(methodAddress);

                            if (split[0].StartsWith("JUMP_IF_FALSE"))
                            {
                                instruction = CyanTriggerAssemblyInstruction.JumpIfFalse(nop);
                            }
                            else if (split[0].Equals("JUMP"))
                            {
                                instruction = CyanTriggerAssemblyInstruction.Jump(nop);
                            }

                            unresolvedJumps.Add((instruction, nop));
                        }
                        else
                        {
                            Debug.LogError($"unknown jump? {add}");
                            break;
                        }
                    }
                    else if (split[0].StartsWith("COPY"))
                    {
                        instruction = CyanTriggerAssemblyInstruction.Copy();
                    }
                    else if (split[0].StartsWith("NOP"))
                    {
                        instruction = CyanTriggerAssemblyInstruction.Nop();
                    }
                    else if (split[0].StartsWith("EXTERN"))
                    {
                        instruction = CyanTriggerAssemblyInstruction.CreateExtern(split[1].Replace("\"", "").Trim());
                    }
                    else if (split[0].StartsWith("POP"))
                    {
                        instruction = CyanTriggerAssemblyInstruction.Pop();
                    }
                    else
                    {
                        Debug.LogWarning($"Unknown instruction: {assembly[i]}");
                    }

                    // Get new method
                    if (methodLocs.TryGetValue(address, out string methodName))
                    {
                        if (method != null)
                        {
                            code.AddMethod(method);
                        }

                        method = new CyanTriggerAssemblyMethod(methodName, false);
                    }

                    Debug.Assert(instruction != null, $"Did not create instruction for assembly: {assembly[i]}");

                    instruction.SetAddress(address);
                    instructionLocs.Add(address, instruction);
                    method.AddAction(instruction);
                }

                if (method != null)
                {
                    code.AddMethod(method);
                }
                else
                {
                    Debug.LogWarning("No methods after reading program!");
                }
                
                foreach (var exportedMethods in udonProgram.EntryPoints.GetExportedSymbols())
                {
                    code.GetMethod(exportedMethods).Export = true;
                }
            }

            // Update instruction locs based on nops
            foreach (var instructionPair in unresolvedJumps)
            {
                var instruction = instructionPair.Item1;
                var nop = instructionPair.Item2;

                if (instructionLocs.TryGetValue(nop.GetAddress(), out CyanTriggerAssemblyInstruction jumpInstruction))
                {
                    instruction.SetJumpInstruction(jumpInstruction);
                }
                else
                {
                    // Usually this case is when the compiler tries to jump to max instruction.
                    //Debug.LogWarning($"Could not get instruction for address: {nop.GetAddress()}");
                    instruction.SetJumpInstruction(null);
                }
            }

            // Update variables that might be jumps
            {
                foreach (var symbol in symbolTable.GetSymbols())
                {
                    Type type = symbolTable.GetSymbolType(symbol);
                    if (type != typeof(uint) 
                        || (!symbol.Contains("_const_intnl_exitJumpLoc_UInt32") // UdonSharp 0.X
                            && !symbol.StartsWith("__gintnl_SystemUInt32_") // UdonSharp 1.0
                            && !symbol.Contains(CyanTriggerAssemblyData.JumpReturnVariableName)))
                    {
                        continue;
                    }

                    uint variableAddress = symbolTable.GetAddressFromSymbol(symbol);
                    uint jumpDestAddress = (uint)udonProgram.Heap.GetHeapVariable(variableAddress);
                    
                    // If jump location isn't an instruction, skip
                    if (!instructionLocs.TryGetValue(jumpDestAddress, out CyanTriggerAssemblyInstruction _))
                    {
                        continue;
                    }
                    
                    // Check if the skipped instruction is a jump.
                    uint jumpSkipAddress =
                        jumpDestAddress
                        - CyanTriggerAssemblyInstruction.GetUdonInstructionSize(CyanTriggerInstructionType.JUMP);
                    if (!instructionLocs.TryGetValue(jumpSkipAddress, out CyanTriggerAssemblyInstruction jumpSkipInstruction))
                    {
                        continue;
                    }
                    if (jumpSkipInstruction == null ||
                        jumpSkipInstruction.GetInstructionType() != CyanTriggerInstructionType.JUMP)
                    {
                        continue;
                    }
                    
                    // Cannot make any assumptions about the instructions around the jump other than maybe the address
                    // of the variable is before the location of the jump address.

                    CyanTriggerAssemblyDataType variable = data.GetVariableNamed(symbol);
                    data.AddJumpReturnVariable(jumpSkipInstruction, variable);
                }
            }

            // Update "this" variable names to match CyanTrigger
            {
                foreach (var symbol in symbolTable.GetSymbols())
                {
                    // Handling for UdonSharp 0.X
                    // UdonGraph and UdonSharp 1.0 do not use special "this" variables but use
                    // individual references of UdonGameObjectComponentHeapReference
                    if (symbol.Contains("_this_intnl_"))
                    {
                        var variable = data.GetVariableNamed(symbol);
                        var type = variable.Type;
                        if (type == typeof(GameObject))
                        {
                            data.RenameVariable(CyanTriggerAssemblyDataConsts.ThisGameObject.ID, variable);
                        }
                        if (type == typeof(Transform))
                        {
                            data.RenameVariable(CyanTriggerAssemblyDataConsts.ThisTransform.ID, variable);
                        }
                        if (type == typeof(UdonBehaviour))
                        {
                            data.RenameVariable(CyanTriggerAssemblyDataConsts.ThisUdonBehaviour.ID, variable);
                        }
                    }
                }
            }
            
            // Check if there are any onVariableChanged callbacks
            {
                foreach (var symbol in symbolTable.GetSymbols())
                {
                    if (code.HasMethod(UdonGraphCompiler.GetVariableChangeEventName(symbol)))
                    {
                        data.GetVariableNamed(symbol).HasCallback = true;
                    }
                }
            }

            return program;
        }

        public static void ProcessProgramForCyanTriggers(CyanTriggerAssemblyProgram program, bool isInstance) // Add asset here to know for prefixing and method signatures
        {
            ConvertFunctionEpilogues(program);
            ConvertBlankCustomEvents(program, isInstance);
        }


        // This function will go through all methods in the program and convert the 
        // function epilogues to CyanTrigger style. This is based on UdonSharp's
        // format, but using different variable names.
        
        // U# 
        // - Pushes variable with max end jump location at method starts. This is used to know where to return.
        // - On calling directly, Pushes address of next line, jumps to method address + 1.
        /* 
         _methodName:
            PUSH, __0_const_intnl_SystemUInt32

            [Method] <- Jump here for local method calls

            PUSH, __0_intnl_returnTarget_UInt32 #Function epilogue
            COPY
            JUMP_INDIRECT, __0_intnl_returnTarget_UInt32
        */

        // UdonGraph
        // - Nothing at beginning, never have jump indirect to methods...
        // - End jump to infinity! 
        /*
         _methodName:
            [Method]
            JUMP, 0xFFFFFFFC
        */
        public static void ConvertFunctionEpilogues(CyanTriggerAssemblyProgram program)
        {
            CyanTriggerAssemblyCode code = program.Code;
            CyanTriggerAssemblyData data = program.Data;

            uint maxAddress = 0;
            foreach (var method in code.GetMethods())
            {
                foreach (var instruction in method.Actions)
                {
                    maxAddress = Math.Max(maxAddress, instruction.GetAddress());
                }
            }

            // Assumed based on CyanTrigger and UdonSharp assembly generation
            string returnVariableName =
                CyanTriggerAssemblyData.GetSpecialVariableName(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ReturnAddress);
            string endAddressVariableName =
                CyanTriggerAssemblyData.GetSpecialVariableName(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.EndAddress);

            bool hasFunctionEpilogue = data.TryGetVariableNamed(returnVariableName, out _);

            // UdonSharp 0.X program
            if (!hasFunctionEpilogue && data.TryGetVariableNamed("__refl_const_intnl_udonTypeID", out _))
            {
                // This seems risky
                string returnJumpVarName = "__0_intnl_returnTarget_UInt32";
                string endJumpVarName = "__0_const_intnl_SystemUInt32";
                CyanTriggerAssemblyDataType udonSharpReturnTarget = data.GetVariableNamed(returnJumpVarName);
                Debug.Assert(udonSharpReturnTarget != null, $"Could not find variable named \"{returnJumpVarName}\" in UdonSharp program.");
                CyanTriggerAssemblyDataType udonSharpEndAddress = data.GetVariableNamed(endJumpVarName);
                Debug.Assert(udonSharpEndAddress != null, $"Could not find variable named \"{endJumpVarName}\" in UdonSharp program.");

                data.RenameVariable(returnVariableName, udonSharpReturnTarget);
                data.RenameVariable(endAddressVariableName, udonSharpEndAddress);

                hasFunctionEpilogue = true;
            }
            
            // UdonSharp 1.0 program
            if (!hasFunctionEpilogue && data.TryGetVariableNamed("__refl_typeid", out _))
            {
                // This seems risky 
                string returnJumpVarName = "__intnl_returnJump_SystemUInt32_0";
                string endJumpVarName = "__const_SystemUInt32_0";
                CyanTriggerAssemblyDataType udonSharpReturnTarget = data.GetVariableNamed(returnJumpVarName);
                Debug.Assert(udonSharpReturnTarget != null, $"Could not find variable named \"{returnJumpVarName}\" in UdonSharp program.");
                CyanTriggerAssemblyDataType udonSharpEndAddress = data.GetVariableNamed(endJumpVarName);
                Debug.Assert(udonSharpEndAddress != null, $"Could not find variable named \"{endJumpVarName}\" in UdonSharp program.");

                data.RenameVariable(returnVariableName, udonSharpReturnTarget);
                data.RenameVariable(endAddressVariableName, udonSharpEndAddress);

                hasFunctionEpilogue = true;
            }

            if (!hasFunctionEpilogue)
            {
                // Unknown program type, assume no function epilogue. 
                // Convert all jump to end to jump to method nop, and convert jump to last address to nop. 
                // Add initial address push and function epilogue.

                foreach (var method in code.GetMethods())
                {
                    for (int cur = 0; cur < method.Actions.Count; ++cur)
                    {
                        var instruction = method.Actions[cur];
                        bool isLast = cur + 1 == method.Actions.Count;

                        CyanTriggerInstructionType instructionType = instruction.GetInstructionType();

                        if (isLast)
                        {
                            Debug.Assert(instructionType == CyanTriggerInstructionType.JUMP, "Last method instruction is not a JUMP instruction! ");
                            // convert to Nop as a way to delete, but not remove jump references to this instruction
                            instruction.ConvertToNop();
                        }
                        else if (instructionType == CyanTriggerInstructionType.JUMP || instructionType == CyanTriggerInstructionType.JUMP_IF_FALSE)
                        {
                            var jumpInstruction = instruction.GetJumpInstruction();
                            if (jumpInstruction.GetInstructionType() == CyanTriggerInstructionType.NOP && jumpInstruction.GetAddress() > maxAddress)
                            {
                                jumpInstruction.SetJumpInstruction(method.EndNop);
                                //instruction.SetJumpInstruction(method.endNop);
                            }
                        }
                    }

                    method.PushInitialEndVariable(data);
                    method.PushMethodEndReturnJump(data);
                }
            }
            else
            {
                foreach (var method in code.GetMethods())
                {
                    int length = method.Actions.Count;
                    // Verify FunctionEpilogues exists to prevent CyanTrigger from adding it again.
                    if (length >= 3)
                    {
                        var pushReturn = method.Actions[length - 3];
                        var copy = method.Actions[length - 2];
                        var jumpIndirect = method.Actions[length - 1];
                    
                        if (pushReturn.GetInstructionType() == CyanTriggerInstructionType.PUSH
                            && pushReturn.GetVariableName() == returnVariableName
                            && copy.GetInstructionType() == CyanTriggerInstructionType.COPY
                            && jumpIndirect.GetInstructionType() == CyanTriggerInstructionType.JUMP_INDIRECT
                            && jumpIndirect.GetVariableName() == returnVariableName)
                        {
                            method.EndReturnJumpAdded = true;
                        }
                        else
                        {
                            // TODO this is bad.
                        }
                    }
                }
            }

            // Ensure variables have proper initial values
            data.CreateSpecialAddressVariables();
        }


        // Find and convert SendCustomEvent(this, "") to jump methods
        // For instance types, convert all SendCustomEvent+Delayed+Networked to special jump instructions
        public static void ConvertBlankCustomEvents(CyanTriggerAssemblyProgram program, bool isInstance)
        {
            CyanTriggerAssemblyCode code = program.Code;
            CyanTriggerAssemblyData data = program.Data;
            
            string sendCustomEventName = CyanTriggerDefinitionResolver.GetMethodSignature(
                typeof(IUdonEventReceiver).GetMethod(nameof(IUdonEventReceiver.SendCustomEvent)));
            string sendCustomNetworkedEventName = CyanTriggerDefinitionResolver.GetMethodSignature(
                typeof(IUdonEventReceiver).GetMethod(nameof(IUdonEventReceiver.SendCustomNetworkEvent)));
            string sendCustomEventDelayedSecondsName = CyanTriggerDefinitionResolver.GetMethodSignature(
                typeof(IUdonEventReceiver).GetMethod(nameof(IUdonEventReceiver.SendCustomEventDelayedSeconds)));
            string sendCustomEventDelayedFramesName = CyanTriggerDefinitionResolver.GetMethodSignature(
                typeof(IUdonEventReceiver).GetMethod(nameof(IUdonEventReceiver.SendCustomEventDelayedFrames)));

            // Special custom action variables. Only add if needed.
            Dictionary<CyanTriggerAssemblyData.CyanTriggerSpecialVariableName, CyanTriggerAssemblyDataType>
                specialVariables = new Dictionary<CyanTriggerAssemblyData.CyanTriggerSpecialVariableName, CyanTriggerAssemblyDataType>();
            CyanTriggerAssemblyDataType GetSpecialVariable(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName variableType)
            {
                if (!specialVariables.TryGetValue(variableType, out var specialVariable))
                {
                    specialVariable = data.GetSpecialVariable(variableType);
                    specialVariables[variableType] = specialVariable;
                }

                return specialVariable;
            }

            // Process all instructions to find all variables that are modified.
            HashSet<string> modifiedVariables = new HashSet<string>();
            HashSet<string> seenBeforeModifiedVariables = new HashSet<string>();
            Dictionary<string, string> variableEventUsage = new Dictionary<string, string>();
            string globalVariableScope = "$Variable is Global";
            
            List<(CyanTriggerAssemblyMethod, int, List<(CyanTriggerAssemblyInstruction, int)>)> customEventAddresses = 
                new List<(CyanTriggerAssemblyMethod, int, List<(CyanTriggerAssemblyInstruction, int)>)>();
            
            CyanTriggerActionVariableDefinition[] oneVariableReadInput =
            {
                new CyanTriggerActionVariableDefinition
                {
                    variableType = CyanTriggerActionVariableTypeDefinition.Constant
                                   | CyanTriggerActionVariableTypeDefinition.VariableInput
                },
            };
            
            CyanTriggerActionVariableDefinition[] copyDefinition =
            {
                new CyanTriggerActionVariableDefinition
                {
                    variableType = CyanTriggerActionVariableTypeDefinition.Constant
                                   | CyanTriggerActionVariableTypeDefinition.VariableInput
                },
                new CyanTriggerActionVariableDefinition
                {
                    variableType = CyanTriggerActionVariableTypeDefinition.VariableInput
                                   | CyanTriggerActionVariableTypeDefinition.VariableOutput
                },
            };
            
            // Go through all methods and find all variables which can be modified.
            foreach (var method in code.GetMethods())
            {
                // Used to check when a variable is first referenced.
                HashSet<string> variablesSeen = new HashSet<string>();

                // Check if a variable is modified or used in multiple events
                void CheckVariableUsage(
                    List<(CyanTriggerAssemblyInstruction, int)> inputs, 
                    CyanTriggerActionVariableDefinition[] variableDefinitions)
                {
                    for (int curVar = 0; curVar < variableDefinitions.Length; ++curVar)
                    {
                        string varName = inputs[curVar].Item1.GetVariableName();
                        bool firstSeen = !variablesSeen.Contains(varName);
                        if (firstSeen)
                        {
                            variablesSeen.Add(varName);
                        }
                        
                        // Found out type, add variable to list of var
                        if ((variableDefinitions[curVar].variableType &
                             CyanTriggerActionVariableTypeDefinition.VariableOutput) != 0)
                        {
                            modifiedVariables.Add(varName);
                        }
                        // This is the first time seeing the variable, yet we are using the value.
                        else if (firstSeen)
                        {
                            seenBeforeModifiedVariables.Add(varName);
                        }
                        
                        // Check if this variable has been seen before. If so, check to see if it was this method.
                        // If the method is a different one, then this variable is used in multiple events and is global.
                        if (!variableEventUsage.TryGetValue(varName, out var methodName))
                        {
                            variableEventUsage.Add(varName, method.Name);
                        }
                        else if (methodName != method.Name)
                        {
                            variableEventUsage[varName] = globalVariableScope;
                        }
                    }
                }
                
                // Starting at 1 since you always need 1 input into copy instructions
                // Ignore last two items as those are copy/jump method epilogues
                for (int curAction = 1; curAction < method.Actions.Count - 2; ++curAction)
                {
                    var instruction = method.Actions[curAction];
                    if (instruction.GetInstructionType() == CyanTriggerInstructionType.COPY)
                    {
                        bool fail = false;
                        List<(CyanTriggerAssemblyInstruction, int)> inputs = new List<(CyanTriggerAssemblyInstruction, int)>();
                        GetInstructionInputs(method.Actions, curAction, inputs, ref fail);
                        if (fail)
                        {
                            continue;
                        }
                        Debug.Assert(inputs.Count == 2, $"Input count does not match expected variable count! {inputs.Count}, 2, Copy");
                        
                        CheckVariableUsage(inputs, copyDefinition);
                    }

                    else if (instruction.GetInstructionType() == CyanTriggerInstructionType.JUMP_IF_FALSE)
                    {
                        bool fail = false;
                        List<(CyanTriggerAssemblyInstruction, int)> inputs = new List<(CyanTriggerAssemblyInstruction, int)>();
                        GetInstructionInputs(method.Actions, curAction, inputs, ref fail);
                        if (fail)
                        {
                            Debug.LogError($"Failed to get inputs for JUMP_IF_FALSE: {instruction.GetSignature()}");
                            continue;
                        }
                        Debug.Assert(inputs.Count == 1, $"Input count does not match expected variable count! {inputs.Count}, 1, Jump If False");
                        
                        CheckVariableUsage(inputs, oneVariableReadInput);
                    }
                    
                    else if (instruction.GetInstructionType() == CyanTriggerInstructionType.JUMP_INDIRECT)
                    {
                        List<(CyanTriggerAssemblyInstruction, int)> inputs =
                            new List<(CyanTriggerAssemblyInstruction, int)>
                            {
                                (CyanTriggerAssemblyInstruction.PushVariable(instruction.GetVariable()), -1)
                            };
                        CheckVariableUsage(inputs, oneVariableReadInput);
                    }

                    else if (instruction.GetInstructionType() == CyanTriggerInstructionType.EXTERN)
                    {
                        string signature = instruction.GetExternSignature();
                        
                        bool fail = false;
                        List<(CyanTriggerAssemblyInstruction, int)> inputs = new List<(CyanTriggerAssemblyInstruction, int)>();
                        GetInstructionInputs(method.Actions, curAction, inputs, ref fail);
                        if (fail)
                        {
                            continue;
                        }

                        // Check if the extern modifies any variables
                        var info = CyanTriggerActionInfoHolder.GetActionInfoHolder("", signature);
                        var variables = info.GetBaseActionVariables(true);
                        
                        Debug.Assert(inputs.Count == variables.Length, $"Input count does not match expected variable count! {inputs.Count}, {variables.Length}, {signature}");

                        CheckVariableUsage(inputs, variables);
                        
                        // Save all sendCustomEvent externs to process if the event is ever modified and needs to be replaced with a jump.
                        if (signature == sendCustomEventName
                            || signature == sendCustomNetworkedEventName
                            || signature == sendCustomEventDelayedSecondsName
                            || signature == sendCustomEventDelayedFramesName)
                        {
                            customEventAddresses.Add((method, curAction, inputs));
                        }
                    }
                }
            }
            
            // Mark all variables that are seen before modifications. These will either be constants or method inputs.
            foreach (var varName in seenBeforeModifiedVariables)
            {
                var variable = data.GetVariableNamed(varName);
                variable.ReadBeforeModified = true;
            }

            // Save information about variable being modified.
            foreach (var varName in modifiedVariables)
            {
                var variable = data.GetVariableNamed(varName);
                variable.IsModified = true;

                if (seenBeforeModifiedVariables.Contains(varName))
                {
                    variable.IsGlobalVariable = true;
                }
            }
            
            // Save information about variables being used and modified in multiple events.
            foreach (var varEventData in variableEventUsage)
            {
                var variable = data.GetVariableNamed(varEventData.Key);
                if (varEventData.Value == globalVariableScope 
                    && variable.IsModified 
                    && seenBeforeModifiedVariables.Contains(variable.Name))
                {
                    variable.IsGlobalVariable = true;
                }
            }

            // Reverse to process in decreasing order since these modify and add actions.
            customEventAddresses.Reverse();
            
            // Go through all method/address pairs to check if the SendCustomEvent is a jump with blank event name, and convert to jump.
            foreach (
                (CyanTriggerAssemblyMethod method, int address, List<(CyanTriggerAssemblyInstruction, int)> inputs) 
                in customEventAddresses)
            {
                var instruction = method.Actions[address];
                string signature = instruction.GetExternSignature();

                int eventInputIndex = 1;
                if (signature == sendCustomNetworkedEventName)
                {
                    eventInputIndex = 2;
                }
                
                var pushUdon = inputs[0].Item1;
                var pushEvent = inputs[eventInputIndex].Item1;

                string eventVarName = pushEvent.GetVariableName();

                if (string.IsNullOrEmpty(eventVarName))
                {
                    Debug.Log($"Failed to properly convert: {pushEvent.GetInstructionType()}");
                    continue;
                }

                // Verify udon variable is for this and not something that is modified.
                var udonVariable = data.GetVariableNamed(pushUdon.GetVariableName());
                if (udonVariable == null 
                    || udonVariable.IsModified
                    || !(udonVariable.DefaultValue is UdonGameObjectComponentHeapReference))
                {
                    continue;
                }

                if (!data.TryGetVariableNamed(eventVarName, out var eventVariable))
                {
                    continue;
                }

                // At this point we know that we are targeting this udon
                // Update event jump locations.
                if (signature == sendCustomEventName)
                {
                    bool eventEmpty = 
                        string.IsNullOrEmpty((string)eventVariable.DefaultValue)
                        && !modifiedVariables.Contains(eventVarName);
                
                    if (eventEmpty)
                    {
                        instruction.ConvertToNop();
                        pushUdon.ConvertToNop();
                        pushEvent.ConvertToNop();
                    
                        var actionJumpVariable = 
                            GetSpecialVariable(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ActionJumpAddress);
                        
                        actionJumpVariable.DefaultValue = 0u;
                        
                        List<CyanTriggerAssemblyInstruction> addedInstructions =
                            new List<CyanTriggerAssemblyInstruction>();
                        
                        CyanTriggerAssemblyInstruction warningNop = CyanTriggerAssemblyInstruction.Nop();
                        CyanTriggerAssemblyInstruction endNop = CyanTriggerAssemblyInstruction.Nop();
                        
                        var constUnsignedZero = data.GetOrCreateVariableConstant(typeof(uint), 0u);
                        var tempBool = data.RequestTempVariable(typeof(bool));
                        var pushTempBool = CyanTriggerAssemblyInstruction.PushVariable(tempBool);
                        
                        // Add check to ignore if value hasn't changed.
                        addedInstructions.Add(CyanTriggerAssemblyInstruction.PushVariable(actionJumpVariable));
                        addedInstructions.Add(CyanTriggerAssemblyInstruction.PushVariable(constUnsignedZero));
                        addedInstructions.Add(pushTempBool);
                        addedInstructions.Add(CyanTriggerAssemblyInstruction.CreateExtern(
                            CyanTriggerDefinitionResolver.GetPrimitiveOperationSignature(
                                typeof(uint), PrimitiveOperation.Inequality)));
                        
                        // If false, log warning notifying the user.
                        addedInstructions.Add(pushTempBool);
                        addedInstructions.Add(CyanTriggerAssemblyInstruction.JumpIfFalse(warningNop));

                        // Add jump to CyanTrigger actions event.
                        addedInstructions.AddRange(CyanTriggerAssemblyActionsUtils.JumpIndirect(data, actionJumpVariable));
                        addedInstructions.Add(CyanTriggerAssemblyInstruction.Jump(endNop));
                        
                        addedInstructions.Add(warningNop);
                        addedInstructions.AddRange(CyanTriggerAssemblyActionsUtils.DebugLog("[CyanTrigger] Custom Action does not have destination set. User actions for the event will be skipped. This can happen when a Custom Action uses Networked events or Delayed events.", data, "LogError"));
                        
                        addedInstructions.Add(endNop);
                        
                        data.ReleaseTempVariable(tempBool);
                        
                        method.Actions.InsertRange(
                            address, 
                            addedInstructions);
                    }
                    
                    // Instances shouldn't need to handle SendCustomEvent as this will happen same frame.
                    // Variables will all be the instance version still.
                    // else if (isInstance)
                    // {
                    //     eventVariable.IsEventName = true;
                    //     
                    //     List<CyanTriggerAssemblyInstruction> instanceJumpActions = new List<CyanTriggerAssemblyInstruction>();
                    //     
                    //     var instanceEventNameVariable = 
                    //         GetSpecialVariable(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ActionInstanceEventName);
                    //     instanceJumpActions.AddRange(CyanTriggerAssemblyActionsUtils.CopyVariables(eventVariable, instanceEventNameVariable));
                    //     
                    //     var instanceJumpVariable = 
                    //         GetSpecialVariable(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ActionInstanceEventJumpAddress);
                    //     instanceJumpActions.AddRange(CyanTriggerAssemblyActionsUtils.JumpIndirect(data, instanceJumpVariable));
                    //     
                    //     instruction.ConvertToNop();
                    //     pushUdon.ConvertToNop();
                    //     pushEvent.ConvertToNop();
                    //
                    //     method.Actions.InsertRange(address, instanceJumpActions);
                    // }
                }
                if (!isInstance)
                {
                    continue;
                }
                
                if (signature == sendCustomNetworkedEventName)
                {
                    var pushTarget = inputs[1].Item1;
                    if (!data.TryGetVariableNamed(pushTarget.GetVariableName(), out var targetVariable))
                    {
                        continue;
                    }
                    
                    eventVariable.IsEventName = true;

                    List<CyanTriggerAssemblyInstruction> instanceJumpActions = new List<CyanTriggerAssemblyInstruction>();
                    
                    var instanceEventNameVariable = 
                        GetSpecialVariable(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ActionInstanceEventName);
                    instanceJumpActions.AddRange(CyanTriggerAssemblyActionsUtils.CopyVariables(eventVariable, instanceEventNameVariable));
                    
                    var instanceEventNetworkTargetVariable = 
                        GetSpecialVariable(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ActionInstanceEventNetworkTarget);
                    instanceJumpActions.AddRange(CyanTriggerAssemblyActionsUtils.CopyVariables(targetVariable, instanceEventNetworkTargetVariable));

                    var instanceJumpVariable = 
                        GetSpecialVariable(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ActionInstanceEventNetworkedJumpAddress);
                    instanceJumpActions.AddRange(CyanTriggerAssemblyActionsUtils.JumpIndirect(data, instanceJumpVariable));
                    
                    instruction.ConvertToNop();
                    pushUdon.ConvertToNop();
                    pushEvent.ConvertToNop();
                    pushTarget.ConvertToNop();

                    method.Actions.InsertRange(address, instanceJumpActions);
                }
                else if (signature == sendCustomEventDelayedFramesName
                         || signature == sendCustomEventDelayedSecondsName)
                {
                    var pushDelayTiming = inputs[2].Item1;
                    var pushMethodTiming = inputs[3].Item1;
                    
                    if (!data.TryGetVariableNamed(pushDelayTiming.GetVariableName(), out var delayTimingVariable)
                        || !data.TryGetVariableNamed(pushMethodTiming.GetVariableName(), out var methodTimingVariable))
                    {
                        continue;
                    }
                    
                    eventVariable.IsEventName = true;

                    List<CyanTriggerAssemblyInstruction> instanceJumpActions = new List<CyanTriggerAssemblyInstruction>();
                    
                    var instanceEventNameVariable = 
                        GetSpecialVariable(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ActionInstanceEventName);
                    instanceJumpActions.AddRange(CyanTriggerAssemblyActionsUtils.CopyVariables(eventVariable, instanceEventNameVariable));
                    
                    
                    var instanceEventDelayTimingVariable = GetSpecialVariable(
                        signature == sendCustomEventDelayedFramesName
                            ? CyanTriggerAssemblyData.CyanTriggerSpecialVariableName
                                .ActionInstanceEventDelayFrames
                            : CyanTriggerAssemblyData.CyanTriggerSpecialVariableName
                                .ActionInstanceEventDelaySeconds);
                    instanceJumpActions.AddRange(CyanTriggerAssemblyActionsUtils.CopyVariables(delayTimingVariable, instanceEventDelayTimingVariable));
                    
                    var instanceEventMethodTimingVariable = 
                        GetSpecialVariable(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ActionInstanceEventDelayTiming);
                    instanceJumpActions.AddRange(CyanTriggerAssemblyActionsUtils.CopyVariables(methodTimingVariable, instanceEventMethodTimingVariable));

                    var instanceJumpVariable = GetSpecialVariable(
                        signature == sendCustomEventDelayedFramesName
                            ? CyanTriggerAssemblyData.CyanTriggerSpecialVariableName
                                .ActionInstanceEventDelayFramesJumpAddress
                            : CyanTriggerAssemblyData.CyanTriggerSpecialVariableName
                                .ActionInstanceEventDelaySecondsJumpAddress);
                    instanceJumpActions.AddRange(CyanTriggerAssemblyActionsUtils.JumpIndirect(data, instanceJumpVariable));
                    
                    instruction.ConvertToNop();
                    pushUdon.ConvertToNop();
                    pushEvent.ConvertToNop();
                    pushDelayTiming.ConvertToNop();
                    pushMethodTiming.ConvertToNop();

                    method.Actions.InsertRange(address, instanceJumpActions);
                }
            }
        }
        
        
        public static CyanTriggerProgramTranslation AddNamespace(CyanTriggerAssemblyProgram program, string prefixNamespace)
        {
            CyanTriggerItemTranslation[] variableTranslations = program.Data.AddPrefixToAllVariables(prefixNamespace);
            CyanTriggerItemTranslation[] methodTranslations = 
                program.Code.AddPrefixToAllMethods(prefixNamespace, variableTranslations);

            Dictionary<string, string> methodMap = new Dictionary<string, string>();
            Dictionary<string, string> variableMap = new Dictionary<string, string>();

            HashSet<CyanTriggerAssemblyDataType> modifiedVariables = new HashSet<CyanTriggerAssemblyDataType>();

            // Add empty entry event to translate to itself. 
            {
                int size = methodTranslations.Length;
                Array.Resize(ref methodTranslations, size + 1);
                methodTranslations[size] = new CyanTriggerItemTranslation
                {
                    BaseName = CyanTriggerActionGroupDefinition.EmptyEntryEventName,
                    TranslatedName = CyanTriggerActionGroupDefinition.EmptyEntryEventName,
                };
            }

            CyanTriggerAssemblyCode code = program.Code;
            CyanTriggerAssemblyData data = program.Data;
            
            foreach (var methodTranslation in methodTranslations)
            {
                methodMap.Add(methodTranslation.BaseName, methodTranslation.TranslatedName);
            }
            
            foreach (var variableTranslation in variableTranslations)
            {
                variableMap.Add(variableTranslation.BaseName, variableTranslation.TranslatedName);
                
                // Check all variables to see if they are event names, and auto convert them to use the new event name.
                var variable = data.GetVariableNamed(variableTranslation.TranslatedName);
                if (!variable.IsEventName)
                {
                    continue;
                }
                
                string defaultValue = (string)variable.DefaultValue;
                if (!string.IsNullOrEmpty(defaultValue) && methodMap.TryGetValue(defaultValue, out string newMethod))
                {
                    modifiedVariables.Add(variable);
                    variable.DefaultValue = newMethod;
                }
            }

            // After all events and variables have changed names, go through all ways to use those variables that are
            // referenced by string and update those names to the new names.
            
            string sendCustomEventName = CyanTriggerDefinitionResolver.GetMethodSignature(
                typeof(IUdonEventReceiver).GetMethod(nameof(IUdonEventReceiver.SendCustomEvent)));
            string sendCustomNetworkedEventName = CyanTriggerDefinitionResolver.GetMethodSignature(
                typeof(IUdonEventReceiver).GetMethod(nameof(IUdonEventReceiver.SendCustomNetworkEvent)));
            string sendCustomEventDelayedSecondsName = CyanTriggerDefinitionResolver.GetMethodSignature(
                typeof(IUdonEventReceiver).GetMethod(nameof(IUdonEventReceiver.SendCustomEventDelayedSeconds)));
            string sendCustomEventDelayedFramesName = CyanTriggerDefinitionResolver.GetMethodSignature(
                typeof(IUdonEventReceiver).GetMethod(nameof(IUdonEventReceiver.SendCustomEventDelayedFrames)));
            
            string setProgramVariableName = CyanTriggerDefinitionResolver.GetMethodSignature(
                typeof(UdonBehaviour).GetMethod(nameof(UdonBehaviour.SetProgramVariable), 
                    new [] {typeof(string), typeof(object)}));
            string getProgramVariableTypeName = CyanTriggerDefinitionResolver.GetMethodSignature(
                typeof(UdonBehaviour).GetMethod(nameof(UdonBehaviour.GetProgramVariableType)));
            string getProgramVariableName = null;
            foreach (var method in typeof(UdonBehaviour).GetMethods())
            {
                if (method.Name == nameof(UdonBehaviour.GetProgramVariable)
                    && method.ReturnParameter?.ParameterType == typeof(object))
                {
                    getProgramVariableName = CyanTriggerDefinitionResolver.GetMethodSignature(method);
                    break;
                }
            }

            foreach (var method in code.GetMethods())
            {
                for (int cur = 0; cur < method.Actions.Count; ++cur)
                {
                    var instruction = method.Actions[cur];
                    
                    // Go through all SendCustomEvent types and update all method names
                    if (instruction.GetInstructionType() == CyanTriggerInstructionType.EXTERN)
                    {
                        int inputIndex = -1;
                        bool isEvent = true;
                        string signature = instruction.GetExternSignature();
                        if (signature == sendCustomEventName 
                            || signature == sendCustomEventDelayedSecondsName
                            || signature == sendCustomEventDelayedFramesName)
                        {
                            inputIndex = 1;
                        }
                        else if (signature == sendCustomNetworkedEventName)
                        {
                            inputIndex = 2;
                        }
                        else if (signature == setProgramVariableName
                                 || signature == getProgramVariableName
                                 || signature == getProgramVariableTypeName)
                        {
                            inputIndex = 1;
                            isEvent = false;
                        }
                        
                        if (inputIndex == -1)
                        {
                            continue;
                        }
                        
                        List<(CyanTriggerAssemblyInstruction, int)> inputs = new List<(CyanTriggerAssemblyInstruction, int)>();
                        bool fail = false;
                        GetInstructionInputs(method.Actions, cur, inputs, ref fail);
                        if (fail)
                        {
                            continue;
                        }

                        var udonPushEvent = inputs[0].Item1;
                        var udonVariable = data.GetVariableNamed(udonPushEvent.GetVariableName());
                        
                        // Skip attempting to rename if the reference isn't self
                        if (!(udonVariable.DefaultValue is UdonGameObjectComponentHeapReference))
                        {
                            continue;
                        }
                        // Check if the Udon variable is modified in the program at all
                        if (udonVariable.IsModified)
                        {
                            continue;
                        }

                        var pushEvent = inputs[inputIndex].Item1;
                        var variable = data.GetVariableNamed(pushEvent.GetVariableName());
                        
                        // Check if the event name variable is modified in the program at all
                        if (variable.IsModified)
                        {
                            continue;
                        }
                        
                        // If event name variable is modified, skip it.
                        if (modifiedVariables.Contains(variable))
                        {
                            continue;
                        }

                        var map = isEvent ? methodMap : variableMap;
                        
                        // Update the event or variable name if it exists in the mapping.
                        string defaultValue = ((string)variable.DefaultValue);
                        if (!string.IsNullOrEmpty(defaultValue) && map.TryGetValue(defaultValue, out string newName))
                        {
                            modifiedVariables.Add(variable);
                            variable.DefaultValue = newName;

                            // SetProgramVariable may be the only way this variable is modified, so mark it.
                            if (signature == setProgramVariableName 
                                && data.TryGetVariableNamed(newName, out var modifiedVariable))
                            {
                                modifiedVariable.IsModified = true;
                                modifiedVariable.IsGlobalVariable = true;
                            }
                        }
                    }

                    // Update variable references for push instructions
                    if (instruction.GetInstructionType() == CyanTriggerInstructionType.PUSH && instruction.GetVariable() == null)
                    {
                        string varName = instruction.GetVariableName();
                        if (variableMap.TryGetValue(varName, out string newName))
                        {
                            var variable = data.GetVariableNamed(newName);
                            instruction.SetVariable(variable);
                        }
                    }
                }
            }

            return new CyanTriggerProgramTranslation
                { TranslatedMethods = methodTranslations, TranslatedVariables = variableTranslations };
        }
    }
}