using System;
using System.Linq;
using System.Reflection;
using IvoryBastion;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
using B=IvoryBastion.BastionBalance;
using S=IvoryBastion.BastionSetting;
internal static class UpgradeChecks
{
    private static int checks;
    private static void Check(bool value,string why){checks++;if(!value)throw new Exception("Upgrade tree: "+why);}
    private static void Near(float value,float expected,string why)=>Check(Math.Abs(value-expected)<.002f,why+": "+value+" != "+expected);
    private static void Invoke(object obj,string name)=>obj.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,null);
    private static BastionController Actor()
    {
        var go=new GameObject();var body=go.AddComponent<CharacterBody>();body.maxHealth=100;body.healthComponent.body=body;body.master=go.AddComponent<CharacterMaster>();body.netId=new(){Value=60000};
        var c=go.AddComponent<BastionController>();c.Body=body;c.Sieged=true;c.Bank.Upgrades.Active=true;
        var utility=go.AddComponent<BastionUtility>();Invoke(utility,"Awake");c.AbilityShield.Resize(100);
        Util.Objects[body.netId.Value]=go;return c;
    }
    private static void Max(BastionBank bank,BastionUpgradeNode node)
    {
        var d=BastionUpgradeTree.Nodes[(int)node];if(d.Parent>=0)Max(bank,(BastionUpgradeNode)d.Parent);
        bank.Fabrication=99999;while(!bank.Upgrades.Maxed(node))Check(bank.Buy(100+(int)node,0,true),"parent-compliant real host purchase succeeds");
    }
    private static BastionUpgradeStatus Roundtrip(BastionUpgradeStatus status)
    {var writer=new NetworkWriter();status.Serialize(writer);var copy=new BastionUpgradeStatus();copy.Deserialize(new NetworkReader(writer.Bytes()));Check(writer.Bytes().Length==26,"bounded upgrade status payload is exactly 26 bytes");return copy;}
    internal static void Run()
    {
        B.Reset();NetworkServer.active=true;Time.time=0;
        var bank=new BastionBank();bank.Upgrades.Active=true;bank.Fabrication=99999;float spent=0;int points=0;
        foreach(var node in BastionUpgradeTree.Nodes)
        {
            int id=(int)node.Id;
            var empty=new BastionBank{Fabrication=99999};empty.Upgrades.Active=true;
            if(node.Parent>=0)Check(!empty.Buy(100+id,0,true)&&empty.Fabrication==99999,"prerequisite cannot be bypassed by host request");
            for(int rank=0;rank<node.Maximum;rank++)
            {
                int expected=(int)Math.Round((node.Tier==1?20:node.Tier==2?60:150)*(1+.5*rank),MidpointRounding.AwayFromZero);
                Check(BastionUpgradeTree.Cost(bank.Upgrades,id)==expected,"tier and invested-point price");
                float before=bank.Fabrication;Check(!bank.Buy(100+id,0,false)&&bank.Fabrication==before,"flight cannot purchase");
                Check(!bank.Buy(100+id,1,true)&&bank.Fabrication==before,"old fork choice cannot spend new points");
                Check(bank.Buy(100+id,0,true)&&bank.Upgrades.Points[id]==rank+1,"one accepted request adds exactly one point");
                Near(before-bank.Fabrication,expected,"exact single purchase spending");spent+=expected;points++;
                Check(BastionUpgradeTree.Valid(bank.Upgrades.Points),"every accepted intermediate graph is valid");
                Check(!string.IsNullOrWhiteSpace(BastionUpgradeText.Effect(node.Id,bank)),"each purchased node has readable dynamic effects");
            }
            float done=bank.Fabrication;Check(!bank.Buy(100+id,0,true)&&done==bank.Fabrication,"mastered node cannot be bought again");
        }
        Check(points==46&&spent==5655,"complete tree is 46 points / 5655 Fabrication, so late paths remain run investments");
        Near(bank.Capacity,45,"racks cap");Near(bank.SiegeReload,1.5f,"autoloader cap");Near(bank.LaserDamage,9,"optics cap");Near(bank.LaserInterval,4,"capacitor cap");
        Near(bank.ShieldCapacityMultiplier,1.4f,"both shield paths combine capacity");Near(bank.ShieldReduction(false),.44f,"shared mobile hardening");Near(bank.ShieldReduction(true),.44f,"shared siege hardening");Near(bank.AegisReduction,.4f,"Aegis hardening");
        bank.DefenseTime=6;Near(bank.DamageMultiplier(true,1),.336f,"mastered Aegis and shared shield multiply to 66.4% reduction");
        Check(bank.Slots==3&&bank.HeavyEvery(false)==6&&bank.CanUseUnit(3),"all optional weapon and drone paths can coexist");
        Near(BastionUtilityUpgrades.RushDistance(bank),18,"Rush stays in Mobile");Near(BastionUtilityUpgrades.MineDamage(bank),1.05f,"mine damage target");
        // A point must improve its own system without granting blanket benefits.
        var specialist=new BastionBank{Fabrication=99999};specialist.Upgrades.Active=true;Check(specialist.Buy(100,0,true),"first rack point");
        Check(specialist.Capacity==35&&specialist.LaserDamage==7&&specialist.SiegeReload==1&&specialist.FlightMultiplier==1&&BastionUtilityUpgrades.MineCount(specialist)==24,"missile point cannot upgrade other abilities");
        Check(!specialist.Buy(0,0,true)&&!specialist.Buy(99,0,true)&&!specialist.Buy(116,0,true)&&!specialist.Buy(int.MaxValue,0,true),"legacy and invalid IDs cannot buy hidden upgrades");
        specialist.Fabrication=29;Check(!specialist.Buy(100,0,true)&&specialist.Fabrication==29,"insufficient currency is unchanged");specialist.Fabrication=float.NaN;Check(!specialist.Buy(100,0,true),"NaN currency rejected");
        var malformed=new byte[16];malformed[1]=1;Check(!BastionUpgradeTree.Valid(malformed),"wire prerequisite bypass rejected");malformed[0]=4;Check(!BastionUpgradeTree.Valid(malformed),"wire rank overflow rejected");Check(!BastionUpgradeTree.Valid(new byte[15]),"wrong point count rejected");
        // Real serialized snapshots: server gate, late snapshots, duplication,
        // reordering and invalid graph must never consume a useful sequence.
        var c=Actor();var sync=c.gameObject.AddComponent<BastionUpgradeSync>();Invoke(sync,"Awake");
        var full=Roundtrip(new(){Owner=c.Body.netId,Sequence=4,Points=(byte[])bank.Upgrades.Points.Clone()});
        full.OnReceived();Check(c.Bank.Upgrades.Points.All(p=>p==0),"server never accepts a client-supplied upgrade snapshot");
        NetworkServer.active=false;full.OnReceived();Check(c.Bank.Upgrades.Points.SequenceEqual(bank.Upgrades.Points)&&c.Body.StatRefreshes==1,"client applies all valid points and refreshes stats once");
        full.OnReceived();Check(c.Body.StatRefreshes==1,"duplicate snapshot harmless");
        Roundtrip(new(){Owner=c.Body.netId,Sequence=3,Points=new byte[16]}).OnReceived();Check(c.Bank.Capacity==45,"late older snapshot cannot roll upgrades back");
        Roundtrip(new(){Owner=c.Body.netId,Sequence=100,Points=malformed}).OnReceived();
        Roundtrip(new(){Owner=c.Body.netId,Sequence=5,Points=new byte[16]}).OnReceived();Check(c.Bank.Capacity==30&&c.Body.StatRefreshes==2,"malformed later sequence does not block valid recovery");
        NetworkServer.active=true;Capstones();B.Reset();Util.Objects.Clear();Time.time=0;
        Console.WriteLine($"PASS: {checks} ranked upgrade economy, specialization, mastery and production snapshot checks.");
    }
    private static void Capstones()
    {
        var c=Actor();var u=c.GetComponent<BastionUtility>();c.AbilityShield.Absorb(80,0);Max(c.Bank,BastionUpgradeNode.Renewal);BastionUpgradeEffects.AegisCast(c);Near(c.AbilityShield.Charge,70,"Aegis max restoration applies once, not once per defense node");
        Max(c.Bank,BastionUpgradeNode.HeavenfallRigs);BastionUpgradeEffects.RushCast(c);Near(c.AbilityShield.Charge,90,"accepted Rush Afterimage");
        Max(c.Bank,BastionUpgradeNode.ThrusterTanks);u.WingburstReserve.Stock=0;u.WingburstReserve.Remaining=7;BastionUpgradeEffects.BlueShiftCast(c);Check(u.WingburstReserve.Stock==1,"Blue Shift restores one Rush charge");BastionUpgradeEffects.BlueShiftCast(c);Check(u.WingburstReserve.Stock==1,"Wing Capacitor cannot repeat inside cooldown");
        Max(c.Bank,BastionUpgradeNode.IonCapacitors);c.Bank.Ammo=0;c.Bank.Laser=4;BastionUpgradeEffects.BeamHit(c,0);Near(c.Bank.Ammo,0,"beam miss cannot siphon");BastionUpgradeEffects.BeamHit(c,3);Near(c.Bank.Ammo,1,"one ammo per beam, not per victim");Near(c.Bank.Laser,3.25f,"three-hit overpenetration");
        Max(c.Bank,BastionUpgradeNode.ChargedNetwork);u.MinesReserve.Stock=0;u.MinesReserve.Remaining=24;BastionUpgradeEffects.MineBurst(c,20);Near(u.MinesReserve.Remaining,21,"mine refund capped at three seconds");BastionUpgradeEffects.MineBurst(c,1);Near(u.MinesReserve.Remaining,21,"mine refund respects shared cooldown");
        var target=new GameObject().AddComponent<CharacterBody>();BastionUpgradeEffects.MineAttached(c,target);BastionUpgradeEffects.MineAttached(c,target);Check(target.Buffs.Count==1&&target.Buffs[0].Duration==.6f,"many latches cannot repeatedly root the same target");
        Time.time=3;target.isBoss=true;BastionUpgradeEffects.MineAttached(c,target);Check(target.Buffs.Count==1,"root respects bosses");target.isBoss=false;BastionUpgradeEffects.MineAttached(c,target);Check(target.Buffs.Count==2,"root gate recovers");
        Max(c.Bank,BastionUpgradeNode.Blueprints);c.AbilityShield.Absorb(100,0);BastionUpgradeEffects.RepairPulse(c,c.Body.corePosition);Near(c.AbilityShield.Charge,B.Get(S.RepairInterval),"repair shield support independent of HP deficit");
        var sync=c.gameObject.AddComponent<BastionUpgradeSync>();Invoke(sync,"Awake");c.AbilityShield.Restore(100);Max(c.Bank,BastionUpgradeNode.Hardening);Invoke(sync,"FixedUpdate");c.AbilityShield.Absorb(100,0);Invoke(sync,"FixedUpdate");Check(c.Body.Buffs.Count==1,"shield break triggers Break Gate once");
        c.AbilityShield.Restore(100);Invoke(sync,"FixedUpdate");c.AbilityShield.Absorb(100,0);Invoke(sync,"FixedUpdate");Check(c.Body.Buffs.Count==1,"Break Gate internal cooldown");
        c.Bank.DefenseTime=1;c.AegisBarrier.Grant(40,100);Invoke(sync,"FixedUpdate");c.Bank.DefenseTime=0;float before=c.AegisBarrier.Charge;Invoke(sync,"FixedUpdate");Near(c.AbilityShield.Charge,8,"Reflow restores shield at fortification expiry");Near(c.AegisBarrier.Charge,before-8,"Reflow consumes transferred Aegis, never duplicates reserve");
        Max(c.Bank,BastionUpgradeNode.MissileRacks);Max(c.Bank,BastionUpgradeNode.ShieldMatrix);Invoke(sync,"OnEnable");
        c.Bank.Ammo=0;float shield=c.AbilityShield.Charge;
        var other=new GameObject().AddComponent<CharacterBody>();GlobalEventManager.Killed(new(){attackerBody=other,victimBody=target});Near(c.Bank.Ammo,0,"other player kills do not grant owned-kill mastery");
        GlobalEventManager.Killed(new(){attackerBody=c.Body,victimBody=target});Near(c.Bank.Ammo,1,"Scavenger Feed grants one missile per qualifying kill");Near(c.AbilityShield.Charge,shield+5,"Salvage Shield restores custom reserve only");
        GlobalEventManager.Killed(new(){attackerBody=c.Body,victimBody=target});Near(c.Bank.Ammo,1,"owned-kill mastery has shared cooldown");
        Time.time+=3;var unitGo=new GameObject();var unit=unitGo.AddComponent<BastionUnit>();unit.Owner=c;var unitBody=unitGo.AddComponent<CharacterBody>();GlobalEventManager.Killed(new(){attackerBody=unitBody,victimBody=target});Near(c.Bank.Ammo,2,"owned drone kills credit owner mastery");
        Invoke(sync,"OnDisable");Time.time+=3;GlobalEventManager.Killed(new(){attackerBody=c.Body,victimBody=target});Near(c.Bank.Ammo,2,"disabled body releases global event subscription");
        Max(c.Bank,BastionUpgradeNode.Autoloader);c.Bank.Ammo=0;Invoke(sync,"FixedUpdate");Near(c.Bank.Ammo,3,"empty reserve kickstart");c.Bank.Ammo=0;Invoke(sync,"FixedUpdate");Near(c.Bank.Ammo,0,"reserve kickstart cannot loop in the same frame");
        Max(c.Bank,BastionUpgradeNode.Hangar);c.Bank.Construction=20;BastionUpgradeEffects.DroneLost(c);Near(c.Bank.Construction,1,"post-destruction emergency rebuild");c.Bank.Construction=20;BastionUpgradeEffects.DroneLost(c);Near(c.Bank.Construction,20,"second drone loss cannot repeat emergency rebuild");
        NetworkServer.active=false;float charge=c.AbilityShield.Charge;BastionUpgradeEffects.AegisCast(c);Near(c.AbilityShield.Charge,charge,"clients cannot grant capstone resources");NetworkServer.active=true;
    }
}

