using System.Collections.Generic;
using RoR2;
using UnityEngine;
namespace IvoryBastion
{
    public sealed class BastionUnitVisual:MonoBehaviour
    {
        internal Transform Rig;internal Transform[] Bones;internal Renderer[] Hidden;
        private static readonly List<BastionUnitVisual> live=new List<BastionUnitVisual>();
        private CharacterBody body;private Vector3 lastPosition;private float born,recoil,activity;private Vector3 velocity;
        private void OnEnable(){live.Add(this);born=Time.time;lastPosition=transform.position;body=GetComponent<CharacterBody>();}
        private void OnDisable(){live.Remove(this);}
        internal static void Cue(Vector3 at)
        {
            BastionUnitVisual closest=null;float distance=4;
            foreach(var v in live)if(v&&v.body){float d=(v.body.corePosition-at).sqrMagnitude;if(d<distance){distance=d;closest=v;}}
            if(closest){closest.recoil=1;closest.activity=1;}
        }
        private void LateUpdate()
        {
            if(!Rig)return;foreach(var r in Hidden)if(r)r.enabled=false;
            float dt=Mathf.Min(.05f,Time.deltaTime),age=Time.time-born,settle=Mathf.SmoothStep(0,1,age/.55f);
            var actual=(transform.position-lastPosition)/Mathf.Max(.001f,dt);lastPosition=transform.position;
            velocity=Vector3.Lerp(velocity,transform.InverseTransformDirection(Vector3.ClampMagnitude(actual,25)),1-Mathf.Exp(-5*dt));
            recoil=Mathf.Lerp(recoil,0,1-Mathf.Exp(-12*dt));activity=Mathf.MoveTowards(activity,0,dt);
            Rig.localPosition=Vector3.up*(Mathf.Sin(Time.time*2.2f)*.025f+(1-settle)*.18f)+Vector3.back*recoil*.06f;
            Rig.localRotation=Quaternion.Slerp(Rig.localRotation,Quaternion.Euler(Mathf.Clamp(velocity.z*.7f,-12,12)-recoil*5,0,-Mathf.Clamp(velocity.x*1.1f,-16,16)),1-Mathf.Exp(-8*dt));
            foreach(var b in Bones)
            {
                if(b.name.StartsWith("wing_")){float sign=b.name.EndsWith("l")?-1:1;b.localRotation=Quaternion.Euler(-Mathf.Min(velocity.magnitude,20)*.3f,sign*(1-settle)*42,sign*(Mathf.Sin(Time.time*2)*2+activity*9+recoil*6));}
                else if(b.name=="sensor")b.localRotation=Quaternion.Euler(Mathf.Sin(Time.time*.7f)*6-activity*5,Mathf.Sin(Time.time*.55f)*28*(1-activity),0);
            }
        }
    }
}
