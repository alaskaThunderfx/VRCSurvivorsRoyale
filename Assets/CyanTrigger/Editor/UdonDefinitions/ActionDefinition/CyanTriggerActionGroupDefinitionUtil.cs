using System;
using System.Collections.Generic;
using UnityEditor;
using Debug = UnityEngine.Debug;

#if CYAN_TRIGGER_DEBUG
using System.Diagnostics;
using UnityEngine.Profiling;
#endif

namespace Cyan.CT.Editor
{
    public class CyanTriggerActionGroupDefinitionUtil
    {
        private Dictionary<string, CyanTriggerActionDefinition> _actionGuidsToActions;
        private Dictionary<string, CyanTriggerActionGroupDefinition> _actionToActionGroups;
        private Dictionary<CyanTriggerActionGroupDefinition, string> _groupToPath;
        private Dictionary<string, CyanTriggerActionGroupDefinition> _pathToGroup;
        
        private Dictionary<string, CyanTriggerActionGroupDefinition> _customInstancedActionGroups;

        // For a given GroupDefinition path, store the guids of actions
        // Used to know how to update remaining lists.
        private readonly Dictionary<string, List<string>> _actionGroupPathToGuids =
            new Dictionary<string, List<string>>();
        
        private List<CyanTriggerActionInfoHolder> _customEventInfoHolders;
        private List<CyanTriggerActionInfoHolder> _customActionInfoHolders;
        
        
        // Not sure why this is stored in this code, but oh well. TODO refactor later
        private Dictionary<string, List<CyanTriggerActionInfoHolder>> _staticEventMethodNameToVariants;
        private Dictionary<string, List<CyanTriggerActionInfoHolder>> _staticActionMethodNameToVariants;
        private Dictionary<string, List<CyanTriggerActionInfoHolder>> _customEventMethodNameToVariants;
        private Dictionary<string, List<CyanTriggerActionInfoHolder>> _customActionMethodNameToVariants;

        private static bool _initializedStaticMethodNames = false;
        private bool _instancedActionsChanged = false;

        private static readonly object Lock = new object();

        private static CyanTriggerActionGroupDefinitionUtil _instance;
        public static CyanTriggerActionGroupDefinitionUtil Instance
        {
            get
            {
                lock (Lock)
                {
                    if (_instance == null)
                    {
                        _instance = new CyanTriggerActionGroupDefinitionUtil();
                    }
                    return _instance;
                }
            }
        }

        public static bool HasInstance()
        {
            return _instance != null;
        }
        
        private CyanTriggerActionGroupDefinitionUtil()
        {
#if CYAN_TRIGGER_DEBUG
            Profiler.BeginSample("CyanTriggerActionGroupDefinitionUtil.CollectAllCyanTriggerActionDefinitions");
            Stopwatch sw = new Stopwatch();
            sw.Start();
#endif
            
            CollectAllCyanTriggerActionDefinitions();
  
#if CYAN_TRIGGER_DEBUG          
            sw.Stop();
            Debug.Log($"CollectAll Time: {sw.Elapsed.TotalSeconds}");
            Profiler.EndSample();
#endif
        }
        
        public void ProcessAssets(
            string[] importedAssets, 
            string[] deletedAssets, 
            string[] movedAssets, 
            string[] movedFromAssetPaths)
        {
            bool changes = false;

            for (int index = 0; index < movedAssets.Length; ++index)
            {
                string oldPath = movedFromAssetPaths[index];
                string newPath = movedAssets[index];

                UpdatePath(oldPath, newPath);
            }
            
            foreach (string path in deletedAssets)
            {
                changes |= RemoveActionGroup(path, true);
            }
            
            // Added assets should always be last
            changes |= AddActionGroupBatch(importedAssets);

            if (changes)
            {
                UpdateItemsAfterChange(true);
            }
        }
        

        public bool TryGetActionDefinition(string guid, out CyanTriggerActionDefinition actionDefinition)
        {
            return _actionGuidsToActions.TryGetValue(guid, out actionDefinition);
        }

