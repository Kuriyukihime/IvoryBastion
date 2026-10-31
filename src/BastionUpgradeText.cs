using System;
using B=IvoryBastion.BastionBalance;
using S=IvoryBastion.BastionSetting;
namespace IvoryBastion
{
    internal static class BastionUpgradeText
    {
        private static string Range(float current,float maximum,string suffix="")=>Math.Abs(current-maximum)<.005f?$"{current:0.##}{suffix}":$"{current:0.##} → {maximum:0.##}{suffix}";
        internal static string Effect(BastionUpgradeNode node,BastionBank bank)
        {
            var tree=bank.Upgrades;int rank=tree.Rank(node);
            switch(node)
            {
                case BastionUpgradeNode.MissileRacks:return "Stockpile "+Range(bank.Capacity,Math.Max(B.Int(S.MissileCapacity),B.Int(S.UpgradedCapacity)));
                case BastionUpgradeNode.Autoloader:return "Reload "+Range(bank.SiegeReload,Math.Max(B.Get(S.SiegeReload),B.Get(S.UpgradedReload)),"/s")+$"\nMax rank: heavy every {B.Int(S.UpgradedHeavyEvery)} shots.";
                case BastionUpgradeNode.IonOptics:return "Ion Lance "+Range(bank.LaserDamage*100,Math.Max(B.Get(S.LaserDamage),B.Get(S.UpgradedLaserDamage)),"% damage");
                case BastionUpgradeNode.IonCapacitors:return "Beam recharge "+Range(bank.LaserInterval,Math.Min(B.Get(S.LaserCooldown),B.Get(S.UpgradedLaserCooldown)),"s");
                case BastionUpgradeNode.WidowFabricators:return "Mines "+Range(BastionUtilityUpgrades.MineCount(bank),Math.Max(B.Int(S.MineCount),B.Int(S.UpgradedMineCount)))+" / field "+Range(BastionUtilityUpgrades.MineFieldRadius(bank),Math.Max(B.Get(S.MineDeployRadius),B.Get(S.UpgradedMineDeployRadius)),"m");
                case BastionUpgradeNode.ChargedNetwork:return "Charged bonus "+Range(BastionUtilityUpgrades.MineChargeBonus(bank),Math.Max(B.Get(S.MineChainBonus),B.Get(S.UpgradedMineChainBonus)),"%")+"\nBlast "+Range(BastionUtilityUpgrades.MineBlastRadius(bank),Math.Max(B.Get(S.MineBlastRadius),B.Get(S.UpgradedMineBlastRadius)),"m");
                case BastionUpgradeNode.Hangar:return "Active drone slots: "+Range(bank.Slots,Math.Min(3,Math.Max(B.Int(S.BaseSlots),Math.Max(B.Int(S.SecondSlots),B.Int(S.ThirdSlots)))))+"\nEach point opens another slot.";
                case BastionUpgradeNode.Blueprints:return rank==0?"Next: Gunner. Then Interceptor, then Repairer.":rank==1?"Gunner unlocked. Next: Interceptor.":rank==2?"Gunner + Interceptor. Next: Repairer.":"All four drone roles unlocked.";
                case BastionUpgradeNode.ShieldMatrix:return "Shared shield capacity +"+Range(B.Get(S.ShieldUpgradeOne)*tree.Progress(node),B.Get(S.ShieldUpgradeOne),"%");
                case BastionUpgradeNode.Hardening:return "Further capacity +"+Range(Math.Max(0,B.Get(S.ShieldUpgradeTwo)-B.Get(S.ShieldUpgradeOne))*tree.Progress(node),Math.Max(0,B.Get(S.ShieldUpgradeTwo)-B.Get(S.ShieldUpgradeOne)),"%")+$"\nMax: +{B.Get(S.HardeningBonus):0.#}% shield DR / {B.Get(S.HardeningDR):0.#}% Aegis DR.";
                case BastionUpgradeNode.AegisCircuit:return "Aegis barrier "+Range(bank.BarrierFraction*100,Math.Max(B.Get(S.AegisBarrier),B.Get(S.UpgradedBarrier)),"% max HP");
                case BastionUpgradeNode.Renewal:return "Aegis healing "+Range(bank.DefenseHealFraction*100,Math.Max(B.Get(S.AegisHealing),B.Get(S.RenewalHealing)),"% max HP")+"\nShield kickstart rises to "+$"{Math.Max(25,B.Get(S.RenewalRestore)):0.#}% capacity.";
                case BastionUpgradeNode.Airframe:return "Flight speed +"+Range((bank.FlightMultiplier-1)*100,B.Get(S.UpgradeFlight),"%");
                case BastionUpgradeNode.ThrusterTanks:return "Thruster reserve "+Range(bank.ThrusterBaseCapacity,Math.Max(B.Int(S.BoostFuel),B.Int(S.UpgradedFuel)),"s");
                case BastionUpgradeNode.HeavenfallRigs:return "Rush: "+Range(BastionUtilityUpgrades.RushDistance(bank),Math.Max(B.Get(S.WingburstDistance),B.Get(S.UpgradedWingburstDistance)),"m")+" / "+Range(BastionUtilityUpgrades.RushShots(bank),Math.Max(B.Int(S.WingburstShots),B.Int(S.UpgradedWingburstShots))," missiles")+"\nRecharge "+Range(BastionUtilityUpgrades.RushCooldown(bank),Math.Min(B.Get(S.WingburstCooldown),B.Get(S.UpgradedWingburstCooldown)),"s");
                case BastionUpgradeNode.BlueShiftDriver:return "Needlefire damage +"+Range((bank.NeedleMultiplier-1)*100,15,"%")+"\nBlue Shift "+Range(bank.ShiftDuration,Math.Max(B.Get(S.ShiftDuration),B.Get(S.UpgradedShiftDuration)),"s");
                default:return "";
            }
        }
    }
}
