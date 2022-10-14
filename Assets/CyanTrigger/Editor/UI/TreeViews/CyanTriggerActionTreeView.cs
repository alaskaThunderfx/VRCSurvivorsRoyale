using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerActionInstanceRenderData
    {
        private SerializedProperty _property;
        public SerializedProperty Property
        {
            get => _property;
            set
            {
                if (_property == value)
                {
                    return;
                }
                _property = value;
                Initialize();
            }
        }

        private int _cachedExpanded = -1;
        private SerializedProperty _expandedProperty;
        public bool IsExpanded
        {
            get
            {
                if (_cachedExpanded == -1)
                {
                    _cachedExpanded = (_expandedProperty != null && _expandedProperty.boolValue) ? 1 : 0;
                }
                return _cachedExpanded == 1;
            }
            set
            {
                int valueInt = value ? 1 : 0;
                if (_expandedProperty != null && _cachedExpanded != valueInt)
                {
                    _expandedProperty.boolValue = value;
                    _expandedProperty.serializedObject.ApplyModifiedProperties();
                    _cachedExpanded = valueInt;
                }
            }
        }

        private string _cachedComment = null;
        private SerializedProperty _commentProperty;
        public string Comment
        {
            get
            {
                if (_cachedComment == null)
                {
                    _cachedComment = _commentProperty != null ? _commentProperty.stringValue : string.Empty;
                }
                return _cachedComment;
            }
            set
            {
                if (_commentProperty != null && _cachedComment != value)
                {
                    _commentProperty.stringValue = value;
                    _cachedComment = value;
                }
            }
        }
        
        private CyanTriggerActionInfoHolder _actionInfo;
        public CyanTriggerActionInfoHolder ActionInfo
        {
            get
            {
                if (!_actionInfo.IsValid())
                {
                    Initialize();
                }
                return _actionInfo;
            }
            set
            {
                if (value == _actionInfo)
                {
                    return;
                }
                _actionInfo = value;
                Initialize();
            }
        }

        public CyanTriggerDataInstance DataInstance;
        public UdonBehaviour UdonBehaviour;
        public bool AllowsUnityObjectConstants;

        public bool[] ExpandedInputs = Array.Empty<bool>();
        public ReorderableList[] InputLists = Array.Empty<ReorderableList>();
        public bool NeedsRedraws;
        public bool NeedsVerify;
        public bool NeedsReinitialization;
        public bool ContainsNull;
        public bool HasPrefabChanges;
        
        public float LastActionLabelHeight;
        public float LastCommentLabelHeight;
        public float LastActionDescriptionLabelHeight;

        private bool _isInitializing;

        private Func<Type, List<CyanTriggerEditorVariableOption>> _getVariableOptionsForType;
        private readonly Dictionary<Type, List<CyanTriggerEditorVariableOption>> _getVariableOptionsCache =
            new Dictionary<Type, List<CyanTriggerEditorVariableOption>>();

        private CyanTriggerActionVariableDefinition[] _variableDefinitionsCached;
        public CyanTriggerActionVariableDefinition[] VariableDefinitions
        {
            get
            {
                if (_variableDefinitionsCached == null)
                {
                    _variableDefinitionsCached = ActionInfo.GetVariablesWithExtras(_property, false);
                }
                return _variableDefinitionsCached;
            }
        }
        
        public void Initialize()
        {
            // In the case where the ActionInfo is invalid, without this check it would infinite loop intializing
            if (_isInitializing)
            {
                return;
            }
            _isInitializing = true;
            
            NeedsReinitialization = false;
            _getVariableOptionsCache.Clear();
            ClearCache();
            
            if (_property != null)
            {
                _actionInfo = CyanTriggerActionInfoHolder.GetActionInfoHolderFromProperties(_property);
                _expandedProperty = _property.FindPropertyRelative(nameof(CyanTriggerActionInstance.expanded));
                    
                var commentBaseProperty = _property.FindPropertyRelative(nameof(CyanTriggerActionInstance.comment));
                _commentProperty = commentBaseProperty.FindPropertyRelative(nameof(CyanTriggerComment.comment));

                UpdateVariableSize();
            }
            else
            {
                _actionInfo = null;
                _expandedProperty = null;
                _commentProperty = null;
                
                ExpandedInputs = Array.Empty<bool>();
                InputLists = Array.Empty<ReorderableList>();
            }

            _isInitializing = false;
        }
        
        public void UpdateVariableSize()
        {
            // Clear cache and get variables again.
            _variableDefinitionsCached = null;
            var variables = VariableDefinitions;

            if (InputLists.Length != variables.Length)
            {
                InputLists = new ReorderableList[variables.Length];
            }
            else
            {
                for (int i = 0; i < InputLists.Length; ++i)
                {
                    InputLists[i] = null;
                }
            }

            if (ExpandedInputs.Length != variables.Length)
            {
                ExpandedInputs = new bool[variables.Length];
                for (int cur = 0; cur < variables.Length; ++cur)
                {
                    // TODO turn these into serialized properties when persisted
                    ExpandedInputs[cur] = true;
                }
            }
        }

        public void UpdateExpandCache()
        {
            if (_expandedProperty != null)
            {
                IsExpanded = _expandedProperty.boolValue;
            }
        }
        
        public void ClearInputLists()
        {
            _getVariableOptionsCache.Clear();
            if (InputLists == null)
            {
                return;
            }

            for (int i = 0; i < InputLists.Length; ++i)
            {
                InputLists[i] = null;
            }
        }

        public void ClearCache()
        {
            _cachedComment = null;
            _cachedExpanded = -1;
        }

        public void SetGetVariableOptions(Func<Type, List<CyanTriggerEditorVariableOption>> getVariableOptionsForType)
        {
            _getVariableOptionsForType = getVariableOptionsForType;
        }
        
        public List<CyanTriggerEditorVariableOption> GetVariableOptionsForType(Type type)
        {
            if (_getVariableOptionsCache.TryGetValue(type, out var options))
            {
                return options;
            }

            if (_getVariableOptionsForType != null)
            {
                options = _getVariableOptionsForType(type);
            }
            else
            {
                options = new List<CyanTriggerEditorVariableOption>();
            }
            _getVariableOptionsCache[type] = options;

            return options;
        }

        public void ClearVariableCache()
        {
            _getVariableOptionsCache.Clear();
        }

        public void ClearVariableCacheForType(Type type)
        {
            if (type == null)
            {
                _getVariableOptionsCache.Clear();
            }
            else
            {
                _getVariableOptionsCache.Remove(type);
                if (type == typeof(UdonBehaviour))
                {
                    _getVariableOptionsCache.Remove(typeof(IUdonEventReceiver));
                }
                else if (type == typeof(IUdonEventReceiver))
                {
                    _getVariableOptionsCache.Remove(typeof(UdonBehaviour));
                }
            }
        }
    }
    
    public class CyanTriggerActionTreeView : CyanTriggerScopedDataTreeView<CyanTriggerActionInstanceRenderData>
    {
        private const float DefaultRowHeight = 20;
        private const float SpaceAboveRowEditor = 2;
        private const float SpaceBetweenRowEditor = 6;
        private const float SpaceBetweenRowEditorSides = 6;
        private const float CellVerticalMargin = 3;
        private const float CellHorizontalMargin = 6;
        private const float ExpandButtonSize = 16;

        private const string CommentControlName = "Action Editor Comment Control";

        public Action OnActionChanged;
        public Func<Type, int, List<CyanTriggerEditorVariableOption>> GetVariableOptions;
        public Func<int, string, string, bool> IsVariableValid;

        private readonly CyanTriggerDataInstance _dataInstance;
        private readonly UdonBehaviour _udonBehaviour;
        private readonly bool _isSceneTrigger;
        private readonly AnimBool _showActions;
        private bool _needsReverify;
        
        private bool _delayRefreshRowHeight = false;
        private bool _delayActionsChanged = false;
        private bool _delayExpandAll = false;


        private int _mouseOverId = -1;
        private int _editingCommentId = -1;
        private bool _focusedCommentEditor = false;
        private float _lastRectWidth = -1;

        private int _lastKeyboardControl = -1;
        private int _focusedActionId = -1;
        private bool _itemHasChanges = false;
        
        private bool _isPlaying;

        private readonly List<int> _delayUpdateDisplayText = new List<int>();
        
        private static MultiColumnHeader CreateColumnHeader()
        {
            MultiColumnHeaderState.Column[] columns =
            {
                new MultiColumnHeaderState.Column
                {
                    minWidth = 50f, width = 100f, headerTextAlignment = TextAlignment.Center, canSort = false
                }
            };
            MultiColumnHeader multiColumnHeader = new MultiColumnHeader(new MultiColumnHeaderState(columns))
            {
                height = 0,
            };
            multiColumnHeader.ResizeToFit();
            
            return multiColumnHeader;
        }
        
        protected override string GetElementDisplayName(SerializedProperty actionProperty, int index)
        {
            CyanTriggerActionInfoHolder actionInfo = 
                CyanTriggerActionInfoHolder.GetActionInfoHolderFromProperties(actionProperty);

            bool withColor = CyanTriggerSettings.Instance.useColorThemes;
            if (CyanTriggerCustomNodeInspectorManager.Instance.TryGetCustomInspectorDisplayText(
                    actionInfo, out var customDisplayText))
            {
                if (_dataInstance != null)
                {
                    return customDisplayText.GetCustomDisplayText(actionInfo, actionProperty, _dataInstance, withColor);
                }
                
                _delayUpdateDisplayText.Add(index);
            }
            
            return actionInfo.GetActionRenderingDisplayName(actionProperty, withColor);
        }
        
        protected override int GetElementScopeDelta(SerializedProperty actionProperty, int index)
        {
            CyanTriggerActionInfoHolder actionInfo = 
                CyanTriggerActionInfoHolder.GetActionInfoHolderFromProperties(actionProperty);
            return actionInfo.GetScopeDelta();
        }
        
        public CyanTriggerActionTreeView(
            SerializedProperty elements,
            CyanTriggerDataInstance cyanTriggerData,
            UdonBehaviour udonBehaviour,
            bool isSceneTrigger,
            Action onActionChanged,
            Func<Type, int, List<CyanTriggerEditorVariableOption>> getVariableOptions,
            Func<int, string, string, bool> isVariableValid) 
            : base(elements, CreateColumnHeader())
        {
            showBorder = true;
            rowHeight = DefaultRowHeight;
            useScrollView = false;

            _dataInstance = cyanTriggerData;
            _udonBehaviour = udonBehaviour;
            _isSceneTrigger = isSceneTrigger;
            OnActionChanged = onActionChanged;

            _showActions = new AnimBool(true, Repaint);

            GetVariableOptions = getVariableOptions;
            IsVariableValid = isVariableValid;

            // Proxy so that each element can draw their own, even if they don't currently have children.
            // This does remove the nice animation though. 
            // TODO fix the nice animation by persisting foldout state
            foldoutOverride = (position, expandedState, style) => expandedState;

            DelayReload();
        }

        public void Dispose()
        {
            if (_focusedActionId != -1 && _itemHasChanges)
            {
                UpdateVariableNamesFromProvider(_focusedActionId, Undo.GetCurrentGroup());
                _itemHasChanges = false;
            }
            
            OnActionChanged?.Invoke();
        }

        protected override void InitializeTreeViewGuiReflection(object treeGUI)
        {
            // Set the selection gui style
            // ReSharper disable once PossibleNullReferenceException
            FieldInfo selectionStyleProperty = treeGUI.GetType().BaseType.GetField("m_SelectionStyle",
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            selectionStyleProperty?.SetValue(treeGUI, CyanTriggerEditorGUIUtil.SelectedStyle);
        }

        protected override void OnElementsSet()
        {
            foreach (var data in GetData())
            {
                int index = GetItemIndex(data.Item2);
                if (index < ItemElements.Length)
                {
                    data.Item1.Property = ItemElements[index];
                }
            }
        }

        protected override void OnBuildRoot(CyanTriggerScopedTreeItem root)
        {
            // On rebuild, assume lists need to be recreated.
            foreach (var data in GetData())
            {
                data.Item1.ClearInputLists();
            }

            // Force reverify
            _needsReverify = true;
        }

        public void VerifyActions()
        {
            // Go through all actions and verify that the listed variable is valid.
            // TODO add other per action checks here
            
            bool VerifyActionInput(
                int actionIndex, 
                SerializedProperty varProp, 
                CyanTriggerActionInstanceRenderData data,
                CyanTriggerActionVariableDefinition varDef)
            {
                var isVariable = varProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
                
                if (!isVariable.boolValue)
                {
                    // Only check for nulls on non hidden types.
                    if ((varDef.variableType & CyanTriggerActionVariableTypeDefinition.Hidden) == 0)
                    {
                        data.ContainsNull |= CyanTriggerPropertyEditor.InputContainsNullVariableOrValue(varProp);
                    }
                    return false;
                }
            
                var varId = varProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
                var varName = varProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));
                var curGuid = varId.stringValue;
                var curName = varName.stringValue;
                
                bool allowsCustomValues =
                    (varDef.variableType & CyanTriggerActionVariableTypeDefinition.Constant) != 0;
                bool outputVar = 
                    (varDef.variableType & CyanTriggerActionVariableTypeDefinition.VariableOutput) != 0;

                // Catch the case for creating a new variable with out inputs.
                // Assume these are always valid.
                // TODO figure out cases where this is invalid?
                // - name field needs to not be empty?
                if (outputVar && !allowsCustomValues && string.IsNullOrEmpty(curName) && !string.IsNullOrEmpty(curGuid))
                {
                    return false;
                }
                
                bool modified = false;
                if (!IsVariableValid(actionIndex, curGuid, curName))
                {
                    varName.stringValue = "";
                    varId.stringValue = "";
                    modified = true;
                }

                data.ContainsNull |= CyanTriggerPropertyEditor.InputContainsNullVariableOrValue(varProp);

                return modified;
            }

            bool VerifyAction(int actionIndex,
                bool beforeInputs,
                SerializedProperty actionProp,
                CyanTriggerActionInstanceRenderData data)
            {
                if (!beforeInputs)
                {
                    return false;
                }

                // Initializing so nothing has nulls
                data.ContainsNull = false;
                
                // Check if allows multi input and list is zero
                var variableDefs = data.VariableDefinitions;
                if (variableDefs.Length > 0)
                {
                    var varDef = variableDefs[0];
                    if ((varDef.variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0)
                    {
                        var multiInputs = actionProp.FindPropertyRelative(nameof(CyanTriggerActionInstance.multiInput));
                        int multiSize = multiInputs.arraySize;
                        data.ContainsNull = multiSize == 0;
                    }
                }

                return false;
            }

            UpdateActionInputs(VerifyActionInput, VerifyAction);
        }

        public CyanTriggerActionInstanceRenderData GetOrCreateExpandDataFromActionIndex(int index)
        {
            return GetOrCreateExpandData(IdStartIndex + index);
        }
        
        private CyanTriggerActionInstanceRenderData GetOrCreateExpandData(int id, bool forceCreate = false)
        {
            var data = GetData(id);
            int index = GetItemIndex(id);
            if (forceCreate || data == null)
            {
                data = new CyanTriggerActionInstanceRenderData();
                SetData(id, data);
            }

            data.DataInstance = _dataInstance;
            data.UdonBehaviour = _udonBehaviour;
            data.AllowsUnityObjectConstants = _isSceneTrigger;

            data.Property = index < ItemElements.Length ? 
                ItemElements[index] : 
                Elements.GetArrayElementAtIndex(index);

            if (data.NeedsReinitialization)
            {
                data.Initialize();
            }
            
            return data;
        }

        private bool ShouldShowVariantSelector(CyanTriggerActionInstanceRenderData actionData)
        {
            var definition = actionData.ActionInfo.Definition;
            return definition == null ||
                !(definition.DefinitionType == CyanTriggerNodeDefinition.UdonDefinitionType.CyanTriggerSpecial ||
                  definition.DefinitionType == CyanTriggerNodeDefinition.UdonDefinitionType.CyanTriggerVariable ||
                  definition.DefinitionType == CyanTriggerNodeDefinition.UdonDefinitionType.Type);
        }
        
        private bool ItemCanExpand(CyanTriggerActionInstanceRenderData actionData)
        {
            return actionData.ActionInfo.IsValid() && 
                   (ShouldShowVariantSelector(actionData) || actionData.VariableDefinitions.Length > 0);
        }

        private bool ItemHasComment(CyanTriggerActionInstanceRenderData actionData)
        {
            string comment = actionData.Comment;
            if (!string.IsNullOrEmpty(comment))
            {
                return true;
            }
            
            var definition = actionData.ActionInfo.Definition;
            return definition?.Definition == CyanTriggerCustomNodeComment.NodeDefinition;
        }

        public void UpdateAllItemDisplayNames()
        {
            _delayRefreshRowHeight = true;
            for (int i = 0; i < Items.Length; ++i)
            {
                if (Items[i] != null)
                {
                    Items[i].displayName = GetElementDisplayName(ItemElements[i], i);
                }
            }
        }

        public void ClearGetVariableCache()
        {
            ClearGetVariableCacheForType(null);
        }

        public void ClearGetVariableCacheForType(Type type)
        {
            foreach (var data in GetData())
            {
                data.Item1.ClearVariableCacheForType(type);
            }
        }
        
        private void UpdateActionInputs(
            Func<int, SerializedProperty, CyanTriggerActionInstanceRenderData, CyanTriggerActionVariableDefinition, bool> inputUpdateMethod, 
            Func<int, bool, SerializedProperty, CyanTriggerActionInstanceRenderData, bool> actionUpdateMethod)
        {
            bool anyChanges = false;
            for (int curAction = 0; curAction < Items.Length; ++curAction)
            {
                var item = Items[curAction];
                if (item == null)
                {
                    continue;
                }

                var data = GetOrCreateExpandData(item.id);
                if (data?.ActionInfo == null)
                {
                    continue;
                }
                
                var variableDefs = data.VariableDefinitions;
                var property = data.Property;
                var inputs = property.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
                int size = inputs.arraySize;

                bool modified = false;
                
                if (actionUpdateMethod != null)
                {
                    modified |= actionUpdateMethod(curAction, true, property, data);
                }
                
                if (inputUpdateMethod != null)
                {
                    for (int curInput = 0; curInput < variableDefs.Length; ++curInput)
                    {
                        var varDef = variableDefs[curInput];
                        if (varDef == null)
                        {
                            continue;
                        }
                        
                        if (curInput == 0 && 
                            (varDef.variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0)
                        {
                            var multiInputs = property.FindPropertyRelative(nameof(CyanTriggerActionInstance.multiInput));
                            int multiSize = multiInputs.arraySize;
                        
                            for (int curMultiInput = 0; curMultiInput < multiSize; ++curMultiInput)
                            {
                                var multiInputProp = multiInputs.GetArrayElementAtIndex(curMultiInput);
                                modified |= inputUpdateMethod(curAction, multiInputProp, data, varDef);
                            }
                        
                            continue;
                        }

                        if (curInput >= size)
                        {
                            break;
                        }
                    
                        var inputProp = inputs.GetArrayElementAtIndex(curInput);
                        modified |= inputUpdateMethod(curAction, inputProp, data, varDef);
                    }
                }
                
                // Ensure inputs are verified first.                
                if (actionUpdateMethod != null)
                {
                    modified |= actionUpdateMethod(curAction, false, property, data);
                }

                if (modified)
                {
                    item.displayName = GetElementDisplayName(data.Property, item.Index);
                    data.ClearInputLists();
                    anyChanges = true;
                }
            }

            if (anyChanges)
            {
                _delayRefreshRowHeight = true;
            }
        }

        private void UpdateVariableNamesFromProvider(int actionId, int curUndo)
        {
            var data = GetData(actionId);
            if (data == null)
            {
                return;
            }
            
            var variables = data.ActionInfo.GetCustomEditorVariableOptions(data.Property);
            if (variables == null || variables.Length == 0)
            {
                return;
            }

            Dictionary<string, string> updatedGuids = new Dictionary<string, string>();
            foreach (var variable in variables)
            {
                updatedGuids.Add(variable.ID, variable.Name);
            }
            
            UpdateVariableNames(updatedGuids);
            
            OnActionChanged?.Invoke();
            
            Undo.CollapseUndoOperations(curUndo-1);
        }
        
        public void UpdateVariableNames(Dictionary<string, string> guidToNames)
        {
            if (guidToNames == null)
            {
                return;
            }

            ClearGetVariableCache();
            
            bool UpdateVariableProperty(
                int actionIndex, 
                SerializedProperty varProp, 
                CyanTriggerActionInstanceRenderData data,
                CyanTriggerActionVariableDefinition varDef)
            {
                var isVariable = varProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
                if (!isVariable.boolValue)
                {
                    return false;
                }
                
                var varId = varProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
                var varName = varProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));
                var curGuid = varId.stringValue;
                var curName = varName.stringValue;
                
                if (guidToNames.TryGetValue(curGuid, out string newName) 
                    && !string.IsNullOrEmpty(curName)
                    && curName != newName)
                {
                    varName.prefabOverride = false;
                    varName.stringValue = newName;
                    return true;
                }

                // Handle when changed variable had a callback and this variable is using the previous version of it.
                if (CyanTriggerCustomNodeOnVariableChanged.IsPrevVariable(varName.stringValue, curGuid))
                {
                    string varMainId = CyanTriggerCustomNodeOnVariableChanged.GetMainVariableId(curGuid);
                    if (!string.IsNullOrEmpty(varMainId) && guidToNames.TryGetValue(varMainId, out newName))
                    {
                        varId.stringValue = CyanTriggerCustomNodeOnVariableChanged.GetPrevVariableGuid(
                            CyanTriggerCustomNodeOnVariableChanged.GetOldVariableName(newName),
                            varMainId);
                        return true;
                    }
                }

                return false;
            }

            UpdateActionInputs(UpdateVariableProperty, null);
        }

        public void DeleteVariables(HashSet<string> guids, HashSet<string> names)
        {
            if (guids.Count == 0 && names.Count == 0)
            {
                return;
            }

            bool ClearDeletedVariableProperty(
                int actionIndex, 
                SerializedProperty varProp, 
                CyanTriggerActionInstanceRenderData data,
                CyanTriggerActionVariableDefinition varDef)
            {
                var isVariable = varProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
                if (!isVariable.boolValue)
                {
                    return false;
                }
                
                var varId = varProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
                var varName = varProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));
                var curGuid = varId.stringValue;
                var curName = varName.stringValue;
                
                if (guids.Contains(curGuid) || (string.IsNullOrEmpty(curGuid) && names.Contains(curName)))
                {
                    varName.stringValue = "";
                    varId.stringValue = "";
                    data.ContainsNull = true;
                    return true;
                }
                
                return false;
            }

            UpdateActionInputs(ClearDeletedVariableProperty, null);
        }

        public void UndoReset()
        {
            ClearData();
            _delayExpandAll = true;
            IdStartIndex = -1; // Clears current id to prevent trying to move them
            Reload(); // Reload to get proper size after undo.
            OnActionChanged?.Invoke();
        }
        
        public void DoLayoutTree()
        {
            _isPlaying = EditorApplication.isPlaying;
            
            _mouseOverId = -1;

            bool showView = _showActions.target;

            EditorGUILayout.BeginVertical();
            EditorGUI.BeginDisabledGroup(_isPlaying);
            
            CyanTriggerPropertyEditor.DrawFoldoutListHeader(
                new GUIContent($"Actions ({VisualSize})"),
                ref showView,
                false,
                0,
                null,
                false,
                null,
                false,
                true,
                true,
                default,
                "CyanTrigger Actions",
                CyanTriggerDocumentationLinks.ActionsDocumentation
                );
            _showActions.target = showView;
            
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();

            Rect actionsHeaderRect = GUILayoutUtility.GetLastRect();
            Event current = Event.current;
            if(Elements.prefabOverride 
               && current.type == EventType.ContextClick 
               && actionsHeaderRect.Contains(current.mousePosition))
            {
                GenericMenu menu = new GenericMenu();
                
                GUIContent applyContent = new GUIContent("Apply All Action Prefab Changes");
                GUIContent revertContent = new GUIContent("Revert All Action Prefab Changes");

                AddPrefabApplyRevertOptions(menu, Elements, -1, Reload, Reload, applyContent, revertContent);
                menu.ShowAsContext();
 
                current.Use(); 
            }

            // Hacky way to show the prefab change mark
            EditorGUI.BeginProperty(actionsHeaderRect, GUIContent.none, Elements);
            EditorGUI.EndProperty();
            
            if (!EditorGUILayout.BeginFadeGroup(_showActions.faded))
            {
                EditorGUILayout.EndFadeGroup();
                return;
            }
            
            if (Size != Elements.arraySize)
            {
                ClearData();
                Reload();
                _delayActionsChanged = true;
            }

            if (_delayExpandAll)
            {
                _delayExpandAll = false;
                ExpandAll();
            }
            
            CheckDelayActionsChanged();
            ReverifyCheck();
            
            VerifyDisplayNames();

            // Remove selection when treeview is not focused.
            if (!HasFocus() && HasSelection())
            {
                SetSelection(new List<int>(), TreeViewSelectionOptions.FireSelectionChanged);
            }
            
            
            // Action has a comment being edited. Check if we it needs to be closed. 
            if (_editingCommentId != -1)
            {
                // Started editing a comment, refresh row heights
                if (!_focusedCommentEditor)
                {
                    _delayRefreshRowHeight = true;
                }

                // Detect if we should close the comment editor
                Event cur = Event.current;
                bool enterPressed = cur.type == EventType.KeyDown &&
                                    cur.keyCode == KeyCode.Return &&
                                    (cur.shift || cur.alt || cur.command || cur.control);
                if ((_focusedCommentEditor && GUI.GetNameOfFocusedControl() != CommentControlName) ||
                    enterPressed)
                {
                    _editingCommentId = -1;
                    GUI.FocusControl(null);
                    _delayRefreshRowHeight = true;

                    if (enterPressed)
                    {
                        cur.Use();
                    }
                }
            }
            
            
            Rect treeRect = EditorGUILayout.BeginVertical();
#if !UNITY_2019_4_OR_NEWER
            treeRect.x += 1;
            treeRect.width -= 2;
#endif
            
            // Calculate if we need to update row heights before getting tree's height for layout purposes.
            if (treeRect.width > 0)
            {
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (_lastRectWidth <= 0 || treeRect.width != _lastRectWidth)
                {
                    _delayRefreshRowHeight = true;
                }
                _lastRectWidth = treeRect.width;
            }
            
            if (_delayRefreshRowHeight && Event.current?.type == EventType.Layout)
            {
                _delayRefreshRowHeight = false;
                RefreshCustomRowHeights();
                Repaint();
            }
            
            treeRect.height = totalHeight + (VisualSize == 0 ? DefaultRowHeight : 1);
            GUILayout.Space(treeRect.height+1);
            
            var listActionFooterIcons = new[]
            {
                new GUIContent("SDK2", "Add action from list of SDK2 actions"),
                EditorGUIUtility.TrIconContent("Favorite", "Add action from favorites actions"),
                EditorGUIUtility.TrIconContent("Toolbar Plus", "Add action from all actions"),
                EditorGUIUtility.TrIconContent("FilterByType", "Add Local Variable"),
                EditorGUIUtility.TrIconContent("TreeEditor.Duplicate", "Duplicate selected item"),
                EditorGUIUtility.TrIconContent("Toolbar Minus", "Remove selection from list")
            };
            
            bool hasSelection = HasSelection();
            CyanTriggerPropertyEditor.DrawButtonFooter(
                listActionFooterIcons, 
                new Action[]
                {
                    AddNewActionFromSDK2List,
                    AddNewActionFromFavoriteList,
                    AddNewActionFromAllList,
                    AddLocalVariable,
                    DuplicateSelectedItems,
                    RemoveSelected
                },
                _isPlaying
                    ? new [] { true, true, true, true, true, true }
                    : new [] { false, false, false, false, !hasSelection, !hasSelection },
                "CyanTrigger Actions",
                CyanTriggerDocumentationLinks.ActionsDocumentation
                );
            
            // Draw the treeview!
            OnGUI(treeRect);

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndFadeGroup();
            

            CheckDelayActionsChanged();
            ReverifyCheck();
        }
        
        protected override void BeforeRowsGUI()
        {
            // Ensure selection style is initialized as it may not be after updating ui settings.
            var selectionStyle = CyanTriggerEditorGUIUtil.SelectedStyle;
            
            float height = treeViewRect.height + state.scrollPos.y;
            if (Event.current.rawType == EventType.Repaint)
            {
                // Draw background color from style
                CyanTriggerEditorGUIUtil.BackgroundColorStyle.Draw(new Rect(0.0f, 0.0f, 100000f, height), false, false, false, false);    
            }
            
            if (VisualSize > 0)
            {
                // Draw the bottom border edge to make it obvious that this is the end of all elements.
                var boxStyle = new GUIStyle();
                boxStyle.normal.background = CyanTriggerImageResources.ActionTreeGrayBox;
                GUI.Box(new Rect(0, height-2, 100000f, 2), GUIContent.none, boxStyle);
            }
        }

        private void VerifyDisplayNames()
        {
            if (_delayUpdateDisplayText.Count > 0)
            {
                foreach (int index in _delayUpdateDisplayText)
                {
                    Items[index].displayName = GetElementDisplayName(ItemElements[index], index);
                }
                _delayUpdateDisplayText.Clear();
            }
        }
        
        private void CheckDelayActionsChanged()
        {
            if (_delayActionsChanged)
            {
                _delayActionsChanged = false;
                OnActionChanged?.Invoke();
            }
        }

        private void ReverifyCheck()
        {
            if (_needsReverify)
            {
                _needsReverify = false;
                VerifyActions();
            }
        }

        public void RefreshHeight()
        {
            foreach (var data in GetData())
            {
                data.Item1.UpdateExpandCache();
            }
            
            _delayRefreshRowHeight = false;
            RefreshCustomRowHeights();
        }

        private float GetLabelHeight(CyanTriggerScopedTreeItem item, CyanTriggerActionInstanceRenderData data, float indent)
        {
            if (data.ActionInfo?.Definition?.Definition == CyanTriggerCustomNodeComment.NodeDefinition)
            {
                return 0;
            }
            
            float width = _lastRectWidth - CellHorizontalMargin * 2 - ExpandButtonSize - 2 - indent;
            
            // Handle prefab bolding
            GUIStyle style = CyanTriggerEditorGUIUtil.TreeViewLabelStyle;
            if (data.Property.prefabOverride)
            {
                style = new GUIStyle(style);
                style.font = EditorStyles.boldFont;
            }
            float height = style.CalcHeight(new GUIContent(item.displayName), width);

            return height;
        }
        
        private float GetCommentHeight(int id, CyanTriggerActionInstanceRenderData data, float indent)
        {
            if (!ItemHasComment(data) && _editingCommentId != id)
            {
                return 0;
            }
            
            float width = _lastRectWidth - CellHorizontalMargin * 2 - ExpandButtonSize - 2 - indent;

            // Handle prefab bolding
            GUIStyle style = CyanTriggerEditorGUIUtil.CommentStyle;
            if (data.Property.prefabOverride)
            {
                style = new GUIStyle(style);
                style.font = EditorStyles.boldFont;
            }
            
            // Colorizing to prevent rich text from being evaluated.
            string escapedComment = $"// {data.Comment}".Colorize(Color.black, true);
            float height = style.CalcHeight(new GUIContent(escapedComment), width);
            return height;
        }
        
        private float GetActionDescriptionHeight(CyanTriggerActionInstanceRenderData data, float indent)
        {
            if (data.ActionInfo == null || !data.ActionInfo.IsAction())
            {
                return 0;
            }
            
            float width = _lastRectWidth - CellHorizontalMargin * 2 - ExpandButtonSize - 2 - indent;

            // Colorizing to prevent rich text from being evaluated.
            string escapedComment = data.ActionInfo.Action.description.Colorize(Color.black, true);
            float height = CyanTriggerEditorGUIUtil.CommentStyle.CalcHeight(new GUIContent(escapedComment), width);
            return height;
        }

        protected override float GetCustomRowHeight(int row, TreeViewItem item)
        {
            var scopedItem = (CyanTriggerScopedTreeItem) item;
            CyanTriggerActionInstanceRenderData data = GetOrCreateExpandData(item.id);
            float indent = GetContentIndent(item);
            bool canExpand = ItemCanExpand(data);

            float labelHeight = GetLabelHeight(scopedItem, data, indent);
            float commentHeight = GetCommentHeight(scopedItem.id, data, indent);
            float actionDescriptionHeight = GetActionDescriptionHeight(data, indent);

            data.LastActionLabelHeight = labelHeight;
            data.LastCommentLabelHeight = commentHeight;
            data.LastActionDescriptionLabelHeight = actionDescriptionHeight;
            
            // top and bottom margin for labels
            float height = 2 * CellVerticalMargin + labelHeight + commentHeight;
            

            if (canExpand && data.IsExpanded)
            {
                height += actionDescriptionHeight;
                
                int index = scopedItem.Index;
                // Should show drop down
                bool shouldShowVariants = ShouldShowVariantSelector(data);
                if (shouldShowVariants)
                {
                    height += DefaultRowHeight;
                    
                    // Add Custom Action Info height
                    if (data.ActionInfo.IsAction())
                    {
                        height += DefaultRowHeight + SpaceBetweenRowEditor;
                    }
                }

                data.SetGetVariableOptions(type => GetVariableOptions(type, index));
                // Force initialize multi-editors before trying to get heights.
                CyanTriggerPropertyEditor.InitializeMultiInputEditors(
                    data,
                    type => data.GetVariableOptionsForType(type));
                
                float inputHeight = CyanTriggerPropertyEditor.GetHeightForActionInstanceInputEditors(data);

                height += inputHeight;
                
                // Add separator spacing
                if (shouldShowVariants && inputHeight > 5)
                {
                    height += SpaceBetweenRowEditor * 2;
                }
                height += SpaceBetweenRowEditor + SpaceAboveRowEditor;
            }

            return height;
        }
        
        protected override void OnRowGUI(RowGUIArgs args)
        {
            var item = (CyanTriggerScopedTreeItem)args.item;
            var data = GetOrCreateExpandData(item.id);
            bool canExpand = ItemCanExpand(data);
            
            Rect rowRect = args.rowRect;
            Event current = Event.current;
            bool fakeIgnoreRightClick = false;
            if (rowRect.Contains(current.mousePosition))
            {
                _mouseOverId = item.id;
                fakeIgnoreRightClick = current.type == EventType.ContextClick;
            }
            
            // Draw border before anything else so it is behind everything.
            var boxStyle = new GUIStyle
            {
                border = new RectOffset(1, 1, 1, 1), 
                normal =
                {
                    background = data.ContainsNull
                        ? CyanTriggerImageResources.ActionTreeWarningOutline
                        : CyanTriggerImageResources.ActionTreeOutlineTop
                }
            };

            // Move action editor to the left to make it more obvious about scope
            float foldoutIndent = GetFoldoutIndent(item) - 2;
            Rect contentsRect = new Rect(rowRect);
            if (item.depth > 0)
            {
                var lineStyle = new GUIStyle
                {
                    normal = { background = CyanTriggerImageResources.ActionTreeGrayBox }
                };
                float space = foldoutIndent / item.depth;
                Rect lineRect = new Rect(contentsRect.x, contentsRect.y, 1, contentsRect.height);
                for (int i = 0; i < item.depth; ++i)
                {
                    GUI.Box(lineRect, GUIContent.none, lineStyle);
                    lineRect.x += space;
                }
                
                contentsRect.xMin += foldoutIndent;
            }
            
            // Draw an outline around the element to emphasize what you are editing.
            GUI.Box(contentsRect, GUIContent.none, boxStyle);


            float itemIndent = GetContentIndent(item);
            float actionLabelHeight = data.LastActionLabelHeight;
            float commentHeight = data.LastCommentLabelHeight;

            Rect propertyDisplay = new Rect(rowRect);
            propertyDisplay.height = actionLabelHeight + commentHeight + CellVerticalMargin * 2;
            EditorGUI.BeginProperty(propertyDisplay, GUIContent.none, data.Property);

            // On prefab changing, label size might increase or decrease enough to change needed height.
            if (data.Property.isInstantiatedPrefab && data.Property.prefabOverride != data.HasPrefabChanges)
            {
                _delayRefreshRowHeight = true;
                data.HasPrefabChanges = data.Property.prefabOverride;
            }

            // Draw Row comment and label
            float labelHeight = DrawRowLabel(
                rowRect, 
                itemIndent,
                actionLabelHeight,
                commentHeight,
                item, 
                data, 
                args.label, 
                canExpand, 
                args.selected, 
                args.focused);

            // Show documentation link when mouse is hovered on this action
            {
                Rect cellRect = new Rect(rowRect);
                cellRect.yMin += CellVerticalMargin;
                
                // For non comment actions, add comment height to move it down.
                if (data.ActionInfo?.Definition?.Definition != CyanTriggerCustomNodeComment.NodeDefinition)
                {
                    cellRect.yMin += commentHeight;
                }
                
                Rect docRect = GetExpandIconRect(cellRect, ExpandButtonSize);

                // Move over to ensure there is room for the Expand button.
                if (canExpand)
                {
                    cellRect.xMax = docRect.xMin;
                    docRect = GetExpandIconRect(cellRect, ExpandButtonSize);
                    docRect.x += 3;
                }

                bool shouldShowDocumentationLink = _mouseOverId == item.id;
                CyanTriggerEditorUtils.DrawDocumentationButtonForActionInfo(docRect, data.ActionInfo, shouldShowDocumentationLink);
            }

            rowRect = contentsRect;
            
            // End property early to prevent bolding of all child items.
            if (fakeIgnoreRightClick)
            {
                Event.current.type = EventType.Used;
            }
            
            EditorGUI.EndProperty();
                
            if (fakeIgnoreRightClick)
            {
                Event.current.type = EventType.ContextClick;
            }
            
            
            // Return early due to not being expanded.
            if (!canExpand || !data.IsExpanded || _delayRefreshRowHeight)
            {
                return;
            }
            
            bool isUndo = (Event.current.commandName == "UndoRedoPerformed");
            if (isUndo)
            {
                data.NeedsRedraws = true;
                ClearInputList(data);
            }

            // Move usable area over by the size of the foldout icon. This is to make it more obvious what scope is included.
            rowRect.xMin += CyanTriggerEditorGUIUtil.FoldoutStyle.fixedWidth;

            // Remove top area from Row
            rowRect.yMin += labelHeight;


            // Draw rect around area to separate it from everything else
            rowRect.x += SpaceBetweenRowEditorSides;
            rowRect.width -= SpaceBetweenRowEditorSides * 2;
            rowRect.yMin += SpaceAboveRowEditor;
            rowRect.height -= SpaceBetweenRowEditor;
            
            if (current.type == EventType.Repaint)
            {
                // Draw background to overwrite the selection blue
                Rect minimalRect = new Rect(rowRect.x + 1, rowRect.y + 1, rowRect.width - 2, rowRect.height - 2);
                CyanTriggerEditorGUIUtil.BackgroundColorStyle.Draw(minimalRect, false, false, false, false);
            
                // Draw the rounded rectangle to contain all inputs
                CyanTriggerEditorGUIUtil.HelpBoxStyle.Draw(rowRect, false, false, false, false); 
            }

            rowRect.yMin += SpaceBetweenRowEditor;

            rowRect.x += SpaceBetweenRowEditorSides;
            rowRect.width -= SpaceBetweenRowEditorSides * 2;

            
            EditorGUI.BeginDisabledGroup(_isPlaying);
            
            if (ShouldShowVariantSelector(data))
            {
                Rect variantRect = new Rect(rowRect);
                variantRect.height = DefaultRowHeight;
                
                DrawVariantSelector(variantRect, args, data);
            
                rowRect.yMin += DefaultRowHeight + SpaceBetweenRowEditor * 2 + data.LastActionDescriptionLabelHeight;
                if (data.ActionInfo.IsAction())
                {
                    rowRect.yMin += DefaultRowHeight + SpaceBetweenRowEditor;
                }

                if (rowRect.height > EditorGUIUtility.singleLineHeight)
                {
                    float sideSpace = 5;
                    float lift = SpaceBetweenRowEditor * 1.5f;
                    Rect separatorRect = new Rect(rowRect.x + sideSpace, rowRect.y - lift, rowRect.width - sideSpace * 2, 1);
                    EditorGUI.DrawRect(separatorRect, CyanTriggerImageResources.LineColorDark);
                }
            }

            int undoGroup = Undo.GetCurrentGroup();
            EditorGUI.BeginChangeCheck();
            int index = item.Index;
            data.SetGetVariableOptions(type => GetVariableOptions(type, index));
            CyanTriggerPropertyEditor.DrawActionInstanceInputEditors(
                data, 
                type => data.GetVariableOptionsForType(type), 
                rowRect, 
                false,
                DeleteVariables);
            
            if (_lastKeyboardControl != GUIUtility.keyboardControl)
            {
                // Previous item had modifications before keyboard focus changed. Try update variable names.
                if (_itemHasChanges)
                {
                    UpdateVariableNamesFromProvider(_focusedActionId, undoGroup);
                }
                
                _lastKeyboardControl = GUIUtility.keyboardControl;
                _focusedActionId = item.id;
                _itemHasChanges = false;
            }
            
            if (undoGroup != Undo.GetCurrentGroup() && _itemHasChanges)
            {
                UpdateVariableNamesFromProvider(item.id, undoGroup);
            }
            
            EditorGUI.EndDisabledGroup();

            // TODO try to minimize the number of times this gets called...
            if (EditorGUI.EndChangeCheck() || data.NeedsRedraws || isUndo)
            {
                item.displayName = GetElementDisplayName(data.Property, item.Index);
                
                _delayRefreshRowHeight = true;

                if (_focusedActionId == item.id)
                {
                    _itemHasChanges = true;
                }
            }

            if (data.NeedsRedraws)
            {
                data.NeedsRedraws = false;
                _delayRefreshRowHeight = true;
            }

            if (data.NeedsVerify)
            {
                data.NeedsVerify = false;
                _delayActionsChanged = true;
            }
        }

        private Rect GetExpandIconRect(Rect rowRect, float size)
        {
            return new Rect(rowRect.xMax - CellHorizontalMargin - size, rowRect.y - 1, size, size);
        }
        
        private float DrawRowLabel(
            Rect rowRect, 
            float itemIndent,
            float actionLabelHeight,
            float commentHeight,
            CyanTriggerScopedTreeItem item, 
            CyanTriggerActionInstanceRenderData data, 
            string label, 
            bool canExpand, 
            bool selected, 
            bool focused)
        {
            float labelHeight = actionLabelHeight + commentHeight + CellVerticalMargin * 2;

            Rect cellRect = new Rect(rowRect);
            cellRect.yMin += CellVerticalMargin;
            
            Rect commentRect = new Rect(cellRect);
            commentRect.height = commentHeight;
            
            cellRect.yMin += commentHeight;

            if (canExpand)
            {
                Rect expandButtonRect = GetExpandIconRect(cellRect, ExpandButtonSize);
                
                // Draw expand button on right of the row.
                GUIContent expandIcon = data.IsExpanded
                    ? CyanTriggerEditorGUIUtil.CloseActionEditorIcon
                    : CyanTriggerEditorGUIUtil.OpenActionEditorIcon;
                
                if (GUI.Button(expandButtonRect, expandIcon, new GUIStyle()))
                {
                    data.IsExpanded = !data.IsExpanded;
                    _delayRefreshRowHeight = true;
                }
            }

            // Draw foldout for items with scope, even if they have no children
            if (item.HasScope)
            {
                int id = item.id;
                
                float foldoutIndent = GetFoldoutIndent(item);
                GUIStyle foldoutStyle = CyanTriggerEditorGUIUtil.FoldoutStyle;
                Rect foldoutRect = new Rect(cellRect.x + foldoutIndent, cellRect.y, foldoutStyle.fixedWidth, foldoutStyle.lineHeight);
                bool isExpanded = IsExpanded(id);
                bool newExpand = GUI.Toggle(foldoutRect, isExpanded, GUIContent.none, foldoutStyle);
                if (isExpanded != newExpand)
                {
                    if (Event.current.alt)
                    {
                        SetExpandedRecursive(id, newExpand);
                    }
                    else
                    {
                        SetExpanded(id, newExpand);
                    }
                }
            }

            float rightMargin = CellHorizontalMargin * 2 + ExpandButtonSize;
            cellRect.width -= rightMargin;
            commentRect.width -= rightMargin;
                
            cellRect.xMin += itemIndent;
            commentRect.xMin += itemIndent;
            
            
            EditorGUI.BeginDisabledGroup(_isPlaying);
            
            // Draw comment and action labels
            if (Event.current.rawType == EventType.Repaint)
            {
                if (commentHeight > 0)
                {
                    string comment = $"// {data.Comment}".Colorize(CyanTriggerColorTheme.Comment, true);
                    CyanTriggerEditorGUIUtil.CommentStyle.Draw(commentRect, comment, false, false, selected, false);
                }

                if (actionLabelHeight > 0)
                {
                    CyanTriggerEditorGUIUtil.TreeViewLabelStyle.Draw(cellRect, label, false, false, selected, focused);
                }
            }
            
            EditorGUI.EndDisabledGroup();

            // Draw comment editor
            if (_editingCommentId == item.id)
            {
                Event cur = Event.current;
                bool wasEscape = (cur.type == EventType.KeyDown && cur.keyCode == KeyCode.Escape);

                commentRect.height += 4;
                GUI.SetNextControlName(CommentControlName);
                string comment = EditorGUI.TextArea(commentRect, data.Comment, EditorStyles.textArea);
                data.Comment = comment;

                if (!_focusedCommentEditor)
                {
                    _focusedCommentEditor = true;
                    EditorGUI.FocusTextInControl(CommentControlName);
                }
                
                float newHeight = GetCommentHeight(item.id, data, itemIndent);
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (newHeight != commentHeight)
                {
                    _delayRefreshRowHeight = true;
                }
                
                Rect completeButtonRect = new Rect(rowRect.xMax - CellHorizontalMargin - ExpandButtonSize, rowRect.y,
                    ExpandButtonSize, EditorGUIUtility.singleLineHeight);
                
                
                bool completeButton = GUI.Button(completeButtonRect, CyanTriggerEditorGUIUtil.CommentCompleteIcon, new GUIStyle());
                
                if (wasEscape || completeButton)
                {
                    if (comment.Length > 0 && comment.Trim().Length == 0)
                    {
                        data.Comment = "";
                    }
                    
                    _editingCommentId = -1;
                    GUI.FocusControl(null);
                    _delayRefreshRowHeight = true;
                }
            }
            
            return labelHeight;
        }
        

        private void DrawVariantSelector(Rect rect, RowGUIArgs args, CyanTriggerActionInstanceRenderData data)
        {
            int variantCount = CyanTriggerActionGroupDefinitionUtil.Instance.GetActionVariantCount(data.ActionInfo);

            var actionTypeProp = data.Property.FindPropertyRelative(nameof(CyanTriggerActionInstance.actionType));
            EditorGUI.BeginProperty(rect, GUIContent.none, actionTypeProp);
            
            float spaceBetween = 5;
            float width = (rect.width - spaceBetween * 2) / 3f;

            Rect labelRect = new Rect(rect.x, rect.y, width, rect.height);
            GUI.Label(labelRect, new GUIContent($"Action Variants ({variantCount})"));
            Rect buttonRect = new Rect(labelRect.xMax + spaceBetween, rect.y, rect.width - spaceBetween - labelRect.width, rect.height);
            
            EditorGUI.BeginDisabledGroup(variantCount <= 1);
            if (GUI.Button(buttonRect, data.ActionInfo.GetMethodSignature(), new GUIStyle(EditorStyles.popup)))
            {
                GenericMenu menu = new GenericMenu();
                
                foreach (var actionVariant in CyanTriggerActionGroupDefinitionUtil.Instance.GetActionVariantInfoHolders(data.ActionInfo))
                {
                    menu.AddItem(new GUIContent(actionVariant.GetMethodSignature()), false, (t) =>
                    {
                        var actionInfo = (CyanTriggerActionInfoHolder) t;
                        if (actionInfo == data.ActionInfo)
                        {
                            return;
                        }
                        
                        // Remove old variable providers
                        var oldVariables = data.ActionInfo.GetCustomEditorVariableOptions(data.Property);
                        var newVariables = actionInfo.GetBaseActionVariables(true);
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
                        DeleteVariables(guidHash, nameHash);
                        
                        
                        // set action type and clear data
                        // TODO find a way to copy some values if type matches
                        CyanTriggerSerializedPropertyUtils.SetActionData(actionInfo, ItemElements[GetItemIndex(args.item.id)]);
                        data = GetOrCreateExpandData(args.item.id, true);
                        data.IsExpanded = true;
                        data.NeedsRedraws = true;
                        
                        _delayActionsChanged = true;
                        _delayRefreshRowHeight = true;
                    }, actionVariant);
                }
                
                menu.ShowAsContext();
            }
            EditorGUI.EndDisabledGroup();
            
            // Override prefab right click to ensure ui refreshes properly.
            Event current = Event.current;
            if (actionTypeProp.prefabOverride
                && current.type == EventType.ContextClick 
                && rect.Contains(current.mousePosition))
            {
                GenericMenu menu = new GenericMenu();

                void RefreshData()
                {
                    data.NeedsReinitialization = true;
                }
                
                AddPrefabApplyRevertOptions(menu, actionTypeProp, GetItemIndex(args.item.id), RefreshData, RefreshData);
                menu.ShowAsContext();
 
                current.Use(); 
            }
            
            EditorGUI.EndProperty();

            // Show custom action information
            if (data.ActionInfo.IsAction())
            {
                labelRect.y += DefaultRowHeight;
                buttonRect.y += DefaultRowHeight;
                Rect definitionRect = new Rect(buttonRect);
                
                GUI.Label(labelRect, new GUIContent("Custom Action Info", "The Udon Program and Custom Action Definition that describe how this action is implemented."));
                
                EditorGUI.BeginDisabledGroup(true);
                
                var actionDefinition = data.ActionInfo.ActionGroup;
                if (actionDefinition is CyanTriggerActionGroupDefinitionUdonAsset udonCustomAction)
                {
                    AbstractUdonProgramSource customProgram = udonCustomAction.udonProgramAsset;
                    Rect programRect = new Rect(buttonRect);
                    programRect.width /= 2;
                    programRect.width -= 2;
                    definitionRect.xMin += programRect.width + 4;
                    EditorGUI.ObjectField(programRect, customProgram, typeof(AbstractUdonProgramSource),false);
                }
                
                EditorGUI.ObjectField(definitionRect, actionDefinition, typeof(CyanTriggerActionGroupDefinition),false);
                EditorGUI.EndDisabledGroup();

                string description = data.ActionInfo.Action.description;
                if (!string.IsNullOrEmpty(description))
                {
                    Rect descriptionRect = new Rect(labelRect.x, labelRect.y + DefaultRowHeight + 3, rect.width, data.LastActionDescriptionLabelHeight);
                    string content = description.Colorize(CyanTriggerColorTheme.Comment, true);
                    GUI.Label(descriptionRect, content, CyanTriggerEditorGUIUtil.CommentLabelStyle);
                }
            }
        }
        
        protected override bool ShowRightClickMenu()
        {
            // Don't handle key events while in playmode
            return !_isPlaying;
        }
        
        protected override bool CanHandleKeyEvents()
        {
            // Don't handle key events while in playmode
            return !_isPlaying;
        }
        
        protected override void HandleKeyEvent()
        {
            Event cur = Event.current;
            // Detect if we should open the comment editor
            bool enterPressed = cur.type == EventType.KeyDown &&
                                cur.keyCode == KeyCode.Return &&
                                (cur.shift || cur.alt || cur.command || cur.control);
            
            // Start commenting on currently selected action.
            if (_editingCommentId == -1 && HasSelection() && enterPressed)
            {
                int shouldCommentId = -1;
                foreach (var selected in GetSelection())
                {
                    int index = GetItemIndex(selected);
                    if (index >= 0 && index < Items.Length && !IsItemHidden(Items[index]))
                    {
                        shouldCommentId = selected;
                        _editingCommentId = selected;
                        break;
                    }
                }

                if (shouldCommentId != -1)
                {
                    _focusedCommentEditor = false;
                    _delayRefreshRowHeight = true;
                    cur.Use();
                    SetSelection(new List<int>(), TreeViewSelectionOptions.FireSelectionChanged);
                }
            }
        }
        
        protected override bool CanDuplicate(IEnumerable<int> items)
        {
            if (_isPlaying)
            {
                return false;
            }
            
            foreach (int id in items)
            {
                int index = GetItemIndex(id);
                if (!CanItemBeMoved(Items[index]))
                {
                    return false;
                }
            }
            
            return true;
        }

        protected override List<(int, int)> DuplicateItems(IEnumerable<int> items)
        {
            List<(int, int)> newIds = new List<(int, int)>();
            HashSet<int> duplicatedInd = new HashSet<int>();
            List<int> sortedItems = new List<int>(items);
            sortedItems.Sort();

            int idStart = IdStartIndex;

            Dictionary<string, string> variableGuidMap = new Dictionary<string, string>();
            
            foreach (int id in sortedItems)
            {
                int index = GetItemIndex(id);
                if (duplicatedInd.Contains(index))
                {
                    continue;
                }
                
                var item = Items[index];
                for (int i = item.Index; i <= item.ScopeEndIndex; ++i)
                {
                    DuplicateAction(ItemElements[i], variableGuidMap);
                    newIds.Add((i + idStart, Elements.arraySize - 1 + idStart));
                    duplicatedInd.Add(i);
                }
            }
            
            OnActionChanged?.Invoke();

            return newIds;
        }
        
        protected override List<SerializedProperty> GetProperties(IEnumerable<int> items)
        {
            List<SerializedProperty> res = new List<SerializedProperty>();
            HashSet<int> duplicatedInd = new HashSet<int>();
            List<int> sortedItems = new List<int>(items);
            sortedItems.Sort();

            foreach (int id in sortedItems)
            {
                int index = GetItemIndex(id);
                if (duplicatedInd.Contains(index))
                {
                    continue;
                }
                
                var item = Items[index];
                for (int i = item.Index; i <= item.ScopeEndIndex; ++i)
                {
                    res.Add(ItemElements[i]);
                    duplicatedInd.Add(i);
                }
            }

            return res;
        }
        
        protected override List<SerializedProperty> DuplicateProperties(IEnumerable<SerializedProperty> items)
        {
            Dictionary<string, string> variableGuidMap = new Dictionary<string, string>();
            List<SerializedProperty> ret = new List<SerializedProperty>();
            
            foreach (var property in items)
            {
                ret.Add(DuplicateAction(property, variableGuidMap));
            }

            return ret;
        }

        protected override bool AllowRenameOption()
        {
            return false;
        }

        protected override void GetRightClickMenuOptions(GenericMenu menu, Event currentEvent)
        {
            base.GetRightClickMenuOptions(menu, currentEvent);

            if (_mouseOverId != -1)
            {
                var data = GetData(_mouseOverId);
                
                menu.AddSeparator("");

                if (data.Property.prefabOverride)
                {
                    void RefreshData()
                    {
                        data.NeedsReinitialization = true;
                    }

                    AddPrefabApplyRevertOptions(menu, data.Property, GetItemIndex(_mouseOverId), RefreshData, RefreshData);
                    menu.AddSeparator("");
                }
                
                menu.AddItem(new GUIContent(ItemHasComment(data) ? "Edit Comment" : "Add Comment"), false, () =>
                {
                    _editingCommentId = _mouseOverId;
                    _focusedCommentEditor = false;
                    SetSelection(new List<int>(), TreeViewSelectionOptions.FireSelectionChanged);
                });
                
                if (ItemCanExpand(data))
                {
                    GUIContent expandContent = data.IsExpanded
                        ? new GUIContent("Close Action Editor")
                        : new GUIContent("Open Action Editor");
                    menu.AddItem(expandContent, false, () =>
                    {
                        _delayRefreshRowHeight = true;
                        data.IsExpanded = !data.IsExpanded;
                    });
                }
            }
            
            menu.AddSeparator("");
            int curMouseOver = _mouseOverId;
            
            // Add new actions at the parent of the selected
            menu.AddItem(new GUIContent("Add Local Variable"), false, () =>
            {
                CyanTriggerSearchWindowManager.Instance.DisplayVariableSearchWindow(
                    GUIUtility.GUIToScreenPoint(currentEvent.mousePosition),
                    definition => AddNewActionAtSelected(definition, curMouseOver));
            });
            menu.AddItem(new GUIContent("Add Favorite Action"), false, () =>
            {
                CyanTriggerSearchWindowManager.Instance.DisplayActionFavoritesSearchWindow(
                    GUIUtility.GUIToScreenPoint(currentEvent.mousePosition),
                    item => AddNewActionAtSelected(item, curMouseOver));
            });
            menu.AddItem(new GUIContent("Add Action"), false, () =>
            {
                CyanTriggerSearchWindowManager.Instance.DisplayActionSearchWindow(
                    GUIUtility.GUIToScreenPoint(currentEvent.mousePosition),
                    holder => AddNewActionDirectAtSelected(holder, curMouseOver));
            });
        }

        private void AddPrefabApplyRevertOptions(
            GenericMenu menu,
            SerializedProperty property,
            int index,
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
                
                OnActionChanged?.Invoke();
                _delayRefreshRowHeight = true;
                onApply?.Invoke();

                if (index != -1)
                {
                    _delayUpdateDisplayText.Add(index);
                }
            });
            
            menu.AddItem(revertContent, false, () =>
            {
                var dataProperty = property.Copy();
                dataProperty.prefabOverride = false;
                foreach (var propObj in dataProperty)
                {
                    var prop = propObj as SerializedProperty;
                    prop.prefabOverride = false;
                }
                
                OnActionChanged?.Invoke();
                _delayRefreshRowHeight = true;
                onRevert?.Invoke();
                
                if (index != -1)
                {
                    _delayUpdateDisplayText.Add(index);
                }
            });
        }
        
        protected override void DoubleClickedItem(int id)
        {
            var data = GetData(id);
            if (data == null || !ItemCanExpand(data))
            {
                return;
            }

            data.IsExpanded = !data.IsExpanded;
            _delayRefreshRowHeight = true;
        }
        
        protected override bool CanItemBeRemoved(CyanTriggerScopedTreeItem item)
        {
            return !IsItemHidden(item) && !_isPlaying;
        }
        
        protected override bool CanItemBeMoved(CyanTriggerScopedTreeItem item)
        {
            return !IsItemHidden(item) && !_isPlaying;
        }

        // TODO create a better method for this instead of implicitly using hidden
        private bool IsItemHidden(CyanTriggerScopedTreeItem item)
        {
            var data = GetOrCreateExpandData(item.id);
            var definition = data.ActionInfo.Definition;
            return definition != null &&
                   CyanTriggerNodeDefinitionManager.DefinitionIsHidden(definition.FullName);
        }

        // TODO allow force setting the parent if it doesn't allow direct children.
        // Ex: if should drop item into condition. This requires modifying both args and parent.
        protected override bool ShouldRejectDragAndDrop(DragAndDropArgs args, CyanTriggerScopedTreeItem parent)
        {
            if (parent == rootItem)
            {
                return false;
            }

            var data = GetOrCreateExpandData(parent.id);
            var definition = data.ActionInfo.Definition;
            return definition != null &&
                   CyanTriggerNodeDefinitionManager.DefinitionPreventsDirectChildren(definition.FullName);
        }
        
        private void AddLocalVariable()
        {
            CyanTriggerSearchWindowManager.Instance.DisplayVariableSearchWindow(AddNewAction);
        }
        
        private void AddNewActionFromAllList()
        {
            CyanTriggerSearchWindowManager.Instance.DisplayActionSearchWindow(AddNewActionDirect);
        }

        private void AddNewActionFromFavoriteList()
        {
            CyanTriggerSearchWindowManager.Instance.DisplayActionFavoritesSearchWindow(AddNewAction);
        }
        
        private void AddNewActionFromSDK2List()
        {
            CyanTriggerSearchWindowManager.Instance.DisplaySDK2ActionFavoritesSearchWindow(AddNewAction);
        }

        private void AddNewAction(UdonNodeDefinition udonNode)
        {
            AddNewActionDirect(CyanTriggerActionInfoHolder.GetActionInfoHolder(udonNode));
        }
        
        private void AddNewAction(CyanTriggerSettingsFavoriteItem favorite)
        {
            AddNewActionDirect(CyanTriggerActionInfoHolder.GetActionInfoHolder(favorite));
        }

        private void AddNewActionDirect(CyanTriggerActionInfoHolder actionInfoHolder)
        {
            AddNewAction(actionInfoHolder);
        }
        
        private void AddNewActionAtSelected(UdonNodeDefinition udonNode, int insertNodeIndex)
        {
            AddNewActionDirectAtSelected(CyanTriggerActionInfoHolder.GetActionInfoHolder(udonNode), insertNodeIndex);
        }
        
        private void AddNewActionAtSelected(CyanTriggerSettingsFavoriteItem favorite, int insertNodeIndex)
        {
            AddNewActionDirectAtSelected(CyanTriggerActionInfoHolder.GetActionInfoHolder(favorite), insertNodeIndex);
        }

        private void AddNewActionDirectAtSelected(CyanTriggerActionInfoHolder actionInfoHolder, int insertNodeIndex)
        {
            int size = Elements.arraySize;
            AddNewAction(actionInfoHolder);
            Reload();

            CyanTriggerScopedTreeItem insertNode = null;
            if (insertNodeIndex != -1)
            {
                insertNode = Items[GetItemIndex(insertNodeIndex)];
            }
            
            var itemsToMove = new List<int> { IdStartIndex + size };
            
            CyanTriggerScopedTreeItem insertParent = insertNode;
            DragAndDropPosition dragPosition;
            int insertIndex = 0;
            if (insertParent == null)
            {
                insertParent = (CyanTriggerScopedTreeItem)rootItem;
                dragPosition = DragAndDropPosition.BetweenItems;
            }
            else if (insertParent.HasScope)
            {
                // if, elseif, while. Try putting it in the condition body
                if (ShouldRejectDragAndDrop(new DragAndDropArgs(), insertParent))
                {
                    SetExpanded(insertParent.id, true);
                    insertParent = (CyanTriggerScopedTreeItem)insertParent.children[1];
                    Debug.Assert(GetData(insertParent.id).ActionInfo.Definition.FullName == CyanTriggerCustomNodeConditionBody.NodeDefinition.fullName);
                }
                dragPosition = DragAndDropPosition.UponItem;
            } 
            else 
            {
                insertParent = (CyanTriggerScopedTreeItem) insertNode.parent;
                insertIndex = insertParent.children.IndexOf(insertNode) + 1;
                dragPosition = DragAndDropPosition.BetweenItems;
            }

            MoveElements(itemsToMove, insertParent, dragPosition, insertIndex);
        }
        
        private List<SerializedProperty> AddNewAction(
            CyanTriggerActionInfoHolder actionInfoHolder, 
            bool includeDependencies = true)
        {
            // Do not allow adding of "local variable" actions.
            // Disable warning for use of Obsolete type CyanTriggerCustomNodeVariable
#pragma warning disable CS0618
            if (actionInfoHolder.Definition?.CustomDefinition is CyanTriggerCustomNodeVariable variableDefinition)
            {
                actionInfoHolder = CyanTriggerActionInfoHolder.GetActionInfoHolder("", 
                    CyanTriggerCustomNodeSetVariable.GetFullnameForType(variableDefinition.Type));
            }
#pragma warning restore CS0618
            
            int startIndex = Elements.arraySize;
            var newProperties = actionInfoHolder.AddActionToEndOfPropertyList(Elements, includeDependencies);
            OnActionChanged?.Invoke();

            for (int i = startIndex; i < Elements.arraySize; ++i)
            {
                int id = i + IdStartIndex;
                SetExpanded(id, true);

                if (includeDependencies)
                {
                    GetOrCreateExpandData(id).IsExpanded = true;
                }
            }

            return newProperties;
        }

        private SerializedProperty DuplicateAction(
            SerializedProperty actionProperty,
            Dictionary<string, string> variableGuidMap)
        {
            var actionInfo = CyanTriggerActionInfoHolder.GetActionInfoHolderFromProperties(actionProperty);
            var dupedPropertyList = AddNewAction(actionInfo, false);
            Debug.Assert(dupedPropertyList.Count == 1,
                $"Duplicating a property returned unexpected size! {dupedPropertyList.Count}");
            var dupedProperty = dupedPropertyList[0];
            
            CyanTriggerSerializedPropertyUtils.CopyDataAndRemapVariables(actionInfo, actionProperty, dupedProperty, variableGuidMap);

            return dupedProperty;
        }
        
        protected override void OnItemsRemoved(List<CyanTriggerScopedTreeItem> removedItems)
        {
            base.OnItemsRemoved(removedItems);
            
            HashSet<string> removedGuids = new HashSet<string>();
            foreach (var item in removedItems)
            {
                var data = GetData(item.id);
                if (data == null)
                {
                    continue;
                }

                var info = data.ActionInfo;
                if (info == null)
                {
                    continue;
                }

                var variables = info.GetCustomEditorVariableOptions(ItemElements[item.Index]);
                if (variables == null || variables.Length == 0)
                {
                    continue;
                }

                foreach (var variable in variables)
                {
                    removedGuids.Add(variable.ID);
                }
            }
            DeleteVariables(removedGuids, new HashSet<string>());
            
            _delayActionsChanged = true;
        }

        protected override void OnElementsRemapped(int[] mapping, int prevIdStart)
        {
            base.OnElementsRemapped(mapping, prevIdStart);
            _delayActionsChanged = true;
        }

        protected override void OnElementRemapped(CyanTriggerActionInstanceRenderData element, int prevIndex, int newIndex)
        {
            ClearInputList(element);
        }

        private void ClearInputList(CyanTriggerActionInstanceRenderData element)
        {
            element.ClearInputLists();
        }
    }
}
