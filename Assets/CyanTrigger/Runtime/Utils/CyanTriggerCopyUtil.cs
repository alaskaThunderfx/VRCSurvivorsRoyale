namespace Cyan.CT
{
    public static class CyanTriggerCopyUtil
    {
        public static CyanTriggerDataInstance CopyCyanTriggerDataInstance(CyanTriggerDataInstance data, bool copyData)
        {
            if (data == null)
            {
                return null;
            }

            CyanTriggerDataInstance ret = new CyanTriggerDataInstance
            {
                version = data.version,
                events = new CyanTriggerEvent[data.events?.Length ?? 0],
                variables = new CyanTriggerVariable[data.variables?.Length ?? 0],
                programSyncMode = data.programSyncMode,
                autoSetSyncMode = data.autoSetSyncMode,
                updateOrder = data.updateOrder,
                programName = data.programName,
                comment = CopyComment(data.comment),
                ignoreEventWarnings = data.ignoreEventWarnings
            };

            for (int cur = 0; cur < ret.events.Length; ++cur)
            {
                ret.events[cur] = CopyEvent(data.events[cur], copyData);
            }

            for (int cur = 0; cur < ret.variables.Length; ++cur)
            {
                ret.variables[cur] = CopyVariable(data.variables[cur], copyData);
            }

            return ret;
        }

        private static CyanTriggerComment CopyComment(CyanTriggerComment comment)
        {
            return new CyanTriggerComment { comment = comment?.comment };
        }

        public static CyanTriggerVariable CopyVariable(CyanTriggerVariable variable, bool copyData)
        {
            if (variable == null)
            {
                return null;
            }

            CyanTriggerVariable ret = new CyanTriggerVariable
            {
                name = variable.name,
                sync = variable.sync,
                isVariable = variable.isVariable,
                variableID = variable.variableID,
                type = new CyanTriggerSerializableType(variable.type.Type),
                typeInfo = variable.typeInfo,
                showInInspector = variable.showInInspector,
                comment = CopyComment(variable.comment)
            };

            object data = variable.data.Obj;
            if (copyData || (data != null && !(data is UnityEngine.Object)))
            {
                if (data is ICyanTriggerCustomType customType)
                {
                    data = customType.Clone();
                }
                ret.data = new CyanTriggerSerializableObject(data);
            }

            return ret;
        }

        public static CyanTriggerActionVariableInstance CopyVariableInst(
            CyanTriggerActionVariableInstance variable, bool copyData)
        {
            if (variable == null)
            {
                return null;
            }

            CyanTriggerActionVariableInstance ret = new CyanTriggerActionVariableInstance
            {
                name = variable.name,
                isVariable = variable.isVariable,
                variableID = variable.variableID,
            };

            // Some values are used in the program and are needed in compilation...
            // CyanTrigger.ActivateCustomTrigger requires string data
            // TODO eventually move those to another field
            object data = variable.data.Obj;
            if (copyData || (data != null && !(data is UnityEngine.Object)))
            {
                if (data is ICyanTriggerCustomType customType)
                {
                    data = customType.Clone();
                }
                ret.data = new CyanTriggerSerializableObject(data);
            }

            return ret;
        }

        public static CyanTriggerActionType CopyActionType(CyanTriggerActionType actionType)
        {
            if (actionType == null)
            {
                return null;
            }

            return new CyanTriggerActionType
            {
                guid = actionType.guid,
                directEvent = actionType.directEvent,
            };
        }

        public static CyanTriggerActionInstance CopyActionInst(CyanTriggerActionInstance action, bool copyData)
        {
            if (action == null)
            {
                return null;
            }

            var ret = new CyanTriggerActionInstance
            {
                actionType = CopyActionType(action.actionType),
                inputs = new CyanTriggerActionVariableInstance[action.inputs?.Length ?? 0],
                multiInput = new CyanTriggerActionVariableInstance[action.multiInput?.Length ?? 0],
                comment = CopyComment(action.comment)
            };

            for (int cur = 0; cur < ret.inputs.Length; ++cur)
            {
                ret.inputs[cur] = CopyVariableInst(action.inputs[cur], copyData);
            }
            for (int cur = 0; cur < ret.multiInput.Length; ++cur)
            {
                ret.multiInput[cur] = CopyVariableInst(action.multiInput[cur], copyData);
            }

            return ret;
        }

        public static CyanTriggerEventOptions CopyEventOptions(CyanTriggerEventOptions eventOptions)
        {
            if (eventOptions == null)
            {
                return null;
            }

            var ret = new CyanTriggerEventOptions
            {
                broadcast = eventOptions.broadcast,
                delay = eventOptions.delay,
                userGate = eventOptions.userGate,
                userGateExtraData =
                    new CyanTriggerActionVariableInstance[eventOptions.userGateExtraData?.Length ?? 0],
                replay = eventOptions.replay,
            };

            for (int cur = 0; cur < ret.userGateExtraData.Length; ++cur)
            {
                // TODO Update copy data once it becomes a reference instead of const in the program.
                ret.userGateExtraData[cur] = CopyVariableInst(eventOptions.userGateExtraData[cur], true);
            }

            return ret;
        }

        public static CyanTriggerEvent CopyEvent(CyanTriggerEvent oldEvent, bool copyData)
        {
            if (oldEvent == null)
            {
                return null;
            }

            var ret = new CyanTriggerEvent
            {
                name = oldEvent.name,
                eventId = oldEvent.eventId,
                actionInstances = new CyanTriggerActionInstance[oldEvent.actionInstances?.Length ?? 0],
                eventInstance = CopyActionInst(oldEvent.eventInstance, copyData),
                eventOptions = CopyEventOptions(oldEvent.eventOptions),
            };

            for (int cur = 0; cur < ret.actionInstances.Length; ++cur)
            {
                ret.actionInstances[cur] = CopyActionInst(oldEvent.actionInstances[cur], copyData);
            }

            return ret;
        }
    }
}