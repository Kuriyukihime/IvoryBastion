using EntityStates;
using UnityEngine.Networking;
namespace IvoryBastion
{
    // A separate, networked state machine lets the pilot boost while firing or using R.
    public sealed class BastionBoost:BaseState
    {
        private BastionController controller;
        public override void OnEnter()
        {
            base.OnEnter();controller=GetComponent<BastionController>();
            if(!controller)return;
            if(isAuthority&&!controller.Ready(BastionAction.Boost)){outer.SetNextStateToMain();return;}
            if(isAuthority)controller.Requests?.BoostIntent(true);
        }
        public override void FixedUpdate()
        {
            base.FixedUpdate();if(!isAuthority)return;
            // Keep the exhausted state latched until physical release. Fuel may recover,
            // but an unchanged held button must never create repeated boost bursts.
            if(!controller||!controller.Alive||controller.Sieged||controller.Deploying||!inputBank.skill2.down)
                outer.SetNextStateToMain();
            else controller.Requests?.BoostIntent(true);
        }
        public override void OnExit(){if(isAuthority&&controller)controller.Requests?.BoostIntent(false);base.OnExit();}
        public override InterruptPriority GetMinimumInterruptPriority()=>InterruptPriority.PrioritySkill;
    }
}
