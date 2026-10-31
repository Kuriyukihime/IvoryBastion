using System;
using System.Collections.Generic;
using System.Linq;
using EntityStates;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
namespace IvoryBastion
{
    // A dedicated state machine carries immutable command batches. It never
    // shares a weapon/animation state, so ending an animation cannot cancel a cast.
    public sealed class BastionRequestBatch:EntityState
    {
        internal BastionRequest[] Requests=Array.Empty<BastionRequest>();
        public override void OnEnter(){base.OnEnter();if(NetworkServer.active)GetComponent<BastionRequests>()?.Receive(Requests);}
        public override void OnSerialize(NetworkWriter w)
        {
            base.OnSerialize(w);int count=Math.Min(Requests.Length,BastionRequestRules.BatchSize);w.Write((byte)count);
            for(int i=0;i<count;i++)
            {
                var r=Requests[i];w.Write(r.Id);w.Write((byte)r.Kind);w.Write(r.Direction);w.Write(r.Token);w.Write(r.Branch);w.Write(r.Choice);w.Write(r.Flag);
            }
        }
        public override void OnDeserialize(NetworkReader r)
        {
            base.OnDeserialize(r);int count=r.ReadByte();var requests=new List<BastionRequest>();
            for(int i=0;i<count;i++)
            {
                var request=new BastionRequest{Id=r.ReadUInt32(),Kind=(BastionRequestKind)r.ReadByte(),Direction=r.ReadVector3(),Token=r.ReadUInt32(),Branch=r.ReadInt32(),Choice=r.ReadInt32(),Flag=r.ReadBoolean()};
                if(i<BastionRequestRules.BatchSize)requests.Add(request);
            }
            Requests=requests.ToArray();
        }
    }
    public sealed class BastionRequests:MonoBehaviour
    {
        private sealed class Job{internal BastionRequest Request;internal float Age,StartedAt,CooldownWait;internal bool Started;}
        private BastionController c;private EntityStateMachine machine;
        private readonly BastionRequestOutbox outbox=new BastionRequestOutbox();
        private readonly BastionRequestWindow received=new BastionRequestWindow();
        private readonly BastionRequestAcks acks=new BastionRequestAcks();
        private readonly BastionSnapshotSequence snapshots=new BastionSnapshotSequence();
        private readonly List<Job> jobs=new List<Job>();
        private float nextFlush,nextBoost,boostLease;private bool localBoost,serverBoost;
        private uint snapshot,activeTransform,latestBoost,noticeSequence,receivedNotice;
        internal uint NoticeId {get;private set;}
        internal string HostNotice {get;private set;}="";
        internal uint NextSnapshot()=>BastionRequestRules.Next(ref snapshot);
        internal uint[] AckSnapshot()=>acks.Snapshot();
        internal bool AcceptSnapshot(uint id,uint[] delivered)
        {
            // Selective receipts are safe even in an old snapshot; bank/form data
            // are not. A delayed snapshot must never roll a transformation back.
            outbox.Acknowledge(delivered);return snapshots.Accept(id);
        }
        internal void ReceiveNotice(uint id,string notice)
        {
            if(!c)c=GetComponent<BastionController>();
            if(!c||!c.Body.hasEffectiveAuthority||id==0||receivedNotice!=0&&!BastionRequestRules.Newer(id,receivedNotice))return;
            receivedNotice=id;c.Notice=notice;c.NoticeUntil=Time.unscaledTime+2.5f;
        }
        private void Awake(){c=GetComponent<BastionController>();}
        internal uint Submit(BastionRequestKind kind,Vector3 direction=default,bool flag=false,uint token=0,int branch=0,int choice=0)
        {
            if(!c)c=GetComponent<BastionController>();
            if(!c||!c.Body||!c.Body.hasEffectiveAuthority)return 0;
            var request=new BastionRequest{Kind=kind,Direction=direction,Flag=flag,Token=token,Branch=branch,Choice=choice};
            if(NetworkServer.active)outbox.Acknowledge(acks.Snapshot());
            // Expiration follows active game time: a pause must not discard
            // an input before FixedUpdate has another opportunity to send it.
            uint id=outbox.Enqueue(request,Time.time);
            if(id==0){c.Notice="HOST REQUEST QUEUE FULL — TRY AGAIN";c.NoticeUntil=Time.unscaledTime+2;return 0;}
            if(NetworkServer.active){request.Id=id;Receive(new[]{request});outbox.Acknowledge(new[]{id});}
            return id;
        }
        internal void BoostIntent(bool held)
        {
            if(!c||!c.Body.hasEffectiveAuthority)return;
            c.BoostRequested=held;
            if(localBoost==held&&(!held||Time.unscaledTime<nextBoost))return;
            localBoost=held;nextBoost=Time.unscaledTime+BastionRequestRules.BoostHeartbeat;
            Submit(BastionRequestKind.Boost,flag:held);
        }
        internal bool AllowInput(BastionAction action)
        {
            if(NetworkServer.active||!c||!c.Body.hasEffectiveAuthority||!c.Alive||c.Deploying)return false;
            bool form=action==BastionAction.Transform||action==BastionAction.Laser&&c.Sieged||action==BastionAction.Protocol&&c.Sieged||action==BastionAction.Redline&&!c.Sieged||action==BastionAction.Saturation&&c.Sieged;
            return form&&BastionRequestRules.NearReady(c.Cooldown(action));
        }
        internal void Receive(BastionRequest[] requests)
        {
            if(!NetworkServer.active||!c||requests==null)return;
            c.EnsureInitialized();
            foreach(var request in requests.Take(BastionRequestRules.BatchSize))
            {
                if(request.Id==0||request.Kind>BastionRequestKind.Mines)continue;
                acks.Add(request.Id);
                if(!received.Accept(request.Id))continue;
                if(jobs.Count>=BastionRequestRules.PendingLimit)
                {
                    int shot=request.Shot?-1:jobs.FindIndex(j=>j.Request.Shot);
                    if(shot<0){Reject(request,"HOST BUSY — TRY AGAIN");continue;}
                    jobs.RemoveAt(shot);
                }
                jobs.Add(new Job{Request=request});
            }
        }
        private void FixedUpdate()
        {
            if(!c||!c.Body)return;
            if(NetworkServer.active)
            {
                if(!c.EnsureInitialized())return;float dt=Time.fixedDeltaTime;
                if(c.BoostRequested&&(Time.unscaledTime>boostLease||c.Sieged||c.Deploying||!c.Alive)){c.BoostRequested=false;serverBoost=false;}
                for(int i=0;i<jobs.Count;)
                {
                    var job=jobs[i];job.Age+=dt;
                    if(Process(job))jobs.RemoveAt(i);else i++;
                }
            }
            if(!c.Body.hasEffectiveAuthority||NetworkServer.active)return;
            if(outbox.Expire(Time.time)){c.Notice="NO HOST CONFIRMATION — TRY AGAIN";c.NoticeUntil=Time.unscaledTime+3;}
            if(Time.unscaledTime<nextFlush||outbox.Count==0)return;
            if(!machine)machine=GetComponents<EntityStateMachine>().FirstOrDefault(m=>m.customName=="BastionRequests");
            if(!machine)return;
            var batch=outbox.Batch(Time.unscaledTime);if(batch.Length==0)return;
            machine.SetNextState(new BastionRequestBatch{Requests=batch});nextFlush=Time.unscaledTime+BastionRequestRules.Flush;
        }
        private bool Process(Job job)
        {
            var r=job.Request;
            if(!c.Alive){if(r.Kind==BastionRequestKind.Transform)ResolveTransform(r,BastionTransformResult.Unavailable);return true;}
            if(r.Kind==BastionRequestKind.Transform)return Transform(job);
            if(r.Kind==BastionRequestKind.Boost)
            {
                if(latestBoost==0||BastionRequestRules.Newer(r.Id,latestBoost))
                {
                    latestBoost=r.Id;boostLease=Time.unscaledTime+BastionRequestRules.BoostLease;
                    bool start=r.Flag&&!c.Sieged&&!c.Deploying;
                    if(start&&!serverBoost)c.Emit(BastionFxKind.Boost,c.Body.corePosition,c.Body.inputBank.aimDirection);
                    c.BoostRequested=serverBoost=start;
                }
                return true;
            }
            if(c.Deploying||r.Siege!=c.Sieged)
            {
                float grace=BastionRequestRules.FormGrace;
                if(c.Deploying&&r.Siege==c.TransformTargetSiege)
                    grace+=BastionTransformSync.Duration(c.TransformTargetSiege)+BastionRequestRules.GroundGrace;
                if(job.Age<=grace)return false;
                Reject(r,"ABILITY UNAVAILABLE IN THIS FORM");return true;
            }
            float remaining=Remaining(r);
            if(remaining>0)
            {
                job.CooldownWait+=Time.fixedDeltaTime;
                if(BastionRequestRules.NearReady(remaining)&&job.CooldownWait<=BastionRequestRules.InputGrace+.15f)return false;
                Reject(r,"ABILITY RECHARGING");return true;
            }
            switch(r.Kind)
            {
                case BastionRequestKind.Pistol:case BastionRequestKind.Laser:case BastionRequestKind.Missile:
                    Vector3 direction=r.Direction;
                    if(!BastionCombat.Finite(direction)||direction.sqrMagnitude<.0001f)direction=c.Body.inputBank.aimDirection;
                    if(!BastionCombat.Finite(direction)||direction.sqrMagnitude<.0001f)direction=transform.forward;
                    c.Shoot((BastionAction)r.Kind,new Ray(c.Body.inputBank.aimOrigin,direction.normalized));break;
                case BastionRequestKind.Aegis:c.ActivateSecondary();break;
                case BastionRequestKind.Redline:case BastionRequestKind.Skyfall:c.ActivateSpecial();break;
                case BastionRequestKind.Wingburst:case BastionRequestKind.Mines:
                    var utility=c.GetComponent<BastionUtility>();
                    if(!utility)Reject(r,"UTILITY UNAVAILABLE");
                    else if(!utility.TryCast(r.Kind==BastionRequestKind.Mines,r.Direction,out var reason))Reject(r,reason);
                    break;
                case BastionRequestKind.Purchase:case BastionRequestKind.Loadout:
                    bool ok=r.Kind==BastionRequestKind.Loadout?c.Bank.SetUnit(r.Branch,r.Choice,c.Sieged):c.Bank.Buy(r.Branch,r.Choice,c.Sieged);
                    if(ok){c.Body.MarkAllStatsDirty();c.Emit(BastionFxKind.Upgrade,c.Body.corePosition,Vector3.up);c.SendStatus();}
                    break;
            }
            return true;
        }
        private float Remaining(BastionRequest r)
        {
            switch(r.Kind)
            {
                case BastionRequestKind.Laser:return c.Bank.Laser;
                case BastionRequestKind.Missile:return c.Bank.Ammo>=1?0:(1-c.Bank.Ammo)/Mathf.Max(.01f,c.Bank.AmmoRate(c.Sieged));
                case BastionRequestKind.Aegis:return c.Bank.ProtocolCharges>0?0:c.Bank.Protocol;
                case BastionRequestKind.Redline:return c.Bank.RedlineCharges>0?0:c.Bank.Redline;
                case BastionRequestKind.Skyfall:return c.Bank.SaturationCharges>0?0:c.Bank.Saturation;
                case BastionRequestKind.Wingburst:case BastionRequestKind.Mines:
                    var utility=c.GetComponent<BastionUtility>();
                    return utility?(utility.Active?.2f:utility.Reserve.Wait(utility.Cost)):0;
                default:return 0;
            }
        }
        private bool Transform(Job job)
        {
            var r=job.Request;
            if(r.Token==0){Reject(r,"");return true;}
            if(c.Sieged==r.Flag){ResolveTransform(r,BastionTransformResult.Applied);return true;}
            if(activeTransform!=0&&activeTransform!=r.Id)
            {
                if(job.Age<=BastionRequestRules.FormGrace)return false;
                ResolveTransform(r,BastionTransformResult.Interrupted);return true;
            }
            if(c.Bank.Toggle>0)
            {
                if(BastionRequestRules.NearReady(c.Bank.Toggle)&&job.Age<=BastionRequestRules.InputGrace+.15f)return false;
                ResolveTransform(r,BastionTransformResult.Cooldown);return true;
            }
            float duration=BastionTransformSync.Duration(r.Flag);
            if(!job.Started)
            {
                job.Started=true;job.StartedAt=job.Age;activeTransform=r.Id;
                c.TransformTargetSiege=r.Flag;c.Deploying=true;
                c.Emit(BastionFxKind.Transform,c.Body.footPosition,Vector3.up,null,0,r.Flag?duration:-duration);
            }
            if(job.Age-job.StartedAt<duration)return false;
            var result=c.SetSiege(r.Flag);
            if((result==BastionTransformResult.TooHigh||result==BastionTransformResult.Blocked)&&job.Age-job.StartedAt<duration+BastionRequestRules.GroundGrace)return false;
            ResolveTransform(r,result);return true;
        }
        private void ResolveTransform(BastionRequest r,BastionTransformResult result)
        {
            if(activeTransform==0||activeTransform==r.Id){activeTransform=0;c.Deploying=false;if(result!=BastionTransformResult.Applied)c.Model?.CancelTransform();}
            c.TransformSync.ReceiveReply(r.Token,result);c.SendStatus();
        }
        private void Reject(BastionRequest r,string notice)
        {
            if(r.Kind==BastionRequestKind.Transform){ResolveTransform(r,BastionTransformResult.Unavailable);return;}
            if(r.Shot||r.Kind==BastionRequestKind.Boost||string.IsNullOrEmpty(notice))return;
            NoticeId=BastionRequestRules.Next(ref noticeSequence);HostNotice=notice;c.SendStatus();
        }
    }
}
