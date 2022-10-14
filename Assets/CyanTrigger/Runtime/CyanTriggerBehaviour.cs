using System.Collections;
using UnityEngine;
using VRC.Udon;

namespace Cyan.CT
{
    public abstract class CyanTriggerBehaviour : MonoBehaviour, ICyanTrigger
    {
        public abstract CyanTriggerDataInstance GetCyanTriggerData();
        public abstract CyanTriggerDataInstance GetCopyOfCyanTriggerData();
        public abstract UdonBehaviour GetUdonBehaviour();
        public abstract void SetVariablePublicValues(object[] variables);

        public void SetVariablePublicValuesInPlaymode(object[] variables)
        {
            var udon = GetUdonBehaviour();
            if (udon == null)
            {
                return;
            }

            var triggerData = GetCyanTriggerData();
            var variableData = triggerData?.variables;
            if (variableData == null)
            {
                return;
            }
            
            for (int index = 0; index < variables.Length && index < variableData.Length; ++index)
            {
                udon.SetProgramVariable(variableData[index].name, variables[index]);
            }
        }
    }
}