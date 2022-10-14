using VRC.Udon.Graph;

namespace Cyan.CT.Editor
{
    public interface ICyanTriggerCustomNodeDependency
    {
        UdonNodeDefinition[] GetDependentNodes();
    }
}