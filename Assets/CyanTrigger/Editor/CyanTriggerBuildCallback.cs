using VRC.SDKBase.Editor.BuildPipeline;

namespace Cyan.CT.Editor
{
    public class CyanTriggerBuildCallback : IVRCSDKBuildRequestedCallback
    {
        public int callbackOrder => 1;
        
        public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
        {
            if (requestedBuildType == VRCSDKRequestedBuildType.Avatar)
            {
                // Why are you building an avatar in this project?
                return true;
            }

            bool valid = true;
            CyanTriggerSettingsData settings = CyanTriggerSettings.Instance;

            // Check settings to verify building should trigger recompile asset triggers.
            if (settings.compileAssetTriggersOnBuild)
            {
                CyanTriggerSerializedProgramManager.CompileAllCyanTriggerEditableAssets(true);
            }

            // Check settings to verify building should trigger recompile scene triggers.
            if (settings.compileSceneTriggersOnBuild)
            {
                valid = CyanTriggerSerializerManager.RecompileAllTriggers(true, false);
            }

            return valid;
        }
    }
}