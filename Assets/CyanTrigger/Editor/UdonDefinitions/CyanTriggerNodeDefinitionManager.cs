
using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Editor;
using VRC.Udon.Graph;
using Debug = UnityEngine.Debug;

#if CYAN_TRIGGER_DEBUG
using System.Diagnostics;
using UnityEngine.Profiling;
#endif

namespace Cyan.CT.Editor
{
    public class CyanTriggerNodeDefinitionManager
    {
        public CyanTriggerNodeDefinitionTrie Root;
        private readonly Dictionary<string, CyanTriggerNodeDefinition> _definitions = 
            new Dictionary<string, CyanTriggerNodeDefinition>();
        private readonly Dictionary<string, CyanTriggerCustomUdonNodeDefinition> _customNodes =
            new Dictionary<string, CyanTriggerCustomUdonNodeDefinition>();
        private readonly HashSet<string> _scopedDefinitions = new HashSet<string>();
        private readonly Dictionary<string, Type> _componentTypes = new Dictionary<string, Type>();

        private readonly Dictionary<string, Type> _typeNamesToTypes = new Dictionary<string, Type>();


        private static readonly HashSet<string> IgnoredDefinitions = new HashSet<string>(
            new[]
            {
                // Events replaced with special versions
                "Event_OnVariableChange", // CyanTrigger's version is called Event_OnVariableChanged (note the 'd' at the end)
                
                // General actions replaced with special versions
                "VRCUdonCommonInterfacesIUdonEventReceiver.__SendCustomEvent__SystemString__SystemVoid", // Replace self items with jump in code
                
                // Special nodes replaced with CyanTrigger versions
                "Block",
                "Branch",
                "Comment",
                "For",
                "While",
                "Get_Variable",
                "Set_Variable",
                "Set_ReturnValue",
                "Is_Valid",
            });
        
        private static readonly HashSet<string> HiddenDefinitions = new HashSet<string>(
            new [] {
                "CyanTriggerSpecial_BlockEnd",
                "CyanTriggerSpecial_Condition",
                "CyanTriggerSpecial_ConditionBody",
                "CyanTrigger.__SendCustomEvent__CyanTrigger__SystemString", // Replaced with UdonBehaviour.SendCustomEvent
            });
        
        private static readonly HashSet<string> PreventDirectChildrenDefinitions = new HashSet<string>(
            new [] {
                "CyanTriggerSpecial_If",
                "CyanTriggerSpecial_ElseIf",
                "CyanTriggerSpecial_While",
            });
        
        private static readonly object Lock = new object();

        private static CyanTriggerNodeDefinitionManager _instance;
        public static CyanTriggerNodeDefinitionManager Instance
        {
            get
            {
                lock (Lock)
                {
                    if (_instance == null)
                    {
                        _instance = new CyanTriggerNodeDefinitionManager();
                    }
                    return _instance;
                }
            }
        }
        
        private CyanTriggerNodeDefinitionManager()
        {
#if CYAN_TRIGGER_DEBUG
            Profiler.BeginSample("CyanTriggerNodeDefinitionManager.ProcessDefinitions");
            Stopwatch sw = new Stopwatch();
            sw.Start();
#endif
            
            ProcessDefinitions();
  
#if CYAN_TRIGGER_DEBUG          
            sw.Stop();
            Debug.Log($"ProcessDefinitions Time: {sw.Elapsed.TotalSeconds} ");
            Profiler.EndSample();
#endif
        }

