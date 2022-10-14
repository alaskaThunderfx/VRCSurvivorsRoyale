using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.Udon;

namespace Cyan.CT.Editor
{
    public class CyanTriggerVariableValueTreeView : CyanTriggerVariableTreeView
    {
        private readonly CyanTriggerVariable[] _variables;
        
        public CyanTriggerVariableValueTreeView(
            SerializedProperty variablesProperty, 
            CyanTriggerVariable[] variables,
            UdonBehaviour[] backingUdon) 
            : base(variablesProperty, backingUdon, true)
        {
            _variables = variables;
            if (_variables != null && Elements.arraySize != _variables.Length)
            {
                Elements.arraySize = _variables.Length;
            }

            DelayReload();
        }

        protected override void BeforeTreeLayout()
        {
            if (Elements.arraySize != _variables.Length)
            {
                Elements.arraySize = _variables.Length;
                // TODO shouldn't this need a reload as well?
            }

            int visible = 0;
            for (int index = 0; index < Items.Length; ++index)
            {
                if (Items[index] != null)
                {
                    Items[index].displayName = GetNameForIndex(index);
                }

                if (!IsElementHidden(null, index))
                {
                    ++visible;
                }
            }

            if (visible != VisualSize)
            {
                Reload();
            }
        }

        protected override string GetElementDisplayName(SerializedProperty property, int index)
        {
            return GetNameForIndex(index);
        }

        protected override bool IsElementHidden(SerializedProperty property, int index)
        {
            if (_variables == null || index >= _variables.Length)
            {
                return false;
            }
            return !_variables[index].DisplayInInspector();
        }

        protected override string GetNameForIndex(int index)
        {
            if (_variables == null || index >= _variables.Length)
            {
                return "";
            }
            return _variables[index].name;
        }

        protected override Type GetTypeForIndex(int index)
        {
            if (_variables == null || index >= _variables.Length)
            {
                return null;
            }
            return _variables[index].type.Type;
        }

        protected override CyanTriggerVariableType GetVariableTypeForIndex(int index)
        {
            if (_variables == null || index >= _variables.Length)
            {
                return CyanTriggerVariableType.Unknown;
            }
            return _variables[index].typeInfo;
        }

        protected override SerializedProperty GetDataPropertyForIndex(int index)
        {
            return ItemElements[index];
        }
        
        protected override object GetDataForIndex(int index)
        {
            SerializedProperty dataProperty = GetDataPropertyForIndex(index);
            return CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataProperty);
        }

        protected override SerializedProperty GetSyncModePropertyForIndex(int index)
        {
            return null;
        }
        
        protected override SerializedProperty GetDisplayNameProperty(int index)
        {
            return null;
        }

        protected override CyanTriggerVariableSyncMode GetSyncModeForIndex(int index)
        {
            if (_variables == null || index >= _variables.Length)
            {
                return CyanTriggerVariableSyncMode.NotSynced;
            }
            
            return _variables[index].sync;
        }
        
        protected override string GetCommentForIndex(int index)
        {
            if (_variables == null || index >= _variables.Length)
            {
                return "";
            }
            
            return _variables[index].comment?.comment;
        }
        
        protected override SerializedProperty GetCommentPropertyForIndex(int index)
        {
            return null;
        }
        
        
        protected override bool AllowRenameOption()
        {
            return false;
        }
        
        protected override bool AllowDeleteOption()
        {
            return false;
        }

        protected override void GetRightClickMenuOptions(GenericMenu menu, Event currentEvent)
        {
            IList<int> selection = GetSelection();
            if (selection.Count == 0)
            {
                return;
            }
            
            // Prefab changes listed first
            GetRevertPrefabRightClickMenuOptions(menu, selection);

            bool anyDisplayValues = false;
            foreach (var id in selection)
            {
                int index = GetItemIndex(id);
                if (!_variables[index].IsDisplayOnly())
                {
                    anyDisplayValues = true;
                    break;
                }
            }

            if (anyDisplayValues)
            {
                menu.AddItem(new GUIContent("Reset to default value"), false, () =>
                {
                    IList<int> curSelection = GetSelection();
                    foreach (var id in curSelection)
                    {
                        int index = GetItemIndex(id);
                        SerializedProperty dataProp = GetDataPropertyForIndex(index);
                        if (dataProp == null)
                        {
                            continue;
                        }
                    
                        CyanTriggerSerializableObject.UpdateSerializedProperty(dataProp, _variables[index].data.Obj);

                        var data = GetData(id);
                        if (data != null)
                        {
                            data.List = null;
                        }
                    }

                    Elements.serializedObject.ApplyModifiedProperties();
                    DelayRefreshRowHeight();
                });
            }
        }

        private void GetRevertPrefabRightClickMenuOptions(GenericMenu menu, IList<int> selection)
        {
            if (IsPlaying || !Elements.isInstantiatedPrefab)
            {
                return;
            }

            bool anyPrefab = false;
            foreach (var id in selection)
            {
                int index = GetItemIndex(id);
                if (_variables[index].IsDisplayOnly())
                {
                    continue;
                }
                
                SerializedProperty dataProp = GetDataPropertyForIndex(index);
                if (dataProp == null)
                {
                    continue;
                }

                if (dataProp.prefabOverride)
                {
                    anyPrefab = true;
                    break;
                }
            }

            if (!anyPrefab)
            {
                return;
            }

            menu.AddItem(new GUIContent("Apply Prefab Change"), false, () =>
            {
                string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                    PrefabUtility.GetOutermostPrefabInstanceRoot(Elements.serializedObject.targetObject));
                
                IList<int> curSelection = GetSelection();
                foreach (var id in curSelection)
                {
                    int index = GetItemIndex(id);
                    SerializedProperty dataProp = GetDataPropertyForIndex(index);
                    if (dataProp == null || !dataProp.prefabOverride)
                    {
                        continue;
                    }
                    
                    PrefabUtility.ApplyPropertyOverride(dataProp, assetPath, InteractionMode.UserAction);
                    
                    var data = GetData(id);
                    if (data != null)
                    {
                        data.List = null;
                    }
                }

                DelayRefreshRowHeight();
            });
            
            menu.AddItem(new GUIContent("Revert Prefab Changes"), false, () =>
            {
                IList<int> curSelection = GetSelection();
                foreach (var id in curSelection)
                {
                    int index = GetItemIndex(id);
                    SerializedProperty dataProp = GetDataPropertyForIndex(index);
                    if (dataProp == null)
                    {
                        continue;
                    }

                    dataProp.prefabOverride = false;
                    
                    var data = GetData(id);
                    if (data != null)
                    {
                        data.List = null;
                    }
                }

                Elements.serializedObject.ApplyModifiedProperties();
                DelayRefreshRowHeight();
            });
        }

        protected override bool CanStartDrag(CanStartDragArgs args) => false;
    }
}