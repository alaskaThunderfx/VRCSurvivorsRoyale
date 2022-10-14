namespace Cyan.CT.Editor
{
    public interface ICyanTriggerCustomNodeValidator
    {
        CyanTriggerErrorType Validate(CyanTriggerActionInstance actionInstance, CyanTriggerDataInstance triggerData, ref string message);
    }
}