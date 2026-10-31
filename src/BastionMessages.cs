using R2API.Networking.Interfaces;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
namespace IvoryBastion
{
    public sealed class BastionStatus:INetMessage
    {
        private NetworkInstanceId owner;private bool siege;private Vector3 anchor;private float shieldCapacity,shieldCharge,aegisCapacity,aegisCharge;private readonly BastionBank bank=new BastionBank();
        private uint transformReply;private BastionTransformResult transformResult;
        private uint snapshot,noticeId;private uint[] acknowledgements=System.Array.Empty<uint>();private string notice="";
        public BastionStatus(){}
        internal BastionStatus(BastionController c)
        {
            owner=c.Body.netId;siege=c.Sieged;anchor=c.Anchor;shieldCapacity=c.AbilityShield.Capacity;shieldCharge=c.AbilityShield.Charge;
            aegisCapacity=c.AegisBarrier.Capacity;aegisCharge=c.AegisBarrier.Charge;
            Copy(c.Bank,bank);transformReply=c.TransformSync.ReplyId;transformResult=c.TransformSync.Result;
            var requests=c.Requests;
            if(requests){snapshot=requests.NextSnapshot();acknowledgements=requests.AckSnapshot();noticeId=requests.NoticeId;notice=requests.HostNotice;}
        }
        private static void Copy(BastionBank a,BastionBank b){b.Thruster.Capacity=a.Thruster.Capacity;b.Thruster.Fuel=a.Thruster.Fuel;b.Thruster.Recovery=a.Thruster.Recovery;b.Thruster.Active=a.Thruster.Active;b.Thruster.Exhausted=a.Thruster.Exhausted;b.Fabrication=a.Fabrication;b.Ammo=a.Ammo;b.Laser=a.Laser;b.Protocol=a.Protocol;b.Redline=a.Redline;b.Saturation=a.Saturation;b.Toggle=a.Toggle;b.DefenseTime=a.DefenseTime;b.RedlineTime=a.RedlineTime;b.Construction=a.Construction;b.MissileSequence=a.MissileSequence;b.BarrageSequence=a.BarrageSequence;b.Reserved=a.Reserved;b.ProtocolCapacity=a.ProtocolCapacity;b.SpecialCapacity=a.SpecialCapacity;b.ProtocolCharges=a.ProtocolCharges;b.RedlineCharges=a.RedlineCharges;b.SaturationCharges=a.SaturationCharges;for(int i=0;i<4;i++){b.Ranks[i]=a.Ranks[i];b.Choices[i]=a.Choices[i];}for(int i=0;i<b.Loadout.Length;i++)b.Loadout[i]=a.Loadout[i];}
        public void Serialize(NetworkWriter w)
        {
            w.Write(owner);w.Write(siege);w.Write(anchor);w.Write(shieldCapacity);w.Write(shieldCharge);w.Write(aegisCapacity);w.Write(aegisCharge);
            foreach(float f in new[]{bank.Fabrication,bank.Ammo,bank.Laser,bank.Protocol,bank.Redline,bank.Saturation,bank.Toggle,bank.DefenseTime,bank.RedlineTime,bank.Construction})w.Write(f);
            foreach(int n in new[]{bank.MissileSequence,bank.BarrageSequence,bank.Reserved,bank.ProtocolCapacity,bank.SpecialCapacity,bank.ProtocolCharges,bank.RedlineCharges,bank.SaturationCharges})w.Write(n);
            for(int i=0;i<4;i++){w.Write(bank.Ranks[i]);w.Write(bank.Choices[i]);}for(int i=0;i<bank.Loadout.Length;i++)w.Write(bank.Loadout[i]);
            w.Write(bank.Thruster.Capacity);w.Write(bank.Thruster.Fuel);w.Write(bank.Thruster.Recovery);w.Write(bank.Thruster.Active);w.Write(bank.Thruster.Exhausted);w.Write(transformReply);w.Write((byte)transformResult);
            w.Write(snapshot);w.Write((byte)acknowledgements.Length);foreach(uint id in acknowledgements)w.Write(id);w.Write(noticeId);w.Write(notice??"");
        }
        public void Deserialize(NetworkReader r)
        {
            owner=r.ReadNetworkId();siege=r.ReadBoolean();anchor=r.ReadVector3();shieldCapacity=r.ReadSingle();shieldCharge=r.ReadSingle();aegisCapacity=r.ReadSingle();aegisCharge=r.ReadSingle();
            bank.Fabrication=r.ReadSingle();bank.Ammo=r.ReadSingle();bank.Laser=r.ReadSingle();bank.Protocol=r.ReadSingle();bank.Redline=r.ReadSingle();bank.Saturation=r.ReadSingle();bank.Toggle=r.ReadSingle();bank.DefenseTime=r.ReadSingle();bank.RedlineTime=r.ReadSingle();bank.Construction=r.ReadSingle();
            bank.MissileSequence=r.ReadInt32();bank.BarrageSequence=r.ReadInt32();bank.Reserved=r.ReadInt32();bank.ProtocolCapacity=r.ReadInt32();bank.SpecialCapacity=r.ReadInt32();bank.ProtocolCharges=r.ReadInt32();bank.RedlineCharges=r.ReadInt32();bank.SaturationCharges=r.ReadInt32();
            for(int i=0;i<4;i++){bank.Ranks[i]=r.ReadInt32();bank.Choices[i]=r.ReadInt32();}for(int i=0;i<bank.Loadout.Length;i++)bank.Loadout[i]=r.ReadInt32();
            bank.Thruster.Capacity=r.ReadInt32();bank.Thruster.Fuel=r.ReadSingle();bank.Thruster.Recovery=r.ReadSingle();bank.Thruster.Active=r.ReadBoolean();bank.Thruster.Exhausted=r.ReadBoolean();transformReply=r.ReadUInt32();transformResult=(BastionTransformResult)r.ReadByte();
            snapshot=r.ReadUInt32();int count=r.ReadByte();acknowledgements=new uint[count];for(int i=0;i<count;i++)acknowledgements[i]=r.ReadUInt32();noticeId=r.ReadUInt32();notice=r.ReadString();
        }
        public void OnReceived()
        {
            if(NetworkServer.active)return;
            var obj=Util.FindNetworkObject(owner);var c=obj?obj.GetComponent<BastionController>():null;if(!c)return;
            var requests=c.Requests;if(!requests||!requests.AcceptSnapshot(snapshot,acknowledgements))return;
            bool stats=c.Bank.Thruster.Active!=bank.Thruster.Active||c.Bank.Ranks[3]!=bank.Ranks[3]||c.Bank.Ranks[1]!=bank.Ranks[1]||c.Bank.RedlineTime>0!=(bank.RedlineTime>0);
            Copy(bank,c.Bank);c.AbilityShield.Receive(shieldCapacity,shieldCharge);c.AegisBarrier.Receive(aegisCapacity,aegisCharge);if(stats)c.Body.MarkAllStatsDirty();
            // Install form, collision dimensions and host position before the
            // receipt allows the owner's input state to finish.
            if(c.Sieged!=siege){c.Sieged=siege;c.ApplyMode(true,anchor);}
            c.Anchor=anchor;
            c.TransformSync.ReceiveReply(transformReply,transformResult);
            requests.ReceiveNotice(noticeId,notice);
        }
    }
    public sealed class BastionEffect:INetMessage
    {
        public NetworkInstanceId Owner,Target;public BastionFxKind Kind;public Vector3 A,B;public uint Id;public float Value;
        public void Serialize(NetworkWriter w){w.Write(Owner);w.Write(Target);w.Write((byte)Kind);w.Write(A);w.Write(B);w.Write(Id);w.Write(Value);}
        public void Deserialize(NetworkReader r){Owner=r.ReadNetworkId();Target=r.ReadNetworkId();Kind=(BastionFxKind)r.ReadByte();A=r.ReadVector3();B=r.ReadVector3();Id=r.ReadUInt32();Value=r.ReadSingle();}
        public void OnReceived(){if(!NetworkServer.active)Apply();}
        public void Apply(){if(Application.isBatchMode)return;BastionFx.Play(this);}
    }
}
