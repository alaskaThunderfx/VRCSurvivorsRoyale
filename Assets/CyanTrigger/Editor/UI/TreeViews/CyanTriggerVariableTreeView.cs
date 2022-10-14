using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;
using VRC.Udon;

namespace Cyan.CT.Editor
{
    public abstract class CyanTriggerVariableTreeView : CyanTriggerScopedDataTreeView<CyanTriggerVariableTreeView.VariableExpandData>
    {
        public class VariableExpandData
        {
            public bool IsExpanded;
            public ReorderableList List;
            public float LastCommentHeight;

            // TODO cache properties

            public void ClearCache()
            {
                List = null;
            }
        }
        
        private const string CommentControlName = "Variable Editor Comment Control";
        
        private const float DefaultRowHeight = 20;
        private const float SpaceBetweenRowEditor = 6;
        private const float SpaceBetweenRowEditorSides = 6;
        private const float CellVerticalMargin = 3;
        private const float CellHorizontalMargin = 6;
        private const float ExpandButtonSize = 16;
        
        private bool _delayRefreshRowHeight = false;
        private bool _isModifyingExpand = false;

        protected readonly UdonBehaviour[] BackingUdons;
        protected readonly bool IsSceneTrigger;
        protected bool IsPlaying;
        protected int MouseOverId = -1;
        
        private int _editingCommentId = -1;
        private bool _focusedCommentEditor = false;
        private float _lastRectWidth = -1;
        private int _shouldRenameIndex = -1;
        private EditorWindow _renameFocusedWindow;
        
        private static MultiColumnHeader CreateColumnHeader()
        {
            string[] columnHeaders = {"Name", "Type", "Value", "Sync"};
            MultiColumnHeaderState.Column[] columns = new MultiColumnHeaderState.Column[4];
            for (int cur = 0; cur < columns.Length; ++cur)
            {
                columns[cur] = new MultiColumnHeaderState.Column
                {
                    minWidth = 50f,
                    width = 100f + (cur == 0 ? 50 : 0), // Increase size of the name column
                    headerTextAlignment = TextAlignment.Center, 
                    canSort = false,
                    headerContent = new GUIContent(columnHeaders[cur]),
                    allowToggleVisibility = cur != 0,
                };
            }
            
            MultiColumnHeader multiColumnHeader = new MultiColumnHeader(new MultiColumnHeaderState(columns))
            {
                height = 18,
            };
            multiColumnHeader.ResizeToFit();
            
            return multiColumnHeader;
        }

        protected CyanTriggerVariableTreeView(
            SerializedProperty elements, 
            UdonBehaviour[] backingUdons,
            bool isSceneTrigger) 
            : base (elements, CreateColumnHeader())
        {
            showBorder = true;
            rowHeight = DefaultRowHeight;
            useScrollView = false;
            showAlternatingRowBackgrounds = false;

            BackingUdons = backingUdons;
            IsSceneTrigger = isSceneTrigger;

            // Proxy so that each element can draw their own, even if they don't currently have children.
            // This does remove the nice animation though. 
            // TODO fix the nice animation by persisting foldout state
            foldoutOverride = (position, expandedState, style) => expandedState;
        }
        
        protected override void InitializeTreeViewGuiReflection(object treeGUI)
        {
            // Set the selection gui style
            // ReSharper disable once PossibleNullReferenceException
            FieldInfo selectionStyleProperty = treeGUI.GetType().BaseType.GetField("m_SelectionStyle",
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            selectionStyleProperty?.SetValue(treeGUI, CyanTriggerEditorGUIUtil.SelectedStyle);
        }

        protected override void OnBuildRoot(CyanTriggerScopedTreeItem root)
        {
            // On rebuild, assume lists need to be recreated.
            foreach (var data in GetData())
            {
                data.Item1.ClearCache();
            }

            GetGroupExpand();
        }
        
        protected override int GetElementScopeDelta(SerializedProperty actionProperty, int index)
        {
            var varType = GetVariableTypeForIndex(index);
            if (varType == CyanTriggerVariableType.SectionStart)
            {
                return 1;
            }
            if (varType == CyanTriggerVariableType.SectionEnd)
            {
                return -1;
            }

            return 0;
        }

        private void GetGroupExpand()
        {
            List<int> expandedIds = new List<int>();
            for (int index = 0; index < Size; ++index)
            {
                if (GetVariableTypeForIndex(index) != CyanTriggerVariableType.SectionStart)
                {
                    continue;
                }
                
                int id = index + IdStartIndex;
                var dataProp = GetDataPropertyForIndex(index);
                if (dataProp != null)
                {
                    object data = CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataProp);
                    bool expand = data is bool dataBool && dataBool;
                    if (expand)
                    {
                        expandedIds.Add(id);
                    }
                }
            }
            
            _isModifyingExpand = true;
            SetExpanded(expandedIds);
            _isModifyingExpand = false;
        }

