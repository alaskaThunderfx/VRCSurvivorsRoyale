
using System;
using UnityEngine;

namespace Cyan.CT.Editor
{
    public static class CyanTriggerVersionMigrator
    {
        // Returns true if the trigger was migrated.
        public static bool MigrateTrigger(CyanTriggerDataInstance cyanTrigger)
        {
            if (cyanTrigger == null || cyanTrigger.variables == null || cyanTrigger.events == null)
            {
                return false;
            }
            
            bool migrated = false;
            if (cyanTrigger.version == 0)
            {
                cyanTrigger.version = 1;
                migrated = true;
                MigrateTriggerToVersion1(cyanTrigger);
            }

            if (cyanTrigger.version == 1)
            {
                cyanTrigger.version = 2;
                migrated = true;
                MigrateTriggerToVersion2(cyanTrigger);
            }

            if (cyanTrigger.version == 2)
            {
                cyanTrigger.version = 3;
                migrated = true;
                MigrateTriggerToVersion3(cyanTrigger);
            }

            if (cyanTrigger.version == 3)
            {
                cyanTrigger.version = 4;
                migrated = true;
                MigrateTriggerToVersion4(cyanTrigger);
            }
            if (cyanTrigger.version == 4)
            {
                cyanTrigger.version = 5;
                migrated = true;
                MigrateTriggerToVersion5(cyanTrigger);
            }
            if (cyanTrigger.version == 5)
            {
                cyanTrigger.version = 6;
                migrated = true;
                // No changes to actual data, but used here to force recompile everything in the project
                // ProgramName was added, and you must be on the current version for this to be supported.
            }

            // TODO add more version migrations as data changes

            // Remember to update CyanTriggerDataInstance.DataVersion when data versioning has changed!
            Debug.Assert(cyanTrigger.version == CyanTriggerDataInstance.DataVersion);

            return migrated;
        }

        #region Version 5 Migration

        /*
        Version 5 Changes
        - Variable providers replaced with out variables
          - Updated for, foreach, condition, and local variables
        - Removed local variable action and replaced with variable.set action
         */
        
        private static void MigrateTriggerToVersion5(CyanTriggerDataInstance cyanTrigger)
        {
            foreach (var eventTrigger in cyanTrigger.events)
            {
                // Find all previously variable provider types and migrate to out variables
                foreach (var action in eventTrigger.actionInstances)
                {
                    if (action?.actionType == null)
                    {
                        continue;
                    }
                    
                    string directAction = action.actionType?.directEvent;
                    if (directAction == CyanTriggerCustomNodeLoopFor.FullName)
                    {
                        if (action.inputs.Length >= 4)
                        {
                            action.inputs[3].isVariable = true;
                        }
                    }
                    else if (directAction == CyanTriggerCustomNodeLoopForEach.FullName)
                    {
                        if (action.inputs.Length >= 3)
                        {
                            action.inputs[1].isVariable = true;
                            action.inputs[2].isVariable = true;
                        }
                    }
                    else if (directAction == CyanTriggerCustomNodeCondition.FullName)
                    {
                        if (action.inputs.Length >= 1)
                        {
                            action.inputs[0].isVariable = true;
                        }
                    }
                    // Disable warning for use of Obsolete type CyanTriggerCustomNodeVariable
#pragma warning disable CS0618
                    else if (CyanTriggerActionInfoHolder.GetActionInfoHolder(action.actionType)
                                 .Definition?.CustomDefinition is CyanTriggerCustomNodeVariable variableDefinition)
                    {
                        action.actionType.directEvent =
                            CyanTriggerCustomNodeSetVariable.GetFullnameForType(variableDefinition.Type);
                        if (action.inputs.Length >= 2)
                        {
                            // Swap the inputs
                            (action.inputs[0], action.inputs[1]) = (action.inputs[1], action.inputs[0]);
                            action.inputs[1].isVariable = true;
                        }
                    }
#pragma warning restore CS0618
                }
            }
        }

        #endregion
        
        #region Version 4 Migration

        /*
         Version 4 Changes
         - Removed CyanTrigger.SendCustomEvent. Migrate to UdonBehaviour.SendCustomEvent
         - Added EventId
         - Fix actions where multi-input is now supported but wasn't before.
         */
        
