using UnityEngine;

namespace Cyan.CT.Editor
{
    public interface ICyanTriggerBaseEditor
    {
        void Repaint();
        void OnChange();
        Object GetTarget();
        bool IsSceneTrigger();
        CyanTriggerProgramAsset GetProgram();
    }
}