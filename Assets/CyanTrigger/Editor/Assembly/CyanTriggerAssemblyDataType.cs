using System;
using System.Collections.Generic;

namespace Cyan.CT.Editor
{
    public class CyanTriggerAssemblyDataType
    {
        // Used in Custom Actions to know if a variable's value ever changes.
        public const string ModifiedValueFlag = "ValueIsModified";
        // Used in Custom Actions to know if a variable is an event name and it should be migrated after namespacing
        public const string IsEventNameVariableFlag = "IsEventName";
        // Used in Custom Actions to know if a variable is modified in multiple methods.
        public const string GlobalVariableFlag = "VariableIsGlobal";
        public const string ReadBeforeModifiedFlag = "VariableIsReadBeforeModified";
        
        public string Name;
        public uint Address;
        public readonly Type Type;
        public bool Export;
        public object DefaultValue;
        public CyanTriggerVariableSyncMode Sync;
        public bool HasCallback;
        public CyanTriggerAssemblyDataType PreviousVariable;
        public bool IsPrevVar;
        public string Guid;

        // Store special data with this variable
        private HashSet<string> _dataFlags = new HashSet<string>();

        private readonly string _resolvedType;

        public CyanTriggerAssemblyDataType(string name, Type type, string resolvedType, bool export)
        {
            Name = name;
            Type = type;
            Export = export;
            _resolvedType = resolvedType;
        }
        
        public override string ToString()
        {
            return $"{Name}: %{_resolvedType}, {GetDefaultString()}";
        }

        public void SetPreviousVariable(CyanTriggerAssemblyDataType prevVariable)
        {
            PreviousVariable = prevVariable;
            prevVariable.IsPrevVar = true;
            prevVariable.DefaultValue = DefaultValue;
        }

        private string GetDefaultString()
        {
            if (CyanTriggerAssemblyData.IsSpecialType(Type) && CyanTriggerAssemblyData.IsIdThisVariable(Name))
            {
                return "this";
            }

            return "null";
        }

        public CyanTriggerAssemblyDataType Clone()
        {
            CyanTriggerAssemblyDataType variable = new CyanTriggerAssemblyDataType(Name, Type, _resolvedType, Export)
            {
                Address = Address,
                DefaultValue = DefaultValue,
                Sync = Sync,
                HasCallback = HasCallback,
                PreviousVariable = PreviousVariable,
                IsPrevVar = IsPrevVar,
                Guid = Guid,
                _dataFlags = new HashSet<string>(_dataFlags),
            };

            return variable;
        }

        public void CopyTo(CyanTriggerAssemblyDataType other)
        {
            other.Address = Address;
            other.DefaultValue = DefaultValue;
            other.Sync = Sync;
            other.HasCallback = HasCallback;
            other.PreviousVariable = PreviousVariable;
            other.IsPrevVar = IsPrevVar;
            other.Guid = Guid;
            other._dataFlags = new HashSet<string>(_dataFlags);
        }

        public bool GetDataFlag(string flag)
        {
            return _dataFlags.Contains(flag);
        }

        public void SetDataFlag(string flag, bool value)
        {
            if (value)
            {
                _dataFlags.Add(flag);
            }
            else
            {
                _dataFlags.Remove(flag);
            }
        }
        
        public bool IsModified
        {
            get => GetDataFlag(ModifiedValueFlag);
            set => SetDataFlag(ModifiedValueFlag, value);
        }
        
        public bool IsEventName
        {
            get => GetDataFlag(IsEventNameVariableFlag);
            set => SetDataFlag(IsEventNameVariableFlag, value);
        }
        
        public bool IsGlobalVariable
        {
            get => GetDataFlag(GlobalVariableFlag);
            set => SetDataFlag(GlobalVariableFlag, value);
        }
        
        public bool ReadBeforeModified
        {
            get => GetDataFlag(ReadBeforeModifiedFlag);
            set => SetDataFlag(ReadBeforeModifiedFlag, value);
        }
    }
}