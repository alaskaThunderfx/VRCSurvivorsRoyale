using System.Collections.Generic;
using System.Text;
using UnityEngine;
using VRC.Udon.Compiler.Compilers;

namespace Cyan.CT.Editor
{
    public class CyanTriggerAssemblyCode
    {
        private readonly Dictionary<string, CyanTriggerAssemblyMethod> _methods;
        private readonly List<string> _orderedMethods;
        private readonly int _updateOrder;

        public CyanTriggerAssemblyCode(int updateOrder = 0)
        {
            _methods = new Dictionary<string, CyanTriggerAssemblyMethod>();
            _orderedMethods = new List<string>();

            _updateOrder = updateOrder;
        }

        public void AddMethod(CyanTriggerAssemblyMethod udonEvent)
        {
            if (_methods.ContainsKey(udonEvent.Name))
            {
                // Duplicate add
                return;
            }
            
            _orderedMethods.Add(udonEvent.Name);
            _methods.Add(udonEvent.Name, udonEvent);
        }

        public bool HasMethod(string eventName)
        {
            return _methods.ContainsKey(eventName);
        }

        public CyanTriggerAssemblyMethod GetMethod(string eventName)
        {
            if (_methods.TryGetValue(eventName, out CyanTriggerAssemblyMethod udonMethod))
            {
                return udonMethod;
            }
            return null;
        }

        public bool GetOrCreateMethod(string eventName, bool export, out CyanTriggerAssemblyMethod udonMethod)
        {
            udonMethod = GetMethod(eventName);
            if (udonMethod == null)
            {
                udonMethod = new CyanTriggerAssemblyMethod(eventName, export);
                AddMethod(udonMethod);
                return true;
            }

            return false;
        }

        public IEnumerable<CyanTriggerAssemblyMethod> GetMethods()
        {
            foreach (var methodName in _orderedMethods)
            {
                yield return _methods[methodName];
            }
        }

        public int GetMethodCount()
        {
            return _methods.Count;
        }

        public void RemoveMethod(string eventName)
        {
            if (_methods.TryGetValue(eventName, out _))
            {
                _methods.Remove(eventName);
                _orderedMethods.Remove(eventName);
            }
        }

        public void Finish()
        {
            foreach(string methodName in _orderedMethods)
            {
                _methods[methodName].Finish();
            }
        }

        public void ApplyAddresses()
        {
            Dictionary<string, uint> methodsToStartAddress = new Dictionary<string, uint>();

            uint curAddress = 0;
            foreach (string eventName in _orderedMethods)
            {
                if (!_methods.TryGetValue(eventName, out var method))
                {
                    Debug.Log($"Method is missing? {eventName}");
                    continue;
                }
                
                curAddress = method.ApplyAddressSize(curAddress);
                methodsToStartAddress.Add(method.Name, method.StartAddress);
            }

            foreach (string eventName in _orderedMethods)
            {
                if (!_methods.TryGetValue(eventName, out _))
                {
                    Debug.Log($"Method is missing? {eventName}");
                    continue;
                }
                _methods[eventName].MapLabelsToAddress(methodsToStartAddress);
            }
        }

        public string Export()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(".code_start");

            if (_updateOrder != 0)
            {
                sb.AppendLine($"  .update_order {_updateOrder}");
            }
            
            foreach (string eventName in _orderedMethods)
            {
                sb.AppendLine(_methods[eventName].ExportMethod());
            }

            sb.AppendLine(".code_end");

            return sb.ToString();
        }

        // TODO option for ignoring vrchat events?
        public CyanTriggerItemTranslation[] AddPrefixToAllMethods(
            string prefixNamespace, 
            CyanTriggerItemTranslation[] variableTranslations)
        {
            List<CyanTriggerItemTranslation> translations = new List<CyanTriggerItemTranslation>();
            
            string variableChangedPrefix = UdonGraphCompiler.GetVariableChangeEventName("");
            
            // Make dictionary of variable names to know if variableChangedEvent rename
            Dictionary<string, string> varChangedEventNames = new Dictionary<string, string>();
            foreach (var translation in variableTranslations)
            {
                varChangedEventNames.Add(
                    UdonGraphCompiler.GetVariableChangeEventName(translation.BaseName), 
                    translation.TranslatedName);
            }

            string networkedNamespace = $"N{prefixNamespace}";

            // Get a list of all methods in order before clearing. Clearing is needed as renaming needs to ensure
            // that no method of the same name already exists in the dictionary that we haven't renamed yet.
            List<CyanTriggerAssemblyMethod> allMethods = new List<CyanTriggerAssemblyMethod>();
            foreach (string eventName in _orderedMethods)
            {
                allMethods.Add(_methods[eventName]);
            }
            _orderedMethods.Clear();
            _methods.Clear();

            for (var index = 0; index < allMethods.Count; index++)
            {
                var method = allMethods[index];
                string eventName = method.Name;

                // Check if the event should be networked or not based on if the first character is an underscore.
                string pref = eventName[0] != '_' ? networkedNamespace : prefixNamespace;

                string newName = $"{pref}_{index}";
                if (varChangedEventNames.TryGetValue(eventName, out string newVarName))
                {
                    newName = UdonGraphCompiler.GetVariableChangeEventName(newVarName);
                }
                else if (eventName.StartsWith(variableChangedPrefix))
                {
                    Debug.Log($"Found var changed event without matching variable: {eventName}");
                }

                method.Name = newName;

                AddMethod(method);
                translations.Add(new CyanTriggerItemTranslation { BaseName = eventName, TranslatedName = newName });
            }

            return translations.ToArray();
        }

        public CyanTriggerAssemblyCode Clone(
            Dictionary<CyanTriggerAssemblyInstruction, CyanTriggerAssemblyInstruction> instructionMapping)
        {
            CyanTriggerAssemblyCode code = new CyanTriggerAssemblyCode();
            foreach (var method in GetMethods())
            {
                var clone = method.Clone();
                code.AddMethod(clone);

                for (int i = 0; i < method.Actions.Count; ++i)
                {
                    instructionMapping.Add(method.Actions[i], clone.Actions[i]);
                }
                
                instructionMapping.Add(method.EndNop, clone.EndNop);
            }

            return code;
        }
        
        public void UpdateMapping(
            Dictionary<CyanTriggerAssemblyInstruction, CyanTriggerAssemblyInstruction> instructionMapping,
            Dictionary<CyanTriggerAssemblyDataType, CyanTriggerAssemblyDataType> variableMapping)
        {
            foreach (var method in GetMethods())
            {
                foreach (var action in method.Actions)
                {
                    action.UpdateMapping(instructionMapping, variableMapping);
                }
            }
        }

        public uint GetUniqueExternCount()
        {
            HashSet<string> uniqueExterns = new HashSet<string>();
            foreach (var method in GetMethods())
            {
                foreach (var action in method.Actions)
                {
                    if (action.GetInstructionType() == CyanTriggerInstructionType.EXTERN)
                    {
                        uniqueExterns.Add(action.GetSignature());
                    }   
                }
            }

            return (uint)uniqueExterns.Count;
        }
    }
}