using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Compiler.Compilers;

namespace Cyan.CT.Editor
{
    public enum CyanTriggerErrorType
    {
        None,
        Warning,
        Error
    }
    
    public static class CyanTriggerUtil
    {
        public enum InvalidReason
        {
            Valid,
            IsNull,
            InvalidDefinition,
            InvalidInput,
            MissingVariable,
            DataIsNull,
            InputTypeMismatch,
            InputLengthMismatch,
        }

        public static bool ValidateTriggerData(CyanTriggerDataInstance dataInstance)
        {
            bool dirty = false;
            if (dataInstance == null || dataInstance.events == null)
            {
                return false;
            }
            
            // TODO if events are empty, create array. What are the potential issues of this? On data load fail, will this wipe the data?
            // TODO verify variables

            foreach (var eventData in dataInstance.events)
            {
                dirty |= ValidateVariables(eventData.eventInstance);
                foreach (var actionData in eventData.actionInstances)
                {
                    dirty |= ValidateVariables(actionData);
                }

                if (string.IsNullOrEmpty(eventData.eventId))
                {
                    eventData.eventId = Guid.NewGuid().ToString();
                    dirty = true;
                }
            }

            return dirty;
        }

        public static bool ValidateVariables(CyanTriggerActionInstance actionInstance)
        {
            if (actionInstance == null)
            {
                return false;
            }
            
            var actionInfoHolder = CyanTriggerActionInfoHolder.GetActionInfoHolder(actionInstance.actionType);
            if (!actionInfoHolder.IsValid() || actionInfoHolder.IsCustomEvent())
            {
                return false;
            }
            
            bool changed = false;
            var variables = actionInfoHolder.GetVariablesWithExtras(actionInstance, false);

            if (actionInstance.inputs == null)
            {
                actionInstance.inputs = new CyanTriggerActionVariableInstance[variables.Length];
                changed = true;
            }
            else if (variables.Length != actionInstance.inputs.Length)
            {
                changed = true;
                Array.Resize(ref actionInstance.inputs, variables.Length); 
            }

            bool firstAllowsMulti = 
                variables.Length > 0 &&
                (variables[0].variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0;
            
            for (int index = 0; index < variables.Length; ++index)
            {
                var variable = variables[index];
                if (variable == null)
                {
                    continue;
                }
                
                if (firstAllowsMulti && index == 0)
                {
                    // Fix issues where something went from not allowing multi-input to allowing multi-input.
                    var firstInput = actionInstance.inputs[0];
                    if (actionInstance.multiInput.Length == 0 && (firstInput.isVariable || firstInput.data.Obj != null))
                    {
                        actionInstance.multiInput = new[]
                        {
                            firstInput
                        };
                        actionInstance.inputs[0] = new CyanTriggerActionVariableInstance();
                        changed = true;
                    }
                    
                    foreach (var varInstance in actionInstance.multiInput)
                    {
                        changed |= ValidateVariable(varInstance, variable);
                    }
                    continue;
                }

                changed |= ValidateVariable(actionInstance.inputs[index], variable);
            }

            return changed;
        }

        public static bool ValidateVariable(
            CyanTriggerActionVariableInstance variableInstance,
            CyanTriggerActionVariableDefinition variableDefinition)
        {
            Type type = variableDefinition.type.Type;
            if (variableInstance == null || variableInstance.isVariable)
            {
                return false;
            }
            
            object data = variableInstance.data.Obj;
            
            // Check is required for the case when a destroyed object is still saved, but shouldn't be. 
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (typeof(UnityEngine.Object).IsAssignableFrom(type) 
                && data != null
                && (!(data is UnityEngine.Object dataObj) || dataObj == null))
            {
                variableInstance.data.Obj = null;
                return true;
            }
            
            bool changes = false;
            if (type == typeof(UdonBehaviour) && data is CyanTrigger trigger)
            {
                changes = true;
                data = variableInstance.data.Obj = trigger.triggerInstance?.udonBehaviour;
            }

            return changes;
        }
        
        
        public static CyanTriggerErrorType IsValid(
            CyanTriggerActionInstance actionInstance, 
            CyanTriggerDataInstance triggerData, 
            ref string message)
        {
            if (actionInstance == null)
            {
                message = InvalidReason.IsNull.ToString();
                return CyanTriggerErrorType.Error;
            }
            
            var actionInfoHolder = CyanTriggerActionInfoHolder.GetActionInfoHolder(actionInstance.actionType);
            if (!actionInfoHolder.IsValid())
            {
                message = InvalidReason.InvalidDefinition.ToString();
                return CyanTriggerErrorType.Error;
            }

            if (actionInfoHolder.IsHidden())
            {
                message = "Action is Hidden";
                return CyanTriggerErrorType.Error;
            }
            
            if (actionInfoHolder.CustomDefinition is ICyanTriggerCustomNodeValidator validator)
            {
                var result = validator.Validate(actionInstance, triggerData, ref message);
                if (result != CyanTriggerErrorType.None)
                {
                    return result;
                }
            }

            if (actionInfoHolder.IsCustomEvent())
            {
                return CyanTriggerErrorType.None;
            }
            
            var variables = actionInfoHolder.GetVariablesWithExtras(actionInstance, false);

            if (variables.Length != actionInstance.inputs.Length)
            {
#if CYAN_TRIGGER_DEBUG
                Debug.LogWarning("Input length did not equal variable def length. This shouldn't happen.");
#endif
                message = InvalidReason.InputLengthMismatch.ToString();
                return CyanTriggerErrorType.Error;
            }
            
            bool firstAllowsMulti = 
                variables.Length > 0 &&
                (variables[0].variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0;
            if (firstAllowsMulti && actionInstance.multiInput.Length == 0)
            {
                message = "Action has no targets and will do nothing.";
                return CyanTriggerErrorType.Warning;
            }
            
            // TODO verify inputs match definition
            for (int input = 0; input < variables.Length; ++input)
            {
                if (variables[input] == null)
                {
                    continue;
                }
                
                InvalidReason reason;
                if (input == 0 && firstAllowsMulti)
                {
                    if (actionInstance.multiInput.Length == 0)
                    {
                        message = InvalidReason.InvalidInput.ToString();
                        return CyanTriggerErrorType.Error;
                    }
                    
                    foreach (var variable in actionInstance.multiInput)
                    {
                        reason = variable.IsValid(variables[input]);
                        if (reason != InvalidReason.Valid)
                        {
                            message = $"{InvalidReason.InvalidInput} - {reason}";
                            return CyanTriggerErrorType.Error;
                        }
                    }
                    continue;
                }
                
                // If a variable that isn't the first allows multiple, something is broken.
                if ((variables[input].variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0)
                {
                    message = InvalidReason.InvalidDefinition.ToString();
                    return CyanTriggerErrorType.Error;
                }
                
                reason = actionInstance.inputs[input].IsValid(variables[input]);
                if (reason != InvalidReason.Valid)
                {
                    message = $"{InvalidReason.InvalidInput} - {reason}";
                    return CyanTriggerErrorType.Error;
                }
            }

            return CyanTriggerErrorType.None;
        }

        public static InvalidReason IsValid(
            this CyanTriggerActionVariableInstance variableInstance, 
            CyanTriggerActionVariableDefinition variableDef = null)
        {
            if (variableInstance == null)
            {
                return InvalidReason.IsNull;
            }
            
            // TODO Check other cases
            
            return InvalidReason.Valid;
        }

        public static bool DisplayInInspector(this CyanTriggerVariable variable)
        {
            if (!variable.showInInspector || variable.typeInfo == CyanTriggerVariableType.SectionEnd)
            {
                return false;
            }
            
            if (string.IsNullOrEmpty(variable.name))
            {
                return false;
            }
            
            return !variable.name.StartsWith(UdonGraphCompiler.INTERNAL_VARIABLE_PREFIX);
        }

        public static bool ShouldVariableDisplayInInspector(SerializedProperty variableProperty)
        {
            SerializedProperty showProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.showInInspector));
            SerializedProperty typeInfoProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.typeInfo));
            CyanTriggerVariableType typeInfo = (CyanTriggerVariableType)typeInfoProperty.enumValueIndex;
            
