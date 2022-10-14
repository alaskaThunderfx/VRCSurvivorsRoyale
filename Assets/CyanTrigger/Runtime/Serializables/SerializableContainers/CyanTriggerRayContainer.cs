using System;
using UnityEngine;

namespace Cyan.CT
{
    [Serializable]
    public class CyanTriggerRayContainer : ICyanTriggerSerializableContainer
    {
        [SerializeField]
        private Vector3 origin;
        [SerializeField]
        private Vector3 direction;

        public static CyanTriggerRayContainer CreateContainer(Ray ray)
        {
            return new CyanTriggerRayContainer(ray);
        }
        
        public CyanTriggerRayContainer(Ray ray)
        {
            origin = ray.origin;
            direction = ray.direction;
        }

        public object GetObject()
        {
            return new Ray(origin, direction);
        }
    }
}