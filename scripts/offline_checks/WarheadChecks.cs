using System;
using System.Linq;
using IvoryBastion;
using UnityEngine;

internal static class WarheadChecks
{
    internal static void Run()
    {
        int count=0;
        void Check(bool value,string name){if(!value)throw new Exception("Warheads: "+name);count++;}
        Check(BastionWarheadGeometry.Count==10,"fixed ultimate launches ten siege warheads");
        Check(BastionWarheadGeometry.Damage==5,"each siege warhead deals 500% damage");
        Check(BastionWarheadGeometry.Count*BastionWarheadGeometry.Damage==50,"maximum combined direct damage is 5000%");
        Check(BastionWarheadGeometry.Radius==15,"Skyfall damage and cluster targeting share a 15m blast radius");
        var positions=new[]{new Vector3(30,0,30),new Vector3(30,0,-30),new Vector3(-30,0,-30),new Vector3(-30,0,30)};
        var plan=BastionWarheadGeometry.Plan(Vector3.zero,positions,null);
        Check(plan.Length==10&&plan.Take(4).Distinct().Count()==4,"uncovered separate enemies receive damage before fallback repeats");
        Check(plan.All(i=>i>=0),"all ten shots have a valid target while enemies remain");
        positions=new[]{new Vector3(15,0,15),new Vector3(15,0,55),new Vector3(55,0,15),new Vector3(55,0,55)};
        plan=BastionWarheadGeometry.Plan(Vector3.zero,positions,null);
        Check(plan.Take(4).Distinct().Count()==4,"one crowded quadrant can receive all initial shots without sector quotas");
        positions=new[]{new Vector3(30,0,30),new Vector3(-30,0,-30)};
        plan=BastionWarheadGeometry.Plan(Vector3.zero,positions,null);
        Check(plan.Take(2).Distinct().Count()==2,"a covered enemy does not steal the other enemy's first useful shot");
        Check(plan.Length==10&&plan.All(i=>i>=0),"fallback allows the remaining warheads to overlap");
        positions=new[]{new Vector3(35,0,35)};
        Check(BastionWarheadGeometry.Plan(Vector3.zero,positions,null).All(i=>i==0),"only enemy may receive all ten warheads");
        Check(BastionWarheadGeometry.Plan(Vector3.zero,Array.Empty<Vector3>(),null).All(i=>i==-1),"no invented targets");
        Check(BastionWarheadGeometry.Plan(Vector3.zero,new[]{new Vector3(91,0,0)},null).All(i=>i==-1),"90m target limit");
        Check(BastionWarheadGeometry.Select(Vector3.zero,new[]{new Vector3(90,0,0)},null)==0,"90m boundary remains eligible");

        // One ordinary enemy, elite, boss and a cluster in four separate sectors.
        positions=new[]{new Vector3(35,0,35),new Vector3(-45,0,40),new Vector3(45,0,-40),
            new Vector3(-40,0,-40),new Vector3(-44,0,-40),new Vector3(-40,0,-44)};
        var kinds=new[]{BastionWarheadTargetKind.Ordinary,BastionWarheadTargetKind.Elite,BastionWarheadTargetKind.Boss,
            BastionWarheadTargetKind.Ordinary,BastionWarheadTargetKind.Ordinary,BastionWarheadTargetKind.Ordinary};
        plan=BastionWarheadGeometry.Plan(Vector3.zero,positions,null,kinds);
        Check(plan.Take(4).SequenceEqual(new[]{3,2,1,0}),"priority is cluster, boss, elite, ordinary; no quadrant forces a worse target");
        plan=BastionWarheadGeometry.Plan(Vector3.zero,positions,new[]{1f,1,3,1,1,1},kinds);
        Check(plan[0]==3&&plan.Skip(1).Take(3).All(i=>i==2)&&plan[4]==1&&plan[5]==0,"tough boss receives enough hits after cluster clear, before elite and ordinary");
        Check(BastionWarheadGeometry.Select(Vector3.zero,positions,new[]{1f,1,1,0,0,0},kinds)==2,"damage reserved for the cluster redirects the next launch to a boss in another sector");
        Check(BastionWarheadGeometry.Select(Vector3.zero,positions.Take(3).ToArray(),null,kinds)==2,"isolated boss outranks elite and ordinary");
        Check(BastionWarheadGeometry.Select(Vector3.zero,positions.Take(2).ToArray(),null,kinds)==1,"isolated elite outranks ordinary");
        Check(BastionWarheadGeometry.Select(Vector3.zero,positions,null,kinds)==3,"a newly available cluster supersedes the earlier boss target");
        Check(BastionWarheadGeometry.Select(Vector3.zero,positions,new[]{1f,0,0,0,0,0},kinds)==0,"uncovered ordinary enemy outranks targets with enough assigned damage");
        for(int angle=0;angle<360;angle+=30)
        {
            float a=angle*MathF.PI/180;var offset=new Vector3(120,8,-70);
            var rotated=positions.Select(p=>new Vector3(p.x*MathF.Cos(a)+p.z*MathF.Sin(a),p.y,-p.x*MathF.Sin(a)+p.z*MathF.Cos(a))+offset).ToArray();
            Check(BastionWarheadGeometry.Plan(offset,rotated,null,kinds).Take(4).SequenceEqual(new[]{3,2,1,0}),"target order is independent of facing, quadrant boundaries and world origin");
        }
        var groups=new[]{new Vector3(-50,0,0),new Vector3(-53,0,2),new Vector3(-49,0,-2),
            new Vector3(50,0,0),new Vector3(52,0,2),new Vector3(49,0,-1),new Vector3(50,0,3),new Vector3(53,0,0)};
        plan=BastionWarheadGeometry.Plan(Vector3.zero,groups,null);
        Check(plan[0]>=3&&plan[1]<3,"larger cluster is hit first; smaller uncovered cluster follows instead of redundant overlap");
        var outside=new[]{new Vector3(80,0,0),new Vector3(95,0,0),new Vector3(96,0,1),new Vector3(-50,0,0)};
        var outsideKinds=new[]{BastionWarheadTargetKind.Ordinary,BastionWarheadTargetKind.Ordinary,BastionWarheadTargetKind.Ordinary,BastionWarheadTargetKind.Boss};
        Check(BastionWarheadGeometry.Select(Vector3.zero,outside,null,outsideKinds)==3,"out-of-range enemies cannot inflate cluster priority");
        var vertical=new[]{new Vector3(20,0,20),new Vector3(20,19,20),new Vector3(20,-19,20),new Vector3(-40,0,-40)};
        Check(BastionWarheadGeometry.Select(Vector3.zero,vertical,null,outsideKinds)==3,"cluster detection respects the spherical blast across different elevations");
        var random=new System.Random(904);
        for(int iteration=0;iteration<70;iteration++)
        {
            positions=Enumerable.Range(0,12).Select(_=>new Vector3((float)random.NextDouble()*100-50,0,(float)random.NextDouble()*100-50)).ToArray();
            plan=BastionWarheadGeometry.Plan(Vector3.zero,positions,null);
            var covered=new bool[positions.Length];
            foreach(int target in plan)
            {
                Check(target>=0&&target<positions.Length,"random layouts retain valid targets");
                if(covered.Any(value=>!value))Check(Enumerable.Range(0,positions.Length).Any(i=>!covered[i]&&(positions[i]-positions[target]).sqrMagnitude<=15*15),"each shot covers an unassigned enemy while one remains");
                for(int i=0;i<positions.Length;i++)if((positions[i]-positions[target]).sqrMagnitude<=15*15)covered[i]=true;
            }
        }
        // Spending the ultimate's own charge cannot drain the primary magazine or
        // advance its heavy-shot cadence, including at zero ammunition.
        foreach(int rank in new[]{0,1,3})foreach(float ammo in new[]{0f,.5f,30f,rank==0?40f:60f})
        {
            var bank=new BastionBank{Ammo=ammo,MissileSequence=7};bank.Ranks[0]=rank;
            float production=bank.AmmoRate(true),capacity=bank.Capacity;
            Check(bank.Spend(BastionAction.Saturation,60),"ultimate charge works independently of available ammunition");
            Check(bank.Ammo==ammo&&bank.Reserved==0&&bank.MissileSequence==7,"ultimate leaves stockpile and primary cadence untouched");
            Check(bank.Capacity==capacity&&bank.AmmoRate(true)==production,"ultimate does not change capacity or production");
            Check(bank.SaturationCharges==0&&bank.Saturation==60,"ultimate still has sixty-second recharge");
            bank.Tick(1,true,false);
            Check(Math.Abs(bank.Ammo-Math.Min(capacity,ammo+production))<.001f,"normal reload continues during ultimate cooldown");
        }
        foreach(var destination in new[]{new Vector3(65,0,0),new Vector3(2,0,1),new Vector3(-40,40,20),new Vector3(0,-70,0)})
        {
            var start=new Vector3(0,10,0);
            Check(Vector3.Distance(BastionWarheadGeometry.Position(start,destination,0),start)<.001f,"arc starts at launch tube");
            Check(Vector3.Distance(BastionWarheadGeometry.Position(start,destination,1),destination)<.001f,"arc reaches impact point");
            var initial=BastionWarheadGeometry.Position(start,destination,.001f)-start;
            var terminal=destination-BastionWarheadGeometry.Position(start,destination,.999f);
            Check(initial.y>0&&Vector3.ProjectOnPlane(initial,Vector3.up).magnitude<initial.y*.01f,"launches vertically upward");
            Check(terminal.y<0&&Vector3.ProjectOnPlane(terminal,Vector3.up).magnitude<-terminal.y*.01f,"dives vertically onto target");
            float highest=Enumerable.Range(0,101).Max(i=>BastionWarheadGeometry.Position(start,destination,i/100f).y);
            Check(highest>Math.Max(start.y,destination.y)+8,"flies visibly above both endpoints");
            Check(BastionWarheadGeometry.Duration(start,destination)>=2.1f&&BastionWarheadGeometry.Duration(start,destination)<=2.9f,"readable bounded flight time");
        }
        Console.WriteLine("PASS: "+count+" warhead cluster/tier priority, coverage, fallback and overhead trajectory checks.");
    }
}
