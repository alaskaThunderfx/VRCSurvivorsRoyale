using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;
using VRC.Udon.Common.Interfaces;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Cyan.CT.Editor
{
    [Flags]
    public enum CyanTriggerActionVariableTypeDefinition
    {
        None = 0,
        Constant = 1, // Value will be unchanged
        VariableInput = 1 << 1, // Value allows variables as input
        VariableOutput = 1 << 2, // Value allows variables to be saved as output
        Hidden = 1 << 3, // Variable will be hidden in the inspector and default value will be used
        AllowsMultiple = 1 << 4, // Allows you to have multiple of this variable. Only used for the first variable in the list.
        // Instanced = 1 << 5 // 
        
        // Event temporary? Only available during the event's lifetime

        // instance or persisted (Forced hidden and means that a new variable is created/copied per instance of this action)
        // allows multiple? (Why make this other than to copy previous UI)
    }
    
    [Serializable]
    public class CyanTriggerActionVariableDefinition
    {
        public CyanTriggerSerializableType type;
        public string udonName;
        public string displayName;
        public string description;
        public CyanTriggerSerializableObject defaultValue;
        
        public CyanTriggerActionVariableTypeDefinition variableType;

        
        public CyanTriggerActionVariableDefinition Clone()
        {
            return new CyanTriggerActionVariableDefinition
            {
                type = type,
                udonName = udonName,
                displayName = displayName,
                description = description,
                defaultValue = new CyanTriggerSerializableObject(defaultValue?.Obj),
                variableType = variableType
            };
        }
    }
    
    [Serializable]
    public class CyanTriggerActionDefinition
    {
        public string guid;
        [FormerlySerializedAs("actionName")] public string actionNamespace;
        public string actionVariantName;
        public string description;

        public CyanTriggerActionVariableDefinition[] variables;

        public string baseEventName;
        public string eventEntry;

        public bool autoAdd;

        public bool IsValid()
        {
            // TODO
            return false;
        }
        
        public bool IsEvent()
        {
            return baseEventName != "Event_Custom";
        }

        public string GetMethodName()
        {
            return $"{actionNamespace}.{actionVariantName}";
        }

        public string GetMethodSignature()
        {
            StringBuilder sb = new StringBuilder();

            //sb.Append(GetMethodName());
            sb.Append(actionVariantName);
            sb.Append('(');

            int count = 0;
            for (int curIn = 0; curIn < variables.Length; ++curIn)
            {
                var variable = variables[curIn];
                if ((variable.variableType & CyanTriggerActionVariableTypeDefinition.Hidden) != 0)
                {
                    continue;
                }

                ++count;
            }

            int added = 0;
            for (int curIn = 0; curIn < variables.Length; ++curIn)
            {
                var variable = variables[curIn];
                if ((variable.variableType & CyanTriggerActionVariableTypeDefinition.Hidden) != 0)
                {
                    continue;
                }
                
                if ((variable.variableType & CyanTriggerActionVariableTypeDefinition.VariableOutput) != 0)
                {
                    sb.Append("out ");
                }
                
                sb.Append(CyanTriggerNameHelpers.GetTypeFriendlyName(variable.type.Type));
                if (added + 1 < count)
                {
                    sb.Append(", ");
                }
                ++added;
            }

            sb.Append(')');

            return sb.ToString();
        }
    }
    
   
#if UNITY_EDITOR 
    public abstract class CyanTriggerActionGroupDefinition : 
        ScriptableObject, 
        IComparable<CyanTriggerActionGroupDefinition>
    {
        public const string EmptyEntryEventName = "__EmptyEntryEvent";
        public const string CustomEventName = "Event_Custom";

        public int autoAddPriority = 0;
        public CyanTriggerActionDefinition[] exposedActions = Array.Empty<CyanTriggerActionDefinition>();
        public bool isMultiInstance = false;
        public string defaultNamespace;

        [NonSerialized]
        private CyanTriggerActionVariableDefinition _instanceVariableDef = null;

        public abstract CyanTriggerAssemblyProgram GetCyanTriggerAssemblyProgram();

        public virtual void Initialize() { }

        public virtual bool DisplayExtraEditorOptions(SerializedObject obj)
        {
            return true;
        }
        
        public virtual void DisplayExtraMethodOptions(SerializedObject obj) { }
        
        public virtual bool IsEditorModifiable()
        {
            return false;
        }
        
        public virtual string GetNamespace()
        {
            if (string.IsNullOrEmpty(defaultNamespace))
            {
                return name;
            }
            return defaultNamespace;
        }

        public CyanTriggerActionVariableDefinition GetInstanceVariableDef()
        {
            if (_instanceVariableDef == null)
            {
                _instanceVariableDef = new CyanTriggerActionVariableDefinition
                {
                    udonName = "instance",
                    displayName = "instance",
                    variableType = CyanTriggerActionVariableTypeDefinition.VariableInput,
                    type = new CyanTriggerSerializableType(typeof(CyanTriggerActionGroupDefinition)),
                    defaultValue = new CyanTriggerSerializableObject()
                };
            }
            
            return _instanceVariableDef;
        }

        public CyanTriggerActionVariableDefinition[] GetVariablesForAction(CyanTriggerActionDefinition action)
        {
            var variables = action.variables;
            if (isMultiInstance)
            {
                int size = variables.Length;
                CyanTriggerActionVariableDefinition[] ret = new CyanTriggerActionVariableDefinition[size + 1];
                ret[0] = GetInstanceVariableDef();
                for (int index = 0; index < size; ++index)
                {
                    ret[index + 1] = variables[index];
                }
                return ret;
            }

            return variables;
        }

        public bool VerifyProgramActions(IUdonProgram program, ref string message)
        {
            HashSet<string> entryPoints = new HashSet<string>(program.EntryPoints.GetExportedSymbols());
            
            var symbolTable = program.SymbolTable;
            var symbols = symbolTable.GetSymbols();
            Dictionary<string, Type> variables = new Dictionary<string, Type>();

            foreach (string symbolName in symbols)
            {
                variables.Add(symbolName, symbolTable.GetSymbolType(symbolName));
            }
            
            foreach (var action in exposedActions)
            {
                if (!action.eventEntry.Equals(EmptyEntryEventName) 
                    && !entryPoints.Contains(action.eventEntry))
                {
                    message = $"Program does not contain event \"{action.eventEntry}\"";
                    return false;
                }
                
                foreach (var variable in action.variables)
                {
                    if (!variables.TryGetValue(variable.udonName, out Type type))
                    {
                        message = $"Program does not contain variable named \"{variable.udonName}\"";
                        return false;
                    }
                    if (variable.type.Type != type)
                    {
                        message = $"Variable named \"{variable.udonName}\" has different type than expected: {type} != {variable.type.Type}";
                    }
                }
            }
            
            return true;
        }

        public bool VerifyCustomAction(ref string message)
        {
            int autoAddCount = 0;
            foreach (var action in exposedActions)
            {
                bool isAutoAdded = action.autoAdd;
                if (isAutoAdded)
                {
                    ++autoAddCount;
                    foreach (var variable in action.variables)
                    {
                        if (variable.variableType != (CyanTriggerActionVariableTypeDefinition.Hidden
                                                      | CyanTriggerActionVariableTypeDefinition.Constant))
                        {
                            message = "Variable on auto added action must be set to hidden!";
                            return false;
                        }
                    }
                }
            }

            if (!isMultiInstance && autoAddCount > 0 && autoAddCount == exposedActions.Length)
            {
                message = "All actions are auto added!";
                return false;
            }

            return true;
        }

        public virtual void AddNewEvent(SerializedProperty eventListProperty, Action onAdded)
        {
            AddNewEvent(
                eventListProperty, 
                "name", 
                "variant", 
                CustomEventName, 
                "name");
            
            onAdded?.Invoke();
        }

        public static SerializedProperty AddNewEvent(
            SerializedProperty eventListProperty,
            string eventName, 
            string eventVariant,
            string baseEvent,
            string entryEvent,
            string guid = "",
            string description = "")
        {
            eventListProperty.arraySize++;
            SerializedProperty newActionProperty = eventListProperty.GetArrayElementAtIndex(eventListProperty.arraySize - 1);
                
            SerializedProperty guidProperty =
                newActionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.guid));
            SerializedProperty nameProperty =
                newActionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.actionNamespace));
            SerializedProperty actionVariantNameProperty =
                newActionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.actionVariantName));
            SerializedProperty baseEventProperty =
                newActionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.baseEventName));
            SerializedProperty entryEventProperty =
                newActionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.eventEntry));
            SerializedProperty descriptionProperty =
                newActionProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.description));
            SerializedProperty variablesProperty =
                newActionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.variables));
            SerializedProperty autoAddProperty =
                newActionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.autoAdd));

            // TODO register with manager to get a GUID instead of creating one here
            guidProperty.stringValue = !string.IsNullOrEmpty(guid) ? guid : Guid.NewGuid().ToString();
            nameProperty.stringValue = eventName;
            actionVariantNameProperty.stringValue = eventVariant;
            baseEventProperty.stringValue = baseEvent;
            descriptionProperty.stringValue = description;
            entryEventProperty.stringValue = entryEvent;

            autoAddProperty.boolValue = false;
            
            // TODO check base event and auto add variable. example: OnTriggerEnter(Collider other)
            variablesProperty.ClearArray();

            eventListProperty.serializedObject.ApplyModifiedProperties();
            
            return newActionProperty;
        }

        public virtual void AddNewVariable(int actionIndex, SerializedProperty variableListProperty, Action onAdded)
        {
            AddNewVariable(variableListProperty, "variable_name", "Variable Name", typeof(void));
            onAdded?.Invoke();
        }

        public static SerializedProperty AddNewVariable(
            SerializedProperty variableListProperty, 
            string variableName, 
            string displayName,
            Type varType,
            string description = "",
            CyanTriggerActionVariableTypeDefinition variableType = 
                CyanTriggerActionVariableTypeDefinition.Constant | CyanTriggerActionVariableTypeDefinition.VariableInput)
        {
            variableListProperty.arraySize++;
            SerializedProperty newVariableProperty = variableListProperty.GetArrayElementAtIndex(variableListProperty.arraySize - 1);
                
            SerializedProperty udonNameProperty =
                newVariableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.udonName));
            SerializedProperty displayNameProperty =
                newVariableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.displayName));
            SerializedProperty varDescriptionProperty =
                newVariableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.description));
            SerializedProperty typeProperty =
                newVariableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.type));
            SerializedProperty typeDefProperty =
                typeProperty.FindPropertyRelative(nameof(CyanTriggerSerializableType.typeDef));
            
            SerializedProperty typeOptionsProperty =
                newVariableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.variableType));
                

            udonNameProperty.stringValue = variableName;
            displayNameProperty.stringValue = displayName;
            varDescriptionProperty.stringValue = description;
            typeDefProperty.stringValue = varType.AssemblyQualifiedName;
            typeOptionsProperty.intValue = (int) (variableType);

            variableListProperty.serializedObject.ApplyModifiedProperties();

            return newVariableProperty;
        }

        public int CompareTo(CyanTriggerActionGroupDefinition other)
        {
            return autoAddPriority - other.autoAddPriority;
        }
    }
#endif
}