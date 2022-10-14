using System;

namespace Cyan.CT
{
    [Serializable]
    public class CyanTriggerSerializableType
    {
        public string typeDef; 
        private Type _type;

        public CyanTriggerSerializableType() {}

        public CyanTriggerSerializableType(Type type)
        {
            _type = type;
            typeDef = type.AssemblyQualifiedName;
        }

        public Type Type
        {
            get
            {
                DeserializeType();
                return _type;
            }
        }

        private void DeserializeType()
        {
            if (!string.IsNullOrEmpty(typeDef))
            {
                _type = GetTypeFromDef();
            }
        }

        private Type GetTypeFromDef()
        {
            return Type.GetType(typeDef);
        }
    }
}
