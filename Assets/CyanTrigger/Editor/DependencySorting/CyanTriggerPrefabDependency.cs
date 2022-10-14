using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Cyan.CT.Editor
{
    // Used for both scenes and prefabs
    public class CyanTriggerPrefabDependency
    {
        public class PrefabData
        {
            public readonly string Path;
            public readonly bool IsPrefab;

            public PrefabData(string path, bool isPrefab)
            {
                Path = path;
                IsPrefab = isPrefab;
            }
        }
        
        private readonly List<CyanTriggerDependency<PrefabData>> _assets = 
            new List<CyanTriggerDependency<PrefabData>>();
        private readonly Dictionary<string, CyanTriggerDependency<PrefabData>> _assetMap = 
            new Dictionary<string, CyanTriggerDependency<PrefabData>>();

        public static bool IsValidPrefabPath(string path)
        {
            return
                path.EndsWith(".prefab", true, CultureInfo.CurrentCulture)  
                && CyanTriggerResourceManager.IsAssetPathEditable(path);
        }
        
        public static bool IsValidScenePath(string path)
        {
            return
                path.EndsWith(".unity", true, CultureInfo.CurrentCulture)  
                && CyanTriggerResourceManager.IsAssetPathEditable(path);
        }

        public static List<string> GetValidPrefabPaths()
        {
            List<string> prefabPaths = new List<string>();
            foreach (string assetGuid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                if (IsValidPrefabPath(assetPath))
                {
                    prefabPaths.Add(assetPath);
                }
            }
            return prefabPaths;
        }
        
        public static List<string> GetValidScenePaths()
        {
            List<string> prefabPaths = new List<string>();
            foreach (string assetGuid in AssetDatabase.FindAssets("t:Scene"))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                if (IsValidScenePath(assetPath))
                {
                    prefabPaths.Add(assetPath);
                }
            }
            return prefabPaths;
        }
        
        public void AddAsset(string assetPath, bool isPrefab)
        {
            if ((isPrefab && !IsValidPrefabPath(assetPath)) || (!isPrefab && !IsValidScenePath(assetPath)))
            {
                return;
            }

            AddAssetInternal(assetPath, isPrefab);
        }
        
        private CyanTriggerDependency<PrefabData> AddAssetInternal(string assetPath, bool isPrefab)
        {
            if (_assetMap.TryGetValue(assetPath, out var dep))
            {
                return dep;
            }
            
            CyanTriggerDependency<PrefabData> asset = new CyanTriggerDependency<PrefabData>(new PrefabData(assetPath, isPrefab));
            _assets.Add(asset);
            _assetMap.Add(assetPath, asset);

            void AddDep(string path)
            {
                if (path == assetPath || !IsValidPrefabPath(path))
                {
                    return;
                }
                
                asset.AddDependency(AddAssetInternal(path, true));
            }
            
            if (isPrefab)
            {
                // Load the prefab and go through every GameObject, finding nested prefab instances
                if(!(AssetDatabase.LoadMainAssetAtPath(assetPath) is GameObject prefab))
                {
                    return null;
                }
                
                List<Transform> searchObjects = new List<Transform>();
                prefab.GetComponentsInChildren(true, searchObjects);
            
                foreach (var obj in searchObjects)
                {
                    if (PrefabUtility.IsAnyPrefabInstanceRoot(obj.gameObject))
                    {
                        var prefabVariant = obj.gameObject;
                        while (prefabVariant != null)
                        {
                            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(obj);
                            AddDep(prefabPath);

                            prefabVariant = PrefabUtility.GetCorrespondingObjectFromSource(prefabVariant);
                        }
                    }
                }
            }
            else
            {
                // Get all dependent prefabs
                var dependencies = AssetDatabase.GetDependencies(assetPath, false)
                    .Where(IsValidPrefabPath);
            
                foreach (string dependency in dependencies)
                {
                    AddDep(dependency);
                }
            }
            
            return asset;
        }

        public List<PrefabData> GetOrder()
        {
            return CyanTriggerDependency<PrefabData>.GetDependencyOrdering(_assets);
        }
    }
}