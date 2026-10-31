using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using IvoryBastion;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
using Mono.Cecil;
static class Program
{
    static int checks;static uint ids,mineIds;
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    static void Check(bool value,string why){checks++;if(!value)throw new Exception("Utility: "+why);}
    static void Near(float a,float b,string why)=>Check(Math.Abs(a-b)<.003f,why+" ("+a+" / "+b+")");
    static void Invoke(object o,string name)=>o.GetType().GetMethod(name,Private).Invoke(o,null);
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,Private).SetValue(o,value);
    static T Get<T>(object o,string name)=>(T)o.GetType().GetField(name,Private).GetValue(o);
    static List<BastionServerSpiderMine> Mines=>(List<BastionServerSpiderMine>)typeof(BastionServerSpiderMine).GetField("live",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
    static (BastionController C,BastionUtility U) Actor(bool authority=true,bool siege=false)
    {
        var go=new GameObject();var b=go.AddComponent<CharacterBody>();b.netId=new(){Value=++ids};b.hasEffectiveAuthority=authority;b.healthComponent.body=b;
        var c=go.AddComponent<BastionController>();c.Body=b;c.Sieged=siege;var u=go.AddComponent<BastionUtility>();Invoke(u,"Awake");Util.Objects[b.netId.Value]=go;return(c,u);
    }
    static CharacterBody Enemy(float z,int rank=0,float x=0)
    {
        var b=new GameObject().AddComponent<CharacterBody>();b.Team=1;b.netId=new(){Value=++ids};b.corePosition=new(x,0,z);b.transform.position=b.corePosition;b.healthComponent.body=b;
        b.mainHurtBox=b.gameObject.AddComponent<HurtBox>();b.mainHurtBox.healthComponent=b.healthComponent;
        b.isBoss=rank==3;b.isElite=rank==2;b.isFlying=rank==1;CharacterBody.readOnlyInstancesList.Add(b);return b;
    }
    static void Reset()
    {
        foreach(var m in Mines.ToArray())UnityEngine.Object.Destroy(m.gameObject);Mines.Clear();NetworkServer.active=true;Time.time=Time.unscaledTime=0;Time.fixedDeltaTime=.02f;Physics.Floor=true;Physics.RadiusAwareFlight=false;Physics.Wall=false;Physics.FloorSlope=Physics.FloorHeight=Physics.StepHeight=0;Physics.StepX=float.PositiveInfinity;
        CharacterBody.readOnlyInstancesList.Clear();BastionServerMicroMissile.Shots.Clear();BlastAttack.Fired.Clear();Util.Objects.Clear();BastionBalance.Reset();
    }
    static void Tick(BastionUtility u,float dt=.02f){Time.fixedDeltaTime=dt;Time.time+=dt;Time.unscaledTime+=dt;Invoke(u,"FixedUpdate");}
    static void StepMines(float seconds,float dt=.02f)
    {for(float t=0;t<seconds-.0001f;t+=dt){Time.fixedDeltaTime=dt;Time.time+=dt;foreach(var m in Mines.ToArray())if(m)Invoke(m,"FixedUpdate");}}
    static BastionServerSpiderMine Spawn(BastionController c,Vector3 center=default)
    {BastionServerSpiderMine.Launch(c,++mineIds,0,12,center,center+Vector3.up);return Mines.Last();}
    static void Place(BastionServerSpiderMine m,BastionMinePhase phase,Vector3 p)
    {m.transform.position=p;Set(m,"phase",phase);Set(m,"age",2f);Set(m,"phaseAge",0f);Set(m,"groundAge",1f);Set(m,"landing",p);}
    static BastionUtilityStatus Copy(BastionUtilityStatus s){var w=new NetworkWriter();s.Serialize(w);var r=new BastionUtilityStatus();r.Deserialize(new NetworkReader(w.Bytes()));return r;}
    static void Main(string[] args)
    {
        if(args.Length==2&&args[0]=="--mine-field-layout")
        {
            var fields=new[]{0,2}.Select(rank=>
            {
                var bank=new BastionBank();bank.Ranks[0]=rank;
                int count=BastionUtilityUpgrades.MineCount(bank);float radius=BastionUtilityUpgrades.MineFieldRadius(bank);
                return new{Name=rank==0?"Base field":"Ordnance II",Count=count,Radius=radius,Blast=BastionUtilityUpgrades.MineBlastRadius(bank),Points=Enumerable.Range(0,count).Select(i=>{var p=BastionMineRules.LandingOffset(i,count,radius,8);return new{X=p.x,Z=p.z,Ring=i%3+1};}).ToArray()};
            }).ToArray();
            File.WriteAllText(args[1],System.Text.Json.JsonSerializer.Serialize(fields,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));return;
        }
        if(args.Length==2&&args[0]=="--mine-presentation")
        {
            var samples=Enumerable.Range(0,201).Select(i=>new{Open=i/200f,Hip=BastionMinePresentation.Hip(i/200f),Knee=BastionMinePresentation.Knee(i/200f)});
            var data=new{BastionMinePresentation.UnfoldSpeed,BastionMinePresentation.LauncherBoundsExpansion,BaseDeploySpan=BastionMineRules.DeployWindup+BastionBalance.Int(BastionSetting.MineCount)*BastionBalance.Get(BastionSetting.MineLaunchInterval),HatchLeft=BastionMinePresentation.HatchAngle(-1,1),HatchRight=BastionMinePresentation.HatchAngle(1,1),Samples=samples};
            File.WriteAllText(args[1],System.Text.Json.JsonSerializer.Serialize(data,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));return;
        }
        EngineeringPauseChecks.Run();UpgradeChecks.Run();EngineeringLayoutChecks.Run(Path.GetFullPath(args[0]));ChainGeometry();MinePresentation();ReserveRules();CastsAndMovement();UpgradeIntegration();Targeting();BatteryTargeting();RushTargetingAndReservations();RushSpreadPlan();MineTerrainClearance();MineLifecycle();MineCapacity();ChainBursts();Snapshots();Dialogue();Display();Native(Path.GetFullPath(args[0]));
        Console.WriteLine($"PASS: {checks} utility, mine lifecycle, chain, dialogue and native integration checks.");
    }
    static void ChainGeometry()
    {
        foreach(float radius in new[]{.5f,8f,15f})for(float t=0;t<=1.001f;t+=.025f)for(int i=0;i<6;i++)for(int p=0;p<3;p++)
        foreach(var v in new[]{BastionMineChainGeometry.Chevron(i,p,t,radius),BastionMineChainGeometry.Spark(i,p,t,radius)})Check(float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)&&v.magnitude<=radius+.001f,"overcharge accents remain finite and inside physical blast radius");
        var a=Actor();Time.unscaledTime=1;a.U.ChainFeedback();Time.unscaledTime=1.2f;a.U.ChainFeedback();Check(a.U.ChainFeedbackCount==2,"burst confirmations count actual detonations");Time.unscaledTime=3;a.U.ChainFeedback();Check(a.U.ChainFeedbackCount==1,"new burst group resets old HUD count");
    }
    static void MinePresentation()
    {
        Near(BastionMinePresentation.Hip(0),-55,"folded hip tucks below the shell");Near(BastionMinePresentation.Knee(0),-100,"folded lower leg tucks inward");Near(BastionMinePresentation.Hip(1),0,"deployed hip rest is unchanged");Near(BastionMinePresentation.Knee(1),0,"deployed knee rest is unchanged");
        Near(BastionMinePresentation.HatchAngle(-1,1),76,"left outer hinge opens upward");Near(BastionMinePresentation.HatchAngle(1,1),-76,"right outer hinge opens upward");Near(BastionMinePresentation.HatchAngle(-1,0),0,"closed hatch pose is unchanged");Near(BastionMinePresentation.UnfoldSpeed,7,"complete unfolding time remains one seventh second");
        for(float opened=-.1f;opened<=1.101f;opened+=.005f)
        {
            float hip=BastionMinePresentation.Hip(opened),knee=BastionMinePresentation.Knee(opened);
            Check(float.IsFinite(hip)&&hip>=-55.001f&&hip<=18.001f&&float.IsFinite(knee)&&knee>=-100.001f&&knee<=.001f,"staged fold angles remain bounded and finite");
            if(opened>=0&&opened<=1)
            {
                Near(BastionMinePresentation.HatchAngle(-1,opened),-BastionMinePresentation.HatchAngle(1,opened),"mirrored lids keep matching opening angles");
                if(opened<.45f)Near(knee,-100,"hip lifts before knee opens");
                if(opened>.8f)Near(knee,0,"foot settles only after knee is extended");
            }
        }
    }
    static void ReserveRules()
    {
        Check(BastionUtilityRules.FinitePoint(Vector3.zero)&&!BastionUtilityRules.FinitePoint(new(float.NaN,0,0)),"world-origin endpoints valid, NaN rejected");
        var dash=new BastionUtilityReserve{Stock=2,Capacity=2,Interval=10};
        var mine=new BastionUtilityReserve{Interval=24};
        Check(dash.Spend(10)&&dash.Spend(10)&&!dash.Spend(10),"two base Rush charges, third blocked");
        Check(mine.Spend(24),"independent mine reserve spends one charge");
        dash.Tick(9.5f);mine.Tick(9.5f);Check(dash.Stock==0&&mine.Stock==0,"neither reserve recharges before its interval");
        dash.Tick(.5f);mine.Tick(.5f);Check(dash.Stock==1&&mine.Stock==0,"first Rush charge returns at ten seconds, not both");Near(dash.Remaining,10,"second Rush charge starts another full interval");
        dash.Tick(9.5f);mine.Tick(9.5f);Check(dash.Stock==1&&mine.Stock==0,"second charge does not recharge in parallel");
        dash.Tick(.5f);mine.Tick(.5f);Check(dash.Stock==2&&mine.Stock==0,"both Rush charges return sequentially by twenty seconds");Near(mine.Remaining,4,"mines retain their independent twenty-four-second interval");
        mine.Tick(4);Check(mine.Stock==1,"mine charge returns at twenty-four seconds");
        dash.Spend(10);dash.Spend(10);dash.Tick(5);dash.Reconfigure(20);Near(dash.Remaining,10,"cooldown changes preserve fractional recharge progress");
        dash.Resize(3);Check(dash.Stock==0&&dash.Capacity==3,"extra stock expands capacity without granting free charges");
        dash.Restock(false);Check(dash.Stock==1,"ordinary restock restores one charge");dash.Restock(true);Check(dash.Stock==3,"full restock respects expanded capacity");
        dash.Receive(900,-1,float.NaN,float.PositiveInfinity);Check(dash.Capacity==1&&dash.Stock==1&&float.IsFinite(dash.Remaining),"invalid snapshots bounded");
        foreach(float dt in new[]{.01f,.02f,1f/60,.07f,.19f}){float distance=0;for(float t=0;t<1.2f;t+=dt)distance+=BastionUtilityRules.TravelSpeed(t,dt,.12f,.6f,15)*dt;Near(distance,15,"dash distance independent of fixed step");}
        Near(BastionBalance.Get(BastionSetting.WingburstCooldown),10,"Rush default recharge per charge");
        Near(BastionBalance.Get(BastionSetting.WingburstDamage),40,"Rush default micro-missile damage percent");
        Near(BastionBalance.Get(BastionSetting.WingburstDistance),15,"Rush default dash range");
        Near(BastionBalance.Get(BastionSetting.WingburstRadius),4,"micro-missile explosion radius retained");
        Near(BastionBalance.Get(BastionSetting.WingburstRange),45,"micro-missile target range retained");
        Near(BastionBalance.Get(BastionSetting.WingburstProc),.1f,"micro-missile item scaling uses the 1.1.3 kit proc reduction");
        Check(BastionBalance.Int(BastionSetting.WingburstShots)==32,"Rush retains full thirty-two missile payload");
    }
    static void CastsAndMovement()
    {
        foreach(float dt in new[]{.02f,1f/60,.07f,.2f})
        {
            Reset();Enemy(30);var(c,u)=Actor();Check(u.TryCast(false,Vector3.zero,out _),"fallback aim cast");var motor=new CharacterMotor();float travelled=0;
            for(int i=0;i<Math.Ceiling(1.2/dt);i++){Tick(u,dt);if(u.Move(motor))travelled+=motor.velocity.magnitude*dt;}
            Check(BastionServerMicroMissile.Shots.Count==32,"exactly 32 missiles despite hitches");
            Check(BastionServerMicroMissile.Shots.Select(s=>s.Id).Distinct().Count()==32,"unique missile identities");Near(travelled,15,"accepted owner movement covers fifteen metres");
            Check(!u.Active&&u.WingburstReserve.Stock==1&&u.WingburstReserve.Capacity==2&&u.MinesReserve.Stock==1,"Rush spends one of two base charges without affecting mines");
            motor.velocity=Vector3.forward*7;Check(!u.Move(motor),"completed rush releases motor immediately");Near(motor.velocity.magnitude,7,"completed rush does not zero normal flight velocity");
        }
        Reset();var a=Actor();
        Check(a.U.WingburstReserve.Stock==2&&a.U.WingburstReserve.Capacity==2,"fresh fallback exposes both base charges before initialization");
        Check(a.U.TryCast(false,Vector3.forward,out _),"first base Rush cast accepted");Tick(a.U,1);
        Check(a.U.TryCast(false,Vector3.forward,out _),"second base Rush cast accepted");Tick(a.U,1);
        Check(!a.U.TryCast(false,Vector3.forward,out _)&&a.U.WingburstReserve.Stock==0,"third base Rush blocked after two accepted casts");
        Near(a.U.WingburstReserve.Remaining,8,"second cast does not restart first charge recharge");
        Tick(a.U,7.5f);Check(a.U.WingburstReserve.Stock==0,"production ledger remains empty before ten seconds");
        Tick(a.U,.5f);Check(a.U.WingburstReserve.Stock==1,"production ledger restores exactly one at ten seconds");
        Tick(a.U,9.5f);Check(a.U.WingburstReserve.Stock==1,"production ledger keeps second charge pending until twenty seconds");
        Tick(a.U,.5f);Check(a.U.WingburstReserve.Stock==2&&a.U.WingburstReserve.Capacity==2,"production ledger restores both by twenty seconds");
        Check(a.U.MinesReserve.Stock==1&&a.U.MinesReserve.Capacity==1,"Rush recharge never changes independent mine reserve");
        Reset();a=Actor();Check(a.U.TryCast(false,Vector3.forward,out _),"targetless mobility permitted");Tick(a.U,1);Check(BastionServerMicroMissile.Shots.Count==0,"no phantom targetless impacts");a.C.Sieged=true;Tick(a.U,9);Check(a.U.WingburstReserve.Stock==2,"Rush recharges while in siege");
        Physics.Floor=false;Check(!a.U.TryCast(true,Vector3.forward,out _)&&a.U.MinesReserve.Stock==1,"no ground preserves mine charge");Physics.Floor=true;Physics.RadiusAwareFlight=false;Physics.Wall=false;Physics.FloorSlope=Physics.FloorHeight=Physics.StepHeight=0;Physics.StepX=float.PositiveInfinity;
        Check(a.U.TryCast(true,Vector3.forward,out _),"mines deploy without enemies");a.C.Sieged=false;Tick(a.U,2);Check(BastionServerSpiderMine.Count(a.C)==24,"accepted mine deployment completes while packing");
        Check(a.U.WingburstReserve.Stock==2&&a.U.MinesReserve.Stock==0&&a.U.MinesReserve.Capacity==1,"mines consume only their independent single charge");Near(a.U.MinesReserve.Remaining,22,"mine recharge remains twenty-four seconds");
        Reset();a=Actor();a.C.Body.skillLocator.utility.bonusStockFromBody=1;Tick(a.U,.02f);
        Check(a.U.WingburstReserve.Capacity==3&&a.U.WingburstReserve.Stock==2,"extra-stock item adds one on top of two without granting free stock");
        Tick(a.U,10);Check(a.U.WingburstReserve.Stock==3,"legitimate extra stock recharges");
        for(int i=0;i<3;i++){Check(a.U.TryCast(false,Vector3.forward,out _),"each of three legitimate item-enhanced charges accepted");Tick(a.U,1);}
        Check(!a.U.TryCast(false,Vector3.forward,out _),"extra-stock item does not permit fourth cast");
        Reset();a=Actor();Enemy(30);Check(a.U.TryCast(false,Vector3.forward,out _),"form-change barrage accepted");Tick(a.U,.2f);int firstShots=BastionServerMicroMissile.Shots.Count;Check(firstShots>0&&firstShots<32,"form change occurs during active payload");
        a.C.Deploying=true;var releasedMotor=new CharacterMotor{velocity=Vector3.forward*7};Check(!a.U.Move(releasedMotor),"form change releases Rush movement");Near(releasedMotor.velocity.magnitude,7,"form change releases without hard stop");Tick(a.U,1);Check(BastionServerMicroMissile.Shots.Count==32&&!a.U.Active,"already-accepted micro-missiles finish through form change");
        NetworkServer.active=false;Check(!a.U.TryCast(false,Vector3.forward,out _),"client cannot authoritatively cast");
    }
    static void UpgradeIntegration()
    {
        foreach(int rank in new[]{0,1,2,3})foreach(int choice in new[]{0,1})
        {
            Reset();var a=Actor();a.C.Bank.Ranks[0]=a.C.Bank.Ranks[3]=rank;a.C.Bank.Choices[0]=choice;Enemy(30);
            Near(BastionUtilityUpgrades.RushDistance(a.C.Bank),rank>=1?18:15,"Airframe improves Rush travel only after purchase");
            Check(BastionUtilityUpgrades.RushShots(a.C.Bank)==(rank>=2?40:32),"Thruster Tank increases actual salvo size");Near(a.U.Cooldown,rank>=3?8:10,"Blue Shift rank changes per-charge Rush recharge");
            Check(a.U.TryCast(false,Vector3.forward,out _),"ranked Rush reaches host acceptance");
            var snapshot=Copy(new BastionUtilityStatus(a.U,1));Check(snapshot.Shots==(rank>=2?40:32),"accepted shot count survives native wire roundtrip");Near(snapshot.Distance,rank>=1?18:15,"accepted range survives wire roundtrip");
            var motor=new CharacterMotor();float travelled=0;for(int i=0;i<60;i++){Tick(a.U);if(a.U.Move(motor))travelled+=motor.velocity.magnitude*.02f;}
            Check(BastionServerMicroMissile.Shots.Count==(rank>=2?40:32),"host fires exactly the upgraded payload");Near(travelled,rank>=1?18:15,"authority motor uses rank-correct travel profile");
            a.C.Sieged=true;Check(a.U.TryCast(true,Vector3.forward,out _),"ranked Widow cast accepted");Near(a.U.CastFieldRadius,rank>=1?18:15,"Magazine expands the captured field");
            Check(a.U.CastShots==(rank>=2?30:24),"Assembly expands the captured mine count");Tick(a.U,3);
            Check(BastionServerSpiderMine.Count(a.C)==(rank>=2?30:24),"host deploys actual rank-correct mine count");
            var radii=Mines.Select(m=>Vector3.ProjectOnPlane(Get<Vector3>(m,"landing"),Vector3.up).magnitude).ToArray();
            for(int ring=1;ring<=3;ring++){float radius=(rank>=1?18:15)*ring/3f;Check(radii.Count(r=>Math.Abs(r-radius)<.01f)==(rank>=2?10:8),"each staggered ring has its complete share of mines");}
            Near(BastionUtilityUpgrades.MineDamage(a.C.Bank),rank>=3&&choice==0?1.05f:.9f,"only Warheads improve the charge coefficient");Near(BastionUtilityUpgrades.MineBlastRadius(a.C.Bank),rank>=3&&choice==1?10:8,"only Ion Lance widens the actual mine blast");
        }
        Reset();var host=Actor(false);host.C.Bank.Ranks[3]=3;Enemy(30);Check(host.U.TryCast(false,Vector3.forward,out _),"remote upgraded Rush accepted by host");var status=Copy(new BastionUtilityStatus(host.U,1));
        NetworkServer.active=false;var client=Actor();status.Owner=client.C.Body.netId;status.OnReceived();Check(client.C.Bank.Ranks[3]==0,"fixture delays the bank upgrade snapshot");
        var clientMotor=new CharacterMotor();float distance=0;for(int i=0;i<60;i++){Tick(client.U);if(client.U.Move(clientMotor))distance+=clientMotor.velocity.magnitude*.02f;}
        Near(distance,18,"accepted host movement profile wins even while client's local rank is stale");
        uint sequence=status.Sequence;foreach(var invalid in new[]{float.NaN,float.PositiveInfinity,-1f,81f}){status.Sequence++;status.CastId++;status.Distance=invalid;status.OnReceived();}
        Check(Get<uint>(client.U,"receivedSequence")==sequence,"malformed movement profiles cannot consume a snapshot sequence or restart movement");
        foreach(Action<BastionUtilityStatus> corrupt in new Action<BastionUtilityStatus>[]
        {p=>p.Shots=0,p=>p.Shots=65,p=>p.Windup=float.NaN,p=>p.Windup=-1,p=>p.Windup=1.1f,p=>p.Span=float.PositiveInfinity,p=>p.Span=0,p=>p.Span=33,p=>p.FieldRadius=float.NaN,p=>p.FieldRadius=-1,p=>p.FieldRadius=31})
        {
            var invalid=Copy(new BastionUtilityStatus(host.U,sequence+1));invalid.Owner=client.C.Body.netId;invalid.CastId++;corrupt(invalid);invalid.OnReceived();
            Check(Get<uint>(client.U,"receivedSequence")==sequence,"invalid accepted-cast field cannot consume a newer status sequence");
        }
        Reset();var a2=Actor();Enemy(30);Check(a2.U.TryCast(false,Vector3.forward,out _),"base cast before mid-salvo upgrade");Tick(a2.U,.2f);a2.C.Bank.Ranks[3]=3;var frozen=Copy(new BastionUtilityStatus(a2.U,1));
        Check(frozen.Shots==32&&frozen.Distance==15,"buying upgrades mid-cast does not mutate the already accepted payload");Tick(a2.U,1);Check(BastionServerMicroMissile.Shots.Count==32,"accepted base salvo finishes exactly once after rank change");
        Check(a2.U.TryCast(false,Vector3.forward,out _)&&a2.U.CastShots==40&&a2.U.CastDistance==18,"following cast uses the newly purchased upgrade");
        Reset();a2=Actor();a2.C.Sieged=true;Check(a2.U.TryCast(true,Vector3.forward,out _),"base mine cast before Assembly purchase");Tick(a2.U,.3f);a2.C.Bank.Ranks[0]=2;Tick(a2.U,3);
        Check(Mines.Count==24&&Mines.All(m=>Vector3.ProjectOnPlane(Get<Vector3>(m,"landing"),Vector3.up).magnitude<=15.01f),"mid-deployment purchase cannot change accepted ring count or radius");
        Reset();a2=Actor();a2.C.Bank.Ranks[3]=3;Check(a2.U.TryCast(false,Vector3.forward,out _),"ranked pre-revival cast");Tick(a2.U,1);var revive=Actor();revive.C.Body.master=a2.C.Body.master;revive.C.Bank=a2.C.Bank;Tick(revive.U,.02f);
        Check(ReferenceEquals(revive.U.WingburstReserve,a2.U.WingburstReserve)&&revive.U.WingburstReserve.Stock==1,"revival reuses master utility stock without free charge creation");Near(revive.U.WingburstReserve.Interval,8,"revived body retains upgraded cooldown");
        Reset();a2=Actor();a2.C.Bank.Ranks[0]=a2.C.Bank.Ranks[3]=3;var custom=BastionBalance.Defaults();custom[(int)BastionSetting.UpgradedMineCount]=1;custom[(int)BastionSetting.UpgradedMineDeployRadius]=3;custom[(int)BastionSetting.UpgradedMineBlastRadius]=.5f;custom[(int)BastionSetting.UpgradedMineChainBonus]=0;custom[(int)BastionSetting.UpgradedWingburstDistance]=1;custom[(int)BastionSetting.UpgradedWingburstShots]=1;custom[(int)BastionSetting.UpgradedWingburstCooldown]=100;BastionBalance.Apply(custom);
        Check(BastionUtilityUpgrades.MineCount(a2.C.Bank)==24&&BastionUtilityUpgrades.RushShots(a2.C.Bank)==32,"custom upgrade sliders cannot reduce base payload sizes");Near(BastionUtilityUpgrades.RushDistance(a2.C.Bank),15,"custom upgrade cannot shrink base travel");Near(BastionUtilityUpgrades.RushCooldown(a2.C.Bank),10,"custom upgrade cannot worsen base recharge");Near(BastionUtilityUpgrades.MineFieldRadius(a2.C.Bank),15,"custom upgrade cannot shrink base field");Near(BastionUtilityUpgrades.MineDamage(a2.C.Bank),.9f,"custom upgrade cannot reduce base charged damage");Near(BastionUtilityUpgrades.MineBlastRadius(a2.C.Bank),8,"custom upgrade cannot shrink base blast");
        Reset();a2=Actor();foreach(int count in Enumerable.Range(1,36))
        {
            var offsets=Enumerable.Range(0,count).Select(i=>BastionMineRules.LandingOffset(i,count,15,8)).ToArray();
            Check(offsets.All(p=>float.IsFinite(p.x)&&float.IsFinite(p.z)&&p.magnitude<=15.001f),"every custom mine count produces finite in-field geometry");
            if(count>=3)Check(offsets.Any(p=>Math.Abs(p.magnitude-5)<.001f)&&offsets.Any(p=>Math.Abs(p.magnitude-10)<.001f)&&offsets.Any(p=>Math.Abs(p.magnitude-15)<.001f),"all valid counts populate each of the three rings");
        }
        foreach(int choice in new[]{0,1})
        {
            Reset();a2=Actor();a2.C.Bank.Ranks[0]=3;a2.C.Bank.Choices[0]=choice;var enemy=Enemy(1);AttachMine(a2.C,enemy);
            BastionServerSpiderMine.TriggerWeaponHit(a2.C,enemy.healthComponent);float pending=a2.C.Reservations[enemy.healthComponent];
            a2.C.Bank.Choices[0]=1-choice;StepMines(.2f);
            var blast=BlastAttack.Fired.Single();Near(blast.baseDamage,choice==0?12.6f:10.8f,"actual host mine uses its accepted specialization damage");Near(blast.radius,choice==0?8:10,"actual host mine uses its accepted specialization radius");Near(pending,blast.baseDamage,"reservation and native burst agree even if upgrade choice changes during anticipation");
            Check(a2.C.Effects.Last(e=>e.Kind==BastionFxKind.MineChainExplosion).Value==blast.radius,"charged visuals use the same accepted radius as native damage");
        }
    }
    static void Targeting()
    {
        Reset();var(c,u)=Actor();var near=Enemy(5);var elite=Enemy(30,2);var boss=Enemy(40,3);Check(BastionUtilityCombat.Find(c,Vector3.zero,45,null)==boss,"boss threat outranks distance");boss.Blocked=true;Check(BastionUtilityCombat.Find(c,Vector3.zero,45,null)==elite,"occluded target excluded");boss.Blocked=false;
        c.Reservations[boss.healthComponent]=1001;Check(BastionUtilityCombat.Find(c,Vector3.zero,45,null,true)==elite,"reserved lethal damage spreads missiles");boss.corePosition=new(0,0,100);elite.Blocked=true;Check(BastionUtilityCombat.Find(c,Vector3.zero,45,null)==near,"range and world obstruction honored");
    }
    static void BatteryTargeting()
    {
        Collider Box(CharacterBody body,float radius=.5f){var box=body.gameObject.AddComponent<Collider>();box.Radius=radius;return box;}
        CharacterBody Pick(BastionController c,Vector3 aim,float range=90,Vector3? from=null)=>BastionBatteryTargeting.Find(c,range,aim,from);
        Reset();var(c,u)=Actor();var close=Enemy(0,0,5);var aimed=Enemy(70);Box(close);Box(aimed);
        Check(Pick(c,Vector3.forward)==aimed,"manual crosshair target overrides a much nearer off-axis enemy");
        c.Reservations[aimed.healthComponent]=100000;c.Reservations[close.healthComponent]=100000;
        Check(Pick(c,Vector3.forward)==aimed,"other incoming attacks cannot override the manually aimed living target");
        Check(Pick(c,Vector3.forward*-1)==close,"aiming at empty space falls back to the nearest enemy, even if incoming damage is lethal");
        aimed.healthComponent.alive=false;Check(Pick(c,Vector3.forward)==close,"a dead manual target immediately falls back to the nearest living enemy");
        Reset();(c,u)=Actor();close=Enemy(0,0,5);aimed=Enemy(70,3,3);Box(close);Box(aimed);
        Check(Pick(c,Vector3.forward)==aimed,"an enemy near the crosshair takes priority over the nearest enemy");
        Reset();(c,u)=Actor();close=Enemy(0,0,5);var inside=Enemy(20,0,5.1f);var outside=Enemy(20,0,-5.6f);
        Check(Pick(c,Vector3.forward)==inside,"an enemy about 14 degrees off the crosshair is inside the generous cone");
        inside.Team=0;Check(Pick(c,Vector3.forward)==close,"an enemy about 16 degrees off the crosshair falls back to nearest");
        Reset();(c,u)=Actor();var nearer=Enemy(30,0,4);var centered=Enemy(60,0,2);
        Check(Pick(c,Vector3.forward)==centered,"inside the cone the enemy closest to the crosshair wins, not the closest to Bastion");
        Reset();(c,u)=Actor();var big=Enemy(30,0,2);Box(big,3);centered=Enemy(50,0,1);
        Check(Pick(c,Vector3.forward)==big,"an enemy actually under the crosshair outranks one merely nearer its center");
        Reset();(c,u)=Actor();close=Enemy(0,0,5);aimed=Enemy(91);Box(close);Box(aimed);
        Check(Pick(c,Vector3.forward)==close,"manual aim still respects configured battery range");
        Reset();(c,u)=Actor();close=Enemy(0,0,5);var front=Enemy(20);var back=Enemy(40,3);Box(front);Box(back);
        CharacterBody.readOnlyInstancesList.Reverse();Check(Pick(c,Vector3.forward)==front,"overlapping targets select the first actual hurtbox on the aim ray");
        Reset();(c,u)=Actor();close=Enemy(5);aimed=Enemy(0,0,60);Box(close);Box(aimed);c.Body.inputBank.aimDirection=Vector3.forward;
        Check(Pick(c,Vector3.right)==aimed,"submitted client aim controls manual targeting rather than stale host input direction");
        aimed.Blocked=true;Check(Pick(c,Vector3.right)==close,"an unreachable aimed target cannot prevent a reachable nearby shot");
        Reset();(c,u)=Actor();close=Enemy(5);aimed=Enemy(0,0,20);Box(close);Box(aimed);
        Physics.Floor=false;Physics.Wall=true;
        Check(BastionCombat.TryMissileRoute(Vector3.zero,aimed,out _,out bool loft)&&loft,"fixture permits a lofted route over an obstructing wall");
        Check(Pick(c,Vector3.right)==close,"terrain blocking the crosshair prevents a manual lock even when the missile could loft to that enemy");
        Reset();(c,u)=Actor();close=Enemy(0,0,5);aimed=Enemy(10,0,5);Box(close);var disabled=Box(aimed,6);
        Check(Pick(c,Vector3.forward)==aimed,"a large hurtbox under the crosshair locks even with its center outside the cone");disabled.enabled=false;
        Check(Pick(c,Vector3.forward)==close,"disabled enemy hurtboxes do not receive manual locks");
        disabled.enabled=true;disabled.gameObject.SetActive(false);Check(Pick(c,Vector3.forward)==close,"inactive enemy hurtboxes do not receive manual locks");
        Reset();(c,u)=Actor();close=Enemy(0,0,5);aimed=Enemy(30,3,10);Box(close);Box(aimed);
        aimed.hurtBoxGroup=aimed.gameObject.AddComponent<HurtBoxGroup>();var extra=new GameObject().AddComponent<HurtBox>();extra.healthComponent=aimed.healthComponent;extra.transform.position=new(0,0,30);extra.gameObject.AddComponent<Collider>().Radius=1;
        aimed.hurtBoxGroup.hurtBoxes=new[]{extra};Check(Pick(c,Vector3.forward)==aimed,"manual aim recognizes secondary hurtboxes on large or modded enemies");
        extra.healthComponent=close.healthComponent;Check(Pick(c,Vector3.forward)==close,"foreign hurtboxes cannot redirect a lock to the wrong body");
        Reset();(c,u)=Actor();close=Enemy(5);var boss=Enemy(20,3);var elite=Enemy(15,2);var flying=Enemy(10,1);
        Check(Pick(c,Vector3.right)==close,"empty-aim fallback ignores boss, elite and flying rank");
        close.Blocked=true;Check(Pick(c,Vector3.right)==flying,"fallback skips unreachable nearby enemies");
        flying.Team=0;Check(Pick(c,Vector3.right)==elite,"allies cannot become fallback targets");
        Reset();(c,u)=Actor();close=Enemy(5);var nearProjectile=Enemy(49);Check(Pick(c,Vector3.right,100,new Vector3(0,0,50))==nearProjectile,"replacement missile fallback picks near its current flight position");
        Reset();(c,u)=Actor();var low=Enemy(10,0,-3);var high=Enemy(10,0,3);CharacterBody.readOnlyInstancesList.Reverse();
        Check(Pick(c,Vector3.forward)==low,"equal-distance fallback uses stable network identity rather than scan order");
        Check(Pick(c,new Vector3(float.NaN,0,1))==null&&Pick(c,Vector3.zero)==null&&Pick(c,Vector3.forward,float.NaN)==null,"invalid aim and ranges cannot produce a target");
        Reset();(c,u)=Actor();Check(Pick(c,Vector3.forward)==null,"empty arenas preserve ammunition by returning no target");
    }
    static CharacterBody RushTarget(BastionController c,Vector3 origin,float range=45)=>BastionUtilityCombat.FindRush(c,origin,range);
    static void RushSpreadPlan()
    {
        Reset();var(c,u)=Actor();var near=Enemy(5);var far=Enemy(30,3);var plan=new BastionRushTargetPlan();
        CharacterBody Pick()=>BastionUtilityCombat.FindRush(c,Vector3.zero,45,plan);
        Check(Pick()==near,"an uncovered nearby enemy wins before a distant boss");
        var shot=plan.Claim(c,near,4);Check(Pick()==far,"next missile spreads to an uncovered enemy instead of repeating the nearest");
        shot.Release();Check(Pick()==near&&plan.Count(near.healthComponent)==0,"a cancelled missile frees its previous splash footprint");
        shot.Release();Check(plan.Count(near.healthComponent)==0,"repeated allocation cleanup never makes coverage negative");
        shot=plan.Claim(c,near,4);shot.Commit();shot.Release();Check(Pick()==far&&plan.Count(near.healthComponent)==1,"a landed missile stays counted through destroy cleanup");
        plan.Claim(c,far,4).Commit();Check(Pick()==near,"after covering all targets, equal coverage returns to nearest");
        plan.Claim(c,near,4).Commit();Check(Pick()==far,"repeat passes choose the least-covered target, regardless of boss health");
        var otherCast=new BastionRushTargetPlan();Check(BastionUtilityCombat.FindRush(c,Vector3.zero,45,otherCast)==near,"a later charge gets an independent coverage plan");
        Check(plan.Count(near.healthComponent)==2,"the previous cast keeps its own coverage while another charge starts");

        Reset();(c,u)=Actor();var center=Enemy(8);var neighbor=Enemy(10);far=Enemy(25);plan=new();
        CharacterBody.readOnlyInstancesList.Add(center);shot=plan.Claim(c,center,4);
        Check(plan.Count(center.healthComponent)==1&&plan.Count(neighbor.healthComponent)==1,"one missile covers its entire splash footprint, with duplicate health counted once");
        Check(Pick()==far,"next shot skips collateral already covered by the first splash");
        var overlap=plan.Claim(c,neighbor,4);shot.Release();
        Check(plan.Count(center.healthComponent)==1&&plan.Count(neighbor.healthComponent)==1,"retargeting one missile preserves other missiles' overlapping coverage");
        overlap.Release();Check(plan.Count(center.healthComponent)==0&&plan.Count(neighbor.healthComponent)==0,"last unlanded missile releases all overlapping coverage");
        var blocked=Enemy(9,0,1);blocked.Blocked=true;var ally=Enemy(9,0,-1);ally.Team=0;
        var disabled=Enemy(9,0,2);disabled.gameObject.AddComponent<Collider>().enabled=false;
        var invalid=Enemy(9,0,-2);invalid.healthComponent.combinedHealth=float.NaN;
        shot=plan.Claim(c,center,4);
        Check(plan.Count(blocked.healthComponent)==0&&plan.Count(ally.healthComponent)==0&&plan.Count(disabled.healthComponent)==0&&plan.Count(invalid.healthComponent)==0,"coverage excludes obstructed, friendly, disabled and invalid victims");
        shot.Release();plan.Claim(c,center,float.NaN).Commit();Check(plan.Count(center.healthComponent)==0,"invalid radius cannot reserve phantom splash");

        Reset();(c,u)=Actor();var marked=Enemy(12);var mine=AttachMine(c,marked);near=Enemy(5);plan=new();
        Check(BastionServerSpiderMine.HasAttachedMarker(c,marked.healthComponent)&&Pick()==marked,"your attached spider marks take priority over a closer unmarked enemy");
        Check(BlastAttack.Fired.Count==0&&Get<BastionMinePhase>(mine,"phase")==BastionMinePhase.Attached,"selecting a marked target never remotely triggers its mine");
        shot=plan.Claim(c,marked,4);Check(Pick()==near,"one planned hit on a marked target lets the salvo spread to other enemies");
        shot.Release();Check(Pick()==marked,"losing the missile restores the unhit mark's priority");
        BastionServerSpiderMine.TriggerWeaponHit(c,marked.healthComponent);
        Check(!BastionServerSpiderMine.HasAttachedMarker(c,marked.healthComponent)&&Pick()==near,"an already triggered mark no longer claims priority");
        Set(mine,"weaponPrimed",false);Set(mine,"phase",BastionMinePhase.Leaping);
        Check(Pick()==near,"a spider still leaping toward an enemy is not an attached mark");
        Set(mine,"phase",BastionMinePhase.Attached);Set(mine,"ended",true);Check(Pick()==near,"an ended spider cannot attract missiles");

        Reset();(c,u)=Actor();var teammate=Actor().C;marked=Enemy(12);mine=AttachMine(teammate,marked);near=Enemy(5);plan=new();
        Check(!BastionServerSpiderMine.HasAttachedMarker(c,marked.healthComponent)&&Pick()==near,"another player's mine cannot divert a Rush that cannot trigger it");
        Check(BastionServerSpiderMine.HasAttachedMarker(teammate,marked.healthComponent),"mine ownership remains distinct between Bastions");
        Reset();(c,u)=Actor();marked=Enemy(12);mine=AttachMine(c,marked);near=Enemy(5);plan=new();marked.Blocked=true;
        Check(Pick()==near,"a mark behind terrain does not bypass Rush visibility");marked.Blocked=false;marked.corePosition=new(0,0,60);
        Check(Pick()==near,"a marked enemy outside Rush range cannot attract a shot");

        Reset();(c,u)=Actor();var targets=new[]{Enemy(5),Enemy(15),Enemy(25),Enemy(35)};
        Check(u.TryCast(false,Vector3.forward,out _),"spread volley accepted");Tick(u,1);
        Check(BastionServerMicroMissile.Shots.Count==32&&targets.All(t=>BastionServerMicroMissile.Shots.Count(s=>s.Target==t)==8),"actual accepted-cast loop distributes 32 shots evenly across four separated targets");
        var accepted=Get<BastionRushTargetPlan>(u,"rushPlan");Check(u.TryCast(false,Vector3.forward,out _),"second charge accepted after first recovery");
        Check(!ReferenceEquals(accepted,Get<BastionRushTargetPlan>(u,"rushPlan"))&&targets.All(t=>accepted.Count(t.healthComponent)==8),"new casts replace the plan without clearing older projectiles' coverage");
    }
    static void RushTargetingAndReservations()
    {
        Reset();var(c,u)=Actor();var boss=Enemy(40,3);var pack=new[]{Enemy(10,0,-1),Enemy(10),Enemy(10,0,1)};Check(RushTarget(c,Vector3.zero)==pack[1],"Rush chooses the nearest individual, not a distant boss");
        Reset();(c,u)=Actor();var ordinary=Enemy(5);var elite=Enemy(30,2);Check(RushTarget(c,Vector3.zero)==ordinary,"an elite cannot override a nearer ordinary enemy");
        Reset();(c,u)=Actor();boss=Enemy(40,3);pack=new[]{Enemy(10,0,-1),Enemy(10),Enemy(10,0,1)};foreach(var body in pack)body.healthComponent.combinedHealth=1;Check(RushTarget(c,Vector3.zero)==pack[1],"enemy health does not change nearest-target priority");
        Reset();(c,u)=Actor();var left=new[]{Enemy(10,0,-9),Enemy(10,0,-8),Enemy(10,0,-7)};var right=new[]{Enemy(10,0,7),Enemy(10,0,8),Enemy(10,0,9)};foreach(var body in left.Concat(right))body.healthComponent.combinedHealth=12;
        var first=RushTarget(c,Vector3.zero);Check(left.Contains(first),"equal-distance enemies use stable network identity ties");var claimed=new BastionMicroSplashReservation();claimed.Assign(c,first,12,4);Check(c.Reservations.Count==3&&left.All(body=>c.Reservations.ContainsKey(body.healthComponent)),"one missile still reserves its projected footprint for other weapons");Check(RushTarget(c,Vector3.zero)==first,"incoming splash reservations cannot pull Rush away from the nearest enemy");claimed.Release();Check(c.Reservations.Count==0,"releasing projected splash clears the entire footprint");
        Reset();(c,u)=Actor();ordinary=Enemy(5);var armored=Enemy(30,2);armored.armor=100;Check(RushTarget(c,Vector3.zero)==ordinary,"distance determines priority, without armor or tier scoring");
        Reset();(c,u)=Actor();ordinary=Enemy(5);var remote=Enemy(110);Check(RushTarget(c,new(0,0,100),20)==remote,"retarget range is measured from the projectile rather than the dashing owner");remote.Blocked=true;Check(RushTarget(c,new(0,0,100),20)==null,"projectile retarget rejects occluded enemies");
        Reset();(c,u)=Actor();var lowId=Enemy(10,0,-6);var highId=Enemy(10,0,6);CharacterBody.readOnlyInstancesList.Reverse();Check(RushTarget(c,Vector3.zero)==lowId,"score and distance ties use stable network identity rather than enumeration order");
        Reset();(c,u)=Actor();var primary=Enemy(10);var neighbor=Enemy(12);neighbor.armor=100;var large=Enemy(19);large.gameObject.AddComponent<Collider>().Radius=6;var outside=Enemy(20);var ally=Enemy(11);ally.Team=0;var blocked=Enemy(11,0,1);blocked.Blocked=true;var invalid=Enemy(11,0,-1);invalid.healthComponent.combinedHealth=float.NaN;var empty=Enemy(11,0,2);empty.healthComponent.combinedHealth=0;var disabled=Enemy(11,0,-2);disabled.gameObject.AddComponent<Collider>().enabled=false;
        CharacterBody.readOnlyInstancesList.Add(primary);c.Reservations[neighbor.healthComponent]=5;claimed=new BastionMicroSplashReservation();claimed.Assign(c,primary,12,4);
        Check(c.Reservations.Count==3,"projected footprint excludes allies, blocked, disabled, outside and invalid-health victims");Near(c.Reservations[primary.healthComponent],12,"primary is reserved exactly once even when listed twice");Near(c.Reservations[neighbor.healthComponent],11,"collateral reservation respects armor and preserves preexisting claims");Near(c.Reservations[large.healthComponent],12,"closest collider surface includes a large enemy whose centre is outside the radius");
        claimed.Release();Check(c.Reservations.Count==1,"cleanup leaves other weapons' claims alone");Near(c.Reservations[neighbor.healthComponent],5,"cleanup restores preexisting reservation exactly");claimed.Release();Near(c.Reservations[neighbor.healthComponent],5,"repeated cleanup is harmless");
        claimed.Assign(c,primary,12,4);claimed.Assign(c,outside,12,1);Check(!c.Reservations.ContainsKey(primary.healthComponent)&&c.Reservations.ContainsKey(outside.healthComponent),"reassignment first clears the old splash footprint");claimed.Release();Check(c.Reservations.Count==1,"reassigned footprint also cleans up fully");
        claimed.Assign(c,primary,12,4);claimed.Assign(c,primary,float.NaN,4);Check(c.Reservations.Count==1,"invalid reassignment releases old claims without reserving invalid damage");
        Reset();(c,u)=Actor();primary=Enemy(10);var groupBody=Enemy(19);groupBody.gameObject.AddComponent<Collider>().enabled=false;groupBody.hurtBoxGroup=groupBody.gameObject.AddComponent<HurtBoxGroup>();var secondary=new GameObject().AddComponent<HurtBox>();secondary.transform.position=new(0,0,13);secondary.healthComponent=groupBody.healthComponent;secondary.gameObject.AddComponent<Collider>();groupBody.hurtBoxGroup.hurtBoxes=new[]{secondary};Check(BastionUtilityCombat.InRushBlast(primary.corePosition,groupBody,4),"splash checks additional real hurtboxes when a large body's main collider misses");
        claimed=new BastionMicroSplashReservation();claimed.Assign(c,primary,12,4);Check(c.Reservations.Count==2,"large-body group reservation still counts health once");groupBody.healthComponent.Dead=true;claimed.Release();Check(c.Reservations.Count==0,"destroyed victim cleanup does not leak a reservation");
        Reset();(c,u)=Actor();primary=Enemy(10);primary.healthComponent.combinedHealth=12;claimed=new BastionMicroSplashReservation();claimed.Assign(c,primary,12,4);Check(RushTarget(c,Vector3.zero)==primary,"the nearest living enemy remains eligible even when pending damage is lethal");claimed.Release();
        Reset();(c,u)=Actor();ordinary=Enemy(5);pack=new[]{Enemy(30,0,-1),Enemy(30),Enemy(30,0,1)};Check(RushTarget(c,Vector3.zero)==ordinary,"a farther dense pack never steals Rush from a close enemy");
        Reset();(c,u)=Actor();ordinary=Enemy(-3);boss=Enemy(12,3);Check(RushTarget(c,Vector3.zero)==ordinary,"Rush prioritizes the closest enemy in any direction, independently of aim");ordinary.Blocked=true;Check(RushTarget(c,Vector3.zero)==boss,"a hidden nearest enemy is skipped for a visible one");boss.healthComponent.alive=false;Check(RushTarget(c,Vector3.zero)==null,"dead and blocked targets cannot receive Rush shots");
    }
    static void Until(BastionServerSpiderMine mine,BastionMinePhase phase,float timeout=2)
    {
        for(float t=0;t<timeout&&mine&&Get<BastionMinePhase>(mine,"phase")!=phase;t+=.02f)StepMines(.02f);
        Check(mine&&Get<BastionMinePhase>(mine,"phase")==phase,"mine reaches "+phase+" within timeout");
    }
    static void MineTerrainClearance()
    {
        Reset();Physics.RadiusAwareFlight=true;
        Check(!BastionCombat.ClearFlight(Vector3.up*.05f,Vector3.up),"native-sized flight sphere at five centimetres overlaps support floor by seven centimetres");
        Check(BastionCombat.ClearFlight(Vector3.up*.3f,Vector3.up),"same floor corridor clears when flight sphere starts above its radius");
        foreach(float slope in new[]{0f,.7f,-.7f,1.8f,2.6f})
        {
            Reset();Physics.RadiusAwareFlight=true;Physics.FloorSlope=slope;var(c,u)=Actor();var enemy=Enemy(1,0,1);
            enemy.corePosition=new(1,slope+1,1);enemy.transform.position=enemy.corePosition;enemy.gameObject.AddComponent<Collider>().Radius=.4f;
            var mine=Spawn(c);Place(mine,BastionMinePhase.Planted,Vector3.zero);Until(mine,BastionMinePhase.Attached,4);
            Check(BlastAttack.Fired.Count==0,"terrain-aware latch only marks and never deals autonomous damage");
        }
        Reset();Physics.RadiusAwareFlight=true;Physics.StepX=.5f;Physics.StepHeight=.4f;var a=Actor();var stepEnemy=Enemy(0,0,1.5f);stepEnemy.corePosition=new(1.5f,1.4f,0);stepEnemy.transform.position=stepEnemy.corePosition;stepEnemy.gameObject.AddComponent<Collider>().Radius=.4f;
        var stepMine=Spawn(a.C);Place(stepMine,BastionMinePhase.Planted,Vector3.zero);Until(stepMine,BastionMinePhase.Attached,4);
        Check(BlastAttack.Fired.Count==0,"small terrain lip does not create an autonomous detonation fallback");
        Reset();Physics.RadiusAwareFlight=true;Physics.StepX=.5f;Physics.StepHeight=.4f;a=Actor();stepEnemy=Enemy(0,0,8);stepEnemy.corePosition=new(8,1.4f,0);stepEnemy.transform.position=stepEnemy.corePosition;stepEnemy.gameObject.AddComponent<Collider>().Radius=.4f;
        stepMine=Spawn(a.C);Place(stepMine,BastionMinePhase.Planted,Vector3.zero);Until(stepMine,BastionMinePhase.Crawling);Until(stepMine,BastionMinePhase.Leaping,4);
        Check(stepMine.transform.position.x>5&&Get<float>(stepMine,"walked")>5,"mine actually crawls over small raised step before reaching latch distance");Near(stepMine.transform.position.y,.4f,"step crawl lands on raised support surface");Until(stepMine,BastionMinePhase.Attached);Check(BlastAttack.Fired.Count==0,"step traversal and attachment still deal no autonomous damage");
        Reset();Physics.RadiusAwareFlight=true;Physics.Wall=true;a=Actor();var blockedEnemy=Enemy(0,0,2.5f);blockedEnemy.corePosition=new(2.5f,1,0);blockedEnemy.transform.position=blockedEnemy.corePosition;blockedEnemy.gameObject.AddComponent<Collider>().Radius=.4f;
        Check(!BastionCombat.ClearFlight(new(0,.45f,0),new(3,.45f,0)),"radius-aware native flight fixture recognizes solid high wall");
        var blockedMine=Spawn(a.C);Place(blockedMine,BastionMinePhase.Crawling,Vector3.zero);Set(blockedMine,"target",blockedEnemy);StepMines(4);
        Check(blockedMine&&Get<BastionMinePhase>(blockedMine,"phase")!=BastionMinePhase.Attached&&blockedMine.transform.position.x<Physics.WallMinX,"mine-safe clearance still blocks actual high wall rather than attaching through it");Check(BlastAttack.Fired.Count==0,"wall obstruction never invokes autonomous damage fallback");

    }
    static BastionServerSpiderMine AttachMine(BastionController owner,CharacterBody target,Vector3 origin=default)
    {
        var mine=Spawn(owner,origin);Place(mine,BastionMinePhase.Planted,origin);Until(mine,BastionMinePhase.Attached,8);
        Check(Get<CharacterBody>(mine,"target")==target,"spider attaches to its actual acquired enemy");return mine;
    }
    static void MineLifecycle()
    {
        Reset();var(c,u)=Actor();var m=Spawn(c);var from=m.transform.position;
        StepMines(.4f);Check(m.transform.position.y>from.y,"mine ejects upward in an arc");StepMines(.8f);
        Check(Get<BastionMinePhase>(m,"phase")==BastionMinePhase.Planted,"mine lands and remains planted");var planted=m.transform.position;StepMines(1);Near((m.transform.position-planted).magnitude,0,"untriggered mine never roams");
        var enemy=Enemy(planted.z+30.5f,0,planted.x);StepMines(.2f);Check(Get<BastionMinePhase>(m,"phase")==BastionMinePhase.Planted,"thirty-metre detection excludes distant enemies");
        enemy.corePosition=new(planted.x,0,planted.z+29);enemy.transform.position=enemy.corePosition;enemy.gameObject.AddComponent<Collider>().Radius=.7f;
        Until(m,BastionMinePhase.Crawling);Until(m,BastionMinePhase.Leaping,5);var preLeap=m.transform.position;var leapState=c.Effects.Last(e=>e.Kind==BastionFxKind.MineState);Check(leapState.Target==enemy&&leapState.B.sqrMagnitude>1,"initial leap snapshot immediately carries real target and launch velocity");
        for(int i=0;i<20&&Get<BastionMinePhase>(m,"phase")==BastionMinePhase.Leaping;i++){enemy.corePosition+=Vector3.right*.05f;enemy.transform.position=enemy.corePosition;StepMines(.02f);}
        Check(Get<BastionMinePhase>(m,"phase")==BastionMinePhase.Attached,"timed leap reliably attaches even while the target moves");Check((m.transform.position-preLeap).magnitude>.5f,"attachment visibly closes the last gap");
        Check(c.Effects.Count(e=>e.Kind==BastionFxKind.MineArming)==1,"one latch cue per attachment");Check(!enemy.Buffs.Any()&&BlastAttack.Fired.Count==0,"attachment marks without autonomous damage or slow");
        var local=Get<Vector3>(m,"anchorLocal");enemy.corePosition+=new Vector3(4,3,-2);enemy.transform.position=enemy.corePosition;enemy.transform.Yaw=90;StepMines(.2f);
        Near((m.transform.position-enemy.transform.TransformPoint(local)).magnitude,0,"attached mine follows target translation and rotation using body-local anchor");
        enemy.corePosition+=new Vector3(100,0,100);enemy.transform.position=enemy.corePosition;StepMines(.2f);Near((m.transform.position-enemy.transform.TransformPoint(local)).magnitude,0,"attached mine follows teleport without tiny-point reacquisition");
        var state=c.Effects.Last(e=>e.Kind==BastionFxKind.MineState);Check(state.Target==enemy,"attachment snapshot includes target for remote reconstruction");Near((state.B-local).magnitude,0,"attachment snapshot B is exact body-local anchor");Near((state.A-m.transform.position).magnitude,0,"attachment snapshot A is current authoritative world anchor");
        StepMines(30);Check(m&&BlastAttack.Fired.Count==0&&!enemy.Buffs.Any(),"attached mines never auto-detonate or slow after thirty seconds");
        StepMines(59.9f-Get<float>(m,"groundAge"));Check(m&&Get<float>(m,"groundAge")>=59.89f,"attached mine survives to 59.9 seconds");Invoke(m,"Send");state=c.Effects.Last(e=>e.Kind==BastionFxKind.MineState);
        int reportedPhase=(int)Math.Floor(state.Value);Check(reportedPhase==(int)BastionMinePhase.Attached&&state.Target==enemy,"sixty-second attachment retains phase and target in snapshot");Near((state.Value-reportedPhase)*100,Get<float>(m,"age"),"sixty-second packed age round-trips");
        StepMines(.15f);Check(!m&&BlastAttack.Fired.Count==0,"untriggered attached mine expires harmlessly after sixty seconds");Check(c.Effects.Count(e=>e.Kind==BastionFxKind.MineEnd)==1,"expiry emits one terminal event");
        Reset();(c,u)=Actor();m=Spawn(c);StepMines(17);Check(m&&BlastAttack.Fired.Count==0,"planted mine survives old fifteen-second lifetime");StepMines(59.9f-Get<float>(m,"groundAge"));Check(m,"planted mine survives to 59.9 ground seconds");StepMines(.15f);Check(!m&&BlastAttack.Fired.Count==0,"planted expiry is harmless");
        foreach(float dt in new[]{.02f,.07f,.2f})
        {
            Reset();(c,u)=Actor();enemy=Enemy(1);m=Spawn(c);Place(m,BastionMinePhase.Crawling,Vector3.zero);Set(m,"target",enemy);StepMines(dt,dt);
            Check(Get<BastionMinePhase>(m,"phase")==BastionMinePhase.Leaping,"close target starts explicit latch path across fixed-step sizes");
            for(int i=0;i<20&&Get<BastionMinePhase>(m,"phase")==BastionMinePhase.Leaping;i++){enemy.corePosition+=Vector3.right*.1f;enemy.transform.position=enemy.corePosition;enemy.transform.Yaw+=20;StepMines(dt,dt);}
            Check(Get<BastionMinePhase>(m,"phase")==BastionMinePhase.Attached,"timed latch completes under hitches and target rotation without collision-point misses");
            Near((m.transform.position-enemy.transform.TransformPoint(Get<Vector3>(m,"anchorLocal"))).magnitude,0,"completed latch reaches exact current target-local anchor");
        }
        Reset();(c,u)=Actor();enemy=Enemy(1);m=Spawn(c);Place(m,BastionMinePhase.Crawling,Vector3.zero);Set(m,"target",enemy);StepMines(.02f);Check(Get<BastionMinePhase>(m,"phase")==BastionMinePhase.Leaping,"close target starts a leap before its death");
        Set(m,"nextSend",0f);enemy.healthComponent.alive=false;StepMines(.02f);Check(!m&&c.Effects.Last().Kind==BastionFxKind.MineEnd,"mine ending mid-leap sends no state snapshot after its terminal event");
        Reset();(c,u)=Actor();enemy=Enemy(1);m=AttachMine(c,enemy);enemy.healthComponent.alive=false;StepMines(.02f);Check(!m&&BlastAttack.Fired.Count==0,"untriggered marked enemy death removes its mine without an explosion");
        Reset();(c,u)=Actor();enemy=Enemy(1);m=AttachMine(c,enemy);c.Body.healthComponent.alive=false;StepMines(.02f);Check(!m&&BlastAttack.Fired.Count==0,"owner death removes attached mines harmlessly");
        Reset();(c,u)=Actor();enemy=Enemy(0,1);enemy.corePosition=new(0,6,0);enemy.transform.position=enemy.corePosition;enemy.gameObject.AddComponent<Collider>().Radius=.6f;m=AttachMine(c,enemy);Check(m.transform.position.y>5,"spider actually leaps onto airborne enemy collider");
        Reset();(c,u)=Actor();enemy=Enemy(2,3);enemy.gameObject.AddComponent<Collider>().enabled=false;var hurt=new GameObject().AddComponent<HurtBox>();hurt.healthComponent=enemy.healthComponent;hurt.transform.position=new(3,2,2);var surface=hurt.gameObject.AddComponent<Collider>();surface.Radius=1.5f;enemy.hurtBoxGroup=enemy.gameObject.AddComponent<HurtBoxGroup>();enemy.hurtBoxGroup.hurtBoxes=new[]{hurt};m=AttachMine(c,enemy);
        Check((surface.ClosestPoint(m.transform.position)-m.transform.position).magnitude<=BastionMineRules.ShellClearance+.002f,"attachment uses enabled body hurtbox-group surface when main collider disabled");
        Reset();(c,u)=Actor();m=Spawn(c);Place(m,BastionMinePhase.Crawling,Vector3.zero);var runner=Enemy(7);Set(m,"target",runner);
        for(int i=0;i<500;i++){runner.corePosition=m.transform.position+Vector3.forward*7;runner.transform.position=runner.corePosition;StepMines(.02f);}
        Check(Get<float>(m,"walked")<=50.001f&&Get<float>(m,"walked")>49.9f,"ground crawl respects fifty-metre total travel budget");Check(Get<BastionMinePhase>(m,"phase")==BastionMinePhase.Planted&&BlastAttack.Fired.Count==0,"unreachable runner leaves harmless planted mine");
        Reset();(c,u)=Actor();for(int i=0;i<49;i++)Spawn(c,new(i*20,0,0));Check(BastionServerSpiderMine.Count(c)==48&&BlastAttack.Fired.Count==0,"twenty-fifth deployment preserves owner cap of twenty-four");Check(c.Effects.Count(e=>e.Kind==BastionFxKind.MineEnd)==1,"cap erases oldest mine without damaging anything");
        Reset();(c,u)=Actor();enemy=Enemy(1);m=Spawn(c);Place(m,BastionMinePhase.Armed,Vector3.zero);Set(m,"target",enemy);StepMines(1);Check(BlastAttack.Fired.Count==0,"legacy Armed state never executes old autonomous fuse");
        float maxAge=BastionBalance.Specs[(int)BastionSetting.MineLifetime].Maximum+BastionBalance.Specs[(int)BastionSetting.MineAirTime].Maximum+BastionMineRules.OverchargeFuse;
        Check(maxAge<100,"maximum supported mine age stays below packed scalar rollover");foreach(var encodedPhase in Enum.GetValues<BastionMinePhase>()){float value=(int)encodedPhase+maxAge*.01f;float decodedPhase=(int)Math.Floor(value);Check(decodedPhase==(int)encodedPhase,"long-lived snapshot preserves phase "+encodedPhase);Near((value-decodedPhase)*100,maxAge,"long-lived snapshot preserves age");}
    }
    static void MineCapacity()
    {
        int Claimed(CharacterBody body)=>Mines.Count(m=>Get<CharacterBody>(m,"target")==body&&new[]{BastionMinePhase.Leaping,BastionMinePhase.Attached,BastionMinePhase.Primed}.Contains(Get<BastionMinePhase>(m,"phase")));
        Reset();var a=Actor();var enemy=Enemy(1);enemy.corePosition+=Vector3.up;enemy.transform.position=enemy.corePosition;enemy.gameObject.AddComponent<Collider>().Radius=.6f;var spiders=new List<BastionServerSpiderMine>();
        for(int i=0;i<24;i++){var mine=Spawn(a.C);Place(mine,BastionMinePhase.Planted,Vector3.zero);spiders.Add(mine);}
        for(int i=0;i<150;i++){StepMines(.02f);Check(Claimed(enemy)<=5,"same-tick normal-target acquisitions reserve at most five slots including leap and primed phases");}
        Check(Claimed(enemy)==5&&spiders.Count(m=>Get<BastionMinePhase>(m,"phase")==BastionMinePhase.Attached)==5,"full field on one normal enemy attaches exactly five");
        Check(spiders.Count(m=>Get<BastionMinePhase>(m,"phase")==BastionMinePhase.Planted)==19,"surplus spiders wait harmlessly while the only target is full");
        var contacts=spiders.Where(m=>Get<BastionMinePhase>(m,"phase")==BastionMinePhase.Attached).Select(m=>Get<Vector3>(m,"anchorLocal")).ToArray();
        Check((contacts[0]-contacts[1]).magnitude>.15f&&(contacts[0]-contacts[2]).magnitude>.15f,"reserved slots fan contacts apart rather than overlapping");
        var other=Enemy(3);StepMines(3);
        Check(Claimed(enemy)==5&&Claimed(other)==5,"waiting mines seek another eligible enemy instead of crowding a full target");
        var secondOwner=Actor();var foreign=Spawn(secondOwner.C);Place(foreign,BastionMinePhase.Planted,Vector3.zero);StepMines(.5f);
        Check(Get<BastionMinePhase>(foreign,"phase")==BastionMinePhase.Planted&&Claimed(enemy)==5,"global target capacity respects another player's slots");
        BastionServerSpiderMine.TriggerWeaponHit(a.C,enemy.healthComponent);StepMines(.1f);Check(Claimed(enemy)==5,"priming does not free slots before the burst");
        StepMines(1);Check(Claimed(enemy)==5&&BlastAttack.Fired.Count==5,"detonation frees slots for waiting replacements without autonomous explosions");
        Reset();a=Actor();enemy=Enemy(1);for(int i=0;i<5;i++)AttachMine(a.C,enemy);
        var old=Mines[0];Set(old,"groundAge",59.99f);var waiting=Spawn(a.C);Place(waiting,BastionMinePhase.Planted,Vector3.zero);StepMines(1);
        Check(!old&&Claimed(enemy)==5&&Get<BastionMinePhase>(waiting,"phase")==BastionMinePhase.Attached,"harmless expiry releases a slot for a new spider");Check(BlastAttack.Fired.Count==0,"capacity management causes no autonomous damage");
        foreach(var category in new[]{(Boss:false,Elite:false,Champion:false,Limit:5),(Boss:false,Elite:true,Champion:false,Limit:8),(Boss:true,Elite:false,Champion:false,Limit:12),(Boss:true,Elite:true,Champion:false,Limit:12),(Boss:false,Elite:true,Champion:true,Limit:12)})
        {
            Reset();a=Actor();enemy=Enemy(1);enemy.isBoss=category.Boss;enemy.isElite=category.Elite;enemy.isChampion=category.Champion;enemy.corePosition+=Vector3.up;enemy.transform.position=enemy.corePosition;enemy.gameObject.AddComponent<Collider>().Radius=.7f;enemy.healthComponent.combinedHealth=10000;
            Check(BastionMineRules.TargetLimit(enemy.isBoss||enemy.isChampion,enemy.isElite)==category.Limit,"boss/champion category overrides elite capacity");
            for(int i=0;i<24;i++){var mine=Spawn(a.C);Place(mine,BastionMinePhase.Planted,Vector3.zero);}
            for(int i=0;i<150;i++){StepMines(.02f);Check(Claimed(enemy)<=category.Limit,"category reservations never overbook "+category.Limit);}
            Check(Claimed(enemy)==category.Limit&&Mines.Count(m=>Get<BastionMinePhase>(m,"phase")==BastionMinePhase.Attached)==category.Limit,"all category slots can attach");
            var occupied=Mines.Where(m=>Get<BastionMinePhase>(m,"phase")==BastionMinePhase.Attached).Select(m=>Get<Vector3>(m,"anchorLocal")).ToArray();
            for(int i=0;i<occupied.Length;i++)for(int j=i+1;j<occupied.Length;j++)Check((occupied[i]-occupied[j]).magnitude>.02f,"all twelve contact slots remain distinct");
            Check(BlastAttack.Fired.Count==0,"even twelve boss marks deal no autonomous damage");BastionServerSpiderMine.TriggerWeaponHit(a.C,enemy.healthComponent);StepMines(.2f);
            Check(BlastAttack.Fired.Count==category.Limit,"actual weapon hit detonates exactly the permitted marks");Near(BlastAttack.Fired.Sum(b=>b.baseDamage),category.Limit*10.8f,"category burst uses ninety-percent detonation damage");
        }
        Reset();var owners=Enumerable.Range(0,4).Select(_=>Actor()).ToArray();var targets=Enumerable.Range(0,9).Select(i=>Enemy(1+i*.6f,i%3==0?3:i%3==1?2:0,(i%3-1)*3)).ToArray();
        foreach(var owner in owners)for(int i=0;i<49;i++){var mine=Spawn(owner.C);Place(mine,BastionMinePhase.Planted,Vector3.zero);}
        Check(Mines.Count==192&&owners.All(o=>BastionServerSpiderMine.Count(o.C)==48),"four owners can hold two fields each; replacement enforces each independent cap");
        for(int i=0;i<150;i++)
        {
            StepMines(.02f);
            foreach(var body in targets)Check(Claimed(body)<=BastionMineRules.TargetLimit(body.isBoss,body.isElite),"four-player simultaneous claims never overbook a target");
        }
        foreach(var owner in owners.Take(2))owner.C.Body.healthComponent.alive=false;StepMines(.02f);
        Check(Mines.Count==96&&owners.Skip(2).All(o=>BastionServerSpiderMine.Count(o.C)==48),"owner death cleans only that owner's two fields and reservations");
        foreach(var mine in Mines)Set(mine,"groundAge",60f);StepMines(.02f);
        Check(Mines.Count==0&&BlastAttack.Fired.Count==0,"large multiplayer field expiry clears all claims without unassisted damage");
    }
    static void ChainBursts()
    {
        foreach(var phase in Enum.GetValues<BastionMinePhase>().Where(p=>p!=BastionMinePhase.Primed))
        {
            Reset();var(c,u)=Actor();var enemy=Enemy(1);var m=Spawn(c);Place(m,phase,Vector3.zero);Set(m,"target",enemy);
            BastionServerSpiderMine.TriggerWeaponBlast(c,Vector3.zero,1000);Check(Get<BastionMinePhase>(m,"phase")==phase&&!Get<bool>(m,"weaponPrimed"),"world explosion proximity alone cannot prime "+phase);
            if(phase!=BastionMinePhase.Attached){BastionServerSpiderMine.TriggerWeaponHit(c,enemy.healthComponent);Check(!Get<bool>(m,"weaponPrimed"),"weapon hit cannot prime an unattached "+phase+" mine");}
            Invoke(m,"Explode");Check(BlastAttack.Fired.Count==0,"unprimed mine has no autonomous damage path "+phase);
        }
        Reset();var a=Actor();var other=Actor();var marked=Enemy(1);var wrong=Enemy(4);var first=AttachMine(a.C,marked);var second=AttachMine(a.C,marked);var foreign=AttachMine(other.C,marked);
        BastionServerSpiderMine.TriggerWeaponHit(a.C,wrong.healthComponent);Check(Mines.All(m=>!Get<bool>(m,"weaponPrimed")),"hitting a different enemy cannot trigger attached markers");
        NetworkServer.active=false;BastionServerSpiderMine.TriggerWeaponHit(a.C,marked.healthComponent);Check(!Get<bool>(first,"weaponPrimed"),"client cannot trigger host mines");NetworkServer.active=true;
        BastionServerSpiderMine.TriggerWeaponHit(a.C,marked.healthComponent);Check(Get<BastionMinePhase>(first,"phase")==BastionMinePhase.Primed&&Get<BastionMinePhase>(second,"phase")==BastionMinePhase.Primed,"one real weapon hit triggers every owner mine attached to its victim");Check(!Get<bool>(foreign,"weaponPrimed"),"weapon hit cannot trigger another owner's attached mine");
        BastionServerSpiderMine.TriggerWeaponHit(a.C,marked.healthComponent);Check(a.C.Effects.Count(e=>e.Kind==BastionFxKind.MineChain)==2,"repeated hit does not duplicate accepted prime feedback");StepMines(.14f);Check(BlastAttack.Fired.Count==0,"primed mine has a visible short anticipation window");StepMines(.02f);
        Check(!first&&!second&&foreign&&BlastAttack.Fired.Count==2,"exactly two owner mines burst once after weapon hit");Near(BlastAttack.Fired[0].baseDamage,10.8f,"each triggered burst deals 90 percent at base twelve damage");Near(BlastAttack.Fired[0].radius,8,"triggered blast retains eight-metre radius");Near(BlastAttack.Fired[0].procCoefficient,.2f,"triggered blast uses the lower kit proc coefficient and retains native item events");Check(marked.Buffs.Count(b=>b.Buff==RoR2Content.Buffs.Slow50&&b.Duration==3)==2,"native actual explosion victims receive three-second Slow50");Check(a.C.Reservations.Count==0,"detonations release pending damage claims");Check(a.C.Effects.Count(e=>e.Kind==BastionFxKind.MineChainExplosion)==2&&a.C.Effects.All(e=>e.Kind!=BastionFxKind.MineExplosion),"all accepted detonations use distinct charged feedback");
        Reset();a=Actor();marked=Enemy(1);wrong=Enemy(-3);first=AttachMine(a.C,marked);second=AttachMine(a.C,wrong,new(0,0,-4));BastionServerSpiderMine.TriggerWeaponHit(a.C,marked.healthComponent);StepMines(.2f);
        Check(!first&&second&&wrong.healthComponent.Hits==1&&!Get<bool>(second,"weaponPrimed"),"mine blast hitting another marked enemy cannot trigger that enemy's mine");StepMines(2);Check(second&&BlastAttack.Fired.Count==1,"no delayed mine-to-mine propagation or autonomous follow-up");
        Reset();a=Actor();marked=Enemy(1);first=AttachMine(a.C,marked);var anchor=first.transform.position;marked.healthComponent.alive=false;marked.healthComponent.combinedHealth=0;BastionServerSpiderMine.TriggerWeaponHit(a.C,marked.healthComponent);StepMines(.2f);
        Check(!first&&BlastAttack.Fired.Count==1,"actual weapon hit still triggers marker when that hit killed the enemy");Near((BlastAttack.Fired[0].position-anchor).magnitude,0,"lethal triggering hit bursts at last valid attachment point");
        Reset();a=Actor();marked=Enemy(1);var fullStack=new List<BastionServerSpiderMine>();for(int i=0;i<3;i++)fullStack.Add(AttachMine(a.C,marked));
        Check(BastionServerSpiderMine.Count(a.C)==3&&fullStack.All(m=>Get<BastionMinePhase>(m,"phase")==BastionMinePhase.Attached),"three permitted marks remain attached until a weapon hit");
        BastionServerSpiderMine.TriggerWeaponHit(a.C,marked.healthComponent);StepMines(.2f);Check(Mines.Count==0&&BlastAttack.Fired.Count==3,"one actual hit triggers exactly three attached mines");
        Reset();a=Actor();marked=Enemy(1);first=AttachMine(a.C,marked);Set(first,"groundAge",59.95f);BastionServerSpiderMine.TriggerWeaponHit(a.C,marked.healthComponent);StepMines(.2f);Check(!first&&BlastAttack.Fired.Count==1,"accepted weapon burst near expiry completes its short anticipation");
        Reset();a=Actor();marked=Enemy(1);first=AttachMine(a.C,marked);marked.corePosition=new(100,5,100);marked.transform.position=marked.corePosition;marked.transform.Yaw=180;StepMines(.02f);BastionServerSpiderMine.TriggerWeaponHit(a.C,marked.healthComponent);var local=Get<Vector3>(first,"anchorLocal");marked.corePosition+=Vector3.right*2;marked.transform.position=marked.corePosition;StepMines(.08f);
        Near((first.transform.position-marked.transform.TransformPoint(local)).magnitude,0,"primed marker continues following living target during anticipation");var state=a.C.Effects.Last(e=>e.Kind==BastionFxKind.MineState);Check(state.Target==marked&&(int)state.Value==(int)BastionMinePhase.Primed,"primed snapshot retains target contract");Near((state.B-local).magnitude,0,"primed snapshot retains exact local anchor");StepMines(.1f);Check(BlastAttack.Fired.Count==1,"remote moving mark detonates from confirmed victim rather than owner proximity");
    }
    static void Snapshots()
    {
        Reset();var host=Actor(false);Check(host.U.TryCast(false,Vector3.forward,out _),"remote owner host cast");Tick(host.U,1.2f);var terminal=Copy(new BastionUtilityStatus(host.U,5));
        Check(terminal.Stock==1&&terminal.Capacity==2,"host snapshot retains one of two base Rush charges");Near(terminal.Interval,10,"host snapshot carries ten-second charge interval");Near(terminal.Remaining,8.8f,"host snapshot carries elapsed recharge");
        Check(terminal.MineStock==1&&terminal.MineCapacity==1,"untouched mine reserve serializes independently");Near(terminal.MineInterval,24,"host snapshot carries mine interval");
        Check(terminal.Kind==BastionUtilityKind.None&&terminal.CastKind==BastionUtilityKind.Wingburst,"terminal status retains accepted dash intent");
        NetworkServer.active=false;Time.fixedDeltaTime=.02f;var client=Actor();terminal.Owner=client.C.Body.netId;terminal.OnReceived();
        Check(client.U.WingburstReserve.Stock==1&&client.U.WingburstReserve.Capacity==2,"remote owner displays both-charge base capacity");Near(client.U.WingburstReserve.Remaining,8.8f,"remote owner uses authoritative recharge progress");
        var motor=new CharacterMotor();float moved=0;for(int i=0;i<60;i++)if(client.U.Move(motor))moved+=motor.velocity.magnitude*.02f;Near(moved,15,"lost start status recovers full accepted fifteen-metre dash");
        terminal.Sequence=6;terminal.OnReceived();Check(!client.U.Move(motor),"duplicate terminal never restarts dash");
        terminal.Sequence=4;terminal.Stock=0;terminal.OnReceived();Check(client.U.WingburstReserve.Stock==1,"older packet cannot remove remaining charge");
        var corrupt=Copy(terminal);corrupt.Sequence=7;corrupt.End=new(float.NaN,0,0);corrupt.OnReceived();Check(client.U.WingburstReserve.Stock==1,"nonfinite snapshot ignored");
        terminal.Sequence=8;terminal.Stock=1;terminal.MineStock=0;terminal.MineRemaining=19;terminal.MineCapacity=1;terminal.MineInterval=24;terminal.ActiveMines=99;terminal.OnReceived();
        Near(client.U.MinesReserve.Remaining,19,"mine reserve independently serialized");
        Check(client.U.WingburstReserve.Stock==1&&client.U.WingburstReserve.Capacity==2&&client.U.MinesReserve.Stock==0&&client.U.MinesReserve.Capacity==1,"valid status preserves both independent stocks and capacities");Check(client.U.ActiveMines==48,"replicated mine count bounded");
        var observer=Actor(false);terminal.Owner=observer.C.Body.netId;terminal.Sequence=1;terminal.OnReceived();observer.C.Body.hasEffectiveAuthority=true;terminal.Sequence=2;terminal.OnReceived();Check(!observer.U.Move(motor),"authority change never replays old dash");
        var late=Actor();terminal.Owner=late.C.Body.netId;terminal.Age=9;terminal.Sequence=1;terminal.OnReceived();Check(!late.U.Move(motor),"late join cannot replay old dash");
        int before=client.U.WingburstReserve.Stock;NetworkServer.active=true;terminal.Owner=client.C.Body.netId;terminal.Sequence=10;terminal.Stock=2;terminal.Capacity=100;terminal.MineStock=99;terminal.OnReceived();
        Check(client.U.WingburstReserve.Stock==before&&client.U.WingburstReserve.Capacity==2&&client.U.MinesReserve.Stock==0,"host ignores unsolicited client utility stock and capacity");
        Reset();var bonusHost=Actor(false);bonusHost.C.Body.skillLocator.utility.bonusStockFromBody=2;Tick(bonusHost.U,20);var bonusStatus=Copy(new BastionUtilityStatus(bonusHost.U,1));
        Check(bonusStatus.Capacity==4&&bonusStatus.Stock==4,"host adds two legitimate item stocks on top of two base stocks");
        Check(bonusStatus.MineCapacity==3&&bonusStatus.MineStock==1,"item bonus preserves separate mine recharge rather than granting stock");
        NetworkServer.active=false;var bonusClient=Actor();bonusStatus.Owner=bonusClient.C.Body.netId;bonusStatus.OnReceived();
        Check(bonusClient.U.WingburstReserve.Capacity==4&&bonusClient.U.WingburstReserve.Stock==4,"client reproduces legitimate four-charge snapshot exactly");
        Check(bonusClient.U.MinesReserve.Capacity==3&&bonusClient.U.MinesReserve.Stock==1,"client independently reproduces legitimate item-enhanced mine capacity");
    }
    static void Dialogue()
    {
        var g=new BastionDialogueGate();Check(g.CanStart("rush",1,0,false),"first line allowed");g.Started("rush",1,0,8,30);Check(!g.CanStart("mines",2,3,false),"global spacing across speakers");Check(!g.CanStart("mines",2,8,true),"active voice prevents overlap even after spacing");Check(g.CanStart("mines",2,8,false),"other line after spacing");Check(!g.CanStart("rush",1,29,false),"same speaker line repeat blocked");Check(g.CanStart("rush",1,30,false),"repeat allowed at30s");g.Started("rush",1,30,8,30);Check(g.CanStart("rush",1,0,false),"new run clock resets history");Check(!g.CanStart("x",1,float.NaN,false),"invalid clock rejected");
    }
    static void Display()
    {
        Near(BastionDisplayRules.Yaw,0,"front facing selection");Near(BastionDisplayRules.Unfold(0),0,"starts mobile");Near(BastionDisplayRules.Unfold(2),1,"settles siege");Near(BastionDisplayRules.Scale(1),.38f,"smaller display framing");
        float previous=0;for(float t=0;t<4;t+=.02f){float v=BastionDisplayRules.Unfold(t);Check(v>=previous&&BastionDisplayRules.FortressScale(v)<=.3801f,"bounded continuous showcase scale");previous=v;}
    }
    static void Native(string root)
    {
        using var assembly=AssemblyDefinition.ReadAssembly(Path.Combine(root,"bin/Release/netstandard2.1/IvoryBastion.dll"));
        string[] Calls(string type,string method)=>assembly.MainModule.Types.Single(t=>t.Name==type).Methods.Where(m=>m.Name==method&&m.HasBody).SelectMany(m=>m.Body.Instructions).Where(i=>i.Operand is MethodReference).Select(i=>((MethodReference)i.Operand).DeclaringType.Name+"::"+((MethodReference)i.Operand).Name).ToArray();
        Check(Calls("BastionHud","SetOpen").Contains("BastionEngineeringPause::SetOpen"),"real Engineering open/close owns the solo pause lifecycle");
        var purchase=Calls("BastionHud","Purchase");Check(Array.IndexOf(purchase,"BastionEngineeringPause::HandlePurchase")>=0&&Array.IndexOf(purchase,"BastionEngineeringPause::HandlePurchase")<Array.IndexOf(purchase,"BastionRequests::Submit"),"real menu handles paused solo purchases before the unchanged multiplayer request path");
        Check(Calls("BastionHud","OnDisable").Contains("BastionHud::SetOpen")&&Calls("BastionHud","OnDestroy").Contains("BastionHud::SetOpen"),"HUD replacement/disable always releases its pause");
        Check(Calls("BastionEngineeringPause","get_Solo").Contains("RoR2Application::get_isInSinglePlayer"),"real pause uses native single-player session flag, never roster count");
        Check(!Calls("BastionEngineeringPause","Tick").Contains("PauseStopController::Pause"),"Engineering cannot request a network-wide pause or open the native overlay over its own UI");
        Check(Calls("BastionMain","HandleMovements").Contains("BastionUtility::Move"),"dash uses existing collision-aware motor");Check(Calls("BastionUtilityState","OnEnter").Contains("BastionRequests::Submit"),"native utility submits reliable host request");
        var weaponBlast=Calls("BastionCombat","Blast");Check(weaponBlast.Contains("BastionServerSpiderMine::TriggerWeaponHit")&&!weaponBlast.Contains("BastionServerSpiderMine::TriggerWeaponBlast"),"own missile impacts prime only actual marked victims");
        Check(Array.IndexOf(weaponBlast,"BlastAttack::Fire")<Array.IndexOf(weaponBlast,"BastionServerSpiderMine::TriggerWeaponHit"),"native blast confirms actual hit results before triggering markers");
        var laser=Calls("BastionCombat","Bullet");Check(laser.Contains("BastionServerSpiderMine::TriggerWeaponHit")&&!laser.Contains("BastionServerSpiderMine::TriggerWeaponBlast")&&!laser.Contains("BastionServerSpiderMine::TriggerWeaponBeam"),"laser primes confirmed actual victims rather than nearby geometry");
        var launch=Calls("BastionServerSpiderMine","Launch");Check(launch.Contains("NetworkServer::get_active")&&launch.Contains("BastionServerSpiderMine::Ground"),"real launches host and terrain gated");var explosion=Calls("BastionServerSpiderMine","Explode");Check(explosion.Contains("BlastAttack::Fire"),"real mine uses native item-interacting blast");Check(!explosion.Any(c=>c.Contains("TriggerWeapon")||c.Contains("PrimeInWeaponBlast")||c=="BastionCombat::Blast"),"native mine explosion cannot call weapon priming helpers");
        Check(Calls("BastionServerMicroMissile","Impact").Contains("BastionCombat::Blast"),"rush missiles use same native weapon integration");Check(Calls("BastionServerWarhead","Impact").Contains("BastionCombat::Blast"),"Skyfall uses same native weapon integration");
        var microImpact=Calls("BastionServerMicroMissile","Impact");Check(Array.IndexOf(microImpact,"BastionMicroSplashReservation::Release")>=0&&Array.IndexOf(microImpact,"BastionMicroSplashReservation::Release")<Array.IndexOf(microImpact,"BastionCombat::Blast"),"real projectile releases its whole footprint before native impact damage");
        var microRetarget=Calls("BastionServerMicroMissile","Retarget");Check(Array.IndexOf(microRetarget,"BastionMicroSplashReservation::Release")>=0&&Array.IndexOf(microRetarget,"BastionMicroSplashReservation::Release")<Array.IndexOf(microRetarget,"BastionUtilityCombat::FindRush"),"real retarget selection cannot be biased by its own old reservations");Check(Array.IndexOf(microRetarget,"BastionServerMicroMissile::Course")<Array.IndexOf(microRetarget,"BastionMicroSplashReservation::Assign"),"retarget only reserves a successfully routed replacement");
        Check(Calls("BastionServerMicroMissile","Launch").Contains("BastionMicroSplashReservation::Assign")&&Calls("BastionServerMicroMissile","OnDestroy").Contains("BastionMicroSplashReservation::Release"),"actual launch and every destroy path use the composed splash reservation lifetime");
        Check(Calls("BastionUtility","TickAttack").Contains("BastionUtilityCombat::FindRush")&&!microRetarget.Contains("BastionUtilityCombat::Find"),"initial and replacement micro-missiles share Rush spread selection");
        Check(Calls("BastionUtilityCombat","FindRush").Contains("BastionServerSpiderMine::HasAttachedMarker")&&Calls("BastionUtilityCombat","FindRush").Contains("BastionRushTargetPlan::Count"),"real selection uses own attached marks and per-cast splash coverage");
        Check(Calls("BastionUtility","TryCast").Contains("BastionRushTargetPlan::.ctor"),"every accepted Rush allocates a new plan");
        Check(Calls("BastionServerMicroMissile","Launch").Contains("BastionRushTargetPlan::Claim")&&Calls("BastionServerMicroMissile","OnDestroy").Contains("Allocation::Release"),"real projectiles claim spread coverage on launch and release it on destruction");
        Check(Array.IndexOf(microRetarget,"Allocation::Release")>=0&&Array.IndexOf(microRetarget,"Allocation::Release")<Array.IndexOf(microRetarget,"BastionUtilityCombat::FindRush")&&Array.IndexOf(microRetarget,"BastionServerMicroMissile::Course")<Array.IndexOf(microRetarget,"BastionRushTargetPlan::Claim"),"retarget removes old spread coverage before selection and claims the replacement only after routing");
        Check(Array.IndexOf(microImpact,"Allocation::Commit")>=0&&Array.IndexOf(microImpact,"Allocation::Commit")<Array.IndexOf(microImpact,"BastionCombat::Blast"),"native impacts commit coverage before damage and destroy cleanup");
        Check(Calls("BastionCombat","Select").Contains("BastionBatteryTargeting::Find"),"real battery shots use the manual-aim/nearest selector");
        var battery=assembly.MainModule.Types.Single(t=>t.Name=="BastionBatteryTargeting");
        Check(battery.Methods.Concat(battery.NestedTypes.SelectMany(t=>t.Methods)).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference m&&m.Name=="Raycast"&&m.DeclaringType.Name=="Collider"),"manual targeting checks actual native enemy colliders");
        Check(Calls("BastionDamageReservation","Assign").Contains("BastionBank::PredictedDamage"),"composed production reservations use actual armor-aware damage prediction");
        Check(Calls("BastionSpiderVisual","Update").Contains("BastionSpiderVisual::FollowAttachment")&&Calls("BastionSpiderVisual","FollowAttachment").Contains("Transform::TransformPoint"),"native visual follows stable attached target-local anchor");
        Check(Calls("BastionUnit","OnDestroy").Contains("BastionUpgradeEffects::DroneLost"),"emergency rebuild runs from real destruction after normal construction setup, not corpse polling");
        Check(!Calls("BastionServerSpiderMine","Leap").Contains("CharacterBody::AddTimedBuff")&&Calls("BastionServerSpiderMine","Explode").Contains("CharacterBody::AddTimedBuff"),"attachment never slows; only confirmed blast applies native Slow50");
        Check((int)BastionMinePhase.Leaping==3&&(int)BastionMinePhase.Attached==4&&(int)BastionMinePhase.Primed==5&&(int)BastionMinePhase.Armed==6,"new ground state preserves all prior mine packet ordinals");
        var utilityType=assembly.MainModule.Types.Single(t=>t.Name=="BastionUtilitySkillDef");
        var stockMethod=utilityType.Methods.Single(m=>m.Name=="GetMaxStock");
        Check(stockMethod.Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="baseMaxStock"),"native max stock uses each form's configured SkillDef capacity");
        Check(!stockMethod.Body.Instructions.Any(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Ldc_I4_1||i.OpCode==Mono.Cecil.Cil.OpCodes.Ldc_I4_2),"native max stock cannot silently hard-code one or two across both forms");
        Check(Calls("BastionUtilitySkillDef","OnFixedUpdate").Contains("GenericSkill::OverrideMaxStock"),"native UI stock follows authoritative ledger capacity including items");
        var content=assembly.MainModule.Types.Single(t=>t.Name=="BastionContent");
        var utilityCalls=content.Methods.Single(m=>m.Name=="Create").Body.Instructions.Where(i=>i.Operand is MethodReference m&&m.Name=="UtilitySkill").ToArray();
        Check(utilityCalls.Length==2,"both native utility definitions registered");
        foreach(var call in utilityCalls)
        {
            var stock=call.Previous;var cooldown=stock.Previous;var token=cooldown.Previous.Previous;
            bool rush=Equals(token.Operand,"WINGBURST"),mines=Equals(token.Operand,"MINES");
            Check(rush||mines,"native utility metadata identified by exact token");
            Check(stock.OpCode==(rush?Mono.Cecil.Cil.OpCodes.Ldc_I4_2:Mono.Cecil.Cil.OpCodes.Ldc_I4_1),"native form metadata has two Rush stocks and one mine stock");
            Check(cooldown.OpCode==Mono.Cecil.Cil.OpCodes.Ldc_R4&&(float)cooldown.Operand==(rush?10f:24f),"native form metadata has ten/twenty-four-second recharge");
        }
        var utilityDefinition=content.Methods.Single(m=>m.Name=="UtilitySkill");
        Check(utilityDefinition.Body.Instructions.Any(i=>i.OpCode==Mono.Cecil.Cil.OpCodes.Stfld&&i.Operand is FieldReference f&&f.Name=="baseMaxStock"&&i.Previous.OpCode==Mono.Cecil.Cil.OpCodes.Ldarg_3),"native utility stores caller stock argument rather than a shared constant");
        var minePose=Calls("BastionSpiderVisual","Update");Check(minePose.Contains("BastionMinePresentation::Hip")&&minePose.Contains("BastionMinePresentation::Knee"),"native mine animation uses tested shared deployment pose");Check(Calls("BastionMineLaunchers","LateUpdate").Contains("BastionMinePresentation::HatchAngle"),"native launchers use tested upward hinge geometry");
        Check(new[]{"Bones","Model"}.All(f=>assembly.MainModule.Types.Single(t=>t.Name=="BastionMineLaunchers").Fields.Single(x=>x.Name==f).IsPublic),"mine launcher rig references are Unity-serialized, so instantiated body/display prefabs keep them");
        Check(Calls("BastionAnimation","LateUpdate").Contains("BastionUtility::get_Duration"),"deployment pose uses the entire accepted mine salvo rather than the unupgraded count");
        var audio=Calls("BastionAudio","Cue");Check(audio.Contains("BastionDialogueGate::CanStart")&&audio.Contains("BastionDialogueGate::Started"),"all voices share actual audio gate");Check(Calls("BastionContent","UtilitySkill").Contains("BastionStateType::For"),"native skill identity retained");
    }
}
