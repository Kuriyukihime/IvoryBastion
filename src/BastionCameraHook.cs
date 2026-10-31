using System;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace IvoryBastion
{
    internal static class BastionCameraHook
    {
        internal static void Apply(ILContext context,Action<ILCursor> emitFade)
        {
            var cursor=new ILCursor(context);
            // Enter after incoming labels so the ordinary no-override branch
            // also runs our correction, after both native fade assignments.
            if(!cursor.TryGotoNext(MoveType.AfterLabel,
                i=>i.MatchLdarg(0),
                i=>Field(i,Code.Ldfld,"fade"),
                i=>Field(i,Code.Ldsfld,"fadeEpsilon")))
                throw new InvalidOperationException("Ivory Bastion: final per-camera native fade read was not found.");
            var field=(FieldReference)cursor.Next.Next.Operand;
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldarg_0);cursor.Emit(OpCodes.Ldfld,field);
            cursor.Emit(OpCodes.Ldarg_0);cursor.Emit(OpCodes.Ldarg_1);
            emitFade(cursor);
            cursor.Emit(OpCodes.Stfld,field);
        }

        private static bool Field(Instruction instruction,Code code,string name)=>
            instruction.OpCode.Code==code&&instruction.Operand is FieldReference field&&
            field.DeclaringType.FullName=="RoR2.CharacterModel"&&field.Name==name;
    }
}
