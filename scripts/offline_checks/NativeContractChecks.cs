using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static class NativeContractChecks
{
    internal static void Run()
    {
        using var game=AssemblyDefinition.ReadAssembly(@"X:\SteamLibrary\steamapps\common\Risk of Rain 2\Risk of Rain 2_Data\Managed\RoR2.dll");
        var body=game.MainModule.Types.Single(t=>t.FullName=="RoR2.CharacterBody");
        var instructions=body.Methods.Single(m=>m.Name=="RecalculateStats").Body.Instructions;
        var sites=instructions.Where(i=>i.OpCode==OpCodes.Ldfld&&i.Operand is FieldReference f&&f.Name=="baseMaxShield"&&f.DeclaringType.FullName=="RoR2.CharacterBody").ToArray();
        if(sites.Length!=1)throw new Exception("Native shield hook must have exactly one insertion point");
        int shieldIndex=instructions.IndexOf(sites[0]);
        if(instructions[shieldIndex+1].OpCode!=OpCodes.Ldarg_0||
           !(instructions[shieldIndex+2].Operand is FieldReference levelShield)||levelShield.Name!="levelMaxShield")
            throw new Exception("R2API's native base/level shield pattern changed");
        var store=instructions.Skip(shieldIndex+1).FirstOrDefault(i=>i.OpCode.Code==Code.Stloc||i.OpCode.Code==Code.Stloc_S||i.OpCode.Code==Code.Stloc_0||i.OpCode.Code==Code.Stloc_1||i.OpCode.Code==Code.Stloc_2||i.OpCode.Code==Code.Stloc_3);
        if(store==null)throw new Exception("Shield result-store insertion point is absent");
        if(!instructions.Take(shieldIndex).Any(i=>i.Operand is MethodReference m&&m.Name=="set_maxHealth"))
            throw new Exception("Shield percentage must use the freshly calculated maximum health");
        var danger=body.Methods.Single(m=>m.Name=="UpdateOutOfCombatAndDanger").Body.Instructions;
        if(!danger.Any(i=>i.OpCode==OpCodes.Ldc_R4&&(float)i.Operand==7f))throw new Exception("Native shield recharge delay changed");
        var health=game.MainModule.Types.Single(t=>t.FullName=="RoR2.HealthComponent");
        var recharge=health.Methods.Single(m=>m.Name=="ServerFixedUpdate").Body.Instructions;
        if(!recharge.Any(i=>i.OpCode==OpCodes.Ldc_R4&&(float)i.Operand==.5f)||
           !recharge.Any(i=>i.Operand is MethodReference m&&m.Name=="get_outOfDanger"))
            throw new Exception("Native shield recharge contract changed");
        if(!instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="RechargeShield"))
            throw new Exception("Newly granted shield capacity is no longer restored by native stats");
        if(recharge.Count(i=>i.Operand is MethodReference m&&m.Name=="set_Networkshield")!=1)
            throw new Exception("Native item shield recharge must have one intercepted write");
        var setter=health.Methods.Single(m=>m.Name=="set_Networkshield");
        if(setter.Parameters.Count!=1||setter.Parameters[0].ParameterType.FullName!="System.Single")
            throw new Exception("Native shield setter hook signature changed");
        var damage=health.Methods.Single(m=>m.Name=="TakeDamageProcess").Body.Instructions;
        int barrier=damage.ToList().FindIndex(i=>i.Operand is MethodReference m&&m.Name=="set_Networkbarrier");
        int shield=damage.ToList().FindIndex(i=>i.Operand is MethodReference m&&m.Name=="set_Networkshield");
        if(barrier<0||shield<=barrier)throw new Exception("Native damage must spend barrier before shield");
        Console.WriteLine("PASS: native stat observation, untouched item shield recharge and barrier-before-shield damage order.");
        var model=game.MainModule.Types.Single(t=>t.FullName=="RoR2.CharacterModel");
        var overlays=model.Methods.Single(m=>m.Name=="UpdateOverlays").Body.Instructions;
        var overlaySites=overlays.Where(i=>i.OpCode==OpCodes.Ldfld&&i.Operand is FieldReference f&&
            f.Name=="shield"&&f.DeclaringType.FullName=="RoR2.HealthComponent").ToArray();
        if(overlaySites.Length!=2||overlaySites.Any(i=>i.Next==null||i.Next.Next==null||
            i.Next.OpCode!=OpCodes.Ldc_R4||(float)i.Next.Operand!=0f||i.Next.Next.OpCode!=OpCodes.Cgt))
            throw new Exception("Native shield overlay presentation must have two shield > 0 comparisons");
        foreach(var material in new[]{"shieldBoosterOverlay","voidShieldBoosterOverlay","energyShieldMaterial","voidShieldMaterial"})
            if(!overlays.Any(i=>i.Operand is FieldReference f&&f.Name==material))
                throw new Exception("Native shield overlay material branch changed: "+material);
        Console.WriteLine("PASS: ordinary and Shield Booster model-overlay hook contracts.");
    }
}