        private void ProcessDefinitions()
        {
            Root = new CyanTriggerNodeDefinitionTrie();
            
            AddCustomNodeDefinitions();

            // Process input and output types to figure out all variable types allowed in udon
            HashSet<Type> allTypes = new HashSet<Type>();
            Type componentType = typeof(Component);
            void AddType(Type type)
            {
                if (type == null || allTypes.Contains(type) || type.IsGenericType || type.IsByRef)
                {
                    return;
                }

                allTypes.Add(type);
                
                AddCustomNodeDefinition(new CyanTriggerCustomNodeSetVariable(type));
                
#pragma warning disable CS0618
                // Type is obsolete, but add it anyway to prevent breaking backwards compatibility.
                AddCustomNodeDefinition(new CyanTriggerCustomNodeVariable(type));
#pragma warning restore CS0618

                if (type.IsEnum)
                {
                    AddCustomNodeDefinition(new CyanTriggerCustomNodeEnumToBaseType(type));
                    // TODO when VRChat supports converting/casting to enums, add <Type>ToEnum
                }

                if (type.IsSubclassOf(componentType) || type == componentType)
                {
                    _componentTypes.Add(type.Name, type);
                }
                
                _typeNamesToTypes.Add(CyanTriggerNameHelpers.GetTypeFriendlyName(type), type);
            }

            CyanTriggerNodeDefinition AddTypeButSkipDefinition(UdonNodeDefinition definition)
            {
                AddType(definition.type);
                return null;
            }
            CyanTriggerNodeDefinition CreateCustomType(UdonNodeDefinition definition)
            {
                return AddCustomNodeDefinition(new CyanTriggerCustomNodeType(definition));
            }
            
            // Special handling of different types to speedup string.StartsWith
            CyanTriggerTrie<char, Func<UdonNodeDefinition, CyanTriggerNodeDefinition>> definitionTypeHandling =
                new CyanTriggerTrie<char, Func<UdonNodeDefinition, CyanTriggerNodeDefinition>>();
            
            // We do not care about Const_ or Variable_ definitions; add the type, but skip the definition
            definitionTypeHandling.AddToTrie("Const_", AddTypeButSkipDefinition);
            definitionTypeHandling.AddToTrie("Variable_", AddTypeButSkipDefinition);
            
            // Type_ nodes should be converted to CyanTrigger special types.
            definitionTypeHandling.AddToTrie("Type_", CreateCustomType);

            IEnumerable<UdonNodeDefinition> definitions = UdonEditorManager.Instance.GetNodeDefinitions();
            foreach (var definition in definitions)
            {
                CyanTriggerNodeDefinition nodeDef;
                if (definitionTypeHandling.GetFromTrie(definition.fullName, out var handler, true))
                {
                    nodeDef = handler(definition);
                }
                else
                {
                    nodeDef = AddUdonNodeDefinition(definition);
                }

                if (nodeDef == null)
                {
                    continue;
                }

                foreach (var type in nodeDef.GetUsedTypes())
                {
                    AddType(type);
                }
            }

            // Force add this so that you can have a CyanTrigger variable type.
            // TODO figure out implications of having this as it may make future work with parameters difficult.
            // AddType(typeof(CyanTrigger));
            
            // Nodes only include type IUdonEventReceiver and not UdonBehaviour directly.
            Type udonType = typeof(UdonBehaviour);
            _componentTypes.Add(udonType.Name, udonType);
            udonType = typeof(IUdonEventReceiver);
            _componentTypes.Add(udonType.Name, udonType);
            
            Root.FinishCreation();
        }

        private void AddCustomNodeDefinitions()
        {
            Type codeAssetGeneratorType = typeof(CyanTriggerCustomUdonNodeDefinition);
            foreach (var type in codeAssetGeneratorType.Assembly.GetTypes())
            {
                if (codeAssetGeneratorType.IsAssignableFrom(type) && 
                    !type.IsAbstract && 
                    type.GetConstructor(Type.EmptyTypes) != null) // ignores variable type
                {
                    CyanTriggerCustomUdonNodeDefinition customDefinition = 
                        (CyanTriggerCustomUdonNodeDefinition)Activator.CreateInstance(type);
                    AddCustomNodeDefinition(customDefinition);
                }
            }
        }

        private CyanTriggerNodeDefinition AddCustomNodeDefinition(CyanTriggerCustomUdonNodeDefinition customDefinition)
        {
            UdonNodeDefinition definition = customDefinition.GetNodeDefinition();
            // Some Type_ nodes are repeated. 
            if (_customNodes.ContainsKey(definition.fullName))
            {
// #if CYAN_TRIGGER_DEBUG
//                 if (!definition.fullName.StartsWith("Type_"))
//                 {
//                     Debug.LogWarning("Custom node already contains node for " +definition.type +" " +definition.fullName);                    
//                 }
// #endif
                return null;
            }
            
            _customNodes.Add(definition.fullName, customDefinition);
            var def = ProcessCyanTriggerNodeDefinition(new CyanTriggerNodeDefinition(customDefinition));

            if (customDefinition is ICyanTriggerCustomNodeScope)
            {
                _scopedDefinitions.Add(definition.fullName);
            }

            return def;
        }
        
        private CyanTriggerNodeDefinition AddUdonNodeDefinition(UdonNodeDefinition definition)
        {
            if (IgnoredDefinitions.Contains(definition.fullName))
            {
                return null;
            }
            
            CyanTriggerNodeDefinition nodeDef = new CyanTriggerNodeDefinition(definition);

            if (nodeDef.DefinitionType == CyanTriggerNodeDefinition.UdonDefinitionType.VrcSpecial
                || nodeDef.DefinitionType == CyanTriggerNodeDefinition.UdonDefinitionType.None)
            {
                //UnityEngine.Debug.LogWarning("Ignoring " + nodeDef.definitionType + " definition: "+ nodeDef.fullName);
                return null;
            }

            return ProcessCyanTriggerNodeDefinition(nodeDef);
        }
        
        private CyanTriggerNodeDefinition ProcessCyanTriggerNodeDefinition(CyanTriggerNodeDefinition nodeDefinition)
        {
            if (!_definitions.ContainsKey(nodeDefinition.FullName))
            {
                _definitions.Add(nodeDefinition.FullName, nodeDefinition);
            }
            // else
            // {
            //     Debug.Log("Duplicate found: " + nodeDefinition.fullName + " " + Definitions[nodeDefinition.fullName].fullName);
            // }
            
            // Add to trie
            Root.AddToTrie(nodeDefinition.GetTrieCategories(), nodeDefinition);

            return nodeDefinition;
        }

        public CyanTriggerNodeDefinition GetDefinition(string name)
        {
            _definitions.TryGetValue(name, out CyanTriggerNodeDefinition ret);
            return ret;
        }

