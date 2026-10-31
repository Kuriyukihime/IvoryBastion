using System;
using System.IO;
using System.Linq;
using System.Reflection;
using IvoryBastion;
using UnityEngine;
static class Checks
{
    static int count;static void Assert(bool ok,string name){count++;if(!ok)throw new Exception(name);}static void Near(float a,float b,string name)=>Assert(Math.Abs(a-b)<.002,name+": "+a+" != "+b);
    static void Main(string[] args)
    {
        if(args.Length>0&&args[0]=="--poses"){PresentationChecks.Export(args[1],args[2]);return;}
        if(args.Length>0&&args[0]=="--config"){BalanceChecks.Run(Path.GetFullPath(args[1]));return;}
        PresentationChecks.Run();
        NativeContractChecks.Run();
        LaserCameraChecks.Run(Path.GetFullPath(args[0]));
        NeedleChecks.Run(Path.GetFullPath(args[0]));
        ProductionChecks.Run();
TransformationChecks.Run();
        ShieldChecks.Run(Path.GetFullPath(args[0]));
        DefenseStackingChecks.Run(Path.GetFullPath(args[0]));
        DefenseAndDroneChecks();
        UnitRoleChecks.Run(Path.GetFullPath(args[0]));
        WarheadChecks.Run();
        ExplosionChecks.Run(Path.GetFullPath(args[0]));
        CombatVisualChecks.Run(Path.GetFullPath(args[0]));
        BalanceChecks.Run(Path.GetFullPath(args[0]));
        DamageBudgetChecks.Run(Path.GetFullPath(args[0]));
        UpgradeDamageBudgetChecks.Run(Path.GetFullPath(args[0]));
        KitBalanceChecks.Run(Path.GetFullPath(args[0]));
        PerformanceChecks.Run(Path.GetFullPath(args[0]));
        var b=new BastionBank();Near(b.Ammo,30,"starting missiles");b.Tick(6,true,false,productionSeconds:6);Near(b.Ammo,30,"siege refill capped at new base capacity");Near(b.Fabrication,1.7f,"idle production");b.Tick(4,false,true,productionSeconds:4);Near(b.Ammo,30,"mobile reload capped");Near(b.Fabrication,1.7f,"mobile cannot fabricate");
        b.Ammo=0;b.DefenseTime=6;b.Tick(7,true,true,productionSeconds:7);Near(b.Ammo,13,"overcharge boundary integration");Near(b.Fabrication,6.6f,"combat income");Near(b.DefenseTime,0,"Aegis ends");
        b.Ammo=0;b.DefenseTime=6;b.Ranks[0]=2;b.Tick(7,true,false);Near(b.Ammo,19.5f,"upgraded doubled reload");
        b.Ammo=44.5f;b.Reserved=0;Assert(b.ReserveBarrage()==44,"integer reservation");b.Tick(8,true,false);Near(b.Ammo,1,"reserve occupies magazine");Assert(b.SpendMissile(true,out var heavy),"reserved shot");Assert(!heavy,"first standard missile");b.RefundBarrage();Near(b.Ammo,44,"refund only unlaunched missiles");Assert(b.Reserved==0,"reservation clear");
        b=new BastionBank{Ammo=30};for(int i=1;i<=24;i++){Assert(b.SpendMissile(false,out heavy),"normal spend");Assert(heavy==(i%8==0),"normal heavy cadence");}Near(b.Ammo,6,"heavy costs one missile");b.Ammo=20;b.ReserveBarrage();for(int i=1;i<=10;i++){b.SpendMissile(true,out heavy);Assert(heavy==(i%5==0),"barrage heavy cadence");}
        foreach(int choice in new[]{0,1})
        {
            var cadence=new BastionBank{Ammo=30};cadence.Ranks[0]=3;cadence.Choices[0]=choice;
            for(int i=1;i<=24;i++){Assert(cadence.SpendMissile(false,out heavy),"specialized missile spend");Assert(heavy==(i%(choice==0?6:8)==0),"warhead specialization improves cadence; laser specialization preserves base");}
            Near(cadence.Ammo,6,"specialized heavy costs one missile");
        }
        var pendingHeavy=new BastionBank{Ammo=.5f,MissileSequence=7};Assert(!pendingHeavy.SpendMissile(false,out heavy),"insufficient ammo cannot fire the pending eighth heavy shot");Assert(pendingHeavy.MissileSequence==7,"dry fire preserves heavy progress");pendingHeavy.Ammo=1;Assert(pendingHeavy.SpendMissile(false,out heavy)&&heavy,"reload preserves the pending eighth heavy shot");
        b=new BastionBank{Fabrication=1000};Assert(!b.Buy(0,0,false),"mobile upgrade denied");Assert(!b.Buy(-1,0,true),"invalid branch denied");Assert(!b.Buy(0,5,true),"invalid choice denied");for(int i=0;i<3;i++)Assert(b.Buy(0,1,true),"rank purchase");Near(b.Fabrication,1000-BastionBank.Cost(0)-BastionBank.Cost(1)-BastionBank.Cost(2),"legacy rank costs");Near(b.LaserDamage,9,"laser specialization");Near(b.LaserInterval,4f,"laser recharge specialization");Assert(!b.Buy(0,0,true),"cannot rebuy max rank");
        Assert(!b.SetUnit(1,1,true),"locked slot denied");b.Ranks[2]=1;Assert(!b.SetUnit(0,1,true),"locked blueprint denied");Assert(b.SetUnit(1,0,true),"second guardian allowed");b.Ranks[2]=2;Assert(b.SetUnit(0,2,true),"default slot accepts interceptor blueprint");Assert(!b.SetUnit(2,2,true),"third slot still locked");b.Ranks[2]=3;Assert(b.SetUnit(2,3,true),"third slot accepts repair blueprint");
        var f=new BastionThrusterReserve();f.Tick(1,true,true,2,0);Near(f.Fuel,2,"boost drains one second per second");Assert(f.Active,"boost active");
        f.Tick(.10f,false,true,2,0);Near(f.Fuel,2,"release recovery delay");f.Tick(.65f,false,true,2,0);Near(f.Fuel,2.75f,"partial delay exact boundary then refill");f.Tick(1f/6,false,true,2,0);Near(f.Fuel,3,"base reserve refills at capacity per two seconds");Assert(!f.Active,"release ends boost");
        f.Tick(3,true,true,2,0);Near(f.Fuel,0,"full three-second reserve");Assert(f.Exhausted&&!f.Active,"depletion ends boost");f.Tick(2.25f,true,true,2,0);Near(f.Fuel,3,"depleted reserve can recharge");Assert(!f.Active&&!f.CanStart,"held empty key cannot stutter restart");f.Tick(.02f,false,true,2,0);Assert(f.CanStart,"release rearms");
        f.Tick(1,true,false,2,0);Near(f.Fuel,3,"siege/deployment cannot spend fuel");Assert(!f.Active,"siege/deployment blocks boost");
        f.Fuel=0;f.Recovery=.25f;f.Tick(1.25f,false,true,2,8);Near(f.Fuel,3,"overdrive doubles recovery");f.Fuel=0;f.Recovery=.25f;f.Tick(.75f,false,true,2,.5f);Near(f.Fuel,1.125f,"overdrive expires during recharge tick");
        f.Fuel=2;f.Resize(5);Near(f.Fuel,2,"capacity upgrade does not create fuel");f.Tick(2,false,true,2,0);Near(f.Fuel,5,"larger reserve fills in same two seconds");f.Resize(3);Near(f.Fuel,3,"capacity reduction clamps fuel");f.Tick(float.NaN,true,true,2,0);Near(f.Fuel,3,"invalid frame time ignored");
        b=new BastionBank{RedlineTime=8};b.Tick(2,true,false);Near(b.RedlineTime,6,"overdrive clock continues in siege");Near(b.DamageMultiplier(true,50),.6f,"base siege shield resistance");b.DefenseTime=6;Near(b.DamageMultiplier(true,50),.39f,"Aegis multiplicative resistance");b.Ranks[1]=3;Near(b.DamageMultiplier(true,50),.336f,"upgraded combined resistance");Near(b.DamageMultiplier(false),1,"unshielded mobile takes full damage");
        Near(BastionBank.PredictedDamage(100,100),50,"armor reservation");Near(BastionBank.PredictedDamage(100,-100),150,"negative armor reservation");b.Tick(float.NaN,true,true);Assert(!float.IsNaN(b.Ammo),"reject invalid dt");
        foreach(float angle in new[]{-10f,0f,10f}){HudPlacement.AboveSkills(new HudPlacement.Box(-960,-540,960,540),new HudPlacement.Box(530,-510,910,-370),370,125,angle,out float x,out float y);var a=HudPlacement.Bounds(x,y,370,125,angle);Assert(a.Bottom>-370,"HUD clears slanted skills");Assert(a.Right<=948,"HUD stays in viewport");}
        b=new BastionBank();b.Capacities(1,1,18,30,45);Assert(b.Thruster.Capacity==4&&b.ProtocolCapacity==2&&b.SpecialCapacity==2,"item stock increases fuel and siege/special charges");Near(b.Thruster.Fuel,3,"extra tank capacity starts empty");b.Tick(6,false,false);Assert(b.ProtocolCharges==1,"bonus Aegis charge still recharging");
        b.Thruster.Fuel=0;b.Advance(BastionAction.Boost,1,2);Near(b.Thruster.Fuel,2,"external recharge adds fuel");b.Restock(BastionAction.Boost);Near(b.Thruster.Fuel,3,"single restock adds one second of fuel");b.Restock(BastionAction.Boost,true);Near(b.Thruster.Fuel,4,"native full reset fills tank");
        b.Spend(BastionAction.Protocol,18);Assert(b.ProtocolCharges==0,"Aegis spends its own charge");Near(b.Thruster.Fuel,4,"Aegis does not spend thruster fuel");b.Restock(BastionAction.Protocol,true);Assert(b.ProtocolCharges==2,"full native Aegis reset");Near(b.Ammo,30,"restock does not create missiles");
        b.Capacities(0,0,18,30,45);Assert(b.Thruster.Capacity==3&&b.ProtocolCharges==1,"losing items clamps independent pools");Near(b.Thruster.Fuel,3,"losing extra stock clamps fuel");b.Ranks[3]=2;b.Capacities(0,0,18,30,45);Assert(b.Thruster.Capacity==5,"Thruster Tank upgrade grants five seconds");Near(b.Thruster.Fuel,3,"purchasing Thruster Tank preserves existing fuel");Near(BastionBank.RechargeSeconds(6,.75f,2),2.5f,"flat and percent cooldown reductions combine");Near(BastionBank.RechargeSeconds(6,.1f,10),.5f,"native minimum recharge");
        var progression=new BastionBank();progression.Tick(180,true,true,productionSeconds:180);progression.Tick(120,true,false,productionSeconds:120);Near(progression.Fabrication,160,"five-minute stage production budget");
        progression.Fabrication+=50*BastionBank.KillFabrication(false,false)+3*BastionBank.KillFabrication(false,true)+BastionBank.KillFabrication(true,true);Near(progression.Fabrication,177.25f,"sample stage including kills");
        progression.Upgrades.Active=true;
        for(int point=0;point<3;point++)Assert(progression.Buy(100,0,true),"sample stage completes one focused core node");
        Assert(!progression.Buy(101,0,true),"sample stage cannot rush the advanced missile path");
        Assert(progression.Buy(108,0,true)&&progression.Buy(108,0,true),"sample stage can invest two defensive points");Assert(!progression.Buy(108,0,true),"sample stage cannot max a second core node");
        progression.Tick(120,false,false);Assert(progression.Upgrades.Points[0]==3&&progression.Upgrades.Points[8]==2,"purchased points persist in mobile");
        Near(BastionBank.MissileRadius(false),5,"standard blast radius");Near(BastionBank.MissileRadius(true),10,"heavy blast radius");
        var mobile=new BastionBank{Ammo=0};mobile.LoadNeedleHit(false);Near(mobile.Ammo,0,"misses and invulnerable hits cannot load missiles");
        for(int i=0;i<60;i++)mobile.LoadNeedleHit(true);
        Near(mobile.Ammo,30,"ten seconds of landed base-speed fire loads thirty missiles");
        Near(mobile.Fabrication,0,"mobile hits do not accelerate upgrade currency");
        mobile.ReserveBarrage();for(int i=0;i<200;i++)mobile.LoadNeedleHit(true);
        Near(mobile.Ammo+mobile.Reserved,30,"hit loading respects reserved magazine capacity");
        Near(mobile.LoadNeedleHit(true),0,"full magazine rejects extra hit credit");
        mobile.RefundBarrage();Near(mobile.Ammo,30,"refund cannot duplicate hit-generated ammo");
        mobile.Ranks[0]=1;Near(mobile.LoadNeedleHit(true),.5f,"hit loading works with upgraded capacity");
        GeometryChecks();
        RepairAndVisualChecks();
        string root=Path.GetFullPath(args[0]);foreach(var path in Directory.GetFiles(Path.Combine(root,"assets"),"*.wav")){using var stream=File.OpenRead(path);var clip=BastionPcmClip.Read(stream);Assert(clip.Rate==48000,"PCM sample rate");Assert(clip.Samples.All(x=>float.IsFinite(x)&&Math.Abs(x)<1),"PCM headroom");}
        var assembly=Assembly.LoadFile(Path.Combine(root,"bin","Release","netstandard2.1","IvoryBastion.dll"));var resources=assembly.GetManifestResourceNames();foreach(var file in Directory.GetFiles(Path.Combine(root,"assets")).Where(p=>new[]{".png",".meshbin",".wav"}.Contains(Path.GetExtension(p))))Assert(resources.Contains("IvoryBastion.assets."+Path.GetFileName(file)),"embedded "+file);
        Console.WriteLine($"PASS: {count} bank, healing, aura sizing, hover, guidance/deployment, cooldown, UI geometry, PCM and embedded-resource assertions.");
    }
    static void DefenseAndDroneChecks()
    {
        Near(BastionDefenseRules.ShieldCapacity(100,false),15,"base flight shield has fifteen health");
        Near(BastionDefenseRules.ShieldCapacity(400,false),60,"flight shield scales with health");
        Near(BastionDefenseRules.ShieldCapacity(400,true),200,"siege shield is half max health");
        var b=new BastionBank();
        Near(b.DamageMultiplier(false,15),.6f,"shielded mobile reduces damage by forty percent");
        Near(b.DamageMultiplier(false,.01f),.6f,"any remaining shield protects the breaking hit");
        Near(b.DamageMultiplier(false,0),1,"depleted shield immediately loses protection");
        Near(b.DamageMultiplier(true,15),.6f,"siege ability shield reduction");
        b.DefenseTime=6;Near(b.DamageMultiplier(true,15),.39f,"Aegis stacks with shield");
        b.Toggle=BastionDefenseRules.ModeSwitchCooldown;b.Tick(5.99f,false,false);
        Assert(b.Stock(BastionAction.Transform)==0,"cannot switch before six seconds");
        b.Tick(.02f,false,false);Assert(b.Stock(BastionAction.Transform)==1,"can switch after six seconds");
        Assert(BastionDefenseRules.CanDeploy(6,true,1),"six-metre boundary is allowed");
        Assert(!BastionDefenseRules.CanDeploy(6.01f,true,1),"above six metres is denied");
        Assert(!BastionDefenseRules.CanDeploy(1,false,1),"no solid floor is denied");
        Assert(!BastionDefenseRules.CanDeploy(1,true,0),"vertical walls are not valid ground");
        Assert(BastionDefenseRules.CanDeploy(2,true,.5f),"slopes permit low deployment");
        var units=new BastionBank();Assert(units.Loadout.Length==3,"all three slots have persistent loadouts");
        Assert(!units.SetUnit(0,1,true),"first slot blueprints remain locked before Legion II");
        units.Ranks[2]=2;
        for(int slot=0;slot<2;slot++)for(int kind=0;kind<4;kind++)
        {Assert(units.SetUnit(slot,kind,true),"each unlocked slot accepts every drone type");Assert(units.Loadout[slot]==kind,"drone choice stored for the requested slot");}
        Assert(!units.SetUnit(2,1,true),"Legion II cannot configure locked third slot");
        units.Ranks[2]=3;
        for(int kind=0;kind<4;kind++)Assert(units.SetUnit(2,kind,true),"Legion III configures third slot");
        Assert(!units.SetUnit(0,4,true)&&!units.SetUnit(-1,0,true)&&!units.SetUnit(3,0,true),"invalid blueprints and slots rejected");
        Assert(!units.SetUnit(0,0,false),"drone configuration still requires siege");
    }
    static void RepairAndVisualChecks()
    {
        foreach(float dt in new[]{.02f,1f/60,.07f})foreach(float fraction in new[]{.18f,.30f})
        {
            var repair=new BastionRepair();double age=0;float total=0;int pulses=0;
            while(age<6-1e-7)
            {
                float step=(float)Math.Min(dt,6-age);
                float healed=repair.Tick(step,true,1000,0,false,(float)(6-age),fraction);
                if(healed>0)pulses++;total+=healed;age+=step;
            }
            total+=repair.Flush();Near(total,1000*fraction,"Aegis healing total survives batching and the final partial tick");
            Assert(pulses<=24,"six-second Aegis cannot emit physics-frame healing spam");
        }
        var passive=new BastionRepair();float sum=0;
        for(int i=0;i<400;i++)sum+=passive.Tick(.02f,true,1000,(i+1)*.02f,false,0,.18f);
        sum+=passive.Flush();Near(sum,50,"passive repair waits three seconds after damage and heals five seconds of an eight-second window");
        var stacked=new BastionRepair();sum=0;
        for(int i=0;i<300;i++)sum+=stacked.Tick(.02f,true,1000,100,false,6-i*.02f,.18f);
        sum+=stacked.Flush();Near(sum,240,"passive and Aegis healing add without changing their rates");
        var combat=new BastionRepair();sum=0;
        for(int i=0;i<50;i++)sum+=combat.Tick(.02f,true,1000,0,true,0,.18f);
        sum+=combat.Flush();Near(sum,10,"combat-repair upgrade keeps one percent per second");
        var partial=new BastionRepair();Near(partial.Tick(.12f,true,100,100,false,6,.18f),0,"partial pulse waits");
        Near(partial.Tick(.02f,false,100,100,false,6,.18f),.48f,"packing flushes only healing already earned");
        Near(partial.Tick(10,false,100,100,false,6,.18f),0,"mobile mode earns no siege healing");
        Near(partial.Flush(),0,"a pulse cannot be paid twice");
        Near(new BastionRepair().Flush(),0,"a new body or stage has no stored healing");
        Near(partial.Tick(float.NaN,true,100,100,false,6,.18f),0,"invalid elapsed time cannot create healing");
        float effect=10000;
        for(int stage=0;stage<100;stage++)
        {
            effect=BastionVisualRules.SelfEffectRadius(effect,false,1);
            Near(effect,1.35f,"repeated stage refresh cannot enlarge the mobile aura");
            effect=BastionVisualRules.SelfEffectRadius(10000,true,1);
            Near(effect,4,"siege self effects stay within the current form size");
        }
        Near(BastionVisualRules.SelfEffectRadius(.23f,false,1),.23f,"small authored effect radius is preserved");
        Near(BastionVisualRules.SelfEffectRadius(2.7f,false,2),2.7f,"intentional item-driven body scaling is preserved once");
        Near(BastionVisualRules.SelfEffectRadius(float.NaN,false,1),1.35f,"invalid effect radius has a finite fallback");
        Near(BastionVisualRules.SelfEffectRadius(4,true,float.PositiveInfinity),4,"invalid scale cannot enlarge an aura");
        Near(BastionVisualRules.HoverStrength(.14f,true),0,"normal grounded deployment does not fire lift jets");
        Near(BastionVisualRules.HoverStrength(1,true),1,"airborne siege has full lift jets");
        Near(BastionVisualRules.HoverStrength(0,false),1,"deployment over a deep void still has lift jets");
        float lift=BastionVisualRules.HoverStrength(.5f,true);Assert(lift>0&&lift<1,"low hovering blends lift strength");
    }
    static void GeometryChecks()
    {
        // Exercise the exact managed guidance used by server and client visuals,
        // with stationary, crossing, retreating and very close targets.
        foreach(bool heavy in new[]{false,true})foreach(float dt in new[]{.02f,1f/30,1f/144})
        {
            Vector3 position=Vector3.zero,target=new Vector3(90,0,0);
            for(float age=0;age<3;age+=dt)position=BastionFlightGeometry.Step(position,target,heavy,dt);
            Near(Vector3.Distance(position,target),0,"long-range missile reaches its target");
            position=Vector3.zero;bool hit=false;
            for(float age=0;age<6;age+=dt)
            {
                target=new Vector3(50+age*10,Mathf.Sin(age*2)*4,age*18);
                var next=BastionFlightGeometry.Step(position,target,heavy,dt);
                if(BastionFlightGeometry.CrossesTarget(position,next,target,.9f)){hit=true;break;}
                position=next;
            }
            Assert(hit,"homing catches a moving target before expiry");
            target=new Vector3(-.2f,.1f,0);
            position=BastionFlightGeometry.Step(Vector3.zero,target,heavy,.2f);
            Near(Vector3.Distance(position,target),0,"nearby target cannot be overshot or orbited");
        }
        Assert(BastionFlightGeometry.CrossesTarget(Vector3.zero,Vector3.right*5,Vector3.right*2,.1f),"swept fuse detects crossing between frames");
        Assert(!BastionFlightGeometry.CrossesTarget(Vector3.zero,Vector3.right*5,new Vector3(2,2,0),.9f),"swept fuse rejects a genuine miss");
        bool Ground(Vector3 p)=>BastionDefenseRules.CanDeploy(p.y,true,1);
        Assert(!BastionFlightGeometry.FindAnchor(new Vector3(0,40,0),Vector3.forward,_=>true,out var anchor,Ground),"high-altitude deployment rejected");
        Assert(BastionFlightGeometry.FindAnchor(new Vector3(0,5,0),Vector3.forward,_=>true,out anchor,Ground),"low hover can deploy");
        Near(anchor.y,7.08f,"low hover keeps current footprint");
        Assert(BastionFlightGeometry.FindAnchor(Vector3.zero,Vector3.forward,p=>p.y>=4.2f,out anchor,Ground),"lift above uneven terrain");
        Assert(anchor.y>=4.2f&&anchor.y-2<=6,"terrain adjustment respects final ground clearance");
        Assert(!BastionFlightGeometry.FindAnchor(new Vector3(0,5,0),Vector3.forward,p=>p.y>8,out anchor,Ground),"clearance lift cannot evade altitude cap");
        Assert(!BastionFlightGeometry.FindAnchor(Vector3.zero,Vector3.forward,_=>true,out anchor,_=>false),"void without solid ground rejects deployment");
        Assert(BastionFlightGeometry.FindAnchor(Vector3.zero,Vector3.forward,p=>p.z < -1.5f&&p.y<3,out anchor,Ground),"step back from a wall when vertical space is blocked");
        Assert(anchor.z < -1.5f,"lateral adjustment favors away from facing");
        Assert(!BastionFlightGeometry.FindAnchor(Vector3.zero,Vector3.forward,_=>false,out anchor,Ground),"sealed geometry still rejects deployment");
    }
}
