using System;
using B=IvoryBastion.BastionBalance;
using S=IvoryBastion.BastionSetting;
namespace IvoryBastion
{
    internal enum BastionUpgradeNode:byte
    {
        MissileRacks,Autoloader,IonOptics,IonCapacitors,WidowFabricators,ChargedNetwork,
        Hangar,Blueprints,ShieldMatrix,Hardening,AegisCircuit,Renewal,
        Airframe,ThrusterTanks,HeavenfallRigs,BlueShiftDriver
    }
    internal sealed class BastionUpgradeState
    {
        internal bool Active;
        internal readonly byte[] Points=new byte[BastionUpgradeTree.Count];
        internal readonly float[] NextPerk=new float[BastionUpgradeTree.Count];
        internal int Rank(BastionUpgradeNode node)=>Math.Min(Points[(int)node],BastionUpgradeTree.Nodes[(int)node].Maximum);
        internal float Progress(BastionUpgradeNode node)=>(float)Rank(node)/BastionUpgradeTree.Nodes[(int)node].Maximum;
        internal bool Maxed(BastionUpgradeNode node)=>Rank(node)==BastionUpgradeTree.Nodes[(int)node].Maximum;
        internal bool Perk(BastionUpgradeNode node,float now,float cooldown)
        {
            if(!Active||!Maxed(node)||float.IsNaN(now)||float.IsInfinity(now)||now<NextPerk[(int)node])return false;
            NextPerk[(int)node]=now+Math.Max(0,cooldown);return true;
        }
    }
    internal sealed class BastionUpgradeDefinition
    {
        internal readonly BastionUpgradeNode Id;
        internal readonly string Name,Bonus;
        internal readonly int Group,Tier,Maximum,Parent,RequiredPoints;
        internal BastionUpgradeDefinition(BastionUpgradeNode id,string name,int group,int tier,int maximum,string bonus,int parent=-1,int required=0)
        {Id=id;Name=name;Group=group;Tier=tier;Maximum=maximum;Bonus=bonus;Parent=parent;RequiredPoints=required;}
    }
    internal static class BastionUpgradeTree
    {
        // Keep these node ordinals stable. The original purchase transport sends
        // the node in its existing integer branch field; no new request channel.
        internal const int Count=16,PurchaseOffset=100;
        internal static readonly BastionUpgradeDefinition[] Nodes={
            new BastionUpgradeDefinition(BastionUpgradeNode.MissileRacks,"MISSILE RACKS",0,1,3,"SCAVENGER FEED: kills recover 1 missile (1s cooldown)."),
            new BastionUpgradeDefinition(BastionUpgradeNode.Autoloader,"AUTOLOADER",0,3,3,"RESERVE KICKSTART: recover 3 missiles when empty (15s cooldown).",0,3),
            new BastionUpgradeDefinition(BastionUpgradeNode.IonOptics,"ION OPTICS",0,1,3,"PIERCING SIPHON: a damaging beam restores 1 missile."),
            new BastionUpgradeDefinition(BastionUpgradeNode.IonCapacitors,"ION CAPACITORS",0,3,3,"OVERPENETRATION: hit 3+ enemies to refund 0.75s beam recharge.",2,3),
            new BastionUpgradeDefinition(BastionUpgradeNode.WidowFabricators,"WIDOW FABRICATORS",0,1,3,"STATIC CLAMP: latching roots non-bosses for 0.6s (2s per target)."),
            new BastionUpgradeDefinition(BastionUpgradeNode.ChargedNetwork,"CHARGED NETWORK",0,3,3,"RECLAMATION: mine kills refund up to 3s Widow recharge (1s cooldown).",4,3),
            new BastionUpgradeDefinition(BastionUpgradeNode.Hangar,"HANGAR EXPANSION",0,3,2,"EMERGENCY REBUILD: a lost drone returns after 1s (30s cooldown)."),
            new BastionUpgradeDefinition(BastionUpgradeNode.Blueprints,"DRONE BLUEPRINTS",0,2,3,"TRIAGE LINK: Repairers also restore your Seraph Shield.",6,1),
            new BastionUpgradeDefinition(BastionUpgradeNode.ShieldMatrix,"SHIELD MATRIX",1,1,3,"SALVAGE SHIELD: kills restore 5% shield capacity (2s cooldown)."),
            new BastionUpgradeDefinition(BastionUpgradeNode.Hardening,"HARDENING",1,3,3,"BREAK GATE: shield break grants 0.35s invulnerability (20s cooldown).",8,3),
            new BastionUpgradeDefinition(BastionUpgradeNode.AegisCircuit,"AEGIS CIRCUIT",1,1,3,"SHIELD KICKSTART: Aegis restores 25% Seraph Shield."),
            new BastionUpgradeDefinition(BastionUpgradeNode.Renewal,"RENEWAL",1,3,3,"BARRIER REFLOW: return 20% unused Aegis to Seraph Shield when it ends.",10,3),
            new BastionUpgradeDefinition(BastionUpgradeNode.Airframe,"AIRFRAME",2,1,3,"SLIPSTREAM: boosting increases Needlefire's ammo recovery by 50%."),
            new BastionUpgradeDefinition(BastionUpgradeNode.ThrusterTanks,"THRUSTER TANKS",2,2,2,"WING CAPACITOR: Blue Shift restores 1 Heavenfall charge (20s cooldown).",12,3),
            new BastionUpgradeDefinition(BastionUpgradeNode.HeavenfallRigs,"HEAVENFALL RIGS",2,2,3,"AFTERIMAGE: Heavenfall restores 20% Seraph Shield.",12,3),
            new BastionUpgradeDefinition(BastionUpgradeNode.BlueShiftDriver,"BLUE SHIFT DRIVER",2,3,3,"CROSSFIRE: Needlefire ricochets during Blue Shift.",13,2)
        };
        internal static bool Available(BastionUpgradeState state,int node)
        {
            if(state==null||node<0||node>=Count)return false;
            var d=Nodes[node];return d.Parent<0||state.Points[d.Parent]>=d.RequiredPoints;
        }
        internal static int Cost(BastionUpgradeState state,int node)
        {
            if(state==null||node<0||node>=Count)return 0;
            var d=Nodes[node];if(state.Points[node]>=d.Maximum)return 0;
            float basis=B.Get(d.Tier==1?S.CostOne:d.Tier==2?S.CostTwo:S.CostThree);
            return (int)Math.Round(Math.Max(0,basis)*(1+state.Points[node]*.5f),MidpointRounding.AwayFromZero);
        }
        internal static bool TryBuy(BastionUpgradeState state,ref float fabrication,int node,int choice,bool deployed)
        {
            if(!deployed||choice!=0||!Available(state,node)||state.Points[node]>=Nodes[node].Maximum)return false;
            int cost=Cost(state,node);if(float.IsNaN(fabrication)||float.IsInfinity(fabrication)||fabrication<cost)return false;
            fabrication-=cost;state.Active=true;state.Points[node]++;return true;
        }
        internal static bool Valid(byte[] points)
        {
            if(points==null||points.Length!=Count)return false;
            foreach(var d in Nodes)
                if(points[(int)d.Id]>d.Maximum||points[(int)d.Id]>0&&d.Parent>=0&&points[d.Parent]<d.RequiredPoints)return false;
            return true;
        }
        internal static float Toward(float baseline,float maximum,BastionUpgradeState state,BastionUpgradeNode node)
        {return baseline+(Math.Max(baseline,maximum)-baseline)*state.Progress(node);}
        internal static float Cooldown(float baseline,float minimum,BastionUpgradeState state,BastionUpgradeNode node)
        {return baseline+(Math.Min(baseline,minimum)-baseline)*state.Progress(node);}
    }
}
