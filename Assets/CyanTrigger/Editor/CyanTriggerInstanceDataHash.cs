using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Cyan.CT.Editor
{
    public static class CyanTriggerInstanceDataHash
    {
        private const int HashVersion = 3; // Increment this whenever hash changes and all items need to be rehashed.
/*

======== Remember to also update CyanTriggerCopyUtil.CopyCyanTriggerDataInstance!! ======== 

Program unique string format:

Hash Version: <version>
Version: <version>
Update Order: <int>
Sync Method: <sync>
variables:
<User Variables>
events:
<Events>



User Variable
<name>, <type>, <synced>, <hasCallback>

Event
Event name: <name>
<EventOption>
<Event Action>
Actions:
<actions>
EndActions
EndEvent

EventOption
<broadcast>, <userGate>, <delay>, <replay>
UserList
<user list inputs>

Action
<Direct: <name>/CustomAction: <guid>>
MultiInputs
<inputs>
Inputs
<inputs>

Input
<type> (const/var "<name>"/var[<id>] <hasCallback>)
*/

        
        // TODO change to odin version
        // This is basically a string encoding of a cyan trigger that does not depend on any variable data.
        public static string GetProgramUniqueStringForCyanTrigger(CyanTriggerDataInstance instanceData)
        {
            if (instanceData == null || instanceData.variables == null || instanceData.events == null)
            {
                return "Null CyanTrigger";
            }
            
            StringBuilder triggerInfo = new StringBuilder();

            HashSet<string> variablesWithCallbacks =
                CyanTriggerCustomNodeOnVariableChanged.GetVariablesWithOnChangedCallback(instanceData.events, out _);
            Dictionary<string, int> variableMap = new Dictionary<string, int>();
            int varCount = 0;

            foreach (var varConst in CyanTriggerAssemblyDataConsts.GetConstVariables())
            {
                variableMap.Add(varConst.ID, varCount++);
            }

            triggerInfo.AppendLine($"Hash Version: {HashVersion}");
            triggerInfo.AppendLine($"Version: {instanceData.version}");
            triggerInfo.AppendLine($"Update Order: {instanceData.updateOrder}");
            
            bool autoSync = instanceData.autoSetSyncMode;
            string syncType = autoSync ? "AutoSync" : instanceData.programSyncMode.ToString();
            triggerInfo.AppendLine($"Sync Method: {syncType}");
            
            // Variables
            {
                triggerInfo.AppendLine("variables:");

                List<CyanTriggerVariable> variables = new List<CyanTriggerVariable>(instanceData.variables);
                
                // Sort by name first to ensure that variable id is set properly to the correct index per variable.
                variables.Sort((var1, var2) => 
                    String.Compare(var1.name, var2.name, StringComparison.Ordinal));
                
                foreach (var variable in variables)
                {
                    if (variable.IsDisplayOnly())
                    {
                        continue;
                    }
                    
                    bool hasCallback = variablesWithCallbacks.Contains(variable.variableID);
                    
                    // The name is defined in the code and is needed in the hash.
                    triggerInfo.AppendLine($"{variable.name}, {variable.type.Type}, {variable.sync}, {hasCallback}");
                    variableMap.Add(variable.variableID, varCount++);

                    if (hasCallback)
                    {
                        string prevVariable = CyanTriggerCustomNodeOnVariableChanged.GetPrevVariableGuid(
                            CyanTriggerCustomNodeOnVariableChanged.GetOldVariableName(variable.name),
                            variable.variableID);
                        variableMap.Add(prevVariable, varCount++);
                    }
                }
            }
            // Events
            {
                triggerInfo.AppendLine("events:");

                List<CyanTriggerEvent> events = new List<CyanTriggerEvent>(instanceData.events);
                // TODO support sorting in compilation time
                // TODO make better sorting?
                // events.Sort((e1, e2) =>
                // {
                //     string e1t = string.IsNullOrEmpty(e1.eventInstance.actionType.guid)
                //         ? e1.eventInstance.actionType.directEvent
                //         : e1.eventInstance.actionType.guid;
                //     
                //     string e2t = string.IsNullOrEmpty(e2.eventInstance.actionType.guid)
                //         ? e2.eventInstance.actionType.directEvent
                //         : e2.eventInstance.actionType.guid;
                //
                //     int ret = String.Compare(e1t, e2t, StringComparison.Ordinal);
                //     if (ret == 0)
                //     {
                //         ret = String.Compare(e1.name, e2.name, StringComparison.Ordinal);
                //     }
                //
                //     return ret;
                // });
                
                for (int cur = 0; cur < events.Count; ++cur)
                {
                    triggerInfo.Append(
                        GetProgramUniqueStringForEvent(events[cur], variablesWithCallbacks, variableMap, ref varCount));
                }
            }

            return triggerInfo.ToString();
        }

        private static string GetProgramUniqueStringForEvent(
            CyanTriggerEvent triggerEvent,
            HashSet<string> variablesWithCallbacks,
            Dictionary<string, int> variableMap,
            ref int varCount)
        {
            if (triggerEvent == null || triggerEvent.eventInstance == null || triggerEvent.actionInstances == null)
            {
                return "Invalid Event!";
            }
            
            StringBuilder eventString = new StringBuilder();

            // Only print name for custom events as these are the only ones that use the name.
            var infoHolder = CyanTriggerActionInfoHolder.GetActionInfoHolder(triggerEvent.eventInstance.actionType);
            if (infoHolder.IsCustomEvent())
            {
                eventString.AppendLine($"Event name: {triggerEvent.name}");
                
                // Get variables
                var eventArgs = infoHolder.GetCustomEventArgumentOptions(triggerEvent, false);
                foreach (var var in eventArgs)
                {
                    if (variableMap.ContainsKey(var.ID))
                    {
                        Debug.Log($"Map already contains id: {var.ID}");
                        variableMap.Remove(var.ID);
                    }
                    variableMap.Add(var.ID, varCount++);
                    
                    eventString.AppendLine($"{var.Name}, {var.Type}, {(var.IsInput ? "Input" : "Output")}");
                }
            }
            eventString.Append(GetProgramUniqueStringForEventOption(triggerEvent.eventOptions, variablesWithCallbacks, variableMap));
            eventString.Append(GetProgramUniqueStringForAction(triggerEvent.eventInstance, variablesWithCallbacks, variableMap, ref varCount));
            eventString.AppendLine("Actions:");
            for (int cur = 0; cur < triggerEvent.actionInstances.Length; ++cur)
            {
                eventString.Append(GetProgramUniqueStringForAction(triggerEvent.actionInstances[cur], variablesWithCallbacks, variableMap, ref varCount));
            }
            
            eventString.AppendLine("EndActions");
            eventString.AppendLine("EndEvent");
            return eventString.ToString();
        }

        private static string GetProgramUniqueStringForEventOption(
            CyanTriggerEventOptions eventOptions,
            HashSet<string> variablesWithCallbacks,
            Dictionary<string, int> variableMap)
        {
            if (eventOptions == null)
            {
                return "Invalid EventOptions!";
            }
            
            StringBuilder eventString = new StringBuilder();
            eventString.AppendLine($"{eventOptions.broadcast}, {eventOptions.replay}, {eventOptions.userGate}, {eventOptions.delay}"); 
            
            if (eventOptions.userGate == CyanTriggerUserGate.UserAllowList || 
                eventOptions.userGate == CyanTriggerUserGate.UserDenyList)
            {
                eventString.AppendLine("UserList");
                // TODO sort user gate? 
                foreach (var userGate in eventOptions.userGateExtraData)
                {
                    eventString.AppendLine(GetProgramUniqueStringForVariable(
                        userGate, 
                        CyanTriggerSerializableInstanceEditor.AllowedUserGateVariableDefinition,
                        variablesWithCallbacks, 
                        variableMap,
                        false));
                }
            }

            return eventString.ToString();
        }
        
        private static string GetProgramUniqueStringForAction(
            CyanTriggerActionInstance actionInstance,
            HashSet<string> variablesWithCallbacks,
            Dictionary<string, int> variableMap,
            ref int varCount)
        {
            if (actionInstance == null || actionInstance.actionType == null || actionInstance.inputs == null)
            {
                return "Invalid Action!";
            }
            
            var infoHolder = CyanTriggerActionInfoHolder.GetActionInfoHolder(
                actionInstance.actionType.guid, actionInstance.actionType.directEvent);
            
            
            StringBuilder actionString = new StringBuilder();
            var actionDefinedVariables = infoHolder.GetCustomEditorVariableOptions(null, actionInstance);
            foreach (var var in actionDefinedVariables)
            {
                if (variableMap.ContainsKey(var.ID))
                {
                    Debug.Log($"Map already contains id: {var.ID}");
                    variableMap.Remove(var.ID);
                }
                variableMap.Add(var.ID, varCount++);
            }

            if (!string.IsNullOrEmpty(actionInstance.actionType.directEvent))
            {
                actionString.AppendLine($"Direct: {actionInstance.actionType.directEvent}");
                
                if (CyanTriggerNodeDefinitionManager.Instance.TryGetCustomDefinition(actionInstance.actionType.directEvent,
                    out var customDefinition) && customDefinition is ICyanTriggerCustomNodeCustomHash customHash)
                {
                    string hash = customHash.GetCustomHash(actionInstance);
                    if (!string.IsNullOrEmpty(hash))
                    {
                        actionString.AppendLine(hash);
                    }
                }
            }
            else
            {
                // custom node
                actionString.AppendLine($"CustomAction: {actionInstance.actionType.guid}");
            }

            // Ignore custom Events here as inputs/multiInputs are for parameters and handled above.
            if (!infoHolder.IsCustomEvent())
            {
                var varDefs = infoHolder.GetVariablesWithExtras(actionInstance, false);
                bool allowsMulti = varDefs.Length > 0 &&
                                   (varDefs[0].variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0;

                if (!allowsMulti && varDefs.Length > 0)
                {
                    actionString.AppendLine("Inputs");
                }
                
                for (int cur = 0; cur < varDefs.Length; ++cur)
                {
                    var varDef = varDefs[cur];
                    if (varDef == null)
                    {
                        continue;
                    }
                    
                    if (cur == 0 && allowsMulti)
                    {
                        actionString.AppendLine("MultiInputs");
                        foreach (var var in actionInstance.multiInput)
                        {
                            actionString.AppendLine(GetProgramUniqueStringForVariable(
                                var,
                                varDef,
                                variablesWithCallbacks, 
                                variableMap));
                        }
                        continue;
                    }

                    if (cur == 1 && allowsMulti)
                    {
                        actionString.AppendLine("Inputs");
                    }

                    actionString.AppendLine(GetProgramUniqueStringForVariable(
                        actionInstance.inputs[cur], 
                        varDef,
                        variablesWithCallbacks, 
                        variableMap));
                }
            }
            
            return actionString.ToString();
        }
        
        private static string GetProgramUniqueStringForVariable(
            CyanTriggerActionVariableInstance variable,
            CyanTriggerActionVariableDefinition def,
            HashSet<string> variablesWithCallbacks,
            Dictionary<string, int> variableMap,
            bool reference = true)
        {
            if (def == null)
            {
                return "";
            }
            
            if (variable == null)
            {
                return "Invalid Variable!";
            }
            
            if (variable.isVariable)
            {
                if (string.IsNullOrEmpty(variable.variableID))
                {
                    return $"{def.type.Type} var \"{variable.name}\"";
                }
                
                bool hasCallback = variablesWithCallbacks.Contains(variable.variableID);
                if (!variableMap.TryGetValue(variable.variableID, out int id))
                {
                    // TODO add a callback to know when there are errors in the hashing process.
                    throw new Exception($"[CyanTrigger] Variable id could not be found: {variable.variableID}");
                }
                return $"{def.type.Type} var[{id}] {hasCallback}";
            }
            
            string data = "";
            if (!reference)
            {
                data = $" {variable.data.Obj}";
            }
            
            return $"{def.type.Type} const {data}";
        }



        public static string HashCyanTriggerInstanceData(CyanTriggerDataInstance instanceData)
        {
            var programString = GetProgramUniqueStringForCyanTrigger(instanceData);
            var bytes = Encoding.ASCII.GetBytes(programString);
            MD5 md5 = new MD5CryptoServiceProvider();
            try
            {
                byte[] result = md5.ComputeHash(bytes);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < result.Length; i++)
                {
                    sb.Append(result[i].ToString("X2"));
                }

                return sb.ToString();
            }
            catch (ArgumentNullException e)
            {
                Debug.LogError("Could not hash CyanTrigger!");
                Debug.LogError(e);
            }

            return null;
        }
    }
}

