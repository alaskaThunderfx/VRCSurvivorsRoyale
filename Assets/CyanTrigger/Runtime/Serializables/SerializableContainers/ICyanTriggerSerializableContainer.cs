namespace Cyan.CT
{
    // Odin does not serialize all types properly. This interface allows for creating serializable versions of a class.
    public interface ICyanTriggerSerializableContainer
    {
        object GetObject();
    }
}