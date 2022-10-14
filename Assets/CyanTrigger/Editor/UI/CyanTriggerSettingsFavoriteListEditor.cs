using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    [CustomEditor(typeof(CyanTriggerSettingsFavoriteList))]
    public class CyanTriggerSettingsFavoriteListEditor : UnityEditor.Editor
    {
        private static readonly GUIContent FavoriteVariablesLabel = new GUIContent("Favorite Variables");
        private static readonly GUIContent FavoriteEventsLabel = new GUIContent("Favorite Events");
        private static readonly GUIContent FavoriteActionsLabel = new GUIContent("Favorite Actions");
        
        private static readonly GUIContent AddVariablesLabel = new GUIContent("Add Variable");
        private static readonly GUIContent AddEventsLabel = new GUIContent("Add Event");
        private static readonly GUIContent AddActionsLabel = new GUIContent("Add Action");

        private SerializedProperty _favoritesProperty;
        private CyanTriggerSettingsFavoritesTreeView _favoritesTreeView;
        private CyanTriggerFavoriteType _favoriteType = CyanTriggerFavoriteType.None;
        
        private void OnEnable()
        {
            if (target == null)
            {
                DestroyImmediate(this);
                return;
            }
            
            _favoritesProperty = serializedObject.FindProperty(nameof(CyanTriggerSettingsFavoriteList.favoriteItems));
            _favoritesTreeView = new CyanTriggerSettingsFavoritesTreeView(_favoritesProperty);
            
            _favoriteType = (CyanTriggerFavoriteType)serializedObject.FindProperty(
                nameof(CyanTriggerSettingsFavoriteList.favoriteType)).enumValueIndex;
        }

        public void SetIdStartIndex(int id)
        {
            _favoritesTreeView.IdStartIndex = id;
        }

        public int GetSize()
        {
            return _favoritesTreeView.Size;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();
            
            if (Event.current.type == EventType.ValidateCommand &&
                Event.current.commandName == "UndoRedoPerformed")
            {
                _favoritesTreeView.Reload();
            }

            switch (_favoriteType)
            {
                case CyanTriggerFavoriteType.Variables:
                    DrawTree(FavoriteVariablesLabel, AddVariablesLabel, AddVariableMenu);
                    break;
                case CyanTriggerFavoriteType.Events:
                    DrawTree(FavoriteEventsLabel, AddEventsLabel, AddEvent);
                    break;
                case CyanTriggerFavoriteType.Actions:
                    DrawTree(FavoriteActionsLabel, AddActionsLabel, AddAction);
                    break;
            }

            ApplyModifiedProperties();
        }

        private void ApplyModifiedProperties()
        {
            bool changes = serializedObject.ApplyModifiedProperties();
            if (_favoriteType == CyanTriggerFavoriteType.Events && changes)
            {
                CyanTriggerEventSearchWindow.ResetCache();
            }
        }
        
        private void DrawTree(
            GUIContent label,
            GUIContent addLabel,
            Action addAction)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            // Catch Undo/redo case
            if (_favoritesTreeView.Size != _favoritesProperty.arraySize)
            {
                _favoritesTreeView.Reload();
            }

            Rect treeRect = EditorGUILayout.BeginVertical();
            treeRect.height = _favoritesTreeView.totalHeight + EditorGUIUtility.singleLineHeight;
            GUILayout.Space(treeRect.height);
            _favoritesTreeView.OnGUI(treeRect);
            EditorGUILayout.EndVertical();
            
            
            EditorGUILayout.Space();
            
            
            Rect buttonAreaRect = EditorGUILayout.BeginHorizontal();
            buttonAreaRect.height = 16;
            const float spaceBetween = 5;
            float width = (buttonAreaRect.width - spaceBetween * 2) / 3;

            Rect button1 = new Rect(buttonAreaRect.x, buttonAreaRect.y, width, buttonAreaRect.height);
            Rect button2 = new Rect(button1.xMax + spaceBetween, buttonAreaRect.y, width, buttonAreaRect.height);
            Rect button3 = new Rect(button2.xMax + spaceBetween, buttonAreaRect.y, width, buttonAreaRect.height);

            if (GUI.Button(button1, addLabel))
            {
                addAction.Invoke();
                _favoritesTreeView.Reload();
            }
            
            if (GUI.Button(button2, "Add Folder"))
            {
                int index = _favoritesProperty.arraySize;
                AddItem(_favoritesProperty, "New Folder", true);
                _favoritesTreeView.Reload();
            
                // Ensure new folder starts expanded
                List<int> expanded = new List<int>(_favoritesTreeView.GetExpanded());
                expanded.Add(index + _favoritesTreeView.IdStartIndex); // expected folder start index
                _favoritesTreeView.SetExpanded(expanded);
                _favoritesTreeView.BeginRename(_favoritesTreeView.GetItem(index));
            }
            
            EditorGUI.BeginDisabledGroup(!_favoritesTreeView.HasSelection());
            if (GUI.Button(button3, "Remove"))
            {
                _favoritesTreeView.RemoveSelected();
            }
            EditorGUI.EndDisabledGroup();
        
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(buttonAreaRect.height);

            EditorGUILayout.EndVertical();
        }
        
        private void AddVariableMenu()
        {
            CyanTriggerSearchWindowManager.Instance.DisplayVariableSearchWindow(AddVariable);
        }
        
        private void AddVariable(UdonNodeDefinition selectedItem)
        {
            if (_favoriteType != CyanTriggerFavoriteType.Variables)
            {
                return;
            }
            
            AddDataItem(_favoritesProperty, 
                CyanTriggerNameHelpers.GetTypeFriendlyName(selectedItem.type), 
                CyanTriggerActionInfoHolder.GetActionInfoHolder(selectedItem));
            
            ApplyModifiedProperties();
            _favoritesTreeView.Reload();
        }

        private void AddEvent()
        {
            CyanTriggerSearchWindowManager.Instance.DisplayEventSearchWindow(AddEvent);
        }
        
        private void AddEvent(CyanTriggerActionInfoHolder selectedItem)
        {
            if (_favoriteType != CyanTriggerFavoriteType.Events)
            {
                return;
            }
            
            AddDataItem(_favoritesProperty, selectedItem.GetActionRenderingDisplayName(), selectedItem);
            
            ApplyModifiedProperties();
            _favoritesTreeView.Reload();
        }
        
        private void AddAction()
        {
            CyanTriggerSearchWindowManager.Instance.DisplayActionSearchWindow(AddAction);
        }
        
        private void AddAction(CyanTriggerActionInfoHolder selectedItem)
        {
            if (_favoriteType != CyanTriggerFavoriteType.Actions)
            {
                return;
            }
            
            AddDataItem(_favoritesProperty, selectedItem.GetActionRenderingDisplayName(), selectedItem);
            
            ApplyModifiedProperties();
            _favoritesTreeView.Reload();
        }
        
        private static void AddDataItem(SerializedProperty property, string itemName, CyanTriggerActionInfoHolder infoHolder)
        {
            string def = infoHolder.Definition?.FullName;
            string guid = infoHolder.Action?.guid;
            AddDataItem(property, itemName, def, guid);
        }
        
        private static void AddDataItem(SerializedProperty property, string itemName, string directDefinition, string guid)
        {
            var element = AddItem(property, itemName, false);
            SetDataForItem(element, directDefinition, guid);
        }

        private static void SetDataForItem(SerializedProperty property, string direct, string guid)
        {
            var dataProp = property.FindPropertyRelative(nameof(CyanTriggerSettingsFavoriteItem.data));
            dataProp.FindPropertyRelative(nameof(CyanTriggerActionType.directEvent)).stringValue = direct;
            dataProp.FindPropertyRelative(nameof(CyanTriggerActionType.guid)).stringValue = guid;
        }
        
        private static SerializedProperty AddItem(SerializedProperty property, string itemName, bool hasScope)
        {
            ++property.arraySize;
            var element = property.GetArrayElementAtIndex(property.arraySize - 1);
            element.FindPropertyRelative(nameof(CyanTriggerSettingsFavoriteItem.item)).stringValue = itemName;
            element.FindPropertyRelative(nameof(CyanTriggerSettingsFavoriteItem.scopeDelta)).intValue = hasScope ? 1 : 0;
            SetDataForItem(element, "", "");
                
            if (hasScope)
            {
                ++property.arraySize;
                var scopeElement = property.GetArrayElementAtIndex(property.arraySize - 1);
                scopeElement.FindPropertyRelative(nameof(CyanTriggerSettingsFavoriteItem.item)).stringValue = 
                    $"_ScopeEnd {itemName}";
                scopeElement.FindPropertyRelative(nameof(CyanTriggerSettingsFavoriteItem.scopeDelta)).intValue = -1;
                SetDataForItem(scopeElement, "", "");
            }

            return element;
        }
    }
}