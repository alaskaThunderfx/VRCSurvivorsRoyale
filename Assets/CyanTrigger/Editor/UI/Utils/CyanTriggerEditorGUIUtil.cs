using UnityEditor;
using UnityEngine;

namespace Cyan.CT.Editor
{
    public static class CyanTriggerEditorGUIUtil
    {
        private static GUIStyle _helpBoxStyle;
        public static GUIStyle HelpBoxStyle
        {
            get
            {
                if (_helpBoxStyle == null)
                {
                    _helpBoxStyle = EditorStyles.helpBox;
                }
                return _helpBoxStyle;
            }
        }
        
        private static GUIStyle _helpBoxClearStyle;
        public static GUIStyle HelpBoxClearStyle
        {
            get
            {
                if (_helpBoxClearStyle == null || _helpBoxClearStyle.normal.background == null)
                {
                    _helpBoxClearStyle = new GUIStyle();
                    _helpBoxClearStyle.normal.background = CyanTriggerImageResources.OutlineBox;
                    _helpBoxClearStyle.border = new RectOffset(1, 1, 1, 1);
                    _helpBoxClearStyle.padding = new RectOffset(8, 8, 0, 0);
                }
                return _helpBoxClearStyle;
            }
        }

        private static GUIStyle _selectedStyle;
        public static GUIStyle SelectedStyle
        {
            get
            {
                if (_selectedStyle == null) 
                {
                    _selectedStyle = new GUIStyle();
                }
                if (_selectedStyle.normal.background == null)
                {
                    _selectedStyle.normal.background = CyanTriggerImageResources.SelectedBox;
                }
                return _selectedStyle;
            }
        }
        
        private static GUIStyle _backgroundColorStyle;
        public static GUIStyle BackgroundColorStyle
        {
            get
            {
                if (_backgroundColorStyle == null || _backgroundColorStyle.normal.background == null) 
                {
                    _backgroundColorStyle = new GUIStyle
                    {
                        normal = { background = CyanTriggerImageResources.BackgroundColorBox},
                    };
                }
                return _backgroundColorStyle;
            }
        }
        
        
        private static GUIStyle _borderedBoxStyle;
        public static GUIStyle BorderedBoxStyle
        {
            get
            {
                if (_borderedBoxStyle == null || _borderedBoxStyle.normal.background == null)
                {
                    _borderedBoxStyle = new GUIStyle
                    {
                        border = new RectOffset(2, 2, 2, 2), 
                        normal = { background = CyanTriggerImageResources.EventInputBackground},
                        padding = new RectOffset(8,8,2,5),
#if UNITY_2019_4_OR_NEWER
                        margin = new RectOffset(4, 4, 0, 0),
#else
                        margin = new RectOffset(5, 5, 0, 0)
#endif
                    };
                }
                return _borderedBoxStyle;
            }
        }
        
        private static GUIStyle _foldoutStyle;
        public static GUIStyle FoldoutStyle
        {
            get
            {
                if (_foldoutStyle == null)
                {
                    _foldoutStyle = "IN Foldout";
                }
                return _foldoutStyle;
            }
        }
        
        private static GUIStyle _treeViewLabelStyle;
        public static GUIStyle TreeViewLabelStyle
        {
            get
            {
                if (_treeViewLabelStyle == null)
                {
                    _treeViewLabelStyle = CreateLineStyle();
                    _treeViewLabelStyle.wordWrap = true;
                }
                _treeViewLabelStyle.richText = CyanTriggerSettings.Instance.useColorThemes;
                _treeViewLabelStyle.normal.textColor = CyanTriggerImageResources.TextColor;
                _treeViewLabelStyle.focused.textColor = CyanTriggerImageResources.TextColor;
                return _treeViewLabelStyle;
            }
        }
        
        private static GUIStyle _commentStyle;
        public static GUIStyle CommentStyle
        {
            get
            {
                if (_commentStyle == null)
                {
                    _commentStyle = CreateLineStyle();
                    _commentStyle.wordWrap = true;
                    _commentStyle.richText = true;
                }
                return _commentStyle;
            }
        }
        
