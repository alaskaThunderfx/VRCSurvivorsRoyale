namespace Cyan.CT
{
    public interface ICyanTriggerProgramAsset
    {
        CyanTriggerDataInstance GetCyanTriggerData();
        CyanTriggerDataInstance GetCopyOfCyanTriggerData();
        (CyanTriggerSerializableObject[], string[]) GetDefaultVariableData();
    }
}