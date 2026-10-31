using System;
using System.Linq;
using System.Reflection;
using IvoryBastion;
using RoR2;
using R2API.Networking;
using UnityEngine;
using UnityEngine.Networking;
static class BalanceSyncChecks
{
    static void Check(bool ok,string message)=>RuntimeChecks.Check(ok,"balance sync: "+message);
    static void Call(object target,string method)=>target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
    static Run RunAt(uint id,ulong seed)=>new(){netId=new(){Value=id},seed=seed};
    static BastionBalanceProfile Copy(BastionBalanceProfile profile)
    {var writer=new NetworkWriter();profile.Serialize(writer);var copy=new BastionBalanceProfile();copy.Deserialize(new NetworkReader(writer.Bytes()));return copy;}
    internal static void Run()
    {
        try
        {
            Time.unscaledTime=0;NetworkServer.active=true;NetworkClient.active=true;
            BastionBalanceConfig.Saved=BastionBalance.Defaults();BastionBalanceConfig.Saved[(int)BastionSetting.NeedleDamage]=150;
            BastionBalanceConfig.Saved[(int)BastionSetting.UpgradedMineCount]=33;
            BastionBalanceConfig.Saved[(int)BastionSetting.UpgradedWingburstShots]=48;
            var host=RunAt(17,923);RoR2.Run.instance=host;
            BastionBalanceProfile packet=null;int broadcasts=0;
            NetworkTestTransport.Deliver=(message,destination)=>{if(message is BastionBalanceProfile p){packet=Copy(p);broadcasts++;}};
            BastionBalanceRuntime.Install();BastionBalanceRuntime.Update();
            Check(packet!=null&&BastionBalance.Get(BastionSetting.NeedleDamage)==150,"host captures settings at run start and publishes them");
            BastionBalanceConfig.Saved[(int)BastionSetting.NeedleDamage]=300;
            Time.unscaledTime=1;BastionBalanceRuntime.Update();Check(packet.Values[(int)BastionSetting.NeedleDamage]==150,"editing settings cannot change the active host profile");
            var writer=new NetworkWriter();packet.Serialize(writer);Check(writer.Bytes().Length<1000,"complete gameplay profile fits below 1KB");
            var spoof=Copy(packet);spoof.Values[(int)BastionSetting.NeedleDamage]=500;spoof.OnReceived();Check(BastionBalance.Get(BastionSetting.NeedleDamage)==150,"host ignores client-sent gameplay profiles");
            for(int i=0;i<10;i++)new BastionBalanceRequest().OnReceived();Check(broadcasts==2,"client profile requests are rate limited independently of skill traffic");
            BastionBalanceRuntime.Remove();NetworkServer.active=false;RoR2.Run.instance=RunAt(17,923);BastionBalanceConfig.Saved[(int)BastionSetting.NeedleDamage]=900;
            int requests=0;NetworkTestTransport.Deliver=(message,destination)=>{if(message is BastionBalanceRequest)requests++;};
            BastionBalanceRuntime.Install();
            var wrongRun=Copy(packet);wrongRun.RunId=new(){Value=18};wrongRun.OnReceived();
            var wrongSeed=Copy(packet);wrongSeed.Seed=924;wrongSeed.OnReceived();
            var wrongSchema=Copy(packet);typeof(BastionBalanceProfile).GetField("schema",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(wrongSchema,999);wrongSchema.OnReceived();
            Check(BastionBalance.Get(BastionSetting.NeedleDamage)==80,"wrong run, seed and schema cannot install stale balance");
            var former=Copy(packet);typeof(BastionBalanceProfile).GetField("schema",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(former,3);former.Values=former.Values.Take(163).ToArray();former.OnReceived();
            Check(BastionBalance.Get(BastionSetting.NeedleDamage)==80&&BastionBalance.Int(BastionSetting.UpgradedMineCount)==30,"previous review schema3 cannot partially install its shorter profile");
            for(int i=0;i<4;i++){Time.unscaledTime=2+i*.6f;BastionBalanceRuntime.Update();}
            Check(requests==4,"a joining client retries profiles after lost requests/responses");
            packet.OnReceived();Check(BastionBalance.Get(BastionSetting.NeedleDamage)==150,"client uses the host profile");
            Check(BastionBalance.Int(BastionSetting.UpgradedMineCount)==33&&BastionBalance.Int(BastionSetting.UpgradedWingburstShots)==48,"new utility-upgrade fields survive host-profile serialization and install");
            Check(BastionBalanceConfig.Saved[(int)BastionSetting.NeedleDamage]==900,"sync never overwrites a client's saved preferences");
            var changed=Copy(packet);changed.Values[(int)BastionSetting.NeedleDamage]=800;changed.OnReceived();
            Check(BastionBalance.Get(BastionSetting.NeedleDamage)==150,"duplicate profiles cannot mutate balance mid-run");
            Time.unscaledTime=10;BastionBalanceRuntime.Update();Check(requests==4,"confirmed clients stop requesting profiles");
            RoR2.Run.EndRun(RoR2.Run.instance);Check(BastionBalance.Get(BastionSetting.NeedleDamage)==80,"leaving a run clears the previous host's profile");
            packet.OnReceived();Check(BastionBalance.Get(BastionSetting.NeedleDamage)==80,"late packets after run destruction are ignored");
            BastionBalanceRuntime.Remove();RoR2.Run.instance=null;BastionBalanceRuntime.Install();packet.OnReceived();
            RoR2.Run.StartRun(RunAt(17,923));Time.unscaledTime=20;BastionBalanceRuntime.Update();packet.OnReceived();
            Check(BastionBalance.Get(BastionSetting.NeedleDamage)==150,"a profile lost before Run spawn is recovered after Run initialization");
            BastionBalanceRuntime.Remove();NetworkServer.active=true;BastionBalanceConfig.Saved=BastionBalance.Defaults();BastionBalanceConfig.Saved[(int)BastionSetting.NeedleDamage]=275;RoR2.Run.instance=RunAt(19,925);BastionBalanceRuntime.Install();
            Check(BastionBalance.Get(BastionSetting.NeedleDamage)==275,"hosting a later run uses local saved settings, not the previous host's values");
            var values=BastionBalance.Snapshot();values[(int)BastionSetting.BaseHealth]=250;values[(int)BastionSetting.MoveSpeed]=9;values[(int)BastionSetting.SprintSpeed]=15;values[(int)BastionSetting.HealthGrowth]=42;BastionBalance.Apply(values);
            var body=new CharacterBody();var balance=new BastionBalanceBody();var components=new Components();components.Add(balance);components.Items.Add(body);Call(balance,"Awake");Call(balance,"OnEnable");Call(balance,"Start");
            Check(body.baseMaxHealth==250&&body.healthComponent.health==250&&body.levelMaxHealth==42,"new bodies apply configured health/growth while preserving a full spawn");
            Check(body.baseMoveSpeed==9&&Math.Abs(body.sprintingSpeedMultiplier-15f/9)<.001f&&!body.autoCalculateLevelStats,"movement uses host values and growth is not overwritten by native auto-calculation");
            Call(balance,"OnDisable");
        }
        finally
        {
            BastionBalanceRuntime.Remove();BastionBalance.Reset();BastionBalanceConfig.Saved=BastionBalance.Defaults();RoR2.Run.instance=null;NetworkTestTransport.Deliver=null;
        }
    }
}
