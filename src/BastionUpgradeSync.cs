using System;
using System.Linq;
using R2API.Networking;
using R2API.Networking.Interfaces;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
namespace IvoryBastion
{
    // Upgrade snapshots are deliberately separate from the protected status
    // packet. Original skill requests, retries, receipts and stock fields stay.
    public sealed class BastionUpgradeStatus:INetMessage
    {
        internal NetworkInstanceId Owner;
        internal uint Sequence;
        internal byte[] Points=Array.Empty<byte>();
        private byte schema=1;
        public void Serialize(NetworkWriter w)
        {w.Write(schema);w.Write(Owner);w.Write(Sequence);w.Write((byte)Points.Length);foreach(byte point in Points)w.Write(point);}
        public void Deserialize(NetworkReader r)
        {schema=r.ReadByte();Owner=r.ReadNetworkId();Sequence=r.ReadUInt32();int count=r.ReadByte();Points=new byte[count];for(int i=0;i<count;i++)Points[i]=r.ReadByte();}
        public void OnReceived()
        {
            if(NetworkServer.active||schema!=1||Sequence==0||!BastionUpgradeTree.Valid(Points))return;
            var obj=Util.FindNetworkObject(Owner);if(obj)obj.GetComponent<BastionUpgradeSync>()?.Receive(this);
        }
    }
    public sealed class BastionUpgradeSync:MonoBehaviour
    {
        private BastionController owner;
        private float nextSend,previousShield;
        private uint sequence,received;
        private readonly byte[] sentPoints=new byte[BastionUpgradeTree.Count];
        private bool wasAegis;
        private void Awake(){owner=GetComponent<BastionController>();}
        private void OnEnable(){GlobalEventManager.onCharacterDeathServer+=Killed;}
        private void OnDisable(){GlobalEventManager.onCharacterDeathServer-=Killed;}
        private void FixedUpdate()
        {
            if(!owner||!owner.Body||!owner.Alive)return;
            if(!NetworkServer.active)return;
            var bank=owner.Bank;bank.Upgrades.Active=true;
            var tree=bank.Upgrades;float now=Time.time;
            if(bank.Ammo<1&&tree.Perk(BastionUpgradeNode.Autoloader,now,15))bank.Ammo=Math.Min(bank.Capacity-bank.Reserved,bank.Ammo+3);
            float shield=owner.AbilityShield.Charge;
            if(previousShield>0&&shield<=0&&tree.Perk(BastionUpgradeNode.Hardening,now,20))owner.Body.AddTimedBuff(RoR2Content.Buffs.HiddenInvincibility,.35f);
            previousShield=shield;
            bool aegis=bank.DefenseTime>0;
            if(wasAegis&&!aegis&&tree.Maxed(BastionUpgradeNode.Renewal))
            {
                float before=owner.AbilityShield.Charge;
                BastionAbilityShield.Restore(owner,owner.AegisBarrier.Charge*.2f);
                float restored=owner.AbilityShield.Charge-before;
                if(restored>0)owner.AegisBarrier.Absorb(restored);
            }
            wasAegis=aegis;
            if(Time.unscaledTime>=nextSend||!sentPoints.SequenceEqual(tree.Points))
            {nextSend=Time.unscaledTime+.25f;Array.Copy(tree.Points,sentPoints,sentPoints.Length);new BastionUpgradeStatus{Owner=owner.Body.netId,Sequence=BastionRequestRules.Next(ref sequence),Points=(byte[])tree.Points.Clone()}.Send(NetworkDestination.Clients);}
        }
        private void Killed(DamageReport report)
        {
            if(!NetworkServer.active||!owner||!owner.Alive||!report.victimBody||!report.attackerBody)return;
            var attacker=report.attackerBody.GetComponent<BastionController>();
            if(!attacker){var unit=report.attackerBody.GetComponent<BastionUnit>();if(unit)attacker=unit.Owner;}
            if(attacker!=owner)return;
            var bank=owner.Bank;float now=Time.time;
            if(bank.Upgrades.Perk(BastionUpgradeNode.MissileRacks,now,1))bank.Ammo=Math.Min(bank.Capacity-bank.Reserved,bank.Ammo+1);
            if(bank.Upgrades.Perk(BastionUpgradeNode.ShieldMatrix,now,2))BastionAbilityShield.Restore(owner,owner.AbilityShield.Capacity*.05f);
        }
        internal void Receive(BastionUpgradeStatus message)
        {
            if(NetworkServer.active||!owner||!owner.Body||received!=0&&!BastionRequestRules.Newer(message.Sequence,received))return;
            received=message.Sequence;
            var state=owner.Bank.Upgrades;bool changed=!state.Active;state.Active=true;
            for(int i=0;i<BastionUpgradeTree.Count;i++){changed|=state.Points[i]!=message.Points[i];state.Points[i]=message.Points[i];}
            if(changed)owner.Body.MarkAllStatsDirty();
        }
    }
}
