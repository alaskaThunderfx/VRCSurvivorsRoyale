using UnityEditor;

namespace Cyan.CT.Editor
{
    [InitializeOnLoad]
    public class CyanTriggerInitializer
    {
        static CyanTriggerInitializer()
        {
            // Ensure data path is ready for all items to use it.
            var manager = CyanTriggerResourceManager.Instance;
            
            // Ensure that CyanTriggerSerialized folder has been moved properly before needing to use it. 
            CyanTriggerSerializedProgramManager.VerifySerializedUdonDirectory();
            
            // Force import samples
            manager.ImportSamples();
            
            // Verify project prefabs are on correct version
            CyanTriggerPrefabMigrator.MigrateAllPrefabs();
        }
    }
}