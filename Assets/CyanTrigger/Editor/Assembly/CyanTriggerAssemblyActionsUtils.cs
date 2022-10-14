using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Enums;
using VRC.Udon.Common.Interfaces;

namespace Cyan.CT.Editor
{
    public static class CyanTriggerAssemblyActionsUtils
    {
        public static readonly List<CyanTriggerAssemblyInstruction> EmptyActions = new List<CyanTriggerAssemblyInstruction>();
        
        
        public static List<CyanTriggerAssemblyInstruction> JumpToFunction(
            CyanTriggerAssemblyProgram triggerProgram, 
            string functionName)
        {
            CyanTriggerAssemblyInstruction jumpAction = CyanTriggerAssemblyInstruction.JumpLabel(functionName);
            CyanTriggerAssemblyDataType jumpReturnVar = triggerProgram.Data.CreateMethodReturnVar(jumpAction);
            CyanTriggerAssemblyInstruction setReturnAction = CyanTriggerAssemblyInstruction.PushVariable(jumpReturnVar);

            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();
            actions.Add(setReturnAction);
            actions.Add(jumpAction);

            return actions;
        }

        public static List<CyanTriggerAssemblyInstruction> JumpIndirect(
            CyanTriggerAssemblyData data,
            CyanTriggerAssemblyDataType jumpVariable)
        {
            CyanTriggerAssemblyInstruction jumpAction = CyanTriggerAssemblyInstruction.JumpIndirect(jumpVariable);
            CyanTriggerAssemblyDataType jumpReturnVar = data.CreateMethodReturnVar(jumpAction);
            CyanTriggerAssemblyInstruction setReturnAction = CyanTriggerAssemblyInstruction.PushVariable(jumpReturnVar);

            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();
            actions.Add(setReturnAction);
            actions.Add(jumpAction);

            return actions;
        }

        public static List<CyanTriggerAssemblyInstruction> CopyVariables(
            CyanTriggerAssemblyDataType srcVariable,
            CyanTriggerAssemblyDataType dstVariable)
        {
            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();

            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(srcVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(dstVariable));
            actions.Add(CyanTriggerAssemblyInstruction.Copy());

            return actions;
        }

        public static List<CyanTriggerAssemblyInstruction> OnVariableChangedCheck(
            CyanTriggerAssemblyProgram program,
            CyanTriggerAssemblyDataType variable)
        {
            if (!variable.HasCallback)
            {
                return EmptyActions;
            }

            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();

            CyanTriggerAssemblyInstruction nop = CyanTriggerAssemblyInstruction.Nop();
            CyanTriggerAssemblyDataType tempBool = program.Data.RequestTempVariable(typeof(bool));
            CyanTriggerAssemblyInstruction pushTempBool = CyanTriggerAssemblyInstruction.PushVariable(tempBool);

            // push prev
            // push cur
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(variable.PreviousVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(variable));

            // push temp bool
            // push comparison
            actions.Add(pushTempBool);
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(
                CyanTriggerDefinitionResolver.GetPrimitiveOperationSignature(
                    variable.Type.IsValueType ? variable.Type : typeof(object),
                    PrimitiveOperation.Inequality)));

            // push temp bool
            // push jump if false nop
            actions.Add(pushTempBool);
            actions.Add(CyanTriggerAssemblyInstruction.JumpIfFalse(nop));

            // call method for variable changed
            // Copying into the old value is handled in the callback itself.
            string methodName = CyanTriggerCustomNodeOnVariableChanged.GetVariableChangeEventName(variable.Name);
            actions.AddRange(JumpToFunction(program, methodName));

            // push nop
            actions.Add(nop);

            program.Data.ReleaseTempVariable(tempBool);

            return actions;
        }

        public static List<CyanTriggerAssemblyInstruction> SetProgramVariable(
            CyanTriggerAssemblyDataType nameVariable,
            CyanTriggerAssemblyDataType dataVariable,
            CyanTriggerAssemblyDataType udonVariable)
        {
            string setProgramVariableMethodName =
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(UdonBehaviour).GetMethod(
                        nameof(UdonBehaviour.SetProgramVariable),
                        new[] { typeof(string), typeof(object) }));

            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();

            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(udonVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(nameVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(dataVariable));
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(setProgramVariableMethodName));

