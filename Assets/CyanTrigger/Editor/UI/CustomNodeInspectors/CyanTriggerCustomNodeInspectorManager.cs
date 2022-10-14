using System.Collections.Generic;

namespace Cyan.CT.Editor
{
    public class CyanTriggerCustomNodeInspectorManager
    {
        private readonly Dictionary<string, ICyanTriggerCustomNodeInspector> _customInspectors =
            new Dictionary<string, ICyanTriggerCustomNodeInspector>();
        private readonly Dictionary<string, ICyanTriggerCustomNodeInspectorDisplayText> _customDisplayText =
            new Dictionary<string, ICyanTriggerCustomNodeInspectorDisplayText>();
        
        private readonly Dictionary<string, ICyanTriggerCustomNodeInspector> _customInspectorsGuids =
            new Dictionary<string, ICyanTriggerCustomNodeInspector>();
        private readonly Dictionary<string, ICyanTriggerCustomNodeInspectorDisplayText> _customDisplayTextGuids =
            new Dictionary<string, ICyanTriggerCustomNodeInspectorDisplayText>();

        private static readonly object Lock = new object();

        private static CyanTriggerCustomNodeInspectorManager _instance;
        public static CyanTriggerCustomNodeInspectorManager Instance
        {
            get
            {
                lock (Lock)
                {
                    if (_instance == null)
                    {
                        _instance = new CyanTriggerCustomNodeInspectorManager();
                    }
                    return _instance;
                }
            }
        }

        private CyanTriggerCustomNodeInspectorManager()
        {
            // TODO Automate this by getting from assembly.
            // This will require refactoring NodeManager so assembly is only parsed once.
            ICyanTriggerCustomNodeInspector[] inspectors =
            {
                new CyanTriggerCustomNodeInspectorClearReplay(),
                
                new CyanTriggerCustomNodeInspectorSendCustomEventUdon(),
                new CyanTriggerCustomNodeInspectorSendCustomEventDelayedFrames(),
                new CyanTriggerCustomNodeInspectorSendCustomEventDelayedSeconds(),
                new CyanTriggerCustomNodeInspectorSendCustomNetworkEvent(),
                
                new CyanTriggerCustomNodeInspectorSetProgramVariable(),
                new CyanTriggerCustomNodeInspectorGetProgramVariable(),
                new CyanTriggerCustomNodeInspectorGetProgramVariableType(),
                
                new CyanTriggerCustomNodeInspectorSetComponentActive(),
                new CyanTriggerCustomNodeInspectorSetComponentActiveToggle(),
                
                new CyanTriggerCustomNodeInspectorAnimatorSetParameter(false), // ResetTrigger
                new CyanTriggerCustomNodeInspectorAnimatorSetParameter(true), // SetTrigger
                new CyanTriggerCustomNodeInspectorAnimatorSetParameter(typeof(bool), PrimitiveOperation.None),
                new CyanTriggerCustomNodeInspectorAnimatorSetParameter(typeof(bool), PrimitiveOperation.UnaryNegation),
                new CyanTriggerCustomNodeInspectorAnimatorSetParameter(typeof(int), PrimitiveOperation.None),
                new CyanTriggerCustomNodeInspectorAnimatorSetParameter(typeof(int), PrimitiveOperation.Addition),
                new CyanTriggerCustomNodeInspectorAnimatorSetParameter(typeof(int), PrimitiveOperation.Subtraction),
                new CyanTriggerCustomNodeInspectorAnimatorSetParameter(typeof(int), PrimitiveOperation.Multiplication),
                new CyanTriggerCustomNodeInspectorAnimatorSetParameter(typeof(int), PrimitiveOperation.Division),
                new CyanTriggerCustomNodeInspectorAnimatorSetParameter(typeof(int), PrimitiveOperation.Remainder),
                new CyanTriggerCustomNodeInspectorAnimatorSetParameter(typeof(float), PrimitiveOperation.None),
                new CyanTriggerCustomNodeInspectorAnimatorSetParameter(typeof(float), PrimitiveOperation.Addition),
                new CyanTriggerCustomNodeInspectorAnimatorSetParameter(typeof(float), PrimitiveOperation.Subtraction),
                new CyanTriggerCustomNodeInspectorAnimatorSetParameter(typeof(float), PrimitiveOperation.Multiplication),
                new CyanTriggerCustomNodeInspectorAnimatorSetParameter(typeof(float), PrimitiveOperation.Division),
                new CyanTriggerCustomNodeInspectorAnimatorSetParameter(typeof(float), PrimitiveOperation.Remainder),
            };

            foreach (var inspector in inspectors)
            {
                string nodeDefinition = inspector.GetNodeDefinitionName();
                bool hasDefinition = !string.IsNullOrEmpty(nodeDefinition);
                
                string guid = inspector.GetCustomActionGuid();
                bool hasGuid = !string.IsNullOrEmpty(guid);
                
                if (hasDefinition)
                {
                    _customInspectors.Add(nodeDefinition, inspector);
                }
                
                if (hasGuid)
                {
                    _customInspectorsGuids.Add(guid, inspector);
                }
                
                if (inspector is ICyanTriggerCustomNodeInspectorDisplayText customDisplayText)
                {
                    if (hasDefinition)
                    {
                        _customDisplayText.Add(nodeDefinition, customDisplayText);
                    }

                    if (hasGuid)
                    {
                        _customDisplayTextGuids.Add(guid, customDisplayText);
                    }
                }
            }
        }

        public bool TryGetCustomInspector(
            CyanTriggerActionInfoHolder actionInfo,
            out ICyanTriggerCustomNodeInspector customNodeInspector)
        {
            string defName = actionInfo.Definition?.FullName;
            if (!string.IsNullOrEmpty(defName))
            {
                // ReSharper disable once AssignNullToNotNullAttribute
                return _customInspectors.TryGetValue(defName, out customNodeInspector);
            }

            string guid = actionInfo.Action?.guid;
            if (!string.IsNullOrEmpty(guid))
            {
                // ReSharper disable once AssignNullToNotNullAttribute
                return _customInspectorsGuids.TryGetValue(guid, out customNodeInspector);
            }
            
            customNodeInspector = null;
            return false;
        }

        public bool TryGetCustomInspectorDisplayText(
            CyanTriggerActionInfoHolder actionInfo,
            out ICyanTriggerCustomNodeInspectorDisplayText customNodeInspectorDisplayText)
        {
            string defName = actionInfo.Definition?.FullName;
            if (!string.IsNullOrEmpty(defName))
            {
                // ReSharper disable once AssignNullToNotNullAttribute
                return _customDisplayText.TryGetValue(defName, out customNodeInspectorDisplayText);
            }
            
            string guid = actionInfo.Action?.guid;
            if (!string.IsNullOrEmpty(guid))
            {
                // ReSharper disable once AssignNullToNotNullAttribute
                return _customDisplayTextGuids.TryGetValue(guid, out customNodeInspectorDisplayText);
            }

            customNodeInspectorDisplayText = null;
            return false;
        }
    }
}