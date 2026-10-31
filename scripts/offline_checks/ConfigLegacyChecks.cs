using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using IvoryBastion;
using UnityEngine;

static class ConfigLegacyChecks
{
    static int count;
    static void Check(bool ok,string why){count++;if(!ok)throw new Exception("Config persistence: "+why);}
    static void Near(float a,float b,string why){Check(Math.Abs(a-b)<.0002f,why);}
    static int Main(string[] args)
    {
        try
        {
            var root=Path.GetFullPath(args[0]);
            typeof(Paths).GetProperty("BepInExConfigPath",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)
                .SetValue(null,Path.Combine(root,".build","config_legacy","BepInEx.cfg"),null);
Run(root);HotkeyLegacyChecks.Run();ConfigDefaultsChecks.Run(root);return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    static void Run(string root)
    {
        var path=Path.Combine(root,".build","config_legacy","IvoryBastion.cfg");
        File.WriteAllText(path,"[Audio]\nEnableAudio = false\nVolume = 0.4\n[Controls]\nEngineering = K\n");
        var config=new ConfigFile(path,false);
        var enabled=config.Bind("Audio","EnableAudio",true,"");
        var volume=config.Bind("Audio","Volume",.65f,new ConfigDescription("",new AcceptableValueRange<float>(0,1)));
        var key=config.Bind("Controls","Engineering",new KeyboardShortcut(KeyCode.V),"");
        BastionBalanceConfig.Bind(config);
        Check(config.Count==BastionBalance.Specs.Count(s=>!s.Retired)+3,"all 140 options are enumerable");
        Check(!enabled.Value&&Math.Abs(volume.Value-.4f)<.001f&&key.Value.MainKey==KeyCode.K,"existing local preferences survive binding");
        foreach(var pair in config)
        {
            var entry=pair.Value;var spec=BastionBalanceConfig.Find(entry);
            Check(entry.SettingType==typeof(bool)||entry.SettingType==typeof(int)||entry.SettingType==typeof(float)||entry.SettingType==typeof(KeyboardShortcut),"all settings have menu control types");
            if(spec!=null)Check(entry.Description.Description.StartsWith(BastionBalanceConfig.Scope),"every balance setting documents host and next-run scope");
        }
        var active=BastionBalance.Defaults();active[(int)BastionSetting.NeedleRate]=10;BastionBalance.Apply(active);
        Near(BastionBalanceConfig.Capture()[(int)BastionSetting.NeedleDamage],80,"capture reads saved settings rather than active host values");
        var rate=(ConfigEntry<float>)config.Single(p=>p.Key.Key==BastionSetting.NeedleRate.ToString()).Value;rate.Value=9;
        Near(BastionBalance.Get(BastionSetting.NeedleRate),10,"saved edits cannot change an active profile");
        Near(BastionBalanceConfig.Capture()[(int)BastionSetting.NeedleRate],9,"next-run capture sees edited values");
        rate.Value=1000;Near(rate.Value,30,"BepInEx enforces slider bounds");rate.Value=9;
        config.Save();var reloaded=new ConfigFile(path,false);BastionBalanceConfig.Bind(reloaded);
        Near(BastionBalanceConfig.Capture()[(int)BastionSetting.NeedleRate],9,"balance settings persist across reload");
        var migrationPath=Path.Combine(root,".build","config_legacy","Migration100.cfg");
        if(File.Exists(migrationPath+".defaults-100"))File.Delete(migrationPath+".defaults-100");
        File.WriteAllText(migrationPath,"");var migration=new ConfigFile(migrationPath,false);
        var oldKeys=new[]{BastionSetting.NeedleDamage,BastionSetting.MissileDamage,BastionSetting.HeavyDamage,BastionSetting.LaserDamage,BastionSetting.UpgradedLaserDamage,BastionSetting.SkyfallDamage};
        var oldValues=new[]{90f,130f,451f,900f,1200f,800f};
        for(int i=0;i<oldKeys.Length;i++){var s=BastionBalance.Specs[(int)oldKeys[i]];migration.Bind(s.Category,s.Key.ToString(),oldValues[i],"");}
        BastionBalanceConfig.Bind(migration);var migrated=BastionBalanceConfig.Capture();
        for(int i=0;i<oldKeys.Length;i++)Near(migrated[(int)oldKeys[i]],i==2?451:BastionBalance.Specs[(int)oldKeys[i]].Default,"1.0 default migration preserves custom values");
        var missile=(ConfigEntry<float>)migration.Single(p=>p.Key.Key==BastionSetting.MissileDamage.ToString()).Value;missile.Value=130;migration.Save();
        BastionBalanceConfig.Bind(new ConfigFile(migrationPath,false));Near(BastionBalanceConfig.Capture()[(int)BastionSetting.MissileDamage],130,"migration never rewrites later user choices");
        // A saved pre-rush review profile must update defaults once, even if
        // its earlier damage migration has already run. Deliberate edits survive.
        var rushPath=Path.Combine(root,".build","config_legacy","Rush100.cfg");
        foreach(string suffix in new[]{".defaults-100",".defaults-100-rush",".defaults-100-rush-impact",".defaults-100-rush-assault",".defaults-100-rush-skirmish",".defaults-100-mine-marks"})if(File.Exists(rushPath+suffix))File.Delete(rushPath+suffix);
        File.WriteAllText(rushPath,"");File.WriteAllText(rushPath+".defaults-100","earlier migration");
        var rush=new ConfigFile(rushPath,false);var transform=rush.Bind("Controls","Transformation",new KeyboardShortcut(KeyCode.F),"");
        var rushKeys=new[]{BastionSetting.MissileCapacity,BastionSetting.UpgradedCapacity,BastionSetting.WingburstCooldown,BastionSetting.WingburstDistance,BastionSetting.WingburstShots,BastionSetting.WingburstDuration,BastionSetting.WingburstWindup};
        var rushOld=new[]{40f,60,6,12,16,.38f,.08f};
        for(int i=0;i<rushKeys.Length;i++){var spec=BastionBalance.Specs[(int)rushKeys[i]];if(spec.Kind==BastionBalanceKind.Int)rush.Bind(spec.Category,spec.Key.ToString(),(int)rushOld[i],"");else rush.Bind(spec.Category,spec.Key.ToString(),rushOld[i],"");}
        BastionBalanceConfig.Bind(rush);var next=BastionBalanceConfig.Capture();
        for(int i=0;i<rushKeys.Length;i++)Near(next[(int)rushKeys[i]],BastionBalance.Specs[(int)rushKeys[i]].Default,"rush default migrated once");
        Check(transform.Value.MainKey==KeyCode.C,"saved F moves to C");
        transform.Value=new KeyboardShortcut(KeyCode.F);((ConfigEntry<int>)rush.Single(p=>p.Key.Key=="MissileCapacity").Value).Value=40;rush.Save();
        var again=new ConfigFile(rushPath,false);var customKey=again.Bind("Controls","Transformation",new KeyboardShortcut(KeyCode.C),"");BastionBalanceConfig.Bind(again);
        Check(customKey.Value.MainKey==KeyCode.F,"subsequent deliberate F choice preserved");Near(BastionBalanceConfig.Capture()[(int)BastionSetting.MissileCapacity],40,"subsequent custom capacity preserved");
        File.Delete(rushPath+".defaults-100-rush");customKey.Value=new KeyboardShortcut(KeyCode.F,KeyCode.LeftControl);((ConfigEntry<float>)again.Single(p=>p.Key.Key=="WingburstCooldown").Value).Value=14;again.Save();BastionBalanceConfig.Bind(again);
        Check(customKey.Value.MainKey==KeyCode.F&&customKey.Value.Modifiers.Contains(KeyCode.LeftControl),"modified/custom binding preserved");Near(BastionBalanceConfig.Capture()[(int)BastionSetting.WingburstCooldown],14,"custom rush cooldown preserved");
        var aliasPath=Path.Combine(root,".build","config_legacy","RushAlias100.cfg");
        foreach(string suffix in new[]{".defaults-100",".defaults-100-rush",".defaults-100-rush-impact",".defaults-100-rush-assault",".defaults-100-rush-skirmish",".defaults-100-mine-marks"})if(File.Exists(aliasPath+suffix))File.Delete(aliasPath+suffix);
        File.WriteAllText(aliasPath,"[Balance - Wingburst]\nWingburstCooldown = 14\nWingburstDistance = 12\nWingburstShots = 16\nWingburstDamage = 42\n");
        BastionBalanceConfig.Bind(new ConfigFile(aliasPath,false));var aliases=BastionBalanceConfig.Capture();
        Near(aliases[(int)BastionSetting.WingburstCooldown],14,"renamed category preserves custom cooldown");Near(aliases[(int)BastionSetting.WingburstDamage],42,"renamed category preserves custom damage");Near(aliases[(int)BastionSetting.WingburstDistance],15,"renamed category upgrades unchanged distance");Near(aliases[(int)BastionSetting.WingburstShots],32,"renamed category upgrades unchanged count");
        File.Delete(aliasPath+".defaults-100-rush");File.WriteAllText(aliasPath,"[Balance - Wingburst]\nWingburstCooldown = 14\n[Balance - Heavenfall Rush]\nWingburstCooldown = 16\n");BastionBalanceConfig.Bind(new ConfigFile(aliasPath,false));Near(BastionBalanceConfig.Capture()[(int)BastionSetting.WingburstCooldown],16,"explicit new-category value takes priority");
        var impactPath=Path.Combine(root,".build","config_legacy","RushImpact100.cfg");
        foreach(string suffix in new[]{".defaults-100",".defaults-100-rush",".defaults-100-rush-impact",".defaults-100-rush-assault",".defaults-100-rush-skirmish",".defaults-100-mine-marks"})if(File.Exists(impactPath+suffix))File.Delete(impactPath+suffix);
        File.WriteAllText(impactPath,"[Balance - Heavenfall Rush]\nWingburstCooldown = 12\nWingburstDistance = 24\nWingburstShots = 36\n[Balance - Wingburst]\nWingburstDamage = 30\nWingburstRadius = 1.5\nWingburstRange = 55\nWingburstProc = 0.3\n");
        File.WriteAllText(impactPath+".defaults-100","already migrated");File.WriteAllText(impactPath+".defaults-100-rush","already migrated");
        var impactConfig=new ConfigFile(impactPath,false);BastionBalanceConfig.Bind(impactConfig);var impact=BastionBalanceConfig.Capture();
        Near(impact[(int)BastionSetting.WingburstCooldown],10,"new sidecar updates previous12s default despite existing rush marker");Near(impact[(int)BastionSetting.WingburstDistance],15,"new sidecar updates the previous24m default despite existing rush marker");Near(impact[(int)BastionSetting.WingburstDamage],40,"older30 percent default reaches the latest50 percent skirmish default");
        Near(impact[(int)BastionSetting.WingburstShots],36,"custom integer missile count survives impact migration");Near(impact[(int)BastionSetting.WingburstRadius],1.5f,"renamed category preserves custom blast radius");Near(impact[(int)BastionSetting.WingburstRange],55,"renamed category preserves custom targeting range");Near(impact[(int)BastionSetting.WingburstProc],.3f,"renamed category preserves custom proc coefficient");
        Check(File.Exists(impactPath+".defaults-100-rush-impact"),"new impact migration has its own one-time marker");
        foreach(var pair in impactConfig)Check(pair.Value.BoxedValue.GetType()==pair.Value.SettingType,"impact migration preserves exact boxed config types");
        ((ConfigEntry<float>)impactConfig.Single(p=>p.Key.Key=="WingburstCooldown").Value).Value=12;((ConfigEntry<float>)impactConfig.Single(p=>p.Key.Key=="WingburstDistance").Value).Value=24;((ConfigEntry<float>)impactConfig.Single(p=>p.Key.Key=="WingburstDamage").Value).Value=30;impactConfig.Save();
        BastionBalanceConfig.Bind(new ConfigFile(impactPath,false));var deliberate=BastionBalanceConfig.Capture();Near(deliberate[(int)BastionSetting.WingburstCooldown],12,"later deliberate return to12s cooldown stays unchanged");Near(deliberate[(int)BastionSetting.WingburstDistance],24,"later deliberate return to old distance stays unchanged");Near(deliberate[(int)BastionSetting.WingburstDamage],30,"later deliberate return to old damage stays unchanged");
        File.Delete(impactPath+".defaults-100-rush-impact");File.WriteAllText(impactPath,"[Balance - Heavenfall Rush]\nWingburstCooldown = 15\nWingburstDistance = 28\nWingburstDamage = 46\n[Balance - Wingburst]\nWingburstDamage = 42\n");
        BastionBalanceConfig.Bind(new ConfigFile(impactPath,false));var customized=BastionBalanceConfig.Capture();Near(customized[(int)BastionSetting.WingburstCooldown],15,"custom cooldown is not treated as old default");Near(customized[(int)BastionSetting.WingburstDistance],28,"custom distance is not treated as an old default");Near(customized[(int)BastionSetting.WingburstDamage],46,"explicit new-category damage takes precedence over legacy alias");
        // A current review install already has every earlier sidecar. Only
        // unchanged40%/2m defaults should receive the assault adjustment.
        var assaultPath=Path.Combine(root,".build","config_legacy","RushAssault100.cfg");
        var oldMarkers=new[]{".defaults-100",".defaults-100-rush",".defaults-100-rush-impact",".defaults-100-mine-lifetime"};
        foreach(string suffix in oldMarkers.Concat(new[]{".defaults-100-rush-assault",".defaults-100-rush-skirmish",".defaults-100-mine-marks"}))if(File.Exists(assaultPath+suffix))File.Delete(assaultPath+suffix);
        File.WriteAllText(assaultPath,"[Balance - Heavenfall Rush]\nWingburstDamage = 40\nWingburstRadius = 2\n");
        foreach(string suffix in oldMarkers)File.WriteAllText(assaultPath+suffix,"earlier migration already applied");
        var assaultConfig=new ConfigFile(assaultPath,false);BastionBalanceConfig.Bind(assaultConfig);var assault=BastionBalanceConfig.Capture();
        Near(assault[(int)BastionSetting.WingburstDamage],40,"new assault sidecar upgrades prior40 percent despite every old marker");
        Near(assault[(int)BastionSetting.WingburstRadius],4,"new assault sidecar upgrades prior2m blast radius");
        Check(File.Exists(assaultPath+".defaults-100-rush-assault"),"assault adjustment records its own one-time marker");
        foreach(var pair in assaultConfig)Check(pair.Value.BoxedValue.GetType()==pair.Value.SettingType,"assault migration preserves exact boxed config types");
        var assaultDamage=(ConfigEntry<float>)assaultConfig.Single(p=>p.Key.Key=="WingburstDamage").Value;
        var assaultRadius=(ConfigEntry<float>)assaultConfig.Single(p=>p.Key.Key=="WingburstRadius").Value;
        assaultDamage.Value=40;assaultRadius.Value=2;assaultConfig.Save();
        BastionBalanceConfig.Bind(new ConfigFile(assaultPath,false));var assaultDeliberate=BastionBalanceConfig.Capture();
        Near(assaultDeliberate[(int)BastionSetting.WingburstDamage],40,"later deliberate return to40 percent survives assault marker");
        Near(assaultDeliberate[(int)BastionSetting.WingburstRadius],2,"later deliberate return to2m survives assault marker");
        File.Delete(assaultPath+".defaults-100-rush-assault");
        File.WriteAllText(assaultPath,"[Balance - Heavenfall Rush]\nWingburstDamage = 135\nWingburstRadius = 6.5\n");
        var assaultCustomConfig=new ConfigFile(assaultPath,false);BastionBalanceConfig.Bind(assaultCustomConfig);var assaultCustom=BastionBalanceConfig.Capture();
        Near(assaultCustom[(int)BastionSetting.WingburstDamage],135,"non-default custom damage survives assault migration");
        Near(assaultCustom[(int)BastionSetting.WingburstRadius],6.5f,"non-default custom blast radius survives expanded-range migration");
        Near(BastionBalance.Specs[(int)BastionSetting.WingburstRadius].Maximum,8,"assault radius exposes intended8m menu/config ceiling");
        ((ConfigEntry<float>)assaultCustomConfig.Single(p=>p.Key.Key=="WingburstRadius").Value).Value=1000;
        Near(((ConfigEntry<float>)assaultCustomConfig.Single(p=>p.Key.Key=="WingburstRadius").Value).Value,8,"native config clamps the expanded radius at8m");

        // The skirmish review follows assault; all prior markers must coexist
        // without preventing its single default adjustment or later choices.
        var skirmishPath=Path.Combine(root,".build","config_legacy","RushSkirmish100.cfg");
        var skirmishOldMarkers=oldMarkers.Concat(new[]{".defaults-100-rush-assault"}).ToArray();
        foreach(string suffix in skirmishOldMarkers.Concat(new[]{".defaults-100-rush-skirmish",".defaults-100-mine-marks"}))if(File.Exists(skirmishPath+suffix))File.Delete(skirmishPath+suffix);
        File.WriteAllText(skirmishPath,"[Balance - Heavenfall Rush]\nWingburstDamage = 100\nWingburstDistance = 30\nWingburstCooldown = 18\nWingburstRadius = 4\n");
        foreach(string suffix in skirmishOldMarkers)File.WriteAllText(skirmishPath+suffix,"earlier migration already applied");
        var skirmishConfig=new ConfigFile(skirmishPath,false);BastionBalanceConfig.Bind(skirmishConfig);var skirmish=BastionBalanceConfig.Capture();
        Near(skirmish[(int)BastionSetting.WingburstDamage],40,"skirmish marker updates former100 percent default despite all previous markers");
        Near(skirmish[(int)BastionSetting.WingburstDistance],15,"skirmish marker updates former30m dash default");
        Near(skirmish[(int)BastionSetting.WingburstCooldown],10,"skirmish marker updates former18s recharge default");
        Near(skirmish[(int)BastionSetting.WingburstRadius],4,"skirmish migration leaves4m blast radius intact");
        Check(File.Exists(skirmishPath+".defaults-100-rush-skirmish"),"skirmish migration writes an independent one-time marker");
        foreach(var pair in skirmishConfig)Check(pair.Value.BoxedValue.GetType()==pair.Value.SettingType,"skirmish migration preserves exact boxed config types");
        ((ConfigEntry<float>)skirmishConfig.Single(p=>p.Key.Key=="WingburstDamage").Value).Value=100;
        ((ConfigEntry<float>)skirmishConfig.Single(p=>p.Key.Key=="WingburstDistance").Value).Value=30;
        ((ConfigEntry<float>)skirmishConfig.Single(p=>p.Key.Key=="WingburstCooldown").Value).Value=18;skirmishConfig.Save();
        BastionBalanceConfig.Bind(new ConfigFile(skirmishPath,false));var skirmishDeliberate=BastionBalanceConfig.Capture();
        Near(skirmishDeliberate[(int)BastionSetting.WingburstDamage],100,"later deliberate100 percent damage survives skirmish marker");
        Near(skirmishDeliberate[(int)BastionSetting.WingburstDistance],30,"later deliberate30m dash survives skirmish marker");
        Near(skirmishDeliberate[(int)BastionSetting.WingburstCooldown],18,"later deliberate18s recharge survives skirmish marker");
        File.Delete(skirmishPath+".defaults-100-rush-skirmish");
        File.WriteAllText(skirmishPath,"[Balance - Heavenfall Rush]\nWingburstDamage = 135\nWingburstDistance = 22\nWingburstCooldown = 14\nWingburstRadius = 6.5\n");
        BastionBalanceConfig.Bind(new ConfigFile(skirmishPath,false));var skirmishCustom=BastionBalanceConfig.Capture();
        Near(skirmishCustom[(int)BastionSetting.WingburstDamage],135,"custom micro-missile damage survives skirmish migration");
        Near(skirmishCustom[(int)BastionSetting.WingburstDistance],22,"custom dash distance survives skirmish migration");
        Near(skirmishCustom[(int)BastionSetting.WingburstCooldown],14,"custom recharge survives skirmish migration");
        Near(skirmishCustom[(int)BastionSetting.WingburstRadius],6.5f,"custom blast radius remains outside skirmish migration scope");

        // Sticky marks acquire farther targets and pursue them without
        // reviving the retired autonomous fuse in older saved profiles.
        var markPath=Path.Combine(root,".build","config_legacy","MineMarks100.cfg");
        var markOldMarkers=skirmishOldMarkers.Concat(new[]{".defaults-100-rush-skirmish"}).ToArray();
        foreach(string suffix in markOldMarkers.Concat(new[]{".defaults-100-mine-marks"}))if(File.Exists(markPath+suffix))File.Delete(markPath+suffix);
        File.WriteAllText(markPath,"[Balance - Widow Array]\nMineTriggerRange = 8\nMineWalkDistance = 12\nMineFuse = 0.1\n");
        foreach(string suffix in markOldMarkers)File.WriteAllText(markPath+suffix,"earlier migration already applied");
        int markWireLength=BastionBalance.Defaults().Length;
        var markConfig=new ConfigFile(markPath,false);BastionBalanceConfig.Bind(markConfig);var mark=BastionBalanceConfig.Capture();
        Near(mark[(int)BastionSetting.MineTriggerRange],30,"legacy mark migration reaches current detection default despite all earlier markers");
        Near(mark[(int)BastionSetting.MineWalkDistance],50,"legacy mark migration reaches current pursuit default");
        Check(File.Exists(markPath+".defaults-100-mine-marks"),"mark migration records its own one-time sidecar");
        Check(mark.Length==markWireLength&&mark.Length==BastionBalance.Specs.Length,"mark/fuse changes preserve full wire profile length");
        Check(BastionBalance.Specs[(int)BastionSetting.MineFuse].Retired,"autonomous mine fuse is a retired compatibility slot");
        Check(BastionBalance.Specs.Count(s=>s.Retired)==3,"leap range, leap time and autonomous fuse retain exactly three retired slots");
        Check(!markConfig.Any(p=>p.Key.Key=="MineFuse"),"legacy autonomous fuse is not a bound config/menu option");
        Near(mark[(int)BastionSetting.MineFuse],BastionBalance.Specs[(int)BastionSetting.MineFuse].Default,"saved0.1s fuse cannot reactivate autonomous explosions");
        foreach(var pair in markConfig)Check(pair.Value.BoxedValue.GetType()==pair.Value.SettingType,"mark migration preserves every boxed config type");
        ((ConfigEntry<float>)markConfig.Single(p=>p.Key.Key=="MineTriggerRange").Value).Value=8;
        ((ConfigEntry<float>)markConfig.Single(p=>p.Key.Key=="MineWalkDistance").Value).Value=12;markConfig.Save();
        BastionBalanceConfig.Bind(new ConfigFile(markPath,false));var markDeliberate=BastionBalanceConfig.Capture();
        Near(markDeliberate[(int)BastionSetting.MineTriggerRange],8,"later deliberate8m detection survives marker");
        Near(markDeliberate[(int)BastionSetting.MineWalkDistance],12,"later deliberate12m pursuit survives marker");
        Near(markDeliberate[(int)BastionSetting.MineFuse],BastionBalance.Specs[(int)BastionSetting.MineFuse].Default,"reloading deliberate ranges cannot revive legacy fuse");
        File.Delete(markPath+".defaults-100-mine-marks");
        File.WriteAllText(markPath,"[Balance - Widow Array]\nMineTriggerRange = 18\nMineWalkDistance = 24\nMineFuse = 0.05\n");
        var markCustomConfig=new ConfigFile(markPath,false);BastionBalanceConfig.Bind(markCustomConfig);var markCustom=BastionBalanceConfig.Capture();
        Near(markCustom[(int)BastionSetting.MineTriggerRange],18,"custom18m detection is preserved during migration");
        Near(markCustom[(int)BastionSetting.MineWalkDistance],24,"custom24m pursuit is preserved during migration");
        Near(markCustom[(int)BastionSetting.MineFuse],BastionBalance.Specs[(int)BastionSetting.MineFuse].Default,"custom old fuse remains inert even while ranges migrate");
        Check(!markCustomConfig.Any(p=>p.Key.Key=="MineFuse")&&markCustom.Length==markWireLength,"custom old fuse cannot reenter menu or shift wire slots");

        var lifetimePath=Path.Combine(root,".build","config_legacy","MineLifetime100.cfg");
        foreach(string suffix in new[]{".defaults-100",".defaults-100-rush",".defaults-100-rush-impact",".defaults-100-mine-lifetime",".defaults-100-rush-assault",".defaults-100-rush-skirmish",".defaults-100-mine-marks"})if(File.Exists(lifetimePath+suffix))File.Delete(lifetimePath+suffix);
        File.WriteAllText(lifetimePath,"[Balance - Widow Array]\nMineLifetime = 15\n");
        foreach(string suffix in new[]{".defaults-100",".defaults-100-rush",".defaults-100-rush-impact"})File.WriteAllText(lifetimePath+suffix,"earlier migration already applied");
        var lifetimeConfig=new ConfigFile(lifetimePath,false);BastionBalanceConfig.Bind(lifetimeConfig);var lifetimeCapture=BastionBalanceConfig.Capture();
        Near(lifetimeCapture[(int)BastionSetting.MineLifetime],60,"new lifetime marker upgrades saved15s despite all earlier migration markers");Check(File.Exists(lifetimePath+".defaults-100-mine-lifetime"),"mine lifetime migration records a separate one-time marker");Check(lifetimeCapture.Length==BastionBalance.Specs.Length,"lifetime change preserves profile wire count");
        var lifetimeEntry=(ConfigEntry<float>)lifetimeConfig.Single(p=>p.Key.Key=="MineLifetime").Value;Check(lifetimeEntry.BoxedValue.GetType()==typeof(float),"lifetime migration retains nativefloat boxed type");Near(BastionBalance.Specs[(int)BastionSetting.MineLifetime].Maximum,60,"lifetime upper bound remains below snapshot age rollover");
        lifetimeEntry.Value=15;lifetimeConfig.Save();BastionBalanceConfig.Bind(new ConfigFile(lifetimePath,false));Near(BastionBalanceConfig.Capture()[(int)BastionSetting.MineLifetime],15,"later deliberate return to15s survives subsequent binding");
        File.Delete(lifetimePath+".defaults-100-mine-lifetime");File.WriteAllText(lifetimePath,"[Balance - Widow Array]\nMineLifetime = 45\n");BastionBalanceConfig.Bind(new ConfigFile(lifetimePath,false));Near(BastionBalanceConfig.Capture()[(int)BastionSetting.MineLifetime],45,"custom lifetime is preserved during the one-time migration");
        var retiredPath=Path.Combine(root,".build","config_legacy","RetiredMines.cfg");
        foreach(string suffix in new[]{".defaults-100",".defaults-100-rush",".defaults-100-rush-impact",".defaults-100-rush-assault",".defaults-100-rush-skirmish",".defaults-100-mine-marks"})if(File.Exists(retiredPath+suffix))File.Delete(retiredPath+suffix);
        File.WriteAllText(retiredPath,"[Balance - Widow Array]\nMineLeapRange = 4.5\nMineLeapTime = 0.5\nMineFuse = 1.1\nMineChainBonus = 125\n");
        var retiredConfig=new ConfigFile(retiredPath,false);BastionBalanceConfig.Bind(retiredConfig);var retiredCapture=BastionBalanceConfig.Capture();
        Check(retiredCapture.Length==BastionBalance.Specs.Length,"retired options keep the full profile wire shape");
        foreach(var spec in BastionBalance.Specs.Where(s=>s.Retired))
        {
            Check(!retiredConfig.Any(p=>p.Key.Key==spec.Key.ToString()),"retired options are not bound or exposed as menu controls");
            Near(retiredCapture[(int)spec.Key],spec.Default,"retired wire slot keeps a safe default");
        }
        Near(retiredCapture[(int)BastionSetting.MineChainBonus],125,"setting after retired slots retains its proper wire index");
        Near(retiredCapture[(int)BastionSetting.MineFuse],BastionBalance.Specs[(int)BastionSetting.MineFuse].Default,"retired autonomous fuse cannot re-enable unaccompanied damage");
        string retiredSaved=File.ReadAllText(retiredPath);
        Check(retiredSaved.Contains("MineLeapRange = 4.5")&&retiredSaved.Contains("MineLeapTime = 0.5")&&retiredSaved.Contains("MineFuse = 1.1"),"unbound legacy leap/fuse values survive saving without becoming active options");
        // Exercise every remaining option, including those beyond retired slots.
        foreach(var pair in retiredConfig)
        {
            var spec=BastionBalanceConfig.Find(pair.Value);if(spec==null)continue;
            float value=spec.Sanitize(spec.Minimum+spec.Step*2);
            pair.Value.BoxedValue=pair.Value.SettingType==typeof(bool)?(object)(value>.5f):pair.Value.SettingType==typeof(int)?(object)(int)value:value;
        }
        var indexed=BastionBalanceConfig.Capture();
        foreach(var pair in retiredConfig)
        {
            var spec=BastionBalanceConfig.Find(pair.Value);if(spec==null)continue;
            float value=pair.Value.BoxedValue is bool flag?(flag?1:0):Convert.ToSingle(pair.Value.BoxedValue);
            Near(indexed[(int)spec.Key],spec.Sanitize(value),"active option capture follows its declared wire key");
        }
        var damagePath=Path.Combine(root,".build","config_legacy","DamageBudget100.cfg");
        string[] prior={".defaults-100",".defaults-100-rush",".defaults-100-rush-impact",".defaults-100-mine-lifetime",".defaults-100-rush-assault",".defaults-100-rush-skirmish",".defaults-100-mine-marks"};
        foreach(string suffix in prior)File.WriteAllText(damagePath+suffix,"earlier migration already applied");
        if(File.Exists(damagePath+".defaults-100-damage-budget"))File.Delete(damagePath+".defaults-100-damage-budget");
        var keys=new[]{BastionSetting.NeedleDamage,BastionSetting.MissileDamage,BastionSetting.HeavyDamage,BastionSetting.LaserDamage,BastionSetting.UpgradedLaserDamage,BastionSetting.WingburstDamage,BastionSetting.SkyfallDamage,BastionSetting.MineDamage,BastionSetting.GunnerDamage};
        float[] oldDamage={85,120,400,800,1000,50,600,150,80},newDamage={80,100,300,700,900,40,500,60,60};
        var text=new System.Text.StringBuilder();for(int i=0;i<keys.Length;i++){var spec=BastionBalance.Specs[(int)keys[i]];text.AppendLine("["+spec.Category+"]");text.AppendLine(keys[i]+" = "+oldDamage[i]);}
        File.WriteAllText(damagePath,text.ToString());var damageConfig=new ConfigFile(damagePath,false);BastionBalanceConfig.Bind(damageConfig);var damage=BastionBalanceConfig.Capture();
        for(int i=0;i<keys.Length;i++){Near(damage[(int)keys[i]],newDamage[i],"prior damage default migrates "+keys[i]);Check(damageConfig.Single(p=>p.Key.Key==keys[i].ToString()).Value.BoxedValue.GetType()==typeof(float),"damage migration preserves native boxed float "+keys[i]);}
        Check(File.Exists(damagePath+".defaults-100-damage-budget"),"damage pass gets its own one-time marker");
        for(int i=0;i<keys.Length;i++)((ConfigEntry<float>)damageConfig.Single(p=>p.Key.Key==keys[i].ToString()).Value).Value=oldDamage[i];damageConfig.Save();
        BastionBalanceConfig.Bind(new ConfigFile(damagePath,false));damage=BastionBalanceConfig.Capture();
        for(int i=0;i<keys.Length;i++)Near(damage[(int)keys[i]],oldDamage[i],"later deliberate former default is preserved "+keys[i]);
        File.Delete(damagePath+".defaults-100-damage-budget");text.Clear();
        for(int i=0;i<keys.Length;i++){var spec=BastionBalance.Specs[(int)keys[i]];text.AppendLine("["+spec.Category+"]");text.AppendLine(keys[i]+" = "+(newDamage[i]+7));}
        File.WriteAllText(damagePath,text.ToString());BastionBalanceConfig.Bind(new ConfigFile(damagePath,false));damage=BastionBalanceConfig.Capture();
        for(int i=0;i<keys.Length;i++)Near(damage[(int)keys[i]],newDamage[i]+7,"custom damage survives first migration "+keys[i]);
        var fieldPath=Path.Combine(root,".build","config_legacy","WidowField100.cfg");
        foreach(string suffix in prior.Concat(new[]{".defaults-100-damage-budget"}))File.WriteAllText(fieldPath+suffix,"earlier migration already applied");
        if(File.Exists(fieldPath+".defaults-100-widow-field"))File.Delete(fieldPath+".defaults-100-widow-field");
        File.WriteAllText(fieldPath,"[Balance - Widow Array]\nMineCount = 12\nMineDamage = 100\nMineDeployRadius = 9\n");
        var fieldConfig=new ConfigFile(fieldPath,false);BastionBalanceConfig.Bind(fieldConfig);var field=BastionBalanceConfig.Capture();
        Near(field[(int)BastionSetting.MineCount],24,"saved previous-review count migrates despite prior damage marker");
        Near(field[(int)BastionSetting.MineDamage],60,"saved previous-review mine damage migrates once");
        Near(field[(int)BastionSetting.MineDeployRadius],15,"saved previous-review field radius migrates once");
        Check(File.Exists(fieldPath+".defaults-100-widow-field"),"three-ring field migration has its own one-time marker");
        Check(fieldConfig.Single(p=>p.Key.Key=="MineCount").Value.BoxedValue is int,"mine count migration preserves native integer config type");
        var upgraded=new[]{BastionSetting.UpgradedMineDeployRadius,BastionSetting.UpgradedMineCount,BastionSetting.UpgradedMineChainBonus,BastionSetting.UpgradedMineBlastRadius,BastionSetting.UpgradedWingburstDistance,BastionSetting.UpgradedWingburstShots,BastionSetting.UpgradedWingburstCooldown};
        foreach(var upgrade in upgraded)
        {
            var entry=fieldConfig.Single(p=>p.Key.Key==upgrade.ToString()).Value;var spec=BastionBalance.Specs[(int)upgrade];
            float value=spec.Sanitize(spec.Default+spec.Step*2);
            entry.BoxedValue=entry.SettingType==typeof(int)?(object)(int)value:value;
        }
        ((ConfigEntry<int>)fieldConfig.Single(p=>p.Key.Key=="MineCount").Value).Value=12;
        ((ConfigEntry<float>)fieldConfig.Single(p=>p.Key.Key=="MineDamage").Value).Value=100;
        ((ConfigEntry<float>)fieldConfig.Single(p=>p.Key.Key=="MineDeployRadius").Value).Value=9;
        fieldConfig.Save();BastionBalanceConfig.Bind(new ConfigFile(fieldPath,false));field=BastionBalanceConfig.Capture();
        Near(field[(int)BastionSetting.MineCount],12,"later deliberate old count survives rebind");Near(field[(int)BastionSetting.MineDamage],100,"later deliberate old damage survives rebind");Near(field[(int)BastionSetting.MineDeployRadius],9,"later deliberate old field size survives rebind");
        foreach(var upgrade in upgraded){var spec=BastionBalance.Specs[(int)upgrade];Near(field[(int)upgrade],spec.Sanitize(spec.Default+spec.Step*2),"new upgrade option persists: "+upgrade);}
        File.Delete(fieldPath+".defaults-100-widow-field");File.WriteAllText(fieldPath,"[Balance - Widow Array]\nMineCount = 27\nMineDamage = 90\nMineDeployRadius = 19.5\n");
        BastionBalanceConfig.Bind(new ConfigFile(fieldPath,false));field=BastionBalanceConfig.Capture();
        Near(field[(int)BastionSetting.MineCount],27,"custom mine count survives first field migration");Near(field[(int)BastionSetting.MineDamage],90,"custom mine damage survives first field migration");Near(field[(int)BastionSetting.MineDeployRadius],19.5f,"custom field radius survives first field migration");
        var treePath=Path.Combine(root,".build","config_legacy","UpgradeTree100.cfg");
        string treeMarker=treePath+".defaults-100-upgrade-tree";
        if(File.Exists(treeMarker))File.Delete(treeMarker);
        File.WriteAllText(treePath,"[Balance - Upgrades]\nCostOne = 60\nCostTwo = 140\nCostThree = 300\n");
        BastionBalanceConfig.Bind(new ConfigFile(treePath,false));var tree=BastionBalanceConfig.Capture();
        Near(tree[(int)BastionSetting.CostOne],20,"old Tier I default migrates once");Near(tree[(int)BastionSetting.CostTwo],60,"old Tier II default migrates once");Near(tree[(int)BastionSetting.CostThree],150,"old Tier III default migrates once");
        Check(File.Exists(treeMarker),"tree migration sidecar recorded");
        File.WriteAllText(treePath,"[Balance - Upgrades]\nCostOne = 60\nCostTwo = 99\nCostThree = 199\n");
        BastionBalanceConfig.Bind(new ConfigFile(treePath,false));tree=BastionBalanceConfig.Capture();
        Near(tree[(int)BastionSetting.CostOne],60,"later deliberate price equal to former default is preserved");Near(tree[(int)BastionSetting.CostTwo],99,"custom price preserved after marker");
        File.Delete(treeMarker);File.WriteAllText(treePath,"[Balance - Upgrades]\nCostOne = 33\nCostTwo = 99\nCostThree = 199\n");
        BastionBalanceConfig.Bind(new ConfigFile(treePath,false));tree=BastionBalanceConfig.Capture();
        Near(tree[(int)BastionSetting.CostOne],33,"custom Tier I price preserved during migration");Near(tree[(int)BastionSetting.CostTwo],99,"custom Tier II price preserved during migration");Near(tree[(int)BastionSetting.CostThree],199,"custom Tier III price preserved during migration");
        Console.WriteLine("PASS: "+count+" real BepInEx config binding, menu coverage, 1.0 migration and persistence checks.");
    }
}
