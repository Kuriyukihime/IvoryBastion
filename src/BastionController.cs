using B = IvoryBastion.BastionBalance;
using S = IvoryBastion.BastionSetting;
using System.Collections.Generic;
using EntityStates;
using System.Linq;
using R2API.Networking;
using R2API.Networking.Interfaces;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
namespace IvoryBastion
{
    internal sealed class BastionLedger:MonoBehaviour
    {internal BastionBank Bank=new BastionBank();internal readonly float[] DockedHealth=new float[3];internal float NextAegisVoice;}
    public sealed class BastionController:MonoBehaviour
    {
        internal static readonly List<BastionController> Instances=new List<BastionController>();
        internal BastionBank Bank=new BastionBank();internal BastionLedger Ledger;
        internal CharacterBody Body;internal bool Sieged,Deploying,BoostRequested;internal Vector3 Anchor;
        internal bool TransformTargetSiege;
        internal readonly BastionTransformSync TransformSync=new BastionTransformSync();
        internal float LastHurt=-99,LastCombat=-99,NoticeUntil;internal string Notice="";
        internal bool Boosting=>Alive&&!Sieged&&!Deploying&&Bank.Thruster.Active;
        internal bool Alive=>Body&&Body.healthComponent&&Body.healthComponent.alive;
        internal readonly BastionShieldReserve AbilityShield=new BastionShieldReserve();
        internal readonly BastionAegisReserve AegisBarrier=new BastionAegisReserve();
        internal float LastAegisImpact=-99;
        // Pre/post-conversion health ratio; capacity follows the live max HP.
        internal float AbilityShieldBase,LastShieldImpact=-99,LastShieldHit=-99;internal int ShieldStatDepth;internal bool RestoreFlightShield;
        internal bool AbilityShieldActive=>Alive&&BastionDefenseRules.ShieldActive(AbilityShield.Charge);
        internal BastionAnimation Model=>Body&&Body.modelLocator&&Body.modelLocator.modelTransform?Body.modelLocator.modelTransform.GetComponent<BastionAnimation>():null;
        internal string DeploymentFailure=$"NO CLEAR SPACE WITHIN {B.Get(S.DeployHeight):0.#}m OF GROUND";
        private BastionTransformResult deploymentResult=BastionTransformResult.Blocked;
        internal readonly List<BastionUnit> Units=new List<BastionUnit>();
        internal readonly Dictionary<HealthComponent,float> Reservations=new Dictionary<HealthComponent,float>();
        private float sync;private int pistolSequence;private uint shotId;private bool initialized,lastRed,lastBoost;
        private CameraTargetParams cameraTarget;private CameraTargetParams.CameraParamsOverrideHandle cameraOverride;
        private readonly BastionRepair repairPulse=new BastionRepair();
        private readonly BastionProductionClock productionClock=new BastionProductionClock();
        private Run productionRun;
        internal static bool RunTimerAdvancing=>Run.instance&&!Run.instance.isRunStopwatchPaused;
        private void Awake(){Body=GetComponent<CharacterBody>();BastionBodyStates.Bind(gameObject);}
        private void OnDisable(){productionClock.Reset();productionRun=null;}
        private void Start(){Instances.Add(this);var aim=new GameObject("IvoryAimOrigin").transform;aim.SetParent(transform,false);aim.localPosition=Vector3.up*.25f;Body.aimOriginTransform=aim;ApplyMode(false);}
        private void Init()
        {
            if(initialized||!Body.master)return;initialized=true;
            if(NetworkServer.active){Ledger=Body.master.GetComponent<BastionLedger>()??Body.master.gameObject.AddComponent<BastionLedger>();Bank=Ledger.Bank;Bank.RefundBarrage();Body.MarkAllStatsDirty();}
        }
        internal bool Ready(BastionAction a)
        {
            if(!Alive||Deploying)return false;
            switch(a)
            {
                case BastionAction.Pistol:return !Sieged;
                case BastionAction.Laser:case BastionAction.Missile:return Sieged&&(Bank.Laser<=0||Bank.Ammo>=1&&Bank.Reserved==0);
                case BastionAction.Transform:return Bank.Toggle<=0;
                case BastionAction.Boost:return !Sieged&&Bank.Thruster.CanStart;
                case BastionAction.Protocol:return Sieged&&Bank.ProtocolCharges>0;
                case BastionAction.Redline:return !Sieged&&Bank.RedlineCharges>0;
                case BastionAction.Saturation:return Sieged&&Bank.SaturationCharges>0&&Bank.Reserved==0;
                default:return false;
            }
        }
        internal float Cooldown(BastionAction a)=>a==BastionAction.Laser?Bank.Laser:a==BastionAction.Boost?(1-Bank.Thruster.Fraction)*B.Get(S.BoostRefill)+Bank.Thruster.Recovery:a==BastionAction.Protocol?Bank.Protocol:a==BastionAction.Transform?Bank.Toggle:a==BastionAction.Redline?Bank.Redline:a==BastionAction.Saturation?Bank.Saturation:0;
        internal float Interval(BastionAction a)=>a==BastionAction.Laser?Bank.LaserInterval:a==BastionAction.Boost?B.Get(S.BoostRefill):a==BastionAction.Protocol?B.Get(S.AegisCooldown):a==BastionAction.Transform?BastionDefenseRules.ModeSwitchCooldown:a==BastionAction.Redline?B.Get(S.ShiftCooldown):a==BastionAction.Saturation?B.Get(S.SkyfallCooldown):0;
        internal float Recharge(float seconds,GenericSkill slot)=>slot?BastionBank.RechargeSeconds(slot.cooldownOverride>0?slot.cooldownOverride:seconds,slot.cooldownScale,slot.flatCooldownReduction,slot.temporaryCooldownPenalty):seconds;
        internal bool CanDeploy()=>TryDeploymentAnchor(out _);
        private bool NearGround(Vector3 foot)
        {
            bool hit=Physics.Raycast(foot+Vector3.up*.15f,Vector3.down,out var floor,BastionDefenseRules.DeploymentHeight+.15f,LayerIndex.world.mask,QueryTriggerInteraction.Ignore);
            return BastionDefenseRules.CanDeploy(hit?floor.distance-.15f:0,hit,hit?floor.normal.y:0);
        }
        private bool TryDeploymentAnchor(out Vector3 destination)
        {
            if(!Body){destination=Vector3.zero;return false;}
            DeploymentFailure=$"TOO HIGH: DEPLOY WITHIN {B.Get(S.DeployHeight):0.#}m OF SOLID GROUND";
            deploymentResult=BastionTransformResult.TooHigh;
            if(!NearGround(Body.footPosition)){destination=Vector3.zero;return false;}
            DeploymentFailure=$"NO CLEAR SPACE WITHIN {B.Get(S.DeployHeight):0.#}m OF GROUND";
            deploymentResult=BastionTransformResult.Blocked;
            return BastionFlightGeometry.FindAnchor(Body.footPosition,Body.inputBank.aimDirection,center=>
            {
                // Only the central collision capsule needs space. Outer supports
                // can hover over slopes, ledges or empty air.
                if(Physics.CheckCapsule(center+Vector3.up*.5f,center-Vector3.up*.5f,1.5f,LayerIndex.world.mask,QueryTriggerInteraction.Ignore))return false;
                Vector3 delta=center-transform.position;
                return delta.sqrMagnitude<.001f||!Physics.SphereCast(transform.position,.23f,delta.normalized,out _,delta.magnitude,LayerIndex.world.mask,QueryTriggerInteraction.Ignore);
            },out destination,NearGround);
        }
        internal BastionTransformResult SetSiege(bool value)
        {
            if(!NetworkServer.active||!Alive)return BastionTransformResult.Unavailable;
            // Explicit desired form makes a repeated/stale intent idempotent.
            if(Sieged==value)return BastionTransformResult.Applied;
            if(Bank.Toggle>0)return BastionTransformResult.Cooldown;
            Vector3 destination=Vector3.zero;if(value&&!TryDeploymentAnchor(out destination))return deploymentResult;
            Sieged=value;Deploying=false;
            if(!value){Bank.RefundBarrage();DockUnits();}
            Body.SetBuffCount(BastionContent.Anchored.buffIndex,value?1:0);ApplyMode(true,value?(Vector3?)destination:null);
            // Override changes may reset GenericSkill stocks through the item
            // bridge. Start the shared lock only after those overrides settle.
            Bank.Toggle=Recharge(BastionDefenseRules.ModeSwitchCooldown,Body.skillLocator.utility);
            RestoreFlightShield=!value&&B.Enabled(S.PackRefillsShield);Body.RecalculateStats();
            return BastionTransformResult.Applied;
        }
        internal void ApplyMode(bool relocate,Vector3? deploymentAnchor=null)
        {
            if(!Body)return;
            float height=BastionVisualRules.Height(Sieged),radius=BastionVisualRules.Radius(Sieged);
            var motor=Body.characterMotor;Vector3 foot=Body.footPosition;
            Vector3 position=deploymentAnchor??(foot+Vector3.up*(height*.5f));
            if(motor&&motor.Motor){motor.Motor.SetCapsuleDimensions(radius,height,0);if(relocate&&(NetworkServer.active||Body.hasEffectiveAuthority)){motor.Motor.ForceUnground();motor.Motor.SetPosition(position);motor.velocity=Vector3.zero;}}
            if(Body.mainHurtBox)
            {
                var hb=Body.mainHurtBox;hb.transform.SetParent(transform,false);hb.transform.localPosition=Vector3.zero;hb.transform.localRotation=Quaternion.identity;hb.transform.localScale=Vector3.one;
                var old=hb.GetComponent<Collider>();if(old)old.enabled=false;
                var cap=hb.GetComponent<CapsuleCollider>()??hb.gameObject.AddComponent<CapsuleCollider>();cap.enabled=true;cap.isTrigger=false;cap.center=Vector3.zero;cap.radius=radius;cap.height=height;cap.direction=1;
            }
            if(Body.modelLocator&&Body.modelLocator.modelTransform){var m=Body.modelLocator.modelTransform;m.localPosition=new Vector3(m.localPosition.x,-height*.5f,m.localPosition.z);}
            BastionVisualSizing.SetBodySize(Body,Sieged);
            if(!Sieged&&NetworkServer.active&&Alive){float heal=repairPulse.Flush();if(heal>0)Body.healthComponent.Heal(heal,default(ProcChainMask));}
            Anchor=relocate?position:transform.position;Body.MarkAllStatsDirty();
            if(!cameraTarget)cameraTarget=GetComponent<CameraTargetParams>();
            if(cameraTarget){cameraTarget.RemoveParamsOverride(cameraOverride,.35f);cameraOverride=cameraTarget.AddParamsOverride(new CameraTargetParams.CameraParamsOverrideRequest{priority=1,cameraParamsData=new CharacterCameraParamsData{idealLocalCameraPos=new Vector3(0,0,Sieged?-11:-6),pivotVerticalOffset=Sieged?1.0f:.25f}},.35f);}
            var sl=Body.skillLocator;if(sl)
            {
                var slots=new[]{sl.primary,sl.secondary,sl.utility,sl.special};
                for(int i=0;i<4;i++)if(slots[i]){if(Sieged)slots[i].SetSkillOverride(this,BastionContent.Siege[i],GenericSkill.SkillOverridePriority.Contextual);else slots[i].UnsetSkillOverride(this,BastionContent.Siege[i],GenericSkill.SkillOverridePriority.Contextual);}
            }
        }
        internal void Shoot(BastionAction a,Ray aim)
        {
            // BastionShot owns the fire interval on the body's input authority.
            // The native state channel validates ownership and carries each shot's
            // aim. Packet delivery can bunch those valid state transitions; a
            // second arrival-time cooldown would silently delete their attacks.
            if(!NetworkServer.active||!Alive||Deploying)return;
            aim.origin=Body.inputBank.aimOrigin;if(!BastionCombat.Finite(aim.direction))return;aim.direction.Normalize();
            if(a==BastionAction.Laser){if(!Sieged||Bank.Laser>0)return;var target=BastionLaserTracker.Find(this,aim.direction);if(!target)return;Bank.Laser=Recharge(Bank.LaserInterval,Body.skillLocator.primary);var end=BastionCombat.Bullet(this,aim,Bank.LaserDamage,BastionLaserRules.Range,B.Get(S.LaserProc),true,out _,target);Emit(BastionFxKind.Laser,Muzzle(true),end);}
            else if(a==BastionAction.Pistol){if(Sieged)return;var end=BastionCombat.Bullet(this,aim,BastionBank.NeedleDamage,B.Get(S.NeedleRange),B.Get(S.NeedleProc),false,out bool damaged);Bank.LoadNeedleHit(damaged);Emit(BastionFxKind.Pistol,Muzzle(false),end);}
            else if(a==BastionAction.Missile){if(!Sieged||Bank.Reserved>0)return;var target=BastionCombat.Select(this,B.Get(S.MissileRange),aim.direction);if(!target)return;if(!Bank.SpendMissile(false,out bool heavy))return;Launch(target,heavy,false);}
            else return;
            LastCombat=Time.time;
        }
        internal void ActivateSecondary()
        {
            if(!NetworkServer.active||!Alive)return;
            if(Sieged)
            {
                if(!Bank.Spend(BastionAction.Protocol,Recharge(B.Get(S.AegisCooldown),Body.skillLocator.secondary)))return;
                Bank.DefenseTime=B.Get(S.AegisDuration);
                AegisBarrier.Grant(Body.maxHealth*Bank.BarrierFraction,Body.healthComponent.fullBarrier);
                if(Bank.Upgrades.Active)BastionUpgradeEffects.AegisCast(this);
                else if(Bank.RenewalShield)BastionAbilityShield.Restore(this,AbilityShield.Capacity*B.Fraction(S.RenewalRestore));
                if(B.Get(S.AegisGuardian)>0)BastionUnit.Spawn(this,-1,BastionUnitKind.Guardian,1,B.Get(S.AegisGuardian));
                Init();
                bool voice=Ledger&&Time.time>=Ledger.NextAegisVoice;
                if(voice)Ledger.NextAegisVoice=Time.time+30;
                Emit(BastionFxKind.Protocol,Body.corePosition,Vector3.up,null,0,voice?1:0);
            }
            SendStatus();
        }
        internal void ActivateSpecial()
        {
            if(!NetworkServer.active||!Alive)return;
            if(Sieged)
            {
                if(!Ready(BastionAction.Saturation)||!BastionCombat.Enemies(Body,Body.corePosition,BastionWarheadGeometry.Range).Any())return;
                // Skyfall supplies its own ten siege warheads. Never reserve, spend
                // or replenish the primary's magazine while activating the ultimate.
                if(!Bank.Spend(BastionAction.Saturation,Recharge(B.Get(S.SkyfallCooldown),Body.skillLocator.special)))return;
                if(!BastionWarheads.Launch(this,ref shotId)){Bank.Restock(BastionAction.Saturation);SendStatus();return;}
                LastCombat=Time.time;Emit(BastionFxKind.Saturation,Body.corePosition,Vector3.up);
            }
            else{if(!Ready(BastionAction.Redline))return;Bank.Spend(BastionAction.Redline,Recharge(B.Get(S.ShiftCooldown),Body.skillLocator.special));Bank.RedlineTime=Bank.ShiftDuration;BastionUpgradeEffects.BlueShiftCast(this);Bank.Thruster.Restore(Bank.Thruster.Capacity*B.Fraction(S.ShiftRestore));Emit(BastionFxKind.Redline,Body.corePosition,Vector3.up);}
            Body.MarkAllStatsDirty();SendStatus();
        }
        private Vector3 Muzzle(bool laser){var model=Model;return model?model.Muzzle(laser,pistolSequence++):Body.inputBank.aimOrigin;}
        internal Vector3 MissileOrigin(CharacterBody target=null)
        {
            var model=Model;Vector3 from=model?model.MissilePort(shotId+1):Body.corePosition;
            // Broad wings can overlap decorative terrain. Fall back to the clear
            // center of the fortress rather than spawn a missile inside a rock.
            bool clear=BastionCombat.ClearFlight(Body.corePosition,from)&&(!target||BastionCombat.TryMissileRoute(from,target,out _,out _));
            return clear?from:Body.corePosition;
        }
        private void Launch(CharacterBody target,bool heavy,bool ultimate)=>BastionCombat.Launch(this,target,MissileOrigin(target),heavy,ultimate,++shotId);
        private float SampleProductionTime()
        {
            var run=Run.instance;
            if(run!=productionRun){productionClock.Reset();productionRun=run;}
            if(!run){productionClock.Reset();return 0;}
            return productionClock.Sample(run.GetRunStopwatch(),!run.isRunStopwatchPaused,Alive&&Sieged);
        }
        private void FixedUpdate()
        {
            Init();float productionSeconds=NetworkServer.active?SampleProductionTime():0;
            if(!Alive)return;float dt=Time.fixedDeltaTime,protocolSeconds=Bank.DefenseTime;
            float refill=Recharge(B.Get(S.BoostRefill),Body.skillLocator.secondary),protocol=Recharge(B.Get(S.AegisCooldown),Body.skillLocator.secondary),redline=Recharge(B.Get(S.ShiftCooldown),Body.skillLocator.special),saturation=Recharge(B.Get(S.SkyfallCooldown),Body.skillLocator.special);
            if(NetworkServer.active)Bank.Capacities(Body.skillLocator.secondary.bonusStockFromBody,Body.skillLocator.special.bonusStockFromBody,protocol,redline,saturation);
            if(NetworkServer.active||Body.hasEffectiveAuthority)Bank.Thruster.Tick(dt,BoostRequested,!Sieged&&!Deploying,refill,!Sieged?Bank.RedlineTime:0);
            Bank.Tick(dt,Sieged,Time.time-LastCombat<8,protocol,redline,saturation,productionSeconds);
            bool boosting=Boosting;if(boosting!=lastBoost){lastBoost=boosting;Body.MarkAllStatsDirty();if(NetworkServer.active)SendStatus();}
            bool red=!Sieged&&Bank.RedlineTime>0;if(red!=lastRed){lastRed=red;Body.MarkAllStatsDirty();}
            if(Sieged&&Body.characterMotor&&(Body.hasEffectiveAuthority||NetworkServer.active)){Body.characterMotor.velocity=Vector3.zero;Body.characterMotor.moveDirection=Vector3.zero;Body.characterMotor.Motor.SetPosition(Anchor);}
            if(!NetworkServer.active)return;
            BastionAbilityShield.Tick(this,dt);
            AegisBarrier.Tick(dt,Body.healthComponent.fullBarrier);
            Body.SetBuffCount(BastionContent.AegisBarrier.buffIndex,AegisBarrier.Charge>0?1:0);
            Body.SetBuffCount(BastionContent.Overcharged.buffIndex,Sieged&&Bank.DefenseTime>0?1:0);Body.SetBuffCount(BastionContent.Redline.buffIndex,red?1:0);
            Body.SetBuffCount(BastionContent.FlightShield.buffIndex,AbilityShieldActive?1:0);
            float healing=repairPulse.Tick(dt,Sieged,Body.maxHealth,Time.time-LastHurt,
                false,protocolSeconds,Bank.DefenseHealFraction);
            if(healing>0)Body.healthComponent.Heal(healing,default(ProcChainMask));
            if(Sieged)ManageUnits();
            sync-=dt;if(sync<=0){sync=.2f;SendStatus();}
        }
        private void ManageUnits()
        {
            if(!Ledger)return;Units.RemoveAll(u=>!u);
            for(int slot=0;slot<Bank.Slots;slot++)
            {
                var unit=Units.Find(u=>u.Slot==slot);var kind=(BastionUnitKind)Bank.Loadout[slot];
                if(unit&&unit.Kind!=kind){unit.Dock();unit=null;}
                if(unit)continue;
                if(Ledger.DockedHealth[slot]>0){BastionUnit.Spawn(this,slot,kind,Ledger.DockedHealth[slot]);Ledger.DockedHealth[slot]=0;}
                else if(Bank.Construction<=0){BastionUnit.Spawn(this,slot,kind,1);Bank.Construction=B.Get(S.DroneRebuild);}
            }
        }
        private void DockUnits(){foreach(var u in Units.ToArray())if(u)u.Dock();Units.Clear();}
        internal void Emit(BastionFxKind kind,Vector3 a,Vector3 b,CharacterBody target=null,uint id=0,float value=0)
        {
            if(!NetworkServer.active)return;
            var m=new BastionEffect{Owner=Body.netId,Target=target?target.netId:NetworkInstanceId.Invalid,Kind=kind,A=a,B=b,Id=id,Value=value};m.Apply();m.Send(NetworkDestination.Clients);
        }
        internal void SendStatus(){if(NetworkServer.active&&Body)new BastionStatus(this).Send(NetworkDestination.Clients);}
        private void OnDestroy()
        {
            Instances.Remove(this);
            // Remove the old model from camera callbacks before its body-owned
            // hurtboxes are destroyed. Otherwise a corpse can break rendering for
            // the newly revived body with GetNearestHurtBoxDistance exceptions.
            if(Body&&Body.modelLocator&&Body.modelLocator.modelTransform)
            {
                var model=Body.modelLocator.modelTransform;var cm=model.GetComponent<CharacterModel>();
                if(cm)cm.enabled=false;Destroy(model.gameObject);
            }
            if(NetworkServer.active){Bank.RefundBarrage();DockUnits();}
        }
        internal BastionRequests Requests=>GetComponent<BastionRequests>();
        internal bool EnsureInitialized(){if(!Body)Body=GetComponent<CharacterBody>();if(!Body)return false;Init();return initialized;}
    }
}
