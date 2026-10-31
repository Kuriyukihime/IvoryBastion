using B=IvoryBastion.BastionBalance;
using S=IvoryBastion.BastionSetting;
using R2API.Networking;
using R2API.Networking.Interfaces;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace IvoryBastion
{
    internal sealed class BastionUtilityLedger:MonoBehaviour
    {internal readonly BastionUtilityReserve Wingburst=new BastionUtilityReserve{Stock=2,Capacity=2,Interval=10};internal readonly BastionUtilityReserve Mines=new BastionUtilityReserve{Interval=24};}

    public sealed class BastionUtility:MonoBehaviour
    {
        internal BastionUtilityReserve WingburstReserve=new BastionUtilityReserve{Stock=2,Capacity=2,Interval=10},MinesReserve=new BastionUtilityReserve{Interval=24};
        internal BastionUtilityReserve Reserve=>Controller&&Controller.Sieged?MinesReserve:WingburstReserve;
        internal BastionController Controller;
        internal BastionUtilityKind Kind;
        internal BastionUtilityKind CastKind;
        internal float Age;
        internal uint CastId;
        internal Vector3 Direction=Vector3.forward,BeamEnd;
        internal CharacterBody Target;
        internal bool Active=>Kind!=BastionUtilityKind.None;
        internal int Cost=>1;
        internal bool Ready=>Controller&&Controller.Alive&&!Controller.Deploying&&!Active&&Reserve.Stock>=Cost;
        private bool initialized;
        private uint sequence,receivedSequence;
        private float nextStatus;
        private int shots;
        private BastionRushTargetPlan rushPlan;
        internal int CastShots;
        internal float CastWindup,CastSpan,CastDistance,CastFieldRadius;
        private uint motionCast;private float motionAge;private bool motionActive;private Vector3 motionDirection;
        private float motionWindup,motionDuration,motionDistance;
        internal BastionUtilityKind VisualKind=>Controller&&Controller.Body&&Controller.Body.hasEffectiveAuthority&&motionActive?BastionUtilityKind.Wingburst:Kind;
        internal float VisualAge=>VisualKind==BastionUtilityKind.Wingburst&&Controller.Body.hasEffectiveAuthority&&motionCast==CastId?motionAge:Age;
        private BastionAnimation cachedModel;
        private Transform left,right;
        private void Awake(){Controller=GetComponent<BastionController>();}
        private bool Initialize()
        {
            if(initialized)return true;
            if(!Controller||!Controller.Body||!Controller.Body.master)return false;
            if(NetworkServer.active)
            {
                var master=Controller.Body.master;
                var ledger=master.GetComponent<BastionUtilityLedger>()??master.gameObject.AddComponent<BastionUtilityLedger>();
                WingburstReserve=ledger.Wingburst;MinesReserve=ledger.Mines;
            }
            initialized=true;return true;
        }
        internal float Cooldown=>Controller?(Controller.Sieged?B.Get(S.MineCooldown):BastionUtilityUpgrades.RushCooldown(Controller.Bank)):10;
        private void CacheBones()
        {
            var model=Controller?Controller.Model:null;if(model==cachedModel)return;cachedModel=model;left=right=null;
            if(!model)return;
            foreach(var bone in model.MobileBones){if(bone.name=="tip_wing_l")left=bone;else if(bone.name=="tip_wing_r")right=bone;}
        }
        internal Vector3 WingOrigin(int shot)
        {
            CacheBones();var bone=shot%2==0?left:right;
            var center=Controller.Body.corePosition;
            var point=bone?bone.position:center+transform.right*(shot%2==0?-.65f:.65f);
            return BastionCombat.ClearFlight(center,point)?point:center;
        }
        internal bool TryCast(bool siege,Vector3 direction,out string reason)
        {
            reason="UTILITY UNAVAILABLE";
            if(!NetworkServer.active||!Initialize()||!Ready||siege!=Controller.Sieged)return false;
            if(siege&&!BastionServerSpiderMine.Ground(Controller.Body.corePosition,out _)){reason="WIDOW ARRAY: NO GROUND — CHARGE SAVED";return false;}
            if(!BastionCombat.Finite(direction)||direction.sqrMagnitude<.0001f)direction=Controller.Body.inputBank.aimDirection;
            if(!BastionCombat.Finite(direction)||direction.sqrMagnitude<.0001f)direction=transform.forward;
            if(!Reserve.Spend(Controller.Recharge(Cooldown,Controller.Body.skillLocator.utility)))return false;
            // Freeze the accepted salvo and movement profile. A purchase or a
            // late rank snapshot cannot shorten/expand an already accepted cast.
            CastShots=siege?BastionUtilityUpgrades.MineCount(Controller.Bank):BastionUtilityUpgrades.RushShots(Controller.Bank);
            CastWindup=siege?BastionMineRules.DeployWindup:B.Get(S.WingburstWindup);
            CastSpan=siege?CastShots*B.Get(S.MineLaunchInterval):B.Get(S.WingburstDuration);
            CastDistance=siege?0:BastionUtilityUpgrades.RushDistance(Controller.Bank);
            CastFieldRadius=BastionUtilityUpgrades.MineFieldRadius(Controller.Bank);
            Direction=direction.normalized;Age=0;shots=0;Target=null;
            rushPlan=siege?null:new BastionRushTargetPlan();
            Kind=siege?BastionUtilityKind.Mines:BastionUtilityKind.Wingburst;
            CastKind=Kind;
            CastId=BastionRequestRules.Next(ref castSequence);
            if(!siege&&Controller.Body.hasEffectiveAuthority)BeginMotion(CastId,Direction);
            mineCenter=Controller.Body.corePosition;
            mineLeft=Controller.Model?Controller.Model.Siege.TransformPoint(BastionMineRules.Mount(0)+Vector3.up*.1f):mineCenter+Vector3.up;
            mineRight=Controller.Model?Controller.Model.Siege.TransformPoint(BastionMineRules.Mount(1)+Vector3.up*.1f):mineCenter+Vector3.up;
            BeamEnd=mineCenter;
            if(!siege)BastionUpgradeEffects.RushCast(Controller);
            Controller.LastCombat=Time.time;Send();return true;
        }
        private uint castSequence,microSequence,mineSequence;private Vector3 mineCenter,mineLeft,mineRight;
        internal int ActiveMines;
        internal float ChainFeedbackUntil;internal int ChainFeedbackCount;
        internal void ChainFeedback()
        {
            if(Time.unscaledTime>ChainFeedbackUntil)ChainFeedbackCount=0;
            ChainFeedbackCount++;ChainFeedbackUntil=Time.unscaledTime+1.4f;
        }
        private void FixedUpdate()
        {
            if(!Initialize())return;
            float dt=Time.fixedDeltaTime;
            if(NetworkServer.active&&Controller.Body.skillLocator&&Controller.Body.skillLocator.utility)
            {
                int bonus=Mathf.Max(0,Controller.Body.skillLocator.utility.bonusStockFromBody);
                WingburstReserve.Resize(2+bonus);MinesReserve.Resize(1+bonus);
                WingburstReserve.Reconfigure(Controller.Recharge(BastionUtilityUpgrades.RushCooldown(Controller.Bank),Controller.Body.skillLocator.utility));
                MinesReserve.Reconfigure(Controller.Recharge(B.Get(S.MineCooldown),Controller.Body.skillLocator.utility));
            }
            if(!Controller.Body.hasEffectiveAuthority){motionActive=false;motionCast=CastId;}
            WingburstReserve.Tick(dt);MinesReserve.Tick(dt);
            if(CastId!=0)Age+=dt;
            if(Active)
            {
                if(!Controller.Alive){End();return;}
                if(NetworkServer.active)TickAttack();
                else if(Age>Duration+.15f){Kind=BastionUtilityKind.None;Target=null;}
            }
            if(NetworkServer.active){nextStatus-=dt;if(nextStatus<=0)Send();}
        }
        internal float Duration=>CastWindup+CastSpan;
        private void TickAttack()
        {
            bool mines=Kind==BastionUtilityKind.Mines;
            float charge=CastWindup,span=CastSpan;
            int count=CastShots;
            int due=BastionUtilityRules.ShotsDue(Age,charge,span,count);
            while(shots<due)
            {
                if(mines)BastionServerSpiderMine.Launch(Controller,BastionRequestRules.Next(ref mineSequence),shots,count,mineCenter,shots%2==0?mineLeft:mineRight,CastFieldRadius);
                else
                {
                    var origin=WingOrigin(shots);
                    var target=BastionUtilityCombat.FindRush(Controller,origin,B.Get(S.WingburstRange),rushPlan);
                    if(target)BastionServerMicroMissile.Launch(Controller,target,origin,Direction,BastionRequestRules.Next(ref microSequence),shots,rushPlan);
                }
                shots++;
            }
            if(Age>=charge+span)End();
        }
        private void End(){Kind=BastionUtilityKind.None;Target=null;if(NetworkServer.active)Send();}
        internal bool Move(CharacterMotor motor)
        {
            if(!Controller.Alive||Controller.Sieged||Controller.Deploying){motionActive=false;return false;}
            if(!motionActive)return false;
            float windup=motionWindup,duration=motionDuration;
            float previous=motionAge;motionAge+=Time.fixedDeltaTime;
            if(previous>=windup+duration){motionActive=false;return false;}
            if(motionAge<=windup)return false;
            motor.moveDirection=motionDirection;
            motor.velocity=motionDirection*BastionUtilityRules.TravelSpeed(previous,Time.fixedDeltaTime,windup,duration,motionDistance);
            return true;
        }
        private void BeginMotion(uint cast,Vector3 direction)
        {motionCast=cast;motionAge=0;motionActive=true;motionDirection=direction;motionWindup=CastWindup;motionDuration=CastSpan;motionDistance=CastDistance;}
        private void Send()
        {if(NetworkServer.active&&Controller&&Controller.Body){ActiveMines=BastionServerSpiderMine.Count(Controller);nextStatus=Active?.075f:.2f;new BastionUtilityStatus(this,BastionRequestRules.Next(ref sequence)).Send(NetworkDestination.Clients);}}
        internal void Receive(BastionUtilityStatus message)
        {
            if(NetworkServer.active||!Controller||!Controller.Body||message.Sequence==0||receivedSequence!=0&&!BastionRequestRules.Newer(message.Sequence,receivedSequence))return;
            receivedSequence=message.Sequence;ActiveMines=Mathf.Clamp(message.ActiveMines,0,BastionMineRules.MaximumMines);
            CastShots=message.Shots;CastWindup=message.Windup;CastSpan=message.Span;CastDistance=message.Distance;CastFieldRadius=message.FieldRadius;
            WingburstReserve.Receive(message.Stock,message.Capacity,message.Remaining,message.Interval);
            MinesReserve.Receive(message.MineStock,message.MineCapacity,message.MineRemaining,message.MineInterval);
            bool same=CastId==message.CastId&&Kind==message.Kind;
            Kind=message.Kind;CastKind=message.CastKind;Age=same?Mathf.Max(Age,message.Age):message.Age;CastId=message.CastId;
            Direction=message.Direction;BeamEnd=message.End;
            // A delayed/duplicate snapshot must not shorten or restart the
            // input owner's collision-aware dash. Native position replication
            // remains in control; this message never teleports the body.
            if(CastId!=0&&CastKind==BastionUtilityKind.Wingburst&&(Kind==BastionUtilityKind.Wingburst||message.Age<=3)&&Controller.Body.hasEffectiveAuthority&&motionCast!=CastId&&!Controller.Sieged&&!Controller.Deploying&&Controller.Alive)BeginMotion(CastId,Direction);
            if(!Controller.Body.hasEffectiveAuthority)motionCast=CastId;
            if(Kind==BastionUtilityKind.Mines)motionActive=false;
            var obj=Util.FindNetworkObject(message.Target);Target=obj?obj.GetComponent<CharacterBody>():null;
        }
    }
}
