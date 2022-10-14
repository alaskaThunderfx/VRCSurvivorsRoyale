using System;
using UnityEditor;
using UnityEngine;

namespace Cyan.CT.Editor
{
    public static class CyanTriggerEditorUtils
    {
        private const string WikiURL = CyanTriggerDocumentationLinks.WikiLink;
        private const string DiscordURL = "https://discord.gg/stPkhM2T6C";
        private const string PatreonURL = "https://www.patreon.com/CyanLaser";

        public static void ShowSettings()
        {
            SettingsService.OpenProjectSettings(CyanTriggerSettingsProvider.SettingsPath);
        }

        public static void AddIndent()
        {
            ++EditorGUI.indentLevel;
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(EditorGUI.indentLevel);
            EditorGUILayout.BeginVertical();
        }

        public static void RemoveIndent()
        {
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            --EditorGUI.indentLevel;
        }
        
        public static void DrawHeader(string title, bool showSettingsButton)
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawTitleLine(title);
            DrawLinks(showSettingsButton);
            
            EditorGUILayout.EndVertical();
        }

        private static void DrawTitleLine(string title)
        {
            Rect titleRect = EditorGUILayout.BeginHorizontal();
            titleRect.height = EditorGUIUtility.singleLineHeight;
            
            Rect right = new Rect(titleRect);
            right.xMin = right.xMax - Mathf.Min(titleRect.width * 0.5f, 100);
            
            Rect left = new Rect(titleRect);
            left.width = titleRect.width - right.width;

            EditorGUI.LabelField(left, $"{title} v{CyanTriggerResourceManager.Instance.GetVersion()}", EditorStyles.boldLabel);

            var rightAlign = new GUIStyle(EditorStyles.label);
            rightAlign.alignment = TextAnchor.MiddleRight;
            GUI.Label(right, "From CyanLaser", rightAlign);
            
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(titleRect.height);
            EditorGUILayout.Space(1);
        }
        
        private static void DrawLinks(bool showSettingsButton)
        {
            Rect buttonAreaRect = EditorGUILayout.BeginHorizontal();
            buttonAreaRect.height = EditorGUIUtility.singleLineHeight;
            const float spaceBetween = 5;
            int count = 3;
            if (showSettingsButton)
            {
                ++count;
            }
            float width = (buttonAreaRect.width - spaceBetween * (count - 1)) / count;

            Rect buttonRect = new Rect(buttonAreaRect.x, buttonAreaRect.y, width, buttonAreaRect.height);
            void UpdateButton()
            {
                buttonRect.x = buttonRect.xMax + spaceBetween;
            }
            
            if (showSettingsButton)
            {
                if (GUI.Button(buttonRect, "Settings"))
                {
                    ShowSettings();
                }
                UpdateButton();
            }
            
            if (GUI.Button(buttonRect, "Wiki"))
            {
                Application.OpenURL(WikiURL);
            }
            UpdateButton();

            if (GUI.Button(buttonRect, "Discord"))
            {
                Application.OpenURL(DiscordURL);
            }
            UpdateButton();

            if (GUI.Button(buttonRect, "Patreon"))
            {
                Application.OpenURL(PatreonURL);
            }
            UpdateButton();

            // TODO GitHub or Youtube tutorials link

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(buttonAreaRect.height);
        }

        public static (GUIContent, Action) GetDocumentationAction(CyanTriggerActionGroupDefinitionUdonAsset udonActionGroup)
        {
            string tooltip = $"Open Custom Action:\n{udonActionGroup.GetNamespace()}";
            GUIContent content = new GUIContent(CyanTriggerImageResources.CyanTriggerCustomActionIcon, tooltip);
            return (content, () => Selection.SetActiveObjectWithContext(udonActionGroup.udonProgramAsset, null));
        }

        public static (GUIContent, Action) GetDocumentationAction(CyanTriggerActionInfoHolder actionInfo)
        {
            if (actionInfo.HasDocumentationLink())
            {
                string tooltip = $"Open Documentation:\n{actionInfo.GetActionRenderingDisplayName()}";
                GUIContent content = new GUIContent(CyanTriggerImageResources.DocumentationIcon, tooltip);
                return (content, () => Application.OpenURL(actionInfo.GetDocumentationLink()));
            }
            if (actionInfo.ActionGroup is CyanTriggerActionGroupDefinitionUdonAsset udonActionGroup)
            {
                return GetDocumentationAction(udonActionGroup);
            }

            return (null, null);
        }

        public static bool DrawDocumentationButtonForActionInfo(
            Rect rect, 
            CyanTriggerActionInfoHolder actionInfo,
            bool shouldShow = true)
        {
            (GUIContent content, Action onClick) = GetDocumentationAction(actionInfo);
            if (content != null && onClick != null)
            {
                DrawDocumentationButtonForActionInfo(rect, content, onClick, shouldShow);
                return true;
            }
            
            return false;
        }

        public static void DrawDocumentationButtonForActionInfo(
            Rect rect, 
            Type type,
            ICyanTriggerCustomType customTypeData, 
            bool shouldShow)
        {
            if (customTypeData == null)
            {
                // Disable warning for use of Obsolete type CyanTriggerCustomNodeVariable
#pragma warning disable CS0618
                string variableType = CyanTriggerCustomNodeVariable.GetFullnameForType(type);
#pragma warning restore CS0618
                var actionInfo = CyanTriggerActionInfoHolder.GetActionInfoHolder("", variableType);
                DrawDocumentationButtonForActionInfo(rect, actionInfo, shouldShow);
                return;
            }
            
            DrawDocumentationButtonForActionInfo(
                rect, 
                customTypeData.GetDocumentationContent(),
                customTypeData.GetDocumentationAction(), 
                shouldShow);
        }
        
        public static void DrawDocumentationButtonForActionInfo(Rect rect, GUIContent content, Action onClick, bool shouldShow)
        {
            // When the doc link should not be shown, we still need to draw it, but do so off screen to prevent clicking. 
            // The button needs to be shown to prevent Control Id's from changing based on mouse position.
            if (!shouldShow)
            {
                rect.x += 100000;
            }

            if (GUI.Button(rect, content, GUIStyle.none) && shouldShow)
            {
                onClick.Invoke();
            }
        }

        public static void DrawDocumentationButton(Rect rect, string docDescription, string link)
        {
            GUIContent content = new GUIContent(CyanTriggerImageResources.DocumentationIcon, $"Open Documentation:\n{docDescription}");
            if (GUI.Button(rect, content, GUIStyle.none))
            {
                Application.OpenURL(link);
            }
        }
    }
}