            return actions;
        }

        private static MethodInfo udonBehaviourGetProgramVariableMethodInfo;
        public static List<CyanTriggerAssemblyInstruction> GetProgramVariable(
            CyanTriggerAssemblyDataType nameVariable,
            CyanTriggerAssemblyDataType dataVariable,
            CyanTriggerAssemblyDataType udonVariable)
        {
            if (udonBehaviourGetProgramVariableMethodInfo == null)
            {
                foreach (var method in typeof(UdonBehaviour).GetMethods())
                {
                    if (method.Name == nameof(UdonBehaviour.GetProgramVariable)
                        && method.ReturnParameter?.ParameterType == typeof(object))
                    {
                        udonBehaviourGetProgramVariableMethodInfo = method;
                        break;
                    }
                }
            }

            string getProgramVariableMethodName =
                CyanTriggerDefinitionResolver.GetMethodSignature(udonBehaviourGetProgramVariableMethodInfo);

            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();

            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(udonVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(nameVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(dataVariable));
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(getProgramVariableMethodName));

            return actions;
        }

        public static List<CyanTriggerAssemblyInstruction> SendCustomEvent(
            CyanTriggerAssemblyProgram program,
            UdonBehaviour udonBehaviour,
            string customEventName)
        {
            CyanTriggerAssemblyDataType udonBehaviourVariable =
                program.Data.GetOrCreateVariableConstant(typeof(UdonBehaviour), udonBehaviour, true);
            CyanTriggerAssemblyDataType methodNameVariable =
                program.Data.GetOrCreateVariableConstant(typeof(string), customEventName);
            return SendCustomEvent(program, udonBehaviourVariable, methodNameVariable);
        }

        public static List<CyanTriggerAssemblyInstruction> SendCustomEvent(
            CyanTriggerAssemblyProgram program,
            CyanTriggerAssemblyDataType udonBehaviourVariable,
            CyanTriggerAssemblyDataType methodNameVariable)
        {
            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();

            CyanTriggerAssemblyInstruction pushUdonBehaviourAction =
                CyanTriggerAssemblyInstruction.PushVariable(udonBehaviourVariable);
            actions.Add(pushUdonBehaviourAction);

            CyanTriggerAssemblyInstruction pushMethodNameAction =
                CyanTriggerAssemblyInstruction.PushVariable(methodNameVariable);
            actions.Add(pushMethodNameAction);

            string sendCustomEventName =
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(UdonBehaviour).GetMethod(
                        nameof(UdonBehaviour.SendCustomEvent),
                        new[] { typeof(string) }));
            CyanTriggerAssemblyInstruction sendCustomEventExtern =
                CyanTriggerAssemblyInstruction.CreateExtern(sendCustomEventName);
            actions.Add(sendCustomEventExtern);

            return actions;
        }

        public static List<CyanTriggerAssemblyInstruction> SendNetworkEvent(
            CyanTriggerAssemblyProgram program,
            string functionName,
            CyanTriggerAssemblyDataType udonVariable,
            NetworkEventTarget networkTarget = NetworkEventTarget.All)
        {
            Debug.Assert(functionName[0] != '_', $"Trying to broadcast to an event that starts with an '_'. {functionName}");
            
            CyanTriggerAssemblyData data = program.Data;
            return SendNetworkEvent(
                udonVariable,
                data.GetOrCreateVariableConstant(typeof(NetworkEventTarget), networkTarget),
                data.GetOrCreateVariableConstant(typeof(string), functionName));
        }

        public static List<CyanTriggerAssemblyInstruction> SendNetworkEvent(
            CyanTriggerAssemblyDataType udonBehaviourVariable,
            CyanTriggerAssemblyDataType networkTargetVariable,
            CyanTriggerAssemblyDataType methodNameVariable)
        {
            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();

            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(udonBehaviourVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(networkTargetVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(methodNameVariable));

            string networkEventName = CyanTriggerDefinitionResolver.GetMethodSignature(
                typeof(UdonBehaviour).GetMethod(
                    nameof(UdonBehaviour.SendCustomNetworkEvent),
                    new[] { typeof(NetworkEventTarget), typeof(string) }));
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(networkEventName));

            return actions;
        }
        
        public static List<CyanTriggerAssemblyInstruction> DelayEvent(
            CyanTriggerAssemblyProgram program,
            string eventName,
            float durationVariable)
        {
            CyanTriggerAssemblyData data = program.Data;
            return SendCustomEventDelaySeconds(
                data.GetThisConst(typeof(IUdonEventReceiver)),
                data.GetOrCreateVariableConstant(typeof(string), eventName),
                data.GetOrCreateVariableConstant(typeof(float), durationVariable, false),
                data.GetOrCreateVariableConstant(typeof(EventTiming), EventTiming.Update));
        }

        public static List<CyanTriggerAssemblyInstruction> SendCustomEventDelaySeconds(
            CyanTriggerAssemblyDataType udonVariable,
            CyanTriggerAssemblyDataType eventNameVariable,
            CyanTriggerAssemblyDataType secondsVariable,
            CyanTriggerAssemblyDataType eventTimingVariable)
        {
            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();
            
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(udonVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(eventNameVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(secondsVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(eventTimingVariable));

            string sendCustomEventName = 
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(UdonBehaviour).GetMethod(
                        nameof(UdonBehaviour.SendCustomEventDelayedSeconds)));
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(sendCustomEventName));

            return actions;
        }
        
        public static List<CyanTriggerAssemblyInstruction> SendCustomEventDelayFrames(
            CyanTriggerAssemblyDataType udonVariable,
            CyanTriggerAssemblyDataType eventNameVariable,
            CyanTriggerAssemblyDataType framesVariable,
            CyanTriggerAssemblyDataType eventTimingVariable)
        {
            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();
            
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(udonVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(eventNameVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(framesVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(eventTimingVariable));

            string sendCustomEventName = 
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(UdonBehaviour).GetMethod(
                        nameof(UdonBehaviour.SendCustomEventDelayedFrames)));
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(sendCustomEventName));

            return actions;
        }
        
        public static List<CyanTriggerAssemblyInstruction> UdonHasNamedVariable(
            CyanTriggerAssemblyData data,
            CyanTriggerAssemblyDataType udonVariable,
            CyanTriggerAssemblyDataType variableNameVariable,
            CyanTriggerAssemblyDataType variableExistsVariable)
        {
            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();
            
            string getProgramVariableTypeMethodName =
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(UdonBehaviour).GetMethod(nameof(UdonBehaviour.GetProgramVariableType)));

            var typeVariable = data.RequestTempVariable(typeof(Type));

            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(udonVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(variableNameVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(typeVariable));
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(getProgramVariableTypeMethodName));
            
            actions.AddRange(UtilitiesIsValid(typeVariable, variableExistsVariable));
            
            data.ReleaseTempVariable(typeVariable);
            
            return actions;
        }
        
        public static List<CyanTriggerAssemblyInstruction> UtilitiesIsValid(
            CyanTriggerAssemblyDataType objectVariable,
            CyanTriggerAssemblyDataType isValidVariable)
        {
            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();
            
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(objectVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(isValidVariable));

            string isValidMethod = 
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(VRC.SDKBase.Utilities).GetMethod(
                        nameof(VRC.SDKBase.Utilities.IsValid)));
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(isValidMethod));

            return actions;
        }
        


        public static List<CyanTriggerAssemblyInstruction> EventUserGate(
            CyanTriggerAssemblyProgram program, 
            string destMethodName,
            CyanTriggerUserGate userGate, 
            CyanTriggerActionVariableInstance[] userGateExtraData)
        {
            if (userGate == CyanTriggerUserGate.Anyone)
            {
                return JumpToFunction(program, destMethodName);
            }

            CyanTriggerAssemblyData data = program.Data;

            List<CyanTriggerAssemblyInstruction> instructions = new List<CyanTriggerAssemblyInstruction>();

            CyanTriggerAssemblyInstruction nop = CyanTriggerAssemblyInstruction.Nop();
            CyanTriggerAssemblyDataType tempBoolVariable = data.RequestTempVariable(typeof(bool));
            CyanTriggerAssemblyInstruction pushTempBoolAction = 
                CyanTriggerAssemblyInstruction.PushVariable(tempBoolVariable);

            if (userGate == CyanTriggerUserGate.Master)
            {
                instructions.Add(pushTempBoolAction);

                string isMasterMethodName = CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(Networking).GetProperty(
                        nameof(Networking.IsMaster), 
                        BindingFlags.Static | BindingFlags.Public).GetGetMethod());
                instructions.Add(CyanTriggerAssemblyInstruction.CreateExtern(isMasterMethodName));
                
                // Jump to end if false
                instructions.Add(pushTempBoolAction);
                instructions.Add(CyanTriggerAssemblyInstruction.JumpIfFalse(nop));
            }
            else if (userGate == CyanTriggerUserGate.InstanceOwner)
            {
                instructions.Add(CyanTriggerAssemblyInstruction.PushVariable(data.GetThisConst(typeof(VRCPlayerApi))));
                instructions.Add(pushTempBoolAction);
                string isInstanceOwnerMethodName = CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(VRCPlayerApi).GetProperty(nameof(VRCPlayerApi.isInstanceOwner)).GetGetMethod());
                instructions.Add(CyanTriggerAssemblyInstruction.CreateExtern(isInstanceOwnerMethodName));

                // Doesn't require getting the local player, potentially saving one extern if it isn't used
                // Sadly does not work:
                // https://feedback.vrchat.com/bug-reports/p/networkingisinstanceowner-true-for-all-users
                //instructions.Add(pushTempBoolAction);
                //string isInstanceOwnerMethodName = CyanTriggerDefinitionResolver.GetMethodSignature(
                //    typeof(Networking).GetProperty(
                //        nameof(Networking.IsInstanceOwner), 
                //        BindingFlags.Static | BindingFlags.Public).GetGetMethod());
                //instructions.Add(CyanTriggerAssemblyInstruction.CreateExtern(isInstanceOwnerMethodName));

                // Jump to end if false
                instructions.Add(pushTempBoolAction);
                instructions.Add(CyanTriggerAssemblyInstruction.JumpIfFalse(nop));
            }
            else if (userGate == CyanTriggerUserGate.Owner)
            {
                CyanTriggerAssemblyDataType triggerProgramGameObjectVariable = data.GetThisConst(typeof(GameObject));
                CyanTriggerAssemblyInstruction pushGameObjectAction = 
                    CyanTriggerAssemblyInstruction.PushVariable(triggerProgramGameObjectVariable);
                instructions.Add(pushGameObjectAction);
                instructions.Add(pushTempBoolAction);

                instructions.Add(
                    CyanTriggerAssemblyInstruction.CreateExtern(
                        CyanTriggerDefinitionResolver.GetMethodSignature(
                            typeof(Networking).GetMethod(
                                nameof(Networking.IsOwner), 
                                BindingFlags.Static | BindingFlags.Public, 
                                null, 
                                new [] { typeof(GameObject) }, null))));
                
                // Jump to end if false
                instructions.Add(pushTempBoolAction);
                instructions.Add(CyanTriggerAssemblyInstruction.JumpIfFalse(nop));
            }
            else if (userGate == CyanTriggerUserGate.UserAllowList ||
                     userGate == CyanTriggerUserGate.UserDenyList)
            {
                bool allow = userGate == CyanTriggerUserGate.UserAllowList;
                List<CyanTriggerAssemblyDataType> variablesToCheck = new List<CyanTriggerAssemblyDataType>();
                for (int curUser = 0; curUser < userGateExtraData.Length; ++curUser)
                {
                    CyanTriggerAssemblyDataType variable =
                        CyanTriggerCompiler.GetInputDataFromVariableInstance(data, userGateExtraData[curUser],
                            typeof(string));

                    if (variable == null)
                    {
                        continue;
                    }
                    variablesToCheck.Add(variable);
                }
                
                // Nothing, so never allow 
                if (variablesToCheck.Count == 0)
                {
                    // Never allow since there isn't anyone to allow
                    if (allow)
                    {
                        // Jump to end
                        instructions.Add(CyanTriggerAssemblyInstruction.PushVariable(
                            data.GetOrCreateVariableConstant(typeof(bool), false)));
                        instructions.Add(CyanTriggerAssemblyInstruction.JumpIfFalse(nop));
                    }
                    // No one on the deny list, so always pass.
                    else
                    {
                        // No need to do anything here as it will just be true.
                    }
                }
                else
                {
                    CyanTriggerAssemblyInstruction pushLocalPlayer =
                        CyanTriggerAssemblyInstruction.PushVariable(data.GetThisConst(typeof(VRCPlayerApi)));
                    
                    CyanTriggerAssemblyDataType tempStringVariable = data.RequestTempVariable(typeof(string));
                    CyanTriggerAssemblyInstruction pushTempString = 
                        CyanTriggerAssemblyInstruction.PushVariable(tempStringVariable);
                    
                    // push local player
                    // push temp string
                    // extern player api display name
                    instructions.Add(pushLocalPlayer);
                    instructions.Add(pushTempString);
                    instructions.Add(CyanTriggerAssemblyInstruction.CreateExtern(
                        CyanTriggerDefinitionResolver.GetFieldSignature(
                            typeof(VRCPlayerApi).GetField(nameof(VRCPlayerApi.displayName)),
                            FieldOperation.Get)));


                    // For deny lists, if you find a match, jump to the end, skipping the method.
                    CyanTriggerAssemblyInstruction foundMatchNop = nop;
                    if (allow)
                    {
                        // For allow list, if you find a match, jump just before the actions
                        foundMatchNop = CyanTriggerAssemblyInstruction.Nop(); 
                    }
                    
                    foreach (var variable in variablesToCheck)
                    {
                        // push temp string
                        // push string specific
                        // push temp bool
                        // extern string compares
                        instructions.Add(CyanTriggerAssemblyInstruction.PushVariable(variable));
                        instructions.Add(pushTempString);
                        instructions.Add(pushTempBoolAction);
                        instructions.Add(CyanTriggerAssemblyInstruction.CreateExtern(
                            CyanTriggerDefinitionResolver.GetMethodSignature(
                                typeof(string).GetMethod(nameof(string.Equals),
                                    BindingFlags.Static | BindingFlags.Public,
                                    null,
                                    new []{typeof(string), typeof(string)},
                                    null))));

                        // Negate the value since we want false if name is equal
                        instructions.Add(pushTempBoolAction);
                        instructions.Add(pushTempBoolAction);
                        instructions.Add(CyanTriggerAssemblyInstruction.CreateExtern(
                            CyanTriggerDefinitionResolver.GetPrimitiveOperationSignature(
                                typeof(bool), 
                                PrimitiveOperation.UnaryNegation)));
                        
                        // Jump to method if name is equal
                        instructions.Add(pushTempBoolAction);
                        instructions.Add(CyanTriggerAssemblyInstruction.JumpIfFalse(foundMatchNop));
                    }

                    if (allow)
                    {
                        // No matches, jump to end without executing
                        instructions.Add(CyanTriggerAssemblyInstruction.Jump(nop));
                        instructions.Add(foundMatchNop);
                    }
                    
                    data.ReleaseTempVariable(tempStringVariable);
                }
            }

            instructions.AddRange(JumpToFunction(program, destMethodName));

            instructions.Add(nop);

            data.ReleaseTempVariable(tempBoolVariable);
            return instructions;
        }
        
        // network event
        public static List<CyanTriggerAssemblyInstruction> EventBroadcast(
            CyanTriggerAssemblyProgram program,
            string destMethodName,
            CyanTriggerBroadcast broadcast)
        {
            Debug.Assert(destMethodName[0] != '_', $"Trying to broadcast to an event that starts with an '_'. {destMethodName}");

            CyanTriggerAssemblyDataType udonVariable = program.Data.GetThisConst(typeof(IUdonEventReceiver));
            
            if (broadcast == CyanTriggerBroadcast.All)
            {
                return SendNetworkEvent(program, destMethodName, udonVariable, NetworkEventTarget.All);
            }
            
            if (broadcast == CyanTriggerBroadcast.Owner)
            {
                return SendNetworkEvent(program, destMethodName, udonVariable, NetworkEventTarget.Owner);
            }

            return EmptyActions;
        }

        public static List<CyanTriggerAssemblyInstruction> CheckBroadcastCountAndLogError(
            CyanTriggerAssemblyProgram program,
            string eventName)
        {
            var data = program.Data;
            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();

            var broadcastCount = 
                data.GetSpecialVariable(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.BroadcastCount);
            var constZero = data.GetOrCreateVariableConstant(typeof(uint), 0u);
            
            CyanTriggerAssemblyDataType tempBoolVariable = data.RequestTempVariable(typeof(bool));
            CyanTriggerAssemblyInstruction pushTempBoolAction = 
                CyanTriggerAssemblyInstruction.PushVariable(tempBoolVariable);

            var endNop = CyanTriggerAssemblyInstruction.Nop();
            
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(broadcastCount));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(constZero));
            actions.Add(pushTempBoolAction);
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(
                CyanTriggerDefinitionResolver.GetPrimitiveOperationSignature(
                    typeof(uint), PrimitiveOperation.GreaterThan)));
            
            // Jump to end if false
            actions.Add(pushTempBoolAction);
            actions.Add(CyanTriggerAssemblyInstruction.JumpIfFalse(endNop));

            actions.AddRange(DebugLog(
                $"[CyanTrigger] Sending network event \"{eventName}\" while in a network event! This can clog the network and can crash players. Make sure to never send a network event from a network event!", 
                data, 
                nameof(Debug.LogError)));
            
            actions.Add(endNop);
            
            return actions;
        }

        public static void AddBroadcastCountToNetworkedEvent(
            CyanTriggerAssemblyProgram program,
            CyanTriggerAssemblyMethod method)
        {
            var data = program.Data;
            var broadcastCount = 
                data.GetSpecialVariable(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.BroadcastCount);

            var constOne = data.GetOrCreateVariableConstant(typeof(uint), 1u);

            var increment = new List<CyanTriggerAssemblyInstruction>
            {
                CyanTriggerAssemblyInstruction.PushVariable(broadcastCount),
                CyanTriggerAssemblyInstruction.PushVariable(constOne),
                CyanTriggerAssemblyInstruction.PushVariable(broadcastCount),
                CyanTriggerAssemblyInstruction.CreateExtern(
                    CyanTriggerDefinitionResolver.GetPrimitiveOperationSignature(typeof(uint), PrimitiveOperation.Addition))
            };
            
            var decrement = new List<CyanTriggerAssemblyInstruction>
            {
                CyanTriggerAssemblyInstruction.PushVariable(broadcastCount),
                CyanTriggerAssemblyInstruction.PushVariable(constOne),
                CyanTriggerAssemblyInstruction.PushVariable(broadcastCount),
                CyanTriggerAssemblyInstruction.CreateExtern(
                    CyanTriggerDefinitionResolver.GetPrimitiveOperationSignature(typeof(uint), PrimitiveOperation.Subtraction))
            };
            
            method.AddActionsFirst(increment);
            method.AddActionsLast(decrement);
        }

        public static List<CyanTriggerAssemblyInstruction> SendToTimerQueue(
            CyanTriggerAssemblyProgram program,
            string eventName, 
            float durationVariable)
        {
            CyanTriggerAssemblyDataType floatVariable =
                program.Data.GetOrCreateVariableConstant(typeof(float), durationVariable, false);
            CyanTriggerAssemblyDataType udonVariable = program.Data.GetThisConst(typeof(IUdonEventReceiver));
            CyanTriggerAssemblyDataType eventNameVariable = 
                program.Data.GetOrCreateVariableConstant(typeof(string), eventName);
            
            return SendToTimerQueue(program, udonVariable, eventNameVariable, floatVariable);
        }
        
        public static List<CyanTriggerAssemblyInstruction> SendToTimerQueue(
            CyanTriggerAssemblyProgram program, 
            CyanTriggerAssemblyDataType udonVariable, 
            CyanTriggerAssemblyDataType eventNameVariable, 
            CyanTriggerAssemblyDataType durationVariable)
        {
            CyanTriggerAssemblyData data = program.Data;

            CyanTriggerAssemblyDataType timerQueueVariable = 
                data.GetSpecialVariable(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.TimerQueue);
            CyanTriggerAssemblyInstruction pushTimerQueue = 
                CyanTriggerAssemblyInstruction.PushVariable(timerQueueVariable);


            CyanTriggerAssemblyDataType udonParamNameVariable = 
                data.GetOrCreateVariableConstant(typeof(string), "EventGraph", false);
            CyanTriggerAssemblyDataType eventParamNameVariable = 
                data.GetOrCreateVariableConstant(typeof(string), "EventName", false);
            CyanTriggerAssemblyDataType durationParamNameVariable = 
                data.GetOrCreateVariableConstant(typeof(string), "EventDuration", false);

            string setProgramVariableMethodName = 
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(UdonBehaviour).GetMethod(
                        nameof(UdonBehaviour.SetProgramVariable),
                        new [] { typeof(string), typeof(object) }));

            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();

            // Push udon graph
            actions.Add(pushTimerQueue);
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(udonParamNameVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(udonVariable));
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(setProgramVariableMethodName));

            // Push event name
            actions.Add(pushTimerQueue);
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(eventParamNameVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(eventNameVariable));
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(setProgramVariableMethodName));

            // Push duration
            actions.Add(pushTimerQueue);
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(durationParamNameVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(durationVariable));
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(setProgramVariableMethodName));

            CyanTriggerAssemblyDataType methodNameVariable = 
                program.Data.GetOrCreateVariableConstant(typeof(string), "Add");
            // Call add
            actions.AddRange(SendCustomEvent(program, timerQueueVariable, methodNameVariable));

            return actions;
        }
        
        public static List<CyanTriggerAssemblyInstruction> RemoveFromTimerQueue(
            CyanTriggerAssemblyProgram program, 
            UdonBehaviour udonBehaviour, 
            string eventName)
        {
            CyanTriggerAssemblyData data = program.Data;

            CyanTriggerAssemblyDataType timerQueueVariable = 
                data.GetSpecialVariable(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.TimerQueue);
            CyanTriggerAssemblyInstruction pushTimerQueue = 
                CyanTriggerAssemblyInstruction.PushVariable(timerQueueVariable);

            CyanTriggerAssemblyDataType udonVariable = 
                data.GetOrCreateVariableConstant(typeof(UdonBehaviour), udonBehaviour, true);
            CyanTriggerAssemblyDataType eventNameVariable = data.GetOrCreateVariableConstant(typeof(string), eventName);

            CyanTriggerAssemblyDataType udonParamNameVariable = 
                data.GetOrCreateVariableConstant(typeof(string), "EventGraph", false);
            CyanTriggerAssemblyDataType eventParamNameVariable = 
                data.GetOrCreateVariableConstant(typeof(string), "EventName", false);

            string setProgramVariableMethodName = 
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(UdonBehaviour).GetMethod(
                        nameof(UdonBehaviour.SetProgramVariable), 
                        new [] { typeof(string), typeof(object) }));

            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();

            // Push udon graph
            actions.Add(pushTimerQueue);
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(udonParamNameVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(udonVariable));
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(setProgramVariableMethodName));

            // Push event name
            actions.Add(pushTimerQueue);
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(eventParamNameVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(eventNameVariable));
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(setProgramVariableMethodName));

            
            CyanTriggerAssemblyDataType methodNameVariable = 
                program.Data.GetOrCreateVariableConstant(typeof(string), "Remove");
            // Call remove
            actions.AddRange(SendCustomEvent(program, timerQueueVariable, methodNameVariable));

            return actions;
        }
        

        public static List<CyanTriggerAssemblyInstruction> GetLocalPlayer(CyanTriggerAssemblyProgram program)
        {
            CyanTriggerAssemblyData data = program.Data;

            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();

            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(data.GetThisConst(typeof(VRCPlayerApi))));
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(Networking).GetProperty(nameof(Networking.LocalPlayer)).GetGetMethod())));
            
            return actions;
        }

        public static List<CyanTriggerAssemblyInstruction> GetLocalPlayerOneTimeInitialization(
            CyanTriggerAssemblyProgram program,
            CyanTriggerAssemblyDataType initVariable)
        {
            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();

            var pushInitVariable = CyanTriggerAssemblyInstruction.PushVariable(initVariable);
            var endNop = CyanTriggerAssemblyInstruction.Nop();
            
            var constFalse = program.Data.GetOrCreateVariableConstant(typeof(bool), false);

            // Jump to end if false
            actions.Add(pushInitVariable);
            actions.Add(CyanTriggerAssemblyInstruction.JumpIfFalse(endNop));
            
            // Ensure this only happens once by setting the variable to false.
            actions.AddRange(CopyVariables(constFalse, initVariable));
            
            // Get the local player variable.
            actions.AddRange(GetLocalPlayer(program));
            
            actions.Add(endNop);
            
            return actions;
        }

        public static List<CyanTriggerAssemblyInstruction> RequestSerialization(
            CyanTriggerAssemblyProgram program)
        {
            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();

            var thisUdon = program.Data.GetThisConst(typeof(IUdonEventReceiver));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(thisUdon));
            
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(UdonBehaviour).GetMethod(nameof(UdonBehaviour.RequestSerialization)))));
            
            return actions;
        }

        public static List<CyanTriggerAssemblyInstruction> SetOwnerToSelf(
            CyanTriggerAssemblyProgram program)
        {
            CyanTriggerAssemblyData data = program.Data;
            
            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();

            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(data.GetThisConst(typeof(VRCPlayerApi))));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(data.GetThisConst(typeof(GameObject))));
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(Networking).GetMethod(nameof(Networking.SetOwner)))));
            
            return actions;
        }

        public static List<CyanTriggerAssemblyInstruction> CheckIsOwner(
            CyanTriggerAssemblyProgram program, 
            CyanTriggerAssemblyInstruction endNop,
            bool notOwner = false)
        {
            CyanTriggerAssemblyData data = program.Data;
            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();

            CyanTriggerAssemblyDataType tempBoolVariable = data.RequestTempVariable(typeof(bool));
            CyanTriggerAssemblyInstruction pushTempBoolAction = 
                CyanTriggerAssemblyInstruction.PushVariable(tempBoolVariable);
            
            CyanTriggerAssemblyDataType triggerProgramGameObjectVariable = data.GetThisConst(typeof(GameObject));
            CyanTriggerAssemblyInstruction pushGameObjectAction = 
                CyanTriggerAssemblyInstruction.PushVariable(triggerProgramGameObjectVariable);
            actions.Add(pushGameObjectAction);
            actions.Add(pushTempBoolAction);

            actions.Add(
                CyanTriggerAssemblyInstruction.CreateExtern(
                    CyanTriggerDefinitionResolver.GetMethodSignature(
                        typeof(Networking).GetMethod(
                            nameof(Networking.IsOwner), 
                            BindingFlags.Static | BindingFlags.Public, 
                            null, 
                            new [] { typeof(GameObject) }, null))));

            // Negate results to make it if user is not owner
            if (notOwner)
            {
                actions.Add(pushTempBoolAction);
                actions.Add(pushTempBoolAction);
                actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(
                    CyanTriggerDefinitionResolver.GetPrimitiveOperationSignature(
                        typeof(bool), 
                        PrimitiveOperation.UnaryNegation)));
            }
            
            // Jump to end if false
            actions.Add(pushTempBoolAction);
            actions.Add(CyanTriggerAssemblyInstruction.JumpIfFalse(endNop));
            
            data.ReleaseTempVariable(tempBoolVariable);
            
            return actions;
        }

        #region Replay Helpers

        public static List<CyanTriggerAssemblyInstruction> ReplayOwnerRequestSerialization(
            CyanTriggerAssemblyProgram program)
        {
            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();
            
            CyanTriggerAssemblyInstruction nop = CyanTriggerAssemblyInstruction.Nop();
            
            // Check if owner before performing serialization.
            actions.AddRange(CheckIsOwner(program, nop));
            
            // Body: Request serialization
            actions.AddRange(RequestSerialization(program));
            
            actions.Add(nop);

            
            return actions;
        }
        
        public static List<CyanTriggerAssemblyInstruction> ReplayUpdateEventCount(
            CyanTriggerAssemblyProgram program,
            CyanTriggerAssemblyDataType replayVariable,
            CyanTriggerReplayData replayData)
        {
            var data = program.Data;
            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();

            CyanTriggerAssemblyInstruction endNop = CyanTriggerAssemblyInstruction.Nop();
            
            // Check if currently executing replay, if so jump to the end without doing anything.
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(replayData.NotExecutingReplay));
            actions.Add(CyanTriggerAssemblyInstruction.JumpIfFalse(endNop));
            
            // Check if owner before updating replay variable.
            actions.AddRange(CheckIsOwner(program, endNop));
            
            // User is confirmed the owner, update replay variable and request serialization
            
            // variable = variable + 1
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(replayVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(program.Data.GetOrCreateVariableConstant(typeof(int), 1)));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(replayVariable));
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(
                CyanTriggerDefinitionResolver.GetPrimitiveOperationSignature(typeof(int), PrimitiveOperation.Addition)));
            
            // Request serialization
            actions.AddRange(RequestSerialization(program));
            
            actions.Add(endNop);

            return actions;
        }
        
        public static List<CyanTriggerAssemblyInstruction> ReplayClearEventCount(
            CyanTriggerAssemblyProgram program,
            CyanTriggerAssemblyDataType replayVariable,
            CyanTriggerReplayData replayData)
        {
            var data = program.Data;
            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();

            CyanTriggerAssemblyInstruction endNop = CyanTriggerAssemblyInstruction.Nop();
            
            // Check if currently executing replay, if so jump to the end without doing anything.
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(replayData.NotExecutingReplay));
            actions.Add(CyanTriggerAssemblyInstruction.JumpIfFalse(endNop));
            
            // Check if this CyanTrigger is currently broadcasting and decide if owner should be set or gated on is owner.
            var tempBool = data.RequestTempVariable(typeof(bool));
            var pushTempBool = CyanTriggerAssemblyInstruction.PushVariable(tempBool);
            
            var constUZero = data.GetOrCreateVariableConstant(typeof(uint), 0u);
            var broadcastCount = 
                data.GetSpecialVariable(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.BroadcastCount);

            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(broadcastCount));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(constUZero));
            actions.Add(pushTempBool);
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(
                CyanTriggerDefinitionResolver.GetPrimitiveOperationSignature(typeof(uint),
                    PrimitiveOperation.Equality)));
            
            CyanTriggerAssemblyInstruction incrementNop = CyanTriggerAssemblyInstruction.Nop();
            CyanTriggerAssemblyInstruction checkOwnerNop = CyanTriggerAssemblyInstruction.Nop();
            
            // If broadcast count is 0, fall through to set owner
            // Else jump to the owner check to verify if the current user should set the value or not.
            actions.Add(pushTempBool);
            actions.Add(CyanTriggerAssemblyInstruction.JumpIfFalse(checkOwnerNop));
            data.ReleaseTempVariable(tempBool);
            
            
            // If not currently in a broadcast event, set owner to self and clear replay variable.
            actions.AddRange(SetOwnerToSelf(program));
            actions.Add(CyanTriggerAssemblyInstruction.Jump(incrementNop));
            
            
            // If currently in a broadcast event, check if current owner of the object. If not owner, do nothing.
            actions.Add(checkOwnerNop);
            
            // Check if owner
            actions.AddRange(CheckIsOwner(program, endNop));
            
            
            // User has passed all checks, set replay count to zero.
            actions.Add(incrementNop);
            
            // Set replay counter to 0.
            var zeroConst = data.GetOrCreateVariableConstant(typeof(int), 0);
            actions.AddRange(CopyVariables(zeroConst, replayVariable));
            
            // Request serialization
            actions.AddRange(RequestSerialization(program));
            
            actions.Add(endNop);
            
            return actions;
        }

        // Added in OnDeserialization to perform Event Replay
        public static List<CyanTriggerAssemblyInstruction> ReplayAddVariableChecks(
            CyanTriggerAssemblyProgram program,
            CyanTriggerReplayData replayData)
        {
            CyanTriggerAssemblyData data = program.Data;

            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();
            
            CyanTriggerAssemblyInstruction nop = CyanTriggerAssemblyInstruction.Nop();
            CyanTriggerAssemblyDataType tempBoolVariable = data.RequestTempVariable(typeof(bool));
            CyanTriggerAssemblyInstruction pushTempBoolAction = 
                CyanTriggerAssemblyInstruction.PushVariable(tempBoolVariable);
            
            // First check that Master has set the sync variable data.
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(replayData.SyncSetData));
            actions.Add(CyanTriggerAssemblyInstruction.JumpIfFalse(nop));
            
            // Next check if the local player has already gone through replay data.
            // (Invert since we want to fall through when it is false)
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(replayData.LocalInitialized));
            actions.Add(pushTempBoolAction);
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(
                CyanTriggerDefinitionResolver.GetPrimitiveOperationSignature(
                    typeof(bool), 
                    PrimitiveOperation.UnaryNegation)));
            
            actions.Add(pushTempBoolAction);
            actions.Add(CyanTriggerAssemblyInstruction.JumpIfFalse(nop));
            
            
            var trueConst = data.GetOrCreateVariableConstant(typeof(bool), true);
            var falseConst = data.GetOrCreateVariableConstant(typeof(bool), false);
            
            // Set local initialized to true
            actions.AddRange(CopyVariables(trueConst, replayData.LocalInitialized));
            
            // Set not executing replay false
            actions.AddRange(CopyVariables(falseConst, replayData.NotExecutingReplay));

            var zeroConst = data.GetOrCreateVariableConstant(typeof(int), 0);
            CyanTriggerAssemblyInstruction pushConstZeroAction = CyanTriggerAssemblyInstruction.PushVariable(zeroConst);
            CyanTriggerAssemblyInstruction intGreater = CyanTriggerAssemblyInstruction.CreateExtern(
                CyanTriggerDefinitionResolver.GetPrimitiveOperationSignature(typeof(int),
                    PrimitiveOperation.GreaterThan));

            int count = 0;
            // Body: go through all replay data types
            foreach (var eventReplayData in replayData.OrderedData)
            {
                CyanTriggerAssemblyInstruction eventDataNop = CyanTriggerAssemblyInstruction.Nop();

                actions.Add(CyanTriggerAssemblyInstruction.PushVariable(eventReplayData.Variable));
                actions.Add(pushConstZeroAction);
                actions.Add(pushTempBoolAction);
                actions.Add(intGreater);
                
                actions.Add(pushTempBoolAction);
                actions.Add(CyanTriggerAssemblyInstruction.JumpIfFalse(eventDataNop));
                
                switch (eventReplayData.ReplayType)
                {
                    case CyanTriggerReplay.ReplayOnce:
                        ++count;
                        actions.AddRange(ReplayAddReplayOnce(program, eventReplayData));
                        break;
                    
                    case CyanTriggerReplay.ReplayParity:
                        ++count;
                        actions.AddRange(ReplayAddReplayParity(program, eventReplayData));
                        break;
                    
                    case CyanTriggerReplay.ReplayAll:
                        ++count;
                        actions.AddRange(ReplayAddReplayAll(program, eventReplayData));
                        break;
                    
                    default:
                        Debug.LogWarning($"Invalid replay type: {eventReplayData.ReplayType}");
                        break;
                }
                
                actions.Add(eventDataNop);
            }

            if (count == 0)
            {
                Debug.LogError("Trying to replay when no replay events were compiled!");
            }
            
            // Set not executing replay true
            actions.AddRange(CopyVariables(trueConst, replayData.NotExecutingReplay));
            
            // End jump
            actions.Add(nop);
            
            data.ReleaseTempVariable(tempBoolVariable);
            
            return actions;
        }
        
        public static List<CyanTriggerAssemblyInstruction> ReplayAddReplayOnce(
            CyanTriggerAssemblyProgram program,
            CyanTriggerEventReplayData eventReplayData)
        {
            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();

            actions.AddRange(JumpToFunction(program, eventReplayData.MethodName));
            
            return actions;
        }
        
        public static List<CyanTriggerAssemblyInstruction> ReplayAddReplayParity(
            CyanTriggerAssemblyProgram program,
            CyanTriggerEventReplayData eventReplayData)
        {
            CyanTriggerAssemblyData data = program.Data;

            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();

            // Always add first action
            actions.AddRange(JumpToFunction(program, eventReplayData.MethodName));
            
            // Check if parity is even to call action again.
            CyanTriggerAssemblyInstruction nop = CyanTriggerAssemblyInstruction.Nop();
            
            CyanTriggerAssemblyDataType tempBoolVariable = data.RequestTempVariable(typeof(bool));
            CyanTriggerAssemblyInstruction pushTempBoolAction = 
                CyanTriggerAssemblyInstruction.PushVariable(tempBoolVariable);
            CyanTriggerAssemblyDataType tempIntVariable = data.RequestTempVariable(typeof(int));
            CyanTriggerAssemblyInstruction pushTempIntAction = 
                CyanTriggerAssemblyInstruction.PushVariable(tempIntVariable);
            
            var zeroConst = data.GetOrCreateVariableConstant(typeof(int), 0);
            var twoConst = data.GetOrCreateVariableConstant(typeof(int), 2);
            
            CyanTriggerAssemblyInstruction pushConstZeroAction = CyanTriggerAssemblyInstruction.PushVariable(zeroConst);
            CyanTriggerAssemblyInstruction pushConstTwoAction = CyanTriggerAssemblyInstruction.PushVariable(twoConst);
            
            CyanTriggerAssemblyInstruction intRemainder = CyanTriggerAssemblyInstruction.CreateExtern(
                CyanTriggerDefinitionResolver.GetPrimitiveOperationSignature(typeof(int),
                    PrimitiveOperation.Remainder));
            CyanTriggerAssemblyInstruction intEquality = CyanTriggerAssemblyInstruction.CreateExtern(
                CyanTriggerDefinitionResolver.GetPrimitiveOperationSignature(typeof(int),
                    PrimitiveOperation.Equality));
            
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(eventReplayData.Variable));
            actions.Add(pushConstTwoAction);
            actions.Add(pushTempIntAction);
            actions.Add(intRemainder);
            
            actions.Add(pushTempIntAction);
            actions.Add(pushConstZeroAction);
            actions.Add(pushTempBoolAction);
            actions.Add(intEquality);
            
            actions.Add(pushTempBoolAction);
            actions.Add(CyanTriggerAssemblyInstruction.JumpIfFalse(nop));
            
            // Body: Call method again
            actions.AddRange(JumpToFunction(program, eventReplayData.MethodName));
            
            // End jump
            actions.Add(nop);
            
            data.ReleaseTempVariable(tempIntVariable);
            data.ReleaseTempVariable(tempBoolVariable);
            
            return actions;
        }
        
        public static List<CyanTriggerAssemblyInstruction> ReplayAddReplayAll(
            CyanTriggerAssemblyProgram program,
            CyanTriggerEventReplayData eventReplayData)
        {
            CyanTriggerAssemblyData data = program.Data;

            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();
            
            CyanTriggerAssemblyDataType zeroConst = data.GetOrCreateVariableConstant(typeof(int), 0);
            CyanTriggerAssemblyDataType oneConst = data.GetOrCreateVariableConstant(typeof(int), 1);
            
            CyanTriggerAssemblyDataType tempIntVariable = data.RequestTempVariable(typeof(int));
            
            CyanTriggerAssemblyInstruction startNop = CyanTriggerAssemblyInstruction.Nop();
            CyanTriggerAssemblyInstruction endNop = CyanTriggerAssemblyInstruction.Nop();
            
            actions.AddRange(CyanTriggerCustomNodeLoopFor.BeginForLoop(
                program, zeroConst, eventReplayData.Variable, oneConst, tempIntVariable, startNop, endNop));
            
            // Body: Call method
            actions.AddRange(JumpToFunction(program, eventReplayData.MethodName));

            actions.AddRange(CyanTriggerCustomNodeLoopFor.EndForLoop(startNop, endNop));

            data.ReleaseTempVariable(tempIntVariable);
            return actions;
        }

        #endregion

        #region Custom Action Event Instance

        public static List<CyanTriggerAssemblyInstruction> AddSendEventForCustomActionInstance(
            CyanTriggerAssemblyProgram program,
            string instanceNamePostfix,
            CyanTriggerAssemblyData.CyanTriggerSpecialVariableName specialEventVariable,
            Dictionary<CyanTriggerAssemblyData.CyanTriggerSpecialVariableName, string> specialVariableTranslations)
        {
            var data = program.Data;
            
            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();

            var thisUdon = data.GetThisConst(typeof(UdonBehaviour));
            
            Type stringType = typeof(string);
            // Get event variable, and append instanceNamePrefix
            var eventVariableName =
                specialVariableTranslations[CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ActionInstanceEventName];
            var eventVariable = data.GetVariableNamed(eventVariableName);

            var pushEventVariable = CyanTriggerAssemblyInstruction.PushVariable(eventVariable);
            var pushPostfix =
                CyanTriggerAssemblyInstruction.PushVariable(
                    data.GetOrCreateVariableConstant(stringType, instanceNamePostfix));
            
            actions.Add(pushEventVariable);
            actions.Add(pushPostfix);
            actions.Add(pushEventVariable);
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(CyanTriggerDefinitionResolver.GetMethodSignature(
                stringType.GetMethod(nameof(string.Concat), new[] { stringType, stringType }))));
            
            // SendCustomEvent shouldn't happen due to it happening in the same frame with the instance variables already set.
            if (specialEventVariable ==
                CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ActionInstanceEventJumpAddress)
            {
                actions.AddRange(SendCustomEvent(program, thisUdon, eventVariable));
            }
            else if (specialEventVariable ==
                     CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ActionInstanceEventDelaySecondsJumpAddress)
            {
                var delaySecondsVariableName =
                    specialVariableTranslations[CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ActionInstanceEventDelaySeconds];
                var delaySecondsVariable = data.GetVariableNamed(delaySecondsVariableName);
                
                var delayTimingVariableName =
                    specialVariableTranslations[CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ActionInstanceEventDelayTiming];
                var delayTimingVariable = data.GetVariableNamed(delayTimingVariableName);
                
                actions.AddRange(SendCustomEventDelaySeconds(thisUdon, eventVariable, delaySecondsVariable, delayTimingVariable));
            }
            else if (specialEventVariable ==
                     CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ActionInstanceEventDelayFramesJumpAddress)
            {
                var delayFramesVariableName =
                    specialVariableTranslations[CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ActionInstanceEventDelayFrames];
                var delayFramesVariable = data.GetVariableNamed(delayFramesVariableName);
                
                var delayTimingVariableName =
                    specialVariableTranslations[CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ActionInstanceEventDelayTiming];
                var delayTimingVariable = data.GetVariableNamed(delayTimingVariableName);

                actions.AddRange(SendCustomEventDelayFrames(thisUdon, eventVariable, delayFramesVariable, delayTimingVariable));
            }
            else if (specialEventVariable ==
                     CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ActionInstanceEventNetworkedJumpAddress)
            {
                var networkTargetVariableName =
                    specialVariableTranslations[CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ActionInstanceEventNetworkTarget];
                var networkTargetVariable = data.GetVariableNamed(networkTargetVariableName);
                
                actions.AddRange(SendNetworkEvent(thisUdon, networkTargetVariable, eventVariable));
            }
            else
            {
                throw new Exception($"Invalid special variable type: {specialEventVariable}");
            }

            return actions;
        }

        #endregion
        
        #region Debug
        
        public static List<CyanTriggerAssemblyInstruction> DebugLog(
            string message, 
            CyanTriggerAssemblyData data, 
            string methodName = "Log")
        {
            CyanTriggerAssemblyDataType messageVariable = data.GetOrCreateVariableConstant(typeof(string), message);
            string udonMethodName = CyanTriggerDefinitionResolver.GetMethodSignature(
                typeof(Debug).GetMethod(methodName, new [] { typeof(object), typeof(UnityEngine.Object) }));

            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(messageVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(CyanTriggerAssemblyDataConsts.ThisGameObject.ID));
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(udonMethodName));

            return actions;
        }

        public static List<CyanTriggerAssemblyInstruction> DebugLogTopHeap(string methodName = "Log")
        {
            string udonMethodName = CyanTriggerDefinitionResolver.GetMethodSignature(
            typeof(Debug).GetMethod(methodName, new [] { typeof(object), typeof(UnityEngine.Object) }));

            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(CyanTriggerAssemblyDataConsts.ThisGameObject.ID));
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(udonMethodName));

            return actions;
        }

        #endregion
    }
}