        private void SetGroupExpand()
        {
            for (int index = 0; index < Size; ++index)
            {
                if (GetVariableTypeForIndex(index) != CyanTriggerVariableType.SectionStart)
                {
                    continue;
                }
                
                int id = index + IdStartIndex;
                bool isExpanded = IsExpanded(id);
                var dataProp = GetDataPropertyForIndex(index);
                if (dataProp != null)
                {
                    CyanTriggerSerializableObject.UpdateSerializedProperty(dataProp, isExpanded);
                }
            }
            
            Elements.serializedObject.ApplyModifiedProperties();
        }
        
        protected override void ExpandedStateChanged()
        {
            if (_isModifyingExpand)
            {
                return;
            }
            SetGroupExpand();
        }

        private VariableExpandData GetOrCreateExpandData(int id)
        {
            var data = GetData(id);
            if (data == null)
            {
                data = new VariableExpandData();
                SetData(id, data);
            }
            
            // TODO update cache?

            return data;
        }
        
        protected virtual void OnUndoOrSizeChanged() {}
        protected virtual void BeforeTreeLayout() {}
        protected virtual void DrawFooter() {}
        
        protected abstract string GetNameForIndex(int index);
        protected abstract Type GetTypeForIndex(int index);
        protected abstract CyanTriggerVariableType GetVariableTypeForIndex(int index);
        protected abstract SerializedProperty GetDisplayNameProperty(int index);
        protected abstract object GetDataForIndex(int index);
        protected abstract SerializedProperty GetDataPropertyForIndex(int index);
        protected abstract SerializedProperty GetSyncModePropertyForIndex(int index);
        protected abstract CyanTriggerVariableSyncMode GetSyncModeForIndex(int index);
        protected abstract string GetCommentForIndex(int index);
        protected abstract SerializedProperty GetCommentPropertyForIndex(int index);
        
        
        protected override void BeforeRowsGUI()
        {
            // Ensure selection style is initialized as it may not be after updating ui settings.
            var selectionStyle = CyanTriggerEditorGUIUtil.SelectedStyle;
            
            if (Event.current.rawType == EventType.Repaint)
            {
                // Draw background color from style
                float height = treeViewRect.height + state.scrollPos.y;
                CyanTriggerEditorGUIUtil.BackgroundColorStyle.Draw(new Rect(0, 0, 100000f, height), false, false, false, false);
            }
        }
        
        public void DoLayoutTree()
        {
            IsPlaying = EditorApplication.isPlaying;
            MouseOverId = -1;
            
            bool isUndo = (Event.current.commandName == "UndoRedoPerformed");
            
            if (Size != Elements.arraySize || isUndo)
            {
                OnUndoOrSizeChanged();
                Reload();
            }

            // Variable has a comment being edited. Check if we it needs to be closed.
            if (_editingCommentId != -1)
            {
                // Started editing a comment, refresh row heights
                if (!_focusedCommentEditor)
                {
                    DelayRefreshRowHeight();
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
                    DelayRefreshRowHeight();

                    if (enterPressed)
                    {
                        cur.Use();
                    }
                }
            }
            
            // Remove selection when treeview is not focused.
            if (!HasFocus() && HasSelection())
            {
                SetSelection(new List<int>(), TreeViewSelectionOptions.FireSelectionChanged);
            }
            
            if (_shouldRenameIndex != -1)
            {
                // When using a search window to add types, the current inspector window is no longer focused,
                // and the TreeView will end renaming in OnGUI without setting focus first.
                if (_renameFocusedWindow)
                {
                    _renameFocusedWindow.Focus();
                    _renameFocusedWindow = null;
                }
                
                BeginRename(Items[_shouldRenameIndex]);
                _shouldRenameIndex = -1;
            }
            
            BeforeTreeLayout();

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
                    DelayRefreshRowHeight();
                }
                _lastRectWidth = treeRect.width;
            }
            
