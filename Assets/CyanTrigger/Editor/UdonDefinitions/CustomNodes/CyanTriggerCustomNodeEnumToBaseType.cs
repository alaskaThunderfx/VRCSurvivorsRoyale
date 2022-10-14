using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeEnumToBaseType : 
        CyanTriggerCustomUdonActionNodeDefinition,
        ICyanTriggerCustomNodeCustomVariableInitialization
    {
        private readonly Type _type;
        private readonly Type _baseEnumType;
        private readonly UdonNodeDefinition _definition;
        private readonly string _friendlyName;
        private readonly string _friendlyType;
        private readonly string _convertMethodName;

        private static readonly Dictionary<Type, string> TypeConvertMethodNames = new Dictionary<Type, string>()
        {
            { typeof(sbyte), nameof(Convert.ToSByte) },
            { typeof(byte), nameof(Convert.ToByte) },
            { typeof(short), nameof(Convert.ToInt16) },
            { typeof(ushort), nameof(Convert.ToUInt16) },
            { typeof(int), nameof(Convert.ToInt32) },
            { typeof(uint), nameof(Convert.ToUInt32) },
            { typeof(long), nameof(Convert.ToInt64) },
            { typeof(ulong), nameof(Convert.ToUInt64) },
            { typeof(char), nameof(Convert.ToChar) },
            { typeof(float), nameof(Convert.ToSingle) },
            { typeof(double), nameof(Convert.ToDouble) },
            { typeof(decimal), nameof(Convert.ToDecimal) },
        };

        public CyanTriggerCustomNodeEnumToBaseType(Type type)
        {
            Debug.Assert(type.IsEnum);
            
            _type = type;
            _baseEnumType = Enum.GetUnderlyingType(type);
            _friendlyName = CyanTriggerNameHelpers.GetTypeFriendlyName(_type);
            _friendlyType = CyanTriggerNameHelpers.GetTypeFriendlyName(_baseEnumType);
            string fullName = GetFullnameForType(_type);

            if (!TypeConvertMethodNames.TryGetValue(_baseEnumType, out _convertMethodName))
            {
#if CYAN_TRIGGER_DEBUG
                Debug.LogError($"Base type for enum is missing convert method! {type} -> {_baseEnumType}");
#endif
            }
            
            _definition = new UdonNodeDefinition(
                $"{_friendlyName} To{_friendlyType}",
                fullName,
                _type,
                new []
                {
                    new UdonNodeParameter
                    {
                        name = "enum",
                        parameterType = UdonNodeParameter.ParameterType.IN,
                        type = _type
                    },
                    new UdonNodeParameter
                    {
                        name = _friendlyType,
                        parameterType = UdonNodeParameter.ParameterType.OUT,
                        type = _baseEnumType
                    }
                },
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<object>(),
                true
            );
        }
        
        public static string GetFullnameForType(Type type)
        {
            Type enumBaseType = Enum.GetUnderlyingType(type);
            string fullName = CyanTriggerNameHelpers.SanitizeName(type.FullName);
            string outputFullName = CyanTriggerNameHelpers.SanitizeName(enumBaseType.FullName);
            string friendlyType = CyanTriggerNameHelpers.GetTypeFriendlyName(enumBaseType);
            return $"{fullName}__.To{friendlyType}__{fullName}__{outputFullName}";
        }
        
        public override UdonNodeDefinition GetNodeDefinition()
        {
            return _definition;
        }

        public override CyanTriggerNodeDefinition.UdonDefinitionType GetDefinitionType()
        {
            return CyanTriggerNodeDefinition.UdonDefinitionType.Method;
        }

        public override string GetDisplayName()
        {
            return $"To{_friendlyType}";
        }

        public override bool HasDocumentation()
        {
            return false;
        }

        public override string GetDocumentationLink()
        {
            return "";
        }

        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            var actionInstance = compileState.ActionInstance;
            var actionMethod = compileState.ActionMethod;
            
            var dataVar =
                compileState.GetDataFromVariableInstance(-1, 0, actionInstance.inputs[0], _type, false);
            var outputVar =
                compileState.GetDataFromVariableInstance(-1, 1, actionInstance.inputs[1], _baseEnumType, true);
            
            string convertToBaseType =
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(Convert).GetMethod(_convertMethodName, new[] { typeof(object) }));
            
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(dataVar));
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(outputVar));
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.CreateExtern(convertToBaseType));
            
            var changedVariables = new List<CyanTriggerAssemblyDataType> { outputVar };
            compileState.CheckVariableChanged(actionMethod, changedVariables);
        }

        public void InitializeVariableProperties(
            SerializedProperty inputsProperty, 
            SerializedProperty multiInputsProperty)
        {
            // variable initialized with name
            {
                string displayName = CyanTriggerNameHelpers.SanitizeName(
                    $"{CyanTriggerNameHelpers.GetCamelCase(_friendlyName)}{char.ToUpper(_friendlyType[0])}{_friendlyType.Substring(1)}");
                
                
                SerializedProperty inputProperty = inputsProperty.GetArrayElementAtIndex(1);
                SerializedProperty nameDataProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                CyanTriggerSerializableObject.UpdateSerializedProperty(nameDataProperty, displayName);
                
                SerializedProperty idProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
                idProperty.stringValue = Guid.NewGuid().ToString();
                
                SerializedProperty isVariableProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
                isVariableProperty.boolValue = true;
            }

            inputsProperty.serializedObject.ApplyModifiedProperties();
        }
    }
}