using System;
using System.IO;
using System.Linq;
using IvoryBastion;
using Mono.Cecil;
using Mono.Cecil.Cil;

static class DefenseStackingChecks
{
    static int count;
    static void Check(bool value,string why){count++;if(!value)throw new Exception("Defense stacking: "+why);}
    static void Near(float actual,float expected,string why)=>Check(Math.Abs(actual-expected)<.002f,why+": "+actual+" != "+expected);
    static void Hit(string name,bool siege,float armor,float duration,bool hardening,float nativeBarrier,
        float aegisCharge,float shieldCharge,float expectedNative,float expectedAegis,float expectedShield,float expectedHealth)
    {
        var bank=new BastionBank{DefenseTime=duration};if(hardening)bank.Ranks[1]=3;
        var aegis=new BastionAegisReserve();aegis.Grant(aegisCharge,1000);
        var seraph=new BastionShieldReserve();seraph.Receive(1000,shieldCharge);
        float damage=BastionBank.PredictedDamage(100*bank.DamageMultiplier(siege),armor);
        float nativeSpent=Math.Min(damage,nativeBarrier);damage-=nativeSpent;
        float reduction=bank.ShieldReduction(siege);
        damage=aegis.Absorb(damage,seraph.Charge>0?reduction:0);
        damage=seraph.Absorb(damage,reduction);
        Near(nativeSpent,expectedNative,name+" native barrier spent");
        Near(aegisCharge-aegis.Charge,expectedAegis,name+" Aegis spent");
        Near(shieldCharge-seraph.Charge,expectedShield,name+" Seraph spent");
        Near(damage,expectedHealth,name+" damage reaching health");
    }
    internal static void Run(string root)
    {
        Hit("mobile shield",false,0,0,false,0,0,100,0,0,60,0);
        Hit("siege shield",true,0,0,false,0,0,100,0,0,60,0);
        Hit("Aegis plus Seraph",true,0,6,false,0,0,100,0,0,39,0);
        Hit("outer Aegis inherits active Seraph",true,0,6,false,0,100,100,0,39,0,0);
        Hit("20 armor and shield",true,20,0,false,0,0,100,0,0,50,0);
        Hit("20 armor with Aegis and Seraph",true,20,6,false,0,100,100,0,32.5f,0,0);
        Hit("Hardening with Aegis and Seraph",true,0,6,true,0,100,100,0,33.6f,0,0);
        Hit("Hardening without charged Seraph",true,0,6,true,0,100,0,0,60,0,0);
        Hit("Seraph breaks",true,0,0,false,0,0,20,0,0,20,66.66667f);
        Hit("both defenses break",true,0,6,false,0,10,20,0,10,20,15);
        Hit("Aegis overflow is not reduced twice",true,0,6,false,0,20,20,0,20,19,0);
        Hit("one Seraph point does not protect health",true,0,6,false,0,10,1,0,10,1,46.66667f);
        Hit("broken Seraph stops fortifying Aegis",true,0,6,false,0,40,0,0,40,0,25);
        Hit("unshielded Aegis buff",true,0,6,false,0,0,0,0,0,0,65);
        Hit("Aegis buff expired, Seraph still charged",true,0,0,false,0,40,50,0,40,20,0);
        Hit("item barrier never inherits Seraph",true,0,6,false,80,40,50,65,0,0,0);
        Hit("item barrier overflows into Aegis",true,0,6,false,10,40,50,10,33,0,0);
        Hit("packing keeps Seraph protection on remaining Aegis",false,0,6,false,0,100,50,0,60,0,0);

        // Buy the actual ranked path. Only full Hardening grants the resized
        // four-point bonus; shield capacity and purchase costs remain unchanged.
        var ranked=new BastionBank{Fabrication=99999};
        for(int rank=0;rank<3;rank++)Check(ranked.Buy(100+(int)BastionUpgradeNode.ShieldMatrix,0,true),"Shield Matrix prerequisite purchase");
        foreach(bool siege in new[]{false,true})Near(ranked.ShieldReduction(siege),.4f,"capacity ranks do not add DR");
        for(int rank=1;rank<=3;rank++)
        {
            Check(ranked.Buy(100+(int)BastionUpgradeNode.Hardening,0,true),"ranked Hardening purchase");
            foreach(bool siege in new[]{false,true})Near(ranked.ShieldReduction(siege),rank==3?.44f:.4f,"same Hardening timing and relative bonus in both forms");
        }
        Near(ranked.AegisReduction,.4f,"Hardening Aegis DR unchanged");
        Near(IvoryBastion.BastionBalance.Get(BastionSetting.HardeningBonus)/IvoryBastion.BastionBalance.Get(BastionSetting.MobileShieldDR),.1f,"Hardening retains its ten-percent relative bonus in mobile");
        Near(IvoryBastion.BastionBalance.Get(BastionSetting.HardeningBonus)/IvoryBastion.BastionBalance.Get(BastionSetting.SiegeShieldDR),.1f,"Hardening retains its ten-percent relative bonus in siege");
        Near(ranked.Fabrication,99234,"shield path still costs 765 Fabrication");

        var pool=new BastionAegisReserve();var random=new Random(2231);
        for(int i=0;i<2000;i++)
        {
            float cap=(float)random.NextDouble()*2000,charge=(float)random.NextDouble()*cap,
                damage=(float)random.NextDouble()*10000,reduction=(float)random.NextDouble()*.95f;
            pool.Receive(cap,charge);float remaining=pool.Absorb(damage,reduction);
            Check(pool.Charge>=0&&pool.Charge<=cap&&remaining>=0&&remaining<=damage,"fortified Aegis stays within resource bounds");
            Near((damage-remaining)*(1-reduction),charge-pool.Charge,"fortified Aegis spends exactly protected damage, including overflow");
        }
        pool.Receive(50,50);Near(pool.Absorb(100,float.NaN),50,"invalid reduction safely gives no bonus");
        pool.Receive(50,50);Near(pool.Absorb(100,1),0,"reduction cap prevents division by zero");Near(pool.Charge,45,"maximum safe protection spends finite points");

        // Max-health changes update capacity and grant only its added points.
        // No ordinary stat refresh can refill damage already taken.
        foreach(bool siege in new[]{false,true})foreach(float upgrade in new[]{1f,1.2f,1.4f})
        {
            var seraph=new BastionShieldReserve();float original=BastionDefenseRules.ShieldCapacity(100,siege,upgrade);
            seraph.Resize(original);seraph.Absorb(10,.5f);
            seraph.Resize(BastionDefenseRules.ShieldCapacity(200,siege,upgrade));
            Near(seraph.Capacity,original*2,"doubling final max HP doubles capacity in either form and every defense rank");
            Near(seraph.Charge,original*2-5,"HP pickup adds only new capacity, retaining existing shield damage");
            seraph.Resize(seraph.Capacity);Near(seraph.Charge,original*2-5,"per-frame capacity observation cannot refill a damaged shield");
            seraph.Resize(original);Near(seraph.Charge,original,"shrinking HP safely clamps capacity and charge");
            float lateBonusHp=200*BastionDefenseRules.HealthConversionRatio(100,100);
            Near(BastionDefenseRules.ShieldCapacity(lateBonusHp,siege,upgrade),original*2,"late health multipliers are not missed");
            float convertedHp=2*BastionDefenseRules.HealthConversionRatio(100,1);
            Near(BastionDefenseRules.ShieldCapacity(convertedHp,siege,upgrade),original*2,"Transcendence retains natural health scaling including late bonuses");
            Near(BastionDefenseRules.ShieldCapacity(50*BastionDefenseRules.HealthConversionRatio(100,100),siege,upgrade),original*.5f,"final curse reduction applies once");
        }

        using var mod=AssemblyDefinition.ReadAssembly(Path.Combine(root,"bin/Release/netstandard2.1/IvoryBastion.dll"));
        var ability=mod.MainModule.Types.Single(t=>t.Name=="BastionAbilityShield");
        var instructions=ability.Methods.Single(m=>m.Name=="Absorb").Body.Instructions;
        int aegis=instructions.ToList().FindIndex(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="BastionAegisReserve"&&m.Name=="Absorb"&&m.Parameters.Count==2);
        int chargeLoad=instructions.Take(Math.Max(0,aegis)).ToList().FindLastIndex(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="BastionShieldReserve"&&m.Name=="get_Charge");
        Check(aegis>=0&&chargeLoad>=0,"shipped absorption checks Seraph charge before passing protection into Aegis");
        Check(instructions.Skip(chargeLoad+1).Take(aegis-chargeLoad-1).Any(i=>i.OpCode.FlowControl==FlowControl.Cond_Branch),"shipped Aegis protection is conditional on Seraph charge");
        foreach(string method in new[]{"Recalculate","Tick"})
            Check(ability.Methods.Single(m=>m.Name==method).Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="RefreshCapacity"),"final-stat and per-frame capacity refresh are both wired into the shipped mod");
        var refresh=ability.Methods.Single(m=>m.Name=="RefreshCapacity").Body.Instructions;
        Check(refresh.Any(i=>i.Operand is MethodReference m&&m.Name=="get_maxHealth"),"shipped capacity reads live final max health");
        Console.WriteLine($"PASS: {count:N0} Aegis/Seraph stacking, armor, overflow, max-HP scaling and compiled integration checks.");
    }
}