        public bool TryGetActionGroupDefinition(
            string guid,
            out CyanTriggerActionGroupDefinition groupDefinition)
        {
            return _actionToActionGroups.TryGetValue(guid, out groupDefinition);
        }
        
        public bool TryGetActionGroupDefinition(
            CyanTriggerActionDefinition actionDefinition,
            out CyanTriggerActionGroupDefinition groupDefinition)
        {
            return _actionToActionGroups.TryGetValue(actionDefinition.guid, out groupDefinition);
        }

        public IEnumerable<CyanTriggerActionGroupDefinition> GetInstancedCustomActions()
        {
            return _customInstancedActionGroups.Values;
        }

        public IEnumerable<CyanTriggerActionInfoHolder> GetEventInfoHolders()
        {
            foreach (var eventInfoHolder in _customEventInfoHolders)
            {
                if (eventInfoHolder.IsHidden())
                {
                    continue;
                }
                
                yield return eventInfoHolder;
            }
        }
        
        public IEnumerable<CyanTriggerActionInfoHolder> GetActionInfoHolders()
        {
            foreach (var actionInfoHolder in _customActionInfoHolders)
            {
                if (actionInfoHolder.IsHidden())
                {
                    continue;
                }
                
                yield return actionInfoHolder;
            }
        }

        public int GetEventVariantCount(CyanTriggerActionInfoHolder eventInfoHolder)
        {
            InitializeStaticMethodVariants();
            
            int count = 0;
            if (_staticEventMethodNameToVariants.TryGetValue(eventInfoHolder.GetDisplayName(), out var eventList))
            {
                count += eventList.Count;
            }
            if (_customEventMethodNameToVariants.TryGetValue(eventInfoHolder.GetDisplayName(), out eventList))
            {
                count += eventList.Count;
            }
            
            return count;
        }
        
        public IEnumerable<CyanTriggerActionInfoHolder> GetEventVariantInfoHolders(
            CyanTriggerActionInfoHolder eventInfoHolder)
        {
            return GetEventVariantInfoHolders(eventInfoHolder.GetDisplayName());
        }

        public IEnumerable<CyanTriggerActionInfoHolder> GetEventVariantInfoHolders(string eventDisplayName)
        {
            InitializeStaticMethodVariants();
            
            if (_staticEventMethodNameToVariants.TryGetValue(eventDisplayName, out var eventList))
            {
                foreach (var eventInfo in eventList)
                {
                    yield return eventInfo;
                }
            }
            if (_customEventMethodNameToVariants.TryGetValue(eventDisplayName, out eventList))
            {
                foreach (var eventInfo in eventList)
                {
                    yield return eventInfo;
                }
            }
        }

        public int GetActionVariantCount(CyanTriggerActionInfoHolder actionInfoHolder)
        {
            InitializeStaticMethodVariants();
            
            int count = 0;
            if (_staticActionMethodNameToVariants.TryGetValue(actionInfoHolder.GetActionRenderingDisplayName(), out var actionList))
            {
                count += actionList.Count;
            }
            if (_customActionMethodNameToVariants.TryGetValue(actionInfoHolder.GetActionRenderingDisplayName(), out actionList))
            {
                count += actionList.Count;
            }
            return count;
        }
        
        public IEnumerable<CyanTriggerActionInfoHolder> GetActionVariantInfoHolders(
            CyanTriggerActionInfoHolder actionInfoHolder)
        {
            return GetActionVariantInfoHolders(actionInfoHolder.GetActionRenderingDisplayName());
        }
        
        public IEnumerable<CyanTriggerActionInfoHolder> GetActionVariantInfoHolders(string actionDisplayName)
        {
            InitializeStaticMethodVariants();
            
            if (_staticActionMethodNameToVariants.TryGetValue(actionDisplayName, out var actionList))
            {
                foreach (var actionInfo in actionList)
                {
                    yield return actionInfo;
                }
            }
            if (_customActionMethodNameToVariants.TryGetValue(actionDisplayName, out actionList))
            {
                foreach (var actionInfo in actionList)
                {
                    yield return actionInfo;
                }
            }
        }

