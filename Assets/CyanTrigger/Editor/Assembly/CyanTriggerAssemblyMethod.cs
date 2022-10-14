using System;
using System.Collections.Generic;
using System.Text;

namespace Cyan.CT.Editor
{
    public class CyanTriggerAssemblyMethod
    {
        public uint StartAddress;
        public readonly List<CyanTriggerAssemblyInstruction> Actions;
        public string Name;
        public bool Export;
        public bool EndReturnJumpAdded;

        public readonly List<CyanTriggerAssemblyInstruction> EndActions;
        
        public CyanTriggerAssemblyInstruction EndNop;

        public CyanTriggerAssemblyMethod(string name, bool export)
        {
            Name = name;
            Export = export;
            Actions = new List<CyanTriggerAssemblyInstruction>();
            EndActions = new List<CyanTriggerAssemblyInstruction>();

            EndNop = CyanTriggerAssemblyInstruction.Nop();
        }
        
        public void AddAction(CyanTriggerAssemblyInstruction action)
        {
            Actions.Add(action);
        }

        public void AddActions(List<CyanTriggerAssemblyInstruction> actions)
        {
            Actions.AddRange(actions);
        }
        
        public void AddActionsLast(List<CyanTriggerAssemblyInstruction> actions)
        {
            EndActions.AddRange(actions);
        }
        
        public void AddActionsFirst(List<CyanTriggerAssemblyInstruction> actions)
        {
            // Use index of 1 to not have issues with the endAddress added at the start.
            Actions.InsertRange(1, actions);
        }

        public void PushInitialEndVariable(CyanTriggerAssemblyData data)
        {
            CyanTriggerAssemblyDataType endAddress = data.GetSpecialVariable(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.EndAddress);
            Actions.Insert(0, CyanTriggerAssemblyInstruction.PushVariable(endAddress));
        }

        public void PushMethodEndReturnJump(CyanTriggerAssemblyData data)
        {
            if (EndReturnJumpAdded)
            {
                return;
            }
            EndReturnJumpAdded = true;
            
            CyanTriggerAssemblyDataType returnAddress = data.GetSpecialVariable(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ReturnAddress);
            AddAction(CyanTriggerAssemblyInstruction.PushVariable(returnAddress));
            AddAction(CyanTriggerAssemblyInstruction.Copy());
            AddAction(CyanTriggerAssemblyInstruction.JumpIndirect(returnAddress));
        }

        public void PushEndNopAndCreateNew()
        {
            Actions.Add(EndNop);
            EndNop = CyanTriggerAssemblyInstruction.Nop();
        }

        public uint ApplyAddressSize(uint address)
        {
            if (Actions.Count == 0)
            {
                return address;
            }

            StartAddress = address + Actions[0].GetInstructionSize();
            foreach (var instruction in Actions)
            {
                instruction.SetAddress(address);
                address += instruction.GetInstructionSize();
            }

            return address;
        }

        public void MapLabelsToAddress(Dictionary<string, uint> methodsToStartAddress)
        {
            foreach (var action in Actions)
            {
                string jumpLabel = action.GetJumpLabel();
                if (!string.IsNullOrEmpty(jumpLabel))
                {
                    if (!methodsToStartAddress.ContainsKey(jumpLabel))
                    {
                        throw new MissingJumpLabelException(jumpLabel);
                    }
                    action.UpdateAddress(methodsToStartAddress[jumpLabel]);
                }
            }
        }

        public void Finish()
        {
            Actions.AddRange(EndActions);
            
            Actions.Add(EndNop);
        }

        public string ExportMethod()
        {
            StringBuilder sb = new StringBuilder();
            if (Export)
            {
                sb.AppendLine($"  .export {Name}");
            }

            sb.AppendLine($"  {Name}:");

            foreach (var action in Actions)
            {
                if (action.GetInstructionType() == CyanTriggerInstructionType.NOP)
                {
                    continue;
                }

                sb.AppendLine($"    {action.Export()}");
            }

            return sb.ToString();
        }

        public CyanTriggerAssemblyMethod Clone()
        {
            CyanTriggerAssemblyMethod method = new CyanTriggerAssemblyMethod(Name, Export);

            foreach (var action in Actions)
            {
                method.AddAction(action.Clone());
            }
            
            return method;
        }

        public class MissingJumpLabelException : Exception
        {
            public readonly string MissingLabel;
            
            public MissingJumpLabelException(string label) : base($"JumpLabel missing: {label}")
            {
                MissingLabel = label;
            }
        }
    }
}