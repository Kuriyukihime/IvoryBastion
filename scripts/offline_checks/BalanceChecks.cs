using System;
using System.IO;
using System.Linq;
using IvoryBastion;
using Mono.Cecil;
static class BalanceChecks
{
    static int count;
    static void Check(bool ok,string why){count++;if(!ok)throw new Exception("Balance config: "+why);}
    static void Near(float a,float b,string why)=>Check(Math.Abs(a-b)<.0002f,why);
    static void Set(float[] profile,BastionSetting key,float value)=>profile[(int)key]=value;
    internal static void Run(string root)
    {
        try
        {
            Check(BastionBalance.Specs.Length==Enum.GetValues<BastionSetting>().Length,"all keys have metadata");
            Check(BastionBalance.Specs.Select(s=>s.Category+"/"+s.Key).Distinct().Count()==BastionBalance.Specs.Length,"unique config definitions");
            var values=BastionBalance.Defaults();
            for(int i=0;i<values.Length;i++)
            {
                var s=BastionBalance.Specs[i];Check((int)s.Key==i,"wire order is deterministic");Check(s.Minimum<=s.Default&&s.Default<=s.Maximum&&s.Step>0,"valid bounded slider/default");
                Near(s.Sanitize(float.NaN),s.Default,"NaN recovers to a safe default");Near(s.Sanitize(float.PositiveInfinity),s.Default,"infinity recovers to a safe default");
                Check(s.Sanitize(s.Minimum-10000)>=s.Minimum&&s.Sanitize(s.Maximum+10000)<=s.Maximum,"out-of-range values clamp");
                Check(!string.IsNullOrWhiteSpace(s.Description)&&!string.IsNullOrWhiteSpace(s.Name),"every option has a readable name and help");
            }
            Check(!BastionBalance.Apply(new float[1])&&!BastionBalance.Apply(null),"incomplete network profiles cannot replace settings");
            Set(values,BastionSetting.NeedleDamage,250);Set(values,BastionSetting.NeedleRate,10);Set(values,BastionSetting.MissileCapacity,80);Set(values,BastionSetting.UpgradedCapacity,60);Set(values,BastionSetting.MissileStartingAmmo,200);
            Set(values,BastionSetting.HeavyEvery,3);Set(values,BastionSetting.SiegeReload,2);Set(values,BastionSetting.AegisReload,3);Set(values,BastionSetting.AegisDuration,10);Set(values,BastionSetting.AegisHealing,40);Set(values,BastionSetting.SiegeRepair,0);
            Set(values,BastionSetting.SiegeShieldDR,90);Set(values,BastionSetting.HardeningBonus,50);Set(values,BastionSetting.SkyfallCount,20);Set(values,BastionSetting.SkyfallRadius,25);Set(values,BastionSetting.CostOne,0);
            Check(BastionBalance.Apply(values),"custom host profile accepted");values[(int)BastionSetting.NeedleDamage]=999;Near(BastionBank.NeedleDamage,2.5f,"active profile is copied and damage uses configured percentages");
            Near(BastionBank.PrimaryFireInterval(BastionAction.Pistol,1),.1f,"custom primary cadence");
            var bank=new BastionBank();Check(bank.Capacity==80&&bank.Ammo==80,"starting ammo clamps to custom capacity");bank.Ranks[0]=1;Check(bank.Capacity==80,"magazine upgrade cannot shrink the base magazine");
            Check(BastionBank.Cost(0)==0&&bank.Buy(1,0,true),"zero-cost upgrades work without spending negative currency");
            bank.Ammo=0;bank.DefenseTime=10;bank.Tick(11,true,false);Near(bank.Ammo,62,"custom Aegis reload multiplier integrates its exact duration boundary");
            bank.Ranks[1]=3;bank.Choices[1]=0;Near(bank.ShieldReduction(true),.95f,"Hardening cannot overflow into negative incoming damage");
            Check(BastionWarheadGeometry.Count==20&&BastionWarheadGeometry.Radius==25,"Skyfall targeting/effects share custom count and radius");
            var heal=new BastionRepair();float total=0;for(int i=0;i<40;i++)total+=heal.Tick(.25f,true,100,0,false,10-i*.25f,.4f);
            Near(total,40,"custom Aegis duration changes timing without multiplying its total healing");
            // BepInEx 5's installed assembly requires the 32-bit CLR. Run its
            // real ConfigFile against our exact binding source in an isolated
            // Framework runner; never patch the installed dependency.
            var start=new System.Diagnostics.ProcessStartInfo("powershell.exe");
            start.UseShellExecute=false;start.CreateNoWindow=true;
            start.RedirectStandardOutput=true;start.RedirectStandardError=true;
            start.ArgumentList.Add("-NoProfile");start.ArgumentList.Add("-ExecutionPolicy");start.ArgumentList.Add("Bypass");
            start.ArgumentList.Add("-File");start.ArgumentList.Add(Path.Combine(root,"scripts","check_config_legacy.ps1"));start.ArgumentList.Add("-Root");start.ArgumentList.Add(root);
            using(var process=System.Diagnostics.Process.Start(start))
            {
                var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();process.WaitForExit();
                Console.Write(output.GetAwaiter().GetResult());Console.Error.Write(errors.GetAwaiter().GetResult());
                Check(process.ExitCode==0,"real BepInEx binding, persistence and option coverage");
            }
            using var assembly=AssemblyDefinition.ReadAssembly(Path.Combine(root,"bin/Release/netstandard2.1/IvoryBastion.dll"));
            var options=assembly.MainModule.Types.Single(t=>t.Name=="BastionOptions").Methods.Single(m=>m.Name=="Register");
            var calls=options.Body.Instructions.Where(i=>i.Operand is MethodReference).Select(i=>(MethodReference)i.Operand).ToArray();
            foreach(var option in new[]{"CheckBoxOption","IntSliderOption","StepSliderOption","KeyBindOption","GenericButtonOption"})Check(calls.Any(m=>m.Name==".ctor"&&m.DeclaringType.Name==option),"menu registers "+option);
            Check(calls.Any(m=>m.Name=="GetEnumerator"&&m.DeclaringType.Name=="ConfigFile"),"menu scans the complete config instead of a hand-picked subset");
            // Every catalog key must be consumed by gameplay code, not merely
            // displayed in a menu or tooltip.
            var files=Directory.GetFiles(Path.Combine(root,"src"),"*.cs").Where(p=>!new[]{"BastionBalanceValues.cs","BastionBalanceConfig.cs","BastionOptions.cs","BastionBalanceText.cs","BastionHud.cs"}.Contains(Path.GetFileName(p)));
            string source=string.Join("\n",files.Select(File.ReadAllText));
            foreach(var spec in BastionBalance.Specs.Where(s=>!s.Retired))Check(System.Text.RegularExpressions.Regex.IsMatch(source,@"\b(?:S|BastionSetting)\."+spec.Key+@"\b"),"gameplay consumes "+spec.Key);
        }
        finally{BastionBalance.Reset();}
        Console.WriteLine($"PASS: {count} config coverage, safe bounds, persistence, custom balance and Risk of Options integration checks.");
    }
}
