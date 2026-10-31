using R2API;
using System.Collections.Generic;
using B = IvoryBastion.BastionBalance;
using S = IvoryBastion.BastionSetting;
namespace IvoryBastion
{
    internal static class BastionBalanceText
    {
        private static LanguageAPI.LanguageOverlay balanceOverlay;
        internal const string FactorySummary="Press <style=cIsUtility>V (default)</style> to open <style=cIsUtility>Engineering</style>.\nEarn <style=cIsUtility>Fabrication</style> in siege mode to buy upgrades that last the run. Free flight in mobile form; <style=cIsHealing>Seraph Shield</style> protects both forms.";
        internal static string[] Keywords(string skill)
        {
            switch(skill)
            {
                case "PASSIVE":return new[]{"KEYWORD_BASTION_FACTORY"};
                case "NEEDLE":return new[]{"KEYWORD_AGILE","KEYWORD_BASTION_NEEDLE_ASSIST"};
                case "BOOST":return new[]{"KEYWORD_AGILE","KEYWORD_BASTION_THRUSTERS"};
                case "WINGBURST":return new[]{"KEYWORD_AGILE","KEYWORD_BASTION_MICROMISSILES"};
                case "MINES":return new[]{"KEYWORD_AGILE","KEYWORD_BASTION_SPIDERS","KEYWORD_BASTION_MARKS","KEYWORD_BASTION_CHARGED_BURST"};
                case "LANCE":return new[]{"KEYWORD_AGILE","KEYWORD_BASTION_ION_LANCE","KEYWORD_BASTION_MISSILE_BATTERY"};
                case "PROTOCOL":return new[]{"KEYWORD_AGILE","KEYWORD_BASTION_AEGIS"};
                case "SATURATION":return new[]{"KEYWORD_AGILE","KEYWORD_BASTION_SKYFALL"};
                default:return new[]{"KEYWORD_AGILE"};
            }
        }
        internal static string[,] UpgradeCards {get;private set;}=Cards();
        internal static string UpgradeChoice(int branch,int choice)
        {
            if(branch==0)return choice==0?$"III WARHEAD SYSTEM\nHeavy every {B.Int(S.UpgradedHeavyEvery)} shots\nMines {B.Get(S.MineDamage)*(1+System.Math.Max(B.Get(S.MineChainBonus),B.Get(S.UpgradedMineChainBonus))/100):0.#}% per detonation":$"III ION LANCE\n{B.Get(S.UpgradedLaserDamage):0.#}% / {B.Get(S.UpgradedLaserCooldown):0.#}s\nMine blasts {System.Math.Max(B.Get(S.MineBlastRadius),B.Get(S.UpgradedMineBlastRadius)):0.#}m";
            return choice==0?$"III HARDENING\nShield DR +{B.Get(S.HardeningBonus):0.#} points; Aegis {B.Get(S.HardeningDR):0.#}%":$"III RENEWAL\nRefill {B.Get(S.RenewalRestore):0.#}% shield; healing {B.Get(S.RenewalHealing):0.#}%";
        }
        private static string[,] Cards()=>new string[,]
        {
            {$"I MAGAZINE\n{B.Int(S.MissileCapacity)} -> {System.Math.Max(B.Int(S.MissileCapacity),B.Int(S.UpgradedCapacity))} missiles\nWidow field {System.Math.Max(B.Get(S.MineDeployRadius),B.Get(S.UpgradedMineDeployRadius)):0.#}m",$"II ASSEMBLY\nReload {B.Get(S.SiegeReload):0.#} -> {B.Get(S.UpgradedReload):0.#}/s\nWidow Array {System.Math.Max(B.Int(S.MineCount),B.Int(S.UpgradedMineCount))} mines","III SPECIALIZATION\nHeavy cadence + mine damage\nor Ion Lance + wider blasts"},
            {$"I CAPACITORS\nShield +{B.Get(S.ShieldUpgradeOne):0.#}%; barrier {B.Get(S.UpgradedBarrier):0.#}%",$"II RECOVERY\nShield +{B.Get(S.ShieldUpgradeTwo):0.#}% total; healing {B.Get(S.UpgradedHealing):0.#}%","III SPECIALIZATION\nStronger reduction, or shield refill"},
            {$"I SECOND HANGAR\n{System.Math.Max(B.Int(S.BaseSlots),B.Int(S.SecondSlots))} autonomous unit slots","II BLUEPRINTS\nGunner, Interceptor and Repair",$"III THIRD HANGAR\n{System.Math.Max(System.Math.Max(B.Int(S.BaseSlots),B.Int(S.SecondSlots)),B.Int(S.ThirdSlots))} configurable unit slots"},
            {$"I AIRFRAME\nFlight +{B.Get(S.UpgradeFlight):0.#}%; Rush {System.Math.Max(B.Get(S.WingburstDistance),B.Get(S.UpgradedWingburstDistance)):0.#}m",$"II THRUSTER TANK\nFuel {B.Int(S.BoostFuel)} -> {System.Math.Max(B.Int(S.BoostFuel),B.Int(S.UpgradedFuel))}s\nRush {System.Math.Max(B.Int(S.WingburstShots),B.Int(S.UpgradedWingburstShots))} micro-missiles",$"III BLUE SHIFT\n{B.Get(S.UpgradedShiftDuration):0.#}s + ricochets\nRush recharge {System.Math.Min(B.Get(S.WingburstCooldown),B.Get(S.UpgradedWingburstCooldown)):0.#}s"}
        };
        internal static void Refresh()
        {
            UpgradeCards=Cards();
            var tokens=new Dictionary<string,string>();
            tokens.Add("BASTION_WINGBURST_NAME","Heavenfall Rush");
            tokens.Add("BASTION_WINGBURST_DESCRIPTION",$"Agile. Dash <style=cIsUtility>{B.Get(S.WingburstDistance):0.#}m</style> and fire <style=cIsDamage>{B.Int(S.WingburstShots)} homing micro-missiles for {B.Get(S.WingburstDamage):0.#}% damage each</style>.\nTwo charges; <style=cIsUtility>{B.Get(S.WingburstCooldown):0.#}s recharge per charge</style>.");
            tokens.Add("KEYWORD_BASTION_MICROMISSILES",$"<style=cKeywordName>Micro-missiles</style><style=cSub>Spread across visible enemies within {B.Get(S.WingburstRange):0.#}m, prioritizing your attached Widow marks, then nearby enemies not yet covered by the salvo. Each missile explodes in a <style=cIsDamage>{B.Get(S.WingburstRadius):0.#}m radius</style>. You can steer and fire while dashing. Hits detonate attached Widow marks.</style>");
            tokens.Add("BASTION_MINES_NAME","Widow Array");
            tokens.Add("BASTION_MINES_DESCRIPTION",$"Deploy <style=cIsUtility>{B.Int(S.MineCount)} spider mines</style> to attach and mark enemies. Hit marked targets with your missiles or Ion Lance to detonate them for <style=cIsDamage>{B.Get(S.MineDamage)*(1+B.Fraction(S.MineChainBonus)):0.#}% damage</style>.\n<style=cIsUtility>{B.Get(S.MineCooldown):0.#}s recharge</style>.");
            tokens.Add("KEYWORD_BASTION_SPIDERS",$"<style=cKeywordName>Spider mines</style><style=cSub>Deploy in three staggered rings reaching {B.Get(S.MineDeployRadius):0.#}m. Mines detect enemies within {B.Get(S.MineTriggerRange):0.#}m, pursue up to {B.Get(S.MineWalkDistance):0.#}m at {B.Get(S.MineWalkSpeed):0.#}m/s and attach to mark them. Maximum attached: 5 normal / 8 elite / 12 boss. {B.Get(S.MineLifetime):0.#}s lifetime after landing; 48 maximum active.</style>");
            tokens.Add("KEYWORD_BASTION_MARKS","<style=cKeywordName>Widow marks</style><style=cSub>Only your missiles or Ion Lance hitting the marked target can detonate its attached mines. Heavenfall and Skyfall also trigger marks. Mines never detonate autonomously. Mine blasts never trigger other mines.</style>");
            tokens.Add("KEYWORD_BASTION_CHARGED_BURST",$"<style=cKeywordName>Charged burst</style><style=cSub>Each charged burst deals <style=cIsDamage>{B.Get(S.MineDamage)*(1+B.Fraction(S.MineChainBonus)):0.#}% damage in {B.Get(S.MineBlastRadius):0.#}m</style> and applies <style=cIsUtility>50% slow for {B.Get(S.MineSlowDuration):0.#}s</style>.</style>");
            tokens.Add("BASTION_PASSIVE_DESCRIPTION",FactorySummary);
            tokens.Add("KEYWORD_BASTION_FACTORY",
                "<style=cKeywordName>Engineering</style><style=cSub>Press <style=cIsUtility>V (default)</style> to open Engineering. While deployed, spend Fabrication on ranked <style=cIsUtility>Siege, Defense and Mobile</style> paths with prerequisites and unique mastery bonuses. Purchases persist through stages and revival. Engineering pauses single player; multiplayer keeps running.</style>\n\n"+
                $"<style=cKeywordName>Fabrication</style><style=cSub>Produce <style=cIsUtility>{B.Get(S.IdleFabrication):0.#}/min idle</style> or <style=cIsUtility>{B.Get(S.CombatFabrication):0.#}/min in combat</style> while deployed. Kills within {B.Get(S.RewardRange):0.#}m grant bonus currency{(B.Enabled(S.AllyKillsReward)?", including ally kills":"")}. Production follows the run timer. Siege also reloads missiles and deploys drones.</style>\n\n"+
                $"<style=cKeywordName>Seraph Shield</style><style=cSub>Independent shield: <style=cIsHealing>{B.Get(S.MobileShield):0.#}% max health mobile / {B.Get(S.SiegeShield):0.#}% siege</style>. Damage reduction: {B.Get(S.MobileShieldDR):0.#}% mobile / {B.Get(S.SiegeShieldDR):0.#}% siege. Recharges after {B.Get(S.MobileShieldDelay):0.#}s mobile / {B.Get(S.SiegeShieldDelay):0.#}s siege without shield or health damage. Barrier protects recharge.</style>");
            tokens.Add("BASTION_NEEDLE_DESCRIPTION",$"Agile. Aim-assisted rounds: <style=cIsDamage>{B.Get(S.NeedleRate):0.#}/s for {B.Get(S.NeedleDamage):0.#}% damage</style> each.\nEach damaging hit restores <style=cIsUtility>{B.Get(S.NeedleAmmo):0.##} missile stock</style>.");
            tokens.Add("KEYWORD_BASTION_NEEDLE_ASSIST",$"<style=cKeywordName>Aim assist</style><style=cSub>A blue reticle marks the visible enemy closest to your crosshair within a narrow {BastionNeedleRules.ConeDegrees:0.#} degree cone and {B.Get(S.NeedleRange):0.#}m range. Shots adjust toward that target. Outside the cone, shots follow your crosshair normally. Cannot lock through terrain.</style>");
            tokens.Add("BASTION_BOOST_DESCRIPTION",$"Hold secondary to boost at <style=cIsUtility>+{B.Get(S.BoostSpeed):0.#}% movement speed</style> while steering and firing.\n{B.Int(S.BoostFuel)}s fuel; release to recharge.");
            tokens.Add("KEYWORD_BASTION_THRUSTERS",$"<style=cKeywordName>Thruster reserve</style><style=cSub>The fuel reserve supports {B.Int(S.BoostFuel)}s of continuous boosting. Refills in {B.Get(S.BoostRefill):0.##}s after {B.Get(S.BoostRecovery):0.##}s without boosting. Release when empty. Blue Shift increases fuel recovery.</style>");
            tokens.Add("BASTION_DEPLOY_DESCRIPTION",$"Transform into an <style=cIsUtility>immobile fortress</style> with free aiming.\n\nRequires solid ground within {B.Get(S.DeployHeight):0.#}m. Deploys in {B.Get(S.DeployDuration):0.##}s; <style=cIsUtility>{B.Get(S.SwitchCooldown):0.#}s mode-switch cooldown</style>.");
            tokens.Add("BASTION_PACK_DESCRIPTION",$"Return to mobile form in <style=cIsUtility>{B.Get(S.PackDuration):0.##}s</style>.{(B.Enabled(S.PackRefillsShield)?" Restore your Seraph Shield.":"")}\n\n<style=cIsUtility>{B.Get(S.SwitchCooldown):0.#}s mode-switch cooldown</style>.");
            tokens.Add("BASTION_REDLINE_DESCRIPTION",$"For <style=cIsUtility>{B.Get(S.ShiftDuration):0.#}s</style>, gain <style=cIsDamage>{B.Get(S.ShiftAttackSpeed):0.#}% attack speed</style>, <style=cIsUtility>{B.Get(S.ShiftMoveSpeed):0.#}% flight speed</style> and {B.Get(S.ShiftRefillMultiplier):0.#}x fuel recharge.\n\nRestore {B.Get(S.ShiftRestore):0.#}% fuel. <style=cIsUtility>{B.Get(S.ShiftCooldown):0.#}s cooldown</style>.");
            tokens.Add("BASTION_LANCE_DESCRIPTION",$"<style=cIsUtility>Tap:</style> Auto-target a <style=cIsDamage>{B.Get(S.LaserDamage):0.#}% piercing beam</style>. {B.Get(S.LaserCooldown):0.#}s recharge.\n<style=cIsUtility>Hold:</style> <style=cIsDamage>{B.Get(S.MissileDamage):0.#}% homing missiles</style>; a <style=cIsDamage>{B.Get(S.HeavyDamage):0.#}% heavy</style> every {B.Int(S.HeavyEvery)} shots.");
            tokens.Add("KEYWORD_BASTION_ION_LANCE",$"<style=cKeywordName>Ion Lance</style><style=cSub>Locks onto a visible enemy within {B.Get(S.LaserRange):0.#}m. The piercing beam causes an <style=cIsDamage>{B.Get(S.LaserRadius):0.#}m impact blast</style>. A target is not hit twice by the beam and impact. No target preserves your shot.</style>");
            tokens.Add("KEYWORD_BASTION_MISSILE_BATTERY",$"<style=cKeywordName>Missile Battery</style><style=cSub>Aim at or near an enemy, within {BastionNeedleRules.BatteryConeDegrees:0.#} degrees of your crosshair, to focus it. Otherwise missiles attack the nearest reachable enemy within {B.Get(S.MissileRange):0.#}m. Fire {B.Get(S.MissileRate):0.#} homing missiles/s. Normal missiles deal <style=cIsDamage>{B.Get(S.MissileDamage):0.#}% damage in {B.Get(S.MissileRadius):0.#}m</style>; every {B.Int(S.HeavyEvery)} shots fires a heavy dealing <style=cIsDamage>{B.Get(S.HeavyDamage):0.#}% damage in {B.Get(S.HeavyRadius):0.#}m</style>. Base stockpile: {B.Int(S.MissileCapacity)}. Reloads {B.Get(S.SiegeReload):0.#}/s in siege mode. Needlefire hits also replenish ammunition.</style>");
            tokens.Add("BASTION_PROTOCOL_DESCRIPTION",$"Gain <style=cIsHealing>{B.Get(S.AegisBarrier):0.#}% max health as barrier</style> and fortify for <style=cIsUtility>{B.Get(S.AegisDuration):0.#}s</style> with <style=cIsHealing>{B.Get(S.AegisDR):0.#}% damage reduction</style>, healing and faster missile reload.\n<style=cIsUtility>{B.Get(S.AegisCooldown):0.#}s cooldown</style>.");
            tokens.Add("KEYWORD_BASTION_AEGIS",$"<style=cKeywordName>Aegis Protocol</style><style=cSub>Heal {B.Get(S.AegisHealing):0.#}% max health over {B.Get(S.AegisDuration):0.#}s total and gain {B.Get(S.AegisReload):0.#}x missile reload.{(B.Get(S.AegisGuardian)>0?$" Deploy a {B.Get(S.AegisGuardian):0.#}s Bulwark.":"")} Stacks with Seraph Shield. The separate Aegis Barrier sits above your shield, protecting its recharge.</style>");
            tokens.Add("BASTION_SATURATION_DESCRIPTION",$"Launch <style=cIsDamage>{B.Int(S.SkyfallCount)} sky-diving warheads</style>, each dealing <style=cIsDamage>{B.Get(S.SkyfallDamage):0.#}% damage in {B.Get(S.SkyfallRadius):0.#}m</style>.\nAuto-targets nearby threats and detonates attached Widow marks. <style=cIsUtility>{B.Get(S.SkyfallCooldown):0.#}s cooldown</style>.");
            tokens.Add("KEYWORD_BASTION_SKYFALL",$"<style=cKeywordName>Skyfall Barrage</style><style=cSub>Automatically targets enemies within {B.Get(S.SkyfallRange):0.#}m, prioritizing clusters, then bosses and elites. Warheads spread coverage when useful and can overlap when fewer targets remain. Detonates attached Widow Array mines. Uses no stockpile ammo.</style>");
            // LanguageAPI.Add is first-write-wins. Keep one owned overlay so
            // menu defaults and later host profiles can replace our descriptions.
            var replacement=LanguageAPI.AddOverlay(tokens);
            balanceOverlay?.Remove();
            balanceOverlay=replacement;
        }
    }
}
