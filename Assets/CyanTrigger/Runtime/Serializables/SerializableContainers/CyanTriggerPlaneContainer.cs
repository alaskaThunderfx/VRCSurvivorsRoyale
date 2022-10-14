using System;
using UnityEngine;

namespace Cyan.CT
{
    [Serializable]
    public class CyanTriggerPlaneContainer : ICyanTriggerSerializableContainer
    {
        [SerializeField]
        private Vector3 normal;
        [SerializeField]
        private float distance;
        
        public static CyanTriggerPlaneContainer CreateContainer(Plane plane)
        {
            return new CyanTriggerPlaneContainer(plane);
        }
        
        public CyanTriggerPlaneContainer(Plane plane)
        {
            normal = plane.normal;
            distance = plane.distance;
        }
            
        public object GetObject()
        {
            return new Plane(normal, distance);
        }
    }
}