        private void RemoveAction(string guid)
        {
            if (!_actionGuidsToActions.TryGetValue(guid, out var action))
            {
                return;
            }

            // If cache doesn't exist, don't try to create a new one. 
            if (!CyanTriggerActionInfoHolder.TryGetCachedInfoFromCustomActionGuid(guid, out var actionInfoHolder))
            {
                return;
            }
            
            // Assume it is always valid.
            if (action.IsEvent())
            {
                _customEventInfoHolders.Remove(actionInfoHolder);
            }
            else
            {
                _customActionInfoHolders.Remove(actionInfoHolder);
            }

            _actionGuidsToActions.Remove(guid);
            _actionToActionGroups.Remove(guid);
                
            CyanTriggerActionInfoHolder.RemoveCustomActionInfo(guid);
        }
        
        private bool RemoveActionGroup(string actionGroupPath, bool batch = false)
        {
            // Action group will always be null here
            if (_pathToGroup.ContainsKey(actionGroupPath))
            {
                _pathToGroup.Remove(actionGroupPath);
            }
            
            if (_customInstancedActionGroups.ContainsKey(actionGroupPath))
            {
                _customInstancedActionGroups.Remove(actionGroupPath);
                _instancedActionsChanged = true;
            }
            
            bool found = _actionGroupPathToGuids.TryGetValue(actionGroupPath, out var actionGuids);
            if (found)
            {
                foreach (var oldGuid in actionGuids)
                {
                    RemoveAction(oldGuid);
                }
            
                _actionGroupPathToGuids.Remove(actionGroupPath);
            
                if (!batch)
                {
                    UpdateItemsAfterChange(false);
                }
            }
            
            return found;
        }

        private void UpdatePath(string oldPath, string newPath)
        {
            if (!_pathToGroup.TryGetValue(oldPath, out var actionGroup))
            {
                return;
            }
            
            _pathToGroup.Remove(oldPath);
            _pathToGroup.Add(newPath, actionGroup);

            if (_groupToPath.ContainsKey(actionGroup))
            {
                _groupToPath[actionGroup] = newPath;
            }
                
            if (_actionGroupPathToGuids.TryGetValue(oldPath, out var guidList))
            {
                _actionGroupPathToGuids.Remove(oldPath);
                _actionGroupPathToGuids.Add(newPath, guidList);
            }

            if (_customInstancedActionGroups.TryGetValue(oldPath, out var instanceActionGroup))
            {
                _customInstancedActionGroups.Remove(oldPath);
                _customInstancedActionGroups.Add(newPath, instanceActionGroup);
            }
        }

        private bool AddActionGroupBatch(string[] paths)
        {
            List<(string, CyanTriggerActionGroupDefinition)> actionGroups =
                new List<(string, CyanTriggerActionGroupDefinition)>(paths.Length);

            bool changes = false;
            foreach (string path in paths)
            {
                var definition = AssetDatabase.LoadAssetAtPath<CyanTriggerActionGroupDefinition>(path);
                if (definition == null || definition.exposedActions == null)
                {
                    continue;
                }

                if (definition is CyanTriggerActionGroupDefinitionUdonAsset udonDefinition)
                {
                    if (udonDefinition.VerifyThisGuid())
                    {
                        EditorUtility.SetDirty(udonDefinition);
                        changes = true;
                    }
                }

                if (definition.isMultiInstance)
                {
                    _customInstancedActionGroups[path] = definition;
                    _instancedActionsChanged = true;
                }
                actionGroups.Add((path, definition));
            }

            if (changes)
            {
                AssetDatabase.SaveAssets();
            }
            
            foreach (var actionGroup in actionGroups) 
            {
                AddActionGroup(actionGroup.Item1, actionGroup.Item2, true);
            }

            return actionGroups.Count > 0;
        }
        
