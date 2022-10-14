using UnityEditor;

namespace Cyan.CT.Editor
{
    public interface ICyanTriggerCustomNodeCustomVariableInitialization
    {
        void InitializeVariableProperties(SerializedProperty inputProperties, SerializedProperty multiInputProperties);
    }
}