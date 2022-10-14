using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace Cyan.CT
{
    public static class CyanTriggerNameHelpers
    {
        private const string EmptyNameString = "name";
        
        // TODO Find other keywords not allowed in udon.
        private static readonly HashSet<string> InvalidNameKeywords = new HashSet<string>
        {
            "true", "false", "null", "this"
        };
        
        // Check for each letter in the 
        // https://stackoverflow.com/questions/1904252
        // Note that this does not include the Underscore character and that needs to be handled manually.
        private static readonly HashSet<UnicodeCategory> ValidNameStartCharacters = new HashSet<UnicodeCategory>
        {
            UnicodeCategory.UppercaseLetter, // Lu
            UnicodeCategory.LowercaseLetter, // Ll
            UnicodeCategory.TitlecaseLetter, // Lt
            UnicodeCategory.ModifierLetter, // Lm
            UnicodeCategory.OtherLetter, // Lo
            
            // Invalid in Udon
            // UnicodeCategory.LetterNumber, // Nl
        };
        private static readonly HashSet<UnicodeCategory> ValidNameExtendCharacters = new HashSet<UnicodeCategory>
        {
            UnicodeCategory.DecimalDigitNumber, // Nd
            
            // Invalid in Udon
            // UnicodeCategory.NonSpacingMark, // Mn
            // UnicodeCategory.SpacingCombiningMark, // Mc
            // UnicodeCategory.ConnectorPunctuation, // Pc
            // UnicodeCategory.Format, // Cf

            // Start Characters
            UnicodeCategory.UppercaseLetter, // Lu
            UnicodeCategory.LowercaseLetter, // Ll
            UnicodeCategory.TitlecaseLetter, // Lt
            UnicodeCategory.ModifierLetter, // Lm
            UnicodeCategory.OtherLetter, // Lo
            
            // Invalid in Udon
            // UnicodeCategory.LetterNumber, // Nl
        };
        
        private static readonly Dictionary<Type, string> TypeFriendlyNameCache = new Dictionary<Type, string>();
        private static readonly Dictionary<Type, string> TypeSanitizedNameCache = new Dictionary<Type, string>();
        private static readonly Dictionary<Type, string[]> TypeCategoriesCache = new Dictionary<Type, string[]>();

        private static readonly CyanTriggerTrie<char, Func<string, string>> MethodFriendlyNamePrefix;

        static CyanTriggerNameHelpers()
        {
            MethodFriendlyNamePrefix = new CyanTriggerTrie<char, Func<string, string>>();
            MethodFriendlyNamePrefix.AddToTrie("set_", MethodFriendlyName_Set);
            MethodFriendlyNamePrefix.AddToTrie("get_", MethodFriendlyName_Get);
            MethodFriendlyNamePrefix.AddToTrie("op_", MethodFriendlyName_Op);
            MethodFriendlyNamePrefix.AddToTrie("ctor", MethodFriendlyName_Ctor);
        }
        
        public static string GetTypeFriendlyName(Type type)
        {
            if (TypeFriendlyNameCache.TryGetValue(type, out string value))
            {
                return value;
            }

            value = GetTypeFriendlyNameInternal(type);
            TypeFriendlyNameCache.Add(type, value);
            return value;
        }

        private static string GetTypeFriendlyNameInternal(Type type)
        {
            if (type.IsArray)
            {
                return $"{GetTypeFriendlyName(type.GetElementType())}Array";
            }
            
            if (type == typeof(int))
            {
                return "int";
            }
            if (type == typeof(uint))
            {
                return "uint";
            }
            if (type == typeof(short))
            {
                return "short";
            }
            if (type == typeof(ushort))
            {
                return "ushort";
            }
            if (type == typeof(long))
            {
                return "long";
            }
            if (type == typeof(ulong))
            {
                return "ulong";
            }
            if (type == typeof(float))
            {
                return "float";
            }
            if (type == typeof(bool))
            {
                return "bool";
            }
            if (type == typeof(IUdonEventReceiver))
            {
                return nameof(UdonBehaviour);
            }
            if (type == typeof(object))
            {
                return "object";
            }
            if (type == typeof(UnityEngine.Object))
            {
                return "UnityObject";
            }

            string name = type.Name;

            if (type.IsNested)
            {
                name = $"{GetTypeFriendlyName(type.DeclaringType)}.{name}";
            }

            if (type.IsEnum)
            {
                name += "Enum";
            }
            
            return name;
        }

        public static string GetSanitizedTypeName(Type type)
        {
            if (TypeSanitizedNameCache.TryGetValue(type, out string value))
            {
                return value;
            }
            
            StringBuilder sb = new StringBuilder();
            foreach (char c in type.ToString())
            {
                if (c == '.')
                {
                    continue;
                }
                if (c == '+')
                {
                    sb.Append('_');
                    continue;
                }
                if (c == '&')
                {
                    sb.Append("Ref");
                    continue;
                }
                sb.Append(c);
            }
            
            value = sb.ToString();
            TypeSanitizedNameCache.Add(type, value);
            return value;
        }

        public static string GetMethodFriendlyName(string methodName)
        {
            if (MethodFriendlyNamePrefix.GetFromTrie(methodName, out var func, true) && func != null)
            {
                methodName = func(methodName);
            }

            return methodName;
        }

        private static string MethodFriendlyName_Set(string methodName)
        {
            return $"Set {methodName.Substring(4)}";
        }
        private static string MethodFriendlyName_Get(string methodName)
        {
            return $"Get {methodName.Substring(4)}";
        }
        private static string MethodFriendlyName_Op(string methodName)
        {
            return methodName.Substring(3);
        }
        private static string MethodFriendlyName_Ctor(string methodName)
        {
            return "Constructor";
        }

        public static string[] GetTypeCategories(Type type)
        {
            if (!TypeCategoriesCache.TryGetValue(type, out var categories))
            {
                categories = type.FullName.Split('.');
                TypeCategoriesCache.Add(type, categories);
            }

            return categories;
        }

        public static string SanitizeName(string originalName)
        {
            if (string.IsNullOrEmpty(originalName))
            {
                return EmptyNameString;
            }
            
            StringBuilder sb = new StringBuilder();

            foreach (char c in originalName)
            {
                if (c == '_' || ValidNameExtendCharacters.Contains(char.GetUnicodeCategory(c)))
                {
                    sb.Append(c);
                }
            }

            if (sb.Length == 0)
            {
                return EmptyNameString;
            }

            char firstChar = sb[0];
            if (firstChar != '_' && !ValidNameStartCharacters.Contains(char.GetUnicodeCategory(firstChar)))
            {
                sb.Insert(0, '_');
            }
            
            string name = sb.ToString();
            
            if (InvalidNameKeywords.Contains(name))
            {
                return $"_{name}";
            }
            
            return name;
        }
        
        public static void TruncateContent(GUIContent content, Rect rect)
        {
            string originalText = content.text;
            Vector2 dim = GUI.skin.label.CalcSize(content);

            int min = 4;
            int max = originalText.Length;

            int itr = 0;
            if (dim.x > rect.width)
            {
                while (min < max && itr < 20)
                {
                    ++itr;
                    int mid = (min + max + 1) / 2;
                    
                    content.text = originalText.Substring(0,mid) + "...";
                    dim = GUI.skin.label.CalcSize(content);

                    if (dim.x > rect.width)
                    {
                        max = mid - 1;
                    }
                    else
                    {
                        if (mid == min)
                        {
                            break;
                        }
                        min = mid;
                    }
                }

                if (itr > 10)
                {
                    Debug.LogWarning("Infinite binary search!");
                }
                content.text = originalText.Substring(0,min) + "...";
            }
        }

        public static string GetCamelCase(string text)
        {
            return Regex.Replace(text, @"([A-Z])([A-Z]+|[a-z0-9_]+)($|[A-Z]\w*)",
                m => $"{m.Groups[1].Value.ToLower()}{m.Groups[2].Value.ToLower()}{m.Groups[3].Value}");
        }
    }
}
