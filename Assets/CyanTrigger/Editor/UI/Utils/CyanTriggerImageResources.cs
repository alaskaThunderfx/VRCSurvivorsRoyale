using System;
using UnityEditor;
using UnityEngine;

namespace Cyan.CT.Editor
{
    public static class CyanTriggerImageResources
    {
        public static readonly Color LineColorDark = EditorGUIUtility.isProSkin ? 
            new Color(0, 0, 0, 0.5f) : 
            new Color(0.5f, 0.5f, 0.5f, 0.5f);

        private static Color LineColor => CyanTriggerSettings.Instance.GetColorTheme().border;
        private static Color BackgroundColor => CyanTriggerSettings.Instance.GetColorTheme().background;
        private static Color SelectionColor => CyanTriggerSettings.Instance.GetColorTheme().selection;
        public static Color WarningColor => CyanTriggerSettings.Instance.GetColorTheme().warning;
        public static Color ErrorColor => CyanTriggerSettings.Instance.GetColorTheme().error;
        public static Color TextColor => CyanTriggerSettings.Instance.GetColorTheme().punctuation;
        
        
        private static Texture2D _outlineBox;
        public static Texture2D OutlineBox
        {
            get
            {
                if (_outlineBox == null)
                {
                    _outlineBox = CreateTexture(3, 3, (x, y) => x == 1 && y == 1 ? Color.clear : LineColorDark);
                }
                return _outlineBox;
            }
        }

        private static Texture2D _actionTreeOutlineTop;
        public static Texture2D ActionTreeOutlineTop
        {
            get
            {
                if (_actionTreeOutlineTop == null)
                {
                    _actionTreeOutlineTop = CreateTexture(3, 3, (x, y) => x == 1 && y <= 1 ? Color.clear : LineColor);
                }
                return _actionTreeOutlineTop;
            }
        }
        
        private static Texture2D _actionTreeWarningOutline;
        public static Texture2D ActionTreeWarningOutline
        {
            get
            {
                if (_actionTreeWarningOutline == null)
                {
                    _actionTreeWarningOutline = CreateTexture(3, 3, (x, y) => x == 1 && y == 1 ? Color.clear : WarningColor);
                }
                return _actionTreeWarningOutline;
            }
        }
        
        private static Texture2D _actionTreeErrorOutline; 
        public static Texture2D ActionTreeErrorOutline
        {
            get
            {
                if (_actionTreeErrorOutline == null)
                {
                    _actionTreeErrorOutline = CreateTexture(3, 3, (x, y) => x == 1 && y == 1 ? Color.clear : ErrorColor);
                }
                return _actionTreeErrorOutline;
            }
        }
        
        private static Texture2D _actionTreeGrayBox;
        public static Texture2D ActionTreeGrayBox 
        {
            get
            {
                if (_actionTreeGrayBox == null)
                {
                    _actionTreeGrayBox = CreateTexture(1,1, (x, y) => LineColor);
                }
                return _actionTreeGrayBox;
            }
        }
        
        private static Texture2D _selectedBox;
        public static Texture2D SelectedBox
        {
            get
            {
                if (_selectedBox == null)
                {
                    _selectedBox = CreateTexture(1,1, (x, y) => SelectionColor);
                }
                return _selectedBox;
            }
        }
        
        private static Texture2D _backgroundColorBox;
        public static Texture2D BackgroundColorBox
        {
            get
            {
                if (_backgroundColorBox == null)
                {
                    _backgroundColorBox = CreateTexture(1,1, (x, y) => BackgroundColor);
                }
                return _backgroundColorBox;
            }
        }
        
        private static Texture2D _eventInputBackground;
        public static Texture2D EventInputBackground
        {
            get
            {
                if (_eventInputBackground == null)
                {
                    _eventInputBackground = CreateTexture(5,5, (x, y) =>
                    {
                        if (x == 4 || y == 4 || x == 0 || y == 0)
                        {
                            return LineColorDark;
                        }

                        if (x == 3 || y == 3 || x == 1 || y == 1)
                        {
                            return LineColor;
                        }

                        return BackgroundColor;
                    });
                }
                return _eventInputBackground;
            }
        }

        private static Texture2D _clearImage;
        public static Texture2D ClearImage
        {
            get
            {
                if (_clearImage == null)
                {
                    _clearImage = CreateTexture(1, 1, (x, y) => Color.clear);
                }
                return _clearImage;
            }
        }
        
        private static Texture2D _scriptIcon;
        public static Texture2D ScriptIcon
        {
            get
            {
                if (_scriptIcon == null)
                {
                    _scriptIcon = EditorGUIUtility.FindTexture("cs Script Icon");
                }
                return _scriptIcon;
            }
        }

        private static Texture2D _cyanTriggerCustomActionIcon;
        public static Texture2D CyanTriggerCustomActionIcon
        {
            get
            {
                if (_cyanTriggerCustomActionIcon == null)
                {
                    _cyanTriggerCustomActionIcon = Resources.Load<Texture2D>("Images/CyanTriggerIconYellow");
                }
                return _cyanTriggerCustomActionIcon;
            }
        }
        
        private static Texture2D _documentationIcon;
        public static Texture2D DocumentationIcon
        {
            get
            {
                if (_documentationIcon == null)
                {
                    _documentationIcon = EditorGUIUtility.FindTexture("_Help@2x");
                }
                return _documentationIcon;
            }
        }
        
        private static Texture2D _lockedIcon;
        public static Texture2D LockedIcon
        {
            get
            {
                if (_lockedIcon == null)
                {
                    _lockedIcon = (Texture2D)EditorGUIUtility.TrIconContent("IN LockButton on").image;
                }
                return _lockedIcon;
            }
        }
        
        private static Texture2D _unlockedIcon;
        public static Texture2D UnlockedIcon
        {
            get
            {
                if (_unlockedIcon == null)
                {
                    _unlockedIcon = (Texture2D)EditorGUIUtility.TrIconContent("IN LockButton").image;
                }
                return _unlockedIcon;
            }
        }

        private static Texture2D _errorIcon;
        public static Texture2D ErrorIcon
        {
            get
            {
                if (_errorIcon == null)
                {
                    _errorIcon = (Texture2D)EditorGUIUtility.TrIconContent("console.erroricon").image;
                }
                return _errorIcon;
            }
        }

        private static Texture2D _folderIcon;
        public static Texture2D FolderIcon
        {
            get
            {
                if (_folderIcon == null)
                {
                    _folderIcon = (Texture2D)EditorGUIUtility.TrIconContent("Folder Icon").image;
                }
                return _folderIcon;
            }
        }


        
        private static Texture2D CreateTexture(int width, int height, Func<int, int, Color> getColor)
        {
            Texture2D ret = new Texture2D(width, height)
            {
                alphaIsTransparency = true,
                filterMode = FilterMode.Point
            };
            for (int y = 0; y < ret.height; ++y)
            {
                for (int x = 0; x < ret.width; ++x)
                {
                    ret.SetPixel(x, y, getColor(x, y));
                }
            }
            ret.Apply();
            return ret;
        }
        
        public static void ClearThemeCache()
        {
            _backgroundColorBox = null;
            _actionTreeOutlineTop = null;
            _actionTreeWarningOutline = null;
            _actionTreeErrorOutline = null;
            _actionTreeGrayBox = null;
            _selectedBox = null;
            _eventInputBackground = null;
        }
    }
}