            // Only allow recalculating height during the layout event and not an other.
            if (_delayRefreshRowHeight && Event.current?.type == EventType.Layout)
            {
                _delayRefreshRowHeight = false;
                RefreshCustomRowHeights();
                Repaint();
            }
            
            treeRect.height = totalHeight + (VisualSize == 0 ? DefaultRowHeight : 0);
            treeRect.y += 1; // Move down to allow for border
            GUILayout.Space(treeRect.height + 1);
            
            DrawFooter();

            OnGUI(treeRect);
            
            EditorGUILayout.EndVertical();
        }

        protected void DelayRefreshRowHeight()
        {
            _delayRefreshRowHeight = true;
        }

        protected override float GetCustomRowHeight(int row, TreeViewItem item)
        {
            VariableExpandData expandData = GetOrCreateExpandData(item.id);
            var scopedItem = (CyanTriggerScopedTreeItem) item;
            float foldoutIndent = GetFoldoutIndent(item);
            
            float commentHeight = GetCommentHeight(scopedItem.id, GetCommentForIndex(scopedItem.Index), foldoutIndent);
            // Cache the comment height to prevent it from changing in other Repaint paths.
            expandData.LastCommentHeight = commentHeight;
            
            float height = DefaultRowHeight
                           + CellVerticalMargin
                           + commentHeight;

            Type type = GetTypeForIndex(scopedItem.Index);
            if (type == null || typeof(ICyanTriggerCustomTypeNoValueEditor).IsAssignableFrom(type))
            {
                return height;
            }
            
            ICyanTriggerCustomType customType = null;
            if (typeof(ICyanTriggerCustomType).IsAssignableFrom(type))
            {
                object dataObj = GetDataForIndex(scopedItem.Index);
                if (dataObj is ICyanTriggerCustomType iCustomType)
                {
                    customType = iCustomType;
                    type = customType.GetBaseType();
                }
            }

            if (type == null || CyanTriggerPropertyEditor.TypeHasSingleLineEditor(type))
            {
                return height;
            }

            if (!expandData.IsExpanded)
            {
                return height;
            }

            if (customType != null)
            {
                // TODO handle this better. The type cannot be instantiated, so it must return in this path. 
                return height;
            }
            
            // Calculate multi line height for the property
            SerializedProperty dataProperty = GetDataPropertyForIndex(scopedItem.Index);
            var data = CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataProperty);
            
            // Initialize type if missing
            bool dirty = false;
            object updatedValue = CyanTriggerPropertyEditor.CreateInitialValueForType(type, data, ref dirty);
            if (dirty)
            {
                data = updatedValue;
                CyanTriggerSerializableObject.UpdateSerializedProperty(dataProperty, data);
            }
            
            height = height + SpaceBetweenRowEditor * 2 + 
                   CyanTriggerPropertyEditor.HeightForEditor(type, data, true, ref expandData.List);

            return height;
        }

        private float GetCommentHeight(int id, string comment, float indent)
        {
            if (string.IsNullOrEmpty(comment) && _editingCommentId != id)
            {
                return 0;
            }

            float width = _lastRectWidth - CellHorizontalMargin * 4 - ExpandButtonSize - 2 - indent;
            // Subtract 0.5 only in layout due to unity bug where CalcHeight method returns different values in Layout vs Repaint.
            if (Event.current?.type == EventType.Layout)
            {
                width -= 0.5f;
            }

            // Colorizing to prevent rich text from being evaluated.
            string escapedComment = $"// {comment}".Colorize(Color.black, true);
            float height = CyanTriggerEditorGUIUtil.CommentStyle.CalcHeight(new GUIContent(escapedComment), width);

            return height + CellVerticalMargin;
        }
        
        protected override bool ShowRightClickMenu()
        {
            return true;
        }
        
        protected override bool CanHandleKeyEvents()
        {
            return false;
        }

        protected override bool CanItemBeMoved(CyanTriggerScopedTreeItem item)
        {
            return false;
        }
        
        protected override bool CanItemBeRemoved(CyanTriggerScopedTreeItem item)
        {
            return false;
        }
        
        protected override bool CanDuplicate(IEnumerable<int> items)
        {
            return false;
        }

        protected override void DoubleClickedItem(int id)
        {
            var data = GetData(id);
            if (data == null)
            {
                return;
            }

            data.IsExpanded = !data.IsExpanded;
            DelayRefreshRowHeight();
        }
        
        protected override void OnRowGUI(RowGUIArgs args)
        {
            var item = (CyanTriggerScopedTreeItem) args.item;
            
            Rect rowRect = args.rowRect;
            Event current = Event.current;
            if (rowRect.Contains(current.mousePosition))
            {
                MouseOverId = item.id;
            }

            DisplayBorder(args, rowRect);

            Rect commentRect = new Rect(rowRect);
            float commentHeight = DisplayVariableComment(item, commentRect);
            
            if (item.HasScope)
            {
                if (args.GetNumVisibleColumns() > 0 && args.GetColumn(0) == 0)
                {
                    Rect cellRect = args.GetCellRect(0);
                    cellRect.yMin += commentHeight;
                    DisplayFolder(cellRect, item);
                }
                return;
            }

            // Only draw variable fields when not renaming the variable
            if (!args.isRenaming)
            {
                Type type = GetTypeForIndex(item.Index);
                for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
                {
                    Rect cellRect = args.GetCellRect(i);
                    cellRect.yMin += commentHeight;
                    cellRect.height = DefaultRowHeight;
                    CellGUI(cellRect, args.GetColumn(i), item, type);
                }
            }

            Rect editorRect = new Rect(rowRect);
            editorRect.y += DefaultRowHeight + SpaceBetweenRowEditor + commentHeight;
            editorRect.height -= DefaultRowHeight + SpaceBetweenRowEditor * 2 + commentHeight;
            editorRect.x += SpaceBetweenRowEditorSides;
            editorRect.width -= SpaceBetweenRowEditorSides * 2;
            DrawMultilineVariableEditor(item.Index, editorRect);
        }

        private void DisplayBorder(RowGUIArgs args, Rect rowRect)
        {
            // Draw border before anything else so it is behind everything.
            var boxStyle = new GUIStyle
            {
                border = new RectOffset(1, 1, 1, 1), 
                normal =
                {
                    background = CyanTriggerImageResources.ActionTreeOutlineTop
                }
            };

            // This is the last visible row, draw the bottom border so that it is obvious it ends here.
            GetFirstAndLastVisibleRows(out _, out var last);
            if (last == args.row)
            {
                GUI.Box(new Rect(rowRect.x, rowRect.yMax-1, rowRect.width, 1), GUIContent.none, boxStyle);
            }

            rowRect.y -= 1;
            GUI.Box(rowRect, GUIContent.none, boxStyle);
        }
        
        private float DisplayVariableComment(CyanTriggerScopedTreeItem item, Rect rowRect)
        {
            float itemIndent = GetFoldoutIndent(item);
            string comment = GetCommentForIndex(item.Index);
            
            VariableExpandData expandData = GetOrCreateExpandData(item.id);
            float commentHeight = expandData.LastCommentHeight;
            
            Rect commentRect = new Rect(rowRect);
            commentRect.yMin += CellVerticalMargin;
            commentRect.height = commentHeight;
            commentRect.width -= CellHorizontalMargin * 2 + ExpandButtonSize;
            commentRect.xMin += itemIndent + CellHorizontalMargin * 2;

            EditorGUI.BeginDisabledGroup(IsPlaying);
            
            // Draw variable comment
            if (Event.current.rawType == EventType.Repaint)
            {
                if (commentHeight > 0)
                {
                    string commentDisplay = $"// {comment}".Colorize(CyanTriggerColorTheme.Comment, true);
                    CyanTriggerEditorGUIUtil.CommentStyle.Draw(commentRect, commentDisplay, false, false, false, false);
                }
            }
            
            EditorGUI.EndDisabledGroup();

            // Draw comment editor
            if (_editingCommentId == item.id)
            {
                SerializedProperty commentProperty = GetCommentPropertyForIndex(item.Index);
                
                Event cur = Event.current;
                bool wasEscape = (cur.type == EventType.KeyDown && cur.keyCode == KeyCode.Escape);

                commentRect.y -= 2;
                commentRect.height += 4;
                GUI.SetNextControlName(CommentControlName);
                comment = EditorGUI.TextArea(commentRect, comment, EditorStyles.textArea);
                commentProperty.stringValue = comment;

                if (!_focusedCommentEditor)
                {
                    _focusedCommentEditor = true;
                    EditorGUI.FocusTextInControl(CommentControlName);
                }
                
                Rect completeButtonRect = new Rect(rowRect.xMax - CellHorizontalMargin - ExpandButtonSize, rowRect.y,
                    ExpandButtonSize, EditorGUIUtility.singleLineHeight);
                
                bool completeButton = GUI.Button(completeButtonRect, CyanTriggerEditorGUIUtil.CommentCompleteIcon, new GUIStyle());
                
                if (wasEscape || completeButton)
                {
                    if (comment.Length > 0 && comment.Trim().Length == 0)
                    {
                        commentProperty.stringValue = "";
                    }
                    
                    _editingCommentId = -1;
                    GUI.FocusControl(null);
                    DelayRefreshRowHeight();
                }
            }
            
            float newCommentHeight = GetCommentHeight(item.id, comment, itemIndent);
            // Check if program asset updates comment size and force height recalculate.
            if (!Mathf.Approximately(expandData.LastCommentHeight, newCommentHeight))
            {
                DelayRefreshRowHeight();
            }

            return commentHeight;
        }

        private void DisplayFolder(Rect cellRect, CyanTriggerScopedTreeItem item)
        {
            cellRect.height = DefaultRowHeight;
            
            int id = item.id;
            
            float foldoutIndent = GetFoldoutIndent(item);
            GUIStyle foldoutStyle = CyanTriggerEditorGUIUtil.FoldoutStyle;
            Rect foldoutRect = new Rect(cellRect.x + foldoutIndent, cellRect.y + 3, foldoutStyle.fixedWidth, foldoutStyle.lineHeight);
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
            
            CellGUI(cellRect, 0, item, null);
        }
        
        private void CellGUI(Rect cellRect, int column, CyanTriggerScopedTreeItem item, Type type)
        {
            CenterRectUsingSingleLineHeight(ref cellRect);
            // Disable if playing, and not drawing value property
            EditorGUI.BeginDisabledGroup(IsPlaying && (column != 2 || !HasAllValidUdon())); 
            
            // Block all property editors from overriding the right click menu
            Event current = Event.current;
            bool fakeIgnoreRightClick = 
                cellRect.Contains(current.mousePosition) 
                && current.type == EventType.ContextClick;
            
            if (fakeIgnoreRightClick)
            {
                Event.current.type = EventType.Used;
            }
            
            cellRect.y += 1;
            float labelYOffset = 2;
            
            switch (column)
            {
                case 0: // Name
                {
                    cellRect.y += labelYOffset;
                    float itemIndent = GetContentIndent(item);
                    cellRect.xMin += itemIndent - 1;
                    GUIContent content = new GUIContent(item.displayName, item.displayName);
                    CyanTriggerNameHelpers.TruncateContent(content, cellRect);
                    
                    SerializedProperty nameProperty = GetDisplayNameProperty(item.Index);
                    bool isHidden = false;
                    if (nameProperty != null)
                    {
                        isHidden = !IsSceneTrigger &&
                                   !CyanTriggerUtil.ShouldVariableDisplayInInspector(ItemElements[item.Index]);
                        EditorGUI.BeginProperty(cellRect, GUIContent.none, nameProperty);
                    }

                    EditorGUI.BeginDisabledGroup(isHidden);

                    content.text = content.text.Colorize(CyanTriggerColorTheme.VariableName);
                    EditorGUI.LabelField(cellRect, content, CyanTriggerEditorGUIUtil.TreeViewLabelStyle);
                    
                    EditorGUI.EndDisabledGroup();
                    
                    if (nameProperty != null)
                    {
                        EditorGUI.EndProperty();
                    }
                    
                    break;
                }
                case 1: // Type
                {
                    cellRect.y += labelYOffset;
                    string typeName = CyanTriggerNameHelpers.GetTypeFriendlyName(type);
                    ICyanTriggerCustomType customTypeData = null;
                    if (typeof(ICyanTriggerCustomType).IsAssignableFrom(type))
                    {
                        object dataObj = GetDataForIndex(item.Index);
                        if (dataObj is ICyanTriggerCustomType customType)
                        {
                            customTypeData = customType;
                            typeName = customType.GetTypeDisplayName();
                        }
                    }
                    
                    // Show documentation link over Type
                    Rect docRect = new Rect(cellRect.xMax - 16, cellRect.y, 16, cellRect.height);
                    bool showDocs = MouseOverId == item.id;
                    CyanTriggerEditorUtils.DrawDocumentationButtonForActionInfo(docRect, type, customTypeData, showDocs);

                    GUIContent content = new GUIContent(typeName, typeName);
                    CyanTriggerNameHelpers.TruncateContent(content, cellRect);
                    content.text = content.text.Colorize(CyanTriggerColorTheme.VariableIndicator);
                    EditorGUI.LabelField(cellRect, content, CyanTriggerEditorGUIUtil.TreeViewLabelStyle);
                    break;
                }
                case 2: // Value
                {
                    if (typeof(ICyanTriggerCustomType).IsAssignableFrom(type) 
                        && !typeof(ICyanTriggerCustomTypeNoValueEditor).IsAssignableFrom(type))
                    {
                        object dataObj = GetDataForIndex(item.Index);
                        if (dataObj is ICyanTriggerCustomType customType)
                        {
                            type = customType.GetBaseType();
                        }
                    }
                    
                    DrawSingleLineVariableEditor(cellRect, item, type);
                    break;
                }
                case 3: // Sync
                {
                    SerializedProperty syncProperty = GetSyncModePropertyForIndex(item.Index);
                   
                    bool canSync = UdonNetworkTypes.CanSync(type);
                    EditorGUI.BeginDisabledGroup(syncProperty == null || !canSync || IsPlaying);

                    int selected = 0;
                    CyanTriggerVariableSyncMode cur = syncProperty == null 
                        ? GetSyncModeForIndex(item.Index)
                        : (CyanTriggerVariableSyncMode)syncProperty.intValue;
                
                    List<CyanTriggerVariableSyncMode> syncOptions = new List<CyanTriggerVariableSyncMode>();
                    syncOptions.Add(CyanTriggerVariableSyncMode.NotSynced);
                    if (canSync)
                    {
                        syncOptions.Add(CyanTriggerVariableSyncMode.Synced);
                    }
                    if (UdonNetworkTypes.CanSyncLinear(type))
                    {
                        syncOptions.Add(CyanTriggerVariableSyncMode.SyncedLinear);
                    }
                    if (UdonNetworkTypes.CanSyncSmooth(type))
                    {
                        syncOptions.Add(CyanTriggerVariableSyncMode.SyncedSmooth);
                    }

                    string[] options = new string[syncOptions.Count];
                    for (int option = 0; option < options.Length; ++option)
                    {
                        options[option] = syncOptions[option].ToString();
                        if (cur == syncOptions[option])
                        {
                            selected = option;
                        }
                    }

                    if (syncProperty != null)
                    {
                        EditorGUI.BeginProperty(cellRect, GUIContent.none, syncProperty);
                    }
                    
                    int newSelected = EditorGUI.Popup(cellRect, selected, options);
                    
                    if (syncProperty != null)
                    {
                        EditorGUI.EndProperty();
                        if (newSelected != selected)
                        {
                            syncProperty.intValue = (int) syncOptions[newSelected];
                        }
                    }
                    EditorGUI.EndDisabledGroup();

                    break;
                }
            }
            
            if (fakeIgnoreRightClick)
            {
                Event.current.type = EventType.ContextClick;
            }
            
            
            EditorGUI.EndDisabledGroup();
        }

        
        private void DrawSingleLineVariableEditor(Rect cellRect, CyanTriggerScopedTreeItem item, Type type)
        {
            if (typeof(ICyanTriggerCustomTypeNoValueEditor).IsAssignableFrom(type))
            {
                // Do nothing as there is no editor?
                return;
            }
            
            if (Event.current.type == EventType.Repaint)
            {
                Rect largerRect = new Rect(cellRect.x-2, cellRect.y-1.5f, cellRect.width+4, cellRect.height+3);
                Rect smallerRect = new Rect(cellRect.x-1, cellRect.y-1, cellRect.width+2, cellRect.height+2);
                
                // Draw background to overwrite the selection blue
                CyanTriggerEditorGUIUtil.BackgroundColorStyle.Draw(smallerRect, false, false, false, false);
            
                // Draw the rounded rectangle to contain all inputs
                // Mainly used to help distinguish certain inspectors, like bool toggle
                CyanTriggerEditorGUIUtil.HelpBoxStyle.Draw(largerRect, false, false, false, false); 
            }
            cellRect = new Rect(cellRect.x+1, cellRect.y+1, cellRect.width-2, cellRect.height-2);
            
            
            if (!CyanTriggerPropertyEditor.TypeHasSingleLineEditor(type))
            {
                VariableExpandData expandData = GetOrCreateExpandData(item.id);
                if (GUI.Button(cellRect, new GUIContent(expandData.IsExpanded ? "Hide" : "Edit")))
                {
                    expandData.IsExpanded = !expandData.IsExpanded;
                    DelayRefreshRowHeight();
                }
            }
            else
            {
                string varName = GetNameForIndex(item.Index);
                if (ShouldDrawEditorInPlaymode(varName))
                {
                    DrawEditorInPlaymode(varName, type, cellRect);
                }
                else
                {   
                    bool disableUnityObjects = !IsSceneTrigger && typeof(UnityEngine.Object).IsAssignableFrom(type);
                    EditorGUI.BeginDisabledGroup(IsPlaying || disableUnityObjects);
                            
                    SerializedProperty dataProperty = GetDataPropertyForIndex(item.Index);
                    CyanTriggerPropertyEditor.DrawEditor(dataProperty, cellRect, GUIContent.none, type, false);
                            
                    EditorGUI.EndDisabledGroup();
                }
            }
        }
        
        private void DrawMultilineVariableEditor(int index, Rect rect)
        {
            Type type = GetTypeForIndex(index);
            ICyanTriggerCustomType customType = null;
            if (typeof(ICyanTriggerCustomType).IsAssignableFrom(type))
            {
                object dataObj = GetDataForIndex(index);
                if (dataObj is ICyanTriggerCustomType iCustomType)
                {
                    customType = iCustomType;
                    type = customType.GetBaseType();
                }
            }

            int id = Items[index].id;
            VariableExpandData expandData = GetOrCreateExpandData(id);
            
            if (!CyanTriggerPropertyEditor.TypeHasSingleLineEditor(type) && expandData.IsExpanded)
            {
                string variableName =  GetNameForIndex(index);
                SerializedProperty dataProperty = GetDataPropertyForIndex(index);
                
                if (type.IsArray)
                {
                    int size = expandData.List == null ? 0 : expandData.List.count;
                    bool showArray = expandData.IsExpanded;

                    string display = $"{CyanTriggerNameHelpers.GetTypeFriendlyName(type)} {variableName}";

                    if (ShouldDrawEditorInPlaymode(variableName))
                    {
                        DrawArrayEditorInPlaymode(variableName, type, rect, new GUIContent(display), ref expandData.IsExpanded, ref expandData.List);
                    }
                    else
                    {
                        bool disableUnityObjects = !IsSceneTrigger && typeof(UnityEngine.Object).IsAssignableFrom(type.GetElementType());
                        EditorGUI.BeginDisabledGroup(IsPlaying || disableUnityObjects);
                        
                        CyanTriggerPropertyEditor.DrawArrayEditor(
                            dataProperty, 
                            new GUIContent(display),
                            type,
                            ref expandData.IsExpanded,
                            ref expandData.List, 
                            false,
                            rect);
                        
                        EditorGUI.EndDisabledGroup();
                    }

                    int newSize = expandData.List == null ? 0 : expandData.List.count;
                    if (size != newSize || showArray != expandData.IsExpanded)
                    {
                        DelayRefreshRowHeight();
                    }
                }
                else if (customType != null)
                {
                    // TODO display inputs for customType
                }
                else
                {
                    if (ShouldDrawEditorInPlaymode(variableName))
                    {
                        DrawEditorInPlaymode(variableName, type, rect);
                    }
                    else
                    {
                        EditorGUI.BeginDisabledGroup(IsPlaying);

                        CyanTriggerPropertyEditor.DrawEditor(dataProperty, rect, GUIContent.none, type, false);
                        
                        EditorGUI.EndDisabledGroup();
                    }
                }
            }
        }

        private bool HasAllValidUdon()
        {
            if (BackingUdons == null || BackingUdons.Length == 0)
            {
                return false;
            }

            for (int index = 0; index < BackingUdons.Length; ++index)
            {
                if (BackingUdons[index] == null)
                {
                    return false;
                }
            }

            return true;
        }

        private bool AllUdonHasVariable(string variableName)
        {
            if (BackingUdons == null || BackingUdons.Length == 0)
            {
                return false;
            }

            foreach (var udon in BackingUdons)
            {
                if (udon == null 
                    // Showing a prefab asset not in the scene
                    || PrefabUtility.IsPartOfPrefabAsset(udon)
                    // Showing a prefab in assets in playmode will not give valid udon
                    || PrefabStageUtility.GetPrefabStage(udon.gameObject) != null
                    // Doesn't have the variable.
                    || udon.GetProgramVariableType(variableName) == null)
                {
                    return false;
                }
            }

            return true;
        }
        
        private bool ShouldDrawEditorInPlaymode(string variableName)
        {
            return IsPlaying && IsSceneTrigger && AllUdonHasVariable(variableName);
        }

        private object GetUdonValueForVariable(string variableName, ref bool valuesDiffer)
        {
            object value = BackingUdons[0].GetProgramVariable(variableName);
            for (int index = 1; index < BackingUdons.Length; ++index)
            {
                object nValue = BackingUdons[index].GetProgramVariable(variableName);
                if (!((value == null && nValue == null) || (value != null && value.Equals(nValue))))
                {
                    valuesDiffer = true;
                    return value;
                }
            }

            return value;
        }

        private void SetUdonValueForVariable(string variableName, object value)
        {
            for (int index = 0; index < BackingUdons.Length; ++index)
            {
                BackingUdons[index].SetProgramVariable(variableName, value);
            }
        }
        
        private void DrawEditorInPlaymode(string variableName, Type type, Rect rect)
        {
            bool valuesDiffer = false;
            object obj = GetUdonValueForVariable(variableName, ref valuesDiffer);
            
            bool multi = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = valuesDiffer;
            
            bool dirty = false;
            obj = CyanTriggerPropertyEditor.DisplayPropertyEditor(rect, GUIContent.none, type, obj, ref dirty, false);
            
            if (dirty)
            {
                SetUdonValueForVariable(variableName, obj);
            }

            EditorGUI.showMixedValue = multi;
        }

        private void DrawArrayEditorInPlaymode(string variableName, Type type, Rect rect, GUIContent displayText, ref bool arrayExpand, ref ReorderableList list)
        {
            bool valuesDiffer = false;
            object obj = GetUdonValueForVariable(variableName, ref valuesDiffer);
            
            bool multi = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = valuesDiffer;
            
            bool dirty = false;
            obj = CyanTriggerPropertyEditor.DisplayArrayPropertyEditor(displayText, type, obj, ref dirty, ref arrayExpand, ref list, false, rect);
            if (dirty)
            {
                SetUdonValueForVariable(variableName, obj);     
            }

            EditorGUI.showMixedValue = multi;
        }
        
        protected override void HandleKeyEvent()
        {
            Event cur = Event.current;
            // Detect if we should open the comment editor
            bool enterPressed = cur.type == EventType.KeyDown &&
                                cur.keyCode == KeyCode.Return &&
                                (cur.shift || cur.alt || cur.command || cur.control);
            
            // Start commenting on currently selected variable.
            if (_editingCommentId == -1 && HasSelection() && enterPressed)
            {
                int shouldCommentId = -1;
                foreach (var selected in GetSelection())
                {
                    int index = GetItemIndex(selected);
                    if (index >= 0 && index < Items.Length)
                    {
                        shouldCommentId = selected;
                        break;
                    }
                }

                if (shouldCommentId != -1 && StartComment(shouldCommentId))
                {
                    cur.Use();
                }
            }
        }

        protected bool StartComment(int id)
        {
            if (GetCommentPropertyForIndex(GetItemIndex(id)) == null)
            {
                return false;
            }
            
            _editingCommentId = id;
            _focusedCommentEditor = false;
            DelayRefreshRowHeight();
            SetSelection(new List<int>(), TreeViewSelectionOptions.FireSelectionChanged);
            return true;
        }

        protected void SetRenameIndex(int index)
        {
            _shouldRenameIndex = index;
        }

        protected void SaveFocusedWindow()
        {
            _renameFocusedWindow = EditorWindow.mouseOverWindow;
        }
    }
}