        private void AddActionGroup(string path, CyanTriggerActionGroupDefinition actionGroup, bool batch)
        {
            if (_groupToPath.TryGetValue(actionGroup, out string oldPath))
            {
                UpdatePath(oldPath, path);
            }
            else
            {
                _groupToPath.Add(actionGroup, path);
                _pathToGroup.Add(path, actionGroup);
            }
            
            UpdateActionGroup(actionGroup, path, batch);
        }

        public void UpdateActionGroup(CyanTriggerActionGroupDefinition actionGroup)
        {
            if (_groupToPath.TryGetValue(actionGroup, out string path))
            {
                UpdateActionGroup(actionGroup, path, false);
            }
            else
            {
#if CYAN_TRIGGER_DEBUG
                Debug.LogWarning($"ActionGroup was not registered: {actionGroup.name}");
#endif
                path = AssetDatabase.GetAssetPath(actionGroup);
                if (!string.IsNullOrEmpty(path))
                {
                    AddActionGroup(path, actionGroup, false);
                }
            }
        }
        
        private void UpdateActionGroup(CyanTriggerActionGroupDefinition actionGroup, string actionPath, bool batch)
        {
            if (actionGroup is CyanTriggerActionGroupDefinitionUdonAsset udonDefinition)
            {
                if (udonDefinition.VerifyThisGuid())
                {
                    EditorUtility.SetDirty(udonDefinition);
                }
            }
            
            if (_actionGroupPathToGuids.TryGetValue(actionPath, out var actionGuids))
            {
                foreach (var oldGuid in actionGuids)
                {
                    // Always remove old versions to properly refresh references. 
                    RemoveAction(oldGuid);
                }
            }
            else
            {
                actionGuids = new List<string>();
                _actionGroupPathToGuids.Add(actionPath, actionGuids);
            }
            
            actionGuids.Clear();

            var exposedActions = actionGroup.exposedActions;
            if (exposedActions != null)
            {
                foreach (var action in exposedActions)
                {
                    string guid = action.guid;
                    actionGuids.Add(guid);
                    
                    // Always need to update because actions references change when editing through the inspector. 
                    _actionGuidsToActions[guid] = action;
                    _actionToActionGroups[guid] = actionGroup;
                    
                    var actionInfoHolder = CyanTriggerActionInfoHolder.GetActionInfoHolder(action, actionGroup);
                    if (action.IsEvent())
                    {
                        _customEventInfoHolders.Add(actionInfoHolder);
                    }
                    else
                    {
                        _customActionInfoHolders.Add(actionInfoHolder);
                    }
                }
            }

            if (actionGroup.isMultiInstance && !_customInstancedActionGroups.ContainsKey(actionPath))
            {
                _customInstancedActionGroups.Add(actionPath, actionGroup);
                _instancedActionsChanged = true;
            }
            if (!actionGroup.isMultiInstance && _customInstancedActionGroups.ContainsKey(actionPath))
            {
                _customInstancedActionGroups.Remove(actionPath);
                _instancedActionsChanged = true;
            }

            if (!batch)
            {
                UpdateItemsAfterChange(true);
            }
        }

        private void CollectAllCyanTriggerActionDefinitions()
        {
            _actionGuidsToActions = new Dictionary<string, CyanTriggerActionDefinition>();
            _actionToActionGroups = new Dictionary<string, CyanTriggerActionGroupDefinition>();
            _groupToPath = new Dictionary<CyanTriggerActionGroupDefinition, string>();
            _pathToGroup = new Dictionary<string, CyanTriggerActionGroupDefinition>();
            _customEventInfoHolders = new List<CyanTriggerActionInfoHolder>();
            _customActionInfoHolders = new List<CyanTriggerActionInfoHolder>();

            _customInstancedActionGroups = new Dictionary<string, CyanTriggerActionGroupDefinition>();
            _instancedActionsChanged = true;

            string[] guids = AssetDatabase.FindAssets($"t:{nameof(CyanTriggerActionGroupDefinition)}");

            // Go through and convert all guids to paths. 
            for (var index = 0; index < guids.Length; index++)
            {
                string guid = guids[index];
                guids[index] = AssetDatabase.GUIDToAssetPath(guid);
            }

            AddActionGroupBatch(guids);

            UpdateItemsAfterChange(true);
        }

