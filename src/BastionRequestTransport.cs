using System;
using System.Collections.Generic;
using UnityEngine;
namespace IvoryBastion
{
    internal enum BastionRequestKind:byte { Pistol,Laser,Missile,Aegis,Redline,Skyfall,Transform,Boost,Purchase,Loadout,Wingburst,Mines }
    internal struct BastionRequest
    {
        internal uint Id,Token;
        internal BastionRequestKind Kind;
        internal Vector3 Direction;
        internal int Branch,Choice;
        internal bool Flag;
        internal bool Shot=>Kind<=BastionRequestKind.Missile;
        internal bool Siege=>Kind==BastionRequestKind.Laser||Kind==BastionRequestKind.Missile||Kind==BastionRequestKind.Aegis||Kind==BastionRequestKind.Skyfall||Kind==BastionRequestKind.Purchase||Kind==BastionRequestKind.Loadout||Kind==BastionRequestKind.Mines;
    }
    // This is the application-level receipt/retry layer. Transport remains the
    // game's native, ownership-checked NetworkStateMachine channel.
    internal static class BastionRequestRules
    {
        internal const int BatchSize=24,PendingLimit=512,AckCount=128;
        internal const float Retry=.18f,Flush=.05f,Timeout=8,InputGrace=.35f,FormGrace=1.6f,GroundGrace=.4f,BoostHeartbeat=.35f,BoostLease=1.5f;
        internal static bool Newer(uint a,uint b)=>unchecked((int)(a-b))>0;
        internal static uint Next(ref uint value){value=unchecked(value+1);if(value==0)value=1;return value;}
        internal static bool NearReady(float remaining)=>!float.IsNaN(remaining)&&!float.IsInfinity(remaining)&&remaining<=InputGrace&&remaining>=0;
    }
    internal sealed class BastionRequestWindow
    {
        private readonly uint[] seen=new uint[4096];
        private uint newest;
        internal bool Accept(uint id)
        {
            if(id==0)return false;
            if(newest==0||BastionRequestRules.Newer(id,newest))newest=id;
            else if(unchecked(newest-id)>=seen.Length)return false;
            int slot=(int)(id%seen.Length);
            if(seen[slot]==id)return false;
            seen[slot]=id;return true;
        }
    }
    internal sealed class BastionRequestAcks
    {
        private readonly List<uint> ids=new List<uint>();
        internal void Add(uint id){if(id==0)return;ids.Remove(id);ids.Add(id);if(ids.Count>BastionRequestRules.AckCount)ids.RemoveAt(0);}
        internal uint[] Snapshot()=>ids.ToArray();
    }
    internal sealed class BastionRequestOutbox
    {
        private sealed class Entry{internal BastionRequest Request;internal float Created,Sent=float.NegativeInfinity;}
        private readonly List<Entry> entries=new List<Entry>();
        private uint sequence;private int cursor;
        internal int Count=>entries.Count;
        internal uint Enqueue(BastionRequest request,float now)
        {
            // A new held/released intent supersedes old boost heartbeats. The
            // host also sequence-checks them, including copies already in flight.
            if(request.Kind==BastionRequestKind.Boost)entries.RemoveAll(e=>e.Request.Kind==BastionRequestKind.Boost);
            if(entries.Count>=BastionRequestRules.PendingLimit)
            {
                int oldShot=entries.FindIndex(e=>e.Request.Shot);
                if(oldShot<0)return 0;
                entries.RemoveAt(oldShot);
            }
            request.Id=BastionRequestRules.Next(ref sequence);
            entries.Add(new Entry{Request=request,Created=now});return request.Id;
        }
        internal void Acknowledge(uint[] ids)
        {
            if(ids==null||ids.Length==0)return;
            // A body can acquire a new input owner. Seed that owner's sequence
            // from host receipts instead of reusing IDs already deduplicated.
            foreach(uint id in ids)if(id!=0&&(sequence==0||BastionRequestRules.Newer(id,sequence)))sequence=id;
            var received=new HashSet<uint>(ids);entries.RemoveAll(e=>received.Contains(e.Request.Id));
        }
        internal bool Expire(float now)
        {
            bool important=false;
            entries.RemoveAll(e=>{if(now-e.Created<BastionRequestRules.Timeout)return false;important|=!e.Request.Shot&&e.Request.Kind!=BastionRequestKind.Boost;return true;});
            return important;
        }
        internal BastionRequest[] Batch(float now)
        {
            var batch=new List<BastionRequest>(BastionRequestRules.BatchSize);
            // Critical actions are never trapped behind a stream of primary fire.
            for(int i=0;i<entries.Count&&batch.Count<BastionRequestRules.BatchSize;i++)
            {
                var e=entries[i];if(e.Request.Shot||now-e.Sent<BastionRequestRules.Retry)continue;
                e.Sent=now;batch.Add(e.Request);
            }
            // Rotate through outstanding shots rather than waiting for the first
            // batch's receipts: high attack speed must not be capped by latency.
            int count=entries.Count;if(count>0)cursor%=count;
            for(int i=0;i<count&&batch.Count<BastionRequestRules.BatchSize;i++)
            {
                var e=entries[cursor];cursor=(cursor+1)%count;
                if(!e.Request.Shot||now-e.Sent<BastionRequestRules.Retry)continue;
                e.Sent=now;batch.Add(e.Request);
            }
            return batch.ToArray();
        }
    }
    internal sealed class BastionSnapshotSequence
    {
        private uint sequence;
        internal bool Accept(uint id){if(id==0||sequence!=0&&!BastionRequestRules.Newer(id,sequence))return false;sequence=id;return true;}
    }
}
