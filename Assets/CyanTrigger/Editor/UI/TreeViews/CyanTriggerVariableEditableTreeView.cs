using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Compiler.Compilers;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerVariableEditableTreeView : CyanTriggerVariableTreeView
    {
        private readonly Action<List<string>> _onVariableAddedOrRemoved;
        private readonly Action<string, string, string> _onVariableRenamed;
        private readonly Func<string, string, string> _getUniqueVariableName;

        private bool _shouldVerifyVariables;
        private readonly List<int> _shouldApplyRename = new List<int>();

        public CyanTriggerVariableEditableTreeView(
            SerializedProperty elements, 
            UdonBehaviour backingUdon,
            bool isSceneTrigger,
            Action<List<string>> onVariableAddedOrRemoved,
            Func<string, string, string> getUniqueVariableName,
            Action<string, string, string> onVariableRenamed) 
            : base (elements, new []{backingUdon}, isSceneTrigger)
        {
            _onVariableAddedOrRemoved = onVariableAddedOrRemoved;
            _getUniqueVariableName = getUniqueVariableName;
            _onVariableRenamed = onVariableRenamed;

            DelayReload();
        }

        protected override string GetNameForIndex(int index)
        {
            return GetElementDisplayName(ItemElements[index], index);
        }
        
        protected override Type GetTypeForIndex(int index)
        {
            SerializedProperty variableProperty = ItemElements[index];
            SerializedProperty typeProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.type));
            SerializedProperty typeDefProperty =
                typeProperty.FindPropertyRelative(nameof(CyanTriggerSerializableType.typeDef));
            return Type.GetType(typeDefProperty.stringValue);
        }
        
        protected override CyanTriggerVariableType GetVariableTypeForIndex(int index)
        {
            SerializedProperty typeProperty = GetVariableTypeInfoProperty(index);
            return (CyanTriggerVariableType)typeProperty.enumValueIndex;
        }

        protected override SerializedProperty GetDataPropertyForIndex(int index)
        {
            SerializedProperty variableProperty = ItemElements[index];
            return variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.data));
        }
        
        protected override object GetDataForIndex(int index)
        {
            SerializedProperty dataProperty = GetDataPropertyForIndex(index);
            return CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataProperty);
        }

        protected override SerializedProperty GetSyncModePropertyForIndex(int index)
        {
            SerializedProperty variableProperty = ItemElements[index];
            return variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.sync));
        }
        
        protected override CyanTriggerVariableSyncMode GetSyncModeForIndex(int index)
        {
            return (CyanTriggerVariableSyncMode)GetSyncModePropertyForIndex(index).intValue;
        }

        protected override SerializedProperty GetDisplayNameProperty(int index)
        {
            SerializedProperty variableProperty = ItemElements[index];
            return variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.name));
        }
        
        protected override string GetCommentForIndex(int index)
        {
            return GetCommentPropertyForIndex(index).stringValue;
        }
        
        protected override SerializedProperty GetCommentPropertyForIndex(int index)
        {
            SerializedProperty variableProperty = ItemElements[index];
            var commentProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.comment));
            return commentProperty.FindPropertyRelative(nameof(CyanTriggerComment.comment));
        }
        
        private SerializedProperty GetVariableTypeInfoProperty(int index)
        {
            SerializedProperty variableProperty = ItemElements[index];
            return variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.typeInfo));
        }
        
        private SerializedProperty GetVariableShowInInspectorProperty(int index)
        {
            SerializedProperty variableProperty = ItemElements[index];
            return variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.showInInspector));
        }
        
        private SerializedProperty GetGuidProperty(int index)
        {
            SerializedProperty variableProperty = ItemElements[index];
            return variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.variableID));
        }

        protected override void OnBuildRoot(CyanTriggerScopedTreeItem root)
        {
            base.OnBuildRoot(root);
            
            _shouldVerifyVariables = true;
        }
        
        protected override string GetElementDisplayName(SerializedProperty property, int index)
        {
            return property.FindPropertyRelative(nameof(CyanTriggerVariable.name)).stringValue;
        }

        protected override bool CanRename(TreeViewItem item)
        {
            // Can't rename while in playmode
            return !IsPlaying;
        }

        protected override void RenameEnded(RenameEndedArgs args)
        {
            if (!args.acceptedRename || args.newName.Equals(args.originalName))
            {
                return;
            }
            
            int index = GetItemIndex(args.itemID);
            var variableProperty = ItemElements[index];

            string newName = args.newName;
            var guid = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.variableID)).stringValue;
            var varItem = Items[index];
            
            // Only check items that don't have scope.
            if (!varItem.HasScope)
            {
                newName = _getUniqueVariableName(newName, guid);
                if (newName.Equals(args.originalName))
                {
                    return;
                }
            }

            variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.name)).stringValue = newName;
            varItem.displayName = newName;

            // Don't try to search all variables when a scoped item updates it's name.
            if (varItem.HasScope)
            {
                _onVariableRenamed?.Invoke("", "", "");
                return;
            }

            _onVariableRenamed?.Invoke(args.originalName, newName, guid);
        }
        
        protected override bool ShowRightClickMenu()
        {
            // TODO 
            return !IsPlaying;
        }
        
        protected override void GetRightClickMenuOptions(GenericMenu menu, Event currentEvent)
        {
            // Prevent right click options while in playmode.
            if (IsPlaying)
            {
                // TODO revert value back to default?
                return;
            }

            var selection = GetSelection();
            GetRevertPrefabRightClickMenuOptions(menu, selection);
            
            base.GetRightClickMenuOptions(menu, currentEvent);
            
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Add Group"), false, AddFolder);
            menu.AddItem(new GUIContent("Add Variable"), false, () =>
            {
                CyanTriggerSearchWindowManager.Instance.DisplayVariableSearchWindow(
                    GUIUtility.GUIToScreenPoint(currentEvent.mousePosition), AddNewVariable, AddNewVariable);
            });
            menu.AddItem(new GUIContent("Add Favorite Variable"), false, () =>
            {
                CyanTriggerSearchWindowManager.Instance.DisplayVariableFavoritesSearchWindow(
                    GUIUtility.GUIToScreenPoint(currentEvent.mousePosition), AddNewVariable);
            });
            
            if (MouseOverId != -1)
            {
                menu.AddSeparator("");
                GUIContent commentContent = new GUIContent(string.IsNullOrEmpty(GetCommentForIndex(MouseOverId - IdStartIndex))
                    ? "Add Comment"
                    : "Edit Comment");
                menu.AddItem(commentContent, false, () =>
                {
                    StartComment(MouseOverId);
                });
            }

            if (IsSceneTrigger)
            {
                return;
            }
            
            // Only show the Show/Hide settings for asset versions
            int publicCount = 0;
            int privateCount = 0;
            foreach (var id in selection)
            {
                int index = GetItemIndex(id);
                
                SerializedProperty variableProperty = ItemElements[index];
                SerializedProperty nameProp = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.name));
                if (nameProp.stringValue.StartsWith(UdonGraphCompiler.INTERNAL_VARIABLE_PREFIX))
                {
                    continue;
                }
                
                SerializedProperty typeInfoProp = 
                    variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.typeInfo));
                CyanTriggerVariableType varType = (CyanTriggerVariableType)typeInfoProp.enumValueIndex;

                if (varType != CyanTriggerVariableType.Variable && varType != CyanTriggerVariableType.SectionStart)
                {
                    continue;
                }
                
                SerializedProperty showProp = 
                    variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.showInInspector));
                
                if (showProp.boolValue)
                {
                    ++publicCount;
                }
                else
                {
                    ++privateCount;
                }
            }
            
            if (publicCount + privateCount > 0) 
            {
                menu.AddSeparator("");

                void SetVariableShowInInspector(bool showInInspector)
                {
                    foreach (var id in selection)
                    {
                        int index = GetItemIndex(id);
                        
                        SerializedProperty variableProperty = ItemElements[index];
                        SerializedProperty nameProp = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.name));
                        if (nameProp.stringValue.StartsWith(UdonGraphCompiler.INTERNAL_VARIABLE_PREFIX))
                        {
                            continue;
                        }
                        
                        SerializedProperty showProp = 
                            variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.showInInspector));
                        showProp.boolValue = showInInspector;
                        
                        _onVariableAddedOrRemoved?.Invoke(null);
                    }
                }
                
                if (publicCount > 0)
                {
                    menu.AddItem(new GUIContent($"Hide Variable{(publicCount > 1? "s" : "")}"), false, () =>
                    {
                        SetVariableShowInInspector(false);
                    });
                }
                if (privateCount > 0)
                {
                    menu.AddItem(new GUIContent($"Show Variable{(privateCount > 1? "s" : "")}"), false, () =>
                    {
                        SetVariableShowInInspector(true);
                    });
                }
            }
        }
        
        private void GetRevertPrefabRightClickMenuOptions(GenericMenu menu, IList<int> selection)
        {
            if (IsPlaying || !Elements.isInstantiatedPrefab)
            {
                return;
            }

            bool anyNameChanges = false;
            bool anyValueChanges = false;
            bool anySyncChanges = false;
            bool anyGuidChanges = false;
            bool anyVisibilityChanges = false;
            foreach (var id in selection)
            {
                int index = GetItemIndex(id);
                
                SerializedProperty variableProperty = ItemElements[index];
                if (variableProperty == null)
                {
                    continue;
                }
                
                SerializedProperty guidProp = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.variableID));
                SerializedProperty nameProp = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.name));
                SerializedProperty showProp = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.showInInspector));

                anyNameChanges |= nameProp.prefabOverride;
                anyGuidChanges |= guidProp.prefabOverride;
                anyVisibilityChanges |= showProp.prefabOverride;
                
                if (Items[index].HasScope)
                {
                    continue;
                }
                
                SerializedProperty syncProp = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.sync));
                SerializedProperty dataProp = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.data));
                
                anyValueChanges |= dataProp.prefabOverride;
                anySyncChanges |= syncProp.prefabOverride;
            }

            if (!anyNameChanges && !anyValueChanges && !anySyncChanges && !anyGuidChanges && !anyVisibilityChanges)
            {
                return;
            }

            void ApplyPrefabProperty(Func<int, SerializedProperty> getPropertyForIndex)
            {
                string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                    PrefabUtility.GetOutermostPrefabInstanceRoot(Elements.serializedObject.targetObject));
                
                IList<int> curSelection = GetSelection();
                foreach (var id in curSelection)
                {
                    int index = GetItemIndex(id);
                    SerializedProperty property = getPropertyForIndex(index);
                    if (property == null || !property.prefabOverride)
                    {
                        continue;
                    }
                    
                    PrefabUtility.ApplyPropertyOverride(property, assetPath, InteractionMode.UserAction);
                    
                    var data = GetData(id);
                    if (data != null)
                    {
                        data.List = null;
                    }
                }

                DelayRefreshRowHeight();
                _onVariableAddedOrRemoved?.Invoke(null);
            }

            void RevertPrefabProperty(Func<int, SerializedProperty> getPropertyForIndex)
            {
                IList<int> curSelection = GetSelection();
                foreach (var id in curSelection)
                {
                    int index = GetItemIndex(id);
                    SerializedProperty property = getPropertyForIndex(index);
                    if (property == null)
                    {
                        continue;
                    }

                    var dataProperty = property.Copy();
                    dataProperty.prefabOverride = false;
                    foreach (var propObj in dataProperty)
                    {
                        var prop = propObj as SerializedProperty;
                        prop.prefabOverride = false;
                    }
                    
                    var data = GetData(id);
                    if (data != null)
                    {
                        data.List = null;
                    }
                }

                Elements.serializedObject.ApplyModifiedProperties();
                DelayRefreshRowHeight();
                _onVariableAddedOrRemoved?.Invoke(null);
                DelayReload();
            }

            // If the guid has changed, then assume the entire variable is off and needs to be applied or reverted.
            if (anyGuidChanges)
            {
                menu.AddItem(new GUIContent("Apply All Variable Prefab Changes"), false, () =>
                {
                    ApplyPrefabProperty(index => ItemElements[index]);
                });
            
                menu.AddItem(new GUIContent("Revert All Variable Prefab Changes"), false, () =>
                {
                    RevertPrefabProperty(index => ItemElements[index]);
                });
                
                menu.AddSeparator("");
                return;
            }

            if (anyNameChanges)
            {
                menu.AddItem(new GUIContent("Apply Name Prefab Changes"), false, () =>
                {
                    _shouldApplyRename.AddRange(GetSelection());
                    ApplyPrefabProperty(GetDisplayNameProperty);
                });
            
                menu.AddItem(new GUIContent("Revert Name Prefab Changes"), false, () =>
                {
                    _shouldApplyRename.AddRange(GetSelection());
                    RevertPrefabProperty(GetDisplayNameProperty);
                });
            }
            
            if (anyValueChanges)
            {
                menu.AddItem(new GUIContent("Apply Value Prefab Changes"), false, () =>
                {
                    ApplyPrefabProperty(GetDataPropertyForIndex);
                });
            
                menu.AddItem(new GUIContent("Revert Value Prefab Changes"), false, () =>
                {
                    RevertPrefabProperty(GetDataPropertyForIndex);
                });
            }
            
            if (anySyncChanges)
            {
                menu.AddItem(new GUIContent("Apply Sync Prefab Changes"), false, () =>
                {
                    ApplyPrefabProperty(GetSyncModePropertyForIndex);
                });
            
                menu.AddItem(new GUIContent("Revert Sync Prefab Changes"), false, () =>
                {
                    RevertPrefabProperty(GetSyncModePropertyForIndex);
                });
            }

            if (anyVisibilityChanges)
            {
                menu.AddItem(new GUIContent("Apply Show/Hide Prefab Changes"), false, () =>
                {
                    ApplyPrefabProperty(GetVariableShowInInspectorProperty);
                });
            
                menu.AddItem(new GUIContent("Revert Show/Hide Prefab Changes"), false, () =>
                {
                    RevertPrefabProperty(GetVariableShowInInspectorProperty);
                });
            }
            
            menu.AddSeparator("");
        }
        
        protected override bool CanHandleKeyEvents()
        {
            // Don't handle key events while in playmode
            return !IsPlaying;
        }

        protected override bool CanItemBeMoved(CyanTriggerScopedTreeItem item)
        {
            // Don't let items move while in playmode
            return !IsPlaying;
        }
        
        protected override bool CanItemBeRemoved(CyanTriggerScopedTreeItem item)
        {
            // Don't let items be removed while in playmode
            return !IsPlaying;
        }
        
        protected override bool CanDuplicate(IEnumerable<int> items)
        {
            // Can't duplicate while in playmode.
            return !IsPlaying;
        }

        protected override List<(int, int)> DuplicateItems(IEnumerable<int> items)
        {
            List<(int, int)> newIds = new List<(int, int)>();
            HashSet<int> duplicatedInd = new HashSet<int>();
            List<int> sortedItems = new List<int>(items);
            sortedItems.Sort();

            int idStart = IdStartIndex;

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
                    DuplicateVariable(ItemElements[i]);
                    newIds.Add((i + idStart, Elements.arraySize - 1 + idStart));
                    duplicatedInd.Add(i);
                }
            }

            return newIds;
        }
        
        protected override void OnItemsRemoved(List<CyanTriggerScopedTreeItem> removedItems)
        {
            base.OnItemsRemoved(removedItems);
            List<string> guids = new List<string>();
            foreach (var item in removedItems)
            {
                if (item.Index >= ItemElements.Length || ItemElements[item.Index] == null)
                {
                    continue;
                }

                var guidProp = ItemElements[item.Index].FindPropertyRelative(nameof(CyanTriggerVariable.variableID));
                guids.Add(guidProp.stringValue);
            }
            
            _onVariableAddedOrRemoved?.Invoke(guids);
        }

        protected override void OnUndoOrSizeChanged()
        {
            _onVariableAddedOrRemoved?.Invoke(null);
        }

        protected override void BeforeTreeLayout()
        {
            if (_shouldApplyRename.Count > 0)
            {
                foreach (var id in _shouldApplyRename)
                {
                    int index = GetItemIndex(id);
                    
                    SerializedProperty variableProperty = ItemElements[index];
                    if (variableProperty == null)
                    {
                        continue;
                    }
                    
                    SerializedProperty guidProp = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.variableID));
                    SerializedProperty nameProp = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.name));

                    _onVariableRenamed(null, nameProp.stringValue, guidProp.stringValue);
                }
                _shouldApplyRename.Clear();
            }

            if (_shouldVerifyVariables)
            {
                _shouldVerifyVariables = false;
                VerifyVariables();
            }
        }
        
        protected override void DrawFooter()
        {
            var listActionFooterIcons = new[]
            {
                EditorGUIUtility.TrIconContent("FolderOpened Icon", "Add Variable Group"),
                EditorGUIUtility.TrIconContent("Favorite", "Add Favorite Variable"),
                EditorGUIUtility.TrIconContent("Toolbar Plus", "Add Variable"),
                EditorGUIUtility.TrIconContent("TreeEditor.Duplicate", "Duplicate selected item"),
                EditorGUIUtility.TrIconContent("Toolbar Minus", "Remove selection from list")
            };
            
            bool hasSelection = HasSelection();
            
            CyanTriggerPropertyEditor.DrawButtonFooter(
                listActionFooterIcons, 
                new Action[]
                {
                    AddFolder,
                    AddNewVariableFromFavoriteList,
                    AddNewVariableFromAllList,
                    DuplicateSelectedItems,
                    RemoveSelected
                },
                IsPlaying 
                    ? new [] { true, true, true, true, true }
                    : new [] { false, false, false, !hasSelection, !hasSelection }
            );
        }

        private void VerifyVariables()
        {
            int size = Elements.arraySize;
            for (int index = 0; index < size; ++index)
            {
                SerializedProperty variableProperty = Elements.GetArrayElementAtIndex(index);
                SerializedProperty typeProperty =
                    variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.type));
                SerializedProperty typeDefProperty =
                    typeProperty.FindPropertyRelative(nameof(CyanTriggerSerializableType.typeDef));
                Type type = Type.GetType(typeDefProperty.stringValue);
                SerializedProperty dataProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.data));
                
                object obj = CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataProperty);
                bool dirty = false;
                obj = CyanTriggerPropertyEditor.CreateInitialValueForType(type, obj, ref dirty);

                if (dirty)
                {
                    if(type.IsArray && typeof(UnityEngine.Object).IsAssignableFrom(type.GetElementType()))
                    {
                        var array = (Array) obj;
                    
                        Array destinationArray = Array.CreateInstance(type.GetElementType(), array.Length);
                        Array.Copy(array, destinationArray, array.Length);
                
                        obj = destinationArray;
                    }
                
                    CyanTriggerSerializableObject.UpdateSerializedProperty(dataProperty, obj);
                }
            }
        }
        
        private void AddNewVariableFromAllList()
        {
            SaveFocusedWindow();
            CyanTriggerSearchWindowManager.Instance.DisplayVariableSearchWindow(AddNewVariable, AddNewVariable);
        }

        private void AddNewVariableFromFavoriteList()
        {
            SaveFocusedWindow();
            CyanTriggerSearchWindowManager.Instance.DisplayVariableFavoritesSearchWindow(AddNewVariable);
        }

        private void AddNewVariableFromCustomActionInstancesList()
        {
            SaveFocusedWindow();
            CyanTriggerSearchWindowManager.Instance.DisplayCustomActionSearchWindow(AddNewVariable);
        }

        private void AddFolder()
        {
            SaveFocusedWindow();
            AddNewVariable("Variable Group", typeof(object), CyanTriggerVariableType.SectionStart, true, null, false);
            AddNewVariable("Variable Group End", typeof(object), CyanTriggerVariableType.SectionEnd, false, null, false);

            SetRenameIndex(Elements.arraySize - 2);
        }
        
        private void AddNewVariable(UdonNodeDefinition def)
        {
            AddNewVariable(CyanTriggerNameHelpers.GetTypeFriendlyName(def.type), def.type, CyanTriggerVariableType.Variable, true);
        }

        private void AddNewVariable(CyanTriggerSettingsFavoriteItem favorite)
        {
            if (string.IsNullOrEmpty(favorite.data.directEvent))
            {
                Debug.LogWarning("Cannot create a new variable without a proper definition!");
                return;
            }

            var def = CyanTriggerNodeDefinitionManager.Instance.GetDefinition(favorite.data.directEvent);
            if (def == null)
            {
                Debug.LogWarning("Cannot create a new variable without a proper definition!");
                return;
            }

            AddNewVariable(def.TypeFriendlyName, def.BaseType, CyanTriggerVariableType.Variable, true);
        }

        private void AddNewVariable(CyanTriggerActionGroupDefinition actionGroupDefinition)
        {
            var value = new CyanTriggerCustomTypeCustomAction(actionGroupDefinition);
            AddNewVariable(value.ActionGroup.GetNamespace(), value.GetType(), CyanTriggerVariableType.Variable, true, value, true);
        }

        private void AddNewVariable(
            string variableName, 
            Type type, 
            CyanTriggerVariableType typeInfo, 
            bool showInInspector,
            object data = default, 
            bool rename = true,
            string comment = "")
        {
            Elements.arraySize++;
            SerializedProperty newVariableProperty = Elements.GetArrayElementAtIndex(Elements.arraySize - 1);

            string id = Guid.NewGuid().ToString();
            
            if (typeInfo != CyanTriggerVariableType.SectionStart 
                && typeInfo != CyanTriggerVariableType.SectionEnd)
            {
                variableName = _getUniqueVariableName(variableName, id);
            }
            CyanTriggerSerializedPropertyUtils.SetVariableData(newVariableProperty, variableName, type, typeInfo, showInInspector, data, id, comment);
            
            _onVariableAddedOrRemoved?.Invoke(null);

            if (rename)
            {
                SetRenameIndex(Elements.arraySize - 1);
            }
        }

        private void DuplicateVariable(SerializedProperty variableProperty)
        {
            SerializedProperty nameProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.name));
            SerializedProperty typeProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.type));
            SerializedProperty typeDefProperty =
                typeProperty.FindPropertyRelative(nameof(CyanTriggerSerializableType.typeDef));
            Type type = Type.GetType(typeDefProperty.stringValue);
            SerializedProperty dataProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.data));
            var data = CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataProperty);
            SerializedProperty typeInfoProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.typeInfo));
            CyanTriggerVariableType typeInfo = (CyanTriggerVariableType)typeInfoProperty.enumValueIndex;
            SerializedProperty showProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.showInInspector));
            SerializedProperty commentProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.comment))
                .FindPropertyRelative(nameof(CyanTriggerComment.comment));
            
            AddNewVariable(nameProperty.stringValue, type, typeInfo, showProperty.boolValue, data, false, commentProperty.stringValue);
        }
        
        protected override void OnElementRemapped(VariableExpandData element, int prevIndex, int newIndex)
        {
            element.List = null;
            _onVariableAddedOrRemoved?.Invoke(null);
        }
    }
}