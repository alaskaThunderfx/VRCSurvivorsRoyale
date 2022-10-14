using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerSerializableInstanceEditor
    {
        private static readonly GUIContent ProgramNameContent = new GUIContent("Program Name",
            "The name for this program. This is mainly used when trying to get a specific CyanTrigger from a GameObject or Component. See GameObject.GetCyanTriggerComponent or Component.GetCyanTriggerComponent.");

        private static readonly GUIContent UpdateOrderContent = new GUIContent("Update Order",
            "Set the value this CyanTrigger should be sorted by when calling Start, Update, LateUpdate, FixedUpdate, and PostLateUpdate. Lower values will be executed earlier.");
        private static readonly GUIContent ProgramSyncModeContent = new GUIContent("Sync Method",
            "How will synced variables within this CyanTrigger be handled?");
        private static readonly GUIContent InteractTextContent = new GUIContent("Interaction Text",
            "Text that will be shown to the user when they highlight an object to interact.");
        private static readonly GUIContent ProximityContent = new GUIContent("Proximity",
            "How close the user needs to be before the object can be interacted with. Note that this is not in unity units and the distance depends on the avatar scale.");

        private static readonly GUIContent IgnoreEventWarningsContent = new GUIContent("Ignore Event Warnings",
            "Should warnings be displayed for special events used in this CyanTrigger? If not ignored, then the warning will display under the event options and will print whenever the CyanTrigger is compiled. Ignoring warnings will remove the warning display and prevent it from being printed on compile.");
        
        
        private const string EventTypeTooltip = "Event Type - Controls when this event will activate. Events may be provided by VRChat directly or created by users through Custom Actions. For provided events, check VRChat docs or Unity MonoBehaviour docs for specifics.";
        private const string EventVariantTooltip = "Event Variant - Additional control on when this event will activate. VRC_Direct means event is provided by VRChat. All other options were created through Custom Actions.";
        private const string UserGateTooltip = "Event Gate - Controls who can activate this event. The Local Player must match the selected option. Check the CyanTrigger Wiki for descriptions of each option.";
        private const string BroadcastTooltip = "Event Broadcast - Controls who will execute the event actions. Check the CyanTrigger Wiki for descriptions of each option.";
        
        public const string UnnamedCustomName = "_Unnamed";
        
        private const string CommentControlName = "Event Editor Comment Control";

        private const int InvalidCommentId = -1;
        private const int CyanTriggerCommentId = -2;
        
        public static readonly CyanTriggerActionVariableDefinition AllowedUserGateVariableDefinition =
            new CyanTriggerActionVariableDefinition
            {
                type = new CyanTriggerSerializableType(typeof(string)),
                variableType = CyanTriggerActionVariableTypeDefinition.Constant |
                               CyanTriggerActionVariableTypeDefinition.VariableInput,
                displayName = "Allowed Users",
                description = "If the local user's name is in this list, they will be allowed to initiate this event."
            };
        public static readonly CyanTriggerActionVariableDefinition DeniedUserGateVariableDefinition =
            new CyanTriggerActionVariableDefinition
            {
                type = new CyanTriggerSerializableType(typeof(string)),
                variableType = CyanTriggerActionVariableTypeDefinition.Constant |
                               CyanTriggerActionVariableTypeDefinition.VariableInput,
                displayName = "Denied Users",
                description = "If the local user's name is in this list, they will be denied from initiating this event."
            };
        
        private static readonly HashSet<CyanTriggerSerializableInstanceEditor> OpenSerializers =
            new HashSet<CyanTriggerSerializableInstanceEditor>();

        private readonly SerializedObject _serializedObject;
        private readonly SerializedProperty _serializedProperty;
        private readonly SerializedProperty _dataInstanceProperty;
        private readonly CyanTriggerSerializableInstance _cyanTriggerSerializableInstance;
        private readonly CyanTriggerDataInstance _cyanTriggerDataInstance;
        
        private readonly SerializedProperty _ignoreEventWarningProperty;
        
        
        private readonly CyanTriggerVariableTreeView _variableTreeView;
        private readonly SerializedProperty _variableDataProperty;

        private readonly Dictionary<Type, CyanTriggerEditorVariableOptionList> _userVariableOptions =
            new Dictionary<Type, CyanTriggerEditorVariableOptionList>();
        private readonly Dictionary<CyanTriggerActionGroupDefinition, CyanTriggerEditorVariableOptionList> _customUserVariableOptions =
            new Dictionary<CyanTriggerActionGroupDefinition, CyanTriggerEditorVariableOptionList>();
        private readonly Dictionary<string, string> _userVariables = new Dictionary<string, string>();

        private CyanTriggerActionInstanceRenderData[] _eventInstanceRenderData = 
            Array.Empty<CyanTriggerActionInstanceRenderData>();
        private CyanTriggerActionInstanceRenderData[] _eventOptionRenderData = 
            Array.Empty<CyanTriggerActionInstanceRenderData>();

        private int _eventListSize;
        private readonly SerializedProperty _eventsProperty;
        private ReorderableList[] _eventActionUserGateLists = Array.Empty<ReorderableList>();
        private Dictionary<int, ReorderableList>[] _eventActionInputLists = Array.Empty<Dictionary<int, ReorderableList>>();
        private Dictionary<int, ReorderableList>[] _eventInputLists = Array.Empty<Dictionary<int, ReorderableList>>();
        
        private int _editingCommentId = InvalidCommentId;
        private bool _editCommentButtonPressed = false;
        private bool _focusedCommentEditor = false;

        private CyanTriggerActionTreeView[] _eventActionTrees = Array.Empty<CyanTriggerActionTreeView>();
        
        private bool _resetVariableInputs = false;
        private bool _shouldRefreshActionDisplay = false;

        private ICyanTriggerBaseEditor _baseEditor;

        private CyanTriggerEditorScopeTree[] _scopeTreeRoot = Array.Empty<CyanTriggerEditorScopeTree>();

        private bool _disposed = false;
        private bool _isPlaying = false;

        private int _idStartIndex = 0;
        public int IdStartIndex
        {
            get => _idStartIndex;
            set
            {
                if (value == _idStartIndex)
                {
                    return;
                }

                _idStartIndex = value;
                UpdateAllTreeIndexCounts();
            }
        }

        public CyanTriggerSerializableInstanceEditor( 
            SerializedProperty serializedProperty, 
            CyanTriggerSerializableInstance cyanTriggerSerializableInstance,
            ICyanTriggerBaseEditor baseEditor)
        {
            OpenSerializers.Add(this);
            
            _serializedProperty = serializedProperty;
            _serializedObject = serializedProperty.serializedObject;
            _cyanTriggerSerializableInstance = cyanTriggerSerializableInstance;
            _baseEditor = baseEditor;
            
            _cyanTriggerDataInstance = cyanTriggerSerializableInstance.triggerDataInstance;
            _dataInstanceProperty =
                serializedProperty.FindPropertyRelative(nameof(CyanTriggerSerializableInstance.triggerDataInstance));

            _variableDataProperty = _dataInstanceProperty.FindPropertyRelative(nameof(CyanTriggerDataInstance.variables));
            _eventsProperty = _dataInstanceProperty.FindPropertyRelative(nameof(CyanTriggerDataInstance.events));
            
            _ignoreEventWarningProperty = _dataInstanceProperty.FindPropertyRelative(nameof(CyanTriggerDataInstance.ignoreEventWarnings));

            _variableTreeView = new CyanTriggerVariableEditableTreeView(
                _variableDataProperty,
                cyanTriggerSerializableInstance.udonBehaviour,
                _baseEditor.IsSceneTrigger(),
                OnVariableAddedOrRemoved,
                (varName, varGuid) =>
                {
                    varName = CyanTriggerNameHelpers.SanitizeName(varName);
                    return GetUniqueVariableName(varName, varGuid, _cyanTriggerDataInstance.variables);
                },
                OnGlobalVariableRenamed);

            UpdateUserVariableOptions();
            
            // Force clear cache to prevent stale data
            CyanTriggerCustomNodeInspectorUtil.ClearCache();
        }
        
        public void Dispose()
        {
            _disposed = true;
            OpenSerializers.Remove(this);
            
            var target = _baseEditor.GetTarget();
            _baseEditor = null;
            
            if (target == null)
            {
                return;
            }

            for (int index = 0; index < _eventActionTrees.Length; ++index)
            {
                if (_eventActionTrees[index] != null)
                {
                    _eventActionTrees[index].Dispose();
                }
            }
        }

        public static void UpdateAllOpenSerializers()
        {
            foreach (var serializer in OpenSerializers)
            {
                serializer.DelayUpdateActionDisplayNames();
            }
        }

        public void DelayUpdateActionDisplayNames()
        {
            _shouldRefreshActionDisplay = true;
        }

        public void OnInspectorGUI()
        {
            _isPlaying = EditorApplication.isPlaying;
            
            Profiler.BeginSample("CyanTriggerEditor");

            _serializedObject.UpdateIfRequiredOrScript();

            if (Event.current.commandName == "UndoRedoPerformed")
            {
                ResetValues();
                _resetVariableInputs = true;
            }

            if (_shouldRefreshActionDisplay)
            {
                _shouldRefreshActionDisplay = false;
                UpdateActionTreeDisplayNames();
            }

            HandleCommentEvents();

            UpdateVariableScope();

            EditorGUILayout.BeginVertical(GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth - 30));
            
            EditorGUILayout.Space();

            RenderWarningsAndErrors();
            
            RenderMainComment();
            
            EditorGUI.BeginDisabledGroup(_isPlaying);
            RenderExtraOptions();

            RenderSyncSection();
            EditorGUI.EndDisabledGroup();
            
            RenderVariables();

            RenderEvents();

            EditorGUILayout.EndVertical();

            HandleTriggerRightClick(GUILayoutUtility.GetLastRect());

            // if ((GUI.changed && _serializedObject.hasModifiedProperties) ||
            //     (Event.current.type == EventType.ValidateCommand &&
            //      Event.current.commandName == "UndoRedoPerformed"))
            // {
            //     MarkDirty();
            // }

            ApplyModifiedProperties();

            CheckResetVariableInputs();
            
            Profiler.EndSample();
        }

        private void ApplyModifiedProperties(bool force = false)
        {
            if (_serializedObject.ApplyModifiedProperties() || force)
            {
                _baseEditor.OnChange();
            }
        }

        private void CheckResetVariableInputs()
        {
            if (_resetVariableInputs)
            {
                _resetVariableInputs = false;
                UpdateUserVariableOptions();
                DelayUpdateActionDisplayNames();
            }
        }

        private void HandleCommentEvents()
        {
            // Event has a comment being edited. Check if we it needs to be closed. 
            if (_editingCommentId != InvalidCommentId)
            {
                // Detect if we should close the comment editor
                Event cur = Event.current;
                bool enterPressed = cur.type == EventType.KeyDown &&
                                    cur.keyCode == KeyCode.Return &&
                                    (cur.shift || cur.alt || cur.command || cur.control);
                bool isCommentControlFocused = GUI.GetNameOfFocusedControl() == CommentControlName;
                if ((!_editCommentButtonPressed && _focusedCommentEditor && !isCommentControlFocused) || enterPressed)
                {
                    _editingCommentId = InvalidCommentId;
                    GUI.FocusControl(null);
                    if (enterPressed)
                    {
                        cur.Use();
                    }
                }
            }
            _editCommentButtonPressed = false;
        }
        
        private void HandleTriggerRightClick(Rect triggerRect)
        {
            if (_isPlaying)
            {
                return;
            }
            
            Event current = Event.current;
            if (current.type == EventType.ContextClick && triggerRect.Contains(current.mousePosition))
            {
                GenericMenu menu = new GenericMenu();

                menu.AddItem(new GUIContent("Edit CyanTrigger Comment"), false, () =>
                {
                    _editingCommentId = CyanTriggerCommentId;
                    _focusedCommentEditor = false;
                });

                void SetAllEventsExpandState(bool value)
                {
                    for (int i = 0; i < _eventsProperty.arraySize; ++i)
                    {
                        SerializedProperty action = _eventsProperty.GetArrayElementAtIndex(i);
                        SerializedProperty expanded = action.FindPropertyRelative(nameof(CyanTriggerEvent.expanded));
                        expanded.boolValue = value;
                    }
                    ApplyModifiedProperties();
                }
            
                menu.AddItem(new GUIContent("Maximize All Events"), false, () => SetAllEventsExpandState(true));
                menu.AddItem(new GUIContent("Minimize All Events"), false, () => SetAllEventsExpandState(false));
                
                menu.AddSeparator("");

                if (_baseEditor.IsSceneTrigger())
                {
                    menu.AddItem(new GUIContent("Compile Scene Triggers"), false, () =>
                    {
                        CyanTriggerSerializerManager.RecompileAllTriggers(true, true);
                    });
                }

                menu.AddItem(new GUIContent("Show CyanTrigger Settings"), false, CyanTriggerEditorUtils.ShowSettings);

                menu.ShowAsContext();
 
                current.Use(); 
            }
        }

        private void ResetValues()
        {
            UpdateUserVariableOptions();

            ResetAllActionTrees();
            
            ResizeEventArrays(_eventsProperty.arraySize);

            for (int i = 0; i < _eventListSize; ++i)
            {
                _eventActionUserGateLists[i] = null;
                
                if (_eventInputLists[i] == null)
                {
                    _eventInputLists[i] = new Dictionary<int, ReorderableList>();
                }
                else
                {
                    _eventInputLists[i].Clear();
                }

                if (_eventActionInputLists[i] == null)
                {
                    _eventActionInputLists[i] = new Dictionary<int, ReorderableList>();
                }
                else
                {
                    _eventActionInputLists[i].Clear();
                }

                var eventData = _eventInstanceRenderData[i];
                if (eventData != null)
                {
                    eventData.ClearInputLists();
                    eventData.Property = _eventsProperty.GetArrayElementAtIndex(i)
                        .FindPropertyRelative(nameof(CyanTriggerEvent.eventInstance));
                }
                
                _eventOptionRenderData[i]?.ClearInputLists();

                // Force recalculate all variable scopes
                _scopeTreeRoot[i] = null;
            }

            UpdateActionTreeViewProperties();
        }

        private void ResetAllActionTrees()
        {
            int eventLength = _eventsProperty.arraySize;
            for (int index = 0; index < eventLength && index < _eventActionTrees.Length; ++index)
            {
                if (_eventActionTrees[index] == null)
                {
                    continue;
                }
                _eventActionTrees[index].UndoReset();
            }
            UpdateActionTreeViewProperties();
        }

        private void UpdateVariableScope()
        {
            if (_scopeTreeRoot.Length != _eventsProperty.arraySize)
            {
                _scopeTreeRoot = new CyanTriggerEditorScopeTree[_eventsProperty.arraySize];
            }

            for (int eventIndex = 0; eventIndex < _eventsProperty.arraySize; ++eventIndex)
            {
                if (_scopeTreeRoot[eventIndex] != null)
                {
                    continue;
                }
                
                _scopeTreeRoot[eventIndex] = new CyanTriggerEditorScopeTree();
                var actionListProperty = _eventsProperty.GetArrayElementAtIndex(eventIndex)
                    .FindPropertyRelative(nameof(CyanTriggerEvent.actionInstances));
                _scopeTreeRoot[eventIndex].CreateStructure(actionListProperty);
            }
        }

        private void UpdateActionTreeDisplayNames()
        {
            for (int index = 0; index < _eventActionTrees.Length; ++index)
            {
                if (_eventActionTrees[index] != null)
                {
                    _eventActionTrees[index].UpdateAllItemDisplayNames();
                }
            }
            
            _baseEditor.Repaint();
        }

        private void RemoveEvents(List<int> toRemove)
        {
            int eventLength = _eventsProperty.arraySize;
            int newCount = eventLength - toRemove.Count;
            toRemove.Sort();
            
            // TODO update all other arrays here too :eyes:
            CyanTriggerActionInstanceRenderData[] tempRenderData =
                new CyanTriggerActionInstanceRenderData[newCount];
            CyanTriggerActionInstanceRenderData[] tempOptionData =
                new CyanTriggerActionInstanceRenderData[newCount];

            CyanTriggerActionTreeView[] tempActionTrees = new CyanTriggerActionTreeView[newCount];
            
            Dictionary<int, ReorderableList>[] tempEventActionInputLists = 
                new Dictionary<int, ReorderableList>[newCount];
            Dictionary<int, ReorderableList>[] tempEventInputLists = 
                new Dictionary<int, ReorderableList>[newCount];

            ReorderableList[] tempEventActionUserGateLists = new ReorderableList[newCount];
            var tempScopeTrees = new CyanTriggerEditorScopeTree[newCount];
            
            int itr = 0;
            for (int index = 0; index < eventLength; ++index)
            {
                if (itr < toRemove.Count && toRemove[itr] == index)
                {
                    _eventsProperty.DeleteArrayElementAtIndex(toRemove[itr]);
                    ++itr;
                    continue;
                }

                int nIndex = index - itr;
                tempRenderData[nIndex] = _eventInstanceRenderData[index];
                tempRenderData[nIndex].Property = _eventsProperty.GetArrayElementAtIndex(nIndex)
                    .FindPropertyRelative(nameof(CyanTriggerEvent.eventInstance));
                tempOptionData[nIndex] = _eventOptionRenderData[index];
                
                tempActionTrees[nIndex] = _eventActionTrees[index];
                
                tempEventActionInputLists[nIndex] = _eventActionInputLists[index];
                tempEventInputLists[nIndex] = _eventInputLists[index];

                tempEventActionUserGateLists[nIndex] = _eventActionUserGateLists[index];
                
                tempScopeTrees[nIndex] = _scopeTreeRoot[index];

                if (itr > 0)
                {
                    tempRenderData[nIndex]?.ClearInputLists();
                    tempOptionData[nIndex]?.ClearInputLists();
                    tempEventActionUserGateLists[nIndex] = null;
                    tempEventInputLists[nIndex]?.Clear();
                    tempEventActionInputLists[nIndex]?.Clear();
                }
            }
            
            _eventInstanceRenderData = tempRenderData;
            _eventOptionRenderData = tempOptionData;
            _eventActionTrees = tempActionTrees;
            _eventActionInputLists = tempEventActionInputLists;
            _eventInputLists = tempEventInputLists;
            _eventActionUserGateLists = tempEventActionUserGateLists;
            _scopeTreeRoot = tempScopeTrees;

            _eventListSize = newCount;
            
            UpdateActionTreeViewProperties();
        }

        private void UpdateActionTreeViewProperties()
        {
            int eventLength = _eventsProperty.arraySize;
            for (int index = 0; index < eventLength && index < _eventActionTrees.Length; ++index)
            {
                UpdateOrCreateActionTreeForEvent(index);
            }

            UpdateAllTreeIndexCounts();
        }

        private void UpdateAllTreeIndexCounts()
        {
            int eventLength = _eventsProperty.arraySize;
            int treeIndexCount = _idStartIndex;
            _variableTreeView.IdStartIndex = treeIndexCount;
            treeIndexCount += _variableTreeView.Size;
            for (int index = 0; index < eventLength && index < _eventActionTrees.Length; ++index)
            {
                if (_eventActionTrees[index] == null)
                {
                    Debug.LogWarning($"[CyanTrigger] Action tree [{index}] is null and cannot set start id!");
                    continue;
                }

                if (_eventActionTrees[index].IdStartIndex != treeIndexCount)
                {
                    _eventActionTrees[index].IdStartIndex = treeIndexCount;
                }
                treeIndexCount += _eventActionTrees[index].Size;
            }
        }

        private void ResizeEventArrays(int newSize)
        {
            _eventListSize = newSize;
            Array.Resize(ref _eventInstanceRenderData, newSize);
            Array.Resize(ref _eventOptionRenderData, newSize);
            Array.Resize(ref _eventActionTrees, newSize);
            Array.Resize(ref _eventActionInputLists, newSize);
            Array.Resize(ref _eventInputLists, newSize);
            Array.Resize(ref _eventActionUserGateLists, newSize);
            Array.Resize(ref _scopeTreeRoot, newSize);

            UpdateActionTreeViewProperties();
        }

        private void SwapEventElements(List<int> toMoveUp)
        {
            foreach (int index in toMoveUp)
            {
                int prev = index - 1;
                _eventsProperty.MoveArrayElement(index, prev);

                SwapElements(_eventInstanceRenderData, index, prev);
                SwapElements(_eventOptionRenderData, index, prev);
                SwapElements(_eventActionTrees, index, prev);
                SwapElements(_eventActionInputLists, index, prev);
                SwapElements(_eventInputLists, index, prev);
                SwapElements(_scopeTreeRoot, index, prev);

                if (_eventInstanceRenderData[index] != null)
                {
                    _eventInstanceRenderData[index].Property = _eventsProperty.GetArrayElementAtIndex(index)
                        .FindPropertyRelative(nameof(CyanTriggerEvent.eventInstance));
                }
                if (_eventInstanceRenderData[prev] != null)
                {
                    _eventInstanceRenderData[prev].Property = _eventsProperty.GetArrayElementAtIndex(prev)
                        .FindPropertyRelative(nameof(CyanTriggerEvent.eventInstance));
                }

                _eventActionUserGateLists[index] = null;
                _eventActionUserGateLists[prev] = null;
                
                _eventInstanceRenderData[index]?.ClearInputLists();
                _eventInstanceRenderData[prev]?.ClearInputLists();
                
                _eventOptionRenderData[index]?.ClearInputLists();
                _eventOptionRenderData[prev]?.ClearInputLists();
                
                _eventActionInputLists[index]?.Clear();
                _eventActionInputLists[prev]?.Clear();
                
                _eventInputLists[index]?.Clear();
                _eventInputLists[prev]?.Clear();
            }

            UpdateActionTreeViewProperties();
        }

        private void MoveEventToIndex(int srcIndex, int dstIndex)
        {
            if (srcIndex == dstIndex)
            {
                return;
            }
            int delta = (int)Mathf.Sign(dstIndex - srcIndex);
            bool isDown = delta > 0;
            List<int> moveOrder = new List<int>();
            for (int cur = srcIndex; cur != dstIndex; cur += delta)
            {
                // When moving elements down, we actually move the one below it up.
                int index = cur + (isDown ? 1 : 0);
                moveOrder.Add(index);
            }
            SwapEventElements(moveOrder);
        }

        private static void SwapElements<T>(IList<T> array, int index1, int index2)
        {
            (array[index1], array[index2]) = (array[index2], array[index1]);
        }

        private void UpdateOrCreateActionTreeForEvent(int index)
        {
            var actionListProperty = _eventsProperty.GetArrayElementAtIndex(index)
                .FindPropertyRelative(nameof(CyanTriggerEvent.actionInstances));
                
            List<CyanTriggerEditorVariableOption> GetVariableOptionsForEvent(Type type, int actionIndex)
            {
                return GetVariableOptions(type, index, actionIndex);
            }

            bool IsVariableValidForEvent(int actionIndex, string guid, string name)
            {
                return IsVariableValid(index, actionIndex, guid, name);
            }

            void OnEventActionsChanged()
            {
                OnActionsChanged(index);
            }
            
            if (_eventActionTrees[index] == null)
            {
                _eventActionTrees[index] = new CyanTriggerActionTreeView(
                    actionListProperty,
                    _cyanTriggerDataInstance,
                    _cyanTriggerSerializableInstance?.udonBehaviour,
                    _baseEditor.IsSceneTrigger(),
                    OnEventActionsChanged, 
                    GetVariableOptionsForEvent,
                    IsVariableValidForEvent);
                _eventActionTrees[index].ExpandAll();
            }
            else
            {
                var actionTree = _eventActionTrees[index];
                actionTree.GetVariableOptions = GetVariableOptionsForEvent;
                actionTree.OnActionChanged = OnEventActionsChanged;
                actionTree.IsVariableValid = IsVariableValidForEvent;
                actionTree.Elements = actionListProperty;
            }
        }

        private void OnActionsChanged(int eventIndex)
        {
            if (eventIndex >= _scopeTreeRoot.Length || _scopeTreeRoot[eventIndex] == null || _disposed)
            {
                return;
            }
            
            // Recalculate action variable inds
            var actionListProperty = _eventsProperty.GetArrayElementAtIndex(eventIndex)
                .FindPropertyRelative(nameof(CyanTriggerEvent.actionInstances));
            _scopeTreeRoot[eventIndex].CreateStructure(actionListProperty);
            
            ApplyModifiedProperties(true);
        }

        private void OnVariableAddedOrRemoved(List<string> removedIds)
        {
            if (removedIds != null && removedIds.Count > 0)
            {
                var removedHash = new HashSet<string>(removedIds);
                var removedNames = new HashSet<string>();
                for (int index = 0; index < _eventActionTrees.Length; ++index)
                {
                    if (_eventActionTrees[index] != null)
                    {
                        _eventActionTrees[index].DeleteVariables(removedHash, removedNames);
                    }
                }
            }

            ApplyModifiedProperties(true);

            _resetVariableInputs = true;
        }

        private void OnGlobalVariableRenamed(string oldName, string newName, string guid)
        {
            if (!string.IsNullOrEmpty(newName) && !string.IsNullOrEmpty(guid))
            {
                Dictionary<string, string> updatedGuids = new Dictionary<string, string>
                {
                    {guid, newName},
                };
            
                for (int index = 0; index < _eventActionTrees.Length; ++index)
                {
                    if (_eventActionTrees[index] != null)
                    {
                        _eventActionTrees[index].UpdateVariableNames(updatedGuids);
                    }
                }
            }

            ApplyModifiedProperties(true);
            
            _resetVariableInputs = true;
        }

        private SerializedProperty AddEvent(CyanTriggerActionInfoHolder actionInfo)
        {
            _eventsProperty.arraySize++;
            SerializedProperty newEvent = _eventsProperty.GetArrayElementAtIndex(_eventsProperty.arraySize - 1);
            CyanTriggerSerializedPropertyUtils.InitializeEventProperties(actionInfo, newEvent);

            ResizeEventArrays(_eventsProperty.arraySize);

            return newEvent;
        }
        
        private void DuplicateEvent(int eventIndex)
        {
            if (eventIndex < 0 || eventIndex >= _eventInstanceRenderData.Length ||
                _eventInstanceRenderData[eventIndex] == null)
            {
                return;
            }
            
            CyanTriggerActionInfoHolder actionInfo = _eventInstanceRenderData[eventIndex].ActionInfo;
            int lastElement = _eventsProperty.arraySize++;
            var srcEvent = _eventsProperty.GetArrayElementAtIndex(eventIndex);
            var newEvent = _eventsProperty.GetArrayElementAtIndex(lastElement);
            
            // Clear info before creating data for the property.
            CyanTriggerSerializedPropertyUtils.InitializeEventProperties(actionInfo, newEvent);
            
            // Resize arrays, which also creates the action tree for this new event.
            ResizeEventArrays(_eventsProperty.arraySize);

            var srcActionTree = _eventActionTrees[eventIndex];
            var dstActionTree = _eventActionTrees[lastElement];
            
            CyanTriggerSerializedPropertyUtils.CopyEventProperties(actionInfo, srcEvent, newEvent);
           
            MoveEventToIndex(_eventsProperty.arraySize - 1, eventIndex + 1);
            
            // Copy tree's expanded values after elements have been moved and trees rebuilt.
            dstActionTree.SetExpandedApplyingStartId(srcActionTree.GetExpandedWithoutStartId());
            
            ApplyModifiedProperties();
            
            ResetValues();
            
            UpdateVariableScope();
        }
        
        

        private void UpdateUserVariableOptions()
        {
            _userVariableOptions.Clear();
            _userVariables.Clear();
            _customUserVariableOptions.Clear();

            CyanTriggerEditorVariableOptionList allVariables = new CyanTriggerEditorVariableOptionList(typeof(CyanTriggerVariable));
            _userVariableOptions.Add(allVariables.Type, allVariables);

            void AddOptionToAllTypes(CyanTriggerEditorVariableOption option)
            {
                _userVariables.Add(option.ID, option.Name);
                
                // Todo, figure if this breaks anything
                if (!option.IsReadOnly)
                {
                    allVariables.VariableOptions.Add(option);
                }
                
                foreach (var type in GetUsableTypes(option.Type))
                {
                    if (!_userVariableOptions.TryGetValue(type, out CyanTriggerEditorVariableOptionList options))
                    {
                        options = new CyanTriggerEditorVariableOptionList(type);
                        _userVariableOptions.Add(type, options);
                    }

                    options.VariableOptions.Add(option);
                }
            }


            for (int var = 0; var < _variableDataProperty.arraySize; ++var)
            {
                SerializedProperty variableProperty = _variableDataProperty.GetArrayElementAtIndex(var);
                if (CyanTriggerUtil.IsVariableDisplayOnly(variableProperty))
                {
                    continue;
                }

                string name = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.name)).stringValue;
                string id = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.variableID)).stringValue;
                
                SerializedProperty typeProperty =
                    variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.type));
                SerializedProperty typeDefProperty =
                    typeProperty.FindPropertyRelative(nameof(CyanTriggerSerializableType.typeDef));
                Type varType = Type.GetType(typeDefProperty.stringValue);

                CyanTriggerEditorVariableOption option = new CyanTriggerEditorVariableOption
                    {ID = id, Name = name, Type = varType};
                
                if (typeof(ICyanTriggerCustomType).IsAssignableFrom(varType))
                {
                    SerializedProperty dataProperty =
                        variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.data));
                    var dataObj = CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataProperty);
                    if (dataObj is ICyanTriggerCustomType customType)
                    {
                        Type baseType = customType.GetBaseType();
                        if (customType is CyanTriggerCustomTypeCustomAction customActionType && customActionType.ActionGroup != null)
                        {
                            var actionGroup = customActionType.ActionGroup;
                            if (!_customUserVariableOptions.TryGetValue(actionGroup, out CyanTriggerEditorVariableOptionList options))
                            {
                                options = new CyanTriggerEditorVariableOptionList(baseType);
                                _customUserVariableOptions.Add(actionGroup, options);
                            }

                            option.Type = baseType;
                            options.VariableOptions.Add(option);
                            _userVariables.Add(option.ID, option.Name);
                        }
                    }
                    continue;
                }
                
                AddOptionToAllTypes(option);
            }

            foreach (var varConst in CyanTriggerAssemblyDataConsts.GetConstVariables())
            {
                AddOptionToAllTypes(varConst);
            }
        }

        private static List<Type> GetUsableTypes(Type type)
        {
            HashSet<Type> foundTypes = new HashSet<Type>();
            Queue<Type> typeQueue = new Queue<Type>();

            if (type == typeof(IUdonEventReceiver))
            {
                type = typeof(UdonBehaviour);
            }
            typeQueue.Enqueue(type);
            foundTypes.Add(type);

            List<Type> typesToReturn = new List<Type>();

            while (typeQueue.Count > 0)
            {
                Type t = typeQueue.Dequeue();

                typesToReturn.Add(t);

                foreach (var interfaceType in t.GetInterfaces())
                {
                    if (!foundTypes.Contains(interfaceType))
                    {
                        typeQueue.Enqueue(interfaceType);
                        foundTypes.Add(interfaceType);
                    }
                }

                Type baseType = t.BaseType;
                if (baseType != null && !foundTypes.Contains(baseType))
                {
                    typeQueue.Enqueue(baseType);
                    foundTypes.Add(baseType);
                }
            }

            return typesToReturn;
        }

        private bool IsVariableValid(int eventIndex, int actionIndex, string guid, string name)
        {
            // TODO verify expected type

            bool emptyGuid = string.IsNullOrEmpty(guid);
            
            // Go through all event variables and compare name/guids.
            
            // Only provide event variables if event is local with no delay.
            var eventData = _cyanTriggerDataInstance.events[eventIndex].eventOptions;
            if (actionIndex == -1 || (eventData.delay == 0 && eventData.broadcast == CyanTriggerBroadcast.Local))
            {
                CyanTriggerActionInfoHolder curActionInfo = _eventInstanceRenderData[eventIndex].ActionInfo;
                SerializedProperty eventInstance = _eventInstanceRenderData[eventIndex].Property;
                foreach (var def in curActionInfo.GetCustomEditorVariableOptions(eventInstance))
                {
                    if ((emptyGuid && def.Name == name) || def.ID == guid)
                    {
                        return true;
                    }
                }
            }

            // If guid is empty but not found as an event variable at this point, assume it is not valid.
            if (emptyGuid)
            {
                return false;
            }
            
            if (_userVariables.ContainsKey(guid))
            {
                return true;
            }

            // Go through action provided variables
            return _scopeTreeRoot[eventIndex].IsVariableValid(actionIndex, guid);
        }

        private List<CyanTriggerEditorVariableOption> GetVariableOptions(Type varType, int eventIndex, int actionIndex)
        {
            // TODO cache this better
            List<CyanTriggerEditorVariableOption> options = new List<CyanTriggerEditorVariableOption>();
            HashSet<string> usedGuids = new HashSet<string>();

            void AddOption(CyanTriggerEditorVariableOption option)
            {
                if (usedGuids.Contains(option.ID))
                {
                    return;
                }
                usedGuids.Add(option.ID);
                options.Add(option);
            }

            // Handle getting Custom Action instance types
            if (varType == typeof(CyanTriggerActionGroupDefinition))
            {
                var actionData = _eventActionTrees[eventIndex]?.GetOrCreateExpandDataFromActionIndex(actionIndex);
                if (actionData == null)
                {
                    return options;
                }

                var actionGroup = actionData.ActionInfo.ActionGroup;
                if (actionGroup != null && _customUserVariableOptions.TryGetValue(actionGroup, out var customOptionsList))
                {
                    foreach (var variableOption in customOptionsList.VariableOptions)
                    {
                        AddOption(variableOption);
                    }
                }

                return options;
            }
            
            
            if (varType.IsByRef)
            {
                varType = varType.GetElementType();
            }
            // IUdonEventReceiver will break the IsAssignable call.
            if (varType == typeof(IUdonEventReceiver))
            {
                varType = typeof(UdonBehaviour);
            }

            // Get event variables if not the event itself.
            if (actionIndex != -1)
            {
                // Only provide event variables if event is local with no delay.
                var eventData = _cyanTriggerDataInstance.events[eventIndex].eventOptions;
                if (eventData.delay == 0 && eventData.broadcast == CyanTriggerBroadcast.Local)
                {
                    CyanTriggerActionInfoHolder curActionInfo = _eventInstanceRenderData[eventIndex].ActionInfo;
            
                    SerializedProperty eventInstance = _eventInstanceRenderData[eventIndex].Property;
                    foreach (var def in curActionInfo.GetCustomEditorVariableOptions(eventInstance))
                    {
                        if (varType.IsAssignableFrom(def.Type) || def.Type.IsAssignableFrom(varType))
                        {
                            AddOption(def);
                        }
                    }
                }
            }

            // These are special case for OnVariableChanged to only get CyanTrigger variables.
            if (varType == typeof(CyanTriggerVariable))
            {
                if (_userVariableOptions.TryGetValue(varType, out var list))
                {
                    foreach (var variableOption in list.VariableOptions)
                    {
                        AddOption(variableOption);
                    }
                }
            }
            else
            {
                // Get user variables of this type
                foreach (var type in GetUsableTypes(varType))
                {
                    if (_userVariableOptions.TryGetValue(type, out var list))
                    {
                        foreach (var variableOption in list.VariableOptions)
                        {
                            if (varType.IsAssignableFrom(variableOption.Type) ||
                                variableOption.Type.IsAssignableFrom(varType))
                            {
                                AddOption(variableOption);
                            }
                        }
                    }
                }
            }
            

            if (_scopeTreeRoot[eventIndex] != null)
            {
                foreach (var option in _scopeTreeRoot[eventIndex].GetVariableOptions(varType, actionIndex).Reverse())
                {
                    AddOption(option);
                }
            }

            // TODO add items that can be casted or tostring'ed
            
            // Check if any have the same name
            HashSet<string> allNames = new HashSet<string>();
            HashSet<string> dupNames = new HashSet<string>();
            foreach (var option in options)
            {
                string name = option.Name;
                if (allNames.Contains(name))
                {
                    dupNames.Add(name);
                }
                else
                {
                    allNames.Add(name);
                }
            }

            Dictionary<string, int> nameCheck = new Dictionary<string, int>();
            for (int index = 0; index < options.Count; ++index)
            {
                var option = options[index];
                string name = option.Name;
                if (!dupNames.Contains(name))
                {
                    continue;
                }
                
                if (!nameCheck.TryGetValue(option.Name, out int count))
                {
                    count = 0;
                }
                ++count;
                nameCheck[option.Name] = count;

                options[index] = new CyanTriggerEditorVariableOption
                {
                    Name = $"{name} [{count}]",
                    ID = option.ID,
                    Type = option.Type,
                    IsInput = option.IsInput,
                    UdonName = option.UdonName,
                    IsReadOnly = option.IsReadOnly,
                };
            }

            return options;
        }

        public static string GetUniqueVariableName(string varName, string id, CyanTriggerVariable[] variables)
        {
            if (variables.Length == 0)
            {
                return varName;
            }
            
            string[] variableNames = new string[variables.Length];
            for (int index = 0; index < variables.Length; ++index)
            {
                if (variables[index].variableID == id || variables[index].IsDisplayOnly())
                {
                    continue;
                }

                variableNames[index] = variables[index].name;
            }

            return GetUniqueVariableName(varName, variableNames);
        }

        // TODO get a unique variable for the entire event
        // public static string GetUniqueVariableName(int eventIndex, int actionIndex)
        // {
        //     // TODO
        // }

        public static string GetUniqueVariableName(string varName, string[] names)
        {
            HashSet<string> existingNames = new HashSet<string>(names);
            
            string varMatchName = varName;
            string varNameBase = varName;
            int lastUnderscore = varName.LastIndexOf('_');
            if (int.TryParse(varName.Substring(lastUnderscore + 1), out int count))
            {
                varNameBase = varName.Substring(0, lastUnderscore);
            }
            
            while (existingNames.Contains(varMatchName))
            {
                ++count;
                varMatchName = $"{varNameBase}_{count}";
            }

            return varMatchName;
        }

        private void RenderWarningsAndErrors()
        {
            var programAsset = _baseEditor.GetProgram();
            if (programAsset == null)
            {
                return;
            }

            // TODO if hash does not match trigger, skip showing errors/warnings.
            
            var warnings = programAsset.warningMessages;

            if (warnings == null)
            {
                warnings = Array.Empty<string>();
            }

            var errors = programAsset.errorMessages;
            if (errors == null)
            {
                errors = Array.Empty<string>();
            }
            
            if (warnings.Length > 0 || errors.Length > 0)
            {
                EditorGUILayout.BeginVertical(CyanTriggerEditorGUIUtil.HelpBoxStyle);

                if (warnings.Length > 0)
                {
                    EditorGUILayout.LabelField("Warnings:");
                    StringBuilder warningText = new StringBuilder();
                    for (int cur = 0; cur < warnings.Length; ++cur)
                    {
                        warningText.AppendLine($"{cur + 1}) {warnings[cur]}");
                    }
                    EditorGUILayout.LabelField(warningText.ToString().Trim(), CyanTriggerEditorGUIUtil.WarningTextStyle);
                }
                
                if (errors.Length > 0)
                {
                    EditorGUILayout.LabelField("Errors:");
                    StringBuilder errorText = new StringBuilder();
                    for (int cur = 0; cur < errors.Length; ++cur)
                    {
                        errorText.AppendLine($"{cur + 1}) {errors[cur]}");
                    }
                    EditorGUILayout.LabelField(errorText.ToString().Trim(), CyanTriggerEditorGUIUtil.ErrorTextStyle);
                }
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }
        }
        
        private void RenderMainComment()
        {
            SerializedProperty nameProperty =
                _dataInstanceProperty.FindPropertyRelative(nameof(CyanTriggerDataInstance.programName));
            
            SerializedProperty commentProperty =
                _dataInstanceProperty.FindPropertyRelative(nameof(CyanTriggerDataInstance.comment));
            SerializedProperty commentTextProperty =
                commentProperty.FindPropertyRelative(nameof(CyanTriggerComment.comment));

            EditorGUILayout.BeginVertical(CyanTriggerEditorGUIUtil.HelpBoxStyle);

            EditorGUI.BeginDisabledGroup(_isPlaying);

            EditorGUILayout.BeginHorizontal();
            
            string originalValue = nameProperty.stringValue;
            string programName = originalValue;
            string defaultProgramName = "";
            
            // Null check needed for when a CyanTrigger is just added to an object.
            var baseProgram = _baseEditor.GetProgram();
            if (baseProgram)
            {
                defaultProgramName = baseProgram.GetDefaultCyanTriggerProgramName();
            }
            if (string.IsNullOrEmpty(programName))
            {
                programName = defaultProgramName;
            }
            
            string editedValue = EditorGUILayout.TextField(ProgramNameContent, programName);
            if (originalValue != editedValue && programName != editedValue)
            {
                if (editedValue == defaultProgramName)
                {
                    editedValue = "";
                }
                nameProperty.stringValue = editedValue;
            }

            EditorGUI.BeginDisabledGroup(_editingCommentId == CyanTriggerCommentId);
            
            if (GUILayout.Button(CyanTriggerEditorGUIUtil.EventCommentContent, GUILayout.Width(25)))
            {
                _editingCommentId = CyanTriggerCommentId;
                _focusedCommentEditor = false;
                GUI.FocusControl(CommentControlName);
                _editCommentButtonPressed = true;
            }
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUI.EndDisabledGroup();

            if (!string.IsNullOrEmpty(commentTextProperty.stringValue) || _editingCommentId == CyanTriggerCommentId)
            {
                RenderCommentSection(commentTextProperty, CyanTriggerCommentId);
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private bool RenderCommentSection(SerializedProperty commentProperty, int index)
        {
            string comment = commentProperty.stringValue;
            // Draw comment editor
            if (_editingCommentId == index && !_editCommentButtonPressed)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(5);
                
                Event cur = Event.current;
                bool wasEscape = (cur.type == EventType.KeyDown && cur.keyCode == KeyCode.Escape);

                GUI.SetNextControlName(CommentControlName);
                string newComment = EditorGUILayout.TextArea(comment, EditorStyles.textArea);
                if (newComment != comment)
                {
                    commentProperty.stringValue = newComment;
                }

                bool completeButton = GUILayout.Button(CyanTriggerEditorGUIUtil.CommentCompleteIcon, new GUIStyle(), GUILayout.Width(16));
                
                GUILayout.Space(5);
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(5);
                
                if (!_focusedCommentEditor)
                {
                    _focusedCommentEditor = true;
                    EditorGUI.FocusTextInControl(CommentControlName);
                }

                if (wasEscape || completeButton)
                {
                    if (newComment.Length > 0 && newComment.Trim().Length == 0)
                    {
                        commentProperty.stringValue = "";
                    }
                
                    _editingCommentId = InvalidCommentId;
                    GUI.FocusControl(null);
                }

                return true;
            }
            
            if (!string.IsNullOrEmpty(comment))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(5);
                
                EditorGUILayout.LabelField($"// {comment}".Colorize(CyanTriggerColorTheme.Comment, true), CyanTriggerEditorGUIUtil.CommentStyle);

                GUILayout.Space(5);
                EditorGUILayout.EndHorizontal();
                return true;
            }

            return false;
        }

        private void RenderExtraOptions()
        {
            // Check for all settings that should be shown
            bool renderInteractSettings = false;
            bool renderUpdateOrderSetting = false;
            if (_cyanTriggerDataInstance?.events != null)
            {
                foreach (var eventType in _cyanTriggerDataInstance.events)
                {
                    var actionType = eventType.eventInstance.actionType;
                    string directEvent = actionType.directEvent;
                    if (string.IsNullOrEmpty(directEvent))
                    {
                        if (!CyanTriggerActionGroupDefinitionUtil.Instance.TryGetActionDefinition(actionType.guid, out var definition))
                        {
                            continue;
                        }
                        directEvent = definition.baseEventName;
                    }
                    
                    if (directEvent.Equals("Event_Interact"))
                    {
                        renderInteractSettings = true;
                    }

                    if (directEvent.Equals("Event_Start") || 
                        directEvent.Equals("Event_Update") || 
                        directEvent.Equals("Event_LateUpdate") || 
                        directEvent.Equals("Event_FixedUpdate") ||
                        directEvent.Equals("Event_PostLateUpdate"))
                    {
                        renderUpdateOrderSetting = true;
                    }
                    
                    // TODO check for other event types
                }
            }

            if (!_cyanTriggerSerializableInstance?.udonBehaviour)
            {
                renderInteractSettings = false;
            }

            // If no settings should show, don't show this section at all.
            if (!(renderInteractSettings || renderUpdateOrderSetting || _ignoreEventWarningProperty.boolValue))
            {
                return;
            }
            
            Rect rect = EditorGUILayout.BeginVertical(CyanTriggerEditorGUIUtil.HelpBoxStyle);
            
            // Show documentation link before showing foldout label
            Rect docRect = new Rect(rect.xMax - 20, rect.y + 4, 20, EditorGUIUtility.singleLineHeight);
            CyanTriggerEditorUtils.DrawDocumentationButton(
                docRect, 
                "CyanTrigger Other Settings", 
                CyanTriggerDocumentationLinks.OtherSettingsDocumentation);
            
            
            SerializedProperty showOtherSettingsProp = 
                _dataInstanceProperty.FindPropertyRelative(nameof(CyanTriggerDataInstance.expandOtherSettings));
            bool showOtherSettings = showOtherSettingsProp.boolValue;
            
            CyanTriggerPropertyEditor.DrawFoldoutListHeader(
                new GUIContent("Other Settings"),
                ref showOtherSettings,
                false,
                0,
                null,
                false,
                null,
                false,
                false
            );
            showOtherSettingsProp.boolValue = showOtherSettings;

            if (!showOtherSettings)
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
                EditorGUILayout.Space();
                return;
            }

            EditorGUILayout.PropertyField(_ignoreEventWarningProperty, IgnoreEventWarningsContent);
            
            // Update order
            if (renderUpdateOrderSetting)
            {
                SerializedProperty updateOrderProperty =
                    _dataInstanceProperty.FindPropertyRelative(nameof(CyanTriggerDataInstance.updateOrder));
            
                EditorGUILayout.PropertyField(updateOrderProperty, UpdateOrderContent);
            }

            // Add interact options
            if (renderInteractSettings)
            {
                SerializedProperty interactTextProperty =
                    _serializedProperty.FindPropertyRelative(
                        nameof(CyanTriggerSerializableInstance.interactText));
                SerializedProperty interactProximityProperty =
                    _serializedProperty.FindPropertyRelative(
                        nameof(CyanTriggerSerializableInstance.proximity));

                RenderInteractSettings(interactTextProperty, interactProximityProperty);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
        }

        public static void RenderInteractSettings(
            SerializedProperty interactTextProperty,
            SerializedProperty interactProximityProperty)
        {
            EditorGUILayout.PropertyField(interactTextProperty, InteractTextContent);

            interactProximityProperty.floatValue = EditorGUILayout.Slider(ProximityContent,
                interactProximityProperty.floatValue, 0f, 100f);
        }

        private void RenderSyncSection()
        {
            Rect rect = EditorGUILayout.BeginVertical(CyanTriggerEditorGUIUtil.HelpBoxStyle);
            
            // Show documentation link before showing foldout label
            Rect docRect = new Rect(rect.xMax - 20, rect.y + 4, 20, EditorGUIUtility.singleLineHeight);
            CyanTriggerEditorUtils.DrawDocumentationButton(
                docRect, 
                "CyanTrigger Sync Settings", 
                CyanTriggerDocumentationLinks.SyncSettingsDocumentation);
            
            
            SerializedProperty showSyncProp = 
                _dataInstanceProperty.FindPropertyRelative(nameof(CyanTriggerDataInstance.expandSyncSection));

            bool showSync = showSyncProp.boolValue;
            CyanTriggerPropertyEditor.DrawFoldoutListHeader(
                new GUIContent("Sync Settings"),
                ref showSync,
                false,
                0,
                null,
                false,
                null,
                false,
                false
            );
            showSyncProp.boolValue = showSync;
            
            
            if (!showSync)
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
                EditorGUILayout.Space();
                return;
            }
            
            SerializedProperty autoSetSyncModeProp = 
                _dataInstanceProperty.FindPropertyRelative(nameof(CyanTriggerDataInstance.autoSetSyncMode));

            EditorGUILayout.PropertyField(autoSetSyncModeProp);
            bool autoSet = autoSetSyncModeProp.boolValue;
            
            EditorGUI.BeginDisabledGroup(autoSet);

            SerializedProperty programSyncModeProperty =
                _dataInstanceProperty.FindPropertyRelative(nameof(CyanTriggerDataInstance.programSyncMode));

            CyanTriggerProgramSyncMode syncMode = CyanTriggerProgramSyncMode.ManualWithAutoRequest;
            UdonBehaviour udonBehaviour = _cyanTriggerSerializableInstance?.udonBehaviour;
            
            if (!autoSet)
            {
                EditorGUILayout.PropertyField(programSyncModeProperty, ProgramSyncModeContent);
                syncMode = (CyanTriggerProgramSyncMode) programSyncModeProperty.intValue;
            }
            else
            {
                if (udonBehaviour != null)
                {
#if !UNITY_2019_4_OR_NEWER
                    syncMode = udonBehaviour.Reliable
                        ? CyanTriggerProgramSyncMode.ManualWithAutoRequest
                        : CyanTriggerProgramSyncMode.Continuous;
#else
                    switch (udonBehaviour.SyncMethod)
                    {
                        case Networking.SyncType.Continuous:
                            syncMode = CyanTriggerProgramSyncMode.Continuous;
                            break;
                        case Networking.SyncType.Manual:
                            syncMode = CyanTriggerProgramSyncMode.ManualWithAutoRequest;
                            break;
                        default:
                            syncMode = CyanTriggerProgramSyncMode.None;
                            break;
                    }
#endif
                }
                else
                {
#if UNITY_2019_4_OR_NEWER
                    syncMode = (_baseEditor.GetProgram()?.shouldBeNetworked ?? false)
                        ? CyanTriggerProgramSyncMode.ManualWithAutoRequest
                        : CyanTriggerProgramSyncMode.None;
#else
                    syncMode = CyanTriggerProgramSyncMode.ManualWithAutoRequest;
#endif
                }
                EditorGUILayout.EnumPopup(ProgramSyncModeContent, syncMode);
            }
            
            if (syncMode == CyanTriggerProgramSyncMode.Continuous)
            {
                EditorGUILayout.HelpBox("Synced variables will automatically be synced with users in the room periodically. This will happen multiple times per second, even if no data changes.", MessageType.Info);
            }
            else if (syncMode == CyanTriggerProgramSyncMode.Manual)
            {
                EditorGUILayout.HelpBox("Synced variables will only be synced with users in the room after UdonBehaviour.RequestSerialization is called.", MessageType.Info);
            }
            else if (syncMode == CyanTriggerProgramSyncMode.ManualWithAutoRequest)
            {
                EditorGUILayout.HelpBox("Modifying a synced variable will automatically request serialization at the end of the event. Note that with fast changing values, only the latest value will be synced with users in the room.", MessageType.Info);
            }
