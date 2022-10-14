
namespace Cyan.CT.Editor
{
    public abstract class CyanTriggerCustomUdonActionNodeDefinition : CyanTriggerCustomUdonNodeDefinition
    {
        public override string GetBaseMethodName(UnityEditor.SerializedProperty eventProperty)
        {
            throw new System.NotImplementedException();
        }
        
        public override string GetBaseMethodName(CyanTriggerEvent evt)
        {
            throw new System.NotImplementedException();
        }
        
        public override bool GetBaseMethod(
            CyanTriggerAssemblyProgram program, 
            CyanTriggerActionInstance actionInstance,
            out CyanTriggerAssemblyMethod method)
        {
            throw new System.NotImplementedException();
        }

        public override CyanTriggerAssemblyMethod AddEventToProgram(CyanTriggerCompileState compileState)
        {
            throw new System.NotImplementedException();
        }
    }
}
