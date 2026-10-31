using B = IvoryBastion.BastionBalance;
using S = IvoryBastion.BastionSetting;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using R2API;
using R2API.Networking;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
namespace IvoryBastion
{
    [BepInPlugin(Guid,"Ivory Bastion",Version)]
    [BepInDependency("com.bepis.r2api.content_management")]
    [BepInDependency("com.bepis.r2api.prefab")]
    [BepInDependency("com.bepis.r2api.language")]
    [BepInDependency("com.bepis.r2api.recalculatestats","1.7.0")]
    [BepInDependency("com.bepis.r2api.networking")]
    [BepInDependency(BastionOptions.PluginGuid,BepInDependency.DependencyFlags.SoftDependency)]
    [R2API.Utils.NetworkCompatibility(R2API.Utils.CompatibilityLevel.EveryoneMustHaveMod,R2API.Utils.VersionStrictness.EveryoneNeedSameModVersion)]
    public sealed class BastionPlugin:BaseUnityPlugin
    {
        public const string Guid="com.yuiki.ivorybastion",Version="1.5.0";
        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> EnableAudio;
        internal static ConfigEntry<float> AudioVolume;
        internal static ConfigEntry<KeyboardShortcut> EngineeringKey,TransformationKey;
        internal static ConfigFile Settings;
        private void Awake()
        {
            Log=Logger;EnableAudio=Config.Bind("Audio","EnableAudio",true,"Enable all Ivory Bastion sound effects and voice lines on this computer, including other players' cues. Changes apply immediately without a restart and do not change anyone else's audio.");
            AudioVolume=Config.Bind("Audio","Volume",.65f,new ConfigDescription("Local audio volume.",new AcceptableValueRange<float>(0,1)));
            EngineeringKey=Config.Bind("Controls","Engineering",new KeyboardShortcut(KeyCode.V),"Open the run upgrade tree. Shown on the HUD.");
            TransformationKey=Config.Bind("Controls","Transformation",new KeyboardShortcut(KeyCode.C),"Deploy or pack independently of the utility skill. The binding and mode-switch cooldown are shown on the HUD.");
            Settings=Config;BastionBalanceConfig.Bind(Config);BastionPresentationSettings.Bind(Config);
            EnableAudio.SettingChanged+=(_,__)=>BastionAudio.SetEnabled(EnableAudio.Value);
            BastionAudio.SetEnabled(EnableAudio.Value);
            NetworkingAPI.RegisterMessageType<BastionStatus>();NetworkingAPI.RegisterMessageType<BastionEffect>();
            NetworkingAPI.RegisterMessageType<BastionUnitStats>();
            NetworkingAPI.RegisterMessageType<BastionUtilityStatus>();
            NetworkingAPI.RegisterMessageType<BastionUpgradeStatus>();
            BastionContent.Create();
            BastionBalanceRuntime.Install();
            BastionOptions.Install();
            BastionItemBridge.Install();
            BastionUtilityItemBridge.Install();
            BastionVisualSizing.Install();
            BastionAbilityShield.Install();
            BastionShieldOverlay.Install();
            BastionShieldStatus.Install();
            BastionCameraFade.Install();
            On.RoR2.HealthComponent.TakeDamage+=Damage;
            RecalculateStatsAPI.GetStatCoefficients+=Stats;
            GlobalEventManager.onCharacterDeathServer+=Killed;
            GlobalEventManager.onServerDamageDealt+=Dealt;
            RoR2.UI.HUD.onHudTargetChangedGlobal+=BastionHud.Attach;
            foreach(var h in RoR2.UI.HUD.readOnlyInstanceList)BastionHud.Attach(h);
            Logger.LogInfo("Ivory Bastion "+Version+": independent mobile/siege kits and server-owned production.");
        }
        private static void Damage(On.RoR2.HealthComponent.orig_TakeDamage orig,HealthComponent hp,DamageInfo info)
            =>BastionDamageHook.Apply(orig,hp,info);
        private static void Stats(CharacterBody b,RecalculateStatsAPI.StatHookEventArgs a)
        {
            var c=b.GetComponent<BastionController>();if(!c)return;
            if(c.Sieged)a.moveSpeedRootCount++;
            else {a.moveSpeedMultAdd+=c.Bank.FlightMultiplier-1;if(c.Boosting)a.moveSpeedMultAdd+=B.Fraction(S.BoostSpeed);if(c.Bank.RedlineTime>0){a.attackSpeedMultAdd+=B.Fraction(S.ShiftAttackSpeed);a.moveSpeedMultAdd+=B.Fraction(S.ShiftMoveSpeed);}}
        }
        private static void Killed(DamageReport r)
        {if(!NetworkServer.active||!r.victimBody)return;foreach(var c in BastionController.Instances)if(c&&c.Sieged&&c.Alive&&BastionCombat.OpposingTeams(c.Body,r.victimBody)&&Vector3.Distance(c.Body.corePosition,r.victimBody.corePosition)<B.Get(S.RewardRange)&&(B.Enabled(S.AllyKillsReward)||r.attackerBody==c.Body||(r.attackerBody&&r.attackerBody.GetComponent<BastionUnit>()?.Owner==c)))c.Bank.AwardKillFabrication(c.Sieged,BastionController.RunTimerAdvancing,r.victimIsBoss,r.victimIsElite);}
        private static void Dealt(DamageReport r)
        {
            if(!r.attackerBody)return;var c=r.attackerBody.GetComponent<BastionController>();
            if(!c){var u=r.attackerBody.GetComponent<BastionUnit>();if(u)c=u.Owner;}
            if(c)c.LastCombat=Time.time;
        }
        private void Update(){BastionAudio.SetPaused(Time.timeScale==0);BastionBalanceRuntime.Update();}
        private void OnDestroy()
        {
            BastionBalanceRuntime.Remove();
            BastionItemBridge.Remove();
            BastionUtilityItemBridge.Remove();
            BastionVisualSizing.Remove();
            BastionAbilityShield.Remove();
            BastionShieldOverlay.Remove();
            BastionShieldStatus.Remove();
            BastionCameraFade.Remove();
            On.RoR2.HealthComponent.TakeDamage-=Damage;RecalculateStatsAPI.GetStatCoefficients-=Stats;
            GlobalEventManager.onCharacterDeathServer-=Killed;GlobalEventManager.onServerDamageDealt-=Dealt;
            RoR2.UI.HUD.onHudTargetChangedGlobal-=BastionHud.Attach;BastionAudio.Shutdown();
        }
    }
}
