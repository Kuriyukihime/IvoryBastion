using System;
using System.Linq;
using System.Collections.Generic;
using BepInEx.Configuration;
namespace IvoryBastion
{
    internal static class BastionBalanceConfig
    {
        internal const string Scope="Host setting. Saved changes apply when the next run starts; multiplayer clients use the host's balance without changing their own config files. ";
        private static readonly List<ConfigEntryBase> entries=new List<ConfigEntryBase>();
        private static readonly Dictionary<ConfigEntryBase,BastionBalanceSpec> specs=new Dictionary<ConfigEntryBase,BastionBalanceSpec>();
        internal static void Bind(ConfigFile file)
        {
            var legacyRush=ReadLegacyRush(file.ConfigFilePath);
            entries.Clear();specs.Clear();
            foreach(var spec in BastionBalance.Specs)
            {
                if(spec.Retired)continue;
                ConfigEntryBase entry;
                string description=Scope+spec.Description;
                switch(spec.Kind)
                {
                    case BastionBalanceKind.Bool:entry=file.Bind(spec.Category,spec.Key.ToString(),spec.Default>.5f,description);break;
                    case BastionBalanceKind.Int:entry=file.Bind(spec.Category,spec.Key.ToString(),(int)spec.Default,new ConfigDescription(description,new AcceptableValueRange<int>((int)spec.Minimum,(int)spec.Maximum)));break;
                    default:entry=file.Bind(spec.Category,spec.Key.ToString(),spec.Default,new ConfigDescription(description,new AcceptableValueRange<float>(spec.Minimum,spec.Maximum)));break;
                }
                entries.Add(entry);specs.Add(entry,spec);
            }
            Migrate100Defaults(file);
            MigrateRushDefaults(file,legacyRush);
            MigrateRushImpactDefaults(file,legacyRush);
            MigrateMineLifetimeDefaults(file);
            MigrateRushAssaultDefaults(file);
            MigrateRushSkirmishDefaults(file);
            MigrateMineMarkDefaults(file);
            MigrateDamageBudgetDefaults(file);
            MigrateWideWidowDefaults(file);
            MigrateUpgradeTreeDefaults(file);
            MigrateMineSpeedDefaults(file);
            Migrate122KitDefaults(file);
            Migrate122ShieldDefaults(file);
            Migrate123FabricationDefaults(file);
            Migrate130MineRangeDefaults(file);
        }
        private static void Migrate130MineRangeDefaults(ConfigFile file)
        {
            string marker=file.ConfigFilePath+".defaults-130-mine-range";
            if(System.IO.File.Exists(marker))return;
            try
            {
                foreach(var entry in entries)
                {
                    var spec=specs[entry];float former=spec.Key==BastionSetting.MineTriggerRange?16:spec.Key==BastionSetting.MineWalkDistance?20:-1;
                    if(former>=0&&Math.Abs(Convert.ToSingle(entry.BoxedValue)-former)<.001f)entry.BoxedValue=spec.Default;
                }
                file.Save();System.IO.File.WriteAllText(marker,"1.3.0 Widow detection/pursuit defaults raised to 30m/50m once; custom and later deliberate settings preserved.");
            }
            catch(System.IO.IOException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion mine range migration: "+error.Message);}
            catch(UnauthorizedAccessException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion mine range migration: "+error.Message);}
        }
        private static void Migrate123FabricationDefaults(ConfigFile file)
        {
            string marker=file.ConfigFilePath+".defaults-123-fabrication";
            if(System.IO.File.Exists(marker))return;
            try
            {
                foreach(var entry in entries)
                {
                    var spec=specs[entry];int former=spec.Key==BastionSetting.IdleFabrication?14:spec.Key==BastionSetting.CombatFabrication?35:-1;
                    if(former>=0&&Convert.ToInt32(entry.BoxedValue)==former)entry.BoxedValue=(int)spec.Default;
                }
                file.Save();System.IO.File.WriteAllText(marker,"1.2.3 Fabrication defaults raised by 20%, rounding idle upward to a whole number (17/42 per minute); custom and later deliberate settings preserved.");
            }
            catch(System.IO.IOException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion Fabrication migration: "+error.Message);}
            catch(UnauthorizedAccessException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion Fabrication migration: "+error.Message);}
        }
        private static void Migrate122ShieldDefaults(ConfigFile file)
        {
            string marker=file.ConfigFilePath+".defaults-122-shield-balance";
            if(System.IO.File.Exists(marker))return;
            try
            {
                foreach(var entry in entries)
                {
                    var spec=specs[entry];float former=spec.Key==BastionSetting.MobileShieldDR||spec.Key==BastionSetting.SiegeShieldDR?50:spec.Key==BastionSetting.HardeningBonus?5:-1;
                    if(former>=0&&Math.Abs(Convert.ToSingle(entry.BoxedValue)-former)<.001f)entry.BoxedValue=spec.Default;
                }
                file.Save();System.IO.File.WriteAllText(marker,"Seraph defaults updated once to 40% DR and a four-point Hardening bonus; custom and later deliberate settings preserved.");
            }
            catch(System.IO.IOException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion shield balance migration: "+error.Message);}
            catch(UnauthorizedAccessException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion shield balance migration: "+error.Message);}
        }
        private static void Migrate122KitDefaults(ConfigFile file)
        {
            string marker=file.ConfigFilePath+".defaults-122-kit-balance";
            if(System.IO.File.Exists(marker))return;
            try
            {
                foreach(var entry in entries)
                {
                    var spec=specs[entry];float former=-1;
                    switch(spec.Key)
                    {
                        case BastionSetting.BaseDamage:former=12;break;
                        case BastionSetting.DamageGrowth:former=2.4f;break;
                        case BastionSetting.GunnerDamage:former=70;break;
                        case BastionSetting.NeedleProc:
                        case BastionSetting.MissileProc:former=.6f;break;
                        case BastionSetting.HeavyProc:
                        case BastionSetting.LaserProc:
                        case BastionSetting.SkyfallProc:former=1;break;
                        case BastionSetting.DroneProc:
                        case BastionSetting.MineProc:former=.5f;break;
                        case BastionSetting.WingburstProc:former=.15f;break;
                    }
                    if(former>=0&&Math.Abs(Convert.ToSingle(entry.BoxedValue)-former)<.001f)
                        entry.BoxedValue=spec.Default;
                }
                file.Save();System.IO.File.WriteAllText(marker,"1.2.2 kit defaults updated once; custom and later deliberate settings preserved. Upgrade settings unchanged.");
            }
            catch(System.IO.IOException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion kit balance migration: "+error.Message);}
            catch(UnauthorizedAccessException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion kit balance migration: "+error.Message);}
        }
        private static void MigrateMineSpeedDefaults(ConfigFile file)
        {
            string marker=file.ConfigFilePath+".defaults-103-mine-speed";
            if(System.IO.File.Exists(marker))return;
            try
            {
                foreach(var entry in entries)
                    if(specs[entry].Key==BastionSetting.MineWalkSpeed&&Math.Abs(Convert.ToSingle(entry.BoxedValue)-5)<.001f)
                        entry.BoxedValue=specs[entry].Default;
                file.Save();System.IO.File.WriteAllText(marker,"Widow walking speed updated once; custom and later deliberate settings preserved.");
            }
            catch(System.IO.IOException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion mine speed migration: "+error.Message);}
            catch(UnauthorizedAccessException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion mine speed migration: "+error.Message);}
        }
        private static void MigrateUpgradeTreeDefaults(ConfigFile file)
        {
            string marker=file.ConfigFilePath+".defaults-100-upgrade-tree";
            if(System.IO.File.Exists(marker))return;
            try
            {
                foreach(var entry in entries)
                {
                    var spec=specs[entry];float former=spec.Key==BastionSetting.CostOne?60:spec.Key==BastionSetting.CostTwo?140:spec.Key==BastionSetting.CostThree?300:-1;
                    if(former>=0&&Math.Abs(Convert.ToSingle(entry.BoxedValue)-former)<.001f)entry.BoxedValue=(int)spec.Default;
                }
                file.Save();System.IO.File.WriteAllText(marker,"Ranked tree point-cost defaults migrated once; custom prices retained.");
            }
            catch(System.IO.IOException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion upgrade-tree migration: "+error.Message);}
            catch(UnauthorizedAccessException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion upgrade-tree migration: "+error.Message);}
        }
        private static void MigrateWideWidowDefaults(ConfigFile file)
        {
            string marker=file.ConfigFilePath+".defaults-100-widow-field";
            if(System.IO.File.Exists(marker))return;
            try
            {
                foreach(var entry in entries)
                {
                    var spec=specs[entry];float former=spec.Key==BastionSetting.MineCount?12:spec.Key==BastionSetting.MineDamage?100:spec.Key==BastionSetting.MineDeployRadius?9:-1;
                    if(former>=0&&Math.Abs(Convert.ToSingle(entry.BoxedValue)-former)<.001f)entry.BoxedValue=spec.Kind==BastionBalanceKind.Int?(object)(int)spec.Default:spec.Default;
                }
                file.Save();System.IO.File.WriteAllText(marker,"Three-ring Widow Array defaults applied once; custom and later deliberate choices preserved.");
            }
            catch(System.IO.IOException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion Widow field migration: "+error.Message);}
            catch(UnauthorizedAccessException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion Widow field migration: "+error.Message);}
        }
        private static void MigrateDamageBudgetDefaults(ConfigFile file)
        {
            string marker=file.ConfigFilePath+".defaults-100-damage-budget";
            if(System.IO.File.Exists(marker))return;
            try
            {
                foreach(var entry in entries)
                {
                    var spec=specs[entry];float former=-1;
                    switch(spec.Key)
                    {
                        case BastionSetting.NeedleDamage:former=85;break;
                        case BastionSetting.MissileDamage:former=120;break;
                        case BastionSetting.HeavyDamage:former=400;break;
                        case BastionSetting.LaserDamage:former=800;break;
                        case BastionSetting.UpgradedLaserDamage:former=1000;break;
                        case BastionSetting.WingburstDamage:former=50;break;
                        case BastionSetting.SkyfallDamage:former=600;break;
                        case BastionSetting.MineDamage:former=150;break;
                        case BastionSetting.GunnerDamage:former=80;break;
                    }
                    if(former>=0&&Math.Abs(Convert.ToSingle(entry.BoxedValue)-former)<.001f)entry.BoxedValue=spec.Default;
                }
                file.Save();System.IO.File.WriteAllText(marker,"Vanilla-referenced 1.0 damage defaults applied once; custom and later deliberate choices preserved.");
            }
            catch(System.IO.IOException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion damage budget migration: "+error.Message);}
            catch(UnauthorizedAccessException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion damage budget migration: "+error.Message);}
        }
        private static Dictionary<BastionSetting,float> ReadLegacyRush(string path)
        {
            // Read before Bind can save the new category. BepInEx keeps old
            // category values as unbound entries; preserve intentional edits.
            var result=new Dictionary<BastionSetting,float>();
            try
            {
                if(!System.IO.File.Exists(path))return result;
                var saved=new Dictionary<string,string>();string section="";
                foreach(string raw in System.IO.File.ReadLines(path))
                {
                    string line=raw.Trim();if(line.Length==0||line.StartsWith("#"))continue;
                    if(line.StartsWith("[")&&line.EndsWith("]")){section=line.Substring(1,line.Length-2);continue;}
                    int split=line.IndexOf('=');if(split>0)saved[section+":"+line.Substring(0,split).Trim()]=line.Substring(split+1).Trim();
                }
                foreach(var spec in BastionBalance.Specs)
                    if(spec.Category=="Balance - Heavenfall Rush"&&!saved.ContainsKey(spec.Category+":"+spec.Key)&&
                        saved.TryGetValue("Balance - Wingburst:"+spec.Key,out string text)&&
                        float.TryParse(text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float value))
                        result[spec.Key]=spec.Sanitize(value);
            }
            catch(System.IO.IOException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion legacy config: "+error.Message);}
            catch(UnauthorizedAccessException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion legacy config: "+error.Message);}
            return result;
        }
        private static void MigrateRushDefaults(ConfigFile file,Dictionary<BastionSetting,float> legacyRush)
        {
            string marker=file.ConfigFilePath+".defaults-100-rush";if(System.IO.File.Exists(marker))return;
            try
            {
                foreach(var entry in entries)
                {
                    var spec=specs[entry];
                    if(legacyRush.TryGetValue(spec.Key,out float retained))entry.BoxedValue=entry.SettingType==typeof(int)?(object)(int)retained:retained;
                    float old=spec.Key==BastionSetting.MissileCapacity?40:spec.Key==BastionSetting.UpgradedCapacity?60:
                        spec.Key==BastionSetting.WingburstCooldown?6:spec.Key==BastionSetting.WingburstDistance?12:spec.Key==BastionSetting.WingburstShots?16:
                        spec.Key==BastionSetting.WingburstDuration?.38f:spec.Key==BastionSetting.WingburstWindup?.08f:-1;
                    if(old>=0&&Math.Abs(Convert.ToSingle(entry.BoxedValue)-old)<.001f)
                        entry.BoxedValue=entry.SettingType==typeof(int)?(object)(int)spec.Default:spec.Default;
                }
                foreach(var pair in file)
                    if(pair.Key.Section=="Controls"&&pair.Key.Key=="Transformation"&&pair.Value is ConfigEntry<KeyboardShortcut> key&&key.Value.MainKey==UnityEngine.KeyCode.F&&!key.Value.Modifiers.Any())
                        key.Value=new KeyboardShortcut(UnityEngine.KeyCode.C);
                file.Save();System.IO.File.WriteAllText(marker,"Unpublished 1.0 rush defaults applied once; later choices are preserved.");
            }
            catch(System.IO.IOException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion rush migration: "+error.Message);}
            catch(UnauthorizedAccessException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion rush migration: "+error.Message);}
        }
        private static void MigrateRushImpactDefaults(ConfigFile file,Dictionary<BastionSetting,float> legacyRush)
        {
            // Earlier review installs already have .defaults-100-rush. Use a
            // distinct marker for the stronger rush, preserving
            // custom values and all choices made after this migration.
            string marker=file.ConfigFilePath+".defaults-100-rush-impact";
            if(System.IO.File.Exists(marker))return;
            try
            {
                foreach(var entry in entries)
                {
                    var spec=specs[entry];
                    if((spec.Key==BastionSetting.WingburstDamage||spec.Key==BastionSetting.WingburstRange||spec.Key==BastionSetting.WingburstRadius||spec.Key==BastionSetting.WingburstProc)&&legacyRush.TryGetValue(spec.Key,out float retained))
                        entry.BoxedValue=entry.SettingType==typeof(int)?(object)(int)retained:retained;
                    float old=spec.Key==BastionSetting.WingburstCooldown?12:spec.Key==BastionSetting.WingburstDistance?24:spec.Key==BastionSetting.WingburstDamage?30:-1;
                    if(old>=0&&Math.Abs(Convert.ToSingle(entry.BoxedValue)-old)<.001f)
                        entry.BoxedValue=entry.SettingType==typeof(int)?(object)(int)spec.Default:spec.Default;
                }
                file.Save();System.IO.File.WriteAllText(marker,"Heavenfall Rush defaults applied once; later choices are preserved.");
            }
            catch(System.IO.IOException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion rush impact migration: "+error.Message);}
            catch(UnauthorizedAccessException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion rush impact migration: "+error.Message);}
        }
        private static void MigrateMineMarkDefaults(ConfigFile file)
        {
            string marker=file.ConfigFilePath+".defaults-100-mine-marks";
            if(System.IO.File.Exists(marker))return;
            try
            {
                foreach(var entry in entries)
                {
                    var spec=specs[entry];
                    float former=spec.Key==BastionSetting.MineTriggerRange?8f:spec.Key==BastionSetting.MineWalkDistance?12f:-1;
                    if(former>=0&&Math.Abs(Convert.ToSingle(entry.BoxedValue)-former)<.001f)entry.BoxedValue=spec.Default;
                }
                file.Save();System.IO.File.WriteAllText(marker,"Widow Array detection and pursuit defaults applied once; later choices are preserved.");
            }
            catch(System.IO.IOException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion mine mark migration: "+error.Message);}
            catch(UnauthorizedAccessException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion mine mark migration: "+error.Message);}
        }
        private static void MigrateRushSkirmishDefaults(ConfigFile file)
        {
            string marker=file.ConfigFilePath+".defaults-100-rush-skirmish";
            if(System.IO.File.Exists(marker))return;
            try
            {
                foreach(var entry in entries)
                {
                    var spec=specs[entry];
                    float former=spec.Key==BastionSetting.WingburstDamage?100f:spec.Key==BastionSetting.WingburstDistance?30f:spec.Key==BastionSetting.WingburstCooldown?18f:-1;
                    if(former>=0&&Math.Abs(Convert.ToSingle(entry.BoxedValue)-former)<.001f)entry.BoxedValue=spec.Default;
                }
                file.Save();System.IO.File.WriteAllText(marker,"Two-charge Heavenfall Rush defaults applied once; later choices are preserved.");
            }
            catch(System.IO.IOException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion rush skirmish migration: "+error.Message);}
            catch(UnauthorizedAccessException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion rush skirmish migration: "+error.Message);}
        }
        private static void MigrateRushAssaultDefaults(ConfigFile file)
        {
            string marker=file.ConfigFilePath+".defaults-100-rush-assault";
            if(System.IO.File.Exists(marker))return;
            try
            {
                foreach(var entry in entries)
                {
                    var spec=specs[entry];
                    float former=spec.Key==BastionSetting.WingburstDamage?40f:spec.Key==BastionSetting.WingburstRadius?2f:-1;
                    if(former>=0&&Math.Abs(Convert.ToSingle(entry.BoxedValue)-former)<.001f)entry.BoxedValue=spec.Default;
                }
                file.Save();System.IO.File.WriteAllText(marker,"Heavenfall Rush damage and splash defaults applied once; later choices are preserved.");
            }
            catch(System.IO.IOException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion rush assault migration: "+error.Message);}
            catch(UnauthorizedAccessException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion rush assault migration: "+error.Message);}
        }
        private static void MigrateMineLifetimeDefaults(ConfigFile file)
        {
            // Earlier review installs already carry the other 1.0 markers.
            // Update the former default once while keeping custom lifetimes.
            string marker=file.ConfigFilePath+".defaults-100-mine-lifetime";
            if(System.IO.File.Exists(marker))return;
            try
            {
                foreach(var entry in entries)
                {
                    var spec=specs[entry];
                    if(spec.Key==BastionSetting.MineLifetime&&Math.Abs(Convert.ToSingle(entry.BoxedValue)-15f)<.001f)
                        entry.BoxedValue=spec.Default;
                }
                file.Save();System.IO.File.WriteAllText(marker,"Widow Array lifetime default applied once; later choices are preserved.");
            }
            catch(System.IO.IOException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion mine lifetime migration: "+error.Message);}
            catch(UnauthorizedAccessException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion mine lifetime migration: "+error.Message);}
        }
        private static void Migrate100Defaults(ConfigFile file)
        {
            // Update saved old defaults once; preserve deliberately customized
            // values and allow subsequent edits back to any value. The marker is
            // local bookkeeping, not a hidden gameplay setting or synced config.
            string marker=file.ConfigFilePath+".defaults-100";
            if(System.IO.File.Exists(marker))return;
            try
            {
                foreach(var entry in entries)
                {
                    var spec=specs[entry];float old=spec.Key==BastionSetting.NeedleDamage?90:
                        spec.Key==BastionSetting.MissileDamage?130:spec.Key==BastionSetting.HeavyDamage?450:
                        spec.Key==BastionSetting.LaserDamage?900:spec.Key==BastionSetting.UpgradedLaserDamage?1200:
                        spec.Key==BastionSetting.SkyfallDamage?800:-1;
                    if(old>=0&&Math.Abs(Convert.ToSingle(entry.BoxedValue)-old)<.001f)entry.BoxedValue=spec.Default;
                }
                file.Save();System.IO.File.WriteAllText(marker,"1.0.0 defaults applied; later custom values are preserved.");
            }
            catch(System.IO.IOException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion default migration: "+error.Message);}
            catch(UnauthorizedAccessException error){System.Diagnostics.Trace.TraceWarning("Ivory Bastion default migration: "+error.Message);}
        }
        internal static BastionBalanceSpec Find(ConfigEntryBase entry)=>specs.TryGetValue(entry,out var spec)?spec:null;
        internal static float[] Capture()
        {
            var values=BastionBalance.Defaults();
            foreach(var entry in entries)
            {
                var spec=specs[entry];
                values[(int)spec.Key]=spec.Sanitize(entry.BoxedValue is bool b?(b?1:0):Convert.ToSingle(entry.BoxedValue));
            }
            return values;
        }
    }
}
