using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cyan.CT.Editor
{
    public enum CyanTriggerInstructionType
    {
        // ReSharper disable InconsistentNaming
        // Enum is used for exporting type to string and should be kept in this format.
        NOP,
        POP,
        COPY,
        PUSH,
        JUMP_IF_FALSE,
        JUMP,
        EXTERN,
        JUMP_INDIRECT,
        // ReSharper restore InconsistentNaming
    }

    public static class CyanTriggerInstructionTypeExtensions
    {
        public static bool IsJump(this CyanTriggerInstructionType instr)
        {
            return
                instr == CyanTriggerInstructionType.JUMP
                || instr == CyanTriggerInstructionType.JUMP_IF_FALSE
                || instr == CyanTriggerInstructionType.JUMP_INDIRECT;
        }
    }
    
    public class CyanTriggerAssemblyInstruction
    {
        private CyanTriggerInstructionType _instructionType;
        private uint _instructionAddress;
        private string _signature;
        private string _jumpLabel;
        private CyanTriggerAssemblyDataType _pushVariable;
        private CyanTriggerAssemblyInstruction _jumpToInstruction;

        public CyanTriggerAssemblyInstruction Clone()
        {
            CyanTriggerAssemblyInstruction action = new CyanTriggerAssemblyInstruction(_instructionType)
            {
                _instructionAddress = _instructionAddress,
                _signature = _signature,
                _jumpLabel = _jumpLabel,
                _pushVariable = _pushVariable,
                _jumpToInstruction = _jumpToInstruction
            };

            return action;
        }
        
        public void UpdateMapping(
            Dictionary<CyanTriggerAssemblyInstruction, CyanTriggerAssemblyInstruction> instructionMapping,
            Dictionary<CyanTriggerAssemblyDataType, CyanTriggerAssemblyDataType> variableMapping)
        {
            if (_pushVariable != null && variableMapping.TryGetValue(_pushVariable, out var newVar))
            {
                _pushVariable = newVar;
            }

            if (_jumpToInstruction != null && instructionMapping.TryGetValue(_jumpToInstruction, out var newJump))
            {
                _jumpToInstruction = newJump;
            }
        }
        
        public static CyanTriggerAssemblyInstruction Copy()
        {
            return new CyanTriggerAssemblyInstruction(CyanTriggerInstructionType.COPY);
        }

        public static CyanTriggerAssemblyInstruction Nop()
        {
            return new CyanTriggerAssemblyInstruction(CyanTriggerInstructionType.NOP);
        }

        public static CyanTriggerAssemblyInstruction Pop()
        {
            return new CyanTriggerAssemblyInstruction(CyanTriggerInstructionType.POP);
        }

        public static CyanTriggerAssemblyInstruction CreateExtern(string methodSignature)
        {
            return new CyanTriggerAssemblyInstruction(CyanTriggerInstructionType.EXTERN, $"\"{methodSignature}\"");
        }

        public static CyanTriggerAssemblyInstruction PushVariable(CyanTriggerAssemblyDataType variable)
        {
            return new CyanTriggerAssemblyInstruction(CyanTriggerInstructionType.PUSH, variable);
        }

        public static CyanTriggerAssemblyInstruction PushVariable(string variableName)
        {
            return new CyanTriggerAssemblyInstruction(CyanTriggerInstructionType.PUSH, variableName);
        }

        public static CyanTriggerAssemblyInstruction JumpIndirect(CyanTriggerAssemblyDataType variable)
        {
            return new CyanTriggerAssemblyInstruction(CyanTriggerInstructionType.JUMP_INDIRECT, variable);
        }

        public static CyanTriggerAssemblyInstruction JumpLabel(string label)
        {
            return new CyanTriggerAssemblyInstruction(CyanTriggerInstructionType.JUMP)
            {
                _jumpLabel = label
            };
        }

        public static CyanTriggerAssemblyInstruction Jump(CyanTriggerAssemblyInstruction instructionJump)
        {
            return new CyanTriggerAssemblyInstruction(CyanTriggerInstructionType.JUMP, instructionJump);
        }

        public static CyanTriggerAssemblyInstruction JumpIfFalse(CyanTriggerAssemblyInstruction instructionJump)
        {
            return new CyanTriggerAssemblyInstruction(CyanTriggerInstructionType.JUMP_IF_FALSE, instructionJump);
        }

        // Private constructors to force using static creation functions above.
        private CyanTriggerAssemblyInstruction(CyanTriggerInstructionType type)
        {
            _instructionType = type;
        }

        private CyanTriggerAssemblyInstruction(CyanTriggerInstructionType type, string sig)
        {
            _instructionType = type;
            _signature = sig;
        }

        private CyanTriggerAssemblyInstruction(CyanTriggerInstructionType type, CyanTriggerAssemblyInstruction instructionJump)
        {
            _instructionType = type;
            _jumpToInstruction = instructionJump;
        }

        private CyanTriggerAssemblyInstruction(CyanTriggerInstructionType type, CyanTriggerAssemblyDataType variable)
        {
            _instructionType = type;
            _pushVariable = variable;
        }

        public void ConvertToNop()
        {
            _instructionType = CyanTriggerInstructionType.NOP;
        }

        public CyanTriggerInstructionType GetInstructionType()
        {
            return _instructionType;
        }

        public string GetSignature()
        {
            return _signature;
        }

        public string GetExternSignature()
        {
            return _signature.Replace("\"", "");
        }

        public CyanTriggerAssemblyDataType GetVariable()
        {
            return _pushVariable;
        }

        public void SetVariable(CyanTriggerAssemblyDataType variable)
        {
            _signature = null;
            _pushVariable = variable;
        }

        public string GetVariableName()
        {
            if (_pushVariable != null)
            {
                return _pushVariable.Name;
            }

            return _signature;
        }
        
        public string GetJumpLabel()
        {
            return _jumpLabel;
        }

        public CyanTriggerAssemblyInstruction GetJumpInstruction()
        {
            return _jumpToInstruction;
        }

        public void SetJumpInstruction(CyanTriggerAssemblyInstruction instructionJump)
        {
            _jumpToInstruction = instructionJump;
        }

        public void SetAddress(uint address)
        {
            _instructionAddress = address;
        }

        public void UpdateAddress(uint address)
        {
            _signature = $"0x{address:X8}";
        }

        public uint GetAddress()
        {
            return _instructionAddress;
        }

        public uint GetAddressAfterInstruction()
        {
            return _instructionAddress + GetInstructionSize();
        }

        public uint GetInstructionSize()
        {
            if (_instructionType == CyanTriggerInstructionType.NOP)
            {
                return 0u;
            }

            return GetUdonInstructionSize(_instructionType);
        }

        private void ExportSignature()
        {
            if (!string.IsNullOrEmpty(_signature))
            {
                return;
            }

            if (_pushVariable != null)
            {
                _signature = _pushVariable.Name;
            }

            if (_jumpToInstruction != null)
            {
                UpdateAddress(_jumpToInstruction._instructionAddress);
            }
        }

        public string Export()
        {
            ExportSignature();

            string output = "";
            switch (_instructionType)
            {
                case CyanTriggerInstructionType.NOP:
                    //output = "# NOP";
                    break;
                case CyanTriggerInstructionType.POP:
                case CyanTriggerInstructionType.COPY:
                    output = _instructionType.ToString();
                    break;
                case CyanTriggerInstructionType.PUSH:
                case CyanTriggerInstructionType.JUMP_IF_FALSE:
                case CyanTriggerInstructionType.JUMP:
                case CyanTriggerInstructionType.EXTERN:
                case CyanTriggerInstructionType.JUMP_INDIRECT:
                    Debug.Assert(!string.IsNullOrEmpty(_signature), "CyanTriggerAssemblyInstruction.Export Signature is empty on export");
                    output = $"{_instructionType}, {_signature}";
                    break;
                default:
                    throw new Exception($"Unsupported UdonInstructionType! {_instructionType}");
            }

// #if CYAN_TRIGGER_DEBUG
//             output = $"    # {_instructionAddress} 0x{_instructionAddress:X8}\n    {output}";
// #endif
            
            return output;
        }

        public static uint GetUdonInstructionSize(CyanTriggerInstructionType instructionType)
        {
            switch (instructionType)
            {
                case CyanTriggerInstructionType.NOP:
                case CyanTriggerInstructionType.POP:
                case CyanTriggerInstructionType.COPY:
                    return 4;
                case CyanTriggerInstructionType.PUSH:
                case CyanTriggerInstructionType.JUMP_IF_FALSE:
                case CyanTriggerInstructionType.JUMP:
                case CyanTriggerInstructionType.EXTERN:
                case CyanTriggerInstructionType.JUMP_INDIRECT:
                    return 8;
                default:
                    throw new Exception($"Unsupported UdonInstructionType! {instructionType}");
            }
        }

        public static int GetUdonInstructionInputCount(CyanTriggerInstructionType instructionType)
        {
            switch (instructionType)
            {
                case CyanTriggerInstructionType.NOP:
                case CyanTriggerInstructionType.PUSH:
                case CyanTriggerInstructionType.JUMP:
                case CyanTriggerInstructionType.JUMP_INDIRECT:
                    return 0;
                case CyanTriggerInstructionType.POP:
                case CyanTriggerInstructionType.JUMP_IF_FALSE:
                    return 1;
                case CyanTriggerInstructionType.COPY:
                    return 2;
                case CyanTriggerInstructionType.EXTERN:
                default:
                    throw new Exception($"Unsupported UdonInstructionType! {instructionType}");
            }
        }

        public override string ToString()
        {
            string s = _signature;
            if (string.IsNullOrEmpty(s) && _pushVariable != null)
            {
                s = _pushVariable.Name;
            }
            return $"{_instructionType}, {s}";
        }
    }
}