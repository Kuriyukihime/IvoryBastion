using System.Linq;
using System.Collections.Generic;
using B=IvoryBastion.BastionBalance;
using S=IvoryBastion.BastionSetting;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
namespace IvoryBastion
{
    // Host-owned spiders mark actual enemies. Only an owner's missile/laser
    // hitting the marked enemy can arm the short, externally triggered burst.
    public sealed class BastionServerSpiderMine:MonoBehaviour
    {
        private static readonly List<BastionServerSpiderMine> live=new List<BastionServerSpiderMine>();
        private struct Slots{internal int Count,Used;}
        private static readonly Dictionary<CharacterBody,Slots> claimed=new Dictionary<CharacterBody,Slots>();
        private static bool claimsDirty=true;
        private BastionController owner;private CharacterBody target;private uint id;
        private BastionMinePhase phase;private float age,groundAge,phaseAge,walked,nextScan,nextSend;
        private int attachmentSlot=-1;
        private Vector3 launch,landing,velocity,leapFrom,anchorLocal;private bool ended,weaponPrimed;
        private float burstDamage,burstRadius;
        private readonly BastionDamageReservation reservation=new BastionDamageReservation();
        internal static int Count(BastionController c){int count=0;foreach(var mine in live)if(mine&&mine.owner==c)count++;return count;}
        // Reserve a slot as soon as the leap starts. All owners share the cap;
        // twelve spiders updating in one host tick cannot claim the same slot.
        private int AvailableSlot(CharacterBody body)
        {
            if(!body)return -1;
            // Many waiting spiders scan the same crowded targets. Rebuild only
            // when a reservation changes, not once per spider per candidate.
            if(claimsDirty)
            {
                claimed.Clear();
                foreach(var mine in live)
                {
                    if(!mine||mine.ended||!mine.target||!HasClaim(mine.phase))continue;
                    claimed.TryGetValue(mine.target,out var entry);entry.Count++;
                    if(mine.attachmentSlot>=0)entry.Used|=1<<mine.attachmentSlot;
                    claimed[mine.target]=entry;
                }
                claimsDirty=false;
            }
            claimed.TryGetValue(body,out var slots);int count=slots.Count,used=slots.Used;
            if(target==body&&HasClaim(phase)){count--;if(attachmentSlot>=0)used&=~(1<<attachmentSlot);}
            int limit=BastionMineRules.TargetLimit(body.isBoss||body.isChampion,body.isElite);
            if(count>=limit)return -1;
            for(int slot=0;slot<limit;slot++)if((used&(1<<slot))==0)return slot;
            return -1;
        }
        private static bool HasClaim(BastionMinePhase phase)=>phase==BastionMinePhase.Leaping||phase==BastionMinePhase.Attached||phase==BastionMinePhase.Primed;
        private static void InvalidateClaims(){claimsDirty=true;if(live.Count==0){claimed.Clear();claimsDirty=false;}}
        internal static bool Ground(Vector3 point,out Vector3 result,float depth=24)
        {
            if(Physics.Raycast(point+Vector3.up*2,Vector3.down,out var hit,depth,LayerIndex.world.mask,QueryTriggerInteraction.Ignore)&&hit.normal.y>.35f){result=hit.point;return true;}
            result=point;return false;
        }
        internal static void Launch(BastionController owner,uint id,int shot,int count,Vector3 center,Vector3 from,float fieldRadius=-1)
        {
            if(!NetworkServer.active||!owner||!owner.Alive)return;
            if(fieldRadius<0||float.IsNaN(fieldRadius)||float.IsInfinity(fieldRadius))fieldRadius=BastionUtilityUpgrades.MineFieldRadius(owner.Bank);
            if(Count(owner)>=BastionMineRules.MaximumMines)foreach(var old in live)if(old&&old.owner==owner){old.End(false);break;}
            Vector3 point=center;bool found=false;
            for(int attempt=0;attempt<4&&!found;attempt++)
            {var offset=BastionMineRules.LandingOffset(shot,count,fieldRadius*(1-attempt*.25f),BastionUtilityUpgrades.MineBlastRadius(owner.Bank));found=Ground(center+offset+Vector3.up*7,out point,45);}
            if(!found&&!Ground(center,out point))return;
            var go=new GameObject("Ivory spider mine (host)");go.transform.position=from;
            var mine=go.AddComponent<BastionServerSpiderMine>();mine.owner=owner;mine.id=id;mine.launch=from;mine.landing=point;live.Add(mine);InvalidateClaims();
            mine.velocity=BastionMineRules.FlightVelocity(from,point,0,B.Get(S.MineAirTime));mine.Send();
            owner.Emit(BastionFxKind.MineLaunch,from,Vector3.up,null,id);
        }
        private void FixedUpdate()
        {
            if(ended)return;
            if(!NetworkServer.active||!owner||!owner.Alive){End(false);return;}
            float dt=Time.fixedDeltaTime;age+=dt;phaseAge+=dt;nextScan-=dt;nextSend-=dt;velocity=Vector3.zero;
            if(phase!=BastionMinePhase.Airborne)groundAge+=dt;
            // A weapon hit already accepted near expiry completes its brief cue.
            if(phase!=BastionMinePhase.Airborne&&phase!=BastionMinePhase.Primed&&groundAge>=B.Get(S.MineLifetime)){End(false);return;}
            switch(phase)
            {
                case BastionMinePhase.Airborne:
                    var next=BastionMineRules.Flight(launch,landing,phaseAge,B.Get(S.MineAirTime));
                    velocity=BastionMineRules.FlightVelocity(launch,landing,phaseAge,B.Get(S.MineAirTime));
                    if(Physics.Linecast(transform.position,next,out var airHit,LayerIndex.world.mask,QueryTriggerInteraction.Ignore)&&phaseAge>.1f)
                    {landing=airHit.point;if(Ground(landing+Vector3.up*.1f,out var below))landing=below;Plant();}
                    else{transform.position=next;if(phaseAge>=B.Get(S.MineAirTime))Plant();}
                    break;
                case BastionMinePhase.Planted:
                    if(nextScan<=0&&BastionMineRules.CanWake(groundAge,0,B.Get(S.MineTriggerRange)))
                    {nextScan=.15f;target=Find();if(target)Change(BastionMinePhase.Crawling);}
                    break;
                case BastionMinePhase.Crawling:Crawl(dt);break;
                case BastionMinePhase.Leaping:Leap(dt);break;
                case BastionMinePhase.Attached:
                    if(!BastionCombat.Enemy(owner.Body,target)){End(false);return;}
                    Follow();break;
                case BastionMinePhase.Primed:
                    // The initiating weapon may have killed the marked enemy.
                    // Keep its last anchor and deliver the accepted burst anyway.
                    if(target&&target.healthComponent&&target.healthComponent.alive)Follow();
                    if(phaseAge>=BastionMineRules.OverchargeFuse){Explode();return;}
                    break;
                case BastionMinePhase.Armed:
                    // Old review snapshots/configuration never create autonomous damage.
                    target=null;Plant();break;
            }
            if(nextSend<=0)Send();
        }
        private void Change(BastionMinePhase next){if(HasClaim(phase)!=HasClaim(next))claimsDirty=true;phase=next;phaseAge=0;Send();}
        private void Plant()
        {if(Ground(transform.position+Vector3.up*.1f,out var point,12))transform.position=point;else transform.position=landing;velocity=Vector3.zero;Change(BastionMinePhase.Planted);}
        private CharacterBody Find()
        {
            CharacterBody best=null;float closest=B.Get(S.MineTriggerRange)*B.Get(S.MineTriggerRange);
            foreach(var body in CharacterBody.readOnlyInstancesList)
            {
                if(!BastionCombat.Enemy(owner.Body,body)||AvailableSlot(body)<0)continue;
                var point=Surface(body,transform.position);float distance=(point-transform.position).sqrMagnitude;
                if(distance>closest||!BastionCombat.Visible(transform.position+Vector3.up*.2f,body))continue;
                if(!BastionMineRules.CanChase(walked,B.Get(S.MineWalkDistance))&&!CanLeap(point))continue;
                best=body;closest=distance;
            }
            return best;
        }
        private static Vector3 Surface(CharacterBody body,Vector3 from)
        {
            Vector3 point=BastionCombat.AimPoint(body);float closest=float.PositiveInfinity;
            Consider(body.mainHurtBox,from,ref point,ref closest);
            var group=body.hurtBoxGroup;if(group&&group.hurtBoxes!=null)foreach(var hurtBox in group.hurtBoxes)Consider(hurtBox,from,ref point,ref closest);
            return point;
        }
        private static void Consider(HurtBox hurtBox,Vector3 from,ref Vector3 point,ref float closest)
        {
            var collider=hurtBox?hurtBox.GetComponent<Collider>():null;if(!collider||!collider.enabled)return;
            var candidate=collider.ClosestPoint(from);if(!BastionCombat.Finite(candidate))return;
            float distance=(candidate-from).sqrMagnitude;if(distance<closest){closest=distance;point=candidate;}
        }
        private bool CanLeap(Vector3 point)
        {
            var delta=point-transform.position;
            return delta.magnitude<=BastionMineRules.LatchRange||Vector3.ProjectOnPlane(delta,Vector3.up).magnitude<=BastionMineRules.LatchRange&&delta.magnitude<=B.Get(S.MineTriggerRange);
        }
        private void Crawl(float dt)
        {
            if(!BastionCombat.Enemy(owner.Body,target)||AvailableSlot(target)<0){target=null;attachmentSlot=-1;Change(BastionMinePhase.Planted);return;}
            var point=Surface(target,transform.position+Vector3.up*.3f);
            if(CanLeap(point)&&BeginLeap())return;
            if(!BastionMineRules.CanChase(walked,B.Get(S.MineWalkDistance))||(point-transform.position).magnitude>B.Get(S.MineTriggerRange)+B.Get(S.MineWalkDistance))
            {target=null;Change(BastionMinePhase.Planted);return;}
            var delta=Vector3.ProjectOnPlane(point-transform.position,Vector3.up);
            var move=delta.normalized*Mathf.Min(delta.magnitude,Mathf.Min(B.Get(S.MineWalkSpeed)*dt,B.Get(S.MineWalkDistance)-walked));
            var next=transform.position+move;
            if(!Ground(next+Vector3.up*.65f,out var ground,4)||Mathf.Abs(ground.y-transform.position.y)>BastionMineRules.MaximumStep||!ClearStep(transform.position,ground))
            {target=null;Change(BastionMinePhase.Planted);return;}
            velocity=(ground-transform.position)/Mathf.Max(dt,.001f);walked+=(ground-transform.position).magnitude;transform.position=ground;
            if(CanLeap(Surface(target,transform.position+Vector3.up*.3f)))BeginLeap();
        }
        private bool BeginLeap()
        {
            int slot=AvailableSlot(target);if(slot<0)return false;
            // Probe the enemy flank at hurtbox height, not a tiny animated bone.
            var center=BastionCombat.AimPoint(target);var outward=Vector3.ProjectOnPlane(transform.position-center,Vector3.up);
            if(outward.sqrMagnitude<.001f)outward=target.transform.forward;
            // Fan contact points across the approaching flank instead
            // of stacking all shells at an identical spot on the carrier.
            outward=outward.normalized;var side=new Vector3(-outward.z,0,outward.x);
            float angle=BastionMineRules.ContactAngle(slot)*(float)System.Math.PI/180;
            outward=outward*(float)System.Math.Cos(angle)+side*(float)System.Math.Sin(angle);
            var probe=center+outward*Mathf.Max(2,(transform.position-center).magnitude+1)+Vector3.up*BastionMineRules.ContactHeight(slot);
            var surface=Surface(target,probe);var normal=probe-surface;
            if(normal.sqrMagnitude<.001f)normal=outward;
            var anchor=surface+normal.normalized*BastionMineRules.ShellClearance;
            if(!BastionCombat.Finite(anchor)||!ClearLeapPath(transform.position,anchor))return false;
            attachmentSlot=slot;anchorLocal=target.transform.InverseTransformPoint(anchor);leapFrom=transform.position;velocity=BastionMineRules.LatchVelocity(leapFrom,anchor,0);Change(BastionMinePhase.Leaping);return true;
        }
        private void Leap(float dt)
        {
            if(!BastionCombat.Enemy(owner.Body,target)){End(false);return;}
            var destination=target.transform.TransformPoint(anchorLocal);var before=transform.position;
            var next=BastionMineRules.LatchFlight(leapFrom,destination,phaseAge);
            if(!ClearLeapSegment(before,next)){target=null;Plant();return;}
            transform.position=next;velocity=(next-before)/Mathf.Max(dt,.001f);
            if(phaseAge>=BastionMineRules.LatchTime)
            {
                // Completing a timed path guarantees attachment even if the enemy moves.
                transform.position=destination;velocity=Vector3.zero;Change(BastionMinePhase.Attached);
                owner.Emit(BastionFxKind.MineArming,transform.position,Vector3.up,target,id,BastionMineRules.LatchTime);
                BastionUpgradeEffects.MineAttached(owner,target);
            }
        }
        private static bool ClearLeapSegment(Vector3 from,Vector3 to)
        {
            var lift=Vector3.up*BastionMineRules.LeapProbeLift;
            return BastionCombat.ClearFlight(from+lift,to+lift);
        }
        private static bool ClearLeapPath(Vector3 from,Vector3 to)
        {
            // Validate the actual raised arc, rather than rejecting a jump
            // because the direct ground-to-flank chord intersects a low step.
            var previous=from;
            for(int i=1;i<=8;i++)
            {
                var next=BastionMineRules.LatchFlight(from,to,BastionMineRules.LatchTime*i/8);
                if(!ClearLeapSegment(previous,next))return false;
                previous=next;
            }
            return true;
        }
        private static bool ClearStep(Vector3 from,Vector3 to)
        {
            // Step up, cross, then lower. A diagonal ray at foot height
            // incorrectly treats an otherwise permitted small step as a wall.
            var lowFrom=from+Vector3.up*BastionMineRules.StepProbeLift;
            var lowTo=to+Vector3.up*BastionMineRules.StepProbeLift;
            float height=Mathf.Max(lowFrom.y,lowTo.y);
            var highFrom=new Vector3(from.x,height,from.z);
            var highTo=new Vector3(to.x,height,to.z);
            return !Physics.Linecast(lowFrom,highFrom,LayerIndex.world.mask,QueryTriggerInteraction.Ignore)&&
                !Physics.Linecast(highFrom,highTo,LayerIndex.world.mask,QueryTriggerInteraction.Ignore)&&
                !Physics.Linecast(highTo,lowTo,LayerIndex.world.mask,QueryTriggerInteraction.Ignore);
        }
        private void Follow(){transform.position=target.transform.TransformPoint(anchorLocal);}
        private void Send()
        {
            // A leap whose target dies ends inside the tick; send nothing after MineEnd.
            if(ended||!owner||!NetworkServer.active)return;nextSend=phase==BastionMinePhase.Leaping?.06f:.18f;
            bool attached=phase==BastionMinePhase.Attached||phase==BastionMinePhase.Primed;
            owner.Emit(BastionFxKind.MineState,transform.position,attached?anchorLocal:velocity,target,id,(int)phase+age*.01f);
        }
        private void Explode()
        {
            if(!weaponPrimed||phase!=BastionMinePhase.Primed)return;
            reservation.Release();
            var result=new BlastAttack{attacker=owner.gameObject,inflictor=owner.gameObject,teamIndex=owner.Body.teamComponent.teamIndex,position=transform.position,
                radius=burstRadius,baseDamage=burstDamage,baseForce=80,procCoefficient=B.Get(S.MineProc),crit=owner.Body.RollCrit(),
                falloffModel=BlastAttack.FalloffModel.None,attackerFiltering=AttackerFiltering.NeverHitSelf}.Fire();
            var slowed=new HashSet<CharacterBody>();foreach(var hit in result.hitPoints){var body=hit.hurtBox?hit.hurtBox.healthComponent?.body:null;if(body&&slowed.Add(body))body.AddTimedBuff(RoR2Content.Buffs.Slow50,B.Get(S.MineSlowDuration));}
            BastionUpgradeEffects.MineBurst(owner,slowed.Count(b=>b.healthComponent&&!b.healthComponent.alive));
            owner.Emit(BastionFxKind.MineChainExplosion,transform.position,Vector3.up,null,id,burstRadius);End(true);
        }
        // Kept as a harmless compatibility bridge. A nearby explosion is not a
        // confirmed hit on the marked enemy and therefore cannot trigger a mine.
        internal static void TriggerWeaponBlast(BastionController owner,Vector3 point,float radius){}
        internal static bool HasAttachedMarker(BastionController owner,HealthComponent victim)
        {
            if(!owner||!victim)return false;
            foreach(var mine in live)
                if(mine&&mine.owner==owner&&!mine.ended&&!mine.weaponPrimed&&mine.phase==BastionMinePhase.Attached&&mine.target&&mine.target.healthComponent==victim)return true;
            return false;
        }
        internal static void TriggerWeaponHit(BastionController owner,HealthComponent victim)
        {
            if(!NetworkServer.active||!owner||!victim)return;
            foreach(var mine in live)
            {
                if(!mine||mine.owner!=owner||mine.ended||mine.weaponPrimed||mine.phase!=BastionMinePhase.Attached||!mine.target||mine.target.healthComponent!=victim)continue;
                mine.Prime();
            }
        }
        private void Prime()
        {
            burstDamage=owner.Body.damage*BastionUtilityUpgrades.MineDamage(owner.Bank);burstRadius=BastionUtilityUpgrades.MineBlastRadius(owner.Bank);
            if(target&&target.healthComponent&&target.healthComponent.alive){Follow();reservation.Assign(owner,target,burstDamage);}
            weaponPrimed=true;Change(BastionMinePhase.Primed);
            owner.Emit(BastionFxKind.MineChain,transform.position+Vector3.up*.8f,transform.position,target,id);
        }
        private void End(bool exploded)
        {if(ended)return;ended=true;reservation.Release();if(owner&&NetworkServer.active&&!exploded)owner.Emit(BastionFxKind.MineEnd,transform.position,Vector3.zero,null,id);live.Remove(this);InvalidateClaims();Destroy(gameObject);}
        private void OnDestroy(){reservation.Release();live.Remove(this);InvalidateClaims();if(!ended&&owner&&NetworkServer.active)owner.Emit(BastionFxKind.MineEnd,transform.position,Vector3.zero,null,id);}
    }
}
