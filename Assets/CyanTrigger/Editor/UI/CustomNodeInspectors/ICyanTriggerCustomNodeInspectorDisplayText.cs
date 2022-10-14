using UnityEditor;

namespace Cyan.CT.Editor
{
    public interface ICyanTriggerCustomNodeInspectorDisplayText
    {
        string GetCustomDisplayText(
            CyanTriggerActionInfoHolder actionInfo, 
            SerializedProperty actionProperty, 
            CyanTriggerDataInstance triggerData,
            bool withColor);
    }
}