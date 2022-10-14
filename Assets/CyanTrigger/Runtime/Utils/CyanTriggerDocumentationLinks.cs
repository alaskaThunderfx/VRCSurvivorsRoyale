using System.Collections.Generic;

namespace Cyan.CT
{
    public static class CyanTriggerDocumentationLinks
    {
        public const string WikiLink = "https://github.com/CyanLaser/CyanTrigger/wiki";
        public const string CustomAction = WikiLink + "/Custom-Actions";
        public const string EditableProgramAsset = WikiLink + "/CyanTrigger-Program-Asset";
        public const string ProgramAsset = WikiLink + "/CyanTrigger-Interface"; // TODO make wiki page on this
        public const string CyanTriggerAsset = WikiLink + "/CyanTrigger-Program-Asset";
        public const string CyanTrigger = WikiLink + "/CyanTrigger-Interface";
        
        #region Interface settings links

        public static readonly string VariablesSettingsDocumentation = $"{WikiLink}/Variables";
        public static readonly string OtherSettingsDocumentation = $"{WikiLink}/CyanTrigger-Interface#other-settings";
        public static readonly string SyncSettingsDocumentation = $"{WikiLink}/Networking#sync-settings";
        public static readonly string EventsDocumentation = $"{WikiLink}/CyanTrigger-Interface#events";
        public static readonly string ActionsDocumentation = $"{WikiLink}/CyanTrigger-Interface#actions";

        #endregion

        #region Custom Node Events

        public static readonly string OnTimerNodeDocumentation = $"{WikiLink}/CyanTrigger-Events-and-Actions#OnTimer-Event";
        public static readonly string OnVariableChangeNodeDocumentation = $"{WikiLink}/Variables#onvariablechanged";
        public static readonly string CustomEventNodeDocumentation = $"{WikiLink}/CyanTrigger-Events-and-Actions#Custom-Event";

        #endregion
        
        #region Custom Node Actions

        #region Special Actions

        public static readonly string BlockNodeDocumentation = $"{WikiLink}/Special-Actions#block";
        public static readonly string ConditionNodeDocumentation = $"{WikiLink}/Special-Actions#condition";
        public static readonly string ConditionBodyNodeDocumentation = $"{WikiLink}/Special-Actions#conditionbody";
        public static readonly string FailIfFalseNodeDocumentation = $"{WikiLink}/Special-Actions#conditionfailiffalse";
        public static readonly string PassIfTrueNodeDocumentation = $"{WikiLink}/Special-Actions#conditionpassiftrue";
        public static readonly string IfNodeDocumentation = $"{WikiLink}/Special-Actions#if";
        public static readonly string ElseNodeDocumentation = $"{WikiLink}/Special-Actions#else";
        public static readonly string ElseIfNodeDocumentation = $"{WikiLink}/Special-Actions#else-if";
        public static readonly string WhileNodeDocumentation = $"{WikiLink}/Special-Actions#while-loop";
        public static readonly string ForNodeDocumentation = $"{WikiLink}/Special-Actions#for-loop";
        public static readonly string ForeachNodeDocumentation = $"{WikiLink}/Special-Actions#foreach-loop";
        public static readonly string BreakNodeDocumentation = $"{WikiLink}/Special-Actions#break";
        public static readonly string ContinueNodeDocumentation = $"{WikiLink}/Special-Actions#continue";
        public static readonly string ReturnNodeDocumentation = $"{WikiLink}/Special-Actions#return";
        public static readonly string ReturnIfDisabledNodeDocumentation = $"{WikiLink}/Special-Actions#returnifdisabled";

        #endregion
        
