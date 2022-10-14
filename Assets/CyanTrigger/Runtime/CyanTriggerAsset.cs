using System;
using UnityEngine;
using VRC.Udon;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Cyan.CT
{
    [HelpURL(CyanTriggerDocumentationLinks.CyanTriggerAsset)]
    public class CyanTriggerAsset : CyanTriggerBehaviour
    {
        public CyanTriggerAssetSerializableInstance assetInstance = new CyanTriggerAssetSerializableInstance();
        
#if UNITY_EDITOR
        private static bool autoAddUdonBehaviour = true;
#endif
        
        private void Reset()
        {
            if (assetInstance == null)
            {
                assetInstance = new CyanTriggerAssetSerializableInstance();
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

            string resetGroupName = Undo.GetCurrentGroupName();
            if (assetInstance == null)
            {
                Undo.RecordObject(this, resetGroupName);
                assetInstance = new CyanTriggerAssetSerializableInstance();
            }
            
            if (autoAddUdonBehaviour)
            {
                // Check if on copy, this udon is equal to one used by another component on the same gameobject. 
                if (assetInstance.udonBehaviour != null)
                {
                    foreach (var ctAsset in GetComponents<CyanTriggerAsset>())
                    {
                        if (ctAsset == this)
                        {
                            continue;
                        }

                        if (ctAsset.assetInstance?.udonBehaviour == assetInstance.udonBehaviour)
                        {
                            assetInstance.udonBehaviour = null;
                            break;
                        }
                    }
                }
                
                if (assetInstance.udonBehaviour == null)
                {
                    assetInstance.udonBehaviour = Undo.AddComponent<UdonBehaviour>(gameObject);
                    if (assetInstance.cyanTriggerProgram != null)
                    {
                        Undo.RecordObject(assetInstance.udonBehaviour, resetGroupName);
                        assetInstance.udonBehaviour.programSource = assetInstance.cyanTriggerProgram;
                        
                        if (PrefabUtility.IsPartOfPrefabInstance(assetInstance.udonBehaviour))
                        {
                            PrefabUtility.RecordPrefabInstancePropertyModifications(assetInstance.udonBehaviour);
                        }
                    }
                }
            }
            
            if (PrefabUtility.IsPartOfPrefabInstance(this))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            }
#endif
        }

        // When adding components using an editor script, DelayReset will break.
        // Use this method to ensure the CyanTriggerAsset component is properly initialized.
        public static CyanTriggerAsset AddCyanTriggerAssetInEditor(GameObject obj)
        {
            CyanTriggerAsset trigger = obj.AddComponent<CyanTriggerAsset>();
            trigger.DelayedReset();

            return trigger;
        }

        internal static CyanTriggerAsset AddFromUdonBehaviour(UdonBehaviour udonBehaviour)
        {
            CyanTriggerAsset ctAsset = null;
#if UNITY_EDITOR
            autoAddUdonBehaviour = false;
            ctAsset = Undo.AddComponent<CyanTriggerAsset>(udonBehaviour.gameObject);
            Undo.RecordObject(ctAsset, Undo.GetCurrentGroupName());
            autoAddUdonBehaviour = true;
            
            var assetInstance = ctAsset.assetInstance;
            assetInstance.udonBehaviour = udonBehaviour;
            assetInstance.proximity = udonBehaviour.proximity;
            assetInstance.interactText = udonBehaviour.interactText;
            var program = udonBehaviour.programSource;
            if (program is ICyanTriggerProgramAsset ctProgramAsset)
            {
                assetInstance.cyanTriggerProgram = program;
                (assetInstance.variableData, assetInstance.variableGuids) = ctProgramAsset.GetDefaultVariableData();

                // Copy previous public variable data from the UdonBehaviour.
                var publicVariables = udonBehaviour.publicVariables;
                var variableData = ctProgramAsset.GetCyanTriggerData().variables;
                for (int index = 0; index < variableData.Length; ++index)
                {
                    CyanTriggerVariable variable = variableData[index];
                    if (publicVariables.TryGetVariableValue(variable.name, out var value))
                    {
                        assetInstance.variableData[index].Obj = value;
                    }
                }
            }
            else
            {
                // This should never happen since the method is called from CyanTriggerEditableProgramAsset
                Debug.LogError("[CyanTriggerAsset] Program is not a CyanTriggerProgramAsset!");
            }
            
            if (PrefabUtility.IsPartOfPrefabInstance(ctAsset))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(ctAsset);
            }
#endif

            return ctAsset;
        }

        public void SetProgram(ICyanTriggerProgramAsset ctProgramAsset)
        {
#if UNITY_EDITOR
            Undo.RecordObject(this, Undo.GetCurrentGroupName());
            Undo.RecordObject(assetInstance.udonBehaviour, Undo.GetCurrentGroupName());

            assetInstance.cyanTriggerProgram = (AbstractUdonProgramSource)ctProgramAsset;
            assetInstance.udonBehaviour.programSource = assetInstance.cyanTriggerProgram;

            (assetInstance.variableData, assetInstance.variableGuids) = ctProgramAsset.GetDefaultVariableData();
            
            if (PrefabUtility.IsPartOfPrefabInstance(this))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(this);
                PrefabUtility.RecordPrefabInstancePropertyModifications(assetInstance.udonBehaviour);
            }
#endif
        }

        private void OnDrawGizmosSelected()
        {
            foreach (var data in assetInstance.variableData)
            {
                if (data.Obj is Component component && component != null)
                {
                    CyanTrigger.DrawLineBetweenTransforms(transform, component.transform);
                }
            
                if (data.Obj is GameObject otherGameObject && otherGameObject != null)
                {
                    CyanTrigger.DrawLineBetweenTransforms(transform, otherGameObject.transform);
                }
            }
        }

        public static bool AssetsHaveSameProgram(CyanTriggerAsset ctAsset1, CyanTriggerAsset ctAsset2)
        {
            if (ctAsset1 == null || ctAsset2 == null)
            {
                return false;
            }

            var instance1 = ctAsset1.assetInstance;
            var instance2 = ctAsset2.assetInstance;
            if (instance1 == null || instance2 == null)
            {
                return false;
            }

            return instance1.cyanTriggerProgram == instance2.cyanTriggerProgram;
        }

        #region CyanTriggerBehaviour

        public override void SetVariablePublicValues(object[] variables)
        {
#if UNITY_EDITOR
            Undo.RecordObject(this, Undo.GetCurrentGroupName());
#endif
            var variableData = assetInstance.variableData;
            for (int index = 0; index < variables.Length && index < variableData.Length; ++index)
            {
                variableData[index].Obj = variables[index];
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
            return assetInstance?.udonBehaviour;
        }

        public override CyanTriggerDataInstance GetCyanTriggerData()
        {
            ICyanTriggerProgramAsset program = ((ICyanTriggerProgramAsset)assetInstance?.cyanTriggerProgram);
            if (program != null)
            {
                return program.GetCyanTriggerData();
            }
            return null;
        }

        public override CyanTriggerDataInstance GetCopyOfCyanTriggerData()
        {
            ICyanTriggerProgramAsset program = ((ICyanTriggerProgramAsset)assetInstance?.cyanTriggerProgram);
            if (program != null)
            {
                return program.GetCopyOfCyanTriggerData();
            }
            return null;
        }

        #endregion
    }

    [Serializable]
    public class CyanTriggerAssetSerializableInstance
    {
        public float proximity = 2f;
        public string interactText = "Use";

        public string[] variableGuids = Array.Empty<string>();
        public CyanTriggerSerializableObject[] variableData = Array.Empty<CyanTriggerSerializableObject>();
        public AbstractUdonProgramSource cyanTriggerProgram;
            
        public bool expandVariables = true;
        public bool expandInteractSettings = true;
        
        [HideInInspector] 
        public UdonBehaviour udonBehaviour;
    }
}