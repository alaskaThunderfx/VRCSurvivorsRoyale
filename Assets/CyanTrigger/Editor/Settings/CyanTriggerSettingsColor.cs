using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Cyan.CT.Editor
{
    public enum CyanTriggerColorTheme
    {
        SpecialAction,
        UdonTypeName,
        CustomActionName,
        ActionName,
        VariableIndicator,
        OutputIndicator,
        VariableName,
        Punctuation,
        Error,
        Warning,
        Comment,
        NullLiteral,
        StringLiteral,
        UnityObjectLiteral,
        ValueLiteral,
        Background,
        Border,
        Selection,
    }
    
    [Serializable]
    public class CyanTriggerSettingsColor
    {
        public static readonly CyanTriggerSettingsColor DarkThemeDefault = new CyanTriggerSettingsColor
        {
            specialActions = new Color(0.424f, 0.584f, 0.922f),
            udonTypeName = new Color(0.757f, 0.569f, 1f),
            customActionName = new Color(0.61f, 0.57f, 1f),
            actionName = new Color(0.224f, 0.8f, 0.561f),
            outputIndicator = new Color(0.9568627f, 0.5960785f, 0.0627451f),
            variableIndicator = new Color(0.4f, 0.765f, 0.8f),
            variableName = new Color(0.8f, 0.8f, 0.8f),
            punctuation = new Color(0.8f, 0.8f, 0.8f),
            warning = new Color(0.957f, 0.4f, 0f),
            error = new Color(1, 0.337f, 0.278f),
            comment = new Color(0.5215687f, 0.7686275f, 0.4235294f),
            nullLiteral = new Color(0.957f, 0.4f, 0f),
            stringLiteral = new Color(0.788f, 0.635f, 0.427f),
            unityObjectLiteral = new Color(0.75f, 0.82f, 0.5f),
            valueLiteral = new Color(0.929f, 0.58f, 0.753f),
            
            background = new Color(0.157f, 0.157f, 0.157f),
            border = new Color(0.251f, 0.251f, 0.251f),
            selection = new Color(0.031f, 0.2f, 0.369f),
        };
        
        public static readonly CyanTriggerSettingsColor DarkThemeNoColor = new CyanTriggerSettingsColor
        {
            specialActions = new Color(0.8f, 0.8f, 0.8f),
            udonTypeName = new Color(0.8f, 0.8f, 0.8f),
            customActionName = new Color(0.8f, 0.8f, 0.8f),
            actionName = new Color(0.8f, 0.8f, 0.8f),
            outputIndicator = new Color(0.8f, 0.8f, 0.8f),
            variableIndicator = new Color(0.8f, 0.8f, 0.8f),
            variableName = new Color(0.8f, 0.8f, 0.8f),
            punctuation = new Color(0.8f, 0.8f, 0.8f),
            warning = new Color(0.957f, 0.4f, 0f),
            error = new Color(1, 0.337f, 0.278f),
            comment = new Color(0.5215687f, 0.7686275f, 0.4235294f),
            nullLiteral = new Color(0.8f, 0.8f, 0.8f),
            stringLiteral = new Color(0.8f, 0.8f, 0.8f),
            unityObjectLiteral = new Color(0.8f, 0.8f, 0.8f),
            valueLiteral = new Color(0.8f, 0.8f, 0.8f),
            
            background = new Color(0.247f, 0.247f, 0.247f),
            border = new Color(0.173f, 0.173f, 0.173f),
            selection = new Color(0.1725f, 0.364f, 0.53f),
        };
        
        public static readonly CyanTriggerSettingsColor LightThemeDefault = new CyanTriggerSettingsColor
        {
            specialActions = new Color(0.059f, 0.329f, 0.839f),
            udonTypeName = new Color(0.42f, 0.184f, 0.729f),
            customActionName = new Color(0.23f, 0.18f, 0.73f),
            actionName = new Color(0f, 0.522f, 0.373f),
            outputIndicator = new Color(0.957f, 0.596f, 0.063f),
            variableIndicator = new Color(0f, 0.576f, 0.631f),
            variableName = new Color(0.22f, 0.22f, 0.22f),
            punctuation = new Color(0.22f, 0.22f, 0.22f),
            warning = new Color(0.957f, 0.596f, 0.063f),
            error = new Color(0.851f, 0.078f, 0f),
            comment = new Color(0.02352941f, 0.5294118f, 0f),
            nullLiteral = new Color(0.957f, 0.4f, 0f),
            stringLiteral = new Color(0.549f, 0.424f, 0.255f),
            unityObjectLiteral = new Color(0.37f, 0.52f, 0.0f),
            valueLiteral = new Color(0.671f, 0.184f, 0.42f),
            
            background = new Color(0.95f, 0.95f, 0.95f),
            border = new Color(0.8f, 0.8f, 0.8f),
            selection = new Color(0.722f, 0.851f, 1f),
        };

        public static readonly CyanTriggerSettingsColor LightThemeNoColor = new CyanTriggerSettingsColor
        {
            specialActions = new Color(0.22f, 0.22f, 0.22f),
            udonTypeName = new Color(0.22f, 0.22f, 0.22f),
            customActionName = new Color(0.22f, 0.22f, 0.22f),
            actionName = new Color(0.22f, 0.22f, 0.22f),
            outputIndicator = new Color(0.22f, 0.22f, 0.22f),
            variableIndicator = new Color(0.22f, 0.22f, 0.22f),
            variableName = new Color(0.22f, 0.22f, 0.22f),
            punctuation = new Color(0.22f, 0.22f, 0.22f),
            warning = new Color(0.957f, 0.596f, 0.063f),
            error = new Color(0.851f, 0.078f, 0f),
            comment = new Color(0.02352941f, 0.5294118f, 0f),
            nullLiteral = new Color(0.22f, 0.22f, 0.22f),
            stringLiteral = new Color(0.22f, 0.22f, 0.22f),
            unityObjectLiteral = new Color(0.22f, 0.22f, 0.22f),
            valueLiteral = new Color(0.22f, 0.22f, 0.22f),
            
            background = new Color(0.792f, 0.792f, 0.792f),
            border = new Color(0.647f, 0.647f, 0.647f),
            selection = new Color(0.227451f, 0.4470589f, 0.6901961f),
        };
        
        public Color specialActions;
        [FormerlySerializedAs("className")] 
        public Color udonTypeName;
        [FormerlySerializedAs("customClassName")] 
        public Color customActionName;
        [FormerlySerializedAs("methodName")] 
        public Color actionName;

        public Color outputIndicator;
        public Color variableIndicator;
        public Color variableName;
        public Color punctuation;
        
        public Color warning;
        public Color error;
        public Color comment;
        
        public Color background;
        public Color border;
        public Color selection;
        
        // Constants
        public Color nullLiteral;
        public Color stringLiteral;
        public Color unityObjectLiteral;
        public Color valueLiteral;
        
        public static CyanTriggerSettingsColor Clone(CyanTriggerSettingsColor other)
        {
            return new CyanTriggerSettingsColor
            {
                specialActions = other.specialActions,
                udonTypeName = other.udonTypeName,
                customActionName = other.customActionName,
                actionName = other.actionName,
                outputIndicator = other.outputIndicator,
                variableIndicator = other.variableIndicator,
                variableName = other.variableName,
                punctuation = other.punctuation,
                warning = other.warning,
                error = other.error,
                comment = other.comment,
                nullLiteral = other.nullLiteral,
                stringLiteral = other.stringLiteral,
                unityObjectLiteral = other.unityObjectLiteral,
                valueLiteral = other.valueLiteral,
                background = other.background,
                border = other.border,
                selection = other.selection
            };
        }
        
        public static CyanTriggerSettingsColor GetDarkTheme()
        {
            return DarkThemeDefault;
        }
        
        public static CyanTriggerSettingsColor GetLightTheme()
        {
            return LightThemeDefault;
        }

        public Color GetColor(CyanTriggerColorTheme color)
        {
            switch (color)
            {
                case CyanTriggerColorTheme.SpecialAction:
                    return specialActions;
                case CyanTriggerColorTheme.UdonTypeName:
                    return udonTypeName;
                case CyanTriggerColorTheme.CustomActionName:
                    return customActionName;
                case CyanTriggerColorTheme.ActionName:
                    return actionName;
                case CyanTriggerColorTheme.VariableIndicator:
                    return variableIndicator;
                case CyanTriggerColorTheme.OutputIndicator:
                    return outputIndicator;
                case CyanTriggerColorTheme.VariableName:
                    return variableName;
                case CyanTriggerColorTheme.Punctuation:
                    return punctuation;
                case CyanTriggerColorTheme.Error:
                    return error;
                case CyanTriggerColorTheme.Warning:
                    return warning;
                case CyanTriggerColorTheme.Comment:
                    return comment;
                case CyanTriggerColorTheme.NullLiteral:
                    return nullLiteral;
                case CyanTriggerColorTheme.StringLiteral:
                    return stringLiteral;
                case CyanTriggerColorTheme.UnityObjectLiteral:
                    return unityObjectLiteral;
                case CyanTriggerColorTheme.ValueLiteral:
                    return valueLiteral;
                case CyanTriggerColorTheme.Background:
                    return background;
                case CyanTriggerColorTheme.Border:
                    return border;
                case CyanTriggerColorTheme.Selection:
                    return selection;
                default:
                    throw new ArgumentOutOfRangeException(nameof(color), color, null);
            }
        }
    }
}