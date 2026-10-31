using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using BepInEx.Bootstrap;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;
using UnityEngine;

namespace IvoryBastion
{
    internal static class BastionOptions
    {
        internal const string PluginGuid="com.rune580.riskofoptions";

        internal static void Install()
        {
            if(!Application.isBatchMode&&Chainloader.PluginInfos.ContainsKey(PluginGuid))
                Register();
        }

        // Keep optional API references out of the caller's JIT path so manual
        // installs without Risk of Options can still use the existing config.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Register()
        {
            const string name="Ivory Bastion";
            ModSettingsManager.SetModDescription(
                "Audio, HUD and controls apply immediately. Balance changes apply when the host starts the next run. Multiplayer clients automatically use the host's balance; their own saved settings are preserved.",
                BastionPlugin.Guid,name);
            ModSettingsManager.SetModIcon(BastionContent.Portrait,BastionPlugin.Guid,name);
            ModSettingsManager.AddOption(new GenericButtonOption("Reset All to Defaults","Reset Settings",
                "Restore every Ivory Bastion setting, including audio, HUD, controls and host balance. A confirmation is required. Audio, HUD and controls apply immediately; balance applies next run.",
                "Reset All to Defaults",ConfirmReset),BastionPlugin.Guid,name);
            foreach(var pair in BastionPlugin.Settings)
            {
                var entry=pair.Value;var spec=BastionBalanceConfig.Find(entry);
                string label=spec?.Name??(entry==BastionPlugin.EnableAudio?"Enable Mod Audio":entry==BastionPlugin.AudioVolume?"Mod Volume":entry==BastionPlugin.EngineeringKey?"Engineering Key":System.Text.RegularExpressions.Regex.Replace(entry.Definition.Key,"([a-z])([A-Z])","$1 $2"));
                string category=entry.Definition.Section,description=entry.Description.Description;
                if(entry is ConfigEntry<bool> flag)
                    ModSettingsManager.AddOption(new CheckBoxOption(flag,new CheckBoxConfig{name=label,category=category,description=description,restartRequired=false}),BastionPlugin.Guid,name);
                else if(entry is ConfigEntry<int> integer)
                {
                    var range=entry.Description.AcceptableValues as AcceptableValueRange<int>;
                    ModSettingsManager.AddOption(new IntSliderOption(integer,new IntSliderConfig{name=label,category=category,description=description,min=range?.MinValue??0,max=range?.MaxValue??100,formatString="{0}",restartRequired=false}),BastionPlugin.Guid,name);
                }
                else if(entry is ConfigEntry<float> number)
                {
                    var range=entry.Description.AcceptableValues as AcceptableValueRange<float>;
                    bool percentage=entry==BastionPlugin.AudioVolume||entry==BastionPresentationSettings.DialogueVolume||entry==BastionPresentationSettings.EffectsVolume||entry==BastionPresentationSettings.AlertVolume||entry==BastionPresentationSettings.HudScale;
                    ModSettingsManager.AddOption(new StepSliderOption(number,new StepSliderConfig{name=label,category=category,description=description,min=range?.MinValue??0,max=range?.MaxValue??1,increment=spec?.Step??.01f,FormatString=percentage?"{0:0%}":"{0:0.##}",restartRequired=false}),BastionPlugin.Guid,name);
                }
                else if(entry is ConfigEntry<KeyboardShortcut> key)
                    ModSettingsManager.AddOption(new KeyBindOption(key,new KeyBindConfig{name=label,category=category,description=description,restartRequired=false}),BastionPlugin.Guid,name);
                else BastionPlugin.Log.LogWarning("No Risk of Options control for "+entry.Definition);
            }
        }
        private static void ConfirmReset()
        {
            var dialog=RoR2.UI.SimpleDialogBox.Create();
            dialog.headerToken=new RoR2.UI.SimpleDialogBox.TokenParamsPair("Reset Ivory Bastion settings?");
            dialog.descriptionToken=new RoR2.UI.SimpleDialogBox.TokenParamsPair("Restore all Ivory Bastion settings to their current defaults, including controls, audio, HUD and host balance? Audio, HUD and controls apply immediately. Balance applies when the next run starts.");
            dialog.AddCancelButton("DIALOG_OPTION_CANCEL");
            dialog.AddActionButton(()=>BastionConfigDefaults.Reset(BastionPlugin.Settings),"Reset to defaults",true);
        }
    }
}
