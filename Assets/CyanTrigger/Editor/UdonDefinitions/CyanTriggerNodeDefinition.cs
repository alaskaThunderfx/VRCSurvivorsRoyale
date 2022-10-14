using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public class CyanTriggerNodeDefinition
    {
        private static readonly CyanTriggerTrie<char, UdonDefinitionType> UdonDefTypeTrie;

        public enum UdonDefinitionType
        {
            None = -1,
            Variable,
            Type,
            Event,
            Method,
            VrcSpecial,
            CyanTriggerSpecial, // Used for special or control nodes
            CyanTriggerVariable,
        }

        static CyanTriggerNodeDefinition()
        {
            UdonDefTypeTrie = new CyanTriggerTrie<char, UdonDefinitionType>();
            
            // -1 to not include None
            int totalDefinitionTypes = Enum.GetNames(typeof(UdonDefinitionType)).Length-1;
            
            for (int i = 0; i < totalDefinitionTypes; ++i)
            {
                UdonDefinitionType defType = (UdonDefinitionType)i;
                string defTypeString = defType.ToString();
                UdonDefTypeTrie.AddToTrie(defTypeString, defType);
            }
        }
        
        
        public readonly string FullName;
        public readonly string MethodName;
        public readonly Type BaseType;

        public readonly UdonDefinitionType DefinitionType = UdonDefinitionType.None;
        public readonly UdonNodeDefinition Definition;
        public readonly CyanTriggerCustomUdonNodeDefinition CustomDefinition;
        public readonly string[] TypeCategories;

        private string _methodDisplayNameCached;
        
        // Lazy load and cache documentation url since VRChat's method of checking is slow.
        private bool _documentationInitialized;
        private string _documentationUrl;
        private string DocumentationUrl
        {
            get
            {
                if (_documentationInitialized)
                {
                    return _documentationUrl;
                }
                _documentationInitialized = true;
                
                if (CustomDefinition != null)
                {
                    if (CustomDefinition.HasDocumentation())
                    {
                        _documentationUrl = CustomDefinition.GetDocumentationLink();
                    }
                }
                else if (!CyanTriggerDocumentationLinks.DefinitionToDocumentation.TryGetValue(FullName,
                             out _documentationUrl) 
                         && UdonGraphExtensions.ShouldShowDocumentationLink(Definition))
                {
                    try
                    {
                        _documentationUrl = UdonGraphExtensions.GetDocumentationLink(Definition);
                    }
                    // Udon's documentation fails on multiple nodes. Try getting it from the base type directly
                    catch (Exception)
                    {
                        // Disable warning for use of Obsolete type CyanTriggerCustomNodeVariable
#pragma warning disable CS0618
                        var typeDefinition = CyanTriggerNodeDefinitionManager.Instance.GetDefinition(
                            CyanTriggerCustomNodeVariable.GetFullnameForType(BaseType));
#pragma warning restore CS0618
                        _documentationUrl = typeDefinition.DocumentationUrl;
                    }
                }
                
                return _documentationUrl;
            }
        }

        private string _typeFriendlyName;
        public string TypeFriendlyName
        {
            get
            {
                if (string.IsNullOrEmpty(_typeFriendlyName))
                {
                    _typeFriendlyName = CyanTriggerNameHelpers.GetTypeFriendlyName(BaseType); 
                }
                return _typeFriendlyName;
            }
        }

        private CyanTriggerActionVariableDefinition[] _variableDefinitions;
        public CyanTriggerActionVariableDefinition[] VariableDefinitions
        {
            get
            {
                if (_variableDefinitions == null)
                {
                    if (CustomDefinition is ICyanTriggerCustomNodeCustomVariableSettings customWithVariables)
                    {
                        _variableDefinitions = customWithVariables.GetCustomVariableSettings();
                    }
                    else
                    {
                        _variableDefinitions = GetVariableDefinitions();
                    }
                }
                return _variableDefinitions;
            }
        }

        public CyanTriggerNodeDefinition(CyanTriggerCustomUdonNodeDefinition customDefinition)
        {
            Definition = customDefinition.GetNodeDefinition();
            CustomDefinition = customDefinition;
            FullName = Definition.fullName;
            
            DefinitionType = customDefinition.GetDefinitionType();

            BaseType = DefinitionType == UdonDefinitionType.Type 
                ?  ((CyanTriggerCustomNodeType)customDefinition).GetBaseType()
                : Definition.type;

            MethodName = customDefinition.GetDisplayName();

            if (DefinitionType == UdonDefinitionType.Method)
            {
                TypeCategories = GetMethodTypeCategories();
            }
            else
            {
                TypeCategories = new [] { DefinitionType.ToString(), MethodName, FullName };
            }
            
            // TODO figure out display names?
        }
        
        public CyanTriggerNodeDefinition(UdonNodeDefinition definition)
        {
            Definition = definition;
            FullName = definition.fullName;
            BaseType = definition.type;
            
            // This shouldn't happen, but in case the sdk updates and does have it happen, log error in debug,
            // but prevent it from throwing actual errors for users.
            if (BaseType == null)
            {
                DefinitionType = UdonDefinitionType.None;
#if CYAN_TRIGGER_DEBUG
                Debug.LogError($"Empty base Type! {FullName}");
#endif
                return;
            }
            
            int periodLoc = FullName.IndexOf('.');
            // Special nodes will not include a period in the name. Use this to know to search for them.
            if (periodLoc == -1)
            {
                if (UdonDefTypeTrie.GetFromTrie(FullName, out UdonDefinitionType def, true))
                {
                    DefinitionType = def;
                }
#if CYAN_TRIGGER_DEBUG
                else
                {
                    Debug.Log($"Failed to find: {FullName}");
                }
#endif
            }

            if (DefinitionType == UdonDefinitionType.Event)
            {
                MethodName = definition.name;
                TypeCategories = new [] { DefinitionType.ToString(), MethodName, FullName };
                return;
            }
            
            // Special Definitions should be handled at this point. Log an error if the sdk adds new items that fail this.
            if (periodLoc == -1)
            {
#if CYAN_TRIGGER_DEBUG
                Debug.LogError($"Failed Special Type: {FullName}");
#endif
                return;
            }

            // Methods will not start with "Method_" but instead start with the class type.
            // At this point they will not have a definition type, so assign them to the method group.
            DefinitionType = UdonDefinitionType.Method;

            if (BaseType == typeof(UnityEngine.Object) && FullName.StartsWith("VRCInstantiate"))
            {
                TypeCategories = new [] { "VRC", "Instantiate", FullName };
                MethodName = "Instantiate";
                _typeFriendlyName = "VRCInstantiate";
                return;
            }

            // Get the method name and type categories.
            string defName = definition.name;
            int spaceLoc = defName.IndexOf(' ');
            // "Unity Object" instead of "Object"
            if (BaseType == typeof(UnityEngine.Object) && spaceLoc == 5)
            {
                spaceLoc = 12;
            }
            
            // Method names are "<Type> methodName"
            // Take advantage of the space and use substring to get the name directly.
            string name = defName.Substring(spaceLoc + 1);
            name = CyanTriggerNameHelpers.GetMethodFriendlyName(name);
            MethodName = name;

            TypeCategories = GetMethodTypeCategories();
        }

        private string[] GetMethodTypeCategories()
        {
            string[] baseTypeCategories = CyanTriggerNameHelpers.GetTypeCategories(BaseType);
            int size = baseTypeCategories.Length;
            string[] categories = new string[size + 2];
            for (int i = 0; i < size; ++i)
            {
                categories[i] = baseTypeCategories[i];
            }
            categories[size] = MethodName;
            categories[size + 1] = FullName;

            return categories;
        }

        private CyanTriggerActionVariableDefinition[] GetVariableDefinitions()
        {
            // Process parameters
            List<CyanTriggerActionVariableDefinition> varDefinitions = new List<CyanTriggerActionVariableDefinition>();

            int outParams = 0;
            foreach (var parameter in Definition.parameters)
            {
                var variableDef = new CyanTriggerActionVariableDefinition
                {
                    type = new CyanTriggerSerializableType(parameter.type == null ? typeof(object) : parameter.type),
                    udonName = parameter.name,
                };
                if (!string.IsNullOrEmpty(parameter.name))
                {
                    variableDef.displayName = Regex.Replace(parameter.name, "(\\B[A-Z])", " $1").Trim();
                    if (variableDef.displayName == "instance")
                    {
                        variableDef.displayName = CyanTriggerNameHelpers.GetTypeFriendlyName(parameter.type);
                    }
                }
                
                if (parameter.parameterType == UdonNodeParameter.ParameterType.IN)
                {
                    variableDef.variableType = CyanTriggerActionVariableTypeDefinition.Constant |
                                               CyanTriggerActionVariableTypeDefinition.VariableInput;
                }
                else if (parameter.parameterType == UdonNodeParameter.ParameterType.OUT)
                {
                    variableDef.variableType = CyanTriggerActionVariableTypeDefinition.VariableOutput |
                                               CyanTriggerActionVariableTypeDefinition.VariableInput;

                    ++outParams;
                }
                else
                {
                    Type paramType = parameter.type;
                    if (!paramType.IsArray)
                    {
                        paramType = paramType.MakeByRefType();
                        variableDef.type = new CyanTriggerSerializableType(paramType);
                    }
                    
                    variableDef.variableType = CyanTriggerActionVariableTypeDefinition.VariableOutput |
                                               CyanTriggerActionVariableTypeDefinition.VariableInput;
                    
                    ++outParams;
                }
                
                if (DefinitionType == UdonDefinitionType.Event)
                {
                    // Don't add output variables for events.
                    if (parameter.parameterType != UdonNodeParameter.ParameterType.IN)
                    {
                        continue;
                    }
                    
                    // Special case for handling OnVariableChanged...
                    variableDef.variableType = 
                        parameter.type == typeof(CyanTriggerVariable) ? 
                            CyanTriggerActionVariableTypeDefinition.VariableInput :
                            CyanTriggerActionVariableTypeDefinition.Constant;
                }
                
                varDefinitions.Add(variableDef);
            }
            
            if (outParams == 0 && Definition.parameters.Count > 0 && Definition.parameters[0].name == "instance" && !BaseType.IsArray)
            {
                varDefinitions[0].variableType |= CyanTriggerActionVariableTypeDefinition.AllowsMultiple;
            }

            // Moving parameters will break things!
            // if (outParams == 1)
            // {
            //     var outParam = varDefinitions[multiIndex];
            //     varDefinitions.RemoveAt(multiIndex);
            //     varDefinitions.Insert(0, outParam);
            //     outParam.variableType |= CyanTriggerActionVariableTypeDefinition.AllowsMultiple;
            // }
            
            return varDefinitions.ToArray();
        }

        public IEnumerable<Type> GetUsedTypes()
        {
            if (BaseType != typeof(void))
            {
                yield return BaseType;
            }
            
            foreach (var parameter in Definition.parameters)
            {
                yield return parameter.type;
            }
        }

        public static Type GetFixedType(UdonNodeDefinition typeDefinition)
        {
            Type returnType = typeDefinition.type;

            // TODO find a more generic way to fix this...
            // IUdonEventReceiver types are Object, and need to be set properly to the expected type.
            Type obj = typeof(UnityEngine.Object);
            if ((returnType == obj || returnType.IsArray && returnType.GetElementType() == obj)
                && typeDefinition.fullName.StartsWith("Type_VRCUdonCommonInterfacesIUdonEventReceive"))
            {
                returnType = returnType.IsArray ? typeof(IUdonEventReceiver[]) : typeof(IUdonEventReceiver);
            }

            return returnType;
        }

        public string[] GetTrieCategories()
        {
            return TypeCategories;
        }

        public bool HasDocumentation()
        {
            if (!string.IsNullOrEmpty(DocumentationUrl))
            {
                return true;
            }

            return false;
        }

        public string GetDocumentationLink()
        {
            return DocumentationUrl;
        }
        
        public string GetMethodDisplayName()
        {
            if (!string.IsNullOrEmpty(_methodDisplayNameCached))
            {
                return _methodDisplayNameCached;
            }
            
            StringBuilder sb = new StringBuilder();

            sb.Append(MethodName);
            sb.Append('(');

            int count = 0;
            var parameters = Definition.parameters;
            foreach (var parameter in parameters)
            {
                Type type = parameter.type;
                bool output = parameter.parameterType == UdonNodeParameter.ParameterType.OUT;
                string friendlyName = CyanTriggerNameHelpers.GetTypeFriendlyName(type);
               
                if (count > 0)
                {
                    sb.Append(", ");
                }
                if (output)
                {
                    sb.Append("out ");
                }
                sb.Append(friendlyName);
                ++count;
            }

            sb.Append(')');

            return _methodDisplayNameCached = sb.ToString();
        }
        
        public string GetActionDisplayName(bool withColor = false)
        {
            if (DefinitionType == UdonDefinitionType.CyanTriggerSpecial)
            {
                return MethodName.Colorize(CyanTriggerColorTheme.SpecialAction, withColor);
            }
            
            if (DefinitionType == UdonDefinitionType.CyanTriggerVariable)
            {
                return TypeFriendlyName.Colorize(CyanTriggerColorTheme.UdonTypeName, withColor);
            }

            string period = ".".Colorize(CyanTriggerColorTheme.Punctuation, withColor);
            if (DefinitionType == UdonDefinitionType.Type)
            {
                string typeName = TypeFriendlyName.Colorize(CyanTriggerColorTheme.UdonTypeName, withColor);
                string typeLiteral = "Type".Colorize(CyanTriggerColorTheme.ActionName, withColor);
                return $"{typeName}{period}{typeLiteral}";
            }
            
            StringBuilder sb = new StringBuilder();

            if (BaseType != null && BaseType != typeof(void))
            {
                sb.Append(TypeFriendlyName.Colorize(CyanTriggerColorTheme.UdonTypeName, withColor));
                sb.Append(period);
            }
            
            sb.Append(MethodName.Colorize(CyanTriggerColorTheme.ActionName, withColor));

            return sb.ToString();
        }
        
        #region Event methods

        public string GetEventName()
        {
            if (DefinitionType != UdonDefinitionType.Event)
            {
                return "";
            }

            // "Event_<eventName>"
            string eventName = FullName.Substring(6);

            return $"_{char.ToLower(eventName[0])}{eventName.Substring(1)}";
        }

        public List<(string, Type)> GetEventVariables(int mask = 6 /* out | in_out */)
        {
            List<(string, Type)> outputs = new List<(string, Type)>();

            if (DefinitionType != UdonDefinitionType.Event)
            {
                return outputs;
            }

            // Remove the underscore
            string eventName = GetEventName().Substring(1);

            // if (eventName.Equals("custom"))
            // {
            //     return outputs;
            // }

            foreach (var parameter in Definition.parameters)
            {
                if (((1 << (int)parameter.parameterType) & mask) == 0)
                {
                    continue;
                }
                
                string paramName;
                if (!string.IsNullOrEmpty(parameter.name))
                {
                    paramName = $"{eventName}{char.ToUpper(parameter.name[0])}{parameter.name.Substring(1)}";
                }
                else
                {
                    paramName = $"{eventName}_parameter";
                }
                outputs.Add((paramName, parameter.type));
            }

            return outputs;
        }

        #endregion
    }
}