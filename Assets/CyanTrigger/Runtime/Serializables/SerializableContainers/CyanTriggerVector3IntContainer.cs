using System;
using UnityEngine;

namespace Cyan.CT
{
    [Serializable]
    public class CyanTriggerVector3IntContainer : ICyanTriggerSerializableContainer
    {
        [SerializeField]
        private int x;
        [SerializeField]
        private int y;
        [SerializeField]
        private int z;

        public static CyanTriggerVector3IntContainer CreateContainer(Vector3Int vector)
        {
            return new CyanTriggerVector3IntContainer(vector);
        }
        
        public CyanTriggerVector3IntContainer(Vector3Int vector)
        {
            x = vector.x;
            y = vector.y;
            z = vector.z;
        }

        public object GetObject()
        {
            return new Vector3Int(x, y, z);
        }
    }
}