using B = IvoryBastion.BastionBalance;
using S = IvoryBastion.BastionSetting;
using System;
namespace IvoryBastion
{
    public enum BastionAction : byte { Pistol, Laser, Missile, Boost, Protocol, Transform, Redline, Saturation }
    public enum BastionUnitKind : byte { Guardian, Gunner, Interceptor, Repair }
    internal sealed class BastionBank
    {
        internal static int IdleFabricationPerMinute=>B.Int(S.IdleFabrication);
        internal static int CombatFabricationPerMinute=>B.Int(S.CombatFabrication);
        internal static float IdleFabrication=>IdleFabricationPerMinute/60f;
        internal static float CombatFabrication=>CombatFabricationPerMinute/60f;
        internal static float NeedleDamage=>B.Fraction(S.NeedleDamage);
        internal static float NeedleAmmoPerHit=>B.Get(S.NeedleAmmo);
        internal static float NeedleFireRate=>B.Get(S.NeedleRate);
        internal static float MissileFireRate=>B.Get(S.MissileRate);
        // The input-authority attack state owns the per-form firing cadence.
        internal static float PrimaryFireInterval(BastionAction action,float attackSpeed)=>1f/((action==BastionAction.Missile?MissileFireRate:NeedleFireRate)*Math.Max(.2f,attackSpeed));
        internal static float KillFabrication(bool boss,bool elite)=>boss?B.Get(S.BossReward):elite?B.Get(S.EliteReward):B.Get(S.KillReward);
        internal void AwardKillFabrication(bool siege,bool timerRunning,bool boss,bool elite)
        {if(siege&&timerRunning)Fabrication=Math.Min(99999,Fabrication+KillFabrication(boss,elite));}
        internal static float MissileRadius(bool heavy)=>B.Get(heavy?S.HeavyRadius:S.MissileRadius);
        internal static float LaserBlastRadius=>B.Get(S.LaserRadius);
        internal static float MissileDamage(bool heavy,bool ultimate)=>B.Fraction(heavy?S.HeavyDamage:S.MissileDamage)*(ultimate?1.25f:1);
        internal float Fabrication, Ammo=Math.Min(B.Int(S.MissileCapacity),B.Int(S.MissileStartingAmmo)), Laser, Protocol, Redline, Saturation, Toggle;
        internal float DefenseTime, RedlineTime, Construction=B.Get(S.FirstDroneDelay);
        internal readonly BastionThrusterReserve Thruster=new BastionThrusterReserve();
        internal int MissileSequence, BarrageSequence, Reserved;
        internal int ProtocolCapacity=1,SpecialCapacity=1,ProtocolCharges=1,RedlineCharges=1,SaturationCharges=1;
        internal readonly int[] Ranks=new int[4], Choices=new int[4];
        internal readonly BastionUpgradeState Upgrades=new BastionUpgradeState();
        internal readonly int[] Loadout=new int[3];
        internal int Capacity => Upgrades.Active?(int)Math.Round(BastionUpgradeTree.Toward(B.Int(S.MissileCapacity),B.Int(S.UpgradedCapacity),Upgrades,BastionUpgradeNode.MissileRacks)):Ranks[0]>=1?Math.Max(B.Int(S.MissileCapacity),B.Int(S.UpgradedCapacity)):B.Int(S.MissileCapacity);
        internal int Slots => Upgrades.Active?Math.Max(1,Math.Min(3,Upgrades.Rank(BastionUpgradeNode.Hangar)>=2?Math.Max(B.Int(S.BaseSlots),Math.Max(B.Int(S.SecondSlots),B.Int(S.ThirdSlots))):Upgrades.Rank(BastionUpgradeNode.Hangar)==1?Math.Max(B.Int(S.BaseSlots),B.Int(S.SecondSlots)):B.Int(S.BaseSlots))):Ranks[2]>=3?Math.Max(Math.Max(B.Int(S.BaseSlots),B.Int(S.SecondSlots)),B.Int(S.ThirdSlots)):Ranks[2]>=1?Math.Max(B.Int(S.BaseSlots),B.Int(S.SecondSlots)):B.Int(S.BaseSlots);
        internal float LaserInterval => Upgrades.Active?BastionUpgradeTree.Cooldown(B.Get(S.LaserCooldown),B.Get(S.UpgradedLaserCooldown),Upgrades,BastionUpgradeNode.IonCapacitors):B.Get(Ranks[0]==3&&Choices[0]==1?S.UpgradedLaserCooldown:S.LaserCooldown);
        internal float LaserDamage => Upgrades.Active?BastionUpgradeTree.Toward(B.Fraction(S.LaserDamage),B.Fraction(S.UpgradedLaserDamage),Upgrades,BastionUpgradeNode.IonOptics):B.Fraction(Ranks[0]==3&&Choices[0]==1?S.UpgradedLaserDamage:S.LaserDamage);
        internal int ThrusterBaseCapacity => Upgrades.Active?(int)Math.Round(BastionUpgradeTree.Toward(B.Int(S.BoostFuel),B.Int(S.UpgradedFuel),Upgrades,BastionUpgradeNode.ThrusterTanks)):Ranks[3]>=2?Math.Max(B.Int(S.BoostFuel),B.Int(S.UpgradedFuel)):B.Int(S.BoostFuel);
        internal float FlightMultiplier => Upgrades.Active?1+B.Fraction(S.UpgradeFlight)*Upgrades.Progress(BastionUpgradeNode.Airframe):Ranks[3]>=1?1+B.Fraction(S.UpgradeFlight):1;
        internal float SiegeReload=>Upgrades.Active?BastionUpgradeTree.Toward(B.Get(S.SiegeReload),B.Get(S.UpgradedReload),Upgrades,BastionUpgradeNode.Autoloader):B.Get(Ranks[0]>=2?S.UpgradedReload:S.SiegeReload);
        internal float ShiftDuration=>Upgrades.Active?BastionUpgradeTree.Toward(B.Get(S.ShiftDuration),B.Get(S.UpgradedShiftDuration),Upgrades,BastionUpgradeNode.BlueShiftDriver):B.Get(Ranks[3]==3?S.UpgradedShiftDuration:S.ShiftDuration);
        internal bool Ricochet=>Upgrades.Active?Upgrades.Maxed(BastionUpgradeNode.BlueShiftDriver):Ranks[3]==3;
        internal float NeedleMultiplier=>Upgrades.Active?1+.15f*Upgrades.Progress(BastionUpgradeNode.BlueShiftDriver):1;
        internal int HeavyEvery(bool barrage) => (Upgrades.Active?Upgrades.Maxed(BastionUpgradeNode.Autoloader):Ranks[0]==3&&Choices[0]==0)?(barrage?4:B.Int(S.UpgradedHeavyEvery)):(barrage?5:B.Int(S.HeavyEvery));
        internal float AmmoRate(bool siege) => (siege?SiegeReload:B.Get(S.MobileReload))*(siege&&DefenseTime>0?B.Get(S.AegisReload):1);
        internal float ShieldCapacityMultiplier=>Upgrades.Active?1+B.Fraction(S.ShieldUpgradeOne)*Upgrades.Progress(BastionUpgradeNode.ShieldMatrix)+Math.Max(0,B.Fraction(S.ShieldUpgradeTwo)-B.Fraction(S.ShieldUpgradeOne))*Upgrades.Progress(BastionUpgradeNode.Hardening):Ranks[1]>=2?1+B.Fraction(S.ShieldUpgradeTwo):Ranks[1]>=1?1+B.Fraction(S.ShieldUpgradeOne):1;
        internal bool HardenedShield=>Upgrades.Active?Upgrades.Maxed(BastionUpgradeNode.Hardening):Ranks[1]==3&&Choices[1]==0;
        internal bool RenewalShield=>Upgrades.Active?Upgrades.Maxed(BastionUpgradeNode.Renewal):Ranks[1]==3&&Choices[1]==1;
        internal float ShieldReduction(bool siege)=>Math.Min(.95f,(siege?BastionDefenseRules.SiegeShieldReduction:BastionDefenseRules.MobileShieldReduction)+(HardenedShield?B.Fraction(S.HardeningBonus):0));
        internal float AegisReduction=>B.Fraction(HardenedShield?S.HardeningDR:S.AegisDR);
        internal float BarrierFraction => Upgrades.Active?BastionUpgradeTree.Toward(B.Fraction(S.AegisBarrier),B.Fraction(S.UpgradedBarrier),Upgrades,BastionUpgradeNode.AegisCircuit):B.Fraction(Ranks[1]>=1?S.UpgradedBarrier:S.AegisBarrier);
        internal float DefenseHealFraction => Upgrades.Active?BastionUpgradeTree.Toward(B.Fraction(S.AegisHealing),B.Fraction(S.RenewalHealing),Upgrades,BastionUpgradeNode.Renewal):B.Fraction(RenewalShield?S.RenewalHealing:Ranks[1]>=2?S.UpgradedHealing:S.AegisHealing);
        internal bool CanUseUnit(int kind)=>kind>=0&&kind<=3&&(kind==0||(Upgrades.Active?Upgrades.Rank(BastionUpgradeNode.Blueprints)>=kind:Ranks[2]>=2));
        internal static int Cost(int rank) => rank==0?B.Int(S.CostOne):rank==1?B.Int(S.CostTwo):rank==2?B.Int(S.CostThree):int.MaxValue;
        // Called once for a damaging primary shot, never from item-proc events.
        internal float LoadNeedleHit(bool damagedEnemy)
        {
            if(!damagedEnemy)return 0;
            float before=Ammo;
            Ammo=Math.Min(Capacity-Reserved,Ammo+NeedleAmmoPerHit*(Upgrades.Active&&Upgrades.Maxed(BastionUpgradeNode.Airframe)&&Thruster.Active?1.5f:1));
            return Math.Max(0,Ammo-before);
        }
        internal bool Buy(int branch,int choice,bool siege)
        {
            if(branch>=BastionUpgradeTree.PurchaseOffset)return BastionUpgradeTree.TryBuy(Upgrades,ref Fabrication,branch-BastionUpgradeTree.PurchaseOffset,choice,siege);
            if(Upgrades.Active)return false;
            if(!siege||branch<0||branch>=4||choice<0||choice>1||Ranks[branch]>=3)return false;
            int cost=Cost(Ranks[branch]); if(Fabrication<cost)return false;
            Fabrication-=cost; Ranks[branch]++; if(Ranks[branch]==3)Choices[branch]=choice; return true;
        }
        internal bool SetUnit(int slot,int kind,bool siege)
        {
            if(!siege||slot<0||slot>=Slots||!CanUseUnit(kind))return false;
            Loadout[slot]=kind; return true;
        }
        internal void Tick(float dt,bool siege,bool combat,float protocolInterval=18,float redlineInterval=40,float saturationInterval=60,float productionSeconds=0)
        {
            if(dt<=0||float.IsNaN(dt)||float.IsInfinity(dt))return;
            // Integrate the overcharge boundary exactly rather than awarding a
            // full doubled tick after the six-second window has expired.
            float active=Math.Min(dt,Math.Max(0,DefenseTime));
            float rate=siege?SiegeReload:B.Get(S.MobileReload);
            Ammo=Math.Min(Capacity-Reserved,Ammo+rate*(dt+(siege?(B.Get(S.AegisReload)-1)*active:0)));
            if(siege&&productionSeconds>0&&!float.IsNaN(productionSeconds)&&!float.IsInfinity(productionSeconds))
                Fabrication=Math.Min(99999,Fabrication+productionSeconds*(combat?CombatFabrication:IdleFabrication));
            Laser=Math.Max(0,Laser-dt); Toggle=Math.Max(0,Toggle-dt);
            DefenseTime=Math.Max(0,DefenseTime-dt); RedlineTime=Math.Max(0,RedlineTime-dt);
            Recharge(ref ProtocolCharges,ProtocolCapacity,ref Protocol,dt,protocolInterval);
            Recharge(ref RedlineCharges,SpecialCapacity,ref Redline,dt,redlineInterval);
            Recharge(ref SaturationCharges,SpecialCapacity,ref Saturation,dt,saturationInterval);
            if(siege)Construction=Math.Max(0,Construction-dt);
        }
        private static void Recharge(ref int stock,int capacity,ref float remaining,float elapsed,float interval)
        {if(stock>=capacity){stock=capacity;remaining=0;return;}remaining-=elapsed;while(remaining<=0&&stock<capacity){stock++;if(stock<capacity)remaining+=Math.Max(.05f,interval);}if(stock==capacity)remaining=0;}
        internal int Stock(BastionAction a)=>a==BastionAction.Boost?(int)Math.Floor(Thruster.Fuel):a==BastionAction.Protocol?ProtocolCharges:a==BastionAction.Redline?RedlineCharges:a==BastionAction.Saturation?SaturationCharges:a==BastionAction.Laser?(Laser<=0?1:0):a==BastionAction.Transform?(Toggle<=0?1:0):1;
        internal void Capacities(int secondaryBonus,int specialBonus,float protocol,float redline,float saturation)
        {
            int pc=1+Math.Max(0,secondaryBonus),sc=1+Math.Max(0,specialBonus);
            Thruster.Resize(ThrusterBaseCapacity+Math.Max(0,secondaryBonus));
            if(pc>ProtocolCapacity&&ProtocolCharges==ProtocolCapacity)Protocol=protocol;
            if(sc>SpecialCapacity){if(RedlineCharges==SpecialCapacity)Redline=redline;if(SaturationCharges==SpecialCapacity)Saturation=saturation;}
            ProtocolCapacity=pc;SpecialCapacity=sc;ProtocolCharges=Math.Min(ProtocolCharges,pc);RedlineCharges=Math.Min(RedlineCharges,sc);SaturationCharges=Math.Min(SaturationCharges,sc);
        }
        internal bool Spend(BastionAction a,float interval)
        {
            switch(a){case BastionAction.Protocol:return SpendStock(ref ProtocolCharges,ProtocolCapacity,ref Protocol,interval);case BastionAction.Redline:return SpendStock(ref RedlineCharges,SpecialCapacity,ref Redline,interval);case BastionAction.Saturation:return SpendStock(ref SaturationCharges,SpecialCapacity,ref Saturation,interval);default:return false;}
        }
        private static bool SpendStock(ref int stock,int capacity,ref float timer,float interval){if(stock<=0)return false;if(stock==capacity)timer=interval;stock--;return true;}
        internal void Restock(BastionAction a,bool all=false)
        {switch(a){case BastionAction.Boost:Thruster.Restore(all?Thruster.Capacity:1);break;case BastionAction.Protocol:ProtocolCharges=all?ProtocolCapacity:Math.Min(ProtocolCapacity,ProtocolCharges+1);if(ProtocolCharges==ProtocolCapacity)Protocol=0;break;case BastionAction.Redline:RedlineCharges=all?SpecialCapacity:Math.Min(SpecialCapacity,RedlineCharges+1);if(RedlineCharges==SpecialCapacity)Redline=0;break;case BastionAction.Saturation:SaturationCharges=all?SpecialCapacity:Math.Min(SpecialCapacity,SaturationCharges+1);if(SaturationCharges==SpecialCapacity)Saturation=0;break;case BastionAction.Laser:Laser=0;break;case BastionAction.Transform:Toggle=0;break;}}
        internal void Advance(BastionAction a,float elapsed,float interval)
        {if(float.IsNaN(elapsed)||float.IsInfinity(elapsed)||elapsed<=0)return;switch(a){case BastionAction.Boost:Thruster.Restore(elapsed*Thruster.Capacity/Math.Max(.05f,interval));break;case BastionAction.Protocol:Recharge(ref ProtocolCharges,ProtocolCapacity,ref Protocol,elapsed,interval);break;case BastionAction.Redline:Recharge(ref RedlineCharges,SpecialCapacity,ref Redline,elapsed,interval);break;case BastionAction.Saturation:Recharge(ref SaturationCharges,SpecialCapacity,ref Saturation,elapsed,interval);break;case BastionAction.Laser:Laser=Math.Max(0,Laser-elapsed);break;case BastionAction.Transform:Toggle=Math.Max(0,Toggle-elapsed);break;}}
        internal static float RechargeSeconds(float interval,float scale,float flat,float penalty=0)=>Math.Min(interval,Math.Max(.5f,interval*scale-flat))+penalty;
        internal bool SpendMissile(bool barrage,out bool heavy)
        {
            heavy=false;
            if(barrage){if(Reserved<=0)return false;Reserved--;heavy=++BarrageSequence%HeavyEvery(true)==0;}
            else{if(Ammo<1)return false;Ammo--;heavy=++MissileSequence%HeavyEvery(false)==0;}
            return true;
        }
        internal int ReserveBarrage(){int count=(int)Math.Floor(Ammo);Ammo-=count;Reserved+=count;BarrageSequence=0;return count;}
        internal void RefundBarrage(){Ammo=Math.Min(Capacity,Ammo+Reserved);Reserved=0;}
        internal float DamageMultiplier(bool siege,float abilityShield=0) =>
            (BastionDefenseRules.ShieldActive(abilityShield)?1-ShieldReduction(siege):1)*(siege&&DefenseTime>0?1-AegisReduction:1);
        internal static float PredictedDamage(float damage,float armor) => damage*(armor>=0?100/(100+armor):2-100/(100-armor));
    }
}
