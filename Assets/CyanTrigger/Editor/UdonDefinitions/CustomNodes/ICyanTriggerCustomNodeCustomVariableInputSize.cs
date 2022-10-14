using UnityEditor;

namespace Cyan.CT.Editor
{
    public interface ICyanTriggerCustomNodeCustomVariableInputSize
    {
        CyanTriggerActionVariableDefinition[] GetExtraVariables(SerializedProperty actionProperty, bool includeEventVariables);
        CyanTriggerActionVariableDefinition[] GetExtraVariables(CyanTriggerActionInstance actionInstance, bool includeEventVariables);
    }
}