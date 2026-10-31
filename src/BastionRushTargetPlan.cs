using System.Collections.Generic;
using RoR2;
namespace IvoryBastion
{
    // One plan per accepted Rush. Count splash coverage, not predicted damage,
    // so a boss's health cannot monopolize the salvo. In-flight missiles keep
    // their own plan even when the next charge starts another cast.
    internal sealed class BastionRushTargetPlan
    {
        private readonly Dictionary<HealthComponent,int> coverage=new Dictionary<HealthComponent,int>();
        internal int Count(HealthComponent victim)=>victim&&coverage.TryGetValue(victim,out var count)?count:0;
        internal Allocation Claim(BastionController owner,CharacterBody primary,float radius)
        {
            var victims=new HashSet<HealthComponent>();
            if(owner&&owner.Alive&&BastionUtilityCombat.ValidRushVictim(owner,primary)&&radius>0&&!float.IsInfinity(radius))
            {
                var center=BastionCombat.AimPoint(primary);
                void Include(CharacterBody body)
                {
                    if(BastionUtilityCombat.ValidRushVictim(owner,body)&&BastionUtilityCombat.InRushBlast(center,body,radius)&&BastionCombat.Visible(center,body))victims.Add(body.healthComponent);
                }
                Include(primary);
                foreach(var body in CharacterBody.readOnlyInstancesList)Include(body);
                foreach(var victim in victims){coverage.TryGetValue(victim,out var count);coverage[victim]=count+1;}
            }
            return new Allocation(this,victims);
        }
        internal sealed class Allocation
        {
            private BastionRushTargetPlan plan;
            private readonly HashSet<HealthComponent> victims;
            internal Allocation(BastionRushTargetPlan plan,HashSet<HealthComponent> victims){this.plan=plan;this.victims=victims;}
            // Landed hits remain counted until this cast finishes spreading.
            internal void Commit(){plan=null;victims.Clear();}
            internal void Release()
            {
                if(plan==null)return;
                foreach(var victim in victims)
                {
                    if(!plan.coverage.TryGetValue(victim,out var count))continue;
                    if(count<=1)plan.coverage.Remove(victim);else plan.coverage[victim]=count-1;
                }
                plan=null;victims.Clear();
            }
        }
    }
}