        public bool TryGetDefinitionFromCompiledName(string compiledName, out CyanTriggerNodeDefinition definition)
        {
            if (string.IsNullOrEmpty(compiledName) 
                || compiledName.Length < 2 
                || compiledName[0] != '_' 
                || !char.IsLower(compiledName[1]))
            {
                definition = null;
                return false;
            }
            
            string definitionName = $"Event_{char.ToUpper(compiledName[1])}{compiledName.Substring(2)}";
            return _definitions.TryGetValue(definitionName, out definition);
        }

        public IEnumerable<CyanTriggerNodeDefinition> GetEventDefinitions()
        {
            return GetDefinitions(CyanTriggerNodeDefinition.UdonDefinitionType.Event);
        }
        
        public IEnumerable<CyanTriggerNodeDefinition> GetVariableDefinitions()
        {
            return GetDefinitions(CyanTriggerNodeDefinition.UdonDefinitionType.CyanTriggerVariable);
        }
        
        private IEnumerable<CyanTriggerNodeDefinition> GetDefinitions(
            CyanTriggerNodeDefinition.UdonDefinitionType definitionType)
        {
            var options = (CyanTriggerNodeDefinitionTrie)Root.Get(definitionType.ToString());
            foreach (var node in options.GetOptions())
            {
                // Everything has one level nesting now...
                var trieOption = node.GetOptions()[0];
                
                if (trieOption.TryGetData(out var udonDef))
                {
                    yield return udonDef;
                }
            }
        }

        public IEnumerable<CyanTriggerNodeDefinition> GetDefinitions()
        {
            return GetDefinitions(Root);
        }

        private static IEnumerable<CyanTriggerNodeDefinition> GetDefinitions(CyanTriggerNodeDefinitionTrie nodeDefinition)
        {
            var allOptions = nodeDefinition.GetOptions();
            foreach (var node in allOptions)
            {
                if (node.TryGetData(out var udonDef))
                {
                    yield return udonDef;
                }
            }
            
            foreach (var node in allOptions)
            {
                foreach (var def in GetDefinitions(node))
                {
                    yield return def;
                }
            }
        }

        public bool TryGetCustomDefinition(string definitionName, out CyanTriggerCustomUdonNodeDefinition customDefinition)
        {
            return _customNodes.TryGetValue(definitionName, out customDefinition);
        }

        public bool DefinitionHasScope(string definitionName)
        {
            return _scopedDefinitions.Contains(definitionName);
        }
        
        public static bool DefinitionIsHidden(string definitionName)
        {
            return HiddenDefinitions.Contains(definitionName);
        }
        
        public static bool DefinitionPreventsDirectChildren(string definitionName)
        {
            return PreventDirectChildrenDefinitions.Contains(definitionName);
        }

        public bool TryGetDirectComponentType(string componentName, out Type componentType)
        {
            return _componentTypes.TryGetValue(componentName, out componentType);
        }

        public bool TryGetComponentType(string componentName, out Type componentType)
        {
            int lastDot = componentName.LastIndexOf('.');
            if (lastDot != -1)
            {
                componentName = componentName.Substring(lastDot + 1);
            }
            componentName = componentName.Replace(" ", "");

            return TryGetDirectComponentType(componentName, out componentType);
        }

        public bool TryGetTypeFromFriendlyName(string typeName, out Type type)
        {
            return _typeNamesToTypes.TryGetValue(typeName, out type);
        }
    }
    
    public class CyanTriggerNodeDefinitionTrie : 
        CyanTriggerTrie<string, CyanTriggerNodeDefinition>, 
        IComparable<CyanTriggerNodeDefinitionTrie>
    {
        public readonly string Name;
        private CyanTriggerNodeDefinitionTrie[] _childrenElements;
        
        // Root constructor
        public CyanTriggerNodeDefinitionTrie() : this("") { }
        
        private CyanTriggerNodeDefinitionTrie(string name)
        {
            Name = name;
        }
        
        protected override CyanTriggerTrie<string, CyanTriggerNodeDefinition> CreateChild(string key)
        {
            return new CyanTriggerNodeDefinitionTrie(key);
        }

        public CyanTriggerNodeDefinitionTrie[] GetOptions()
        {
            return _childrenElements;
        }

        public bool HasOptions()
        {
            return _childrenElements.Length > 0;
        }

        public void FinishCreation()
        {
            FinishCreationInternal(true);
        }
        
        private void FinishCreationInternal(bool root)
        {
            // Items will be sorted before displaying. This list will remain unsorted.
            _childrenElements = new CyanTriggerNodeDefinitionTrie[Children.Count];
            int index = 0;
            foreach (var cyanTriggerTrie in Children.Values)
            {
                var child = (CyanTriggerNodeDefinitionTrie)cyanTriggerTrie;
                _childrenElements[index] = child;
                ++index;
                
                child.FinishCreationInternal(false);
            }

            // Cleanup memory
            if (!root)
            {
                Children.Clear();
            }
        }

        public int CompareTo(CyanTriggerNodeDefinitionTrie other)
        {
            return String.Compare(Name, other.Name, StringComparison.Ordinal);
        }
    }
}
