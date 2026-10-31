using RoR2;
using UnityEngine;
namespace IvoryBastion
{
    public sealed class BastionEngine:MonoBehaviour
    {
        private BastionController controller;private BastionSiegeThrusters siegeThrusters;private BastionPcmVoice voice;private float retry;
        private void Start(){controller=GetComponent<BastionController>();siegeThrusters=GetComponent<BastionSiegeThrusters>();}
        private void Update()
        {
            if(Application.isBatchMode||!controller)return;var listener=LocalUserManager.GetFirstLocalUser()?.cachedBody;
            if(!controller.Alive||!listener){if(voice!=null)voice.Volume=0;return;}
            float distance=Vector3.Distance(listener.corePosition,controller.Body.corePosition);
            if(distance>45||!BastionPlugin.EnableAudio.Value){if(voice!=null)voice.Volume=0;return;}
            if((voice==null||voice.Stopped)&&Time.unscaledTime>retry){retry=Time.unscaledTime+2;voice=BastionAudio.Engine();}if(voice==null)return;
            float speed=controller.Body.characterMotor?controller.Body.characterMotor.velocity.magnitude:0;
            float lift=siegeThrusters?siegeThrusters.Strength:0;
            voice.Volume=BastionPlugin.AudioVolume.Value*(controller.Sieged?.055f+lift*.035f:.07f+Mathf.Min(speed/20,.6f)*.05f+(controller.Boosting?.09f:0))*Mathf.Pow(1-distance/45,2);
            voice.Pitch=controller.Sieged?.7f+lift*.18f:1+Mathf.Clamp01(speed/20)*.25f+(controller.Boosting?.3f:0);voice.Pan=Mathf.Clamp(Vector3.Dot(listener.transform.right,(transform.position-listener.corePosition).normalized),-.8f,.8f);
        }
        private void OnDestroy(){voice?.Dispose();}
    }
}
