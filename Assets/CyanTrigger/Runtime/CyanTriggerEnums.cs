
namespace Cyan.CT
{
    public enum CyanTriggerVariableSyncMode
    {
        NotSynced = 0,
        Synced = 1,
        SyncedLinear = 2,
        SyncedSmooth = 3,
    }
    
    public enum CyanTriggerUserGate
    {
        Anyone = 0,
        Owner = 1,
        Master = 2,
        UserAllowList = 3,
        UserDenyList = 4,
        InstanceOwner = 5,
    }

    public enum CyanTriggerBroadcast
    {
        Local = 0,
        Owner = 1,
        All = 2,
    }

    // Buffering replacement
    public enum CyanTriggerReplay
    {
        None = 0,
        ReplayOnce = 1,
        ReplayParity = 2,
        ReplayAll = 3,
    }
    
    public enum CyanTriggerProgramSyncMode
    { 
        Continuous = 0,
        Manual = 1,
        ManualWithAutoRequest = 2,
#if UNITY_2019_4_OR_NEWER
        // Not supported in 2018 version.
        None = 3,
#endif
    }
}
