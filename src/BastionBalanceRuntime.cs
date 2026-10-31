using System;
using R2API.Networking;
using R2API.Networking.Interfaces;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
namespace IvoryBastion
{
    internal static class BastionBalanceRuntime
    {
        private static Run current;
        private static bool confirmed;
        private static float nextRequest,nextBroadcast,repeatUntil;
        private static float[] hostValues;
        internal static void Install()
        {
            NetworkingAPI.RegisterMessageType<BastionBalanceProfile>();
            NetworkingAPI.RegisterMessageType<BastionBalanceRequest>();
            Run.onRunStartGlobal+=Begin;Run.onRunDestroyGlobal+=End;
            if(Run.instance)Begin(Run.instance);
        }
        internal static void Remove()
        {Run.onRunStartGlobal-=Begin;Run.onRunDestroyGlobal-=End;current=null;confirmed=false;hostValues=null;BastionBalance.Reset();}
        private static void Begin(Run run)
        {
            current=run;confirmed=NetworkServer.active;nextRequest=nextBroadcast=0;repeatUntil=Time.unscaledTime+10;
            if(NetworkServer.active){hostValues=BastionBalanceConfig.Capture();BastionBalance.Apply(hostValues);}
            else{hostValues=null;BastionBalance.Reset();}
            Applied();
        }
        private static void End(Run run)
        {if(current!=run)return;current=null;confirmed=false;hostValues=null;BastionBalance.Reset();BastionBalanceText.Refresh();}
        internal static void Update()
        {
            if(!Run.instance)return;
            if(current!=Run.instance)Begin(Run.instance);
            if(current.netId==NetworkInstanceId.Invalid)return;
            if(NetworkServer.active)
            {if(Time.unscaledTime<repeatUntil&&Time.unscaledTime>=nextBroadcast)Broadcast();}
            else if(NetworkClient.active&&!confirmed&&Time.unscaledTime>=nextRequest)
            {nextRequest=Time.unscaledTime+.5f;new BastionBalanceRequest().Send(NetworkDestination.Server);}
        }
        internal static void Broadcast()
        {
            if(!NetworkServer.active||!current||current!=Run.instance||hostValues==null||current.netId==NetworkInstanceId.Invalid||Time.unscaledTime<nextBroadcast)return;
            nextBroadcast=Time.unscaledTime+.5f;
            new BastionBalanceProfile{RunId=current.netId,Seed=current.seed,Values=hostValues}.Send(NetworkDestination.Clients);
        }
        internal static void Receive(NetworkInstanceId runId,ulong seed,int schema,float[] values)
        {
            // Immutable for this run: late/duplicate profiles cannot alter an
            // active ability. Pre-spawn packets can be dropped safely; a joining
            // client requests the profile repeatedly until it has a matching run.
            if(NetworkServer.active||confirmed||!current||current!=Run.instance||current.netId!=runId||current.seed!=seed||schema!=BastionBalance.Schema)return;
            if(!BastionBalance.Apply(values))return;
            confirmed=true;Applied();
        }
        private static void Applied()
        {BastionBalanceText.Refresh();BastionBalanceBody.RefreshAll();}
    }
    public sealed class BastionBalanceRequest:INetMessage
    {
        public void Serialize(NetworkWriter writer){}
        public void Deserialize(NetworkReader reader){}
        public void OnReceived(){if(NetworkServer.active)BastionBalanceRuntime.Broadcast();}
    }
    public sealed class BastionBalanceProfile:INetMessage
    {
        internal NetworkInstanceId RunId;internal ulong Seed;internal float[] Values;
        private int schema=BastionBalance.Schema;
        public void Serialize(NetworkWriter w)
        {
            w.Write(RunId);w.Write(Seed);w.Write((ushort)schema);int count=Values?.Length??0;w.Write((ushort)count);
            if(Values!=null)foreach(float value in Values)w.Write(value);
        }
        public void Deserialize(NetworkReader r)
        {
            RunId=r.ReadNetworkId();Seed=r.ReadUInt64();schema=r.ReadUInt16();int count=r.ReadUInt16();
            if(count>256){Values=null;return;}
            Values=new float[count];for(int i=0;i<count;i++)Values[i]=r.ReadSingle();
        }
        public void OnReceived(){if(!NetworkServer.active)BastionBalanceRuntime.Receive(RunId,Seed,schema,Values);}
    }
    public sealed class BastionBalanceBody:MonoBehaviour
    {
        private static readonly System.Collections.Generic.List<BastionBalanceBody> bodies=new System.Collections.Generic.List<BastionBalanceBody>();
        private CharacterBody body;private bool started,applied;
        private void Awake(){body=GetComponent<CharacterBody>();}
        private void OnEnable(){bodies.Add(this);}
        private void OnDisable(){bodies.Remove(this);}
        private void Start(){started=true;Apply();}
        private void FixedUpdate(){if(!applied)Apply();}
        internal static void RefreshAll(){foreach(var b in bodies.ToArray())if(b&&b.started)b.Apply();}
        private void Apply()
        {
            if(!body||!body.master)return;applied=true;
            var unit=GetComponent<BastionUnit>();
            if(unit){body.baseArmor=BastionUnitRules.Armor(unit.Kind);body.levelArmor=0;body.MarkAllStatsDirty();return;}
            float previous=body.maxHealth;
            body.autoCalculateLevelStats=false;
            body.baseMaxHealth=BastionBalance.Get(BastionSetting.BaseHealth);body.levelMaxHealth=BastionBalance.Get(BastionSetting.HealthGrowth);
            body.baseDamage=BastionBalance.Get(BastionSetting.BaseDamage);body.levelDamage=BastionBalance.Get(BastionSetting.DamageGrowth);
            body.baseRegen=BastionBalance.Get(BastionSetting.BaseRegen);body.levelRegen=BastionBalance.Get(BastionSetting.RegenGrowth);
            body.baseArmor=BastionBalance.Get(BastionSetting.BaseArmor);body.levelArmor=0;
            body.baseMoveSpeed=BastionBalance.Get(BastionSetting.MoveSpeed);body.sprintingSpeedMultiplier=BastionBalance.Get(BastionSetting.SprintSpeed)/body.baseMoveSpeed;
            body.baseAttackSpeed=BastionBalance.Get(BastionSetting.BaseAttackSpeed);body.baseCrit=BastionBalance.Get(BastionSetting.BaseCrit);
            body.RecalculateStats();
            if(NetworkServer.active&&body.healthComponent&&body.healthComponent.alive)
                body.healthComponent.Networkhealth=Mathf.Clamp(body.healthComponent.health+body.maxHealth-previous,1,body.healthComponent.fullHealth);
        }
    }
}
