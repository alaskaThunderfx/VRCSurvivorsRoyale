using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
#if UNITY_2019_4_OR_NEWER
using UnityEditor.PackageManager.UI;
#endif
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Cyan.CT.Editor
{
    public class CyanTriggerResourceManager
    {
        private const string PackagePath = "Packages/com.cyan.cyantrigger";
        private const string DefaultDataPath = "Assets/CyanTrigger";

        private const string ExamplesSampleName = "ExamplesAndPrefabs";
        private static readonly HashSet<string> AutoImportSampleNames = new HashSet<string>()
        {
            ExamplesSampleName,
            "DefaultCustomActions"
        };

        private string _cachedDataPath;
        
        private readonly PackageInfo _packageInfo = null;
        private string _version;

        private static readonly object Lock = new object();

        private static CyanTriggerResourceManager _instance;
        public static CyanTriggerResourceManager Instance
        {
            get
            {
                lock (Lock)
                {
                    if (_instance == null)
                    {
                        _instance = new CyanTriggerResourceManager();
                    }
                    return _instance;
                }
            }
        }

        public static bool HasInstance()
        {
            return _instance != null;
        }

        private bool IsPackage()
        {
            return _packageInfo != null;
        }

        public void ImportSamples()
        {
#if UNITY_2019_4_OR_NEWER
            if (!IsPackage())
            {
                return;
            }
            
            var samples = Sample.FindByPackage(_packageInfo.name, _packageInfo.version);
            foreach (var sample in samples)
            {
                if (!AutoImportSampleNames.Contains(sample.displayName))
                {
                    continue;
                }
                
                if (sample.isImported)
                {
                    continue;
                }

                // If current sample is the examples sample, skip if this project has already imported this version. 
                // If it is a new version that the last imported, overwrite it. 
                if (sample.displayName == ExamplesSampleName)
                {
                    var settings = CyanTriggerSettings.Instance;
                    string version = GetVersion();

                    if (!version.Equals(settings.lastExampleVersionImported))
                    {
                        settings.lastExampleVersionImported = version;
                        EditorUtility.SetDirty(settings);
                    }
                    else
                    {
                        continue;
                    }
                }

                sample.Import(Sample.ImportOptions.HideImportWindow | Sample.ImportOptions.OverridePreviousImports);
            }
#endif
        }

        private CyanTriggerResourceManager()
        {
#if UNITY_2019_4_OR_NEWER
            _packageInfo = PackageInfo.FindForAssetPath(PackagePath);
            FindDataPath();
#endif
        }

        private void FindDataPath()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(CyanTriggerSettingsData)}");
            List<CyanTriggerSettingsData> settingsData = new List<CyanTriggerSettingsData>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                // Ignore any settings in package directories.
                if (!path.StartsWith("Assets"))
                {
                    continue;
                }
                
                var dataPathObj = AssetDatabase.LoadAssetAtPath<CyanTriggerSettingsData>(path);
                if (dataPathObj == null)
                {
                    continue;
                }
                
                settingsData.Add(dataPathObj);
            }
            
            CyanTriggerSettingsData settings = null;
            if (settingsData.Count > 0)
            {
                settings = settingsData[0];
                
                if (settingsData.Count > 1)
                {
                    string print = "Multiple CyanTriggerSettingsData objects exist in the project! Using the first as the main data path. Please delete the extras!";
                    foreach (var pathObj in settingsData)
                    {
                        print += $"\n{AssetDatabase.GetAssetPath(pathObj)}";
                    }
                    Debug.LogError(print);
                }
            }
            
            if (settings == null || settings.directoryPath == null)
            {
                // Verify the path exists
                if (!AssetDatabase.IsValidFolder(DefaultDataPath))
                {
                    AssetDatabase.CreateFolder("Assets", "CyanTrigger");
                }

                if (settings == null)
                {
                    settings = CyanTriggerSettings.LoadSettings(DefaultDataPath);
                }
                
                settings.directoryPath = AssetDatabase.LoadAssetAtPath<Object>(DefaultDataPath);
                EditorUtility.SetDirty(settings);

                // TODO initialize anything here.
                _cachedDataPath = DefaultDataPath;
                return;
            }

            _cachedDataPath = AssetDatabase.GetAssetPath(settings.directoryPath);
        }

        public void VerifyDataPath(
            string[] importedAssets, 
            string[] deletedAssets, 
            string[] movedAssets, 
            string[] movedFromAssetPaths)
        {
            foreach (string deletedPath in deletedAssets)
            {
                if (deletedPath == _cachedDataPath)
                {
                    _cachedDataPath = null;
                    break;
                }
            }

            for (int index = 0; index < movedAssets.Length; ++index)
            {
                string movedAssetOriginalPath = movedFromAssetPaths[index];
                if (movedAssetOriginalPath == _cachedDataPath)
                {
                    _cachedDataPath = movedAssets[index];
                    Object cachedDataPathObject = AssetDatabase.LoadAssetAtPath<Object>(_cachedDataPath);
                    Debug.Assert(cachedDataPathObject != null, $"Failed to load directory object: {_cachedDataPath}");
                    break;
                }
            }
        }

        public string GetDataPath()
        {
            if (string.IsNullOrEmpty(_cachedDataPath))
            {
                FindDataPath();
            }
            
            return _cachedDataPath;
        }
        public string GetFullDataPath()
        {
            return Path.GetFullPath(GetDataPath());
        }

        public string GetAssetPath()
        {
            if (IsPackage())
            {
                return _packageInfo.assetPath;
            }
            return GetDataPath();
        }
        public string GetFullAssetPath()
        {
            if (IsPackage())
            {
                return _packageInfo.resolvedPath;
            }
            return GetFullDataPath();
        }

        public string GetVersion()
        {
            if (!string.IsNullOrEmpty(_version))
            {
                return _version;
            }
            
            if (IsPackage())
            {
                return _version = _packageInfo.version;
            }
            
            // CyanTrigger imported directly. Read package manifest file itself and find version.
            string path = Path.Combine(GetFullDataPath(), "package.json");
            if (File.Exists(path))
            {
                string fileContents = File.ReadAllText(path);
                foreach (var line in fileContents.Split('\n'))
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("\"version\""))
                    {
                        int firstQuote = trimmed.IndexOf('"', 9);
                        int lastQuote = trimmed.LastIndexOf('"');
                        ++firstQuote;
                        _version = trimmed.Substring(firstQuote, lastQuote - firstQuote);
                        return _version;
                    }
                }
            }

            _version = "Unknown";
            return _version;
        }

        public static bool IsAssetPathEditable(string path)
        {
            PackageInfo packageInfo = null;
#if UNITY_2019_4_OR_NEWER
            packageInfo = PackageInfo.FindForAssetPath(path);
#endif
            if (packageInfo == null)
            {
                return true;
            }

            switch (packageInfo.source)
            {
                case PackageSource.Embedded:
                case PackageSource.Local:
                    return true;
            }

            return false;
        }
    }
}