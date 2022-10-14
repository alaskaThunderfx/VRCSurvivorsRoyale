using UnityEditor;

namespace Cyan.CT.Editor
{
    public class CyanTriggerResourceManagerPostProcessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets, 
            string[] deletedAssets, 
            string[] movedAssets, 
            string[] movedFromAssetPaths)
        {
            if (!CyanTriggerResourceManager.HasInstance())
            {
                return;
            }
            
            CyanTriggerResourceManager.Instance
                .VerifyDataPath(importedAssets, deletedAssets, movedAssets, movedFromAssetPaths);
        }
    }
}