using B = IvoryBastion.BastionBalance;
using S = IvoryBastion.BastionSetting;
using EntityStates;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
namespace IvoryBastion
{
    public class BastionPrimary:BaseState
    {
        private BastionController c;
        public override void OnEnter(){base.OnEnter();c=GetComponent<BastionController>();if(isAuthority&&c&&!c.Sieged)Shoot(BastionAction.Pistol);}
        private void Shoot(BastionAction a)=>outer.SetNextState(new BastionShot{Action=a,Aim=GetAimRay()});
        public override void FixedUpdate(){base.FixedUpdate();if(!isAuthority||!c)return;if(!inputBank.skill1.down)Shoot(BastionAction.Laser);else if(fixedAge>=.20f)Shoot(BastionAction.Missile);}
        public override InterruptPriority GetMinimumInterruptPriority()=>InterruptPriority.Skill;
    }
    public class BastionShot:BaseState
    {
        public BastionAction Action;public Ray Aim;private float duration;
        public override void OnEnter()
        {
            base.OnEnter();duration=Action==BastionAction.Laser?.1f:BastionBank.PrimaryFireInterval(Action,attackSpeedStat);
            var c=GetComponent<BastionController>();
            if(isAuthority&&c&&Action==BastionAction.Missile)
            {
                var hint=c.Bank.Reserved>0?"SKYFALL IS USING THE MISSILE BATTERY":c.Bank.Ammo<1?"MISSILE BATTERY RELOADING":!BastionCombat.Select(c,B.Get(S.MissileRange),Aim.direction)?$"MISSILES: NO CLEAR ROUTE WITHIN {B.Get(S.MissileRange):0.#}m":null;
                if(hint!=null){c.Notice=hint;c.NoticeUntil=Time.unscaledTime+.5f;}
            }
            if(isAuthority&&c)c.Requests?.Submit((BastionRequestKind)Action,Aim.direction);
        }
        public override void FixedUpdate(){base.FixedUpdate();if(!isAuthority||fixedAge<duration)return;var c=GetComponent<BastionController>();if(c&&Action!=BastionAction.Laser&&inputBank.skill1.down)outer.SetNextState(new BastionShot{Action=c.Sieged?BastionAction.Missile:BastionAction.Pistol,Aim=GetAimRay()});else outer.SetNextStateToMain();}
        public override void OnSerialize(NetworkWriter w){base.OnSerialize(w);w.Write((byte)Action);w.Write(Aim.origin);w.Write(Aim.direction);}
        public override void OnDeserialize(NetworkReader r){base.OnDeserialize(r);Action=(BastionAction)r.ReadByte();Aim=new Ray(r.ReadVector3(),r.ReadVector3());}
        public override InterruptPriority GetMinimumInterruptPriority()=>InterruptPriority.Skill;
    }
    public class BastionSecondary:BaseState
    {
        public override void OnEnter(){base.OnEnter();if(isAuthority)GetComponent<BastionController>()?.Requests?.Submit(BastionRequestKind.Aegis);}
        public override void FixedUpdate(){base.FixedUpdate();if(isAuthority&&fixedAge>=.2f)outer.SetNextStateToMain();}
        public override InterruptPriority GetMinimumInterruptPriority()=>InterruptPriority.PrioritySkill;
    }
    public class BastionSpecial:BaseState
    {
        public override void OnEnter()
        {
            base.OnEnter();var c=GetComponent<BastionController>();if(!c)return;
            if(isAuthority&&c.Sieged&&!System.Linq.Enumerable.Any(BastionCombat.Enemies(c.Body,c.Body.corePosition,BastionWarheadGeometry.Range))){c.Notice="SKYFALL: NO TARGET / CHARGE SAVED";c.NoticeUntil=Time.unscaledTime+2;}
            if(isAuthority)c.Requests?.Submit(c.Sieged?BastionRequestKind.Skyfall:BastionRequestKind.Redline,GetAimRay().direction);
        }
        public override void FixedUpdate(){base.FixedUpdate();if(isAuthority&&fixedAge>.15f)outer.SetNextStateToMain();}
        public override InterruptPriority GetMinimumInterruptPriority()=>InterruptPriority.Skill;
    }
    public class BastionTransform:BaseState
    {
        private bool deploying,resolved;private uint request;private float duration;private BastionController c;
        public override void OnEnter()
        {
            base.OnEnter();c=GetComponent<BastionController>();if(!c)return;
            if(isAuthority){request=c.TransformSync.NextRequest();deploying=!c.Sieged;}
            duration=BastionTransformSync.Duration(deploying);
            if(isAuthority)
            {
                c.TransformTargetSiege=deploying;c.Deploying=true;
                if(c.Requests?.Submit(BastionRequestKind.Transform,flag:deploying,token:request)==0)
                {c.Deploying=false;c.TransformSync.ReceiveReply(request,BastionTransformResult.Unavailable);}
            }
        }
        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if(!c){if(isAuthority)outer.SetNextStateToMain();return;}
            if(!isAuthority)return;
            if(c.TransformSync.CanRelease(request,deploying,c.Sieged))
            {
                resolved=true;c.Deploying=false;
                if(c.TransformSync.Result!=BastionTransformResult.Applied)
                {
                    c.Model?.CancelTransform();
                    c.Notice=BastionTransformSync.Notice(c.TransformSync.Result);c.NoticeUntil=Time.unscaledTime+3;
                }
                outer.SetNextStateToMain();
            }
            else if(!c.Alive||fixedAge>=BastionTransformSync.Timeout)
            {
                c.Model?.CancelTransform();
                if(c.Alive){c.Notice="TRANSFORM: NO HOST CONFIRMATION / TRY AGAIN";c.NoticeUntil=Time.unscaledTime+3;}
                outer.SetNextStateToMain();
            }
            else if(fixedAge>duration+.35f)
            {
                c.Notice="TRANSFORM: WAITING FOR HOST";c.NoticeUntil=Time.unscaledTime+.3f;
            }
        }
        public override void OnExit()
        {
            if(c)
            {
                // The host job owns completion independently of this visual state.
                if(!NetworkServer.active){if(!resolved)c.Model?.CancelTransform();c.Deploying=false;}
            }
            base.OnExit();
        }
        public override void OnSerialize(NetworkWriter w){base.OnSerialize(w);w.Write(request);w.Write(deploying);}
        public override void OnDeserialize(NetworkReader r){base.OnDeserialize(r);request=r.ReadUInt32();deploying=r.ReadBoolean();}
        public override InterruptPriority GetMinimumInterruptPriority()=>InterruptPriority.Frozen;
    }
    // Purchases use the body's authority-checked native state channel. No public
    // client message can name another player's body and spend its currency.
    public class BastionPurchase:BaseState
    {
        public int Branch,Choice;public bool Loadout;
        public override void OnEnter(){base.OnEnter();if(isAuthority)GetComponent<BastionController>()?.Requests?.Submit(Loadout?BastionRequestKind.Loadout:BastionRequestKind.Purchase,branch:Branch,choice:Choice);}
        public override void FixedUpdate(){base.FixedUpdate();if(isAuthority&&fixedAge>.05f)outer.SetNextStateToMain();}
        public override void OnSerialize(NetworkWriter w){base.OnSerialize(w);w.Write(Branch);w.Write(Choice);w.Write(Loadout);}
        public override void OnDeserialize(NetworkReader r){base.OnDeserialize(r);Branch=r.ReadInt32();Choice=r.ReadInt32();Loadout=r.ReadBoolean();}
        public override InterruptPriority GetMinimumInterruptPriority()=>InterruptPriority.Any;
    }
}
