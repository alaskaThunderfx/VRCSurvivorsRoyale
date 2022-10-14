using UnityEngine;

namespace Cyan.CT
{
    // Mainly used as a dummy class to help with editor inspectors to properly save data
    public class CyanTriggerScriptableObject : ScriptableObject
    {
        public CyanTriggerSerializableInstance triggerInstance = 
            CyanTriggerSerializableInstance.CreateEmptyInstance();
    }
}