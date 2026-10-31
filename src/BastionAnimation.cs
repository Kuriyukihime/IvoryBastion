using System;
using System.Linq;
using RoR2;
using UnityEngine;
namespace IvoryBastion
{
    [DefaultExecutionOrder(12000)]
    public sealed class BastionAnimation:MonoBehaviour
    {
        public CharacterBody Owner;public Transform Mobile,Siege;public Transform[] MobileBones,SiegeBones;public Renderer[] Original;
        internal bool Showcase;internal float ShowcaseUnfold;
        public CharacterModel.RendererInfo[] MobileRenderers,SiegeRenderers;
        private Vector3[] mr,sr;private Quaternion[] mp,sp;private Light[] lights;private Renderer[] emissive;
        private readonly BastionPoseInput input=new BastionPoseInput();private readonly BastionPoseFrame mobileFrame=new BastionPoseFrame(),siegeFrame=new BastionPoseFrame();
        private readonly float[] hatchTimes=Enumerable.Repeat(-100f,72).ToArray(),hatchAges=new float[72];
        private readonly float[] rackFiredAt={-100f,-100f,-100f,-100f},rackKicks=new float[4];
        private readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
        private float shootUntil,leftKick,rightKick,laserKick,missileKick,deathAge,transformTime=-99,transformSpan,salvoUntil,powerStarted=-99;
        private float leftSkyfallKick,rightSkyfallKick;
        private float laserFiredAt=-99,lastForwardSpeed,acceleration,wingburstPoseAge=-1,minePoseAge=-1;
        private BastionController cachedController;private CharacterModel cachedModel;private BastionUtility utility;
        private float aim,boost,forward,strafe,vertical,brake,turn,pitch,lastSpeed,lastYaw,defense,overdrive,nextDisplays,haloPhase;
        private void Start()
        {
            mr=MobileBones.Select(b=>b.localPosition).ToArray();sr=SiegeBones.Select(b=>b.localPosition).ToArray();
            mp=MobileBones.Select(b=>b.localRotation).ToArray();sp=SiegeBones.Select(b=>b.localRotation).ToArray();
            lights=GetComponentsInChildren<Light>(true);emissive=MobileRenderers.Concat(SiegeRenderers).Where(i=>i.renderer.name.EndsWith("_Blue")||i.renderer.name.EndsWith("_Core")||i.renderer.name.EndsWith("_Reconstructed")).Select(i=>i.renderer).ToArray();
            lastYaw=transform.eulerAngles.y;
            cachedController=Owner?Owner.GetComponent<BastionController>():null;cachedModel=GetComponent<CharacterModel>();
        }
        internal Vector3 Muzzle(bool laser,int sequence)
        {
            var marker=(laser?SiegeBones:MobileBones).FirstOrDefault(b=>b.name==(laser?"cannon_muzzle":sequence%2==0?"muzzle_l":"muzzle_r"));
            if(marker)return marker.position;
            if(laser){var cannon=SiegeBones.FirstOrDefault(b=>b.name=="cannon");if(cannon)return cannon.TransformPoint(new Vector3(0,0,1.81f));}
            int sign=sequence%2==0?-1:1;var fore=MobileBones.FirstOrDefault(b=>b.name==(sign<0?"fore_l":"fore_r"));return fore?fore.TransformPoint(new Vector3(sign*.023f,-.251f,.070f)):transform.position;
        }
        internal Vector3 MissilePort(uint sequence)
        {
            if(SiegeBones.Any(b=>b.name=="cannon_muzzle"))
            {
                string portName=$"hatch_{sequence%4}_{sequence/4%6}_{sequence/24%3}";
                var port=SiegeBones.FirstOrDefault(b=>b.name==portName);
                if(port)return port.TransformPoint(new Vector3(0,0,.04f));
            }
            bool rear=sequence%4>=2;int sign=sequence%2==0?-1:1;string name=(rear?"shield_":"rack_")+(sign<0?"l":"r");var bone=SiegeBones.FirstOrDefault(b=>b.name==name);if(!bone)return transform.position;
            float z=rear?-.59f:.28f,dx=rear?-.17f:0,extra=rear?.16f:0;var pts=new[]{new Vector3(sign*(.79f+dx),.46f,z),new Vector3(sign*(1.16f+dx),.85f,z-.04f),new Vector3(sign*(1.36f+dx),1.50f,z-.12f),new Vector3(sign*(1.20f+dx),2.18f+extra,z-.22f),new Vector3(sign*(.97f+dx),2.59f+extra,z-.26f)};
            int row=(int)(sequence/4%6),col=(int)(sequence/24%3)-1;float f=1+(row+.6f)/6.9f*2.4f;int i=Mathf.Min(3,(int)f);float t=f-i;Vector3 a=pts[Mathf.Max(0,i-1)],b=pts[i],c=pts[i+1],d=pts[Mathf.Min(4,i+2)];var p=.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t);p+=new Vector3(col*(row<4?.145f:.12f),0,.23f);
            return bone.TransformPoint(p-new Vector3(sign*.8f,.47f,z));
        }
        internal Vector3 SkyfallPort(int sequence)
        {
            string suffix=sequence%2==0?"l":"r";
            var marker=SiegeBones.FirstOrDefault(b=>b.name=="skyfall_muzzle_"+suffix);
            return marker?marker.position:Siege.TransformPoint(new Vector3(sequence%2==0?-.78f:.78f,2.60f,-1.94f));
        }
        internal void Cue(BastionFxKind kind,float value=0,Vector3 source=default,uint id=0)
        {
            if(kind==BastionFxKind.Pistol)
            {
                if(Owner&&(source-Owner.corePosition).sqrMagnitude>25)return;
                shootUntil=Time.time+.28f;if((Muzzle(false,0)-source).sqrMagnitude<(Muzzle(false,1)-source).sqrMagnitude)leftKick=1;else rightKick=1;
            }
            if(kind==BastionFxKind.Laser){laserFiredAt=Time.time;laserKick=0;shootUntil=Time.time+.5f;}
            if(kind==BastionFxKind.Missile||kind==BastionFxKind.HeavyMissile)
            {
                missileKick=1;int nearest=0;float best=float.PositiveInfinity;
                // Choose the actual launch port; launch IDs also include other projectiles.
                foreach(var hatch in SiegeBones)if(hatch.name.StartsWith("hatch_"))
                {float distance=(hatch.position-source).sqrMagnitude;if(distance<best){best=distance;nearest=(hatch.name[6]-'0')+(hatch.name[8]-'0')*4+(hatch.name[10]-'0')*24;}}
                hatchTimes[nearest]=Time.time;
                // The replicated source is the actual port, not a projectile
                // sequence (which also includes pistol and Skyfall shots).
                rackFiredAt[nearest%4]=Time.time;
            }
            if(kind==BastionFxKind.SiegeWarhead)
            {
                missileKick=.8f;
                if((SkyfallPort(0)-source).sqrMagnitude<(SkyfallPort(1)-source).sqrMagnitude)leftSkyfallKick=1;else rightSkyfallKick=1;
            }
            if(kind==BastionFxKind.Saturation)salvoUntil=Time.time+4.5f;
            if(kind==BastionFxKind.Redline)powerStarted=Time.time;
            if(kind==BastionFxKind.Transform){transformTime=Time.time;transformSpan=value==0?.8f:value;}
        }
        internal void CancelTransform(){transformTime=-99;transformSpan=0;}
        private static float Damp(float a,float b,float rate,float dt)=>Mathf.Lerp(a,b,1-Mathf.Exp(-rate*dt));
        private static Vector3 U(System.Numerics.Vector3 v)=>new Vector3(v.X,v.Y,v.Z);
        private void Apply(Transform[] bones,Vector3[] rest,Quaternion[] previous,BastionPoseFrame frame,float dt)
        {
            for(int i=0;i<bones.Length;i++)
            {
                if(!frame.Bones.TryGetValue(bones[i].name,out var p))continue;
                string name=bones[i].name;
                float rate=name.StartsWith("hatch_")?32:name.StartsWith("wing_")||name.StartsWith("vane_")?14:name.StartsWith("leg_")||name.StartsWith("boot_")?11:name=="torso"?16:24;
                if(input.WingburstAge>=input.WingburstWindup+input.WingburstDuration)rate=Mathf.Max(rate,28);
                var target=Quaternion.Euler(U(p.Rotation));previous[i]=Quaternion.Slerp(previous[i],target,1-Mathf.Exp(-rate*dt));bones[i].localRotation=previous[i];
                // The recoil envelope already eases the impulse and recovery.
                // A second slow filter hides the breech kick at normal FPS.
                float positionRate=name=="cannon"&&laserKick>.01f?70:rate;
                bones[i].localPosition=Vector3.Lerp(bones[i].localPosition,rest[i]+U(p.Offset),1-Mathf.Exp(-positionRate*dt));
            }
        }
        private void LateUpdate()
        {
            if(mr==null)return;float dt=Mathf.Min(.05f,Time.deltaTime);foreach(var r in Original)if(r)r.enabled=false;
            if(Time.unscaledTime>=nextDisplays){nextDisplays=Time.unscaledTime+.5f;var model=GetComponent<CharacterModel>();if(model){model.itemDisplayRuleSet=null;model.DisableAllItemDisplays();}foreach(var display in GetComponentsInChildren<ItemDisplay>(true))if(display.gameObject.activeSelf)display.gameObject.SetActive(false);}
            if(Owner&&Owner.healthComponent&&!Owner.healthComponent.alive)
            {deathAge+=dt;foreach(var light in lights)if(light)light.intensity=Mathf.Max(0,1.2f-deathAge*2);float fade=1-Mathf.Clamp01((deathAge-.6f)/1f);Mobile.localRotation=Quaternion.Euler(15+deathAge*35,0,deathAge*20);Mobile.localScale=Vector3.one*.78f*fade;Siege.localScale=Vector3.one*1.45f*fade;return;}
            if(deathAge>0){deathAge=0;powerStarted=laserFiredAt=-99;transformTime=-99;transformSpan=0;leftKick=rightKick=laserKick=missileKick=leftSkyfallKick=rightSkyfallKick=0;for(int i=0;i<4;i++){rackFiredAt[i]=-100; rackKicks[i]=0;}Mobile.localRotation=Siege.localRotation=Quaternion.identity;}
            var c=cachedController?cachedController:Owner?Owner.GetComponent<BastionController>():null;cachedController=c;bool sieged=c&&c.Sieged;float now=Time.time;
            bool changing=transformSpan!=0&&(now-transformTime<Mathf.Abs(transformSpan)||c&&c.Deploying&&c.TransformTargetSiege==(transformSpan>0));
            float unfold=changing?Mathf.Clamp01((now-transformTime)/Mathf.Abs(transformSpan)):sieged?1:0;if(changing&&transformSpan<0)unfold=1-unfold;
            if(Showcase&&!Owner)unfold=ShowcaseUnfold;
            // Hand off once the rising seat has emerged, rather than retaining
            // a standing pilot inside the already assembled fortress.
            float pilotHandoff=Showcase&&!Owner?.28f:.34f;
            Mobile.gameObject.SetActive(unfold<pilotHandoff);Siege.gameObject.SetActive(unfold>.015f);BastionVisualSizing.SetRenderers(cachedModel?cachedModel:GetComponent<CharacterModel>(),unfold>=pilotHandoff?SiegeRenderers:MobileRenderers);
            Vector3 velocity=Owner&&Owner.characterMotor?Owner.characterMotor.velocity:Vector3.zero,local=transform.InverseTransformDirection(velocity);float speed=velocity.magnitude;
            float yaw=transform.eulerAngles.y,angular=Mathf.DeltaAngle(lastYaw,yaw)/Mathf.Max(.001f,dt);lastYaw=yaw;
            acceleration=Damp(acceleration,Mathf.Clamp((local.z-lastForwardSpeed)/Mathf.Max(.001f,dt)/55,-1,1),9,dt);lastForwardSpeed=local.z;
            forward=Damp(forward,Mathf.Clamp(local.z/14,-1,1),7,dt);strafe=Damp(strafe,Mathf.Clamp(local.x/14,-1,1),7,dt);vertical=Damp(vertical,Mathf.Clamp(local.y/14,-1,1),6,dt);
            float braking=Mathf.Clamp01((lastSpeed-speed)/Mathf.Max(.001f,dt)/55);
            brake=Damp(brake,braking,braking>brake?14:4.5f,dt);lastSpeed=speed;turn=Damp(turn,Mathf.Clamp(angular/180,-1,1),6,dt);
            aim=Damp(aim,now<shootUntil?1:0,18,dt);boost=Damp(boost,c&&c.Boosting?1:0,10,dt);defense=Damp(defense,c&&c.Bank.DefenseTime>0?1:0,8,dt);overdrive=Damp(overdrive,c&&c.Bank.RedlineTime>0?1:0,8,dt);
            leftKick=Damp(leftKick,0,20,dt);rightKick=Damp(rightKick,0,20,dt);laserKick=BastionChoreography.Recoil(now-laserFiredAt);missileKick=Damp(missileKick,0,12,dt);
            leftSkyfallKick=Damp(leftSkyfallKick,0,14,dt);rightSkyfallKick=Damp(rightSkyfallKick,0,14,dt);
            input.LeftSkyfallKick=leftSkyfallKick;input.RightSkyfallKick=rightSkyfallKick;
            input.Acceleration=acceleration;
            input.CannonHeat=BastionChoreography.CannonHeat(now-laserFiredAt);
            for(int i=0;i<4;i++)rackKicks[i]=BastionChoreography.LaunchRecoil(now-rackFiredAt[i]);input.RackKicks=rackKicks;
            input.PowerAge=now-powerStarted;input.Time=now;input.Forward=forward;input.Strafe=strafe;input.Vertical=vertical;input.Brake=brake;input.Turn=turn;input.Aim=aim;input.Boost=boost;input.Defense=defense;input.Overdrive=overdrive;input.Unfold=unfold;
            if(!utility&&Owner)utility=Owner.GetComponent<BastionUtility>();
            if(utility&&utility.VisualKind==BastionUtilityKind.Wingburst)
            {wingburstPoseAge=utility.VisualAge;input.WingburstWindup=utility.CastWindup;input.WingburstDuration=utility.CastSpan;}
            else if(wingburstPoseAge>=0){wingburstPoseAge+=dt;if(wingburstPoseAge>input.WingburstWindup+input.WingburstDuration+.15f)wingburstPoseAge=-1;}
            if(utility&&utility.Kind==BastionUtilityKind.Mines)
            {minePoseAge=utility.Age;input.MineSpan=utility.Duration;}
            else if(minePoseAge>=0){minePoseAge+=dt;if(minePoseAge>input.MineSpan+.3f)minePoseAge=-1;}
            input.WingburstAge=wingburstPoseAge;input.MineAge=minePoseAge;
            if(utility)
            {
                var dash=transform.InverseTransformDirection(utility.Direction);input.WingburstSide=dash.x;input.WingburstForward=dash.z;input.WingburstVertical=dash.y;
            }
            float targetPitch=Owner&&Owner.inputBank?-Mathf.Asin(Mathf.Clamp(Owner.inputBank.aimDirection.y,-1,1))*Mathf.Rad2Deg:0;
            pitch=Damp(pitch,targetPitch,20,dt);input.Pitch=pitch;
            input.LeftKick=leftKick;input.RightKick=rightKick;input.LaserKick=laserKick;input.MissileKick=missileKick;input.Salvo=Mathf.Clamp01((salvoUntil-now)*3);
            haloPhase=Mathf.Repeat(haloPhase+dt*(4+defense*6+input.Salvo*4),360);input.HaloPhase=haloPhase;
            for(int i=0;i<72;i++)hatchAges[i]=now-hatchTimes[i];input.HatchAges=hatchAges;
            if(Mobile.gameObject.activeSelf){BastionChoreography.Mobile(input,mobileFrame);float settle=1-Mathf.Exp(-(input.WingburstAge>=input.WingburstWindup+input.WingburstDuration?28:12)*dt);Mobile.localPosition=Vector3.Lerp(Mobile.localPosition,U(mobileFrame.Offset),settle);Mobile.localRotation=Quaternion.Slerp(Mobile.localRotation,Quaternion.Euler(0,Showcase&&!Owner?BastionDisplayRules.Yaw:0,0)*Quaternion.Euler(U(mobileFrame.Rotation)),settle);Mobile.localScale=Vector3.one*.78f*mobileFrame.Scale*(Showcase&&!Owner?BastionDisplayRules.Scale(unfold):1);Apply(MobileBones,mr,mp,mobileFrame,dt);}
            if(Siege.gameObject.activeSelf){BastionChoreography.Siege(input,siegeFrame);Siege.localPosition=U(siegeFrame.Offset);Siege.localRotation=Quaternion.Euler(0,Showcase&&!Owner?BastionDisplayRules.Yaw:0,0);Siege.localScale=Vector3.one*1.45f*siegeFrame.Scale*(Showcase&&!Owner?BastionDisplayRules.FortressScale(unfold):1);Apply(SiegeBones,sr,sp,siegeFrame,dt);}
            float power=1+boost*.18f+defense*.12f+overdrive*.18f+laserKick*.25f+BastionChoreography.PowerCharge(input.PowerAge)*.65f+BastionChoreography.PowerRelease(input.PowerAge)*.80f;
            foreach(var light in lights)if(light)light.intensity=1.1f*power;
            foreach(var renderer in emissive)if(renderer){renderer.GetPropertyBlock(properties);properties.SetFloat("_EmPower",(renderer.name.EndsWith("_Core")?2.1f:1.35f)*power);renderer.SetPropertyBlock(properties);}
        }
    }
}
