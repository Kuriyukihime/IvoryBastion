using System;
using B=IvoryBastion.BastionBalance;
using S=IvoryBastion.BastionSetting;
namespace IvoryBastion
{
    // One source of truth for host attacks, owner prediction and presentation.
    // Ranks/choices already travel in the original authoritative bank status.
    internal static class BastionUtilityUpgrades
    {
        private static bool Rank(BastionBank bank,int branch,int rank)=>bank!=null&&bank.Ranks[branch]>=rank;
        internal static float RushDistance(BastionBank bank)=>bank!=null&&bank.Upgrades.Active?BastionUpgradeTree.Toward(B.Get(S.WingburstDistance),B.Get(S.UpgradedWingburstDistance),bank.Upgrades,BastionUpgradeNode.HeavenfallRigs):Rank(bank,3,1)?Math.Max(B.Get(S.WingburstDistance),B.Get(S.UpgradedWingburstDistance)):B.Get(S.WingburstDistance);
        internal static int RushShots(BastionBank bank)=>bank!=null&&bank.Upgrades.Active?(int)Math.Round(BastionUpgradeTree.Toward(B.Int(S.WingburstShots),B.Int(S.UpgradedWingburstShots),bank.Upgrades,BastionUpgradeNode.HeavenfallRigs)):Rank(bank,3,2)?Math.Max(B.Int(S.WingburstShots),B.Int(S.UpgradedWingburstShots)):B.Int(S.WingburstShots);
        internal static float RushCooldown(BastionBank bank)=>bank!=null&&bank.Upgrades.Active?BastionUpgradeTree.Cooldown(B.Get(S.WingburstCooldown),B.Get(S.UpgradedWingburstCooldown),bank.Upgrades,BastionUpgradeNode.HeavenfallRigs):Rank(bank,3,3)?Math.Min(B.Get(S.WingburstCooldown),B.Get(S.UpgradedWingburstCooldown)):B.Get(S.WingburstCooldown);
        internal static float MineFieldRadius(BastionBank bank)=>bank!=null&&bank.Upgrades.Active?BastionUpgradeTree.Toward(B.Get(S.MineDeployRadius),B.Get(S.UpgradedMineDeployRadius),bank.Upgrades,BastionUpgradeNode.WidowFabricators):Rank(bank,0,1)?Math.Max(B.Get(S.MineDeployRadius),B.Get(S.UpgradedMineDeployRadius)):B.Get(S.MineDeployRadius);
        internal static int MineCount(BastionBank bank)=>bank!=null&&bank.Upgrades.Active?(int)Math.Round(BastionUpgradeTree.Toward(B.Int(S.MineCount),B.Int(S.UpgradedMineCount),bank.Upgrades,BastionUpgradeNode.WidowFabricators)):Rank(bank,0,2)?Math.Max(B.Int(S.MineCount),B.Int(S.UpgradedMineCount)):B.Int(S.MineCount);
        internal static float MineChargeBonus(BastionBank bank)=>bank!=null&&bank.Upgrades.Active?BastionUpgradeTree.Toward(B.Get(S.MineChainBonus),B.Get(S.UpgradedMineChainBonus),bank.Upgrades,BastionUpgradeNode.ChargedNetwork):Rank(bank,0,3)&&bank.Choices[0]==0?Math.Max(B.Get(S.MineChainBonus),B.Get(S.UpgradedMineChainBonus)):B.Get(S.MineChainBonus);
        internal static float MineDamage(BastionBank bank)=>B.Fraction(S.MineDamage)*(1+MineChargeBonus(bank)/100);
        internal static float MineBlastRadius(BastionBank bank)=>bank!=null&&bank.Upgrades.Active?BastionUpgradeTree.Toward(B.Get(S.MineBlastRadius),B.Get(S.UpgradedMineBlastRadius),bank.Upgrades,BastionUpgradeNode.ChargedNetwork):Rank(bank,0,3)&&bank.Choices[0]==1?Math.Max(B.Get(S.MineBlastRadius),B.Get(S.UpgradedMineBlastRadius)):B.Get(S.MineBlastRadius);
    }
}
