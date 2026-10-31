using RoR2;
using UnityEngine;
namespace IvoryBastion
{
    // Every announcement passes the shared listener gate in BastionAudio.
    public sealed class BastionVoice:MonoBehaviour
    {
        private CharacterBody body;private BastionPcmVoice current;
        internal static void Request(CharacterBody body,string line)
        {
            if(!body||!body.healthComponent||!body.healthComponent.alive)return;
            var speaker=body.GetComponent<BastionVoice>()??body.gameObject.AddComponent<BastionVoice>();speaker.body=body;
            var playback=BastionAudio.Cue(line,body.corePosition,1,1,true,body.netId.Value);
            if(playback!=null&&!playback.Stopped)speaker.current=playback;
        }
        private void Update()
        {if(!body||!body.healthComponent||!body.healthComponent.alive){current?.Dispose();current=null;return;}BastionAudio.PositionVoice(current,body.corePosition);}
        private void OnDestroy(){current?.Dispose();}
    }
}
