using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.Udon.Serialization.OdinSerializer;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Cyan.CT
{
    [Serializable]
    public class CyanTriggerSerializableObject : ISerializationCallbackReceiver
    {
        private object _obj;
        public object Obj
        {
            get
            {
                if (_obj == null)
                {
                    _obj = DecodeObject(objEncoded, unityObjects);
                }

                return _obj;
            }
            set
            {
                if (_obj == value)
                {
                    return;
                }
                _obj = value;
                objEncoded = EncodeObject(_obj, out unityObjects);
            }
        }
        
        [SerializeField]
        private string objEncoded;
        [SerializeField]
        private List<Object> unityObjects;

        public CyanTriggerSerializableObject() {}

        public CyanTriggerSerializableObject(object obj)
        {
            Obj = obj;
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            // Clear cache to ensure new data is decoded when used. 
            _obj = null;
        }
        
        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        public static string EncodeObject(object obj, out List<Object> unityObjects)
        {
            obj = GetContainerVersion(obj);
            
            byte[] serializedBytes = SerializationUtility.SerializeValue(obj, DataFormat.Binary, out unityObjects);
            return Convert.ToBase64String(serializedBytes);
        }

        public static object DecodeObject(string objEncoded, List<Object> unityObjects)
        {
            if (!string.IsNullOrEmpty(objEncoded))
            {
                byte[] serializedBytes = Convert.FromBase64String(objEncoded);
                
                object result =
                    SerializationUtility.DeserializeValue<object>(serializedBytes, DataFormat.Binary, unityObjects);

                if (result is ICyanTriggerSerializableContainer proxyRes)
                {
                    return proxyRes.GetObject();
                }

                return result;
            }

            return null;
        }

#if UNITY_EDITOR
        public static object ObjectFromSerializedProperty(SerializedProperty property)
        {
            SerializedProperty objEncodedProperty = property.FindPropertyRelative(nameof(objEncoded));
            SerializedProperty unityObjectsProperty = property.FindPropertyRelative(nameof(unityObjects));

            List<Object> objs = new List<Object>();
            for (int cur = 0; cur < unityObjectsProperty.arraySize; ++cur)
            {
                SerializedProperty obj = unityObjectsProperty.GetArrayElementAtIndex(cur);
                objs.Add(obj.objectReferenceValue);
            }
            
            return DecodeObject(objEncodedProperty.stringValue, objs);
        }

        public static void UpdateSerializedProperty(SerializedProperty property, object obj)
        {
            string encoded = EncodeObject(obj, out var objs);
            
            SerializedProperty objEncodedProperty = property.FindPropertyRelative(nameof(objEncoded));
            SerializedProperty unityObjectsProperty = property.FindPropertyRelative(nameof(unityObjects));

            objEncodedProperty.stringValue = encoded;
            int size = unityObjectsProperty.arraySize = objs?.Count ?? 0;
                
            for (int cur = 0; cur < size; ++cur)
            {
                SerializedProperty objProp = unityObjectsProperty.GetArrayElementAtIndex(cur);
                // ReSharper disable once PossibleNullReferenceException
                objProp.objectReferenceValue = objs[cur];
            }
        }

        public static void CopySerializedProperty(SerializedProperty srcProperty, SerializedProperty dstProperty)
        {
            SerializedProperty srcObjEncodedProperty = srcProperty.FindPropertyRelative(nameof(objEncoded));
            SerializedProperty srcUnityObjectsProperty = srcProperty.FindPropertyRelative(nameof(unityObjects));
            
            SerializedProperty dstObjEncodedProperty = dstProperty.FindPropertyRelative(nameof(objEncoded));
            SerializedProperty dstUnityObjectsProperty = dstProperty.FindPropertyRelative(nameof(unityObjects));

            dstObjEncodedProperty.stringValue = srcObjEncodedProperty.stringValue;
            dstUnityObjectsProperty.arraySize = srcUnityObjectsProperty.arraySize;
            
            for (int cur = 0; cur < srcUnityObjectsProperty.arraySize; ++cur)
            {
                SerializedProperty srcObjProp = srcUnityObjectsProperty.GetArrayElementAtIndex(cur);
                SerializedProperty dstObjProp = dstUnityObjectsProperty.GetArrayElementAtIndex(cur);
                dstObjProp.objectReferenceValue = srcObjProp.objectReferenceValue;
            }
        }
#endif
        
        private static object GetContainerVersion(object obj)
        {
            if (obj is Ray ray)
            {
                return new CyanTriggerRayContainer(ray);
            }
            if (obj is Ray[] rayArray)
            {
                return new CyanTriggerContainerArray<Ray, CyanTriggerRayContainer>(
                    rayArray,
                    CyanTriggerRayContainer.CreateContainer);
            }
            
            if (obj is Plane plane)
            {
                return new CyanTriggerPlaneContainer(plane);
            }
            if (obj is Plane[] planeArray)
            {
                return new CyanTriggerContainerArray<Plane, CyanTriggerPlaneContainer>(
                    planeArray,
                    CyanTriggerPlaneContainer.CreateContainer);
            }
            
            if (obj is Vector2Int vector2Int)
            {
                return new CyanTriggerVector2IntContainer(vector2Int);
            }
            if (obj is Vector2Int[] vector2IntArray)
            {
                return new CyanTriggerContainerArray<Vector2Int, CyanTriggerVector2IntContainer>(
                    vector2IntArray,
                    CyanTriggerVector2IntContainer.CreateContainer);
            }
            
            if (obj is Vector3Int vector3Int)
            {
                return new CyanTriggerVector3IntContainer(vector3Int);
            }
            if (obj is Vector3Int[] vector3IntArray)
            {
                return new CyanTriggerContainerArray<Vector3Int, CyanTriggerVector3IntContainer>(
                    vector3IntArray,
                    CyanTriggerVector3IntContainer.CreateContainer);
            }

            return obj;
        }
    }
}