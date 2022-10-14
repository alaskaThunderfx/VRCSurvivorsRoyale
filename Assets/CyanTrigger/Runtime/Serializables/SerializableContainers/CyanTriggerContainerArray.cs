using System;
using UnityEngine;

namespace Cyan.CT
{
    [Serializable]
    public class CyanTriggerContainerArray<TBaseType, TContainerType> 
        : ICyanTriggerSerializableContainer where TContainerType : ICyanTriggerSerializableContainer
    {
        [SerializeField] 
        private TContainerType[] container;
        
        public CyanTriggerContainerArray(TBaseType[] items, Func<TBaseType, TContainerType> convertMethod)
        {
            int length = items.Length;
            container = new TContainerType[length];
            for (int index = 0; index < length; ++index)
            {
                container[index] = convertMethod(items[index]);
            }
        }

        public object GetObject()
        {
            int length = container.Length;
            TBaseType[] results = new TBaseType[length];
            for (int index = 0; index < length; ++index)
            {
                results[index] = (TBaseType)container[index]?.GetObject();
            }

            return results;
        }
    }
}