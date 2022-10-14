using UnityEditor;
using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public abstract class CyanTriggerCustomUdonNodeDefinition
    {
        public abstract UdonNodeDefinition GetNodeDefinition();

        public abstract CyanTriggerNodeDefinition.UdonDefinitionType GetDefinitionType();
        public abstract string GetDisplayName();
        
        public abstract string GetBaseMethodName(SerializedProperty eventProperty);
        
        public abstract string GetBaseMethodName(CyanTriggerEvent evt);

        public abstract string GetDocumentationLink();

        public virtual bool HasDocumentation()
        {
            return true;
        }
        
        public abstract bool GetBaseMethod(
            CyanTriggerAssemblyProgram program,
            CyanTriggerActionInstance actionInstance,
            out CyanTriggerAssemblyMethod method);
        
        public abstract CyanTriggerAssemblyMethod AddEventToProgram(CyanTriggerCompileState compileState);
        public abstract void AddActionToProgram(CyanTriggerCompileState compileState);
    }
}
