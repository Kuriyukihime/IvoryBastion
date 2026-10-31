using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using IvoryBastion;
using Mono.Cecil;
static class UnitRoleChecks
{
    static int count;
    static void Check(bool ok,string message){count++;if(!ok)throw new Exception("Drone roles: "+message);}
    static IEnumerable<TypeDefinition> Descendants(TypeDefinition type)
    {yield return type;foreach(var nested in type.NestedTypes)foreach(var child in Descendants(nested))yield return child;}
    internal static void Run(string root)
    {
        float Dps(BastionUnitKind kind)=>BastionUnitRules.ShotDamage(kind)/BastionUnitRules.ShotInterval(kind);
        float Durability(BastionUnitKind kind)=>BastionUnitRules.HealthMultiplier(kind)*(1+BastionUnitRules.Armor(kind)/100);
        Check(Dps(BastionUnitKind.Gunner)>=6*Dps(BastionUnitKind.Guardian),"damage drone still provides at least six times the defender's damage after kit tuning");
        Check(Durability(BastionUnitKind.Guardian)>3*Durability(BastionUnitKind.Gunner),"defender meaningfully outlasts the damage drone");
        Check(BastionUnitRules.ShotRange(BastionUnitKind.Gunner)>3*BastionUnitRules.ShotRange(BastionUnitKind.Guardian),"ranged gunner and close defender have distinct reach");
        Check(Dps(BastionUnitKind.Repair)==0&&Dps(BastionUnitKind.Interceptor)==0,"support units cannot also provide offensive damage");
        var allies=new[]{new{Id="full health, depleted shield",Health=100f,Maximum=100f,Combined=.09f},new{Id="injured",Health=25f,Maximum=100f,Combined=.25f}};
        Check(allies.OrderBy(a=>a.Combined).First().Health==100,"regression fixture reproduces shield-based healing starvation");
        Check(allies.OrderBy(a=>BastionUnitRules.RepairPriority(a.Health,a.Maximum)).First().Id=="injured","healing reaches missing health despite another ally's empty shield");
        Check(BastionUnitRules.RepairPriority(10,100)<BastionUnitRules.RepairPriority(70,100),"repair prioritizes the most injured health fraction");
        foreach(var value in new[]{(0f,100f),(100f,100f),(110f,100f),(-1f,100f),(1f,0f),(float.NaN,100f),(10f,float.NaN),(float.PositiveInfinity,100f),(1f,float.PositiveInfinity)})
            Check(float.IsPositiveInfinity(BastionUnitRules.RepairPriority(value.Item1,value.Item2)),"dead, full, overhealed and invalid targets do not waste a healing pulse");
        using var assembly=AssemblyDefinition.ReadAssembly(Path.Combine(root,"bin/Release/netstandard2.1/IvoryBastion.dll"));
        var unit=assembly.MainModule.Types.Single(t=>t.Name=="BastionUnit");
        var calls=Descendants(unit).SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Where(i=>i.Operand is MethodReference).Select(i=>(MethodReference)i.Operand).ToArray();
        foreach(string name in new[]{"HealthMultiplier","ShotInterval","ShotDamage","ShotRange","RepairPriority"})
            Check(calls.Any(m=>m.DeclaringType.Name=="BastionUnitRules"&&m.Name==name),"actual drone behavior uses shared "+name+" rules");
        Check(calls.Any(m=>m.DeclaringType.Name=="BulletAttack"&&m.Name=="Fire")&&calls.Any(m=>m.DeclaringType.Name=="NetworkServer"&&m.Name=="get_active"),"drone attacks retain host-only native damage/item events");
        var create=assembly.MainModule.Types.Single(t=>t.Name=="BastionContent").Methods.Single(m=>m.Name=="CreateUnits");
        Check(create.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="BastionUnitRules"&&m.Name=="Armor"),"each network prefab receives its role armor");
        Console.WriteLine($"PASS: {count} drone specialization, healing starvation and compiled integration checks.");
    }
}
