using System;
using System.Text;

namespace Cyan.CT
{
    [Serializable]
    public class CyanTriggerEventArgData
    {
        public string eventName;
        public string eventDisplayName;
        public string[] variableNames = Array.Empty<string>();
        public string[] variableUdonNames = Array.Empty<string>();
        public bool[] variableOuts = Array.Empty<bool>();
        // Variable will be serialized with Odin
        // ReSharper disable once InconsistentNaming
        public Type[] variableTypes = Array.Empty<Type>();

        public override string ToString()
        {
            return GetUniqueString(true);
        }

        public string GetUniqueString(bool addVarNames)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(eventName);

            if (variableTypes.Length > 0)
            {
                sb.Append('(');

                for (int index = 0; index < variableTypes.Length; ++index)
                {
                    if (index > 0)
                    {
                        sb.Append(", ");
                    }
                    if (variableOuts[index])
                    {
                        sb.Append("out ");
                    }
                    sb.Append(CyanTriggerNameHelpers.GetTypeFriendlyName(variableTypes[index]));
                
                    if (addVarNames)
                    {
                        sb.Append(" ");
                        sb.Append(variableUdonNames[index]);
                    }
                }
            
                sb.Append(')');
            }
            
            return sb.ToString();
        }

        public bool Equals(CyanTriggerEventArgData argData)
        {
            if (argData == null)
            {
                return false;
            }
            
            if (!eventName.Equals(argData.eventName))
            {
                return false;
            }

            int size = variableNames.Length;
            if (argData.variableNames.Length != size
                || argData.variableUdonNames.Length != size
                || argData.variableOuts.Length != size
                || argData.variableTypes.Length != size)
            {
                return false;
            }

            for (int index = 0; index < size; ++index)
            {
                if (argData.variableNames[index] != variableNames[index]
                    || argData.variableUdonNames[index] != variableUdonNames[index]
                    || argData.variableOuts[index] != variableOuts[index]
                    || argData.variableTypes[index] != variableTypes[index])
                {
                    return false;
                }
            }

            return true;
        }
    }
}