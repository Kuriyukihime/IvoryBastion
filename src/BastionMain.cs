using EntityStates;
using UnityEngine;
namespace IvoryBastion
{
    public class BastionMain:GenericCharacterMain
    {
        private bool flight;private float gravity,air;private BastionController c;private BastionUtility utility;
        public override void OnEnter(){base.OnEnter();c=GetComponent<BastionController>();gravity=characterMotor.gravityScale;air=characterMotor.airControl;}
        public override void HandleMovements()
        {
            // Flight owns vertical input. A normal jump would add an impulse on top of it.
            if(c&&c.Alive)jumpInputReceived=false;
            base.HandleMovements();if(!c||!isAuthority)return;
            var facing=Vector3.ProjectOnPlane(aimDirection,Vector3.up);if(characterDirection&&facing.sqrMagnitude>.01f)characterDirection.forward=facing.normalized;
            bool fly=c.Alive;
            if(fly!=flight)
            {
                var f=characterMotor.flightParameters;
                f.channeledFlightGranterCount=Mathf.Max(0,f.channeledFlightGranterCount+(fly?1:-1));
                characterMotor.flightParameters=f;
                // Flight and anti-gravity are separate native capabilities.
                // Custom gravity can bypass gravityScale=0; this grant disables
                // the gravity path itself without replacing other mods' grants.
                var g=characterMotor.gravityParameters;
                g.channeledAntiGravityGranterCount=Mathf.Max(0,g.channeledAntiGravityGranterCount+(fly?1:-1));
                characterMotor.gravityParameters=g;
                characterMotor.Motor.SetGroundSolvingActivation(!fly);
                if(fly&&characterMotor.Motor.GroundingStatus.IsStableOnGround)characterMotor.Motor.ForceUnground();
                flight=fly;
            }
            characterMotor.gravityScale=fly?0:gravity;characterMotor.airControl=fly?1:air;
            // Ground solving normally sends this transition. Preserve airborne
            // bookkeeping once after entering flight, while snapping is disabled.
            if(fly&&!characterMotor.Motor.GroundingStatus.IsStableOnGround&&characterMotor.Motor.LastGroundingStatus.IsStableOnGround)
                characterMotor.PostGroundingUpdate(Time.fixedDeltaTime);
            if(c.Sieged||c.Deploying){characterMotor.moveDirection=Vector3.zero;characterMotor.velocity=Vector3.zero;return;}
            if(!utility)utility=GetComponent<BastionUtility>();
            if(utility&&utility.Move(characterMotor)){jumpInputReceived=false;return;}
            // ForceUnground adds 7.5 mm of altitude every motor tick. Disabling only
            // ground snapping above preserves collisions without that perpetual lift.
            characterMotor.moveDirection=BastionPilotMotion.Direction(moveVector,aimDirection,transform.forward,inputBank.jump.down,c.Boosting);
            characterMotor.velocity=Vector3.Lerp(characterMotor.velocity,characterMotor.moveDirection*characterBody.moveSpeed,1-Mathf.Exp(-12*Time.fixedDeltaTime));jumpInputReceived=false;
        }
        public override void OnExit()
        {
            if(flight)
            {
                var f=characterMotor.flightParameters;
                f.channeledFlightGranterCount=Mathf.Max(0,f.channeledFlightGranterCount-1);characterMotor.flightParameters=f;
                var g=characterMotor.gravityParameters;
                g.channeledAntiGravityGranterCount=Mathf.Max(0,g.channeledAntiGravityGranterCount-1);characterMotor.gravityParameters=g;
                characterMotor.Motor.SetGroundSolvingActivation(true);flight=false;
            }
            characterMotor.gravityScale=gravity;characterMotor.airControl=air;base.OnExit();
        }
    }
}
