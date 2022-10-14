using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Cyan.CT.Editor
{
    public class CyanTriggerSettingsFavoritesTreeView : CyanTriggerScopedTreeView
    {
        private CyanTriggerActionInfoHolder[] _actionInfos;
        
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
                height = 0f
            };
            multiColumnHeader.ResizeToFit();
            
            return multiColumnHeader;
        }
        
        protected override string GetElementDisplayName(SerializedProperty property, int index)
        {
            return property.FindPropertyRelative(nameof(CyanTriggerSettingsFavoriteItem.item)).stringValue;
        }
        
        protected override int GetElementScopeDelta(SerializedProperty property, int index)
        {
            return property.FindPropertyRelative(nameof(CyanTriggerSettingsFavoriteItem.scopeDelta)).intValue;
        }

        private CyanTriggerActionInfoHolder GetActionInfo(int index)
        {
            var dataProp = 
                ItemElements[index].FindPropertyRelative(nameof(CyanTriggerSettingsFavoriteItem.data));
            var guidProp = dataProp.FindPropertyRelative(nameof(CyanTriggerActionType.guid));
            var defProp = dataProp.FindPropertyRelative(nameof(CyanTriggerActionType.directEvent));
            return CyanTriggerActionInfoHolder.GetActionInfoHolder(guidProp.stringValue, defProp.stringValue);
        }
        
        public CyanTriggerSettingsFavoritesTreeView(SerializedProperty elements) : base (elements, CreateColumnHeader())
        {
            showBorder = true;
            showAlternatingRowBackgrounds = true;
            useScrollView = true;
            
            DelayReload();
        }

        protected override void OnBuildRoot(CyanTriggerScopedTreeItem root)
        {
            _actionInfos = new CyanTriggerActionInfoHolder[Size];
        }

        protected override bool CanRename(TreeViewItem item)
        {
            return ((CyanTriggerScopedTreeItem)item).HasScope;
        }
        
        protected override void RenameEnded(RenameEndedArgs args)
        {
            int index = GetItemIndex(args.itemID);
            Elements.GetArrayElementAtIndex(index)
                .FindPropertyRelative(nameof(CyanTriggerSettingsFavoriteItem.item)).stringValue = args.newName;
            Items[index].displayName = args.newName;
        }

        // TODO make more generic?
        protected override void OnRowGUI(RowGUIArgs args)
        {
            var item = (CyanTriggerScopedTreeItem)args.item;
            Rect cellRect = args.GetCellRect(0);

            CyanTriggerActionInfoHolder actionInfo = _actionInfos[item.Index];
            if (actionInfo == null)
            {
                actionInfo = _actionInfos[item.Index] = GetActionInfo(item.Index);
            }
            
            Rect folderRect = cellRect;
            folderRect.x += GetContentIndent(item);
            folderRect.width = 20;
            
            if (folderRect.xMax < cellRect.xMax)
            {
                Texture2D icon;
                string toolTip = item.displayName;
                if (item.HasScope)
                {
                    icon = CyanTriggerImageResources.FolderIcon;
                }
                else if (!actionInfo.IsValid())
                {
                    icon = CyanTriggerImageResources.ErrorIcon;
                    toolTip = $"{toolTip} (Invalid)";
                }
                else if (actionInfo.IsAction())
                {
                    icon = CyanTriggerImageResources.CyanTriggerCustomActionIcon;
                    toolTip = $"{toolTip} (Custom)";
                }
                else
                {
                    icon = CyanTriggerImageResources.ScriptIcon;
                }

                EditorGUI.LabelField(folderRect, new GUIContent(icon, toolTip));
                cellRect.width -= folderRect.width;
                cellRect.x += folderRect.width;
            }
        
            // icon and label
            args.rowRect = cellRect;
            
            base.OnRowGUI(args);
        }
    }
}

