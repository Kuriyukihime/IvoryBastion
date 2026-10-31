using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
namespace IvoryBastion
{
    // Installed-game contract, shared with offline compatibility checks.
    internal static class BastionShieldDamageSite
    {
        internal static void Locate(MethodDefinition method,out Instruction load,out VariableDefinition damage)
        {
            var shieldWrite=method.Body.Instructions.FirstOrDefault(i=>i.Operand is MethodReference m&&
                m.DeclaringType.FullName=="RoR2.HealthComponent"&&m.Name=="set_Networkshield");
            var writes=method.Body.Instructions.TakeWhile(i=>i!=shieldWrite).Where(i=>i.Operand is MethodReference m&&
                m.DeclaringType.FullName=="RoR2.HealthComponent"&&m.Name=="set_Networkbarrier").ToArray();
            if(writes.Length!=2||writes[0].Previous?.OpCode!=OpCodes.Sub)
                throw new InvalidOperationException("Ivory Bastion: native barrier damage layout changed.");
            int index=Local(writes[0].Previous.Previous);load=writes[1].Next;
            if(index<0||load==null||Local(load)!=index||index>=method.Body.Variables.Count||method.Body.Variables[index].VariableType.FullName!="System.Single")
                throw new InvalidOperationException("Ivory Bastion: post-barrier Seraph Shield damage site was not found.");
            damage=method.Body.Variables[index];
        }
        private static int Local(Instruction i)
        {
            if(i==null)return -1;
            switch(i.OpCode.Code)
            {
                case Code.Ldloc_0:return 0;case Code.Ldloc_1:return 1;case Code.Ldloc_2:return 2;case Code.Ldloc_3:return 3;
                case Code.Ldloc:case Code.Ldloc_S:return i.Operand is VariableDefinition v?v.Index:-1;
                default:return -1;
            }
        }
    }
}
