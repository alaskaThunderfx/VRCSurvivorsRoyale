using System;
using UnityEngine;
using VRC.Udon;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Cyan.CT
{
    [DisallowMultipleComponent]
    [HelpURL(CyanTriggerDocumentationLinks.CyanTrigger)]
    public class CyanTrigger : CyanTriggerBehaviour
    {
        public CyanTriggerSerializableInstance triggerInstance;

#if UNITY_EDITOR
        private static bool autoAddUdonBehaviour = true;
#endif
        private void Reset()
        {
            if (triggerInstance == null)
            {
                triggerInstance = CyanTriggerSerializableInstance.CreateInstance();
            }
          
#if UNITY_EDITOR
            // Reset is called before the AddComponent undo action is finalized.
            // Delay calling reset logic to ensure that new undo operations add in order.
            EditorApplication.update += DelayedReset;
#endif
        }

        public void DelayedReset()
        {
#if UNITY_EDITOR
            EditorApplication.update -= DelayedReset;

            if (this == null)
            {
                return;
            }

            Undo.RecordObject(this, Undo.GetCurrentGroupName());
            if (triggerInstance == null)
            {
                triggerInstance = CyanTriggerSerializableInstance.CreateInstance();
            }
            
            if (autoAddUdonBehaviour && triggerInstance.udonBehaviour == null)
            {
                triggerInstance.udonBehaviour = Undo.AddComponent<UdonBehaviour>(gameObject);
            }
            
            if (PrefabUtility.IsPartOfPrefabInstance(this))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            }
#endif
        }

        public void Verify()
        {
#if UNITY_EDITOR
            // Verify that trigger data is valid.
            // When importing CyanTriggers, any compile errors will cause prefabs to import without data.
            // This checks both that data is missing and the object is part of a prefab. If both are true, reimport it. 
            if (triggerInstance == null)
            {
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
                if (path != null)
                {
                    AssetDatabase.ImportAsset(path);
                }
            }
#endif
        }

        // When adding components using an editor script, DelayReset will break.
        // Use this method to ensure the CyanTrigger component is properly initialized.
        public static CyanTrigger AddCyanTriggerInEditor(GameObject obj)
        {
            CyanTrigger trigger = obj.AddComponent<CyanTrigger>();
            trigger.DelayedReset();

            return trigger;
        }

        internal static CyanTrigger AddFromCyanTriggerAsset(CyanTriggerAsset ctAsset)
        {
            var udonBehaviour = ctAsset.assetInstance?.udonBehaviour;
            if (udonBehaviour == null)
            {
                return null;
            }
            
            CyanTrigger trigger = null;
            
#if UNITY_EDITOR
            autoAddUdonBehaviour = false;
            trigger = Undo.AddComponent<CyanTrigger>(udonBehaviour.gameObject);
            Undo.RecordObject(trigger, Undo.GetCurrentGroupName());
            autoAddUdonBehaviour = true;

            var instance = trigger.triggerInstance;
            instance.udonBehaviour = udonBehaviour;
            instance.proximity = udonBehaviour.proximity;
            instance.interactText = udonBehaviour.interactText;
            var program = udonBehaviour.programSource;
            if (program is ICyanTriggerProgramAsset ctProgramAsset)
            {
                instance.triggerDataInstance = ctProgramAsset.GetCopyOfCyanTriggerData();
                var variableData = ctAsset.assetInstance.variableData;
                var newVariableData = instance.triggerDataInstance.variables;
                
                for (int index = 0; index < variableData.Length; ++index)
                {
                    var variable = newVariableData[index];
                    variable.data.Obj = variableData[index].Obj;
                    variable.showInInspector = true;
                }
            }
            else
            {
                // This should never happen since the method is called from CyanTriggerEditableProgramAsset
                Debug.LogError("[CyanTrigger] Program is not a CyanTriggerProgramAsset!");
            }
          
            Undo.DestroyObjectImmediate(ctAsset);
            
            if (PrefabUtility.IsPartOfPrefabInstance(trigger))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(trigger);
            }
#endif

            return trigger;
        }


        private void OnDrawGizmosSelected()
        {
            var data = triggerInstance?.triggerDataInstance;
            if (data == null || data.variables == null || data.events == null)
            {
                return;
            }
            
            foreach (var variable in data.variables)
            {
                DrawLineToObject(variable);
            }
            
            foreach (var trigEvent in data.events)
            {
                DrawLineToObjects(trigEvent.eventInstance);

                foreach (var action in trigEvent.actionInstances)
                {
                    DrawLineToObjects(action);
                }
            }
        }

        private void DrawLineToObjects(CyanTriggerActionInstance actionInstance)
        {
            foreach (var input in actionInstance.inputs)
            {
                DrawLineToObject(input);
            }

            if (actionInstance.multiInput != null)
            {
                foreach (var input in actionInstance.multiInput)
                {
                    DrawLineToObject(input);
                }
            }
        }

        private void DrawLineToObject(CyanTriggerActionVariableInstance variableInstance)
        {
            if (variableInstance.isVariable || variableInstance.data?.Obj == null)
            {
                return;
            }

            if (variableInstance.data.Obj is Component component && component != null)
            {
                DrawLineBetweenTransforms(transform, component.transform);
            }
            
            if (variableInstance.data.Obj is GameObject otherGameObject && otherGameObject != null)
            {
                DrawLineBetweenTransforms(transform, otherGameObject.transform);
            }
        }

        internal static void DrawLineBetweenTransforms(Transform transform1, Transform transform2)
        {
            if (transform1 == null 
                || transform2 == null 
                || transform1 == transform2
                || transform1.gameObject.scene != transform2.gameObject.scene)
            {
                return;
            }

            Gizmos.DrawLine(transform1.position, transform2.position);
        }

        #region CyanTriggerBehaviour

        public override void SetVariablePublicValues(object[] variables)
        {
#if UNITY_EDITOR
            Undo.RecordObject(this, Undo.GetCurrentGroupName());
#endif
            var variableData = triggerInstance.triggerDataInstance.variables;
            for (int index = 0; index < variables.Length && index < variableData.Length; ++index)
            {
                variableData[index].data.Obj = variables[index];
            }
           
#if UNITY_EDITOR 
            if (PrefabUtility.IsPartOfPrefabInstance(this))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            }
#endif
        }

        public override UdonBehaviour GetUdonBehaviour()
        {
            return triggerInstance?.udonBehaviour;
        }

        public override CyanTriggerDataInstance GetCyanTriggerData()
        {
            return triggerInstance?.triggerDataInstance;
        }
        public override CyanTriggerDataInstance GetCopyOfCyanTriggerData()
        {
            var triggerData = GetCyanTriggerData();
            if (triggerData == null)
            {
                return null;
            }

            return CyanTriggerCopyUtil.CopyCyanTriggerDataInstance(triggerData, true);
        }

        #endregion
    }

    [Serializable]
    public class CyanTriggerSerializableInstance
    {
        public float proximity = 2f;
        public string interactText = "Use";
        public CyanTriggerDataInstance triggerDataInstance; // TODO encode this directly instead of encoding each children individually?
        
        [HideInInspector]
        public UdonBehaviour udonBehaviour;

        public static CyanTriggerSerializableInstance CreateInstance()
        {
            var instance = new CyanTriggerSerializableInstance
            {
                triggerDataInstance = CyanTriggerDataInstance.CreateInitialized(),
            };
            return instance;
        }

        public static CyanTriggerSerializableInstance CreateEmptyInstance()
        {
            return new CyanTriggerSerializableInstance();
        }

        private CyanTriggerSerializableInstance() { }
    }
}