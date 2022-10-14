using VRC.Udon;

namespace Cyan.CT
{
    public interface ICyanTrigger
    {
        void SetVariablePublicValues(object[] variables);
        void SetVariablePublicValuesInPlaymode(object[] variables);
        UdonBehaviour GetUdonBehaviour();
        CyanTriggerDataInstance GetCyanTriggerData();
        CyanTriggerDataInstance GetCopyOfCyanTriggerData();
    }
}