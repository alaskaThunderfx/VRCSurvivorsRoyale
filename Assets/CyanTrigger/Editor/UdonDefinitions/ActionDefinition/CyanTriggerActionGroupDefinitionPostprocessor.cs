using UnityEditor;

namespace Cyan.CT.Editor
{
    public class CyanTriggerActionGroupDefinitionPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets, 
            string[] deletedAssets, 
            string[] movedAssets, 
            string[] movedFromAssetPaths)
        {
            if (!CyanTriggerActionGroupDefinitionUtil.HasInstance())
            {
                return;
            }
            
            CyanTriggerActionGroupDefinitionUtil.Instance
                .ProcessAssets(importedAssets, deletedAssets, movedAssets, movedFromAssetPaths);
        }
    }
}