#if UNITY_2019_4_OR_NEWER
            else if (syncMode == CyanTriggerProgramSyncMode.None)
            {
                EditorGUILayout.HelpBox("Networked actions will be disabled for this CyanTrigger. Variables will not sync, the owner of this object cannot be set, and networked events will be ignored.", MessageType.Info);
            }
#endif

            bool shouldCheckForContinuousWarning = udonBehaviour != null;
#if UNITY_2019_4_OR_NEWER
            shouldCheckForContinuousWarning &= syncMode != CyanTriggerProgramSyncMode.None;
#endif
            if (shouldCheckForContinuousWarning)
            {
                bool requiresContinuous = CyanTriggerUtil.GameObjectRequiresContinuousSync(udonBehaviour);
                bool isManual = syncMode == CyanTriggerProgramSyncMode.Manual 
                                || syncMode == CyanTriggerProgramSyncMode.ManualWithAutoRequest;

                if (isManual && requiresContinuous)
                {
                    EditorGUILayout.HelpBox("This CyanTrigger is set to Manual sync and the object has the VRCObjectSync Component. These are incompatible and the object's position will not sync. If an object has ObjectSync, all Udon need to be on Continuous Sync. It is recommended to use multiple objects with the position sync'ed object using Continuous Sync and another object with Manual Sync to sync variables.", MessageType.Error);
                }
            }
            
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
        }
        
        private void RenderVariables()
        {
            Rect rect = EditorGUILayout.BeginVertical(CyanTriggerEditorGUIUtil.HelpBoxStyle);

            // Show documentation link before showing foldout label
            Rect docRect = new Rect(rect.xMax - 20, rect.y + 4, 20, EditorGUIUtility.singleLineHeight);
            CyanTriggerEditorUtils.DrawDocumentationButton(
                docRect, 
                "CyanTrigger Variables", 
                CyanTriggerDocumentationLinks.VariablesSettingsDocumentation);
            

            SerializedProperty showVariablesProp = 
                _dataInstanceProperty.FindPropertyRelative(nameof(CyanTriggerDataInstance.expandVariables));
            bool showVariables = showVariablesProp.boolValue;
            
            EditorGUILayout.BeginVertical();
            EditorGUI.BeginDisabledGroup(_isPlaying);
            
            // TODO allow dragging objects/components here to add them as variables
            CyanTriggerPropertyEditor.DrawFoldoutListHeader(
                new GUIContent("Variables"),
                ref showVariables,
                false,
                0,
                null,
                false,
                null,
                false,
                false
                );
            showVariablesProp.boolValue = showVariables;
            
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
            
            Rect variableHeaderRect = GUILayoutUtility.GetLastRect();
            Event current = Event.current;
            if(_variableDataProperty.prefabOverride 
               && current.type == EventType.ContextClick 
               && variableHeaderRect.Contains(current.mousePosition))
            {
                GenericMenu menu = new GenericMenu();
                
                GUIContent applyContent = new GUIContent("Apply All Variable Prefab Changes");
                GUIContent revertContent = new GUIContent("Revert All Variable Prefab Changes");

                AddPrefabApplyRevertOptions(
                    menu, 
                    _variableDataProperty, 
                    _variableTreeView.Reload,
                    _variableTreeView.Reload,
                    applyContent, 
                    revertContent);
                    
                menu.ShowAsContext();
 
                current.Use(); 
            }

            // Hacky way to show the prefab change mark
            EditorGUI.BeginProperty(variableHeaderRect, GUIContent.none, _variableDataProperty);
            EditorGUI.EndProperty();
            
            if (!showVariables)
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
                EditorGUILayout.Space();
                
                return;
            }
            
            _variableTreeView.DoLayoutTree();
            
            if (_variableDataProperty.arraySize != _variableTreeView.Size)
            {
                _resetVariableInputs = true;
            }

            CheckResetVariableInputs();

            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();
            EditorGUILayout.Space();
        }

        private void RenderEvents()
        {
            int eventLength = _eventsProperty.arraySize;

            if (_eventListSize != eventLength)
            {
                if (_eventListSize < eventLength)
                {
                    ResizeEventArrays(eventLength);
                }
                else
                {
                    Debug.LogWarning($"Event size does not match! {_eventListSize} {eventLength}");
                    ResetValues();
                }
            }

            UpdateAllTreeIndexCounts();

            List<int> toRemove = new List<int>();
            List<int> toMoveUp = new List<int>();

            for (int curEvent = 0; curEvent < eventLength; ++curEvent)
            {
                EditorGUILayout.BeginVertical(CyanTriggerEditorGUIUtil.HelpBoxStyle);

                SerializedProperty eventProperty = _eventsProperty.GetArrayElementAtIndex(curEvent);
                SerializedProperty eventInfo = eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.eventInstance));
                SerializedProperty expandedProperty = eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.expanded));
                
                if (_eventInstanceRenderData[curEvent] == null)
                {
                    _eventInstanceRenderData[curEvent] = new CyanTriggerActionInstanceRenderData
                    {
                        Property = eventInfo,
                        DataInstance = _cyanTriggerDataInstance,
                        AllowsUnityObjectConstants = _baseEditor.IsSceneTrigger(),
                    };
                    
                    // TODO, do this better?
                    _eventOptionRenderData[curEvent] = new CyanTriggerActionInstanceRenderData
                    {
                        // Currently only for user gate
                        InputLists = new ReorderableList[1],
                        ExpandedInputs = new [] {true},
                        AllowsUnityObjectConstants = _baseEditor.IsSceneTrigger(),
                    };
                }
                if (_eventInstanceRenderData[curEvent].NeedsReinitialization)
                {
                    _eventInstanceRenderData[curEvent].Initialize();
                }
                
                CyanTriggerActionInfoHolder curActionInfo = _eventInstanceRenderData[curEvent].ActionInfo;

                // Render event comment 
                {
                    SerializedProperty eventComment =
                        eventInfo.FindPropertyRelative(nameof(CyanTriggerActionInstance.comment));
                    SerializedProperty eventCommentProperty =
                        eventComment.FindPropertyRelative(nameof(CyanTriggerComment.comment));

                    if (RenderCommentSection(eventCommentProperty, curEvent))
                    {
                        GUILayout.Space(5);
                    }
                }
                
                if (_isPlaying && _cyanTriggerSerializableInstance.udonBehaviour != null && _baseEditor.IsSceneTrigger())
                {
                    var udon = _cyanTriggerSerializableInstance.udonBehaviour;
                    string eventName = CyanTriggerCompiler.GetDebugEventExecutionName(curEvent);

                    // Get the program property, since this is unlikely to change compared to "
                    var programProperty = typeof(UdonBehaviour)
                        .GetField("_program", BindingFlags.Instance | BindingFlags.NonPublic);

                    // Check if the udonBehaviour has a program with an exported event of this name to know
                    // if the execute button should be rendered.
                    IUdonProgram udonProgram = (IUdonProgram)programProperty?.GetValue(udon);
                    if (udonProgram != null && udonProgram.EntryPoints.HasExportedSymbol(eventName))
                    {
                        if (GUILayout.Button("Execute"))
                        {
                            // TODO show input options for events
                            udon.SendCustomEvent(eventName);
                        }
                        EditorGUILayout.Space();
                    }
                }
                
                TriggerModifyAction modifyAction = RenderEventHeader(curEvent, eventProperty, curActionInfo);
                
                if (expandedProperty.boolValue)
                {
                    RenderEventWarnings(curActionInfo);
                    
                    RenderEventOptions(curEvent, eventProperty, curActionInfo);
                    
                    EditorGUILayout.Space();

                    RenderEventActions(curEvent);
                }
                

                EditorGUILayout.EndVertical();

                Rect eventRect = GUILayoutUtility.GetLastRect();

                if (modifyAction == TriggerModifyAction.None)
                {
                    HandleEventRightClick(eventRect, curEvent);
                }
                else if (modifyAction == TriggerModifyAction.Delete)
                {
                    toRemove.Add(curEvent);
                }
                else if (modifyAction == TriggerModifyAction.MoveUp)
                {
                    toMoveUp.Add(curEvent);
                }
                else if (modifyAction == TriggerModifyAction.MoveDown)
                {
                    toMoveUp.Add(curEvent + 1);
                }

                EditorGUILayout.Space();
                EditorGUILayout.Space();
            }

            if (toRemove.Count > 0)
            {
                RemoveEvents(toRemove);
            }

            if (toMoveUp.Count > 0)
            {
                SwapEventElements(toMoveUp);
            }
            
            RenderAddEventButton();
        }

        private TriggerModifyAction RenderEventHeader(
            int index, 
            SerializedProperty eventProperty,
            CyanTriggerActionInfoHolder actionInfo)
        {
            SerializedProperty eventInfo = eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.eventInstance));
            SerializedProperty expandedProperty = eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.expanded));

            float headerRowHeight = 16f;
            Rect rect = EditorGUILayout.BeginHorizontal(GUILayout.Height(headerRowHeight));

            Rect foldoutRect = new Rect(rect.x + 14, rect.y, 6, rect.height);
            bool expanded = expandedProperty.boolValue;
            bool newExpand = EditorGUI.Foldout(foldoutRect, expanded, GUIContent.none, true);
            if (newExpand != expanded)
            {
                expandedProperty.boolValue = newExpand;
                expanded = newExpand;
            }
            
            float spaceBetween = 5;
            float initialSpace = foldoutRect.width + 14;
            float initialOffset = foldoutRect.xMax;

            float baseWidth = (rect.width - initialSpace - spaceBetween * 2) / 3.0f;
            float opButtonWidth = (baseWidth - 2 * spaceBetween) / 3.0f;

            Rect removeRect = new Rect(rect.xMax - opButtonWidth, rect.y, opButtonWidth, rect.height);
            Rect downRect = new Rect(removeRect.x - spaceBetween - opButtonWidth, rect.y, opButtonWidth,
                rect.height);
            Rect upRect = new Rect(downRect.x - spaceBetween - opButtonWidth, rect.y, opButtonWidth, rect.height);

            void DrawContentInCenterOfRect(Rect contentRect, GUIContent content, GUIStyle style = null)
            {
                if (style == null)
                {
                    style = GUI.skin.label;
                }
                
                Vector2 size = style.CalcSize(content);
                GUI.Label(new Rect(contentRect.center.x - size.x * 0.5f, 
                    contentRect.center.y - size.y * 0.5f, size.x, size.y), content, style);
            }
            
            EditorGUI.BeginDisabledGroup(_isPlaying);
            
            // Draw modify buttons (move up, down, delete)
            TriggerModifyAction modifyAction = TriggerModifyAction.None;
            {
                EditorGUI.BeginDisabledGroup(index == 0);
                if (GUI.Button(upRect, GUIContent.none))
                {
                    modifyAction = TriggerModifyAction.MoveUp;
                }
                DrawContentInCenterOfRect(upRect, new GUIContent("▲", "Move Event Up"));

                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginDisabledGroup(index == _eventsProperty.arraySize - 1);
                if (GUI.Button(downRect, GUIContent.none))
                {
                    modifyAction = TriggerModifyAction.MoveDown;
                }
                DrawContentInCenterOfRect(downRect, new GUIContent("▼", "Move Event Down"));

                EditorGUI.EndDisabledGroup();

                if (GUI.Button(removeRect, GUIContent.none))
                {
                    modifyAction = TriggerModifyAction.Delete;
                }
                DrawContentInCenterOfRect(removeRect, new GUIContent("✖", "Delete Event"));
            }
            
            EditorGUI.EndDisabledGroup();

            // Draw hidden event header
            if (!expanded)
            {
                GUILayout.Space(EditorGUIUtility.singleLineHeight);
                
                EditorGUI.BeginDisabledGroup(_isPlaying);

                float eventWidth = rect.width - initialOffset - opButtonWidth * 3 - spaceBetween * 2;
                Rect eventLabelRect = new Rect(initialOffset, rect.y, eventWidth, rect.height);

                string actionDisplayName = actionInfo.IsAction()
                    ? actionInfo.GetActionRenderingDisplayName()
                    : actionInfo.GetDisplayName();
                
                if (actionInfo.IsCustomEvent())
                {
                    SerializedProperty nameProperty = eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.name));
                    string customName = string.IsNullOrEmpty(nameProperty.stringValue)
                        ? UnnamedCustomName
                        : nameProperty.stringValue;
                    actionDisplayName = $"\"{customName}\"";
                }
                
                EditorGUI.LabelField(eventLabelRect, actionDisplayName);
                
                EditorGUI.EndDisabledGroup();
                
                EditorGUILayout.EndHorizontal();
                return modifyAction;
            }
            GUILayout.Space(EditorGUIUtility.singleLineHeight * 2);
            
            
            Rect typeRect = new Rect(initialOffset, rect.y, baseWidth, rect.height);
            Rect typeVariantRect = new Rect(typeRect.xMax + spaceBetween, rect.y, baseWidth, rect.height);

            Rect propertyRect = new Rect(typeRect);
            propertyRect.xMax = typeVariantRect.xMax;
            
            bool valid = actionInfo.IsValid();

            void UpdateEventActionInfo(CyanTriggerActionInfoHolder newActionInfo)
            {
                if (actionInfo.Equals(newActionInfo))
                {
                    return;
                }
                
                var oldVariables = actionInfo.GetCustomEditorVariableOptions(eventInfo);
                var newVariables = newActionInfo.GetBaseActionVariables(true);
                var nameHash = new HashSet<string>();
                var guidHash = new HashSet<string>();
                foreach (var variable in oldVariables)
                {
                    nameHash.Add(variable.Name);
                    
                    string guid = variable.ID;
                    if (!string.IsNullOrEmpty(guid))
                    {
                        guidHash.Add(guid);   
                    }
                }
                foreach (var variable in newVariables)
                {
                    nameHash.Remove(variable.displayName);
                }
                
                _eventActionTrees[index].DeleteVariables(guidHash, nameHash);
                
                CyanTriggerSerializedPropertyUtils.SetActionData(newActionInfo, eventInfo, false);
                _eventInstanceRenderData[index].ActionInfo = newActionInfo;
                ApplyModifiedProperties(true);
            }
            
            EditorGUI.BeginDisabledGroup(_isPlaying);

            
            var eventType = eventInfo.FindPropertyRelative(nameof(CyanTriggerActionInstance.actionType));
            EditorGUI.BeginProperty(propertyRect, GUIContent.none, eventType);
            
            // Override prefab right click to ensure ui refreshes properly.
            bool disableButtons = false;
            Event current = Event.current;
            bool mouseOverProperty = propertyRect.Contains(current.mousePosition);
            if (eventType.prefabOverride && mouseOverProperty)
            {
                if (current.type == EventType.ContextClick)
                {
                    GenericMenu menu = new GenericMenu();

                    void ReinitializeData()
                    {
                        _eventInstanceRenderData[index].NeedsReinitialization = true;
                    }
                    
                    AddPrefabApplyRevertOptions(menu, eventType, ReinitializeData, ReinitializeData);
                    menu.ShowAsContext();
 
                    current.Use();
                }

                if (current.button == 1 && (current.type == EventType.MouseDown || current.type == EventType.MouseUp))
                {
                    disableButtons = true;
                }
            }
            
            // Show documentation link for event type
            Rect docRect = new Rect(typeVariantRect.xMax - 16, typeVariantRect.y, 16, typeVariantRect.height);
            bool showedDocs = CyanTriggerEditorUtils.DrawDocumentationButtonForActionInfo(docRect, actionInfo, mouseOverProperty);
            // if documentation showed, decrease the width of the variant button.
            if (mouseOverProperty && showedDocs)
            {
                typeVariantRect.xMax = docRect.xMin - spaceBetween;
            }
            
            EditorGUI.BeginDisabledGroup(disableButtons);
            if (GUI.Button(typeRect, actionInfo.GetDisplayName(), EditorStyles.popup))
            {
                void UpdateEvent(CyanTriggerSettingsFavoriteItem newEventInfo)
                {
                    var data = newEventInfo.data;
                    var newActionInfo =
                        CyanTriggerActionInfoHolder.GetActionInfoHolder(data.guid, data.directEvent);
                    
                    UpdateEventActionInfo(newActionInfo);
                }

                CyanTriggerSearchWindowManager.Instance.DisplayEventsFavoritesSearchWindow(UpdateEvent, true);
            }
            EditorGUI.LabelField(typeRect, new GUIContent("", EventTypeTooltip));

            int variantCount = CyanTriggerActionGroupDefinitionUtil.Instance.GetEventVariantCount(actionInfo);
            EditorGUI.BeginDisabledGroup(!valid || variantCount <= 1);
            if (GUI.Button(typeVariantRect, actionInfo.GetVariantName(), EditorStyles.popup))
            {
                GenericMenu menu = new GenericMenu();
                
                foreach (var actionVariant in CyanTriggerActionGroupDefinitionUtil.Instance.GetEventVariantInfoHolders(actionInfo))
                {
                    menu.AddItem(new GUIContent(actionVariant.GetVariantName()), false, (t) =>
                    {
                        UpdateEventActionInfo((CyanTriggerActionInfoHolder) t);
                    }, actionVariant);
                }
                
                menu.ShowAsContext();
            }
            EditorGUI.LabelField(typeVariantRect, new GUIContent("", EventVariantTooltip));
            
            EditorGUI.EndDisabledGroup(); // disable lack of variants
            EditorGUI.EndDisabledGroup(); // Disable buttons from right click
            EditorGUI.EndProperty();


            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            
            // Render gate, networking
            SerializedProperty eventOptions = eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.eventOptions));
            SerializedProperty userGateProperty =
                eventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.userGate));
            SerializedProperty broadcastProperty =
                eventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.broadcast));
           

            Rect subHeaderRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(20f));
            GUILayout.Space(20f);
            
            // Show event header documentation link
            docRect = new Rect(subHeaderRect.x, subHeaderRect.y+1, 16, subHeaderRect.height);
            CyanTriggerEditorUtils.DrawDocumentationButton(
                docRect, 
                "CyanTrigger Events", 
                CyanTriggerDocumentationLinks.EventsDocumentation);

            subHeaderRect.x = initialOffset;

            Rect gateRect = new Rect(subHeaderRect.x, subHeaderRect.y, baseWidth, subHeaderRect.height);
            Rect broadcastRect = new Rect(gateRect.xMax + spaceBetween, subHeaderRect.y, baseWidth, subHeaderRect.height);
            
            EditorGUI.PropertyField(gateRect, userGateProperty, GUIContent.none);
            EditorGUI.LabelField(gateRect, new GUIContent("", UserGateTooltip));
            
            string[] broadcastOptions = {"Local", "Send To Owner", "Send To All"};
            int broadcastIndex = broadcastProperty.intValue;
            EditorGUI.BeginProperty(broadcastRect, GUIContent.none, broadcastProperty);
            int newBroadcastIndex = EditorGUI.Popup(broadcastRect, broadcastIndex, broadcastOptions);
            EditorGUI.EndProperty();
            EditorGUI.LabelField(broadcastRect, new GUIContent("", BroadcastTooltip));
            if (broadcastIndex != newBroadcastIndex)
            {
                broadcastProperty.intValue = newBroadcastIndex;
            }

            {
                Rect menuRect = new Rect(removeRect.x, subHeaderRect.y, opButtonWidth, headerRowHeight);
                Rect duplicateRect = new Rect(menuRect.x - spaceBetween - opButtonWidth, subHeaderRect.y, opButtonWidth,
                    headerRowHeight);
                Rect commentRect = new Rect(duplicateRect.x - spaceBetween - opButtonWidth, subHeaderRect.y, opButtonWidth, 
                    headerRowHeight);

                if (GUI.Button(menuRect, GUIContent.none))
                {
                    ShowEventOptionsMenu(index);
                }

#if !UNITY_2019_4_OR_NEWER               
                menuRect.yMin += 4;
#endif
                DrawContentInCenterOfRect(menuRect, CyanTriggerEditorGUIUtil.EventMenuIcon);
                
                if (GUI.Button(duplicateRect, GUIContent.none))
                {
                    DuplicateEvent(index);
                }
                DrawContentInCenterOfRect(duplicateRect, CyanTriggerEditorGUIUtil.EventDuplicateIcon);

                EditorGUI.BeginDisabledGroup(_editingCommentId != InvalidCommentId);
                if (GUI.Button(commentRect, GUIContent.none))
                {
                    GUI.FocusControl(CommentControlName);
                    _editCommentButtonPressed = true;
                    _editingCommentId = index;
                    _focusedCommentEditor = false;
                }
                EditorGUI.EndDisabledGroup();
                
                DrawContentInCenterOfRect(commentRect, CyanTriggerEditorGUIUtil.EventCommentContent);
            }
            
            EditorGUILayout.EndHorizontal();
            
            if (actionInfo.IsAction())
            {
                GUILayout.Space(2);
                
                Rect customActionRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(20f));
                GUILayout.Space(20f);
                
                float width = (customActionRect.width - EditorGUIUtility.labelWidth - spaceBetween - 2) / 2;
                Rect labelRect = new Rect(customActionRect.x, customActionRect.y, EditorGUIUtility.labelWidth, customActionRect.height);
                Rect programRect = new Rect(labelRect.xMax + 2, customActionRect.y, width, customActionRect.height);
                Rect definitionRect = new Rect(programRect.xMax + spaceBetween, customActionRect.y, width, customActionRect.height);

                EditorGUI.LabelField(labelRect, new GUIContent("Custom Action Info", "The Udon Program and Custom Action Definition that describe how this event is implemented."));
                EditorGUI.BeginDisabledGroup(true);
                
                var actionDefinition = actionInfo.ActionGroup;
                if (actionDefinition is CyanTriggerActionGroupDefinitionUdonAsset udonCustomAction)
                {
                    AbstractUdonProgramSource customProgram = udonCustomAction.udonProgramAsset;
                    EditorGUI.ObjectField(programRect, customProgram, typeof(AbstractUdonProgramSource),false);
                }
                
                EditorGUI.ObjectField(definitionRect, actionDefinition, typeof(CyanTriggerActionGroupDefinition),false);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
                
                string description = actionInfo.Action.description;
                if (!string.IsNullOrEmpty(description))
                {
                    string content = description.Colorize(CyanTriggerColorTheme.Comment, true);
                    GUILayout.Label(content, CyanTriggerEditorGUIUtil.CommentLabelStyle);
                }
            }
            
            // playmode check
            EditorGUI.EndDisabledGroup();

            return modifyAction;
        }

        private void RenderEventWarnings(CyanTriggerActionInfoHolder actionInfo)
        {
            if (_ignoreEventWarningProperty.boolValue)
            {
                return;
            }
            
            var warningMessages = actionInfo.GetEventWarnings();
            if (warningMessages != null && warningMessages.Count > 0)
            {
                foreach (var message in warningMessages)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.HelpBox(message, MessageType.Warning);
                    GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
                    buttonStyle.wordWrap = true;

                    if (GUILayout.Button("Ignore All Warnings", buttonStyle, GUILayout.ExpandHeight(true), GUILayout.Width(70)))
                    {
                        _ignoreEventWarningProperty.boolValue = true;
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
            }
        }
        
        private void RenderEventOptions(
            int eventIndex, 
            SerializedProperty eventProperty, 
            CyanTriggerActionInfoHolder actionInfo)
        {
            EditorGUI.BeginDisabledGroup(_isPlaying);
            
            SerializedProperty eventOptions = eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.eventOptions));
            SerializedProperty delayProperty = eventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.delay));
            SerializedProperty broadcastProperty = eventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.broadcast));
            SerializedProperty replayProperty = eventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.replay));

            CyanTriggerBroadcast broadcast = (CyanTriggerBroadcast)broadcastProperty.enumValueIndex;
            
            SerializedProperty userGateProperty =
                eventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.userGate));
            
            SerializedProperty eventInstance =
                eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.eventInstance));

            List<CyanTriggerEditorVariableOption> GetThisEventVariables(Type type)
            {
                return GetVariableOptions(type, eventIndex, -1);
            }
            
            if (userGateProperty.intValue == (int) CyanTriggerUserGate.UserAllowList ||
                userGateProperty.intValue == (int) CyanTriggerUserGate.UserDenyList)
            {
                SerializedProperty specificUserGateProperty =
                    eventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.userGateExtraData));

                var definition = userGateProperty.intValue == (int) CyanTriggerUserGate.UserAllowList ?
                    AllowedUserGateVariableDefinition :
                    DeniedUserGateVariableDefinition;

                Rect rectRef = Rect.zero;
                CyanTriggerPropertyEditor.DrawActionVariableInstanceMultiInputEditor(
                    _eventOptionRenderData[eventIndex],
                    0,
                    specificUserGateProperty,
                    definition,
                    GetThisEventVariables,
                    ref rectRef,
                    true
                );
            }

            // TODO variable or const delay value
            EditorGUILayout.PropertyField(delayProperty,
                new GUIContent("Delay in Seconds",
                    "This event will be delayed for the given seconds before performing any actions."));

            float delayTime = delayProperty.floatValue;
            if (delayTime < 0)
            {
                delayProperty.floatValue = delayTime = 0;
            }

            bool isCustomEvent = actionInfo.IsCustomEvent();
            bool enableEventVariables = broadcast == CyanTriggerBroadcast.Local && delayTime == 0;
            bool enableParams = isCustomEvent && enableEventVariables;
            
            // Handle "Event_Custom" specially to display the name parameter here
            if (isCustomEvent) 
            {
                SerializedProperty nameProperty = eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.name));
                EditorGUILayout.PropertyField(nameProperty, new GUIContent("Name", "The name of this event."));
                
                string name = nameProperty.stringValue;
                string sanitizedName = name;
                
                if (string.IsNullOrEmpty(sanitizedName))
                {
                    sanitizedName = UnnamedCustomName;
                }
                
                sanitizedName = CyanTriggerNameHelpers.SanitizeName(sanitizedName);

                if (string.IsNullOrEmpty(sanitizedName))
                {
                    sanitizedName = UnnamedCustomName;
                }
                
                if (CyanTriggerNodeDefinitionManager.Instance.TryGetDefinitionFromCompiledName(sanitizedName, 
                        out var node)
                    && node.CustomDefinition == null)
                {
                    EditorGUILayout.HelpBox($"This Custom Event will act the same as {node.FullName}", MessageType.Warning);
                }
                
                // Check if it has arguments and the name is unique. If not, show warning.
                // TODO Hash this value to prevent the n^2 lookup (per event checking every other event)
                var multiInputListProperty = 
                    eventInstance.FindPropertyRelative(nameof(CyanTriggerActionInstance.multiInput));
                int multSize = multiInputListProperty.arraySize;
                if (enableParams && multSize > 0)
                {
                    var events = _cyanTriggerDataInstance.events;
                    bool match = false;
                    for (int index = 0; index < events.Length; ++index)
                    {
                        if (index == eventIndex || index >= _eventInstanceRenderData.Length)
                        {
                            continue;
                        }

                        var eventRenderData = _eventInstanceRenderData[index];
                        if (eventRenderData == null)
                        {
                            continue;
                        }
                        
                        string eventName = eventRenderData.ActionInfo.GetEventCompiledName(events[index]);
                        if (sanitizedName.Equals(eventName))
                        {
                            match = true;
                            break;
                        }
                    }

                    if (match)
                    {
                        EditorGUILayout.HelpBox("Custom Events with Parameters must have a unique name!", MessageType.Error);
                    }
                }

                if (name != sanitizedName)
                {
                    nameProperty.stringValue = sanitizedName;
                }
            }
            
            // Show replay settings
            if (broadcast == CyanTriggerBroadcast.All)
            {
                // Replay options
                EditorGUILayout.PropertyField(replayProperty, new GUIContent("Replay", "How should this event be handled for late joiners? Replay is not the same as SDK2 Buffering as order is not preserved. You can clear the replay count for an event using the ClearReplay action."));
            }
            else
            {
                // Ensure that other events are not set to replay.
                replayProperty.enumValueIndex = (int)CyanTriggerReplay.None;
            }

            CyanTriggerActionVariableDefinition[] variableDefinitions = actionInfo.GetBaseActionVariables(false);
            CyanTriggerEditorVariableOption[] eventVariableOptions = Array.Empty<CyanTriggerEditorVariableOption>();
            // Prevent showing event inputs when not available.
            if (enableEventVariables)
            {
                eventVariableOptions = actionInfo.GetCustomEditorVariableOptions(eventInstance);
            }
            if (!isCustomEvent && variableDefinitions.Length + eventVariableOptions.Length > 0)
            {
                GUILayout.Space(4);

                var eventRenderData = _eventInstanceRenderData[eventIndex];
                bool expanded = eventRenderData.IsExpanded;
                CyanTriggerPropertyEditor.DrawFoldoutListHeader(
                    new GUIContent($"{actionInfo.GetActionRenderingDisplayName()} Inputs"),
                    ref expanded,
                    false,
                    0,
                    null,
                    false,
                    null,
                    false,
                    true
                );

                eventRenderData.IsExpanded = expanded;

                if (expanded)
                {
                    // Draw an outline around the element to emphasize what you are editing.
                    EditorGUILayout.BeginVertical(CyanTriggerEditorGUIUtil.BorderedBoxStyle);
                    
                    // Show variable inputs
                    if (variableDefinitions.Length > 0)
                    {
                        CyanTriggerPropertyEditor.DrawActionInstanceInputEditors(
                            eventRenderData,
                            GetThisEventVariables, 
                            Rect.zero, 
                            true,
                            _eventActionTrees[eventIndex].DeleteVariables);
                        
                        // TODO figure out a better method here. This is hacky and I hate it.
                        if (eventRenderData.ActionInfo.Definition?.Definition ==
                            CyanTriggerCustomNodeOnVariableChanged.NodeDefinition)
                        {
                            CyanTriggerCustomNodeOnVariableChanged.SetVariableExtraData(
                                eventInstance, 
                                _cyanTriggerDataInstance.variables);
                        }
                    }
                    
                    // Display variables provided by the event.
                    if (eventVariableOptions.Length > 0)
                    {
                        GUILayout.Space(2);
                        
                        // TODO clean up visuals here. This is kind of ugly
                        EditorGUILayout.BeginVertical(CyanTriggerEditorGUIUtil.HelpBoxStyle);
                        EditorGUILayout.LabelField("Event Variables");

                        foreach (var variable in eventVariableOptions)
                        {
                            Rect variableRect = EditorGUILayout.BeginHorizontal();
                            GUIContent variableLabel = new GUIContent(
                                 $"{CyanTriggerNameHelpers.GetTypeFriendlyName(variable.Type)} {variable.Name}");
                            Vector2 dim = GUI.skin.label.CalcSize(variableLabel);
                            variableRect.height = 16;
                            variableRect.x = variableRect.xMax - dim.x;
                            variableRect.width = dim.x;
                            EditorGUI.LabelField(variableRect, variableLabel);
                            EditorGUILayout.EndHorizontal();
                            GUILayout.Space(variableRect.height);
                        }

                        EditorGUILayout.EndVertical();
                    }
                    
                    EditorGUILayout.EndVertical();
                }
                GUILayout.Space(7);
            }
            
            // Custom Event input arguments
            if (isCustomEvent)
            {
                // Hacky way to determine if arguments are valid.
                // Input array will include 1 element with the "isVariable" property set to true if this custom event
                // can have and does have variables.
                // If value is set to false, or the input array is empty, then this custom event cannot/does not have variables.
                var inputListProperty = 
                    eventInstance.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
                var multiInputListProperty = 
                    eventInstance.FindPropertyRelative(nameof(CyanTriggerActionInstance.multiInput));

                int customParamIndex = 0;
                int multSize = multiInputListProperty.arraySize;
                int inputSize = inputListProperty.arraySize;
                if (multSize > 0 && inputSize != 1)
                {
                    inputListProperty.arraySize = inputSize = 1;
                }
                if (multSize == 0 && inputSize != 0)
                {
                    inputListProperty.arraySize = inputSize = 0;
                }
                if (inputSize > 0)
                {
                    var validInputProp = inputListProperty.GetArrayElementAtIndex(customParamIndex);
                    var validProp =
                        validInputProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
                    validProp.boolValue = enableParams;
                }

                if (enableParams || inputSize > 0 || multSize > 0)
                {
                    GUILayout.Space(4);

                    if (!enableParams)
                    {
                        EditorGUILayout.HelpBox("Custom Events can only have parameters when broadcast is local and delay is 0.", MessageType.Warning);
                        if (GUILayout.Button("Remove All Parameters"))
                        {
                            multiInputListProperty.ClearArray();
                        }
                    }
                    EditorGUI.BeginDisabledGroup(!enableParams);
                    
                    var eventRenderData = _eventInstanceRenderData[eventIndex];
                    bool expanded = eventRenderData.IsExpanded;
                    CyanTriggerPropertyEditor.DrawFoldoutListHeader(
                        new GUIContent("Parameters", "Add variables that can be set as input from the calling action."),
                        ref expanded,
                        false,
                        0,
                        null,
                        false,
                        null,
                        false,
                        true
                    );
                    eventRenderData.IsExpanded = expanded;
                    
                    if (expanded)
                    {
                        int expectedSize = 1;
                        // Ensure proper size - Should be 1...
                        if (eventRenderData.ExpandedInputs.Length != expectedSize)
                        {
                            Array.Resize(ref eventRenderData.InputLists, expectedSize);
                        }
                        
                        // If the list is empty, create it.
                        var parameterList = eventRenderData.InputLists[customParamIndex];
                        if (parameterList == null)
                        {
                            // Get a unique name for the variable based on all other variables in this event and the public variables
                            string GetUniqueName(string variableName, int varIndex)
                            {
                                List<string> varNames = new List<string>();
                                foreach (var variable in _cyanTriggerDataInstance.variables)
                                {
                                    if (variable.IsDisplayOnly())
                                    {
                                        continue;
                                    }
                                    
                                    varNames.Add(variable.name);
                                }

                                int size = multiInputListProperty.arraySize;
                                for (int index = 0; index < size; ++index)
                                {
                                    // Skip this variable
                                    if (index == varIndex)
                                    {
                                        continue;
                                    }

                                    var prop = multiInputListProperty.GetArrayElementAtIndex(index);
                                    var nameProp =
                                        prop.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));
                                    varNames.Add(nameProp.stringValue);
                                }
                                
                                return GetUniqueVariableName(variableName, varNames.ToArray());
                            }

                            string[] variableGuids;
                            Type[] parameterTypes;
                            GUIContent[] parameterTypesContent;
                            UpdateVariableCached();
                            
                            void UpdateVariableCached()
                            {
                                int size = multiInputListProperty.arraySize;
                                parameterTypesContent = new GUIContent[size];
                                variableGuids = new string[size];
                                parameterTypes = new Type[size];
                                for (int index = 0; index < size; ++index)
                                {
                                    SerializedProperty property = multiInputListProperty.GetArrayElementAtIndex(index);
                                    var dataTypeProp =
                                        property.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                                    var idProp =
                                        property.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
                                    
                                    object typeObj = CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataTypeProp);
                                    string typeName = "<Invalid>";
                                    string typeTooltip = $"Type is Invalid! {typeObj}";
                                    if (typeObj is Type type)
                                    {
                                        typeName = CyanTriggerNameHelpers.GetTypeFriendlyName(type);
                                        typeTooltip = type.FullName;
                                        parameterTypes[index] = type;
                                    }
                                    parameterTypesContent[index] = new GUIContent(typeName, typeTooltip);
                                    variableGuids[index] = idProp.stringValue;
                                }
                            }

                            // Variable updated, meaning all variables inputs for a given type should be cleared to prevent stale data.
                            void ClearActionGetVariableCache(int index)
                            {
                                if (_eventActionTrees[eventIndex] != null)
                                {
                                    _eventActionTrees[eventIndex].ClearGetVariableCacheForType(parameterTypes[index]);
                                }
                            }
                            
                            
                            parameterList = new ReorderableList(
                                multiInputListProperty.serializedObject, 
                                multiInputListProperty, 
                                true, 
                                false, 
                                true, 
                                true);
                            parameterList.headerHeight = 0;
                            parameterList.drawElementCallback = (rect, index, isActive, isFocused) =>
                            {
                                SerializedProperty property = multiInputListProperty.GetArrayElementAtIndex(index);
                                var nameProp =
                                    property.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));
                                var inOutProp =
                                    property.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));

                                float spaceBetween = 5;
                                
                                // type, name input, input/output toggle
                                Rect nameRect = new Rect(rect);
                                nameRect.width /= 2;
                                rect.xMin += nameRect.width;
                                nameRect.width -= spaceBetween;

                                Rect typeRect = new Rect(rect);
                                typeRect.width /= 2;
                                rect.xMin += typeRect.width;
                                typeRect.width -= spaceBetween;
                                
                                Rect inOutRect = new Rect(rect);

                                // Render name
                                string nameOrig = nameProp.stringValue;
                                EditorGUI.PropertyField(nameRect, nameProp, GUIContent.none);
                                string name = CyanTriggerNameHelpers.SanitizeName(nameProp.stringValue);
                                if (nameOrig != name)
                                {
                                    name = nameProp.stringValue = GetUniqueName(name, index);
                                    
                                    // Rename all items using this variable.
                                    Dictionary<string, string> updatedGuids = new Dictionary<string, string>
                                    {
                                        {variableGuids[index], name},
                                    };
            
                                    // Updating variable name will also clear Get Variable cache.
                                    if (_eventActionTrees[eventIndex] != null)
                                    {
                                        _eventActionTrees[eventIndex].UpdateVariableNames(updatedGuids);
                                    }
                                }

                                // Render type
                                EditorGUI.LabelField(typeRect, parameterTypesContent[index]);
                                
                                // Input Output option
                                // true = 0 = input
                                // false = 1 = output
                                int selected = inOutProp.boolValue ? 0 : 1;
                                GUIContent[] options =
                                {
                                    new GUIContent("Input", "Variable data will be provided by the caller of this event."), 
                                    new GUIContent("Output", "Variable value will be read back by the caller after executing this event. This event must set the value of this variable. Value may by uninitialized at start.")
                                };
                                EditorGUI.BeginProperty(inOutRect, GUIContent.none, inOutProp);
                                int newSelected = EditorGUI.Popup(inOutRect, GUIContent.none, selected, options);
                                if (newSelected != selected)
                                {
                                    inOutProp.boolValue = 0 == newSelected;
                                }
                                EditorGUI.EndProperty();
                            };
                            parameterList.onAddCallback = reorderableList =>
                            {
                                void AddVariable(UdonNodeDefinition udonNodeDefinition)
                                {
                                    int last = multiInputListProperty.arraySize++;
                                    SerializedProperty property = multiInputListProperty.GetArrayElementAtIndex(last);
                                    var idProp =
                                        property.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
                                    var nameProp =
                                        property.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));
                                    var inOutProp =
                                        property.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
                                    var dataTypeProp =
                                        property.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
        
                                    Type type = udonNodeDefinition.type;
                                    idProp.stringValue = Guid.NewGuid().ToString();
                                    nameProp.stringValue = 
                                        GetUniqueName(CyanTriggerNameHelpers.GetTypeFriendlyName(type), last);
                                    inOutProp.boolValue = true;
                                    CyanTriggerSerializableObject.UpdateSerializedProperty(dataTypeProp, type);
                                    
                                    ApplyModifiedProperties();
                                    UpdateVariableCached();
                                    
                                    // Clear all cached actions Get Variable data
                                    ClearActionGetVariableCache(last);
                                }
                                CyanTriggerSearchWindowManager.Instance.DisplayVariableSearchWindow(AddVariable);
                            };
                            parameterList.onRemoveCallback = reorderableList =>
                            {
                                int index = reorderableList.index;
                                if (eventIndex < _eventActionTrees.Length)
                                {
                                    var actionList = _eventActionTrees[eventIndex];
                                    SerializedProperty property = multiInputListProperty.GetArrayElementAtIndex(index);
                                    var idProp =
                                        property.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
                                    var nameProp =
                                        property.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));
                            
                                    var nameHash = new HashSet<string> {nameProp.stringValue};
                                    var guidHash = new HashSet<string> {idProp.stringValue};

                                    actionList.DeleteVariables(guidHash, nameHash);
                                }

                                // Perform remove
                                multiInputListProperty.DeleteArrayElementAtIndex(index);
                                int size = multiInputListProperty.arraySize;
                                if (index >= size - 1)
                                {
                                    reorderableList.index = size - 1;
                                }
                            
                                // Clear all cached actions Get Variable data
                                // Clear before updating the cache as the type data still exists.
                                ClearActionGetVariableCache(index);
                                
                                UpdateVariableCached();
                            };
                            parameterList.onReorderCallback = _ =>
                            {
                                UpdateVariableCached();
                            };
                            
                            eventRenderData.InputLists[customParamIndex] = parameterList;
                        }

                        parameterList.DoLayoutList();
                    }
                    EditorGUI.EndDisabledGroup();
                }
            }
            
            EditorGUI.EndDisabledGroup();
        }

        private void RenderEventActions(int eventIndex)
        {
            if (_eventActionTrees[eventIndex] == null)
            {
                Debug.LogWarning($"Event action tree is null for event {eventIndex}");
                UpdateOrCreateActionTreeForEvent(eventIndex);
                _eventActionTrees[eventIndex].ExpandAll();
                UpdateActionTreeViewProperties();
            }
            _eventActionTrees[eventIndex].DoLayoutTree();
        }

        private void HandleEventRightClick(Rect eventRect, int eventIndex)
        {
            if (_isPlaying)
            {
                return;
            }
            
            Event current = Event.current;
            if(current.type == EventType.ContextClick && eventRect.Contains(current.mousePosition))
            {
                ShowEventOptionsMenu(eventIndex);
                current.Use(); 
            }
        }

        private void ShowEventOptionsMenu(int eventIndex)
        {
            /*
            Move Event to Top
            Move Event to Bottom
            Open All Scope
            Close All Scope
             */


            GenericMenu menu = new GenericMenu();
                
            GUIContent moveEventUpContent = new GUIContent("Move Event Up");
            GUIContent moveEventDownContent = new GUIContent("Move Event Down");
            if (eventIndex > 0)
            {
                menu.AddItem(moveEventUpContent, false, () =>
                {
                    SwapEventElements(new List<int> {eventIndex});
                    ApplyModifiedProperties();
                });
                // TODO Move to top, 
            }
            else
            {
                menu.AddDisabledItem(moveEventUpContent, false);
            }
            if (eventIndex + 1 < _eventsProperty.arraySize)
            {
                menu.AddItem(moveEventDownContent, false, () =>
                {
                    SwapEventElements(new List<int> {eventIndex + 1});
                    ApplyModifiedProperties();
                });
                // TODO move to Bottom
            }
            else
            {
                menu.AddDisabledItem(moveEventDownContent, false);
            }

            SerializedProperty eventProperty = _eventsProperty.GetArrayElementAtIndex(eventIndex);
            SerializedProperty expandedProperty = eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.expanded));
            void ToggleEventExpanded()
            {
                expandedProperty.boolValue = !expandedProperty.boolValue;
                ApplyModifiedProperties();
            }

            GUIContent eventExpandOption = expandedProperty.boolValue
                ? new GUIContent("Minimize Event", "")
                : new GUIContent("Maximize Event", "");
            menu.AddItem(eventExpandOption, false, ToggleEventExpanded);

            void SetActionEditorExpandState(bool value)
            {
                SerializedProperty actions = eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.actionInstances));

                for (int i = 0; i < actions.arraySize; ++i)
                {
                    SerializedProperty action = actions.GetArrayElementAtIndex(i);
                    SerializedProperty expanded = action.FindPropertyRelative(nameof(CyanTriggerEvent.expanded));
                    expanded.boolValue = value;
                }
                ApplyModifiedProperties();
                _eventActionTrees[eventIndex].RefreshHeight();
            }
            
            menu.AddItem(new GUIContent("Open all Action Editors"), false, () => SetActionEditorExpandState(true));
            menu.AddItem(new GUIContent("Close all Action Editors"), false, () => SetActionEditorExpandState(false));

            
            // Add or edit comment for the event
            SerializedProperty eventInstance =
                eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.eventInstance));
            SerializedProperty eventComment =
                eventInstance.FindPropertyRelative(nameof(CyanTriggerActionInstance.comment));
            SerializedProperty eventCommentProperty =
                eventComment.FindPropertyRelative(nameof(CyanTriggerComment.comment));
            GUIContent commentContent = new GUIContent(string.IsNullOrEmpty(eventCommentProperty.stringValue)
                ? "Add Comment"
                : "Edit Comment");
            menu.AddItem(commentContent, false, () =>
            {
                _editingCommentId = eventIndex;
                _focusedCommentEditor = false;
            });
            
                
            menu.AddSeparator("");
            
            menu.AddItem(new GUIContent("Duplicate Event"), false, () =>
            {
                DuplicateEvent(eventIndex);
            });
            
            menu.AddSeparator("");
            
            menu.AddItem(new GUIContent("Delete Event"), false, () =>
            {
                RemoveEvents(new List<int> {eventIndex});
                ApplyModifiedProperties();
            });

            if (_eventActionTrees[eventIndex] != null)
            {
                GUIContent clearAllActionsContent = new GUIContent("Clear All Actions");
                if (_eventActionTrees[eventIndex].Elements.arraySize > 0)
                {
                    menu.AddItem(clearAllActionsContent, false, () =>
                    {
                        _eventActionTrees[eventIndex].Elements.ClearArray();
                        ApplyModifiedProperties();
                    });
                }
                else
                {
                    menu.AddDisabledItem(clearAllActionsContent, false);
                }
            }

            menu.ShowAsContext();
        }

        private void AddPrefabApplyRevertOptions(
            GenericMenu menu, 
            SerializedProperty property,
            Action onApply = null,
            Action onRevert = null,
            GUIContent applyContent = null,
            GUIContent revertContent = null)
        {
            if (applyContent == null)
            {
                applyContent = new GUIContent("Apply Prefab Change");
            }
            if (revertContent == null)
            {
                revertContent = new GUIContent("Revert Prefab Changes");
            }
            
            menu.AddItem(applyContent, false, () =>
            {
                string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                    PrefabUtility.GetOutermostPrefabInstanceRoot(property.serializedObject.targetObject));
                
                PrefabUtility.ApplyPropertyOverride(property, assetPath, InteractionMode.UserAction);
                
                ApplyModifiedProperties(true);
                onApply?.Invoke();
            });
            
            menu.AddItem(revertContent, false, () =>
            {
                var dataProperty = property.Copy();
                foreach (var propObj in dataProperty)
                {
                    var prop = propObj as SerializedProperty;
                    prop.prefabOverride = false;
                }
                
                ApplyModifiedProperties(true);
                onRevert?.Invoke();
            });
        }
        
        private void RenderAddEventButton()
        {
            EditorGUI.BeginDisabledGroup(_isPlaying);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Add Event", GUILayout.Height(EditorGUIUtility.singleLineHeight * 2)))
            {
                void AddFavoriteEvent(CyanTriggerSettingsFavoriteItem newEventInfo)
                {
                    var data = newEventInfo.data;
                    AddEvent(CyanTriggerActionInfoHolder.GetActionInfoHolder(data.guid, data.directEvent));
                    ApplyModifiedProperties();
                }

                CyanTriggerSearchWindowManager.Instance.DisplayEventsFavoritesSearchWindow(AddFavoriteEvent, true);
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();
        }

        private enum TriggerModifyAction
        {
            None,
            Delete,
            MoveUp,
            MoveDown,
        }
    }
    
    public class CyanTriggerEditorVariableOption
    {
        public Type Type;
        public string Name;
        public string ID;
        public bool IsReadOnly;
        
        // Hacked in here for Custom Event arguments
        public bool IsInput;
        public string UdonName;
    }

    public class CyanTriggerEditorVariableOptionList
    {
        public readonly Type Type;
        public readonly List<CyanTriggerEditorVariableOption> 
            VariableOptions = new List<CyanTriggerEditorVariableOption>();

        public CyanTriggerEditorVariableOptionList(Type t)
        {
            Type = t;
        }
    }

    public class CyanTriggerEditorScopeTree
    {
        private readonly List<CyanTriggerEditorVariableOption> _variableOptions =
            new List<CyanTriggerEditorVariableOption>();
        private readonly List<int> _prevIndex = new List<int>();
        private readonly List<int> _startIndex = new List<int>();

        public IEnumerable<CyanTriggerEditorVariableOption> GetVariableOptions(Type varType, int index)
        {
            if (index < 0 || index >= _startIndex.Count)
            {
                yield break;
            }
            int ind = _startIndex[index];

            while (ind != -1)
            {
                var variable = _variableOptions[ind];
                if (varType.IsAssignableFrom(variable.Type) || variable.Type.IsAssignableFrom(varType))
                {
                    yield return variable;
                }
                
                ind = _prevIndex[ind];
            }
        }

        // TODO validate expected name or type
        public bool IsVariableValid(int index, string guid)
        {
            if (index < 0 || index >= _startIndex.Count)
            {
                return false;
            }
            int ind = _startIndex[index];

            while (ind != -1)
            {
                var variable = _variableOptions[ind];
                if (variable.ID == guid)
                {
                    return true;
                }
                
                ind = _prevIndex[ind];
            }

            return false;
        }
        

        public void CreateStructure(SerializedProperty actionList)
        {
            _variableOptions.Clear();
            _prevIndex.Clear();
            _startIndex.Clear();

            Stack<int> lastScopes = new Stack<int>();
            int lastScopeIndex = -1;
            for (int i = 0; i < actionList.arraySize; ++i)
            {
                SerializedProperty actionProperty = actionList.GetArrayElementAtIndex(i);
                CyanTriggerActionInfoHolder actionInfo = 
                    CyanTriggerActionInfoHolder.GetActionInfoHolderFromProperties(actionProperty);
                int scopeDelta = actionInfo.GetScopeDelta();

                if (scopeDelta > 0)
                {
                    lastScopes.Push(lastScopeIndex);
                }
                else if (scopeDelta < 0)
                {
                    lastScopeIndex = lastScopes.Pop();
                }
                
                _startIndex.Add(lastScopeIndex);
                
                var variables = actionInfo.GetCustomEditorVariableOptions(actionProperty);
                if (variables != null)
                {
                    foreach (var variable in variables)
                    {
                        _prevIndex.Add(lastScopeIndex);
                        lastScopeIndex = _variableOptions.Count;
                        
                        _variableOptions.Add(variable);
                    }
                }
            }
        }
    }
}
