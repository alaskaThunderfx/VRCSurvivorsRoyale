
using System.Collections.Generic;

namespace Cyan.CT.Editor
{
    public class CyanTriggerAssemblyProgram
    {
        public readonly CyanTriggerAssemblyCode Code;
        public readonly CyanTriggerAssemblyData Data;

        public CyanTriggerAssemblyProgram(CyanTriggerAssemblyCode code, CyanTriggerAssemblyData data)
        {
            Data = data;
            Code = code;
        }

        public string FinishAndExport()
        {
            Finish();
            ApplyAddresses();
            return Export();
        }

        public void Finish()
        {
            // Ensure that all event variables are added.
            foreach (var method in Code.GetMethods())
            {
                Data.GetEventVariables(method.Name);
            }
            
            Code.Finish();
        }

        public void ApplyAddresses()
        {
            Data.ApplyAddresses();
            Code.ApplyAddresses();
            Data.FinalizeJumpVariableAddresses();
        }

        public string Export()
        {
            return $"{Data.Export()}\n{Code.Export()}";
        }

        public CyanTriggerAssemblyProgram Clone()
        {
            Dictionary<CyanTriggerAssemblyInstruction, CyanTriggerAssemblyInstruction> instructionMapping =
                new Dictionary<CyanTriggerAssemblyInstruction, CyanTriggerAssemblyInstruction>();
            Dictionary<CyanTriggerAssemblyDataType, CyanTriggerAssemblyDataType> variableMapping =
                new Dictionary<CyanTriggerAssemblyDataType, CyanTriggerAssemblyDataType>();

            CyanTriggerAssemblyProgram program =
                new CyanTriggerAssemblyProgram(Code.Clone(instructionMapping), Data.Clone(variableMapping));

            program.Code.UpdateMapping(instructionMapping, variableMapping);
            program.Data.UpdateJumpInstructions(instructionMapping);
            
            return program;
        }
        
        public void MergeProgram(CyanTriggerAssemblyProgram program)
        {
            Dictionary<CyanTriggerAssemblyDataType, CyanTriggerAssemblyDataType> variableMapping =
                new Dictionary<CyanTriggerAssemblyDataType, CyanTriggerAssemblyDataType>();
            
            CyanTriggerAssemblyData.MergeData(Data, variableMapping, program.Data);
            
            foreach (var method in program.Code.GetMethods())
            {
                Code.AddMethod(method);
            }
            
            Dictionary<CyanTriggerAssemblyInstruction, CyanTriggerAssemblyInstruction> instructionMapping =
                new Dictionary<CyanTriggerAssemblyInstruction, CyanTriggerAssemblyInstruction>();
            Code.UpdateMapping(instructionMapping, variableMapping);
        }

        public uint GetHeapSize()
        {
            // Heap size is equal to variable count + unique extern count
            return (uint)Data.GetVariableCount() + Code.GetUniqueExternCount();
        }
    }
}
