using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerActionInfoHolder
    {
        private const string InvalidString = "<Invalid>";
        
        private static readonly CyanTriggerActionInfoHolder InvalidAction;
        private static readonly Dictionary<string, CyanTriggerActionInfoHolder> CustomActions =
            new Dictionary<string, CyanTriggerActionInfoHolder>();
        private static readonly Dictionary<string, CyanTriggerActionInfoHolder> DefinitionActions =
            new Dictionary<string, CyanTriggerActionInfoHolder>();
        
        private static readonly object Lock = new object();
        
        public readonly CyanTriggerNodeDefinition Definition;
        public readonly CyanTriggerActionDefinition Action;
        public readonly CyanTriggerActionGroupDefinition ActionGroup;
        public readonly CyanTriggerNodeDefinition BaseDefinition;
        private bool _isInvalid;

        public CyanTriggerCustomUdonNodeDefinition CustomDefinition => Definition?.CustomDefinition;
        public CyanTriggerNodeDefinition DefOrBaseDef => Definition ?? BaseDefinition;

        static CyanTriggerActionInfoHolder()
        {
            InvalidAction = new CyanTriggerActionInfoHolder();
        }

        #region Static ActionInfoHolder Getters

        public static CyanTriggerActionInfoHolder GetActionInfoHolder(CyanTriggerActionType actionType)
        {
            return GetActionInfoHolder(actionType.guid, actionType.directEvent);
        }
        
        public static CyanTriggerActionInfoHolder GetActionInfoHolder(string guid, string directEvent)
        {
            if (!string.IsNullOrEmpty(guid))
            {
                return GetActionInfoHolderFromGuid(guid);
            }
            if (!string.IsNullOrEmpty(directEvent))
            {
                return GetActionInfoHolderFromDefinition(directEvent);
            }

            return InvalidAction;
        }

        public static CyanTriggerActionInfoHolder GetActionInfoHolder(CyanTriggerSettingsFavoriteItem favoriteItem)
        {
            var actionType = favoriteItem.data;
            return GetActionInfoHolder(actionType.guid, actionType.directEvent);
        }
        
        private static CyanTriggerActionInfoHolder GetActionInfoHolderFromGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return InvalidAction;
            }
            
            // Force instance to be created here if this is the first time.
            var actionGroupDefinitionUtilInstance = CyanTriggerActionGroupDefinitionUtil.Instance;

            lock (Lock)
            {
                if (CustomActions.TryGetValue(guid, out var actionInfo))
                {
                    if (actionInfo.IsValid())
                    {
                        return actionInfo;
                    }

                    CustomActions.Remove(guid);
                }

                if (!actionGroupDefinitionUtilInstance
                        .TryGetActionDefinition(guid, out CyanTriggerActionDefinition actionDef))
                {
                    return InvalidAction;
                }

                if (!actionGroupDefinitionUtilInstance.TryGetActionGroupDefinition(actionDef, out var actionGroup))
                {
                    return InvalidAction;
                }

                // Action will always be invalid. Do not create a new one and return the old one.
                if (actionGroup == null)
                {
                    return actionInfo;
                }
                
                actionInfo = new CyanTriggerActionInfoHolder(actionDef, actionGroup);
                CustomActions.Add(guid, actionInfo);

                return actionInfo;
            }
        }

        public static CyanTriggerActionInfoHolder GetActionInfoHolder(CyanTriggerActionDefinition actionDef)
        {
            if (actionDef == null || string.IsNullOrEmpty(actionDef.guid))
            {
                return InvalidAction;
            }
            
            // Force instance to be created here if this is the first time.
            var actionGroupDefinitionUtilInstance = CyanTriggerActionGroupDefinitionUtil.Instance;

            lock (Lock)
            {
                if (CustomActions.TryGetValue(actionDef.guid, out var actionInfo))
                {
                    if (actionInfo.IsValid())
                    {
                        return actionInfo;
                    }
                    CustomActions.Remove(actionDef.guid);
                }

                if (!actionGroupDefinitionUtilInstance.TryGetActionGroupDefinition(actionDef,
                        out var actionGroup))
                {
                    return InvalidAction;
                }

                // Action will always be invalid. Do not create a new one and return the old one.
                if (actionGroup == null)
                {
                    return actionInfo;
                }
                
                actionInfo = new CyanTriggerActionInfoHolder(actionDef, actionGroup);
                CustomActions.Add(actionDef.guid, actionInfo);

                return actionInfo;
            }
        }
        
        public static CyanTriggerActionInfoHolder GetActionInfoHolder(
            CyanTriggerActionDefinition actionDef, 
            CyanTriggerActionGroupDefinition actionGroup)
        {
            if (actionDef == null || string.IsNullOrEmpty(actionDef.guid))
            {
                return InvalidAction;
            }
            
            if (actionGroup == null && !CyanTriggerActionGroupDefinitionUtil.Instance
                    .TryGetActionGroupDefinition(actionDef, out actionGroup))
            {
                return InvalidAction;
            }
            
            lock (Lock)
            {
                if (CustomActions.TryGetValue(actionDef.guid, out var actionInfo))
                {
                    if (actionInfo.IsValid())
                    {
                        return actionInfo;
                    }

                    CustomActions.Remove(actionDef.guid);
                }

                actionInfo = new CyanTriggerActionInfoHolder(actionDef, actionGroup);
                CustomActions.Add(actionDef.guid, actionInfo);

                return actionInfo;
            }
        }
        
        public static CyanTriggerActionInfoHolder GetActionInfoHolder(UdonNodeDefinition definition)
        {
            if (definition == null)
            {
                return InvalidAction;
            }

            return GetActionInfoHolderFromDefinition(definition.fullName);
        }
        
        public static CyanTriggerActionInfoHolder GetActionInfoHolder(CyanTriggerNodeDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.FullName))
            {
                return InvalidAction;
            }

            lock (Lock)
            {
                string key = definition.FullName;
                if (DefinitionActions.TryGetValue(key, out var actionInfo))
                {
                    if (actionInfo.IsValid())
                    {
                        return actionInfo;
                    }

                    DefinitionActions.Remove(key);
                }

                actionInfo = new CyanTriggerActionInfoHolder(definition);
                DefinitionActions.Add(key, actionInfo);

                return actionInfo;
            }
        }
        
        private static CyanTriggerActionInfoHolder GetActionInfoHolderFromDefinition(string definition)
        {
            if (string.IsNullOrEmpty(definition))
            {
                return InvalidAction;
            }

            lock (Lock)
            {
                if (DefinitionActions.TryGetValue(definition, out var actionInfo))
                {
                    if (actionInfo.IsValid())
                    {
                        return actionInfo;
                    }

                    DefinitionActions.Remove(definition);
                }

                var def = CyanTriggerNodeDefinitionManager.Instance.GetDefinition(definition);
                if (def == null)
                {
                    return InvalidAction;
                }

                actionInfo = new CyanTriggerActionInfoHolder(def);
                DefinitionActions.Add(definition, actionInfo);

                return actionInfo;
            }
        }
        
        public static CyanTriggerActionInfoHolder GetActionInfoHolderFromProperties(SerializedProperty actionProperty)
        {
            SerializedProperty actionTypeProperty =
                actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.actionType));
            SerializedProperty directEvent =
                actionTypeProperty.FindPropertyRelative(nameof(CyanTriggerActionType.directEvent));
            SerializedProperty guidProperty =
                actionTypeProperty.FindPropertyRelative(nameof(CyanTriggerActionType.guid));

            return GetActionInfoHolder(guidProperty.stringValue, directEvent.stringValue);
        }
        
        public static bool TryGetCachedInfoFromCustomActionGuid(string guid, out CyanTriggerActionInfoHolder actionInfo)
        {
            lock (Lock)
            {
                return CustomActions.TryGetValue(guid, out actionInfo);
            }
        }
        
        #endregion

        public static void RemoveCustomActionInfo(string guid)
        {
            lock (Lock)
            {
                if (CustomActions.TryGetValue(guid, out var actionInfo))
                {
                    actionInfo.InvalidateActionInfo();
                    CustomActions.Remove(guid);
                }
            }
        }

        private CyanTriggerActionInfoHolder()
        {
            _isInvalid = true;
        }

        private CyanTriggerActionInfoHolder(CyanTriggerNodeDefinition definition)
        {
            Definition = definition;
        }

        private CyanTriggerActionInfoHolder(
            CyanTriggerActionDefinition action,
            CyanTriggerActionGroupDefinition actionGroup)
        {
            Action = action;
            ActionGroup = actionGroup;
            
            if (ActionGroup == null)
            {
                CyanTriggerActionGroupDefinitionUtil.Instance.TryGetActionGroupDefinition(action, out ActionGroup);
            }

            if (Action != null)
            {
                BaseDefinition = CyanTriggerNodeDefinitionManager.Instance.GetDefinition(Action.baseEventName);
            }
        }

        public CyanTriggerActionType GetActionType()
        {
            return new CyanTriggerActionType
            {
                guid = Action?.guid,
                directEvent = Definition?.FullName
            };
        }

        public string GetDisplayName(bool withColor = false, bool forActionDisplay = false)
        {
            if (Definition != null)
            {
                return Definition.GetActionDisplayName(withColor);
            }

            if (Action != null)
            {
                string actionName = Action.actionNamespace;
                // Only display the * when displayed in the main action inspector.
                if (forActionDisplay)
                {
                    actionName = $"{Action.actionNamespace}*";
                }
                return actionName.Colorize(CyanTriggerColorTheme.CustomActionName, withColor);
            }

            return InvalidString.Colorize(CyanTriggerColorTheme.Error, withColor);
        }

        public string GetVariantName()
        {
            if (Definition != null)
            {
                return "VRC_Direct";
            }

            if (Action != null)
            {
                return Action.actionVariantName;
            }

            return InvalidString;
        }

        public string GetActionRenderingDisplayName(bool withColor = false, bool forActionDisplay = false)
        {
            string displayName = GetDisplayName(withColor, forActionDisplay);

            if (Definition != null)
            {
                return displayName;
            }
            
            string period = ".".Colorize(CyanTriggerColorTheme.Punctuation, withColor);
            CyanTriggerColorTheme color = IsValid() ? CyanTriggerColorTheme.ActionName : CyanTriggerColorTheme.Error;
            return $"{displayName}{period}{GetVariantName().Colorize(color, withColor)}";
        }

        public string GetMethodSignature()
        {
            if (Definition != null)
            {
                return Definition.GetMethodDisplayName();
            }
            
            if (Action != null)
            {
                return Action.GetMethodSignature();
            }
            
            return InvalidString;
        }

        public bool HasDocumentationLink()
        {
            if (!IsValid())
            {
                return false;
            }
            
            if (Definition == null)
            {
                return false;
            }

            return Definition.HasDocumentation();
        }

        public string GetDocumentationLink()
        {
            if (!IsValid())
            {
                return string.Empty;
            }
            
            if (Definition == null)
            {
                return string.Empty;
            }
            
            return Definition.GetDocumentationLink();
        }

        public string GetEventCompiledName(SerializedProperty eventProperty)
        {
            if (!IsEvent())
            {
                return "";
            }
            
            if (IsCustomEvent())
            {
                return eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.name)).stringValue;
            }
            if (CustomDefinition != null)
            {
                return CustomDefinition.GetBaseMethodName(eventProperty);
            }
            
            var def = DefOrBaseDef;
            string name = def.Definition.name;
            return $"_{char.ToLower(name[0])}{name.Substring(1)}";
        }
        
        public string GetEventCompiledName(CyanTriggerEvent evt)
        {
            if (!IsEvent())
            {
                return "";
            }

            if (IsCustomEvent())
            {
                return evt.name;
            }
            if (CustomDefinition != null)
            {
                return CustomDefinition.GetBaseMethodName(evt);
            }
            
            var def = DefOrBaseDef;
            string name = def.Definition.name;
            return $"_{char.ToLower(name[0])}{name.Substring(1)}";
        }

        private void InvalidateActionInfo()
        {
            _isInvalid = true;
        }
        
        public bool IsValid()
        {
            return !_isInvalid && (IsDefinitionInternal() || IsActionInternal());
        }

        // Internal version to prevent infinite looping between calling IsAction and IsValid.
        private bool IsActionInternal()
        {
            return Action != null && ActionGroup != null;
        }
        
        // Internal version to prevent infinite looping between calling IsDefinition and IsValid.
        private bool IsDefinitionInternal()
        {
            return Definition != null;
        }

        public bool IsAction()
        {
            if (!IsValid())
            {
                return false;
            }
            
            return IsActionInternal();
        }
        
        public bool IsDefinition()
        {
            if (!IsValid())
            {
                return false;
            }
            
            return IsDefinitionInternal();
        }

        public bool IsEvent()
        {
            if (!IsValid())
            {
                return false;
            }
            
            if (Action != null)
            {
                return BaseDefinition.FullName != CyanTriggerActionGroupDefinition.CustomEventName;
            }

            return Definition.DefinitionType == CyanTriggerNodeDefinition.UdonDefinitionType.Event;
        }

        public bool IsCustomEvent()
        {
            if (!IsValid())
            {
                return false;
            }
            
            return Definition != null && Definition.FullName == CyanTriggerActionGroupDefinition.CustomEventName;
        }

        public bool IsHidden()
        {
            if (!IsValid())
            {
                return false;
            }
            
            if (Action == null)
            {
                return false;
            }

            return Action.autoAdd;
        }

        public bool Equals(CyanTriggerActionInfoHolder o)
        {
            return 
                o != null 
                && Definition == o.Definition 
                && Action == o.Action 
                && ActionGroup == o.ActionGroup;
        }

        public List<string> GetEventWarnings()
        {
            if (Definition == null)
            {
                return null;
            }

            List<string> warningMessages = new List<string>();
            switch (Definition.FullName)
            {
                // case "Event_OnPlayerJoined":
                // case "Event_OnPlayerLeft":
                //     warningMessages.Add("On Player Joined and Left events will fire when any player enters or leaves the instance. If you want local or remote players only, switch from \"VRC_Direct\" to the appropriate Event Variant.");
                //     break;
                
                case "Event_OnStationEntered":
                case "Event_OnStationExited":
                    warningMessages.Add("On Station Entered and Exited events will fire when any player enters or exits the station. If you want local or remote players only, switch from \"VRC_Direct\" to the appropriate Event Variant.");
                    break;
                    
                case "Event_OnPlayerTriggerEnter":
                case "Event_OnPlayerTriggerStay":
                case "Event_OnPlayerTriggerExit":
                    warningMessages.Add("On Player Trigger Enter, Stay, and Exit events will fire for all players. If you want local or remote players only, switch from \"VRC_Direct\" to the appropriate Event Variant.");
                    break;
                
                case "Event_OnPlayerCollisionEnter":
                case "Event_OnPlayerCollisionStay":
                case "Event_OnPlayerCollisionExit":
                    warningMessages.Add("On Player Collision Enter, Stay, and Exit events will fire for all players. If you want local or remote players only, switch from \"VRC_Direct\" to the appropriate Event Variant.");
                    break;
                
                case "Event_OnPlayerParticleCollision":
                    warningMessages.Add("On Player Particle Collision will fire when any player is hit by a particle. If you want local or remote players only, switch from \"VRC_Direct\" to the appropriate Event Variant.");
                    break;
                
                case "Event_OnTriggerEnter":
                case "Event_OnTriggerStay":
                case "Event_OnTriggerExit":
                    warningMessages.Add("On Trigger Enter, Stay, and Exit events will fire for all objects. If you want to limit the types of objects that can trigger this event, switch from \"VRC_Direct\" to the appropriate Event Variant.");
                    break;
                
                case "Event_OnCollisionEnter":
                case "Event_OnCollisionStay":
                case "Event_OnCollisionExit":
                    warningMessages.Add("On Collision Enter, Stay, and Exit events will fire for all objects. If you want to limit the types of objects that can trigger this event, switch from \"VRC_Direct\" to the appropriate Event Variant.");
                    break;
                
                case "Event_OnParticleCollision":
                    warningMessages.Add("On Particle Collision will fire when any non player object is hit by a particle. If you want to limit the types of objects that can trigger this event, switch from \"VRC_Direct\" to the appropriate Event Variant.");
                    break;

                case "Event_InputUse":
                case "Event_InputGrab":
                case "Event_InputDrop":
                case "Event_InputJump":
                    warningMessages.Add("VRChat Input events will fire when the input button is pressed and when the button is released for either hand. If you only want button down, button up, or only a specific hand, switch from \"VRC_Direct\" to the appropriate Event Variant.");
                    break;
            }

            // TODO add more checks for different warning types.
            
            // TODO if base event is update, don't allow non local broadcast.

            return warningMessages;
        }

        public CyanTriggerActionVariableDefinition[] GetVariablesWithExtras(
            SerializedProperty actionProperty, 
            bool includeEventVariables)
        {
            if (!IsValid())
            {
                return Array.Empty<CyanTriggerActionVariableDefinition>();
            }
            
            if (CustomDefinition is ICyanTriggerCustomNodeCustomVariableInputSize customInputSize)
            {
                return customInputSize.GetExtraVariables(actionProperty, includeEventVariables);
            }
            if (IsCustomEvent())
            {
                var inputProp = actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
                if (inputProp.arraySize == 1)
                {
                    return new[] { new CyanTriggerActionVariableDefinition() };
                }
                return Array.Empty<CyanTriggerActionVariableDefinition>();
            }
            return GetBaseActionVariables(includeEventVariables);
        }
        
        public CyanTriggerActionVariableDefinition[] GetVariablesWithExtras(
            CyanTriggerActionInstance actionInstance,
            bool includeEventVariables)
        {
            if (!IsValid())
            {
                return Array.Empty<CyanTriggerActionVariableDefinition>();
            }
            
            if (CustomDefinition is ICyanTriggerCustomNodeCustomVariableInputSize customInputSize)
            {
                return customInputSize.GetExtraVariables(actionInstance, includeEventVariables);
            }
            if (IsCustomEvent())
            {
                if (actionInstance.inputs.Length == 1)
                {
                    return new[] { new CyanTriggerActionVariableDefinition() };
                }
                return Array.Empty<CyanTriggerActionVariableDefinition>();
            }
            return GetBaseActionVariables(includeEventVariables);
        }

        public CyanTriggerActionVariableDefinition[] GetCustomActionVariables()
        {
            List<CyanTriggerActionVariableDefinition> variables = new List<CyanTriggerActionVariableDefinition>();
            
            if (ActionGroup != null && ActionGroup.isMultiInstance)
            {
                variables.Add(ActionGroup.GetInstanceVariableDef());
            }
            
            bool isEvent = IsEvent();
            foreach (var variable in Action.variables)
            {
                if (isEvent && (variable.variableType & CyanTriggerActionVariableTypeDefinition.VariableOutput) != 0)
                {
                    // Always show event variables for custom actions as these are required
                    // for creating new output variable guids. 
                    var vari = variable.Clone();
                    vari.variableType |= CyanTriggerActionVariableTypeDefinition.Hidden;
                    variables.Add(vari);
                }
                else
                {
                    variables.Add(variable);
                }
            }

            return variables.ToArray();
        }
        
        public CyanTriggerActionVariableDefinition[] GetBaseActionVariables(bool includeEventVariables)
        {
            if (!IsValid())
            {
                return Array.Empty<CyanTriggerActionVariableDefinition>();
            }
            
            List<CyanTriggerActionVariableDefinition> variables = new List<CyanTriggerActionVariableDefinition>();
            
            if (Action != null)
            {
                variables.AddRange(GetCustomActionVariables());
            }
            
            var def = DefOrBaseDef;
            if (def == null || def.FullName == CyanTriggerActionGroupDefinition.CustomEventName)
            {
                return variables.ToArray();
            }
            variables.AddRange(def.VariableDefinitions);

            if (includeEventVariables && def.DefinitionType == CyanTriggerNodeDefinition.UdonDefinitionType.Event)
            {
                foreach ((string name, Type type) in def.GetEventVariables())
                {
                    variables.Add(new CyanTriggerActionVariableDefinition
                    {
                        type = new CyanTriggerSerializableType(type),
                        displayName = name,
                        udonName = name,
                        variableType = 
                            CyanTriggerActionVariableTypeDefinition.Hidden 
                            | (type == typeof(CyanTriggerVariable) 
                                ? CyanTriggerActionVariableTypeDefinition.VariableInput 
                                : CyanTriggerActionVariableTypeDefinition.Constant)
                    });
                }
            }

            return variables.ToArray();
        }
        
        public CyanTriggerEventArgData GetBaseEventArgData(CyanTriggerEvent ctEvent = null)
        {
            if (!IsValid())
            {
                return null;
            }

            string eventName = GetEventCompiledName(ctEvent);
            
            // prevent allowing users to set variable data for changed events.
            if (CustomDefinition is CyanTriggerCustomNodeOnVariableChanged)
            {
                return new CyanTriggerEventArgData { eventName = eventName };
            }

            if (IsCustomEvent())
            {
                if (ctEvent == null)
                {
                    return new CyanTriggerEventArgData { eventName = eventName, eventDisplayName = eventName};
                }
                
                var eventParams = GetCustomEventArgumentOptions(ctEvent, false);
                return CyanTriggerUtil.CreateEventArgData(eventName, eventName, eventParams);
            }
            
            string displayName = CyanTriggerNameHelpers.SanitizeName(GetActionRenderingDisplayName(false, false));
            return CyanTriggerUtil.CreateEventArgData(eventName, displayName, GetBaseActionVariables(true));
        }

        public CyanTriggerEditorVariableOption[] GetCustomEditorVariableOptions(SerializedProperty actionProperty)
        {
            if (!IsValid())
            {
                return Array.Empty<CyanTriggerEditorVariableOption>();
            }
            
            if (IsCustomEvent())
            {
                return GetCustomEventArgumentOptions(actionProperty);
            }
            
            SerializedProperty inputsProperty =
                actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
            
            if (CustomDefinition is ICyanTriggerCustomNodeCustomVariableOptions editorVarOptions)
            {
                // TODO better support custom output variables.
                return editorVarOptions.GetCustomEditorVariableOptions(inputsProperty);
            }

            List<CyanTriggerEditorVariableOption> outputVariables = new List<CyanTriggerEditorVariableOption>();

            var def = DefOrBaseDef;
            if (def != null && def.DefinitionType == CyanTriggerNodeDefinition.UdonDefinitionType.Event)
            {
                foreach (var (varName, varType) in def.GetEventVariables(/* output only UdonNodeParameter.ParameterType */))
                {
                    outputVariables.Add(new CyanTriggerEditorVariableOption 
                        {Type = varType, Name = varName, IsReadOnly = true});
                }
            }
            
            var varDefs = GetVariablesWithExtras(actionProperty, false);
            if (inputsProperty.arraySize != varDefs.Length)
            {
                return Array.Empty<CyanTriggerEditorVariableOption>();
            }
            
            for (int i = 0; i < varDefs.Length; ++i)
            {
                var varDef = varDefs[i];
                if (varDef == null)
                {
                    continue;
                }
                
                bool allowsCustomValues =
                    (varDef.variableType & CyanTriggerActionVariableTypeDefinition.Constant) != 0;
                bool outputVar = 
                    (varDef.variableType & CyanTriggerActionVariableTypeDefinition.VariableOutput) != 0;

                if (outputVar && !allowsCustomValues)
                {
                    SerializedProperty inputProperty = inputsProperty.GetArrayElementAtIndex(i);
                    var nameProperty = inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));
                    if (!string.IsNullOrEmpty(nameProperty.stringValue))
                    {
                        continue;
                    }
                    
                    SerializedProperty idProperty =
                        inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
                    string guid = idProperty.stringValue;
                    if (string.IsNullOrEmpty(guid))
                    {
                        continue;
                    }

                    SerializedProperty nameDataProperty =
                        inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                    var data = CyanTriggerSerializableObject.ObjectFromSerializedProperty(nameDataProperty);
                    if (!(data is string name) || string.IsNullOrEmpty(name))
                    { 
                        continue;
                    }
                    
                    Type type = varDef.type.Type;
                    if (type.IsByRef)
                    {
                        type = type.GetElementType();
                    }
                    
                    outputVariables.Add(new CyanTriggerEditorVariableOption
                    {
                        ID = guid,
                        Name = name,
                        Type = type,
                    });
                }
            }
            
            return outputVariables.ToArray();
        }

        public CyanTriggerEditorVariableOption[] GetCustomEditorVariableOptions(
            CyanTriggerAssemblyProgram program,
            CyanTriggerActionInstance actionInstances)
        {
            if (!IsValid())
            {
                return Array.Empty<CyanTriggerEditorVariableOption>();
            }
            
            CyanTriggerActionVariableInstance[] variableInstances = actionInstances.inputs;
            // Disable warning for use of Obsolete type CyanTriggerCustomNodeVariableProvider
#pragma warning disable CS0612
            if (CustomDefinition is CyanTriggerCustomNodeVariableProvider variableProvider)
            {
                return variableProvider.GetCustomEditorVariableOptions(program, variableInstances);
            }
#pragma warning restore CS0612
            
            List<CyanTriggerEditorVariableOption> outputVariables = new List<CyanTriggerEditorVariableOption>();
            var varDefs = GetVariablesWithExtras(actionInstances, false);
            if (variableInstances.Length != varDefs.Length)
            {
                return Array.Empty<CyanTriggerEditorVariableOption>();
            }
            
            for (int i = 0; i < varDefs.Length; ++i)
            {
                var varDef = varDefs[i];
                if (varDef == null || variableInstances[i] == null)
                {
                    continue;
                }
                
                bool allowsCustomValues =
                    (varDef.variableType & CyanTriggerActionVariableTypeDefinition.Constant) != 0;
                bool outputVar = 
                    (varDef.variableType & CyanTriggerActionVariableTypeDefinition.VariableOutput) != 0;

                if (outputVar && !allowsCustomValues)
                {
                    if (!string.IsNullOrEmpty(variableInstances[i].name))
                    {
                        continue;
                    }

                    string id = variableInstances[i].variableID;
                    if (string.IsNullOrEmpty(id))
                    {
                        continue;
                    }
                    
                    Type type = varDef.type.Type;
                    if (type.IsByRef)
                    {
                        type = type.GetElementType();
                    }

                    string name = (string)variableInstances[i].data.Obj;
                    if (program != null)
                    {
                        var variable = program.Data.AddVariable("local_var", type, false);
                        program.Data.SetVariableGuid(variable, id);
                        name = variable.Name;
                    }
                    
                    outputVariables.Add(new CyanTriggerEditorVariableOption
                    {
                        ID = id,
                        Name = name,
                        Type = type,
                    });
                }
            }
            
            return outputVariables.ToArray();
        }

        private CyanTriggerEditorVariableOption CreateCustomEventArgumentOption(
            string id,
            string name,
            Type type,
            bool isInput,
            string eventName,
            bool formalizeName)
        {
            string formalName = CyanTriggerAssemblyData.CreateCustomEventArgName(eventName, name);
            
            return new CyanTriggerEditorVariableOption
            {
                ID = id,
                Name = formalizeName ? formalName : name,
                Type = type,
                IsInput = isInput,
                UdonName = formalName,
            };
        }
        
        // Covers getting CustomEvent arguments
        public CyanTriggerEditorVariableOption[] GetCustomEventArgumentOptions(
            CyanTriggerEvent eventInstance,
            bool formalizeName)
        {
            if (!IsValid())
            {
                return Array.Empty<CyanTriggerEditorVariableOption>();
            }
            
            var multiInput = eventInstance?.eventInstance?.multiInput;
            var inputs = eventInstance?.eventInstance?.inputs;
            if (!IsCustomEvent() 
                || inputs == null
                || inputs.Length == 0
                || inputs[0] == null
                || !inputs[0].isVariable
                || multiInput == null)
            {
                return Array.Empty<CyanTriggerEditorVariableOption>();
            }

            string eventName = eventInstance.name;
            if (string.IsNullOrEmpty(eventName))
            {
                return Array.Empty<CyanTriggerEditorVariableOption>();
            }
            
            List<CyanTriggerEditorVariableOption> outputVariables = new List<CyanTriggerEditorVariableOption>();

            foreach (var input in multiInput)
            {
                string name = input.name;
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                string id = input.variableID;
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                object typeObj = input.data.Obj;
                if (!(typeObj is Type type))
                {
                    continue;
                }

                outputVariables.Add(
                    CreateCustomEventArgumentOption(id, name, type, input.isVariable, eventName, formalizeName));
            }
            
            return outputVariables.ToArray();
        }

        public CyanTriggerEditorVariableOption[] GetCustomEventArgumentOptions(SerializedProperty actionProperty)
        {
            if (!IsValid())
            {
                return Array.Empty<CyanTriggerEditorVariableOption>();
            }
            
            if (!IsCustomEvent())
            {
                return Array.Empty<CyanTriggerEditorVariableOption>();
            }
            
            var inputsProperty = actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
            if (inputsProperty.arraySize == 0)
            {
                return Array.Empty<CyanTriggerEditorVariableOption>();
            }
            
            var validInputProp = inputsProperty.GetArrayElementAtIndex(0);
            var validProp =
                validInputProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));

            if (!validProp.boolValue)
            {
                return Array.Empty<CyanTriggerEditorVariableOption>();
            }

            List<CyanTriggerEditorVariableOption> outputVariables = new List<CyanTriggerEditorVariableOption>();
            
            var multiInput = actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.multiInput));
            int size = multiInput.arraySize;
            for (int index = 0; index < size; ++index)
            {
                var inputProp = multiInput.GetArrayElementAtIndex(index);

                var nameProp =
                    inputProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));
                string name = nameProp.stringValue;
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }
                
                var idProp =
                    inputProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
                string id = idProp.stringValue;
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }
                
                var dataTypeProp =
                    inputProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                object typeObj = CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataTypeProp);
                if (!(typeObj is Type type))
                {
                    continue;
                }
                
                var isInputProp =
                    inputProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));

                // TODO properly provide event name to create formalized name.
                outputVariables.Add(
                    CreateCustomEventArgumentOption(id, name, type, isInputProp.boolValue, "", false));
            }

            return outputVariables.ToArray();
        }
        

        public int GetScopeDelta()
        {
            if (Definition == null)
            {
                return 0;
            }
            
            // Check if item has scope (block, blockend, while, for, if, else if, else)
            if (CyanTriggerNodeDefinitionManager.Instance.DefinitionHasScope(Definition.FullName))
            {
                return 1;
            }

            if (Definition.Definition == CyanTriggerCustomNodeBlockEnd.NodeDefinition)
            {
                return -1;
            }
            return 0;
        }

        public List<SerializedProperty> AddActionToEndOfPropertyList(
            SerializedProperty actionListProperty, 
            bool includeDependencies)
        {
            List<SerializedProperty> properties = new List<SerializedProperty>();
            actionListProperty.arraySize++;
            SerializedProperty newActionProperty =
                actionListProperty.GetArrayElementAtIndex(actionListProperty.arraySize - 1);
            properties.Add(newActionProperty);
            
            CyanTriggerSerializedPropertyUtils.SetActionData(this, newActionProperty);
            
            // If scope, add appropriate end point
            if (includeDependencies &&
                CustomDefinition is ICyanTriggerCustomNodeDependency customWithDependency)
            {
                foreach (var dependency in customWithDependency.GetDependentNodes())
                {
                    properties.AddRange(
                        GetActionInfoHolder(dependency).AddActionToEndOfPropertyList(actionListProperty, true));
                }
            }

            return properties;
        }

        public string GetActionRenderingDisplayName(SerializedProperty actionProperty, bool withColor)
        {
            string signature = GetActionRenderingDisplayName(withColor, true);

            if (!IsValid() || !CyanTriggerSettings.Instance.actionDetailedView)
            {
                return signature;
            }
            
            // Disable warning for use of Obsolete type CyanTriggerCustomNodeVariable
#pragma warning disable CS0618
            if (CustomDefinition is CyanTriggerCustomNodeVariable)
            {
                var variableDefinitions = GetBaseActionVariables(true);
                SerializedProperty inputsProperty =
                    actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
                SerializedProperty inputNameProperty = inputsProperty.GetArrayElementAtIndex(0);
                SerializedProperty inputDataProperty = inputsProperty.GetArrayElementAtIndex(1);
                
                string name = GetTextForProperty(inputNameProperty, variableDefinitions[0], false);
                name = name.Substring(1, name.Length - 2);
                name = name.Colorize(CyanTriggerColorTheme.VariableName, withColor);

                string propText = GetTextForProperty(inputDataProperty, variableDefinitions[1], withColor);
                string varText = "var".Colorize(CyanTriggerColorTheme.VariableIndicator, withColor);
                string setText = "Set".Colorize(CyanTriggerColorTheme.ActionName, withColor);
                string period = ".".Colorize(CyanTriggerColorTheme.Punctuation, withColor);
                string leftPar = "(".Colorize(CyanTriggerColorTheme.Punctuation, withColor);
                string rightPar = ")".Colorize(CyanTriggerColorTheme.Punctuation, withColor);
                
                return $"{varText} {name} {signature}{period}{setText}{leftPar}{propText}{rightPar}";
            }
#pragma warning restore CS0618
            
            return $"{signature}{GetMethodArgumentDisplay(actionProperty, withColor)}";
        }
        
        public string GetMethodArgumentDisplay(SerializedProperty actionProperty, bool withColor = false)
        {
            SerializedProperty inputsProperty =
                actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
            
            StringBuilder sb = new StringBuilder();

            string comma = ", ".Colorize(CyanTriggerColorTheme.Punctuation, withColor);
            string nullText = null;
            string GetNullText()
            {
                if (string.IsNullOrEmpty(nullText))
                {
                    nullText = "null".Colorize(CyanTriggerColorTheme.NullLiteral, withColor);
                }
                return nullText;
            }
            
            var variableDefinitions = GetVariablesWithExtras(actionProperty, true);
            int displayCount = 0;
            for (int input = 0; input < inputsProperty.arraySize && input < variableDefinitions.Length; ++input)
            {
                var variableDef = variableDefinitions[input];
                if (variableDef == null)
                {
                    continue;
                }
                
                if ((variableDef.variableType & CyanTriggerActionVariableTypeDefinition.Hidden) != 0)
                {
                    continue;
                }

                if (displayCount > 0)
                {
                    sb.Append(comma);
                }
                ++displayCount;
                
                Type varType = variableDef.type.Type;
                
                if (input == 0 &&
                    (variableDef.variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0)
                {
                    SerializedProperty multiInputsProperty =
                        actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.multiInput));
                    
                    if (multiInputsProperty.arraySize == 0)
                    {
                        sb.Append(GetNullText());
                    }
                    else if (multiInputsProperty.arraySize == 1)
                    {
                        SerializedProperty multiInputProperty = multiInputsProperty.GetArrayElementAtIndex(0);
                        sb.Append(GetTextForProperty(multiInputProperty, variableDef, withColor));
                    }
                    else
                    {
                        string arrayType = $"{CyanTriggerNameHelpers.GetTypeFriendlyName(varType)}Array";
                        sb.Append(arrayType.Colorize(CyanTriggerColorTheme.ValueLiteral, withColor));
                    }
                    
                    continue;
                }
                
                SerializedProperty inputProperty = inputsProperty.GetArrayElementAtIndex(input);
                sb.Append(GetTextForProperty(inputProperty, variableDef, withColor));
            }

            if (sb.Length > 0)
            {
                string leftPar = "(".Colorize(CyanTriggerColorTheme.Punctuation, withColor);
                string rightPar = ")".Colorize(CyanTriggerColorTheme.Punctuation, withColor);
                return $"{leftPar}{sb}{rightPar}";
            }
            
            return sb.ToString();
        }

        public static string GetTextForProperty(
            SerializedProperty inputProperty,
            CyanTriggerActionVariableDefinition variableDef,
            bool withColor = false)
        {
            SerializedProperty isVariableProperty =
                inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
            SerializedProperty nameProperty =
                inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));
            SerializedProperty idProperty =
                inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
            SerializedProperty varNameDataProperty =
                inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
            
            return GetTextForProperty(
                isVariableProperty.boolValue,
                nameProperty.stringValue,
                idProperty.stringValue,
                CyanTriggerSerializableObject.ObjectFromSerializedProperty(varNameDataProperty),
                variableDef,
                withColor
            );
        }
        
        public static string GetTextForProperty(
            CyanTriggerActionVariableInstance variableData,
            CyanTriggerActionVariableDefinition variableDef,
            bool withColor = false)
        {
            return GetTextForProperty(
                variableData.isVariable,
                variableData.name,
                variableData.variableID,
                variableData.data.Obj,
                variableDef,
                withColor
            );
        }
        
        public static string GetTextForProperty(
            bool isVariable, 
            string displayName,
            string variableId,
            object dataValue,
            CyanTriggerActionVariableDefinition variableDef, 
            bool withColor = false)
        {
            if (isVariable)
            {
                bool isOutput =
                    (variableDef.variableType & CyanTriggerActionVariableTypeDefinition.VariableOutput) != 0;
               
                bool newVar = false;
                if (string.IsNullOrEmpty(displayName))
                {
                    if (!string.IsNullOrEmpty(variableId))
                    {
                        if (dataValue is string varName && !string.IsNullOrEmpty(varName))
                        {
                            newVar = true;
                            // TODO sanitize variable name?
                            displayName = varName.Colorize(CyanTriggerColorTheme.VariableName, withColor);
                        }
                    }
                    
                    if (string.IsNullOrEmpty(displayName))
                    {
                        displayName = "null".Colorize(CyanTriggerColorTheme.NullLiteral, withColor);
                    }
                }
                else
                {
                    displayName = displayName.Colorize(CyanTriggerColorTheme.VariableName, withColor);
                }

                string varText = (newVar ? "new var" : "var").Colorize(CyanTriggerColorTheme.VariableIndicator, withColor);
                string outText = (isOutput ? "out ".Colorize(CyanTriggerColorTheme.OutputIndicator, withColor) : "");
                
                // TODO verify that name is always filled :eyes:
                return $"{outText}{varText} {displayName}";
            }
            
            Type varType = variableDef.type.Type;
            string typeName = $"const {CyanTriggerNameHelpers.GetTypeFriendlyName(varType)}";
            string value = "null";
            
            if (dataValue == null)
            {
                return "null".Colorize(CyanTriggerColorTheme.NullLiteral, withColor);
            }

            CyanTriggerColorTheme color = CyanTriggerColorTheme.ValueLiteral;
            
            if (varType == typeof(string))
            {
                value = $"\"{dataValue}\"";

                color = CyanTriggerColorTheme.StringLiteral;
            }
            else if (dataValue is GameObject gameObject)
            {
                color = CyanTriggerColorTheme.UnityObjectLiteral;
                value = VRC.Tools.GetGameObjectPath(gameObject);
            }
            else if (dataValue is Component component)
            {
                color = CyanTriggerColorTheme.UnityObjectLiteral;
                value = VRC.Tools.GetGameObjectPath(component.gameObject);
            }
            else if (dataValue is UnityEngine.Object obj)
            {
                color = CyanTriggerColorTheme.UnityObjectLiteral;
                value = obj.name;
            }
            
            // special struct types
            else if (dataValue is Matrix4x4 matrix)
            {
                color = CyanTriggerColorTheme.ValueLiteral;
                value = $"({matrix.GetRow(0)}, {matrix.GetRow(1)}, {matrix.GetRow(2)}, {matrix.GetRow(3)})";
            }
            
            // Everything else
            else
            {
                value = dataValue.ToString();
                if (value == varType.FullName ||
                    (varType.IsValueType 
                     && !varType.IsPrimitive
                     && varType.GetMethod("ToString", new Type[0]).DeclaringType == typeof(ValueType)))
                {
                    // TODO
                    value = typeName;
                }
            }
            
            return value.Colorize(color, withColor);
        }
    }
}