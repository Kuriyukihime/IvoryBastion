using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using IvoryBastion;
using B=IvoryBastion.BastionBalance;
using S=IvoryBastion.BastionSetting;
internal static class DamageBudgetChecks
{
    private static int count;
    private static void Check(bool value,string why){count++;if(!value)throw new Exception("Damage budget: "+why);}
    private static BastionBank Bank(int tier)
    {var bank=new BastionBank();if(tier>0){bank.Ranks[0]=3;bank.Choices[0]=tier==1?0:1;bank.Ranks[2]=3;bank.Ranks[3]=3;}bank.Ammo=bank.Capacity;return bank;}
    private static double AverageMissile(BastionBank bank)=>B.Fraction(S.MissileDamage)+(B.Fraction(S.HeavyDamage)-B.Fraction(S.MissileDamage))/bank.HeavyEvery(false);
    private static double Drones(int tier)=>tier==0?B.Fraction(S.BulwarkDamage)/B.Get(S.BulwarkInterval):3*B.Fraction(S.GunnerDamage)/B.Get(S.GunnerInterval);
    private static double MineVolley(BastionBank bank,int cap)=>Math.Min(cap,BastionUtilityUpgrades.MineCount(bank))*BastionUtilityUpgrades.MineDamage(bank);
    private static double Supplement(int tier,int targetCap)=>Drones(tier)+B.Get(S.SkyfallCount)*B.Fraction(S.SkyfallDamage)/B.Get(S.SkyfallCooldown)+MineVolley(Bank(tier),targetCap)/B.Get(S.MineCooldown);
    private static object Row(string build,int tier,int cap,string enemyCategory)
    {
        var bank=Bank(tier);double average=AverageMissile(bank);
        double reload=B.Get(tier>0?S.UpgradedReload:S.SiegeReload)*(1+(B.Get(S.AegisReload)-1)*B.Get(S.AegisDuration)/B.Get(S.AegisCooldown));
        double laser=bank.LaserDamage/bank.LaserInterval,extra=Supplement(tier,cap);
        double sustain=reload*average+laser+extra,loaded=B.Get(S.MissileRate)*average+laser+extra;
        var sim=Simulate(tier,cap,600);
        if(build=="revised")
        {
            Check(sustain<11.6&&loaded<15.3,"strongest legal upgrade branch and twelve-mark boss remain below the high-end 20x/sec vanilla primary ceiling with room for item/AoE scaling");
            Check(sim.Total/600<11.7,"real stock/recharge simulation stays within sustained all-kit budget including opening reserves");
        }
        double initialAllowance=bank.Capacity*B.Fraction(S.HeavyDamage)+B.Get(S.SkyfallCount)*B.Fraction(S.SkyfallDamage)+MineVolley(bank,cap)+bank.LaserDamage;
        Check(sim.Total<=sustain*600+initialAllowance,"native stock simulation cannot create free reload or combine exclusive weapon upgrades");
        return new{build,enemyCategory,form=tier==0?"siege_base":tier==1?"siege_warhead_specialization":"siege_laser_specialization",targetCap=cap,mineCount=BastionUtilityUpgrades.MineCount(bank),minePercent=BastionUtilityUpgrades.MineDamage(bank)*100,mineVolleyPercent=MineVolley(bank,cap)*100,capacity=bank.Capacity,heavyEvery=bank.HeavyEvery(false),laserPercent=bank.LaserDamage*100,laserCooldown=bank.LaserInterval,missileAveragePercent=average*100,averagedReload=reload,sustainedCoefficientPerSecond=sustain*100,sustainedLevel1Dps=sustain*B.Get(S.BaseDamage),loadedBatteryCoefficientPerSecond=loaded*100,loadedBatteryLevel1Dps=loaded*B.Get(S.BaseDamage),simulated600sCoefficientPerSecond=sim.Total/6,simulated600sLevel1Dps=sim.Total/600*B.Get(S.BaseDamage),simulation=sim};
    }
    private sealed class Sample
    {public int Normal{get;set;}public int Heavy{get;set;}public int Lasers{get;set;}public int Aegis{get;set;}public int Skyfall{get;set;}public int MineDeployments{get;set;}public double ProducedAmmo{get;set;}public double Total{get;set;}}
    private static Sample Simulate(int tier,int cap,float seconds)
    {
        var bank=Bank(tier);var sample=new Sample();float actionGate=0,mineGate=0;
        const float dt=.01f;
        for(int frame=0;frame<(int)(seconds/dt);frame++)
        {
            float before=bank.Ammo;bank.Tick(dt,true,true);sample.ProducedAmmo+=bank.Ammo-before;actionGate-=dt;mineGate-=dt;
            if(bank.ProtocolCharges>0&&bank.Spend(BastionAction.Protocol,B.Get(S.AegisCooldown)))
            {bank.DefenseTime=B.Get(S.AegisDuration);sample.Aegis++;}
            if(bank.SaturationCharges>0&&bank.Spend(BastionAction.Saturation,B.Get(S.SkyfallCooldown)))
            {sample.Total+=B.Get(S.SkyfallCount)*B.Fraction(S.SkyfallDamage);sample.Skyfall++;}
            if(mineGate<=0)
            {mineGate+=B.Get(S.MineCooldown);sample.Total+=MineVolley(bank,cap);sample.MineDeployments++;}
            sample.Total+=Drones(tier)*dt;
            if(actionGate>0)continue;
            if(bank.Laser<=0)
            {sample.Lasers++;sample.Total+=bank.LaserDamage;bank.Laser=bank.LaserInterval;actionGate=.1f;}
            else if(bank.SpendMissile(false,out bool heavy))
            {if(heavy)sample.Heavy++;else sample.Normal++;sample.Total+=B.Fraction(heavy?S.HeavyDamage:S.MissileDamage);actionGate=BastionBank.PrimaryFireInterval(BastionAction.Missile,1);}
        }
        Check(sample.Normal+sample.Heavy<=bank.Capacity+sample.ProducedAmmo+.01,"native bank cannot spend more than its exact integrated reload plus opening stock");
        return sample;
    }
    private static object Mobile(string build,int tier)
    {
        double duty=B.Get(tier>0?S.UpgradedShiftDuration:S.ShiftDuration)/B.Get(S.ShiftCooldown);
        double primary=B.Get(S.NeedleRate)*B.Fraction(S.NeedleDamage)*(1+B.Fraction(S.ShiftAttackSpeed)*duty);
        var bank=Bank(tier);int shots=BastionUtilityUpgrades.RushShots(bank);float cooldown=BastionUtilityUpgrades.RushCooldown(bank);
        double rush=shots*B.Fraction(S.WingburstDamage)/cooldown;
        double total=primary+rush; // Mobile drones are docked.
        if(build=="revised")Check(total<13,"mobile all-kit single-target cycle stays below vanilla sustained ceiling despite its mobility");
        return new{build,form=tier==0?"mobile_base":"mobile_legacy_skirmisher",primaryCoefficientPerSecond=primary*100,rushPerCast=shots*B.Get(S.WingburstDamage),rushRecharge=cooldown,totalCoefficientPerSecond=total*100,level1Dps=total*B.Get(S.BaseDamage)};
    }
    private static void PurchasedUpgrades()
    {
        foreach(int choice in new[]{0,1})
        {
            var bank=new BastionBank{Fabrication=99999};
            for(int rank=1;rank<=3;rank++)
            {
                float before=bank.Fabrication;int cost=BastionBank.Cost(rank-1);
                Check(!bank.Buy(0,choice,false)&&bank.Fabrication==before&&bank.Ranks[0]==rank-1,"mobile purchase cannot grant mine upgrades or spend Fabrication");
                Check(bank.Buy(0,choice,true)&&bank.Ranks[0]==rank&&bank.Fabrication==before-cost,"real Ordnance purchase deducts exactly one rank cost");
                Check(BastionUtilityUpgrades.MineFieldRadius(bank)==18&&BastionUtilityUpgrades.MineCount(bank)==(rank>=2?30:24),"purchased field/count bonuses activate at their respective ranks");
                Check(Math.Abs(BastionUtilityUpgrades.MineDamage(bank)-(rank==3&&choice==0?1.05f:.9f))<.0001f&&BastionUtilityUpgrades.MineBlastRadius(bank)==(rank==3&&choice==1?10:8),"purchased specialization grants only its chosen mine bonus");
                Check(BastionUtilityUpgrades.RushDistance(bank)==15,"Ordnance purchase does not grant Skirmisher upgrades");
            }
            float finished=bank.Fabrication;Check(!bank.Buy(0,1-choice,true)&&bank.Fabrication==finished&&bank.Choices[0]==choice,"completed specialization cannot be swapped or repurchased");
            for(int rank=1;rank<=3;rank++)
            {
                Check(bank.Buy(3,0,true),"real Skirmisher purchase succeeds");
                Check(BastionUtilityUpgrades.RushDistance(bank)==18&&BastionUtilityUpgrades.RushShots(bank)==(rank>=2?40:32)&&BastionUtilityUpgrades.RushCooldown(bank)==(rank==3?8:10),"purchased Rush range, salvo and cooldown activate at their proper ranks");
                Check(BastionUtilityUpgrades.MineCount(bank)==30,"cross-branch purchase preserves the earlier mine upgrade");
            }
        }
        var poor=new BastionBank();Check(!poor.Buy(0,0,true)&&BastionUtilityUpgrades.MineCount(poor)==24,"unfunded purchase cannot grant mine count");
    }
    internal static void Run(string root)
    {
        PurchasedUpgrades();
        var revised=B.Defaults();var previous=(float[])revised.Clone();
        // The last review already contained the kit damage nerfs. Compare this
        // field/upgrade revision against that actual 12-mine, 150% review.
        previous[(int)S.MineCount]=12;previous[(int)S.MineDamage]=100;previous[(int)S.MineDeployRadius]=9;
        previous[(int)S.UpgradedMineCount]=12;previous[(int)S.UpgradedMineChainBonus]=previous[(int)S.MineChainBonus];
        previous[(int)S.UpgradedMineBlastRadius]=previous[(int)S.MineBlastRadius];
        previous[(int)S.UpgradedWingburstDistance]=previous[(int)S.WingburstDistance];
        previous[(int)S.UpgradedWingburstShots]=previous[(int)S.WingburstShots];
        previous[(int)S.UpgradedWingburstCooldown]=previous[(int)S.WingburstCooldown];
        var rows=new List<object>();
        try
        {
            foreach(bool updated in new[]{false,true})
            {
                B.Apply(updated?revised:previous);string label=updated?"revised":"previous_review";
                foreach(var category in new[]{(Name:"normal",Before:3,After:5),(Name:"elite",Before:5,After:8),(Name:"boss",Before:8,After:12)})
                    for(int tier=0;tier<3;tier++)rows.Add(Row(label,tier,updated?category.After:category.Before,category.Name));
                rows.Add(Mobile(label,0));rows.Add(Mobile(label,1));
            }
            File.WriteAllText(Path.Combine(root,".build","damage-budget-report.json"),JsonSerializer.Serialize(new{assumptions="HISTORICAL exclusive-branch regression; not the current ranked tree. See kit-balance-113.json for current early/all-path builds. Level1, no items/crit/armor, one isolated living target, all shots connect, drones alive/in range. New attached limits are5 normal/8 elite/12 boss, shared across players; previous review used3/5/8. Long-run reload includes perfect Aegis use. Loaded-battery figures average cooldown skills and cannot be sustained indefinitely. Native bank simulation uses .01s ticks and .1s Ion Lance/.2s missile action gates; attachment/travel time and Aegis casting cost omitted, so it remains optimistic. Weapon upgrade specializations are exclusive, including their mine bonuses. Mobile averages Blue Shift and the actual Skirmisher Rush upgrades; opening extra Rush charge excluded from sustained averages. AoE collateral and item procs are not counted.",vanillaCorruptedVoidFiendCoefficientPerSecond=2000,vanillaDualSawMultCoefficientPerSecond=2000,rows},new JsonSerializerOptions{WriteIndented=true}));
        }
        finally{B.Reset();}
        Console.WriteLine($"PASS: {count} vanilla-referenced damage budgets and native stock/reload simulations; wrote .build/damage-budget-report.json.");
    }
}