            if (!showProperty.boolValue
                || typeInfo == CyanTriggerVariableType.SectionEnd)
            {
                return false;
            }
            
            SerializedProperty nameProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.name));
            string varName = nameProperty.stringValue;
            if (string.IsNullOrEmpty(varName))
            {
                return false;
            }

            return !varName.StartsWith(UdonGraphCompiler.INTERNAL_VARIABLE_PREFIX);
        }

        public static bool IsDisplayOnly(this CyanTriggerVariable variable)
        {
            return variable.typeInfo == CyanTriggerVariableType.SectionStart
                   || variable.typeInfo == CyanTriggerVariableType.SectionEnd;
        }
        
        public static bool IsVariableDisplayOnly(SerializedProperty variableProperty)
        {
            SerializedProperty typeInfoProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.typeInfo));
            CyanTriggerVariableType typeInfo = (CyanTriggerVariableType)typeInfoProperty.enumValueIndex;
            
            return typeInfo == CyanTriggerVariableType.SectionStart
                   || typeInfo == CyanTriggerVariableType.SectionEnd;
        }
        

        public static bool HasSyncedVariables(this CyanTriggerDataInstance data)
        {
            foreach (var variable in data.variables)
            {
                if (variable.IsDisplayOnly())
                {
                    continue;
                }
                
                if (variable.sync != CyanTriggerVariableSyncMode.NotSynced)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool GameObjectRequiresContinuousSync(UdonBehaviour thisUdon)
        {
            if (thisUdon == null)
            {
                return false;
            }
            
            UdonBehaviour[] behaviours = thisUdon.GetComponents<UdonBehaviour>();
            VRCObjectSync[] syncs = thisUdon.GetComponents<VRCObjectSync>();

            bool positionSynced = syncs.Length > 0;
            foreach (var udon in behaviours)
            {
#pragma warning disable 618
                positionSynced |= udon.SynchronizePosition;
#pragma warning restore 618
            }

            return positionSynced;
        }

        // TODO remove when dropping 2018 support.
#if !UNITY_2019_4_OR_NEWER
        // Check if there are any continuous synced udon behaviours on the object.
        public static bool GetObjectSyncMethod(UdonBehaviour thisUdon)
        {
            if (thisUdon == null) 
            {
                return true;
            }

            bool manual = true;
            UdonBehaviour[] behaviours = thisUdon.GetComponents<UdonBehaviour>();
            foreach (var udon in behaviours)
            {
                if (udon == thisUdon)
                {
                    continue;
                }

                manual &= udon.Reliable;
            }

            return manual;
        }
        
        // Currently returns if this CyanTrigger should be Reliable (manual) sync.
        public static bool GetSyncMode(CyanTriggerDataInstance instance, UdonBehaviour thisUdon)
        {
            if (!instance.autoSetSyncMode)
            {
                switch (instance.programSyncMode)
                {
                    case CyanTriggerProgramSyncMode.Continuous:
                        return false;
                    case CyanTriggerProgramSyncMode.Manual:
                    case CyanTriggerProgramSyncMode.ManualWithAutoRequest:
                    default:
                        return true;
                }
            }
            
            // If the CyanTrigger has any synced variables, then use the Sync Method set by the user.
            if (instance.HasSyncedVariables())
            {
                return instance.programSyncMode != CyanTriggerProgramSyncMode.Continuous;
            }

            // CyanTrigger doesn't care what sync method the udon behaviour is set to since there are no synced
            // variables. Determine the best option.
            return !GameObjectRequiresContinuousSync(thisUdon) && GetObjectSyncMethod(thisUdon);
        }
        
#else
        // Check if there are any udon behaviours on the object and return their sync method, assuming this the
        // object's sync method.
        public static Networking.SyncType GetObjectSyncMethod(UdonBehaviour thisUdon)
        {
            if (thisUdon == null)
            {
                return Networking.SyncType.Unknown;
            }
            
            UdonBehaviour[] behaviours = thisUdon.GetComponents<UdonBehaviour>();
            foreach (var udon in behaviours)
            {
                if (udon == thisUdon 
                    || udon.SyncMethod == Networking.SyncType.None 
                    || udon.SyncMethod == Networking.SyncType.Unknown)
                {
                    continue;
                }

                return udon.SyncMethod;
            }

            return Networking.SyncType.Unknown;
        }

        public static Networking.SyncType GetSyncMode(CyanTriggerDataInstance instance, UdonBehaviour thisUdon, bool shouldBeNetworked)
        {
            // If not auto-setting, just use what the user has selected.
            if (!instance.autoSetSyncMode)
            {
                switch (instance.programSyncMode)
                {
                    case CyanTriggerProgramSyncMode.Continuous:
                        return Networking.SyncType.Continuous;
                    case CyanTriggerProgramSyncMode.Manual:
                    case CyanTriggerProgramSyncMode.ManualWithAutoRequest:
                        return Networking.SyncType.Manual;
                    case CyanTriggerProgramSyncMode.None:
                        return Networking.SyncType.None;
                    default:
                        return Networking.SyncType.Unknown;
                }
            }

            if (!shouldBeNetworked)
            {
                return Networking.SyncType.None;
            }
            
            // Sync method already set by other udon behaviours, use that instead.
            Networking.SyncType syncMethod = GetObjectSyncMethod(thisUdon);
            if (syncMethod != Networking.SyncType.Unknown)
            {
                return syncMethod;
            }
            
            // This object is position synced, and is required to be continuous.
            if (GameObjectRequiresContinuousSync(thisUdon))
            {
                return Networking.SyncType.Continuous;
            }

            return Networking.SyncType.Manual;
        }
#endif

        public static List<CyanTriggerActionGroupDefinition> GetCustomActionDependencies(
            CyanTriggerDataInstance triggerData)
        {
            HashSet<CyanTriggerActionGroupDefinition> actionGroupDefinitions = 
                new HashSet<CyanTriggerActionGroupDefinition>();
            
            foreach (var trigEvent in triggerData.events)
            {
                var eventType = trigEvent.eventInstance.actionType;
                if (!string.IsNullOrEmpty(eventType.guid) &&
                    CyanTriggerActionGroupDefinitionUtil.Instance.TryGetActionGroupDefinition(
                        eventType.guid, out var actionGroupDefinition))
                {
                    actionGroupDefinitions.Add(actionGroupDefinition);
                }

                foreach (var actionInstance in trigEvent.actionInstances)
                {
                    var actionType = actionInstance.actionType;
                    if (!string.IsNullOrEmpty(actionType.guid) &&
                        CyanTriggerActionGroupDefinitionUtil.Instance.TryGetActionGroupDefinition(
                            actionType.guid, out actionGroupDefinition))
                    {
                        actionGroupDefinitions.Add(actionGroupDefinition);
                    }
                }
            }

            return new List<CyanTriggerActionGroupDefinition>(actionGroupDefinitions);
        }
        
        internal static string Colorize(this string text, Color color, bool shouldColor = true)
        {
            if (!shouldColor)
            {
                return text;
            }
            
            // Prevent rich text tags from being interpreted.
            if (text.IndexOf('<') != -1)
            {
                // Unicode value is for zero width space. 
                text = text.Replace("<", "<\u200B");
            }
            
            // Darken the input color if gui is disabled currently.
            if (!GUI.enabled || Application.isPlaying)
            {
                color *= 0.75f;
            }

            // Converting to html can have a cost. Black color is special and should speed up call.
            string htmlColor = color == Color.black 
                ? "000000"
                : ColorUtility.ToHtmlStringRGB(color);
            
            return $"<color=#{htmlColor}>{text}</color>";
        }
        
        internal static string Colorize(this string text, CyanTriggerColorTheme color, bool shouldColor)
        {
            if (!shouldColor)
            {
                return text;
            }
            
            return Colorize(text, CyanTriggerSettings.Instance.GetColorTheme().GetColor(color), true);
        }
        
        internal static string Colorize(this string text, CyanTriggerColorTheme color)
        {
            var settings = CyanTriggerSettings.Instance;
            if (!settings.useColorThemes)
            {
                return text;
            }
            
            return Colorize(text, settings.GetColorTheme().GetColor(color), true);
        }
        
        
        public static CyanTriggerEventArgData CreateEventArgData(string eventName, string eventDisplayName, CyanTriggerEditorVariableOption[] eventParams)
        {
            List<string> paramNames = new List<string>(eventParams.Length);
            List<string> paramUdonNames = new List<string>(eventParams.Length);
            List<Type> paramTypes = new List<Type>(eventParams.Length);
            List<bool> paramOutputs = new List<bool>(eventParams.Length);

            foreach (var eventParam in eventParams)
            {
                paramNames.Add(eventParam.Name);
                paramTypes.Add(eventParam.Type);
                paramOutputs.Add(!eventParam.IsInput);
                paramUdonNames.Add(eventParam.UdonName);
            }
                        
            return new CyanTriggerEventArgData
            {
                eventName = eventName,
                eventDisplayName = eventDisplayName,
                variableNames = paramNames.ToArray(),
                variableUdonNames = paramUdonNames.ToArray(),
                variableTypes = paramTypes.ToArray(),
                variableOuts = paramOutputs.ToArray(),
            };
        }
        
        public static CyanTriggerEventArgData CreateEventArgData(string eventName, string eventDisplayName, CyanTriggerActionVariableDefinition[] eventParams)
        {
            List<string> paramNames = new List<string>(eventParams.Length);
            List<string> paramUdonNames = new List<string>(eventParams.Length);
            List<Type> paramTypes = new List<Type>(eventParams.Length);
            List<bool> paramOutputs = new List<bool>(eventParams.Length);

            foreach (var eventParam in eventParams)
            {
                // Only show hidden items. Visible items here represent Custom Action parameters which should be ignored.
                if ((eventParam.variableType & CyanTriggerActionVariableTypeDefinition.Hidden) == 0)
                {
                    continue;
                }
                
                paramNames.Add(eventParam.displayName);
                paramTypes.Add(eventParam.type.Type);
                paramOutputs.Add((eventParam.variableType & CyanTriggerActionVariableTypeDefinition.VariableOutput) != 0);
                paramUdonNames.Add(eventParam.udonName);
            }
                        
            return new CyanTriggerEventArgData
            {
                eventName = eventName,
                eventDisplayName = eventDisplayName,
                variableNames = paramNames.ToArray(),
                variableUdonNames = paramUdonNames.ToArray(),
                variableTypes = paramTypes.ToArray(),
                variableOuts = paramOutputs.ToArray(),
            };
        }
    }
}