        public static readonly string ClearReplayNodeDocumentation = $"{WikiLink}/Networking#clear-replay-action";
        public static readonly string CommentNodeDocumentation = $"{WikiLink}/CyanTrigger-Events-and-Actions#comment";
        public static readonly string SetComponentActiveNodeDocumentation = $"{WikiLink}/CyanTrigger-Events-and-Actions#GameObjectSetComponentActive";
        public static readonly string SetComponentActiveToggleNodeDocumentation = $"{WikiLink}/CyanTrigger-Events-and-Actions#GameObjectSetComponentActiveToggle";
        public static readonly string StringNewLineNodeDocumentation = $"{WikiLink}/CyanTrigger-Events-and-Actions#StringGet-newLine";

        public static readonly string GetCyanTriggerProgramNameNodeDocumentation = $"{WikiLink}/CyanTrigger-Events-and-Actions#GetCyanTriggerProgramName";
        public static readonly string IsCyanTriggerProgramNodeDocumentation = $"{WikiLink}/CyanTrigger-Events-and-Actions#IsCyanTriggerProgram";

        public static readonly string SetVariableNodeDocumentation = $"{WikiLink}/CyanTrigger-Events-and-Actions#Set-Variable";
        public static readonly string TypeNodeDocumentation = $"{WikiLink}/CyanTrigger-Events-and-Actions#Type";
        
        //"https://docs.vrchat.com/docs/special-nodes#sendcustomevent";
        public static readonly string SendCustomEventNodeDocumentation = CustomEventNodeDocumentation;
        public static readonly string SetReturnValueNodeDocumentation = "https://docs.vrchat.com/docs/network-components#onownershiprequest";

        #endregion

