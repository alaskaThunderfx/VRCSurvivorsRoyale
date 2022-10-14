using System;
using UnityEngine;

namespace Cyan.CT
{
    [Serializable]
    public class CyanTriggerVector2IntContainer : ICyanTriggerSerializableContainer
    {
        [SerializeField]
        private int x;
        [SerializeField]
        private int y;

        public static CyanTriggerVector2IntContainer CreateContainer(Vector2Int vector)
        {
            return new CyanTriggerVector2IntContainer(vector);
        }
        
        public CyanTriggerVector2IntContainer(Vector2Int vector)
        {
            x = vector.x;
            y = vector.y;
        }

        public object GetObject()
        {
            return new Vector2Int(x, y);
        }
    }
}