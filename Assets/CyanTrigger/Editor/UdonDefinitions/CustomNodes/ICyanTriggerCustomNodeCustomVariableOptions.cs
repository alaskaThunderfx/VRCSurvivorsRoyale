using UnityEditor;

namespace Cyan.CT.Editor
{
    public interface ICyanTriggerCustomNodeCustomVariableOptions
    {
        CyanTriggerEditorVariableOption[] GetCustomEditorVariableOptions(SerializedProperty variableProperties);
    }
}