        private static void MigrateTriggerToVersion4(CyanTriggerDataInstance cyanTrigger)
        {
            var ctConst = CyanTriggerAssemblyDataConsts.ThisCyanTrigger;
            var udonConst = CyanTriggerAssemblyDataConsts.ThisUdonBehaviour;
            
            foreach (var eventTrigger in cyanTrigger.events)
            {
                // Add EventIds to all events
                if (string.IsNullOrEmpty(eventTrigger.eventId))
                {
                    eventTrigger.eventId = Guid.NewGuid().ToString();
                }

                // Find all CyanTrigger.SendCustomEvent and replace with UdonBehaviour.SendCustomEvent
                foreach (var action in eventTrigger.actionInstances)
                {
                    if (action?.actionType?.directEvent == CyanTriggerCustomNodeSendCustomEvent.FullName)
                    {
                        action.actionType.directEvent = CyanTriggerCustomNodeSendCustomEventUdon.FullName;
                        // Go through variables and replace with Udon version
                        var ctInputs = action.multiInput;
                        for (int i = 0; i < ctInputs.Length; ++i)
                        {
                            var input = ctInputs[i];

                            var ctData = input.data.Obj;
                            object udonData = null;
                            if (ctData is CyanTrigger ct)
                            {
                                udonData = ct.triggerInstance?.udonBehaviour;
                            }
                            input.data.Obj = udonData;
                            
                            if (input.isVariable)
                            {
                                if (input.variableID == ctConst.ID)
                                {
                                    input.variableID = udonConst.ID;
                                    input.name = udonConst.Name;
                                }
                            }
                        }
                    }
                    
                    // Fix issues where something went from not allowing multi-input to allowing multi-input.
                    var actionInfo = CyanTriggerActionInfoHolder.GetActionInfoHolder(action?.actionType);
                    if (actionInfo.IsValid())
                    {
                        var variables = actionInfo.GetBaseActionVariables(false);
                        if (variables.Length > 0)
                        {
                            var firstVar = variables[0];
                            if ((firstVar.variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0
                                && action?.multiInput.Length == 0
                                && (action.inputs[0].isVariable || action.inputs[0].data.Obj != null))
                            {
                                action.multiInput = new[]
                                {
                                    action.inputs[0]
                                };
                                action.inputs[0] = new CyanTriggerActionVariableInstance();
                            }
                        }
                    }
                }
            }
        }

        #endregion
        
        #region Version 3 Migration

        /*
         Version 3 changes
         - Removed OnAnimatorMove (No changes needed)
         - Added oldValue option to OnVariableChanged that requires storing variable id
         */
        private static void MigrateTriggerToVersion3(CyanTriggerDataInstance cyanTrigger)
        {
            foreach (var eventTrigger in cyanTrigger.events)
            {
                if (eventTrigger.eventInstance.actionType.directEvent ==
                    CyanTriggerCustomNodeOnVariableChanged.OnVariableChangedEventName)
                {
                    CyanTriggerCustomNodeOnVariableChanged.MigrateEvent(
                        eventTrigger.eventInstance,
                        cyanTrigger.variables);
                }
            }
        }


        #endregion
        
        #region Version 2 Migration
        /*
         Version 2 changes
         - Renaming PassIfTrue and FailIfFalse with "Condition" prefix
         - Renaming "ActivateCustomTrigger" to "SendCustomEvent"
        */
        private static void MigrateTriggerToVersion2(CyanTriggerDataInstance cyanTrigger)
        {
            void MigrateTriggerActionData(CyanTriggerActionInstance actionInstance)
            {
                switch (actionInstance.actionType.directEvent)
                {
                    case "CyanTriggerSpecial_FailIfFalse":
                        actionInstance.actionType.directEvent = "CyanTriggerSpecial_ConditionFailIfFalse";
                        break;
                    case "CyanTriggerSpecial_PassIfTrue":
                        actionInstance.actionType.directEvent = "CyanTriggerSpecial_ConditionPassIfTrue";
                        break;
                    case "CyanTrigger.__ActivateCustomTrigger__CyanTrigger__SystemString":
                        actionInstance.actionType.directEvent = "CyanTrigger.__SendCustomEvent__CyanTrigger__SystemString";
                        break;
                }
            }
            
            foreach (var eventTrigger in cyanTrigger.events)
            {
                foreach (var actionInstance in eventTrigger.actionInstances)
                {
                    MigrateTriggerActionData(actionInstance);
                }
            }
        }

        #endregion
        
        #region Version 1 Migration
        
        /*
         Version 1 changes
         - "this" variables now start with an underscore
         - variable providers use variable id and name instead of two variable's data fields
        */
        private static void MigrateTriggerToVersion1(CyanTriggerDataInstance cyanTrigger)
        {
            void MigrateTriggerVariable(CyanTriggerActionVariableInstance variableInstance)
            {
                if (variableInstance.isVariable && variableInstance.variableID.StartsWith("this_"))
                {
                    variableInstance.variableID = $"_{variableInstance.variableID}";
                }
            }
            
            void MigrateTriggerActionData(CyanTriggerActionInstance actionInstance)
            {
                foreach (var variable in actionInstance.multiInput)
                {
                    MigrateTriggerVariable(variable);
                }
                foreach (var variable in actionInstance.inputs)
                {
                    MigrateTriggerVariable(variable);
                }
            }
            
            foreach (var eventTrigger in cyanTrigger.events)
            {
                foreach (var actionInstance in eventTrigger.actionInstances)
                {
                    MigrateTriggerActionData(actionInstance);
                    
                    // Update variable providers so variables only take one input instead of two
                    // Disable warning for use of Obsolete type CyanTriggerCustomNodeVariableProvider
# pragma warning disable CS0612
                    if (CyanTriggerNodeDefinitionManager.Instance.TryGetCustomDefinition(actionInstance.actionType.directEvent,
                            out var customDefinition) && customDefinition is CyanTriggerCustomNodeVariableProvider variableProvider)
                    {
                        variableProvider.MigrateTriggerToVersion1(actionInstance);
                    }
#pragma warning restore CS0612
                }
            }
        }

        #endregion
    }
}
