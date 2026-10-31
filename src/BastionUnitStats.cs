using R2API.Networking.Interfaces;
using RoR2;
using UnityEngine.Networking;
namespace IvoryBastion
{
    public sealed class BastionUnitStats:INetMessage
    {
        public NetworkInstanceId Unit,Owner;public float Health,Damage;public int Slot;
        public void Serialize(NetworkWriter w){w.Write(Unit);w.Write(Owner);w.Write(Health);w.Write(Damage);w.Write(Slot);}
        public void Deserialize(NetworkReader r){Unit=r.ReadNetworkId();Owner=r.ReadNetworkId();Health=r.ReadSingle();Damage=r.ReadSingle();Slot=r.ReadInt32();}
        public void OnReceived()
        {if(NetworkServer.active)return;var unit=Util.FindNetworkObject(Unit);var owner=Util.FindNetworkObject(Owner);var b=unit?unit.GetComponent<CharacterBody>():null;var u=unit?unit.GetComponent<BastionUnit>():null;var c=owner?owner.GetComponent<BastionController>():null;if(!b||!u)return;
            if(b.baseMaxHealth!=Health||b.baseDamage!=Damage){b.baseMaxHealth=Health;b.levelMaxHealth=0;b.baseDamage=Damage;b.levelDamage=0;b.baseRegen=0;b.levelRegen=0;b.RecalculateStats();}
            u.Slot=Slot;if(c){u.Owner=c;if(!c.Units.Contains(u))c.Units.Add(u);}
        }
    }
}