        private void UpdateItemsAfterChange(bool shouldSort)
        {
            if (shouldSort)
            {
                _customEventInfoHolders.Sort(EventInfoComparer);
                _customActionInfoHolders.Sort(ActionInfoComparer);
            }
            
            UpdateMethodVariants();
            
            CyanTriggerActionSearchWindow.ResetCache();
            CyanTriggerEventSearchWindow.ResetCache();

            if (_instancedActionsChanged)
            {
                _instancedActionsChanged = false;
                CyanTriggerCustomActionGroupSearchWindow.ResetCache();
                CyanTriggerVariableSearchWindow.ResetCache();
            }
        }

        private static void AddToNameToVariantList(
            Dictionary<string, List<CyanTriggerActionInfoHolder>> nameToVariants,
            string actionName, 
            CyanTriggerActionInfoHolder infoHolder)
        {
            if (!nameToVariants.TryGetValue(actionName, out var eventList))
            {
                eventList = new List<CyanTriggerActionInfoHolder>();
                nameToVariants.Add(actionName, eventList);
            }
                    
            eventList.Add(infoHolder);
        }

        private void InitializeStaticMethodVariants()
        {
            if (_initializedStaticMethodNames)
            {
                return;
            }
            _initializedStaticMethodNames = true;
            
            _staticEventMethodNameToVariants = new Dictionary<string, List<CyanTriggerActionInfoHolder>>();
            _staticActionMethodNameToVariants = new Dictionary<string, List<CyanTriggerActionInfoHolder>>();
            
            foreach (var udonDef in CyanTriggerNodeDefinitionManager.Instance.GetDefinitions())
            {
                var infoHolder = CyanTriggerActionInfoHolder.GetActionInfoHolder(udonDef);
                if (udonDef.DefinitionType == CyanTriggerNodeDefinition.UdonDefinitionType.Event)
                {
                    AddToNameToVariantList(_staticEventMethodNameToVariants, udonDef.MethodName, infoHolder);
                }
                else
                {
                    AddToNameToVariantList(_staticActionMethodNameToVariants, udonDef.GetActionDisplayName(), infoHolder);
                }
            }
        }

        private void UpdateMethodVariants()
        {
            _customEventMethodNameToVariants = new Dictionary<string, List<CyanTriggerActionInfoHolder>>();
            _customActionMethodNameToVariants = new Dictionary<string, List<CyanTriggerActionInfoHolder>>();

            foreach (var eventInfo in _customEventInfoHolders)
            {
                if (eventInfo.IsHidden())
                {
                    continue;
                }
                
                AddToNameToVariantList(
                    _customEventMethodNameToVariants, 
                    eventInfo.GetDisplayName(), 
                    eventInfo);
            }
            foreach (var actionInfo in _customActionInfoHolders)
            {
                if (actionInfo.IsHidden())
                {
                    continue;
                }

                AddToNameToVariantList(
                    _customActionMethodNameToVariants, 
                    actionInfo.GetActionRenderingDisplayName(), 
                    actionInfo);
            }
        }

        private static int EventInfoComparer(CyanTriggerActionInfoHolder info1, CyanTriggerActionInfoHolder info2)
        {
            return string.Compare(info1.GetDisplayName(), info2.GetDisplayName(), StringComparison.Ordinal);
        }
        
        private static int ActionInfoComparer(CyanTriggerActionInfoHolder info1, CyanTriggerActionInfoHolder info2)
        {
            return string.Compare(info1.GetActionRenderingDisplayName(), info2.GetActionRenderingDisplayName(), StringComparison.Ordinal);
        }
    }
}