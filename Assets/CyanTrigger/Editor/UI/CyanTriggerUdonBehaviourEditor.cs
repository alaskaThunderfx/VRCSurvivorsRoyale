using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Editor;

namespace Cyan.CT.Editor
{
    [CustomUdonBehaviourInspector(typeof(CyanTriggerProgramAsset))]
    public class CyanTriggerUdonBehaviourEditor : UnityEditor.Editor
    {
        // TODO consider drawing CyanTrigger UI here and hide actual CyanTrigger component?
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            UdonBehaviour udonBehaviour = (UdonBehaviour)target;
            if (udonBehaviour != null)
            {
                bool dirty = false;
                udonBehaviour.RunEditorUpdate(ref dirty);
                if (dirty && !Application.isPlaying)
                {
                    EditorSceneManager.MarkSceneDirty(udonBehaviour.gameObject.scene);
                }
            }
        }
    }

    [CustomUdonBehaviourInspector(typeof(CyanTriggerEditableProgramAsset))]
    public class CyanTriggerEditableUdonBehaviourEditor : CyanTriggerUdonBehaviourEditor { }
}