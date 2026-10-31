using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using IvoryBastion;
using B=IvoryBastion.BastionBalance;
using S=IvoryBastion.BastionSetting;
internal static class Program
{
    private static object Snapshot(BastionBank b)=>new{
        b.Capacity,b.Slots,Reload=b.SiegeReload,HeavyEvery=b.HeavyEvery(false),LaserPercent=b.LaserDamage*100,LaserCooldown=b.LaserInterval,
        MineCount=BastionUtilityUpgrades.MineCount(b),MineField=BastionUtilityUpgrades.MineFieldRadius(b),MineBonus=BastionUtilityUpgrades.MineChargeBonus(b),
        MinePercent=BastionUtilityUpgrades.MineDamage(b)*100,MineRadius=BastionUtilityUpgrades.MineBlastRadius(b),
        ShieldCapacityBonus=(b.ShieldCapacityMultiplier-1)*100,ShieldDR=b.ShieldReduction(false)*100,AegisDR=b.AegisReduction*100,
        BarrierPercent=b.BarrierFraction*100,HealingPercent=b.DefenseHealFraction*100,FlightBonus=(b.FlightMultiplier-1)*100,
        Fuel=b.ThrusterBaseCapacity,RushDistance=BastionUtilityUpgrades.RushDistance(b),RushShots=BastionUtilityUpgrades.RushShots(b),RushCooldown=BastionUtilityUpgrades.RushCooldown(b),
        NeedleBonus=(b.NeedleMultiplier-1)*100,b.ShiftDuration};
    internal static void Main(string[] args)
    {
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
        string root=Path.GetFullPath(args[0]);B.Reset();var nodes=new List<object>();
        foreach(var node in BastionUpgradeTree.Nodes)
        {
            var bank=new BastionBank{Fabrication=99999};bank.Upgrades.Active=true;
            void Require(int id,int rank)
            {
                var parent=BastionUpgradeTree.Nodes[id];if(parent.Parent>=0)Require(parent.Parent,parent.RequiredPoints);
                while(bank.Upgrades.Points[id]<rank)if(!bank.Buy(100+id,0,true))throw new Exception("Wiki prerequisite purchase failed: "+id);
            }
            if(node.Parent>=0)Require(node.Parent,node.RequiredPoints);
            var ranks=new List<object>();int total=0;
            for(int rank=1;rank<=node.Maximum;rank++)
            {
                int cost=BastionUpgradeTree.Cost(bank.Upgrades,(int)node.Id);total+=cost;
                if(!bank.Buy(100+(int)node.Id,0,true))throw new Exception("Wiki upgrade purchase failed: "+node.Id);
                ranks.Add(new{Rank=rank,Cost=cost,Total=total,Values=Snapshot(bank),InGameDescription=BastionUpgradeText.Effect(node.Id,bank)});
            }
            nodes.Add(new{Id=node.Id.ToString(),Name=node.Name,Group=new[]{"Siege","Defense","Mobile"}[node.Group],node.Tier,node.Maximum,
                Parent=node.Parent<0?null:BastionUpgradeTree.Nodes[node.Parent].Id.ToString(),RequiredRank=node.RequiredPoints,Mastery=node.Bonus,Ranks=ranks});
        }
        var data=new{Version=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"package","manifest.json"))).RootElement.GetProperty("version_number").GetString(),
            Status="unreleased review",Defaults=B.Specs.ToDictionary(s=>s.Key.ToString(),s=>s.Default),Nodes=nodes};
        Directory.CreateDirectory(Path.Combine(root,".build"));File.WriteAllText(Path.Combine(root,".build","wiki-data.json"),JsonSerializer.Serialize(data,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Exported "+nodes.Count+" real upgrade paths and "+B.Specs.Length+" default settings for the local wiki; no game/dependency assemblies loaded.");
    }
}
