using System.Collections.Generic;
using VRC.Udon.Editor.ProgramSources;

namespace Cyan.CT.Editor
{
    public class CyanTriggerProgramDependency
    {
        public class ProgramData
        {
            public CyanTriggerActionGroupDefinition ActionGroupDefinition;
            public readonly CyanTriggerProgramAsset Program;
            
            public ProgramData(CyanTriggerProgramAsset program)
            {
                Program = program;
            }
        }
        
        private readonly List<CyanTriggerDependency<ProgramData>> _programDependencies =
            new List<CyanTriggerDependency<ProgramData>>();
        private readonly Dictionary<UdonProgramAsset, CyanTriggerDependency<ProgramData>> _dependencies = 
            new Dictionary<UdonProgramAsset, CyanTriggerDependency<ProgramData>>();

        private bool _processedPrograms = false;

        public CyanTriggerProgramDependency(List<CyanTriggerProgramAsset> programs)
        {
            foreach (var program in programs)
            {
                var programData = new ProgramData(program);
                var dependencyObj = new CyanTriggerDependency<ProgramData>(programData);
                _programDependencies.Add(dependencyObj);
                _dependencies.Add(program, dependencyObj);
            }
        }

        public List<CyanTriggerActionGroupDefinition> ProcessPrograms()
        {
            // Processed before, clear previous dependency data. 
            if (_processedPrograms)
            {
                CyanTriggerDependency<ProgramData>.ClearAllDependencies(_programDependencies);
            }
            _processedPrograms = true;
            
            // List of program Assets that will not be compiled that have been processed
            HashSet<CyanTriggerActionGroupDefinition> nonDependentProgramAssets = 
                new HashSet<CyanTriggerActionGroupDefinition>();

            // Gather all dependencies
            foreach (var programData in _programDependencies)
            {
                var program = programData.Data.Program;
                // Debug.Log($"Checking Dependencies: {program.name}");
                var triggerData = program.GetCyanTriggerData();
                HashSet<UdonProgramAsset> depHash = new HashSet<UdonProgramAsset>();
                var myDepNode = _dependencies[program];
                
                // TODO CustomAction dependencies can be cached.
                // Go through all events and actions to find all CustomActions this program is dependent on.
                foreach (CyanTriggerActionGroupDefinition actionGroupDefinition in 
                         CyanTriggerUtil.GetCustomActionDependencies(triggerData))
                {
                    // TODO when other types are supported, add them here. 
                    if (!(actionGroupDefinition is CyanTriggerActionGroupDefinitionUdonAsset udonActionGroup))
                    {
                        continue;
                    }

                    var programAsset = udonActionGroup.udonProgramAsset;

                    // Skip dependent programs that have already been processed 
                    if (depHash.Contains(programAsset))
                    {
                        continue;
                    }
                    depHash.Add(programAsset);
                    
                    if (_dependencies.TryGetValue(programAsset, out var dep))
                    {
                        // Debug.Log($"Dependency: {program.name} -> {programAsset.name}");
                        myDepNode.AddDependency(dep);
                        
                        // TODO find a better way to get/cache this.
                        if (dep.Data.ActionGroupDefinition == null)
                        {
                            dep.Data.ActionGroupDefinition = actionGroupDefinition;
                        }
                    }
                    else
                    {
                        nonDependentProgramAssets.Add(actionGroupDefinition);
                    }
                }
            }

            return new List<CyanTriggerActionGroupDefinition>(nonDependentProgramAssets);
        }
        
        public bool GetOrder(
            out List<ProgramData> sortedItems,
            out List<ProgramData> failedToSort)
        {
            if (!_processedPrograms)
            {
                ProcessPrograms();
            }
            
            return CyanTriggerDependency<ProgramData>.GetDependencyOrdering(_programDependencies, out sortedItems, out failedToSort);
        }
    }
}