        private static GUIStyle _commentLabelStyle;
        public static GUIStyle CommentLabelStyle
        {
            get
            {
                if (_commentLabelStyle == null)
                {
                    _commentLabelStyle = new GUIStyle(EditorStyles.label);
                    _commentLabelStyle.wordWrap = true;
                    _commentLabelStyle.richText = true;
                }
                return _commentLabelStyle;
            }
        }

        private static GUIStyle _warningTextStyle;
        public static GUIStyle WarningTextStyle
        {
            get
            {
                if (_warningTextStyle == null)
                {
                    _warningTextStyle = new GUIStyle(EditorStyles.textArea);
                }
                _warningTextStyle.normal.textColor = CyanTriggerImageResources.WarningColor;
                return _warningTextStyle;
            }
        }
        
        private static GUIStyle _errorTextStyle;
        public static GUIStyle ErrorTextStyle
        {
            get
            {
                if (_errorTextStyle == null)
                {
                    _errorTextStyle = new GUIStyle(EditorStyles.textArea);
                }
                _errorTextStyle.normal.textColor = CyanTriggerImageResources.ErrorColor;
                return _errorTextStyle;
            }
        }


        private static GUIContent _openActionEditorIcon;
        public static GUIContent OpenActionEditorIcon
        {
            get
            {
                if (_openActionEditorIcon == null)
                {
                    _openActionEditorIcon = EditorGUIUtility.TrIconContent("winbtn_win_max_h", "Open Action Editor");
                }
                return _openActionEditorIcon;
            }
        }
        
        private static GUIContent _closeActionEditorIcon;
        public static GUIContent CloseActionEditorIcon
        {
            get
            {
                if (_closeActionEditorIcon == null)
                {
                    _closeActionEditorIcon = EditorGUIUtility.TrIconContent("winbtn_win_min_h", "Close Action Editor");
                }
                return _closeActionEditorIcon;
            }
        }
        
        private static GUIContent _commentCompleteIcon;
        public static GUIContent CommentCompleteIcon
        {
            get
            {
                if (_commentCompleteIcon == null)
                {
                    _commentCompleteIcon = EditorGUIUtility.TrIconContent("FilterSelectedOnly", "Close comment editor");
                }
                return _commentCompleteIcon;
            }
        }

        private static GUIContent _eventMenuIcon;
        public static GUIContent EventMenuIcon
        {
            get
            {
                if (_eventMenuIcon == null)
                {
#if UNITY_2019_4_OR_NEWER
                    _eventMenuIcon = EditorGUIUtility.TrIconContent("_Menu", "View Event Options");
#else
                    _eventMenuIcon = EditorGUIUtility.TrIconContent("LookDevPaneOption", "View Event Options");
#endif
                }
                return _eventMenuIcon;
            }
        }
        
        private static GUIContent _eventDuplicateIcon;
        public static GUIContent EventDuplicateIcon
        {
            get
            {
                if (_eventDuplicateIcon == null)
                {
                    _eventDuplicateIcon = EditorGUIUtility.TrIconContent("TreeEditor.Duplicate", "Duplicate Event");
                }
                return _eventDuplicateIcon;
            }
        }
        
        private static GUIContent _eventCommentContent;
        public static GUIContent EventCommentContent
        {
            get
            {
                if (_eventCommentContent == null)
                {
                    _eventCommentContent = new GUIContent("//", "Edit Comment");
                }
                return _eventCommentContent;
            }
        }

        public static void ClearThemeCache()
        {
            if (_helpBoxClearStyle != null)
            {
                _helpBoxClearStyle.normal.background = null;
            }
            if (_borderedBoxStyle != null)
            {
                _borderedBoxStyle.normal.background = null;
            }
            if (_selectedStyle != null)
            {
                _selectedStyle.normal.background = null;
            }
            if (_backgroundColorStyle != null)
            {
                _backgroundColorStyle.normal.background = null;
            }
              
            _warningTextStyle = null;
            _errorTextStyle = null;
        }

        private static GUIStyle CreateLineStyle()
        {
            GUIStyle lineStyle = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).FindStyle("TV Line");
            GUIStyle style = new GUIStyle(lineStyle)
            {
                // Fix bug where Layout and Repaint give different heights for Event comments 
                stretchWidth = true,
            };

            return style;
        }
    }
}