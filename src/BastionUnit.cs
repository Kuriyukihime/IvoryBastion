using System.Linq;
using B = IvoryBastion.BastionBalance;
using S = IvoryBastion.BastionSetting;
using R2API.Networking;
using R2API.Networking.Interfaces;
using RoR2;
using RoR2.CharacterAI;
using RoR2.Projectile;
using UnityEngine;
using UnityEngine.Networking;
namespace IvoryBastion
{
    public sealed class BastionUnit:MonoBehaviour
    {
        public BastionUnitKind Kind;
        internal BastionController Owner;internal int Slot=-1;private CharacterBody body;private float expires,attack,pulse,sync;private bool docking;
        internal static void Spawn(BastionController owner,int slot,BastionUnitKind kind,float health,float lifetime=0)
        {
            if(!NetworkServer.active||!owner||!owner.Alive)return;
            var position=owner.Body.corePosition+Quaternion.Euler(0,slot*120+50,0)*Vector3.forward*4+Vector3.up*2;
            var master=new MasterSummon{masterPrefab=BastionContent.UnitMasters[(int)kind],position=position,rotation=owner.transform.rotation,summonerBodyObject=owner.gameObject,teamIndexOverride=owner.Body.teamComponent.teamIndex,ignoreTeamMemberLimit=true,useAmbientLevel=false}.Perform();
            if(!master)return;var b=master.GetBody();if(!b)return;var u=b.GetComponent<BastionUnit>();u.Owner=owner;u.Slot=slot;u.expires=lifetime>0?Time.time+lifetime:0;owner.Units.Add(u);
            b.baseMaxHealth=owner.Body.maxHealth*BastionUnitRules.HealthMultiplier(kind);b.levelMaxHealth=0;b.baseDamage=owner.Body.damage;b.levelDamage=0;b.baseRegen=0;b.levelRegen=0;b.baseArmor=BastionUnitRules.Armor(kind);b.levelArmor=0;b.RecalculateStats();b.healthComponent.Networkhealth=b.maxHealth*Mathf.Clamp(health,.01f,1);
        }
        private void Start(){body=GetComponent<CharacterBody>();if(!Application.isBatchMode)BuildModel();}
        private void Resolve(){if(Owner||!body||!body.master||!body.master.minionOwnership)return;var m=body.master.minionOwnership.ownerMaster;if(m&&m.GetBody())Owner=m.GetBody().GetComponent<BastionController>();}
        internal void Dock()
        {
            if(!NetworkServer.active||docking)return;docking=true;
            if(Owner){if(Slot>=0&&Owner.Ledger&&body&&body.healthComponent.alive)Owner.Ledger.DockedHealth[Slot]=body.healthComponent.health/body.healthComponent.fullHealth;Owner.Units.Remove(this);}
            var master=body?body.master:null;NetworkServer.Destroy(gameObject);if(master)NetworkServer.Destroy(master.gameObject);
        }
        private void FixedUpdate()
        {
            if(!NetworkServer.active||!body)return;Resolve();if(!Owner||!Owner.Alive||!Owner.Sieged||expires>0&&Time.time>=expires){Dock();return;}
            if(!body.healthComponent.alive)return;
            if(Time.time>=sync){sync=Time.time+1;new BastionUnitStats{Unit=body.netId,Owner=Owner.Body.netId,Health=body.baseMaxHealth,Damage=body.baseDamage,Slot=Slot}.Send(NetworkDestination.Clients);}
            var offset=Owner.Body.corePosition-transform.position;if(offset.sqrMagnitude>900&&body.rigidbody)body.rigidbody.velocity=offset.normalized*12;
            if(Time.time>=attack)
            {
                attack=Time.time+BastionUnitRules.ShotInterval(Kind);
                if(Kind==BastionUnitKind.Repair)
                {
                    float radius=B.Get(S.RepairRange);
                    BastionUpgradeEffects.RepairPulse(Owner,body.corePosition);
                    var ally=CharacterBody.readOnlyInstancesList.Where(b=>b&&b.teamComponent&&b.teamComponent.teamIndex==body.teamComponent.teamIndex&&b.healthComponent&&b.healthComponent.alive&&(b.corePosition-body.corePosition).sqrMagnitude<radius*radius&&BastionUnitRules.RepairPriority(b.healthComponent.health,b.healthComponent.fullHealth)<float.PositiveInfinity).OrderBy(b=>BastionUnitRules.RepairPriority(b.healthComponent.health,b.healthComponent.fullHealth)).FirstOrDefault();
                    if(ally&&ally.healthComponent.health<ally.healthComponent.fullHealth&&B.Get(S.RepairHealing)>0){ally.healthComponent.Heal(ally.maxHealth*B.Fraction(S.RepairHealing)*B.Get(S.RepairInterval),default(ProcChainMask));Owner.Emit(BastionFxKind.Repair,body.corePosition,ally.corePosition);}
                }
                else if(Kind==BastionUnitKind.Interceptor)Intercept();
                else
                {
                    float range=BastionUnitRules.ShotRange(Kind);
                    var target=BastionCombat.Enemies(body,body.corePosition,range).Where(b=>BastionCombat.Visible(body.corePosition,b)).OrderBy(b=>(b.corePosition-body.corePosition).sqrMagnitude).FirstOrDefault();
                    if(target){var from=body.corePosition;new BulletAttack{owner=gameObject,weapon=gameObject,origin=from,aimVector=(target.corePosition-from).normalized,damage=Owner.Body.damage*BastionUnitRules.ShotDamage(Kind),maxDistance=range,procCoefficient=B.Get(S.DroneProc),radius=.3f,hitMask=LayerIndex.CommonMasks.bullet,stopperMask=LayerIndex.CommonMasks.bullet,isCrit=Owner.Body.RollCrit(),falloffModel=BulletAttack.FalloffModel.None}.Fire();Owner.Emit(BastionFxKind.UnitShot,from,target.corePosition);}
                }
            }
            if(Kind==BastionUnitKind.Guardian&&Time.time>=pulse)
            {
                pulse=Time.time+B.Get(S.BulwarkPulse);Owner.Emit(BastionFxKind.Guardian,body.corePosition,Vector3.up);
                foreach(var enemy in BastionCombat.Enemies(body,body.corePosition,Mathf.Max(B.Get(S.BulwarkPullRadius),B.Get(S.BulwarkTauntRange))).ToArray())
                {
                    if(!BastionCombat.Visible(body.corePosition,enemy))continue;
                    if((enemy.corePosition-body.corePosition).sqrMagnitude<B.Get(S.BulwarkPullRadius)*B.Get(S.BulwarkPullRadius)){if(!enemy.isBoss){var force=(body.corePosition-enemy.corePosition).normalized*B.Get(S.BulwarkPullForce);if(enemy.characterMotor)enemy.characterMotor.velocity+=force;else if(enemy.rigidbody)enemy.rigidbody.AddForce(force,ForceMode.VelocityChange);}var hurt=enemy.GetComponent<SetStateOnHurt>();if(hurt&&hurt.canBeStunned&&B.Get(S.BulwarkStun)>0)hurt.SetStun(B.Get(S.BulwarkStun));}
                    if(enemy.master&&B.Get(S.BulwarkTauntDuration)>0&&(enemy.corePosition-body.corePosition).sqrMagnitude<=B.Get(S.BulwarkTauntRange)*B.Get(S.BulwarkTauntRange))foreach(var ai in enemy.master.GetComponents<BaseAI>()){ai.currentEnemy.gameObject=gameObject;ai.currentEnemy.bestHurtBox=body.mainHurtBox;ai.enemyAttention=B.Get(S.BulwarkTauntDuration);}
                }
            }
        }
        private void Intercept()
        {
            var candidates=Object.FindObjectsOfType<ProjectileController>();
            float radius=B.Get(S.InterceptorRange);
            var projectile=candidates.Where(p=>p&&p.teamFilter&&p.teamFilter.teamIndex!=body.teamComponent.teamIndex&&!p.cannotBeDeleted&&(p.transform.position-body.corePosition).sqrMagnitude<radius*radius).OrderBy(p=>(p.transform.position-body.corePosition).sqrMagnitude).FirstOrDefault();
            if(projectile){Owner.Emit(BastionFxKind.Intercept,body.corePosition,projectile.transform.position);NetworkServer.Destroy(projectile.gameObject);attack=Time.time+B.Get(S.InterceptorCooldown);}
        }
        private void BuildModel()
        {
            var locator=body.modelLocator;if(!locator||!locator.modelTransform)return;
            var model=locator.modelTransform;var cm=model.GetComponent<CharacterModel>();var old=model.GetComponentsInChildren<Renderer>();foreach(var renderer in old)renderer.enabled=false;
            var infos=new System.Collections.Generic.List<CharacterModel.RendererInfo>();
            var rig=BastionModel.Rig(model.gameObject,"unit_"+Kind.ToString().ToLowerInvariant(),1,infos,out var bones);
            if(cm){cm.baseRendererInfos=infos.ToArray();cm.itemDisplayRuleSet=null;cm.DisableAllItemDisplays();}
            var visual=gameObject.AddComponent<BastionUnitVisual>();visual.Rig=rig;visual.Bones=bones;visual.Hidden=old;
        }
        private void OnDestroy(){if(NetworkServer.active&&Owner){Owner.Units.Remove(this);if(!docking&&Slot>=0){Owner.Bank.Construction=Mathf.Max(Owner.Bank.Construction,B.Get(S.DroneRebuild));BastionUpgradeEffects.DroneLost(Owner);}}}
    }
}
