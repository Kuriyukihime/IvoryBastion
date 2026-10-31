using EntityStates;
using UnityEngine;

namespace IvoryBastion
{
    public sealed class BastionDeath:GenericCharacterDeath
    {
        // Do not let GenericCharacterDeath.OnExit destroy a successfully revived
        // model merely because the old death state spent four seconds airborne.
        protected override bool shouldAutoDestroy=>!healthComponent||!healthComponent.alive;
        protected override void PlayDeathSound()
        {
            if(characterBody&&!Application.isBatchMode)BastionAudio.Cue("shutdown",characterBody.corePosition);
        }
        protected override void CreateDeathEffects()
        {
            if(!characterBody||Application.isBatchMode)return;
            var c=GetComponent<BastionController>();float radius=c&&c.Sieged?4:1.3f;
            BastionFx.Pulse(characterBody.corePosition,radius,.7f,BastionModel.Blue,true);
            BastionFx.Pulse(characterBody.footPosition,radius*1.2f,.7f,BastionModel.White);
        }
    }
}
