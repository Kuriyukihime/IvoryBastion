using System.Collections.Generic;
using B=IvoryBastion.BastionBalance;
using S=IvoryBastion.BastionSetting;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
namespace IvoryBastion
{
    public sealed class BastionSpiderVisual:MonoBehaviour
    {
        private static readonly Dictionary<string,BastionSpiderVisual> live=new Dictionary<string,BastionSpiderVisual>();
        private static readonly Dictionary<string,float> ended=new Dictionary<string,float>();
        private string key;private BastionMinePhase phase;private float reportedAge=-1,sincePacket,life,legOpen,clamp,missingTargetAge,nextResolve,latchAge,leapAge,nextSurface;
        private Vector3 velocity,localAnchor;
        private CharacterBody attachedTarget,leapTarget;private NetworkInstanceId targetId;
        private BastionMineTargetMark mark;private Transform[] bones;private Renderer[] renderers;private int[] legs;private bool[] blue;
        private readonly BastionMineSurfaceAttachment surface=new BastionMineSurfaceAttachment();
        private Vector3 contactOffset;private LineRenderer contactRim;
        private readonly MaterialPropertyBlock glow=new MaterialPropertyBlock();
        internal bool Attached=>phase==BastionMinePhase.Attached||phase==BastionMinePhase.Primed;
        internal bool Overcharged=>phase==BastionMinePhase.Primed;
        internal CharacterBody AttachedTarget=>attachedTarget;
        internal static void Receive(BastionEffect message,CharacterBody owner,CharacterBody target)
        {
            if(Application.isBatchMode)return;string key=message.Owner.Value+":"+message.Id;
            if(ended.TryGetValue(key,out float until)&&Time.time<until)return;
            int kind=Mathf.FloorToInt(message.Value);float age=(message.Value-kind)*100;
            if(kind<0||kind>(int)BastionMinePhase.Armed)return;
            if(!live.TryGetValue(key,out var mine)||!mine)
            {
                var go=new GameObject("Ivory articulated spider mine");go.transform.position=message.A;
                mine=go.AddComponent<BastionSpiderVisual>();mine.key=key;live[key]=mine;
                BastionModel.Rig(go,"spider_mine",1,new List<CharacterModel.RendererInfo>(),out mine.bones);
                mine.contactRim=BastionUtilityFx.Line(go.transform,25,.013f,BastionModel.Blue);
                mine.renderers=go.GetComponentsInChildren<Renderer>();
                // Every Unity name read allocates; resolve leg and glow roles once, not per frame.
                mine.legs=System.Array.ConvertAll(mine.bones,Leg);
                mine.blue=System.Array.ConvertAll(mine.renderers,r=>r.sharedMaterial&&r.sharedMaterial.name.Contains("MineBlue"));
            }
            if(age+.003f<mine.reportedAge)return;
            bool wasAttached=mine.Attached;var oldPhase=mine.phase;
            mine.reportedAge=age;mine.sincePacket=0;mine.phase=(BastionMinePhase)kind;
            if(!wasAttached||!mine.Attached)mine.transform.position=message.A;
            if(oldPhase!=mine.phase&&mine.phase==BastionMinePhase.Leaping)mine.leapAge=0;
            if(mine.Attached)
            {
                // Attached/Primed B is the host's body-local anchor, not a world
                // velocity. This survives target turns, teleportation and scale.
                if(!wasAttached||mine.targetId.Value!=message.Target.Value||(mine.localAnchor-message.B).sqrMagnitude>.0001f)
                {mine.surface.Clear();mine.nextSurface=0;mine.latchAge=0;mine.contactOffset=Vector3.zero;}
                mine.targetId=message.Target;mine.localAnchor=message.B;
                mine.BindAttachment(target,message.A);mine.Show(mine.FollowAttachment(0)||mine.Overcharged);
            }
            else
            {
                mine.ClearAttachment();mine.Show(true);
                mine.leapTarget=mine.phase==BastionMinePhase.Leaping?target:null;
                mine.velocity=mine.phase==BastionMinePhase.Airborne||mine.phase==BastionMinePhase.Crawling||mine.phase==BastionMinePhase.Leaping?message.B:Vector3.zero;
            }
        }
        internal static bool End(uint owner,uint id)
        {
            string key=owner+":"+id;bool first=!ended.TryGetValue(key,out float until)||Time.time>=until;ended[key]=Time.time+4;
            if(live.TryGetValue(key,out var mine)&&mine)Destroy(mine.gameObject);live.Remove(key);
            if(ended.Count>256){var remove=new List<string>();foreach(var pair in ended)if(pair.Value<Time.time)remove.Add(pair.Key);foreach(var item in remove)ended.Remove(item);}
            return first;
        }
        private void BindAttachment(CharacterBody target,Vector3 anchor)
        {
            if(attachedTarget!=target)
            {
                if(mark)mark.Remove(this);mark=null;attachedTarget=target;
                surface.Clear();nextSurface=0;
                if(target)
                {
                    missingTargetAge=0;mark=BastionMineTargetMark.Add(target,this);
                }
            }
            if(target&&!mark)mark=BastionMineTargetMark.Add(target,this);
        }
        private void ClearAttachment()
        {if(mark)mark.Remove(this);mark=null;surface.Clear();contactOffset=Vector3.zero;attachedTarget=null;leapTarget=null;targetId=NetworkInstanceId.Invalid;missingTargetAge=0;}
        private void Show(bool visible){foreach(var renderer in renderers)if(renderer)renderer.enabled=visible;}
        private bool FollowAttachment(float dt)
        {
            if(!attachedTarget)
            {
                nextResolve-=dt;
                if(nextResolve<=0)
                {
                    nextResolve=.1f;var obj=Util.FindNetworkObject(targetId);
                    if(obj)BindAttachment(obj.GetComponent<CharacterBody>(),transform.position);
                }
                if(!attachedTarget)
                {missingTargetAge+=dt;if(missingTargetAge>1.2f)Destroy(gameObject);return false;}
            }
            if(attachedTarget.healthComponent&&!attachedTarget.healthComponent.alive)
            {
                // A priming weapon may kill its carrier. Keep the last valid
                // world impact anchor throughout the short overcharge flash.
                if(!Overcharged)Destroy(gameObject);return false;
            }
            missingTargetAge=0;var seed=attachedTarget.transform.TransformPoint(localAnchor);
            if(!surface.Bound)
            {
                nextSurface-=dt;
                if(nextSurface<=0)
                {
                    nextSurface=.75f;
                    if(surface.Bind(attachedTarget,seed)&&surface.Follow(out var contact,out _))
                    {contactOffset=seed-contact;latchAge=0;}
                }
            }
            Vector3 normal;
            if(surface.Follow(out var point,out normal))transform.position=point+contactOffset*(1-BastionMinePresentation.Contact(latchAge));
            else{transform.position=seed;normal=seed-attachedTarget.corePosition;}
            if(normal.sqrMagnitude<.001f)normal=attachedTarget.transform.up;
            var rotation=Quaternion.FromToRotation(Vector3.up,normal.normalized);
            transform.rotation=dt<=0?rotation:Quaternion.Slerp(transform.rotation,rotation,1-Mathf.Exp(-20*dt));
            return true;
        }
        private void Update()
        {
            float dt=Time.deltaTime;life+=dt;sincePacket+=dt;if(Attached)latchAge+=dt;if(phase==BastionMinePhase.Leaping)leapAge+=dt;
            if(sincePacket>1.2f||life>B.Get(S.MineLifetime)+B.Get(S.MineAirTime)+3){Destroy(gameObject);return;}
            if(Attached)Show(FollowAttachment(dt)||Overcharged);
            else
            {
                if(phase==BastionMinePhase.Airborne)
                {
                    var previous=transform.position;transform.position+=velocity*dt;
                    velocity+=Vector3.down*(8*BastionMineRules.ArcHeight/(B.Get(S.MineAirTime)*B.Get(S.MineAirTime))*dt);
                    if(Physics.Linecast(previous,transform.position,out var hit,LayerIndex.world.mask,QueryTriggerInteraction.Ignore)&&hit.normal.y>.35f){transform.position=hit.point;velocity=Vector3.zero;}
                }
                if(phase==BastionMinePhase.Crawling&&sincePacket<.25f)
                {var next=transform.position+velocity*dt;if(BastionServerSpiderMine.Ground(next+Vector3.up*.4f,out var ground,4))transform.position=ground;}
                if(phase==BastionMinePhase.Leaping&&sincePacket<.25f)transform.position+=velocity*dt;
                if(velocity.sqrMagnitude>.05f)
                {var forward=Vector3.ProjectOnPlane(velocity,Vector3.up);if(forward.sqrMagnitude>.01f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(forward),1-Mathf.Exp(-18*dt));}
                if(phase==BastionMinePhase.Leaping&&leapTarget)
                {
                    var outward=transform.position-leapTarget.corePosition;
                    if(outward.sqrMagnitude>.001f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.FromToRotation(Vector3.up,outward.normalized),BastionMinePresentation.Contact(leapAge)*.6f);
                }
            }
            legOpen=Mathf.MoveTowards(legOpen,phase==BastionMinePhase.Airborne?0:1,dt*BastionMinePresentation.UnfoldSpeed);
            clamp=Mathf.MoveTowards(clamp,Attached?BastionMinePresentation.Clamp(latchAge):0,dt*10);
            float size=Attached&&attachedTarget?Mathf.Clamp(attachedTarget.radius*.8f,.55f,1):1;
            float flash=Attached?BastionMinePresentation.LatchFlash(latchAge):0;
            transform.localScale=new Vector3(size*(1+flash*.07f),size*(1-flash*.12f),size*(1+flash*.07f));
            for(int b=0;b<bones.Length;b++)
            {
                var bone=bones[b];int leg=legs[b];
                if(leg>=0&&leg<10)
                {
                    int i=leg;float gait=phase==BastionMinePhase.Crawling?Mathf.Sin(life*25+i*Mathf.PI)*17:Overcharged?Mathf.Sin(life*80+i*Mathf.PI)*4:0;
                    float a=(i+.5f)*Mathf.PI/4;var axis=new Vector3(-Mathf.Sin(a),0,Mathf.Cos(a));
                    bone.localRotation=Quaternion.AngleAxis(BastionMinePresentation.Hip(legOpen)+clamp*18,axis)*Quaternion.Euler(0,gait,0);
                }
                if(leg>=10)
                {
                    int i=leg-10;float a=(i+.5f)*Mathf.PI/4;
                    float lift=phase==BastionMinePhase.Crawling?Mathf.Max(0,Mathf.Sin(life*25+i*Mathf.PI))*16:0;
                    bone.localRotation=Quaternion.AngleAxis(BastionMinePresentation.Knee(legOpen)+lift-clamp*35,new Vector3(-Mathf.Sin(a),0,Mathf.Cos(a)));
                }
            }
            float pulse=Overcharged?6+Mathf.Sin(life*80)*2:Attached?3.4f+Mathf.Sin(life*5)*.45f+flash*4:1.4f+Mathf.Sin(life*2)*.2f;
            for(int r=0;r<renderers.Length;r++)
            {
                var renderer=renderers[r];
                if(renderer==contactRim||!renderer.sharedMaterial)continue;
                float power=blue[r]?pulse:Attached?.45f+flash*1.2f:0;
                glow.SetFloat("_EmPower",power);glow.SetColor("_EmColor",Overcharged?Color.white:BastionModel.Blue);
                glow.SetColor("_EmissionColor",(Overcharged?Color.white:BastionModel.Blue)*power);renderer.SetPropertyBlock(glow);
            }
            contactRim.enabled=Attached&&attachedTarget&&missingTargetAge==0;
            if(contactRim.enabled)
            {
                float radius=.31f+flash*.1f;
                for(int i=0;i<25;i++){float a=i*Mathf.PI*2/24;contactRim.SetPosition(i,transform.TransformPoint(new Vector3(Mathf.Cos(a)*radius,.12f,Mathf.Sin(a)*radius)));}
                BastionUtilityFx.Color(contactRim,Overcharged?Color.white:BastionModel.Blue,.5f+flash*.4f);
            }
        }
        // Hip digit 0-9, knee 10-19, any other bone -1.
        private static int Leg(Transform bone)
        {string name=bone.name;return name.StartsWith("mine_hip_")?name[name.Length-1]-'0':name.StartsWith("mine_knee_")?10+name[name.Length-1]-'0':-1;}
        private void OnDestroy(){ClearAttachment();if(key!=null&&live.TryGetValue(key,out var current)&&current==this)live.Remove(key);}
    }
    public sealed class BastionMineLaunchers:MonoBehaviour
    {
        // Public so Unity copies them when RoR2 instantiates the body/display prefab.
        public Transform[] Bones;public BastionAnimation Model;private BastionUtility utility;private float open;
        private void LateUpdate()
        {
            if(Model&&Model.Owner&&!utility)utility=Model.Owner.GetComponent<BastionUtility>();
            bool launch=utility&&utility.Kind==BastionUtilityKind.Mines;
            open=Mathf.MoveTowards(open,launch?1:0,Time.deltaTime*(launch?9:4));
            for(int i=0;i<Bones.Length;i++)if(Bones[i].name.StartsWith("mine_hatch_")){int side=Bones[i].name.EndsWith("0")?-1:1;Bones[i].localRotation=Quaternion.Euler(0,0,BastionMinePresentation.HatchAngle(side,open));}
        }
    }
}
