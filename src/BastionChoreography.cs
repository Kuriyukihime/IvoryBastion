using System;
using System.Collections.Generic;
using System.Numerics;
namespace IvoryBastion
{
    internal sealed class BastionPoseInput
    {
        public float Time,Forward,Strafe,Vertical,Brake,Turn,Pitch,Aim,Boost,Unfold=1;
        public float HaloPhase,PowerAge=-1;
        public float LeftKick,RightKick,LaserKick,MissileKick,Defense,Overdrive,Salvo;
        public float LeftSkyfallKick,RightSkyfallKick;
        public float Acceleration,CannonHeat;
        public float WingburstAge=-1,WingburstWindup=.12f,WingburstDuration=.6f,WingburstSide,WingburstForward=1,WingburstVertical;
        public float MineAge=-1,MineSpan=.87f;
        public float[] RackKicks;
        public float[] HatchAges;
    }
    internal sealed class BastionJointPose { public Vector3 Rotation,Offset; }
    internal sealed class BastionPoseFrame
    {
        public Vector3 Rotation,Offset;public float Scale=1;
        public readonly Dictionary<string,BastionJointPose> Bones=new Dictionary<string,BastionJointPose>();
        public void Set(string name,Vector3 rotation,Vector3 offset=default)
        {if(!Bones.TryGetValue(name,out var p)){p=new BastionJointPose();Bones[name]=p;}p.Rotation=rotation;p.Offset=offset;}
    }
    // Shared by the Unity driver and the offline pose renderer. All movement
    // here is visual: no hitboxes, attack cadence, damage or locomotion change.
    internal static class BastionChoreography
    {
        private sealed class SideNames
        {
            internal readonly string Arm,Fore,Hand,Leg,Boot,Wing,Vane,Rack,Shield,Skyfall;
            internal SideNames(string side)
            {Arm="arm_"+side;Fore="fore_"+side;Hand="hand_"+side;Leg="leg_"+side;Boot="boot_"+side;Wing="wing_"+side;Vane="vane_"+side;Rack="rack_"+side;Shield="shield_"+side;Skyfall="skyfall_"+side;}
        }
        private static readonly SideNames[] sides={new SideNames("l"),new SideNames("r")};
        private static readonly string[] anchors={"anchor_0","anchor_1","anchor_2","anchor_3","anchor_4","anchor_5"},vents={"vent_0","vent_1","vent_2","vent_3"};
        private static readonly string[,,] hatches=HatchNames();
        private static string[,,] HatchNames()
        {var names=new string[4,6,3];for(int r=0;r<4;r++)for(int row=0;row<6;row++)for(int c=0;c<3;c++)names[r,row,c]="hatch_"+r+"_"+row+"_"+c;return names;}
        private static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
        private static float Clamp(float x,float a=0,float b=1)=>Math.Max(a,Math.Min(b,x));
        private static float Sin(float x)=>(float)Math.Sin(x);
        internal static float Stage(float t,float a,float b){float s=Clamp((t-a)/(b-a));return s*s*(3-2*s);}
        internal static float Hatch(float age)
        {if(age<0||age>=.72f)return 0;return Stage(age,0,.065f)*(1-Stage(age,.24f,.72f));}
        internal static float PowerCharge(float age)=>Stage(age,0,.22f)*(1-Stage(age,.24f,.50f));
        internal static float PowerRelease(float age)=>Stage(age,.24f,.42f)*(1-Stage(age,.64f,1.25f));
        internal static float Recoil(float age)
        {if(age<0||age>.65f)return 0;return Stage(age,0,.035f)*(1-Stage(age,.065f,.48f));}
        internal static float CannonHeat(float age)
        {if(age<0||age>1.9f)return 0;return Stage(age,0,.11f)*(1-Stage(age,.36f,1.9f));}
        internal static float LaunchRecoil(float age)
        {if(age<0||age>.42f)return 0;return Stage(age,0,.024f)*(1-Stage(age,.06f,.42f));}
        internal static void Mobile(BastionPoseInput p,BastionPoseFrame f)
        {
            float forward=Clamp(p.Forward,-1,1),side=Clamp(p.Strafe,-1,1),rise=Clamp(p.Vertical,-1,1),boost=Clamp(p.Boost),brake=Clamp(p.Brake),aim=Clamp(p.Aim);
            float turn=Clamp(p.Turn,-1,1),unfold=Clamp(p.Unfold),idle=Sin(p.Time*1.65f);
            float travel=Clamp((float)Math.Sqrt(forward*forward+side*side)),climb=Math.Max(0,rise),dive=Math.Max(0,-rise);
            float charge=PowerCharge(p.PowerAge),release=PowerRelease(p.PowerAge),free=1-aim,acceleration=Clamp(p.Acceleration,-1,1);
            // The pelvis leads a turn while the chest and gaze counter-bank.
            // This leaves the wing/leg silhouette expressive without making
            // her gun arm swing away from the camera's intended aim.
            float roll=Clamp(-side*(23+boost*6)-turn*13,-36,36)*(1-unfold*.55f)*(1-aim*.12f);
            float pitch=Clamp(forward*(25+boost*9)-brake*25-climb*7+dive*7+acceleration*6-charge*7-release*9,-29,40)*(1-unfold*.55f)*(1-aim*.30f);
            float weaponPitch=Clamp(p.Pitch,-65,65),yaw=-side*5-turn*4;
            float torsoPitch=-pitch*.24f+weaponPitch*.14f*aim+charge*11-release*9-acceleration*7,torsoRoll=-roll*.32f+idle*.45f*(1-travel);
            float bodyPitch=pitch+torsoPitch,bodyRoll=roll+torsoRoll;
            f.Scale=1-.12f*unfold;f.Rotation=V(pitch,0,roll);
            // Rotate about the pelvis, rather than swinging the entire model
            // around its feet. The avatar stays centered on the real body.
            float radians=(float)Math.PI/180,px=pitch*radians,rz=roll*radians,pivot=.93f*f.Scale;
            f.Offset=V(-side*.025f+Sin(rz)*pivot,.075f+idle*.013f*(1-travel*.65f)-unfold*.07f+release*.025f+pivot*(1-(float)Math.Cos(px)*(float)Math.Cos(rz)),-Sin(px)*(float)Math.Cos(rz)*pivot);
            f.Set("root",V(0,yaw,0));
            f.Set("torso",V(torsoPitch,-yaw*.65f,torsoRoll));
            f.Set("head",V(Clamp(p.Pitch,-65,65)*.64f-bodyPitch*.83f-release*3,yaw*-.35f,-bodyRoll*.65f));
            for(int i=0;i<2;i++)
            {
                var names=sides[i];float s=i==0?-1:1,kick=i==0?p.LeftKick:p.RightKick;
                float cycle=Sin(p.Time*1.65f+s*.75f),inside=Clamp(.5f+s*(.12f+side*.45f+turn*.20f));
                float spread=brake*22+Math.Abs(side)*7,drift=cycle*(1-travel)*1.7f;
                // Blue Shift gathers the forearms into a deliberate guarded
                // pose, then opens the chest and fan. Aiming retains priority.
                f.Set(names.Arm,V(-6-aim*35+weaponPitch*.55f*aim-bodyPitch*.60f*aim-kick*5+free*(forward*7+boost*9+acceleration*5-charge*26-release*26),s*(-5+aim*4+free*charge*9)-yaw*.12f,s*(-4-brake*17-aim*16+free*(release*46-charge*18))-bodyRoll*.24f*aim),V(0,0,-kick*.012f));
                f.Set(names.Fore,V(-8-aim*16+weaponPitch*.40f*aim-bodyPitch*.40f*aim-kick*8-free*(charge*44+release*12+brake*16),-s*free*charge*17,-s*aim*4));
                f.Set(names.Hand,V(-aim*7+kick*6-release*9*free,0,s*(3+free*charge*7)));
                // A tucked inside knee and trailing outside leg make banking,
                // boost and braking readable from the usual rear camera.
                // Calves flex in the same anatomical direction throughout;
                // they no longer cancel the thigh rotation into rigid sticks.
                float tuck=inside*(7+travel*13+boost*9)+climb*(9+inside*8);
                float hip=-6-tuck-forward*5-brake*(25+inside*7)+dive*10+drift-unfold*18-acceleration*7-charge*(3+inside*7)+release*(2-inside*5);
                float knee=10+tuck*(1.15f+boost*.22f)+boost*travel*10+brake*(22-inside*3)-dive*6-drift+unfold*12+acceleration*9+charge*(5+inside*10);
                f.Set(names.Leg,V(Clamp(hip,-49,12),-side*6+s*(2+travel*2),s*(4+Math.Abs(side)*5+brake*4)+side*3));
                f.Set(names.Boot,V(Clamp(knee,5,62),s*2,s*(1+brake*2)));
                f.Set(names.Wing,V(forward*7-rise*10+brake*10+acceleration*11,s*(11+boost*17+p.Overdrive*5+spread+unfold*25-charge*17+release*19),s*(-6+side*s*11+idle*.5f-unfold*16-release*31+charge*13)-turn*4));
                f.Set(names.Vane,V(-forward*9+rise*12-brake*17-acceleration*14,s*(-7+boost*19+brake*25+unfold*26-charge*20+release*23),s*(4+side*s*8+Sin(p.Time*1.65f+.65f)*.65f+boost*6+release*30-charge*7)-turn*6));
            }
            Wingburst(p,f);
        }
        private static Vector3 PivotCompensation(Vector3 rotation,float pivot)
        {float x=rotation.X*(float)Math.PI/180,z=rotation.Z*(float)Math.PI/180;return V(Sin(z)*pivot,pivot*(1-(float)Math.Cos(x)*(float)Math.Cos(z)),-Sin(x)*(float)Math.Cos(z)*pivot);}
        private static void Blend(BastionPoseFrame f,string bone,Vector3 rotation,float weight)
        {if(f.Bones.TryGetValue(bone,out var previous))rotation=Vector3.Lerp(previous.Rotation,rotation,Clamp(weight));f.Set(bone,rotation);}
        internal static void Wingburst(BastionPoseInput p,BastionPoseFrame f)
        {
            if(p.WingburstAge<0)return;
            float w=Math.Max(.03f,p.WingburstWindup),d=Math.Max(.15f,p.WingburstDuration),t=p.WingburstAge;
            float gather=Stage(t,0,w)*(1-Stage(t,w,w+.09f));
            float run=Stage(t,w-.025f,w+.09f)*(1-Stage(t,w+d-.12f,w+d+.10f));
            float brake=Stage(t,w+d-.14f,w+d+.015f)*(1-Stage(t,w+d+.015f,w+d+.14f));
            float power=Math.Max(gather,Math.Max(run,brake));if(power<=0)return;
            float x=Clamp(p.WingburstSide,-1,1),z=Clamp(p.WingburstForward,-1,1),y=Clamp(p.WingburstVertical,-1,1),aim=Clamp(p.Aim);
            var before=f.Rotation;
            float lean=run*(z*28-y*10)-gather*8-brake*16;
            f.Rotation+=V(lean,0,-x*run*29+x*brake*9);
            f.Offset+=PivotCompensation(f.Rotation,.93f*f.Scale)-PivotCompensation(before,.93f*f.Scale)+V(0,-gather*.055f+brake*.035f,0);
            Blend(f,"torso",V(-gather*12-run*9+brake*12,-x*run*11,x*run*8),power);
            if(f.Bones.TryGetValue("head",out var head))head.Rotation+=V(-lean*.86f+gather*3,x*run*6,0);
            for(int i=0;i<2;i++)
            {
                float side=i==0?-1:1,alternate=i==0?1:.68f;var n=sides[i];
                Blend(f,n.Arm,V(-gather*38-run*24+brake*16,side*(gather*16-run*9),side*(-gather*24+run*15+brake*22)),power*(1-aim));
                if(aim>0&&f.Bones.TryGetValue(n.Arm,out var arm))arm.Rotation.X-=lean*.85f*aim;
                Blend(f,n.Fore,V(-gather*59-run*34,0,-side*run*9),power*(1-aim));
                Blend(f,n.Hand,V(-gather*15+run*7,0,side*5),power*(1-aim));
                Blend(f,n.Leg,V(-gather*(16+alternate*13)-run*(22+alternate*17)-brake*22,side*3,side*(5+run*4)),power);
                Blend(f,n.Boot,V(14+gather*27+run*(30+alternate*14)+brake*25,0,side*3),power);
                Blend(f,n.Wing,V(gather*15-run*16,side*(12-gather*28+run*37),side*(-6-gather*20-run*20+brake*20)),power);
                Blend(f,n.Vane,V(-gather*20+run*22,side*(-7-gather*24+run*29),side*(4+gather*13-run*20+brake*13)),power);
            }
        }
        internal static void MineDeployment(BastionPoseInput p,BastionPoseFrame f)
        {
            if(p.MineAge<0)return;float active=Stage(p.MineAge,0,.12f)*(1-Stage(p.MineAge,p.MineSpan,p.MineSpan+.25f));
            foreach(var n in sides){if(f.Bones.TryGetValue(n.Arm,out var arm))arm.Rotation.X-=active*7;if(f.Bones.TryGetValue(n.Hand,out var hand))hand.Rotation.X-=active*9;}
        }
        internal static void Siege(BastionPoseInput p,BastionPoseFrame f)
        {
            float u=Clamp(p.Unfold),baseOpen=Stage(u,.025f,.38f),cannon=Stage(u,.24f,.78f),seat=Stage(u,.10f,.56f),salvo=Clamp(p.Salvo),defense=Clamp(p.Defense);
            // Only the root-weighted deck needs a brief compact emergence. All
            // later expansion comes from real hinges/lifts, not whole-rig growth.
            float emerge=Stage(u,.015f,.18f),settle=Stage(u,.18f,.26f);
            float lockIn=Stage(u,.46f,.64f)*(1-Stage(u,.70f,.96f));
            f.Scale=.24f+.72f*emerge+.04f*settle;f.Rotation=Vector3.Zero;f.Offset=V(0,-.04f*(1-baseOpen)-lockIn*.022f,0);
            f.Set("root",Vector3.Zero);
            // The queen seat rises on its own lift before the gun locks forward.
            // Pilot and armrests share the same lift travel; recoil stays in the gun.
            float seatLift=-.65f*(1-seat);
            f.Set("throne",Vector3.Zero,V(0,seatLift,0));
            f.Set("torso",V(-p.LaserKick*3+defense*2-salvo,0,Sin(p.Time*1.2f)*.12f),V(0,seatLift,-p.LaserKick*.012f));
            f.Set("head",V(Clamp(p.Pitch,-40,50)*.35f+p.LaserKick*2-defense*3,Sin(p.Time*.31f)*1.2f,0));
            // The barrel begins upright inside the central package. Its breech
            // pivots clear of the seat before the carriage extends forward.
            float gunHinge=Stage(u,.15f,.53f),haloLift=Stage(u,.13f,.60f);
            f.Set("cannon",V(-102*(1-gunHinge)+Clamp(p.Pitch,-40,50)*gunHinge,0,0),V(0,-.38f*(1-gunHinge)-lockIn*.012f,-1.05f*(1-cannon)-p.LaserKick*.24f));
            f.Set("halo",V(-82*(1-haloLift),0,p.HaloPhase),V(0,-.86f*(1-haloLift),.30f*(1-haloLift)));
            f.Set("radar",V(0,Sin(p.Time*.45f)*24,0));
            for(int i=0;i<2;i++)
            {
                var names=sides[i];float s=i==0?-1:1;
                float front=Stage(u,.065f+i*.018f,.54f+i*.018f),rear=Stage(u,.20f+i*.018f,.81f+i*.018f);
                float frontKick=p.RackKicks!=null&&p.RackKicks.Length>i?Clamp(p.RackKicks[i]):p.MissileKick;
                float rearKick=p.RackKicks!=null&&p.RackKicks.Length>i+2?Clamp(p.RackKicks[i+2]):0;
                f.Set(names.Rack,V(-84*(1-front)+Clamp(p.Pitch*.16f,-10,10)-frontKick*2.8f,s*(1-front)*57,s*(defense*2.5f+frontKick*.8f)),V(-s*.78f*(1-front),-.34f*(1-front),-.57f*(1-front)-frontKick*.065f));
                f.Set(names.Shield,V(-81*(1-rear)-rearKick*2.8f,s*(1-rear)*66,s*(defense*3.5f+rearKick*.8f)),V(-s*.80f*(1-rear),-.34f*(1-rear),.56f*(1-rear)-rearKick*.065f));
                f.Set(names.Arm,V(-salvo*10+defense*7-p.LaserKick*3,0,s*(-salvo*6+defense*4)));
                f.Set(names.Fore,V(salvo*11-defense*9,0,s*defense*3));
                f.Set(names.Hand,V(-defense*4,0,s*(Sin(p.Time*1.2f+i)*.6f+salvo*5)));
                float kick=i==0?p.LeftSkyfallKick:p.RightSkyfallKick;
                // Compact cradles track outward on short rails. The fixed
                // forks stay above the folded supports rather than diving.
                float cradle=Stage(u,.28f,.76f);
                f.Set(names.Skyfall,V(-12*(1-cradle),0,0),V(-s*.18f*(1-cradle),.05f*(1-cradle)-kick*.035f,.10f*(1-cradle)));
            }
            for(int i=0;i<6;i++)
            {
                float a=(i+.5f)*(float)Math.PI/3,open=Stage(u,.015f+i*.012f,.35f+i*.012f),closed=1-open;
                // Negative rotation lifts each radial foot up into its storage
                // pocket, then swings it down/out to meet the ground.
                f.Set(anchors[i],V(-closed*72*(float)Math.Cos(a),0,closed*72*Sin(a)),V(-Sin(a)*.12f*closed,.07f*closed,-(float)Math.Cos(a)*.12f*closed));
            }
            for(int i=0;i<4;i++)
            {
                float a=(i+.5f)*(float)Math.PI/2,heat=Clamp(Math.Max(p.CannonHeat,p.LaserKick*1.4f)+salvo*.35f);
                // These hinges sit behind the plates: a positive local Z arm
                // needs outward yaw / negative pitch to open, not fold inward.
                f.Set(vents[i],V(-heat*Sin(a)*23,heat*(float)Math.Cos(a)*23,0),V(heat*(float)Math.Cos(a)*.072f,heat*Sin(a)*.072f,0));
            }
            for(int rack=0;rack<4;rack++)for(int row=0;row<6;row++)for(int col=0;col<3;col++)
            {
                int index=rack+row*4+col*24;float opening=p.HatchAges!=null&&index<p.HatchAges.Length?Hatch(p.HatchAges[index]):0;
                f.Set(hatches[rack,row,col],V(0,(rack%2==0?-1:1)*opening*86,0));
            }
            MineDeployment(p,f);
        }
        internal static string Parent(string name)
        {
            if(name=="root")return null;
            if(name.StartsWith("mine_knee_"))return name.Replace("mine_knee_","mine_hip_");
            if(name.StartsWith("skyfall_muzzle_"))return "skyfall_"+name.Substring(name.Length-1);
            if(name=="cannon_muzzle")return "cannon";
            if(name.StartsWith("muzzle_"))return name.Replace("muzzle_","fore_");
            if(name.StartsWith("tip_"))return name.Substring(4);
            if(name.StartsWith("hatch_")){int i=name[6]-'0';return new[]{"rack_l","rack_r","shield_l","shield_r"}[i];}
            if(name.StartsWith("vent_"))return "cannon";
            if(name=="head"||name.StartsWith("arm_")||name.StartsWith("wing_")||name.StartsWith("vane_"))return "torso";
            if(name.StartsWith("fore_"))return name.Replace("fore_","arm_");
            if(name.StartsWith("hand_"))return name.Replace("hand_","fore_");
            if(name.StartsWith("boot_"))return name.Replace("boot_","leg_");
            return "root";
        }
    }
}
