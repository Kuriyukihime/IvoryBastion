using System;
using System.Linq;
using System.Reflection;
using IvoryBastion;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System.IO;
using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

static class Checks
{
    static int count;
    static readonly Func<float,HealthComponent,DamageInfo,float> Absorb=typeof(BastionAbilityShield)
        .GetMethod("Absorb",BindingFlags.NonPublic|BindingFlags.Static)
        .CreateDelegate<Func<float,HealthComponent,DamageInfo,float>>();
    static void Check(bool ok,string why){count++;if(!ok)throw new Exception("Defense integration: "+why);}
    static void Near(float actual,float expected,string why,float tolerance=.003f)=>Check(Math.Abs(actual-expected)<tolerance,why+$": {actual} != {expected}");
    sealed class Actor
    {
        internal readonly BastionController C=new();
        internal CharacterBody Body=>C.Body;internal HealthComponent Hp=>Body.healthComponent;
        internal Actor(bool siege=true,float maxHealth=100)
        {
            C.Sieged=siege;C.Body=new(){Controller=C,maxHealth=maxHealth,requestedMaxHealth=maxHealth};
            Body.healthComponent=new(){body=Body,health=maxHealth};
            C.AbilityShield.Resize(BastionDefenseRules.ShieldCapacity(maxHealth,siege));
        }
        internal void Hit(float amount,bool blocked=false,bool rejected=false,bool bypassArmor=false)=>
            BastionDamageHook.Apply(NativeDamage,Hp,new(){damage=amount,blocked=blocked,rejected=rejected,bypassArmor=bypassArmor});
    }
    // This stand-in models only the installed game's native ordering. The
    // outer damage hook and custom absorption callback are production source.
    static void NativeDamage(HealthComponent hp,DamageInfo info)
    {
        if(!NetworkServer.active||!hp.alive||info.rejected||info.blocked)return;
        float damage=info.damage;
        if(!info.bypassArmor)damage=BastionBank.PredictedDamage(damage,hp.body.armor);
        hp.LastReportedDamage=damage;
        float paid=Math.Min(hp.barrier,damage);hp.barrier-=paid;damage-=paid;
        damage=Absorb(damage,hp,info);
        paid=Math.Min(hp.shield,damage);hp.shield-=paid;damage-=paid;
        hp.health=Math.Max(0,hp.health-damage);
    }
    static void Main()
    {
        BastionBalance.Reset();NetworkServer.active=true;Time.time=10;
        DamageAndClocks();RechargeAndCues();HealthAndStatObservation();HealingAndDecay();Conservation();PackagedIntegration();
        Console.WriteLine($"PASS: {count:N0} production damage-hook/absorption/recharge/stat-observer integration checks (engine stand-ins; no game launched).");
    }
    static void DamageAndClocks()
    {
        foreach(bool siege in new[]{false,true})
        {
            var a=new Actor(siege);float before=a.C.AbilityShield.Charge;a.Hit(24);
            Near(before-a.C.AbilityShield.Charge,14.4f,"24 incoming damage spends 14.4 Seraph points in either form");
            Near(a.Hp.LastReportedDamage,24,"floating report can remain twenty-four before custom absorption");
            Near(a.Hp.health,100,"Seraph prevents hull damage");Near(a.C.LastShieldHit,Time.time,"Seraph damage restarts only its recharge clock");
        }
        var actor=new Actor();actor.C.Bank.DefenseTime=6;actor.C.AegisBarrier.Grant(40,100);actor.Hit(24);
        Near(actor.C.AegisBarrier.Charge,30.64f,"active Aegis plus Seraph spends 9.36 Aegis points");
        Near(actor.C.AbilityShield.Charge,50,"Aegis hit leaves Seraph points intact");Near(actor.C.LastShieldHit,-99,"Aegis hit leaves recharge clock intact");
        actor=new Actor();actor.Body.armor=20;actor.C.Bank.DefenseTime=6;actor.C.AegisBarrier.Grant(40,100);actor.Hit(24);
        Near(actor.C.AegisBarrier.Charge,32.2f,"armor and both reductions spend 7.8 points from a 24-damage hit");
        actor=new Actor();actor.C.Bank.Ranks[1]=3;actor.C.Bank.DefenseTime=6;actor.C.AegisBarrier.Grant(40,100);actor.Hit(100);
        Near(actor.C.AegisBarrier.Charge,6.4f,"Hardening combines to 66.4 percent protection");
        actor=new Actor();actor.C.AbilityShield.Receive(50,0);actor.C.Bank.DefenseTime=6;actor.C.AegisBarrier.Grant(40,100);actor.Hit(24);
        Near(actor.C.AegisBarrier.Charge,24.4f,"broken Seraph removes its bonus while Aegis retains 35 percent");
        actor=new Actor();actor.Hp.barrier=100;actor.C.AbilityShield.Absorb(40,.5f);float shield=actor.C.AbilityShield.Charge;actor.Hit(24);
        Near(actor.Hp.barrier,76,"item barrier never inherits Seraph reduction");Near(actor.C.AbilityShield.Charge,shield,"native barrier protects Seraph");
        Near(actor.C.LastShieldHit,-99,"native barrier does not interrupt Seraph recharge");Near(actor.C.LastHurt,Time.time,"native barrier damage still interrupts passive hull repair");
        actor=new Actor();actor.Hp.barrier=10;actor.Hp.shield=10;actor.C.AegisBarrier.Grant(10,100);actor.C.AbilityShield.Receive(50,10);actor.C.Bank.DefenseTime=6;actor.Hit(100);
        Near(actor.Hp.barrier+actor.Hp.shield+actor.C.AegisBarrier.Charge+actor.C.AbilityShield.Charge,0,"all four layers break in correct order");
        Near(actor.Hp.health,88.33333f,"cross-layer overflow reaches health without applying Seraph reduction twice");
        Check(actor.C.Effects.Count(e=>e==BastionFxKind.ShieldBreak)==1,"shield-break cue emitted once at actual depletion");
        actor.Hit(1);Check(actor.C.Effects.Count(e=>e==BastionFxKind.ShieldBreak)==1,"subsequent unshielded hits do not repeat break cue");
        foreach(bool rejected in new[]{false,true})
        {
            actor=new Actor();actor.Hit(100,blocked:!rejected,rejected:rejected);
            Near(actor.C.AbilityShield.Charge,50,"blocked or rejected damage spends no shield");Near(actor.C.LastHurt,-99,"blocked or rejected damage does not interrupt repair");
        }
        actor=new Actor();actor.Body.armor=100;actor.Hit(24,bypassArmor:true);Near(actor.C.AbilityShield.Charge,35.6f,"bypass-armor damage still receives Seraph protection");
        actor=new Actor();NetworkServer.active=false;actor.Hit(24);Near(actor.C.AbilityShield.Charge,50,"clients cannot spend defense pools");NetworkServer.active=true;
    }
    static void RechargeAndCues()
    {
        var actor=new Actor();actor.C.AbilityShield.Absorb(60,.5f);actor.C.LastShieldHit=0;
        Time.time=3.99f;BastionAbilityShield.Tick(actor.C,.02f);Near(actor.C.AbilityShield.Charge,20,"siege waits four seconds");
        Time.time=4.01f;BastionAbilityShield.Tick(actor.C,.02f);Near(actor.C.AbilityShield.Charge,20.25f,"only time beyond four-second boundary recharges");
        Check(actor.C.Effects.Count(e=>e==BastionFxKind.ShieldRecharge)==1,"recharge start emits one cue");
        actor.Hp.barrier=30;Time.time=4.1f;actor.Hit(24);BastionAbilityShield.Tick(actor.C,.1f);
        Near(actor.C.AbilityShield.Charge,22.75f,"recharge continues behind item barrier");
        Check(actor.C.Effects.Count(e=>e==BastionFxKind.ShieldRecharge)==1,"barrier-only hit does not restart recharge cue");
        actor.Hp.barrier=0;actor.Hit(4);float after=actor.C.AbilityShield.Charge;BastionAbilityShield.Tick(actor.C,1);
        Near(actor.C.AbilityShield.Charge,after,"direct Seraph hit restarts recharge delay");
        Time.time=8.2f;BastionAbilityShield.Tick(actor.C,.1f);Check(actor.C.Effects.Count(e=>e==BastionFxKind.ShieldRecharge)==2,"a later interrupted refill emits its new start");
        BastionAbilityShield.Restore(actor.C,100);Check(actor.C.Effects.Count(e=>e==BastionFxKind.ShieldRecharge)==2,"explicit restore has no natural recharge cue");
        actor=new Actor(false);actor.C.AbilityShield.Absorb(10,.5f);actor.C.LastShieldHit=0;
        Time.time=6.99f;BastionAbilityShield.Tick(actor.C,.02f);Near(actor.C.AbilityShield.Charge,10,"mobile waits seven seconds");
        Time.time=7.01f;BastionAbilityShield.Tick(actor.C,.02f);Near(actor.C.AbilityShield.Charge,10.075f,"mobile boundary recharge uses its own capacity");
    }
    static void HealthAndStatObservation()
    {
        BastionAbilityShield.Install();
        try
        {
            using var assembly=AssemblyDefinition.ReadAssembly(typeof(Checks).Assembly.Location);
            var native=assembly.MainModule.Types.Single(t=>t.FullName=="RoR2.CharacterBody").Methods.Single(m=>m.Name==nameof(CharacterBody.NativeStats));
            // Compile a normal managed probe assembly: the game's older
            // MonoMod dynamic-method generator targets Unity's Mono runtime.
            using var probe=AssemblyDefinition.CreateAssembly(new("DefenseStatProbe",new Version(1,0)),"DefenseStatProbe",ModuleKind.Dll);
            var method=CopyNativeStats(native,probe.MainModule);
            using var context=new ILContext(method){ReferenceBag=new RuntimeILReferenceBag()};
            context.Invoke(IL.RoR2.CharacterBody.Patch);
            using var bytes=new MemoryStream();probe.Write(bytes);
            var stats=Assembly.Load(bytes.ToArray()).GetType("StatProbe").GetMethod("Run").CreateDelegate<Action<CharacterBody>>();
            foreach(bool siege in new[]{false,true})foreach(bool converted in new[]{false,true})foreach(int rank in new[]{0,1,2})
            {
                var actor=new Actor(siege);actor.Body.convertHealth=converted;actor.Body.lateMultiplier=2;
                actor.C.Bank.Ranks[1]=rank;
                actor.C.AbilityShield.Absorb(10,.5f);
                On.RoR2.CharacterBody.Run(body=>stats(body),actor.Body);
                float capacity=(siege?100:30)*(rank==2?1.4f:rank==1?1.2f:1);
                Near(actor.C.AbilityShield.Capacity,capacity,"executed stat observer includes late doubling with or without Transcendence");
                Near(actor.C.AbilityShield.Charge,capacity-5,"health growth preserves existing shield damage");
                Near(actor.Body.maxHealth,converted?2:200,"observer preserves native final max health");
                Near(actor.Body.maxShield,converted?300:0,"observer preserves native item/conversion shields");
                On.RoR2.CharacterBody.Run(body=>stats(body),actor.Body);Near(actor.C.AbilityShield.Charge,capacity-5,"repeated stat updates do not refill unchanged capacity");
                actor.Body.cursePenalty=2;On.RoR2.CharacterBody.Run(body=>stats(body),actor.Body);
                Near(actor.C.AbilityShield.Capacity,capacity/2,"curse applies exactly once");
            }
        }
        finally{BastionAbilityShield.Remove();}
        var late=new Actor();late.C.AbilityShield.Absorb(40,.5f);late.Body.maxHealth=200;late.C.LastShieldHit=Time.time;
        BastionAbilityShield.Tick(late.C,.02f);Near(late.C.AbilityShield.Capacity,100,"post-wrapper max-health change caught next host tick");
        Near(late.C.AbilityShield.Charge,80,"late max-health growth adds capacity without repairing old damage");
    }
    static MethodDefinition CopyNativeStats(MethodDefinition source,ModuleDefinition module)
    {
        var type=new TypeDefinition("","StatProbe",Mono.Cecil.TypeAttributes.Public|Mono.Cecil.TypeAttributes.Abstract|Mono.Cecil.TypeAttributes.Sealed,module.ImportReference(typeof(object)));module.Types.Add(type);
        var method=new MethodDefinition("Run",Mono.Cecil.MethodAttributes.Public|Mono.Cecil.MethodAttributes.Static,module.TypeSystem.Void);
        method.Parameters.Add(new(module.ImportReference(typeof(CharacterBody))));type.Methods.Add(method);
        method.Body.InitLocals=source.Body.InitLocals;method.Body.MaxStackSize=source.Body.MaxStackSize;
        var locals=source.Body.Variables.ToDictionary(v=>v,v=>new VariableDefinition(module.ImportReference(v.VariableType)));
        foreach(var local in source.Body.Variables)method.Body.Variables.Add(locals[local]);
        var instructions=source.Body.Instructions.ToDictionary(i=>i,i=>Instruction.Create(OpCodes.Nop));
        foreach(var original in source.Body.Instructions)
        {
            var copy=instructions[original];copy.OpCode=original.OpCode;
            copy.Operand=original.Operand switch
            {
                Instruction target=>instructions[target],
                Instruction[] targets=>targets.Select(t=>instructions[t]).ToArray(),
                VariableDefinition local=>locals[local],
                MethodReference call=>module.ImportReference(call),
                FieldReference field=>module.ImportReference(field),
                TypeReference value=>module.ImportReference(value),
                _=>original.Operand
            };
            method.Body.Instructions.Add(copy);
        }
        return method;
    }
    static void HealingAndDecay()
    {
        foreach(int rank in new[]{0,2,3})
        {
            var bank=new BastionBank{DefenseTime=6};bank.Ranks[1]=rank;bank.Choices[1]=1;
            var repair=new BastionRepair();float healing=0;int pulses=0;
            for(int frame=0;frame<350;frame++)
            {
                float before=bank.DefenseTime;bank.Tick(.02f,true,false);
                float amount=repair.Tick(.02f,true,100,0,false,before,bank.DefenseHealFraction);healing+=amount;if(amount>0)pulses++;
            }
            healing+=repair.Flush();Near(healing,rank==0?18:rank==2?30:35,"Aegis healing duration and defense upgrade total");
            Check(pulses<=25,"ordinary healing stays batched instead of spamming one item proc per frame");
        }
        var aegis=new BastionAegisReserve();aegis.Grant(40,100);aegis.Tick(1,100);Near(aegis.Charge,35,"Aegis retains native barrier decay curve");
        aegis.Grant(1000,100);Near(aegis.Charge,100,"stacked Aegis casts retain full-barrier cap");aegis.Tick(0,50);Near(aegis.Charge,50,"max-health loss clamps existing Aegis");
        var actor=new Actor();actor.C.AbilityShield.Absorb(60,.5f);actor.C.LastShieldHit=Time.time;
        actor.Hp.health=100;actor.Hp.shield=100;actor.Hp.barrier=100;BastionAbilityShield.Tick(actor.C,0);
        Near(actor.C.AbilityShield.Charge,20,"healing, item shields and item barrier never refill Seraph");
    }
    static void Conservation()
    {
        var random=new Random(3201);Time.time=20;
        for(int i=0;i<3000;i++)
        {
            var a=new Actor(i%2==0,4096);float s=i%7==0?0:(float)random.NextDouble()*50,y=(float)random.NextDouble()*40;
            a.C.AbilityShield.Receive(100,s);a.C.AegisBarrier.Receive(40,y);a.Hp.barrier=(float)random.NextDouble()*40;a.Hp.shield=(float)random.NextDouble()*20;
            a.Body.armor=(float)random.NextDouble()*230-30;a.C.Bank.DefenseTime=i%3==0?6:0;
            float nativeBarrier=a.Hp.barrier,nativeShield=a.Hp.shield,raw=(float)random.NextDouble()*2000;
            float incoming=BastionBank.PredictedDamage(raw*a.C.Bank.DamageMultiplier(a.C.Sieged),a.Body.armor);
            a.Hit(raw);
            float accounted=nativeBarrier-a.Hp.barrier+(y-a.C.AegisBarrier.Charge)/(s>0?.6f:1)+(s-a.C.AbilityShield.Charge)/.6f+nativeShield-a.Hp.shield+4096-a.Hp.health;
            Near(accounted,incoming,"production hooks conserve post-armor damage across all native/custom layers",.005f);
            Check(a.C.AbilityShield.Charge>=0&&a.C.AegisBarrier.Charge>=0&&a.Hp.shield>=0&&a.Hp.barrier>=0,"no layer creates negative resources");
        }
    }
    static void PackagedIntegration()
    {
        using var mod=AssemblyDefinition.ReadAssembly(Path.Combine("bin","Release","netstandard2.1","IvoryBastion.dll"));
        TypeDefinition Type(string name)=>mod.MainModule.Types.Single(t=>t.Name==name);
        bool Calls(MethodDefinition method,string type,string name)=>method.Body.Instructions.Any(i=>i.Operand is MethodReference call&&call.DeclaringType.Name==type&&call.Name==name);
        var plugin=Type("BastionPlugin");var hook=Type("BastionDamageHook").Methods.Single(m=>m.Name=="Apply");
        Check(Calls(plugin.Methods.Single(m=>m.Name=="Damage"),"BastionDamageHook","Apply"),"shipped plugin routes incoming damage through tested production hook");
        Check(plugin.Methods.Single(m=>m.Name=="Awake").Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldftn&&i.Operand is MethodReference m&&m.Name=="Damage"),"tested hook remains registered with the native damage event");
        Check(Calls(hook,"NetworkServer","get_active")&&Calls(hook,"BastionBank","DamageMultiplier")&&Calls(hook,"orig_TakeDamage","Invoke"),"shipped damage hook retains host gate, Aegis multiplier and native damage processing");
        Check(!hook.Body.Instructions.Any(i=>i.OpCode==OpCodes.Stfld&&i.Operand is FieldReference f&&f.Name=="LastShieldHit"),"native barrier hit tracking never interrupts Seraph recharge");
        var controller=Type("BastionController");var secondary=controller.Methods.Single(m=>m.Name=="ActivateSecondary");
        Check(Calls(secondary,"NetworkServer","get_active")&&Calls(secondary,"BastionBank","Spend")&&Calls(secondary,"BastionAegisReserve","Grant"),"actual Aegis cast still spends one host-owned charge and grants only its independent pool");
        Check(Calls(controller.Methods.Single(m=>m.Name=="FixedUpdate"),"HealthComponent","Heal"),"Aegis and passive repair remain ordinary item-compatible healing");
    }
}
