using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using IvoryBastion;
using B=IvoryBastion.BastionBalance;
using S=IvoryBastion.BastionSetting;
internal static class KitBalanceChecks
{
    private static int count;
    private static void Check(bool value,string why){count++;if(!value)throw new Exception("1.2.2 kit balance: "+why);}
    private static void Near(double a,double b,string why)=>Check(Math.Abs(a-b)<.002,why+": "+a+" != "+b);
    private static readonly (S Key,float Old)[] Changed={
        (S.BaseDamage,12),(S.DamageGrowth,2.4f),(S.GunnerDamage,70),
        (S.NeedleProc,.6f),(S.MissileProc,.6f),(S.HeavyProc,1),(S.LaserProc,1),
        (S.SkyfallProc,1),(S.WingburstProc,.15f),(S.MineProc,.5f),(S.DroneProc,.5f),
        (S.MobileShieldDR,50),(S.SiegeShieldDR,50),(S.HardeningBonus,5),
        (S.IdleFabrication,14),(S.CombatFabrication,35),
        (S.MineTriggerRange,16),(S.MineWalkDistance,20)};
    private static BastionBank Build(string name,out int gunners,out double spent)
    {
        var bank=new BastionBank{Fabrication=99999};bank.Upgrades.Active=true;gunners=0;
        void Buy(BastionUpgradeNode node,int ranks)
        {for(int i=0;i<ranks;i++)Check(bank.Buy(100+(int)node,0,true),"real prerequisite/cost validation: "+name+" / "+node);}
        if(name=="full")
        {foreach(var node in BastionUpgradeTree.Nodes)Buy(node.Id,node.Maximum);gunners=bank.Slots;}
        else
        {
            if(name=="laser"||name=="laser+gunners"||name=="early-mixed")Buy(BastionUpgradeNode.IonOptics,3);
            if(name=="gunners"||name=="laser+gunners"||name=="early-mixed")
            {Buy(BastionUpgradeNode.Hangar,1);Buy(BastionUpgradeNode.Blueprints,1);gunners=bank.Slots;Check(gunners==2,"early Hangar/Blueprints gives two slots");}
            if(name=="loader"||name=="early-mixed")
            {Buy(BastionUpgradeNode.MissileRacks,3);Buy(BastionUpgradeNode.Autoloader,1);}
        }
        spent=99999-bank.Fabrication;return bank;
    }
    private sealed class Result
    {
        public string Build{get;set;}
        public int TargetCap{get;set;}
        public double Fabrication{get;set;}
        public double SustainedDps{get;set;}
        public double LoadedDps{get;set;}
        public double GunnerDps{get;set;}
        public double FirstGenerationAtGBonusDps{get;set;}
        public double FirstGenerationAtGProcsPerSecond{get;set;}
    }
    private static Result Siege(string name,int cap)
    {
        var bank=Build(name,out int gunners,out double spent);double stat=B.Get(S.BaseDamage);
        double heavy=1d/bank.HeavyEvery(false),normal=1-heavy;
        double average=normal*B.Fraction(S.MissileDamage)+heavy*B.Fraction(S.HeavyDamage);
        double reload=bank.SiegeReload*(1+(B.Get(S.AegisReload)-1)*B.Get(S.AegisDuration)/B.Get(S.AegisCooldown));
        // Only these mastered nodes grant no-kill, isolated-target ammo refunds.
        if(bank.Upgrades.Points[(int)BastionUpgradeNode.IonOptics]==3)reload+=1d/bank.LaserInterval;
        if(bank.Upgrades.Points[(int)BastionUpgradeNode.Autoloader]==3)reload+=3d/15;
        double laser=bank.LaserDamage/bank.LaserInterval;
        double sky=B.Get(S.SkyfallCount)*B.Fraction(S.SkyfallDamage)/B.Get(S.SkyfallCooldown);
        double mines=Math.Min(cap,BastionUtilityUpgrades.MineCount(bank))*BastionUtilityUpgrades.MineDamage(bank)/B.Get(S.MineCooldown);
        double drones=gunners>0?gunners*B.Fraction(S.GunnerDamage)/B.Get(S.GunnerInterval):B.Fraction(S.BulwarkDamage)/B.Get(S.BulwarkInterval);
        double weighted=reload*(normal*B.Fraction(S.MissileDamage)*B.Get(S.MissileProc)+heavy*B.Fraction(S.HeavyDamage)*B.Get(S.HeavyProc))
            +laser*B.Get(S.LaserProc)+sky*B.Get(S.SkyfallProc)+mines*B.Get(S.MineProc);
        double procs=.1*(reload*(normal*B.Get(S.MissileProc)+heavy*B.Get(S.HeavyProc))+B.Get(S.LaserProc)/bank.LaserInterval
            +B.Get(S.SkyfallCount)*B.Get(S.SkyfallProc)/B.Get(S.SkyfallCooldown)+Math.Min(cap,BastionUtilityUpgrades.MineCount(bank))*B.Get(S.MineProc)/B.Get(S.MineCooldown));
        // One AtG: native 10% * proc chance; damage is 3 * triggering damage.
        // Drone attackers have their own inventory, so never grant them owner's AtG.
        return new Result{Build=name,TargetCap=cap,Fabrication=spent,SustainedDps=(reload*average+laser+sky+mines+drones)*stat,
            LoadedDps=(B.Get(S.MissileRate)*average+laser+sky+mines+drones)*stat,GunnerDps=gunners>0?drones*stat:0,
            FirstGenerationAtGBonusDps=.3*weighted*stat,FirstGenerationAtGProcsPerSecond=procs};
    }
    private static double Mobile(bool full)
    {
        var bank=Build(full?"full":"base",out _,out _);
        double duty=bank.ShiftDuration/B.Get(S.ShiftCooldown);
        double needle=B.Get(S.NeedleRate)*B.Fraction(S.NeedleDamage)*bank.NeedleMultiplier*(1+B.Fraction(S.ShiftAttackSpeed)*duty);
        double rush=BastionUtilityUpgrades.RushShots(bank)*B.Fraction(S.WingburstDamage)*(1d/BastionUtilityUpgrades.RushCooldown(bank)+(full?1d/B.Get(S.ShiftCooldown):0));
        return(needle+rush)*B.Get(S.BaseDamage); // Drones dock while mobile.
    }
    internal static void Run(string root)
    {
        var current=B.Defaults();var previous=new float[current.Length];
        var baseline=JsonSerializer.Deserialize<Dictionary<string,float>>(File.ReadAllText(Path.Combine(root,"scripts","offline_checks","fixtures","balance112defaults.json")));
        Check(baseline.Count==current.Length,"published 1.1.2 fixture covers every setting");
        foreach(var spec in B.Specs)previous[(int)spec.Key]=baseline[spec.Key.ToString()];
        var before=new List<Result>();var after=new List<Result>();var mobile=new List<object>();
        try
        {
            foreach(var change in Changed)Near(previous[(int)change.Key],change.Old,"explicit former value matches published fixture");
            foreach(var spec in B.Specs)
                if(!Changed.Any(c=>c.Key==spec.Key))Near(current[(int)spec.Key],previous[(int)spec.Key],"all other kit and upgrade settings preserved");
            Near(current[(int)S.MineTriggerRange],30,"authorized 1.3.0 mine detection default");
            Near(current[(int)S.MineWalkDistance],50,"authorized 1.3.0 mine pursuit default");
            foreach(int level in new[]{1,10,30})
                Near((current[(int)S.BaseDamage]+(level-1)*current[(int)S.DamageGrowth])/(previous[(int)S.BaseDamage]+(level-1)*previous[(int)S.DamageGrowth]),10d/12,"damage reduction remains proportional through leveling");
            foreach(string name in new[]{"base","laser","gunners","laser+gunners","loader","early-mixed","full"})
            foreach(int cap in new[]{5,12})
            {
                B.Apply(previous);var old=Siege(name,cap);before.Add(old);
                B.Apply(current);var next=Siege(name,cap);after.Add(next);
                Near(old.Fabrication,next.Fabrication,"unchanged purchase cost and availability");
                Check(next.SustainedDps<=old.SustainedDps*.834&&next.SustainedDps>=old.SustainedDps*.77,"17-23% less raw sustained kit damage across common early paths");
                Check(next.LoadedDps<=old.LoadedDps*.834&&next.LoadedDps>=old.LoadedDps*.77,"loaded-battery damage falls as well as reload DPS");
                Check(next.FirstGenerationAtGBonusDps<old.FirstGenerationAtGBonusDps*.55,"at least 45% less first-generation AtG damage; no inherited drone items");
                Check(next.FirstGenerationAtGProcsPerSecond<old.FirstGenerationAtGProcsPerSecond*.65,"proc reduction is independent of raw damage reduction");
                if(name=="gunners")Near(next.Fabrication,210,"real early gunner purchase remains 210 FAB");
                if(name=="full")Near(next.Fabrication,5655,"full tree remains 5655 FAB");
            }
            foreach(bool full in new[]{false,true})
            {B.Apply(previous);double old=Mobile(full);B.Apply(current);double next=Mobile(full);Near(next/old,10d/12,"mobile includes both Needle and Rush with drones docked");mobile.Add(new{FullTree=full,Before=old,After=next});}
            Near(after.Single(r=>r.Build=="full"&&r.TargetCap==12).SustainedDps,113.75,"all-tree isolated boss reload budget");
            Near(after.Single(r=>r.Build=="gunners"&&r.TargetCap==5).SustainedDps,70.875,"early two-Gunner raw budget");
            double skyOld=.1*previous[(int)S.SkyfallProc]*previous[(int)S.SkyfallCount]*5;
            double skyNew=.1*current[(int)S.SkyfallProc]*current[(int)S.SkyfallCount]*5;
            Near(skyOld,5,"previous ten-warhead five-target volley AtG opportunity");Near(skyNew,2,"new volley reduces first-generation AtG opportunities by 60%");
            File.WriteAllText(Path.Combine(root,".build","kit-balance-122.json"),JsonSerializer.Serialize(new{
                Assumptions="Level 1, zero items/crit/armor unless explicitly one AtG. One immortal target; all weapons and allowed marks connect; cooldowns averaged and Aegis reload used perfectly. Gunners survive in range. No kill refunds, collateral damage, cast/travel losses or recursive proc chains. Loaded battery is temporary. First-generation AtG is an expectation, not a simulated complete item build. Vanilla rows are primaries; Bastion rows are combined kit, so this is context rather than an equal full-kit comparison.",
                Before=before,After=after,Mobile=mobile,ProcCoefficients=Changed.Where(c=>c.Key.ToString().EndsWith("Proc")).Select(c=>new{Setting=c.Key.ToString(),Before=c.Old,After=current[(int)c.Key]}),
                VanillaPrimaryReferences=new[]{new{Name="Captain quick shotgun",Dps=12d*8*1.2/(.9+.1),Condition="All 8 pellets connect; minimum charge, not focused long-range charge"},new{Name="MUL-T dual saw Power Mode",Dps=11d*10*2,Condition="Both saws connect in melee; Power Mode"},new{Name="Corrupted Void Fiend beam",Dps=12d*20,Condition="Corrupted form, sustained beam; corruption uptime not averaged"}},
                FiveTargetSkyfallAtG=new{Before=skyOld,After=skyNew}},new JsonSerializerOptions{WriteIndented=true}));
        }
        finally{B.Reset();}
        Console.WriteLine($"PASS: {count} kit-only early-purchase, leveling, reload, full-tree and first-generation item-proc budget checks; wrote .build/kit-balance-122.json.");
    }
}
