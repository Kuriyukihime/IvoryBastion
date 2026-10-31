using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using IvoryBastion;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
static class RuntimeChecks
{
    internal static int Count;
    internal static void Check(bool ok,string message){Count++;if(!ok)throw new Exception("Networking: "+message);}
    static void Tick(BastionRequests requests,float seconds=.02f)
    {Time.fixedDeltaTime=seconds;Time.unscaledTime+=seconds;Time.time+=seconds;typeof(BastionRequests).GetMethod("FixedUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(requests,null);}
    sealed class Actor
    {
        internal BastionController C=new();internal BastionRequests Requests=new();internal EntityStateMachine Machine=new(){customName="BastionRequests"};internal BastionUtility Utility=new();
        internal Actor(bool authority,uint id=1)
        {
            var components=new Components();components.Add(C);components.Add(Requests);components.Add(Machine);components.Add(Utility);
            C.Body.hasEffectiveAuthority=authority;C.Body.netId=new(){Value=id};
            typeof(BastionRequests).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(Requests,null);
        }
    }
    static BastionRequest Command(uint id,BastionRequestKind kind=BastionRequestKind.Pistol,bool flag=false,uint token=0)=>new(){Id=id,Kind=kind,Flag=flag,Token=token,Direction=Vector3.forward};
    static BastionRequestBatch Copy(BastionRequestBatch batch)
    {var writer=new NetworkWriter();batch.OnSerialize(writer);var copy=new BastionRequestBatch();copy.OnDeserialize(new NetworkReader(writer.Bytes()));return copy;}
    static BastionStatus Copy(BastionStatus status)
    {var writer=new NetworkWriter();status.Serialize(writer);var copy=new BastionStatus();copy.Deserialize(new NetworkReader(writer.Bytes()));return copy;}
    static void Main(string[] args)
    {
        if(args.Length>1&&args[0]=="--delivery-edge"){DeliveryEdges(args[1]);return;}
        WindowsAndOutbox();RuntimeAbilities();LossAndReordering();SnapshotsAndSerialization();
        DeliveryEdges("all");
        CriticalTraffic();
        UtilityDelivery();
        NativeChecks.Run(Path.GetFullPath(args[0]));
        BalanceSyncChecks.Run();
        Console.WriteLine($"PASS: {Count} networking checks (actual request/status source, packet-loss simulation and installed native transport contracts).");
    }
    static void UtilityDelivery()
    {
        foreach(var kind in new[]{BastionRequestKind.Wingburst,BastionRequestKind.Mines})
        {
            NetworkServer.active=true;Time.time=Time.unscaledTime=0;var actor=new Actor(false);actor.C.Sieged=kind==BastionRequestKind.Mines;
            var command=Command(1,kind);actor.Requests.Receive(new[]{command,command});Tick(actor.Requests);actor.Requests.Receive(new[]{command});Tick(actor.Requests);
            Check(actor.Utility.Casts==1&&actor.Utility.Reserve.Stock==1,"utility retry spends exactly once: "+kind);
            Check(actor.Utility.LastSiege==actor.C.Sieged,"utility dispatch preserves intended form");
            actor.Utility.Reserve.Stock=0;actor.Utility.Reserve.Remaining=.15f;actor.Requests.Receive(new[]{Command(2,kind)});Tick(actor.Requests);
            Check(actor.Utility.Casts==1,"near-ready utility waits rather than discarding input");actor.Utility.Reserve.Tick(.2f);Tick(actor.Requests);
            Check(actor.Utility.Casts==2,"near-ready utility executes once after recharge");
            var box=new BastionRequestOutbox();for(int i=0;i<100;i++)box.Enqueue(Command(0),0);uint id=box.Enqueue(Command(0,kind),0);
            Check(box.Batch(0)[0].Id==id,"utility takes priority over primary backlog");
        }
    }
    static void WindowsAndOutbox()
    {
        var window=new BastionRequestWindow();Check(!window.Accept(0),"zero request rejected");
        Check(window.Accept(10)&&window.Accept(8)&&!window.Accept(10)&&!window.Accept(8),"reordering accepted once; duplicates rejected");
        for(uint id=11;id<10000;id++)Check(window.Accept(id),"continuous request stream");
        Check(!window.Accept(1),"an ancient retry cannot replay after leaving the window");
        window=new();Check(window.Accept(uint.MaxValue-1)&&window.Accept(uint.MaxValue)&&window.Accept(1)&&window.Accept(2)&&!window.Accept(uint.MaxValue),"sequence wrap preserves deduplication");
        uint next=uint.MaxValue;Check(BastionRequestRules.Next(ref next)==1,"sequence skips zero");
        var snapshots=new BastionSnapshotSequence();Check(snapshots.Accept(uint.MaxValue)&&snapshots.Accept(1)&&!snapshots.Accept(uint.MaxValue)&&!snapshots.Accept(0),"snapshot wrap and stale rejection");
        var box=new BastionRequestOutbox();for(int i=0;i<80;i++)box.Enqueue(Command(0),0);
        uint important=box.Enqueue(Command(0,BastionRequestKind.Aegis),0);
        var first=box.Batch(0);Check(first.Length==24&&first[0].Id==important,"critical cast takes priority over primary backlog");
        var sent=new HashSet<uint>(first.Select(r=>r.Id));foreach(var r in box.Batch(.05f))sent.Add(r.Id);foreach(var r in box.Batch(.10f))sent.Add(r.Id);foreach(var r in box.Batch(.15f))sent.Add(r.Id);
        Check(sent.Count==81,"all pending shots can transmit before the first ACK; no RTT cap");
        box.Acknowledge(sent.ToArray());Check(box.Count==0,"selective ACK clears every delivered command");
        box.Enqueue(Command(0,BastionRequestKind.Boost,true),0);box.Enqueue(Command(0,BastionRequestKind.Boost,false),.01f);
        Check(box.Count==1&&!box.Batch(.01f)[0].Flag,"release supersedes unsent held boost");
        Check(!box.Expire(9),"expired boost does not spam cast-failure notices");box.Enqueue(Command(0,BastionRequestKind.Transform),10);Check(box.Expire(18)&&box.Count==0,"lost critical request expires visibly and releases its queue");
        box=new();for(int i=0;i<BastionRequestRules.PendingLimit;i++)box.Enqueue(Command(0),0);
        Check(box.Enqueue(Command(0,BastionRequestKind.Skyfall),0)!=0&&box.Count==BastionRequestRules.PendingLimit,"saturated primary queue makes room for ultimate");
        foreach(float f in new[]{float.NaN,float.PositiveInfinity,-1f,1f})Check(!BastionRequestRules.NearReady(f),"grace rejects invalid or long cooldown");
        Check(BastionRequestRules.NearReady(.35f)&&BastionRequestRules.NearReady(0),"bounded near-ready input grace");
    }
    static void RuntimeAbilities()
    {
        NetworkServer.active=true;Time.unscaledTime=0;
        var a=new Actor(false);var transform=Command(1,BastionRequestKind.Transform,true,1);
        a.Requests.Receive(new[]{transform,transform,transform});
        // The visual Weapon state may exit or change immediately; the actual
        // server job must still complete without any further state callbacks.
        for(int i=0;i<60;i++)Tick(a.Requests);
        Check(a.C.Sieged&&a.C.ModeChanges==1&&a.C.Transforms==1&&!a.C.Deploying,"one accepted transform completes independently of presentation");
        a.Requests.Receive(new[]{transform});Tick(a.Requests);Check(a.C.ModeChanges==1&&a.C.TransformSync.HasReply(1),"transform resend neither toggles back nor spends twice");
        a.C.Bank.ProtocolCharges=0;a.C.Bank.Protocol=.25f;
        a.Requests.Receive(new[]{Command(2,BastionRequestKind.Aegis)});
        Tick(a.Requests);Check(a.C.Secondary==0,"near-ready cast waits rather than bypasses cooldown");
        a.C.Bank.Protocol=0;a.C.Bank.ProtocolCharges=1;
        Tick(a.Requests);a.Requests.Receive(new[]{Command(2,BastionRequestKind.Aegis)});Tick(a.Requests);
        Check(a.C.Secondary==1&&a.C.Bank.ProtocolCharges==0,"Aegis delivery and retries spend exactly once");
        a.Requests.Receive(new[]{Command(3,BastionRequestKind.Aegis)});Tick(a.Requests);Check(a.C.Secondary==1&&a.Requests.HostNotice=="ABILITY RECHARGING","long cooldown is rejected and reported");
        a.C.Bank.Fabrication=300;var purchase=Command(4,BastionRequestKind.Purchase);a.Requests.Receive(new[]{purchase,purchase});Tick(a.Requests);
        Check(a.C.Bank.Buys==1&&a.C.Bank.Fabrication==240,"purchase retries cannot spend fabrication twice");
        a.Requests.Receive(new[]{Command(5,BastionRequestKind.Skyfall),Command(5,BastionRequestKind.Skyfall)});Tick(a.Requests);
        Check(a.C.Special==1&&a.C.Bank.SaturationCharges==0,"ultimate retries launch once");
        a=new Actor(false);a.C.GroundResults.Enqueue(BastionTransformResult.Blocked);a.C.GroundResults.Enqueue(BastionTransformResult.TooHigh);
        a.Requests.Receive(new[]{Command(1,BastionRequestKind.Transform,true,1)});for(int i=0;i<65;i++)Tick(a.Requests);
        Check(a.C.Sieged&&a.C.Transforms==1,"late ground/position data gets a short retry without replaying the animation");
        a=new Actor(false);for(int i=0;i<100;i++)a.C.GroundResults.Enqueue(BastionTransformResult.TooHigh);
        a.Requests.Receive(new[]{Command(1,BastionRequestKind.Transform,true,1)});for(int i=0;i<70;i++)Tick(a.Requests);
        Check(!a.C.Sieged&&!a.C.Deploying&&a.C.TransformSync.Result==BastionTransformResult.TooHigh,"persistent deployment-height failure stays enforced and unlocks input");
        a=new Actor(false);a.C.Bank.Toggle=.25f;a.Requests.Receive(new[]{Command(1,BastionRequestKind.Transform,true,1)});Tick(a.Requests);
        Check(a.C.Transforms==0,"transform waits for its real host cooldown");a.C.Bank.Toggle=0;for(int i=0;i<55;i++)Tick(a.Requests);Check(a.C.Sieged,"near-ready transform eventually commits");
        a=new Actor(false);a.C.Initialized=false;a.Requests.Receive(new[]{Command(1)});Tick(a.Requests);Check(a.C.Shots==0,"early cast cannot use a temporary pre-ledger bank");a.C.Initialized=true;Tick(a.Requests);Check(a.C.Shots==1,"early cast survives ledger initialization");
        a=new Actor(false);a.Requests.Receive(new[]{Command(1,BastionRequestKind.Transform,true,1)});Tick(a.Requests);a.C.Alive=false;Tick(a.Requests);
        Check(a.C.ModeChanges==0&&!a.C.Deploying&&a.C.TransformSync.Result==BastionTransformResult.Unavailable,"death cancels accepted transformation safely");
        a=new Actor(true);a.C.BoostRequested=true;a.Requests.Submit(BastionRequestKind.Boost,flag:true);Tick(a.Requests);
        Check(a.C.BoostRequested&&a.C.BoostEffects==1,"host prediction still emits boost ignition once");
        a.Requests.Receive(new[]{Command(3,BastionRequestKind.Boost,false),Command(2,BastionRequestKind.Boost,true)});Tick(a.Requests);
        Check(!a.C.BoostRequested,"an old start cannot overwrite a newer release");
        a.Requests.Receive(new[]{Command(4,BastionRequestKind.Boost,true)});Tick(a.Requests);Tick(a.Requests,1.6f);
        Check(!a.C.BoostRequested,"boost lease clears a missing release/disconnected owner");
        a=new Actor(true);NetworkServer.active=false;a.Requests.Receive(new[]{Command(1)});Tick(a.Requests);Check(a.C.Shots==0,"clients cannot process server damage requests");
    }
    static void LossAndReordering()
    {
        Time.unscaledTime=0;var client=new Actor(true,71);var host=new Actor(false,71);Util.Bodies[71]=client.C;
        var random=new Random(92813);var packets=new List<(float At,bool Server,Action Deliver)>();int dropped=0,snapshots=0;
        client.Machine.Deliver=state=>
        {
            var batch=Copy((BastionRequestBatch)state);
            if(dropped++<5||random.NextDouble()<.30)return;
            packets.Add((Time.unscaledTime+.06f+(float)random.NextDouble()*.50f,true,()=>host.Requests.Receive(batch.Requests)));
            if(random.NextDouble()<.30)packets.Add((Time.unscaledTime+.6f,true,()=>host.Requests.Receive(batch.Requests)));
        };
        const int shots=240;int issued=0;float nextShot=0,nextStatus=0;
        for(int frame=0;frame<650;frame++)
        {
            float now=frame*.02f;Time.unscaledTime=now;NetworkServer.active=false;
            while(issued<shots&&now>=nextShot){client.Requests.Submit(BastionRequestKind.Pistol,Vector3.forward);issued++;nextShot+=.01f;}
            Tick(client.Requests,0); // Drive the real flush without advancing the global simulation clock.
            foreach(var packet in packets.Where(p=>p.At<=now).OrderByDescending(p=>p.At).ToArray())
            {packets.Remove(packet);NetworkServer.active=packet.Server;packet.Deliver();}
            NetworkServer.active=true;Time.unscaledTime=now;Tick(host.Requests,.02f);
            if(now>=nextStatus)
            {
                nextStatus=now+.2f;var status=Copy(new BastionStatus(host.C));
                if(snapshots++>3&&random.NextDouble()>.30)packets.Add((now+.08f+(float)random.NextDouble()*.5f,false,status.OnReceived));
            }
        }
        Check(issued==shots&&host.C.Shots==shots,"240 primary attacks arrive exactly once through initial send failures, 30% loss, duplicates and reordering");
        var box=(BastionRequestOutbox)typeof(BastionRequests).GetField("outbox",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(client.Requests);
        Check(box.Count==0,"ACK loss and ACK history rollover still drain the client queue");
        // Drop the native presentation state entirely: a separately retried
        // transform request must reach the host, commit, and release its owner.
        NetworkServer.active=false;uint token=client.C.TransformSync.NextRequest();client.Requests.Submit(BastionRequestKind.Transform,flag:true,token:token);dropped=0;
        for(int frame=0;frame<300;frame++)
        {
            float now=13+frame*.02f;Time.unscaledTime=now;NetworkServer.active=false;Tick(client.Requests,0);
            foreach(var packet in packets.Where(p=>p.At<=now).OrderByDescending(p=>p.At).ToArray()){packets.Remove(packet);NetworkServer.active=packet.Server;packet.Deliver();}
            NetworkServer.active=true;Time.unscaledTime=now;Tick(host.Requests,.02f);
            if(frame%10==0){var status=Copy(new BastionStatus(host.C));packets.Add((now+.20f,false,status.OnReceived));}
        }
        Check(host.C.ModeChanges==1&&client.C.Sieged&&client.C.TransformSync.CanRelease(token,true,client.C.Sieged),"lost presentation and first five request sends cannot strand client transformation");
    }
    static void DeliveryEdges(string which)
    {
        if(which=="pause"||which=="all")
        {
            NetworkServer.active=false;Time.unscaledTime=Time.time=0;
            var actor=new Actor(true);int batches=0;actor.Machine.Deliver=_=>batches++;
            actor.Requests.Submit(BastionRequestKind.Aegis);
            Time.unscaledTime=30;Tick(actor.Requests,0);
            Check(batches==1,"an unsent ability survives a thirty-second game pause and is sent on resume");
        }
        if(which=="queue"||which=="all")
        {
            NetworkServer.active=true;Time.unscaledTime=Time.time=0;var actor=new Actor(false);
            for(uint id=1;id<=BastionRequestRules.PendingLimit;id+=BastionRequestRules.BatchSize)
                actor.Requests.Receive(Enumerable.Range(0,Math.Min(BastionRequestRules.BatchSize,BastionRequestRules.PendingLimit-(int)id+1))
                    .Select(i=>Command(id+(uint)i,BastionRequestKind.Missile)).ToArray());
            // A delayed burst filled the host's queue while it still knew the
            // old form. The form has caught up before the critical command.
            actor.C.Sieged=true;actor.Requests.Receive(new[]{Command(513,BastionRequestKind.Aegis)});Tick(actor.Requests);
            Check(actor.C.Secondary==1,"a full host primary queue cannot discard an arriving Aegis request");
        }
        if(which=="form"||which=="all")
        {
            var values=BastionBalance.Defaults();values[(int)BastionSetting.DeployDuration]=3;values[(int)BastionSetting.PackDuration]=3;
            BastionBalance.Apply(values);
            try
            {
                foreach(bool siege in new[]{true,false})
                {
                    NetworkServer.active=true;Time.unscaledTime=Time.time=0;var actor=new Actor(false);actor.C.Sieged=!siege;
                    actor.Requests.Receive(new[]{Command(1,BastionRequestKind.Transform,siege,1),Command(2,siege?BastionRequestKind.Aegis:BastionRequestKind.Redline)});
                    for(int frame=0;frame<180;frame++)Tick(actor.Requests);
                    Check(siege?actor.C.Secondary==1:actor.C.Special==1,"queued ability survives a configured three-second "+(siege?"deployment":"packing"));
                }
            }
            finally{BastionBalance.Reset();}
        }
        if(which=="cooldown"||which=="all")
        {
            NetworkServer.active=true;Time.unscaledTime=Time.time=0;var actor=new Actor(false);
            actor.C.Bank.ProtocolCharges=0;actor.C.Bank.Protocol=.2f;
            actor.Requests.Receive(new[]{Command(1,BastionRequestKind.Transform,true,1),Command(2,BastionRequestKind.Aegis)});
            for(int frame=0;frame<90;frame++)
            {
                if(actor.C.Sieged&&actor.C.Bank.ProtocolCharges==0)
                {
                    actor.C.Bank.Protocol=Math.Max(0,actor.C.Bank.Protocol-.02f);
                    if(actor.C.Bank.Protocol==0)actor.C.Bank.ProtocolCharges=1;
                }
                Tick(actor.Requests);
            }
            Check(actor.C.Secondary==1,"time waiting for a form change does not consume near-ready cooldown grace");
        }
        if(which=="ownership"||which=="all")
        {
            foreach(bool localHost in new[]{false,true})
            {
                NetworkServer.active=true;Time.time=Time.unscaledTime=0;var host=new Actor(false,91);
                host.Requests.Receive(Enumerable.Range(1,5).Select(id=>Command((uint)id)).ToArray());Tick(host.Requests);
                host.C.Sieged=true;
                if(localHost){host.C.Body.hasEffectiveAuthority=true;host.Requests.Submit(BastionRequestKind.Aegis);}
                else
                {
                    var status=Copy(new BastionStatus(host.C));var client=new Actor(false,91);Util.Bodies[91]=client.C;
                    NetworkServer.active=false;status.OnReceived();client.C.Body.hasEffectiveAuthority=true;
                    client.Machine.Deliver=state=>{NetworkServer.active=true;host.Requests.Receive(Copy((BastionRequestBatch)state).Requests);NetworkServer.active=false;};
                    client.Requests.Submit(BastionRequestKind.Aegis);Tick(client.Requests);
                }
                NetworkServer.active=true;Tick(host.Requests);
                Check(host.C.Secondary==1,"new "+(localHost?"host":"client")+" owner does not reuse another owner's request IDs");
            }
        }
    }
    static void CriticalTraffic()
    {
        foreach(double loss in new[]{.3,.6})for(int seed=0;seed<8;seed++)
        {
            Time.time=Time.unscaledTime=0;uint owner=(uint)(200+seed);
            var client=new Actor(true,owner);var host=new Actor(false,owner);
            client.C.Sieged=host.C.Sieged=true;host.C.Bank.Ammo=10000;host.C.Bank.Fabrication=500;host.C.Bank.Ranks[2]=2;
            Util.Bodies[owner]=client.C;
            var random=new Random(seed+3200);var packets=new List<(float At,bool Server,Action Deliver)>();int sends=0;
            client.Machine.Deliver=state=>
            {
                var batch=Copy((BastionRequestBatch)state);
                if(sends++<5||random.NextDouble()<loss)return;
                packets.Add((Time.unscaledTime+.05f+(float)random.NextDouble()*.75f,true,()=>host.Requests.Receive(batch.Requests)));
                if(random.NextDouble()<.4)packets.Add((Time.unscaledTime+.8f,true,()=>host.Requests.Receive(batch.Requests)));
            };
            NetworkServer.active=false;
            for(int i=0;i<240;i++)client.Requests.Submit(BastionRequestKind.Missile,Vector3.forward);
            client.Requests.Submit(BastionRequestKind.Aegis);
            client.Requests.Submit(BastionRequestKind.Skyfall);
            client.Requests.Submit(BastionRequestKind.Purchase,branch:0,choice:0);
            client.Requests.Submit(BastionRequestKind.Loadout,branch:0,choice:2);
            for(int frame=0;frame<650;frame++)
            {
                float now=frame*.02f;Time.time=Time.unscaledTime=now;NetworkServer.active=false;Tick(client.Requests,0);
                foreach(var packet in packets.Where(p=>p.At<=now).OrderByDescending(p=>p.At).ToArray())
                {packets.Remove(packet);NetworkServer.active=packet.Server;packet.Deliver();}
                NetworkServer.active=true;Time.time=Time.unscaledTime=now;Tick(host.Requests);
                if(frame%10==0&&random.NextDouble()>=loss)
                {
                    var status=Copy(new BastionStatus(host.C));
                    packets.Add((now+.05f+(float)random.NextDouble()*.75f,false,status.OnReceived));
                }
            }
            Check(host.C.Secondary==1&&host.C.Special==1,"Aegis and Skyfall execute exactly once under "+loss+" bidirectional packet loss");
            Check(host.C.Bank.Buys==1&&host.C.Bank.Fabrication==440&&host.C.Bank.Loadout[0]==2,"purchase and drone command survive primary traffic, loss and duplicates");
            Check(host.C.Shots==240,"primary traffic is delivered without duplicates alongside critical abilities");
            var box=(BastionRequestOutbox)typeof(BastionRequests).GetField("outbox",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(client.Requests);
            Check(box.Count==0,"mixed-action receipts drain despite dropped and reordered status updates");
        }
    }
    static void SnapshotsAndSerialization()
    {
        NetworkServer.active=true;var host=new Actor(false,81);host.C.Bank.Ammo=17;var before=Copy(new BastionStatus(host.C));
        host.C.Sieged=true;host.C.Bank.Ammo=9;host.C.Bank.Thruster.Fuel=1.2f;host.C.AbilityShield.Receive(50,23);host.C.AegisBarrier.Receive(40,31);
        host.C.TransformSync.ReceiveReply(1,BastionTransformResult.Applied);var after=Copy(new BastionStatus(host.C));
        var client=new Actor(true,81);Util.Bodies[81]=client.C;NetworkServer.active=false;
        after.OnReceived();before.OnReceived();after.OnReceived();
        Check(client.C.Sieged&&client.C.Applies==1&&client.C.Bank.Ammo==9&&client.C.AbilityShield.Charge==23,"stale/duplicate status cannot blink form or rewind resources/shield");
        Check(client.C.AegisBarrier.Capacity==40&&client.C.AegisBarrier.Charge==31,"Aegis capacity and damage survive serialization and stale/duplicate snapshots");
        Check(client.C.TransformSync.CanRelease(1,true,true)&&client.C.Bank.Thruster.Fuel==1.2f,"receipt follows installed form and full bank serialization");
        var requests=Enum.GetValues<BastionRequestKind>().Select((k,i)=>new BastionRequest{Id=(uint)i+1,Kind=k,Direction=new(1,2,3),Token=89,Branch=2,Choice=1,Flag=true}).ToArray();
        var copy=Copy(new BastionRequestBatch{Requests=requests});
        Check(copy.Requests.Length==requests.Length&&copy.Requests.Select((r,i)=>r.Id==requests[i].Id&&r.Kind==requests[i].Kind&&r.Token==89&&r.Direction.z==3&&r.Branch==2&&r.Choice==1&&r.Flag).All(x=>x),"all request fields round-trip for every action");
        var writer=new NetworkWriter();new BastionRequestBatch{Requests=Enumerable.Repeat(requests[0],100).ToArray()}.OnSerialize(writer);
        Check(writer.Bytes().Length<1000&&Copy(new BastionRequestBatch{Requests=Enumerable.Repeat(requests[0],100).ToArray()}).Requests.Length==24,"bounded native batches fit below 1KB");
    }
}
