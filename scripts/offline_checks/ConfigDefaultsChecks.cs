using System;
using System.Globalization;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using IvoryBastion;
using UnityEngine;

internal static class ConfigDefaultsChecks
{
    private static int count;
    private static void Check(bool valid,string why){count++;if(!valid)throw new Exception("Settings reset: "+why);}
    private static void Near(float actual,float expected,string why)=>Check(Math.Abs(actual-expected)<.001f,why);

    internal static void Run(string root)
    {
        try
        {
            string folder=Path.Combine(root,".build","config_legacy");
            string path=Path.Combine(folder,"Reset103.cfg");
            File.WriteAllText(path,"");
            var config=new ConfigFile(path,false);
            var audio=config.Bind("Audio","EnableAudio",true,"");
            var volume=config.Bind("Audio","Volume",.65f,"");
            var key=config.Bind("Controls","Engineering",new KeyboardShortcut(KeyCode.V),"");
            var hud=config.Bind("Presentation","CombatHudScale",1f,"");
            BastionBalanceConfig.Bind(config);
            audio.Value=false;volume.Value=.12f;key.Value=new KeyboardShortcut(KeyCode.K,KeyCode.LeftControl);hud.Value=1.2f;
            var rate=(ConfigEntry<float>)config.Single(p=>p.Key.Key==BastionSetting.NeedleRate.ToString()).Value;
            var speed=(ConfigEntry<float>)config.Single(p=>p.Key.Key==BastionSetting.MineWalkSpeed.ToString()).Value;
            rate.Value=9;speed.Value=8;
            int audioEvents=0;audio.SettingChanged+=(sender,args)=>audioEvents++;
            var active=BastionBalance.Defaults();active[(int)BastionSetting.NeedleRate]=13;BastionBalance.Apply(active);
            BastionConfigDefaults.Reset(config);
            Check(config.SaveOnConfigSet,"reset restores the original autosave policy");
            foreach(var pair in config)Check(Equals(pair.Value.BoxedValue,pair.Value.DefaultValue),"all option types reset: "+pair.Key);
            Check(audioEvents==1,"reset invokes local setting-change handlers exactly once");
            Check(key.Value.MainKey==KeyCode.V&&!key.Value.Modifiers.Any(),"reset removes custom key modifiers");
            Near(speed.Value,6,"reset uses the new six-metre mine-speed default");
            Near(BastionBalance.Get(BastionSetting.NeedleRate),13,"reset does not overwrite an active host profile mid-run");
            Near(BastionBalanceConfig.Capture()[(int)BastionSetting.NeedleRate],6,"the next run captures the reset balance");
            var saved=new ConfigFile(path,false);
            Check(saved.Bind("Audio","EnableAudio",false,"").Value,"reset audio is persisted");
            Check(saved.Bind("Controls","Engineering",new KeyboardShortcut(KeyCode.K),"").Value.MainKey==KeyCode.V,"reset control is persisted");
            BastionBalanceConfig.Bind(saved);
            Near(BastionBalanceConfig.Capture()[(int)BastionSetting.MineWalkSpeed],6,"reset balance survives reload");
            config.SaveOnConfigSet=false;volume.Value=.2f;BastionConfigDefaults.Reset(config);
            Check(!config.SaveOnConfigSet,"reset preserves a caller's disabled autosave policy");
            Near(new ConfigFile(path,false).Bind("Audio","Volume",0f,"").Value,.65f,"reset saves once even if autosave was disabled");
            BastionConfigDefaults.Reset(null);

            foreach(float previous in new[]{5f,7.5f})
            {
                string fixture=Path.Combine(folder,previous==5?"MineSpeed103Default.cfg":"MineSpeed103Custom.cfg");
                string marker=fixture+".defaults-103-mine-speed";
                if(File.Exists(marker))File.Delete(marker);
                File.WriteAllText(fixture,"[Balance - Widow Array]\nMineWalkSpeed = "+previous.ToString(CultureInfo.InvariantCulture)+"\n");
                var file=new ConfigFile(fixture,false);BastionBalanceConfig.Bind(file);
                var entry=(ConfigEntry<float>)file.Single(p=>p.Key.Key==BastionSetting.MineWalkSpeed.ToString()).Value;
                Near(entry.Value,previous==5?6:previous,"only the previous default migrates automatically");
                Check(File.Exists(marker),"mine-speed migration records completion");
                entry.Value=5;file.Save();
                BastionBalanceConfig.Bind(new ConfigFile(fixture,false));
                Near(BastionBalanceConfig.Capture()[(int)BastionSetting.MineWalkSpeed],5,"later deliberate five-metre settings survive reload");
            }
            KitMigration(root);
            KitMigration(root,true);
            FabricationMigration(root);
            MineRangeMigration(root);
            Console.WriteLine("PASS: "+count+" real BepInEx reset, persistence, local-event, next-run balance and default-migration checks.");
        }
        finally{BastionBalance.Reset();}
    }
    private static void FabricationMigration(string root)
    {
        var keys=new[]{BastionSetting.IdleFabrication,BastionSetting.CombatFabrication};
        int caseIndex=0;
        foreach(var saved in new[]{new[]{14,35},new[]{9,20},new[]{0,0},new[]{14,60},new[]{50,35}})
        {
            string path=Path.Combine(root,".build","config_legacy","Fabrication123-"+(caseIndex++)+".cfg");
            string marker=path+".defaults-123-fabrication";
            if(File.Exists(marker))File.Delete(marker);
            File.WriteAllText(path,"[Balance - Fabrication]\nIdleFabrication = "+saved[0]+"\nCombatFabrication = "+saved[1]+"\n");
            var active=BastionBalance.Defaults();active[(int)keys[0]]=111;active[(int)keys[1]]=222;BastionBalance.Apply(active);
            var config=new ConfigFile(path,false);BastionBalanceConfig.Bind(config);
            Check(File.Exists(marker),"Fabrication migration records completion");
            var captured=BastionBalanceConfig.Capture();
            for(int i=0;i<keys.Length;i++)
            {
                Near(captured[(int)keys[i]],saved[i]==(i==0?14:35)?(i==0?17:42):saved[i],"migrate former integer default, preserving custom/disabled production");
                Near(BastionBalance.Get(keys[i]),i==0?111:222,"migration leaves active run balance unchanged");
                var entry=(ConfigEntry<int>)config.Single(p=>p.Key.Key==keys[i].ToString()).Value;
                entry.Value=i==0?14:35;
            }
            config.Save();config=new ConfigFile(path,false);BastionBalanceConfig.Bind(config);captured=BastionBalanceConfig.Capture();
            Near(captured[(int)keys[0]],14,"later deliberate old idle rate survives reload");
            Near(captured[(int)keys[1]],35,"later deliberate old combat rate survives reload");
            BastionConfigDefaults.Reset(config);
            captured=BastionBalanceConfig.Capture();
            Near(captured[(int)keys[0]],17,"Reset All restores new idle default");
            Near(captured[(int)keys[1]],42,"Reset All restores new combat default");
        }
    }
    private static void MineRangeMigration(string root)
    {
        var keys=new[]{BastionSetting.MineTriggerRange,BastionSetting.MineWalkDistance};
        int caseIndex=0;
        foreach(var saved in new[]{new[]{16,20},new[]{18,24},new[]{1,2},new[]{16,24},new[]{18,20},new[]{30,50}})
        {
            string path=Path.Combine(root,".build","config_legacy","MineRange130-"+(caseIndex++)+".cfg");
            string marker=path+".defaults-130-mine-range";
            if(File.Exists(marker))File.Delete(marker);
            File.WriteAllText(path,"[Balance - Widow Array]\nMineTriggerRange = "+saved[0]+"\nMineWalkDistance = "+saved[1]+"\nMineWalkSpeed = 7\nMineLifetime = 45\n");
            File.WriteAllText(path+".defaults-100-mine-marks","older mark migration already completed");
            var active=BastionBalance.Defaults();active[(int)keys[0]]=7;active[(int)keys[1]]=9;BastionBalance.Apply(active);
            var config=new ConfigFile(path,false);BastionBalanceConfig.Bind(config);var captured=BastionBalanceConfig.Capture();
            Check(File.Exists(marker),"new mine-range migration runs even after older mark migration");
            Check(captured.Length==BastionBalance.Specs.Length,"range increase preserves the balance wire profile length");
            for(int i=0;i<keys.Length;i++)
            {
                Near(captured[(int)keys[i]],saved[i]==(i==0?16:20)?(i==0?30:50):saved[i],"only the previous mine range default migrates");
                Near(BastionBalance.Get(keys[i]),i==0?7:9,"mine range migration does not change active run balance");
                ((ConfigEntry<float>)config.Single(p=>p.Key.Key==keys[i].ToString()).Value).Value=i==0?16:20;
            }
            Near(captured[(int)BastionSetting.MineWalkSpeed],7,"range migration preserves custom walking speed");
            Near(captured[(int)BastionSetting.MineLifetime],45,"range migration preserves custom lifetime");
            config.Save();BastionBalanceConfig.Bind(new ConfigFile(path,false));captured=BastionBalanceConfig.Capture();
            Near(captured[(int)keys[0]],16,"later deliberate old detection survives reload");
            Near(captured[(int)keys[1]],20,"later deliberate old pursuit survives reload");
            config=new ConfigFile(path,false);BastionBalanceConfig.Bind(config);BastionConfigDefaults.Reset(config);captured=BastionBalanceConfig.Capture();
            Near(captured[(int)keys[0]],30,"Reset All restores 30m detection without bound clamping");
            Near(captured[(int)keys[1]],50,"Reset All restores 50m pursuit without bound clamping");
        }
    }
    private static void KitMigration(string root,bool shield=false)
    {
        var keys=shield?new[]{BastionSetting.MobileShieldDR,BastionSetting.SiegeShieldDR,BastionSetting.HardeningBonus}:new[]{BastionSetting.BaseDamage,BastionSetting.DamageGrowth,BastionSetting.GunnerDamage,
            BastionSetting.NeedleProc,BastionSetting.MissileProc,BastionSetting.HeavyProc,BastionSetting.LaserProc,
            BastionSetting.SkyfallProc,BastionSetting.WingburstProc,BastionSetting.MineProc,BastionSetting.DroneProc};
        var old=shield?new[]{50f,50f,5f}:new[]{12f,2.4f,70f,.6f,.6f,1f,1f,1f,.15f,.5f,.5f};
        foreach(bool custom in new[]{false,true})
        {
            string path=Path.Combine(root,".build","config_legacy",(shield?"Shield122":"Kit122")+(custom?"Custom.cfg":"Defaults.cfg"));
            string marker=path+(shield?".defaults-122-shield-balance":".defaults-122-kit-balance");
            if(shield)File.WriteAllText(path+".defaults-122-kit-balance","previous review kit migration already completed");
            if(File.Exists(marker))File.Delete(marker);
            string text="";
            for(int i=0;i<keys.Length;i++)
            {
                var spec=BastionBalance.Specs[(int)keys[i]];
                text+="["+spec.Category+"]\n"+keys[i]+" = "+(old[i]+(custom?.07f:0)).ToString(CultureInfo.InvariantCulture)+"\n";
            }
            File.WriteAllText(path,text);
            var active=BastionBalance.Defaults();active[(int)BastionSetting.BaseDamage]=19;BastionBalance.Apply(active);
            var config=new ConfigFile(path,false);BastionBalanceConfig.Bind(config);
            Check(File.Exists(marker),"kit migration records one-time completion");
            var captured=BastionBalanceConfig.Capture();
            for(int i=0;i<keys.Length;i++)
            {
                var spec=BastionBalance.Specs[(int)keys[i]];
                Near(captured[(int)keys[i]],custom?old[i]+.07f:spec.Default,"migrate exact former default, preserve custom: "+keys[i]);
                var entry=(ConfigEntry<float>)config.Single(p=>p.Key.Key==keys[i].ToString()).Value;
                entry.Value=old[i];
            }
            Near(BastionBalance.Get(BastionSetting.BaseDamage),19,"loading next-run settings never changes active host balance");
            config.Save();BastionBalanceConfig.Bind(new ConfigFile(path,false));captured=BastionBalanceConfig.Capture();
            for(int i=0;i<keys.Length;i++)Near(captured[(int)keys[i]],old[i],"later deliberate old default survives reload: "+keys[i]);
        }
    }
}
