using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using IvoryBastion;
using B=IvoryBastion.BastionBalance;
using S=IvoryBastion.BastionSetting;
internal static class UpgradeDamageBudgetChecks
{
    private static int count;
    private static void Check(bool value,string why){count++;if(!value)throw new Exception("Point-tree damage budget: "+why);}
    private static BastionBank Build(bool full)
    {
        var bank=new BastionBank{Fabrication=99999};bank.Upgrades.Active=true;
        if(full)foreach(var d in BastionUpgradeTree.Nodes)for(int p=0;p<d.Maximum;p++)Check(bank.Buy(100+(int)d.Id,0,true),"all weapon paths can actually coexist; no exclusive-branch assumptions");
        bank.Ammo=bank.Capacity;return bank;
    }
    private static double Drones(BastionBank bank,bool full)=>full?bank.Slots*B.Fraction(S.GunnerDamage)/B.Get(S.GunnerInterval):B.Fraction(S.BulwarkDamage)/B.Get(S.BulwarkInterval);
    private static double Mines(BastionBank bank,int cap)=>Math.Min(cap,BastionUtilityUpgrades.MineCount(bank))*BastionUtilityUpgrades.MineDamage(bank);
    internal static void Run(string root)
    {
        B.Reset();var rows=new List<object>();
        foreach(bool full in new[]{false,true})foreach(int cap in new[]{5,8,12})
        {
            var bank=Build(full);double average=B.Fraction(S.MissileDamage)+(B.Fraction(S.HeavyDamage)-B.Fraction(S.MissileDamage))/bank.HeavyEvery(false);
            double reload=bank.SiegeReload*(1+(B.Get(S.AegisReload)-1)*B.Get(S.AegisDuration)/B.Get(S.AegisCooldown));
            double bonus=full?1d/bank.LaserInterval+3d/15:0;
            double extra=bank.LaserDamage/bank.LaserInterval+Drones(bank,full)+B.Get(S.SkyfallCount)*B.Fraction(S.SkyfallDamage)/B.Get(S.SkyfallCooldown)+Mines(bank,cap)/B.Get(S.MineCooldown);
            double sustain=(reload+bonus)*average+extra,loaded=B.Get(S.MissileRate)*average+extra;
            var sim=Simulate(full,cap,600);
            Check(sustain<12.2&&loaded<16,"all-point no-kill budget remains below the existing 20x/sec high-end primary reference");
            Check(sim.Dps<148,"actual bank/cadence simulation including opening stock remains within optimistic full-kit budget");
            rows.Add(new{FullTree=full,TargetCap=cap,Capacity=bank.Capacity,ReloadPerSecond=reload,MasteryAmmoPerSecond=bonus,SustainedCoefficientPerSecond=sustain,Level1SustainedDps=sustain*B.Get(S.BaseDamage),LoadedBatteryDps=loaded*B.Get(S.BaseDamage),Simulated600s=new{sim.Dps,sim.Missiles,sim.Lasers}});
        }
        var mobile=Build(true);double duty=mobile.ShiftDuration/B.Get(S.ShiftCooldown);
        double primary=B.Get(S.NeedleRate)*B.Fraction(S.NeedleDamage)*mobile.NeedleMultiplier*(1+B.Fraction(S.ShiftAttackSpeed)*duty);
        double rush=BastionUtilityUpgrades.RushShots(mobile)*B.Fraction(S.WingburstDamage)*(1/BastionUtilityUpgrades.RushCooldown(mobile)+1/B.Get(S.ShiftCooldown));
        Check(primary+rush<9,"mobile damage includes Wing Capacitor without wrongly counting docked drones");
        File.WriteAllText(Path.Combine(root,".build","upgrade-damage-budget.json"),JsonSerializer.Serialize(new{Assumptions="Actual all-point tree, level1/active-profile base damage, one isolated non-dying target, no items/crit/armor. All missiles, ten Skyfall warheads and allowed marks connect; three Gunners stay alive/in range in siege. Perfect Aegis recharge boost. Reserve Kickstart and Piercing Siphon counted. No kill or three-target refunds in isolated-target DPS. Mobile drones are docked. Loaded values are temporary; opening reserves included only in simulation. AoE, items, travel and casting loss omitted. This is an optimistic bound, not live gameplay measurement.",Rows=rows,MobileFullTree=new{PrimaryDps=primary*B.Get(S.BaseDamage),RushDps=rush*B.Get(S.BaseDamage),TotalDps=(primary+rush)*B.Get(S.BaseDamage)},Economy=new{TotalPoints=46,AllTreeFabrication=5655,CoreNode=90,CoreAndAdvancedPath=765,EarlyGunnerAndSecondSlot=210,CombatMinutesForAdvancedPathWithoutKills=765d/B.Get(S.CombatFabrication),CombatMinutesForAdvancedPathWithTenNormalKillsPerMinute=765d/(B.Get(S.CombatFabrication)+10*B.Get(S.KillReward))}},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS: {count} simultaneous full-tree damage/economy checks; wrote .build/upgrade-damage-budget.json.");
    }
    private static (double Dps,int Missiles,int Lasers) Simulate(bool full,int cap,int seconds)
    {
        var bank=Build(full);double damage=0;int missiles=0,lasers=0;float gate=0,mineGate=0;const float dt=.01f;
        for(int frame=0;frame<seconds/dt;frame++)
        {
            bank.Tick(dt,true,true);gate-=dt;mineGate-=dt;
            if(full&&bank.Ammo<1&&bank.Upgrades.Perk(BastionUpgradeNode.Autoloader,frame*dt,15))bank.Ammo=Math.Min(bank.Capacity,bank.Ammo+3);
            if(bank.ProtocolCharges>0&&bank.Spend(BastionAction.Protocol,B.Get(S.AegisCooldown)))bank.DefenseTime=B.Get(S.AegisDuration);
            if(bank.SaturationCharges>0&&bank.Spend(BastionAction.Saturation,B.Get(S.SkyfallCooldown)))damage+=B.Get(S.SkyfallCount)*B.Fraction(S.SkyfallDamage);
            if(mineGate<=0){mineGate+=B.Get(S.MineCooldown);damage+=Mines(bank,cap);}
            damage+=Drones(bank,full)*dt;if(gate>0)continue;
            if(bank.Laser<=0){damage+=bank.LaserDamage;bank.Laser=bank.LaserInterval;gate=.1f;lasers++;if(full)bank.Ammo=Math.Min(bank.Capacity,bank.Ammo+1);}
            else if(bank.SpendMissile(false,out bool heavy)){damage+=B.Fraction(heavy?S.HeavyDamage:S.MissileDamage);gate=BastionBank.PrimaryFireInterval(BastionAction.Missile,1);missiles++;}
        }
        return(damage/seconds*B.Get(S.BaseDamage),missiles,lasers);
    }
}