        // VRC Events and Actions
        public static readonly Dictionary<string, string> DefinitionToDocumentation = new Dictionary<string, string>
        {
            {
                "VRCUdonCommonInterfacesIUdonEventReceiver.__GetProgramVariable__SystemString__SystemObject",
                "https://docs.vrchat.com/docs/special-nodes#get-program-variable"
            },
            {
                "VRCUdonCommonInterfacesIUdonEventReceiver.__SetProgramVariable__SystemString_SystemObject__SystemVoid",
                "https://docs.vrchat.com/docs/special-nodes#set-program-variable"
            },
            {
                "VRCUdonCommonInterfacesIUdonEventReceiver.__SendCustomEventDelayedFrames__SystemString_SystemInt32_VRCUdonCommonEnumsEventTiming__SystemVoid",
                "https://docs.vrchat.com/docs/special-nodes#sendcustomeventdelayedframes"
            },
            {
                "VRCUdonCommonInterfacesIUdonEventReceiver.__SendCustomEventDelayedSeconds__SystemString_SystemSingle_VRCUdonCommonEnumsEventTiming__SystemVoid",
                "https://docs.vrchat.com/docs/special-nodes#sendcustomeventdelayedseconds"
            },
            {
                "VRCUdonCommonInterfacesIUdonEventReceiver.__SendCustomNetworkEvent__VRCUdonCommonInterfacesNetworkEventTarget_SystemString__SystemVoid",
                "https://docs.vrchat.com/docs/special-nodes#sendcustomnetworkevent"
            },

            // VRChat events
            { "Event_Interact", "https://docs.vrchat.com/docs/event-nodes#interact" },
            { "Event_OnDrop", "https://docs.vrchat.com/docs/event-nodes#ondrop" },
            { "Event_OnPickup", "https://docs.vrchat.com/docs/event-nodes#onpickup" },
            { "Event_OnPickupUseDown", "https://docs.vrchat.com/docs/event-nodes#onpickupusedown" },
            { "Event_OnPickupUseUp", "https://docs.vrchat.com/docs/event-nodes#onpickupuseup" },
            { "Event_OnPlayerJoined", "https://docs.vrchat.com/docs/event-nodes#onplayerjoined" },
            { "Event_OnPlayerLeft", "https://docs.vrchat.com/docs/event-nodes#onplayerleft" },
            { "Event_OnStationEntered", "https://docs.vrchat.com/docs/event-nodes#onstationentered" },
            { "Event_OnStationExited", "https://docs.vrchat.com/docs/event-nodes#onstationexited" },
            { "Event_OnVideoEnd", "https://docs.vrchat.com/docs/event-nodes#onvideoend" },
            { "Event_OnVideoError", "https://docs.vrchat.com/docs/event-nodes#onvideoerror" },
            { "Event_OnVideoLoop", "https://docs.vrchat.com/docs/event-nodes#onvideoloop" },
            { "Event_OnVideoPause", "https://docs.vrchat.com/docs/event-nodes#onvideopause" },
            { "Event_OnVideoPlay", "https://docs.vrchat.com/docs/event-nodes#onvideoplay" },
            { "Event_OnVideoStart", "https://docs.vrchat.com/docs/event-nodes#onvideostart" },
            { "Event_OnVideoReady", "https://docs.vrchat.com/docs/event-nodes#onvideoready" },
            { "Event_OnPlayerTriggerEnter", "https://docs.vrchat.com/docs/event-nodes#onplayertriggerenter" },
            { "Event_OnPlayerTriggerStay", "https://docs.vrchat.com/docs/event-nodes#onplayertriggerstay" },
            { "Event_OnPlayerTriggerExit", "https://docs.vrchat.com/docs/event-nodes#onplayertriggerexit" },
            { "Event_OnPlayerCollisionEnter", "https://docs.vrchat.com/docs/event-nodes#onplayercollisionenter" },
            { "Event_OnPlayerCollisionStay", "https://docs.vrchat.com/docs/event-nodes#onplayercollisionstay" },
            { "Event_OnPlayerCollisionExit", "https://docs.vrchat.com/docs/event-nodes#onplayercollisionexit" },
            { "Event_OnPlayerParticleCollision", "https://docs.vrchat.com/docs/event-nodes#onplayerparticlecollision" },
            { "Event_OnPlayerRespawn", "https://docs.vrchat.com/docs/event-nodes#onplayerrespawn" },

            // Network events
            { "Event_OnPreSerialization", "https://docs.vrchat.com/docs/network-components#onpreserialization" },
            { "Event_OnDeserialization", "https://docs.vrchat.com/docs/network-components#ondeserialization" },
            { "Event_OnPostSerialization", "https://docs.vrchat.com/docs/network-components#onpostserialization" },
            { "Event_Spawn", "https://docs.vrchat.com/docs/network-components#onspawn" },
            { "Event_OnOwnershipRequest", "https://docs.vrchat.com/docs/network-components#onownershiprequest" },
            {
                "Event_OnOwnershipTransferred", "https://docs.vrchat.com/docs/network-components#onownershiptransferred"
            },

            // Midi events
            { "Event_MidiNoteOn", "https://docs.vrchat.com/docs/midi#midinoteon" },
            { "Event_MidiNoteOff", "https://docs.vrchat.com/docs/midi#midinoteoff" },
            { "Event_MidiControlChange", "https://docs.vrchat.com/docs/midi#midicontrolchange" },

            // Input events
            { "Event_InputJump", "https://docs.vrchat.com/docs/input-events#inputjump" },
            { "Event_InputUse", "https://docs.vrchat.com/docs/input-events#inputuse" },
            { "Event_InputGrab", "https://docs.vrchat.com/docs/input-events#inputgrab" },
            { "Event_InputDrop", "https://docs.vrchat.com/docs/input-events#inputdrop" },
            { "Event_InputMoveHorizontal", "https://docs.vrchat.com/docs/input-events#inputmovehorizontal" },
            { "Event_InputMoveVertical", "https://docs.vrchat.com/docs/input-events#inputmovevertical" },
            { "Event_InputLookVertical", "https://docs.vrchat.com/docs/input-events#inputlookvertical" },
            { "Event_InputLookHorizontal", "https://docs.vrchat.com/docs/input-events#inputlookhorizontal" },

            { "Event_Custom", CustomEventNodeDocumentation },
        };
    }
}