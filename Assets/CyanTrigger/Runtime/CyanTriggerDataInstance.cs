using System;

namespace Cyan.CT
{
    // This is all the data that should be affect compilation of a CyanTrigger program. 
    // Adding any compilation required data should also be added to these classes:
    // - CyanTriggerCopyUtil.CopyCyanTriggerDataInstance
    // - CyanTriggerInstanceDataHash.GetProgramUniqueStringForCyanTrigger
    [Serializable]
    public class CyanTriggerDataInstance
    {
        public const int DataVersion = 6;

        public int version;
        // public bool addDebugLogsPerAction
        public int updateOrder;
        public bool autoSetSyncMode = true;
        public CyanTriggerProgramSyncMode programSyncMode;
        public string programName;
        
        public CyanTriggerEvent[] events = Array.Empty<CyanTriggerEvent>();
        public CyanTriggerVariable[] variables = Array.Empty<CyanTriggerVariable>();
        
        // Data that does not affect compilation and is visual only
        public CyanTriggerComment comment;
        public bool expandVariables = false;
        public bool expandOtherSettings = true;
        public bool expandSyncSection = false;
        public bool ignoreEventWarnings = false;

        public static CyanTriggerDataInstance CreateInitialized()
        {
            return new CyanTriggerDataInstance
            {
                version = DataVersion,
                events = Array.Empty<CyanTriggerEvent>(),
                variables = Array.Empty<CyanTriggerVariable>(),
                programSyncMode = CyanTriggerProgramSyncMode.ManualWithAutoRequest,
                autoSetSyncMode = true,
            };
        }
    }

    public enum CyanTriggerVariableType
    {
        Variable = 0,
        Unknown,
        SectionStart,
        SectionEnd,
    }

    [Serializable]
    public class CyanTriggerVariable : CyanTriggerActionVariableInstance
    {
        public CyanTriggerSerializableType type;
        public CyanTriggerVariableSyncMode sync;
        public bool showInInspector = true;
        public CyanTriggerVariableType typeInfo = CyanTriggerVariableType.Variable;
        public CyanTriggerComment comment;
    }
    
    [Serializable]
    public class CyanTriggerActionVariableInstance
    {
        public bool isVariable;
        public string name;
        public string variableID;
        public CyanTriggerSerializableObject data = new CyanTriggerSerializableObject();

        public CyanTriggerActionVariableInstance() { }

        public CyanTriggerActionVariableInstance(object obj)
        {
            data = new CyanTriggerSerializableObject(obj);
            isVariable = false;
        }

        public CyanTriggerActionVariableInstance(string varName, string varGuid)
        {
            isVariable = true;
            name = varName;
            variableID = varGuid;
        }
    }
    
    [Serializable]
    public class CyanTriggerActionInstance
    {
        // Active false means do not generate assembly. Acts like commenting out the code
        // public bool active; // TODO
        
        public CyanTriggerActionType actionType;
        public CyanTriggerActionVariableInstance[] inputs = Array.Empty<CyanTriggerActionVariableInstance>();
        // For first input only if it allows multiple
        public CyanTriggerActionVariableInstance[] multiInput = Array.Empty<CyanTriggerActionVariableInstance>();
        
        // Data that does not affect compilation and is visual only
        public bool expanded;
        public CyanTriggerComment comment;
    }

    [Serializable]
    public class CyanTriggerActionType
    {
        public string directEvent;
        public string guid;
    }
    
    [Serializable]
    public class CyanTriggerEventOptions
    {
        public CyanTriggerUserGate userGate;
        public CyanTriggerActionVariableInstance[] userGateExtraData = Array.Empty<CyanTriggerActionVariableInstance>();
        public CyanTriggerBroadcast broadcast;   
        public float delay;
        public CyanTriggerReplay replay; // Buffering replacement

        // TODO figure out how to add custom input variables for custom triggers?
        // Local variables
        // CyanTriggerVariable[] tempVariables
    }
    
    [Serializable]
    public class CyanTriggerEvent
    {
        // TODO remove name field and use custom trigger's input directly
        public string name;
        public string eventId; // Allow referencing an event that doesn't depend on event type or order in the list.
        public CyanTriggerActionInstance eventInstance;
        public CyanTriggerActionInstance[] actionInstances = Array.Empty<CyanTriggerActionInstance>();

        public CyanTriggerEventOptions eventOptions;
        
        // Data that does not affect compilation and is visual only
        public bool expanded = true;
    }

    // This data isn't used in compilation, but is helpful in creating programs
    [Serializable]
    public class CyanTriggerComment
    {
        public string comment;
    }
}
