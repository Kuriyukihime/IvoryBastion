using B = IvoryBastion.BastionBalance;
using S = IvoryBastion.BastionSetting;
using System.Linq;
using EntityStates;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace IvoryBastion
{
    public sealed partial class BastionHud:MonoBehaviour
    {
        private HUD hud;private BastionController c;private RectTransform root,factory,panel,parent;private TMP_FontAsset font;private bool open;private MPEventSystem cursor;
        private TMP_Text title,transformKey,ammo,feed,system,currency,key,factoryStatus,factoryRate,header,notice;private Image portrait,fabFill;private BastionChrome chrome;
        private BastionResourceBar ammoFill,secondaryFill,factoryFill;
        private readonly Vector3[] corners=new Vector3[4];private float nextLayout,shownAmmo,shownFuel,shownFab;
        private static readonly string[] UnitNames={"BULWARK","GUNNER","INTERCEPTOR","REPAIRER"},UnitAssets={"guardian","gunner","interceptor","repair"};
        private static readonly Color Quiet=new Color(.56f,.68f,.79f),Gold=new Color(1,.78f,.29f),Ready=new Color(.55f,.88f,1);
        internal static void Attach(HUD target){if(target&&!Application.isBatchMode&&!target.GetComponent<BastionHud>())target.gameObject.AddComponent<BastionHud>();}
        private void Awake(){hud=GetComponent<HUD>();}
        private RectTransform Rect(string name,Transform p,float x,float y,float w,float h)
        {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(p,false);r.gameObject.layer=p.gameObject.layer;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);return r;}
        private TMP_Text Label(Transform p,string text,float x,float y,float w,float h,float size,Color color,bool wrap=false)
        {var t=Rect(text.Length>0?text:"Readout",p,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=text;t.fontSize=size;t.color=color;t.alignment=TextAlignmentOptions.MidlineLeft;t.raycastTarget=false;t.enableWordWrapping=wrap;t.enableAutoSizing=true;t.fontSizeMin=size-1;t.fontSizeMax=size;t.overflowMode=TextOverflowModes.Ellipsis;return t;}
        private Image Image(Transform p,float x,float y,float w,float h,Color color,string asset=null)
        {var i=Rect("Artwork",p,x,y,w,h).gameObject.AddComponent<BastionSmoothImage>();i.color=color;i.raycastTarget=false;if(asset!=null){i.sprite=BastionContent.Icon(asset);i.preserveAspect=true;}return i;}
        private Image Bar(Transform p,float x,float y,float w,Color tint)
        {
            Image(p,x,y,w+2,7,new Color(.27f,.38f,.48f,.65f));
            var track=Image(p,x,y,w,5,new Color(.055f,.10f,.16f));
            var bar=Image(track.transform,-w*.5f,0,w,5,tint);bar.rectTransform.pivot=new Vector2(0,.5f);
            for(int i=1;i<8;i++)Image(track.transform,-w*.5f+w*i/8,0,1,5,new Color(.015f,.04f,.07f,.28f));
            return bar;
        }
        private BastionResourceBar ResourceBar(Transform p,float x,float y,float w,float h)
        {
            var track=Image(p,x,y,w,h,new Color(.055f,.095f,.13f,.96f));
            var fill=Rect("Resource meter",track.transform,0,0,w-1.5f,h-1.5f).gameObject.AddComponent<BastionResourceBar>();
            fill.raycastTarget=false;return fill;
        }
        private void Frame(Transform p,float x,float y,float w,float h,Color color)
        {
            const float arm=11;
            foreach(int side in new[]{-1,1})foreach(int top in new[]{-1,1})
            {
                Image(p,x+side*(w*.5f-arm*.5f),y+top*h*.5f,arm,1,color);
                Image(p,x+side*w*.5f,y+top*(h*.5f-arm*.5f),1,arm,color);
            }
        }
        private Button Button(Transform p,string text,float x,float y,float w,float h,UnityEngine.Events.UnityAction click,out TMP_Text label)
        {var i=Image(p,x,y,w,h,new Color(.07f,.16f,.25f,1));i.raycastTarget=true;var b=i.gameObject.AddComponent<Button>();b.targetGraphic=i;b.onClick.AddListener(click);var colors=b.colors;colors.highlightedColor=new Color(.57f,.80f,1);colors.pressedColor=new Color(.26f,.63f,.92f);colors.disabledColor=new Color(.35f,.40f,.46f,.8f);b.colors=colors;label=Label(i.transform,text,0,0,w-14,h,14,BastionModel.White);label.alignment=TextAlignmentOptions.Center;return b;}
        private void Build()
        {
            font=hud.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.font)?.font??TMP_Settings.defaultFontAsset;
            root=Rect("Seraph combat module",CombatFrame(),0,0,HudPlacement.CombatWidth,HudPlacement.CombatHeight);root.gameObject.AddComponent<LayoutElement>().ignoreLayout=true;
            chrome=root.gameObject.AddComponent<BastionChrome>();chrome.raycastTarget=false;chrome.Angled=false;
            Frame(root,-124,6,62,66,new Color(.68f,.84f,.98f,.55f));portrait=Image(root,-124,6,60,64,Color.white,"hud_mobile");
            title=Label(root,"",-80,51,144,18,12.5f,Color.white);
            transformKey=Label(root,"",77,51,154,18,10.5f,Ready);transformKey.alignment=TextAlignmentOptions.MidlineRight;
            ammo=Label(root,"",36,26,232,29,25,Color.white);
            ammoFill=ResourceBar(root,36,6,232,8);feed=Label(root,"",36,-10,232,16,10.5f,Quiet);secondaryFill=ResourceBar(root,36,-26,232,5);
            Image(root,0,-37,304,1,new Color(.32f,.52f,.7f,.35f));system=Label(root,"",0,-49,304,18,10.5f,Ready);

            // Keep production beside combat, clear of the native ally/drone
            // list below currencies. Both modules share the skills' HUD plane.
            factory=Rect("Seraph fabrication module",root,0,0,HudPlacement.FactoryWidth,HudPlacement.FactoryHeight);
            factory.gameObject.AddComponent<LayoutElement>().ignoreLayout=true;
            var factoryChrome=factory.gameObject.AddComponent<BastionChrome>();factoryChrome.raycastTarget=false;factoryChrome.Angled=false;
            Image(factory,-85,29,21,21,Color.white,"factory");Label(factory,"FABRICATION",21,29,160,18,11,Color.white);
            currency=Label(factory,"",-35,6,124,27,24,Color.white);factoryRate=Label(factory,"",67,6,62,24,10,Quiet);factoryRate.alignment=TextAlignmentOptions.Right;
            factoryStatus=Label(factory,"",0,-12,196,16,10,Quiet);factoryFill=ResourceBar(factory,0,-24,196,5);
            key=Label(factory,"",0,-36,196,16,10.5f,Ready);
            panel=Rect("Seraph engineering",parent,0,0,1040,680);var panelChrome=panel.gameObject.AddComponent<BastionChrome>();panelChrome.Angled=false;panelChrome.raycastTarget=false;
            Image(panel,-464,292,47,47,Color.white,"factory");header=Label(panel,"",30,301,925,29,27,Color.white);
            notice=Label(panel,"",30,272,925,23,14,Quiet);fabFill=Bar(panel,0,247,984,BastionResourcePalette.Tint(BastionResourceKind.Fabrication));
            BuildEngineering();
            panel.gameObject.SetActive(false);
        }
        private void Purchase(int branch,int choice,bool loadout)
        {
            if(!c||!c.Body.hasEffectiveAuthority||!c.Sieged||c.Deploying)return;
            if(BastionEngineeringPause.HandlePurchase(this,c,branch,choice,loadout))return;
            c.Requests?.Submit(loadout?BastionRequestKind.Loadout:BastionRequestKind.Purchase,branch:branch,choice:choice);
        }
        private void SetOpen(bool value)
        {
            if(open==value)return;open=value;if(panel)panel.gameObject.SetActive(value);
            if(value){cursor=MPEventSystem.readOnlyInstancesList.FirstOrDefault(e=>e.localUser!=null&&e.localUser.cachedBody==c.Body);if(cursor)cursor.cursorOpenerCount++;}
            else if(cursor){cursor.cursorOpenerCount=Mathf.Max(0,cursor.cursorOpenerCount-1);cursor=null;}
            BastionEngineeringPause.SetOpen(this,value);
        }
        private static void Fill(Image image,float width,float value){image.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,width*Mathf.Clamp01(value));}
        private void Update()
        {
            c=hud&&hud.targetBodyObject?hud.targetBodyObject.GetComponent<BastionController>():null;
            if(!c||!c.Alive||!Run.instance){if(root)root.gameObject.SetActive(false);if(factory)factory.gameObject.SetActive(false);SetOpen(false);return;}
            if(!root){parent=hud.mainContainer?hud.mainContainer.GetComponent<RectTransform>():null;if(!parent)return;Build();}root.gameObject.SetActive(true);if(factory)factory.gameObject.SetActive(true);
            if(c.Body.hasEffectiveAuthority&&BastionPlugin.EngineeringKey.Value.IsDown())SetOpen(!open);if(open&&Input.GetKeyDown(KeyCode.Escape))SetOpen(false);
            var b=c.Bank;float blend=1-Mathf.Exp(-14*Time.unscaledDeltaTime);shownAmmo=Mathf.Lerp(shownAmmo,b.Ammo/b.Capacity,blend);shownFuel=Mathf.Lerp(shownFuel,b.Thruster.Fraction,blend);
            bool aegis=c.Sieged&&b.DefenseTime>0;chrome.SetAccent(aegis?Gold:BastionModel.Blue);portrait.sprite=BastionContent.Icon(c.Sieged?"hud_siege":"hud_mobile");
            title.text=c.Deploying?"RECONFIGURING":c.Sieged?"SIEGE SYSTEMS":"FLIGHT SYSTEMS";
            string transformBinding=BastionPlugin.TransformationKey.Value.MainKey==KeyCode.None?"UNBOUND":BastionPlugin.TransformationKey.Value.ToString();
            transformKey.text=$"[{transformBinding}] {(c.Sieged?"PACK":"DEPLOY")}"+(b.Toggle>0?$" {b.Toggle:0.0}s":"");
            transformKey.color=b.Toggle>0||c.Deploying?Quiet:Ready;
            ammo.text=c.Sieged?$"{Mathf.FloorToInt(b.Ammo):00}<size=14> / {b.Capacity}</size>":$"{Mathf.RoundToInt(b.Thruster.Fraction*100):00}<size=14>%</size>";
            int heavyEvery=b.HeavyEvery(false),heavyProgress=b.MissileSequence%heavyEvery;
            ammoFill.Set(c.Sieged?shownAmmo:shownFuel,c.Sieged?BastionResourceKind.Missiles:BastionResourceKind.Fuel,c.Sieged?b.Capacity:1);
            secondaryFill.Set(c.Sieged?heavyProgress/(float)heavyEvery:shownAmmo,c.Sieged?BastionResourceKind.Heavy:BastionResourceKind.Missiles,c.Sieged?heavyEvery:b.Capacity);
            ammo.color=BastionResourcePalette.Tint(c.Sieged?BastionResourceKind.Missiles:BastionResourceKind.Fuel);
            feed.text=c.Sieged?$"RELOAD {b.AmmoRate(true):0.#}/s  /  HEAVY IN {heavyEvery-heavyProgress}":b.Thruster.Exhausted?"RELEASE SECONDARY TO REARM":$"MISSILE STOCKPILE {Mathf.FloorToInt(b.Ammo)}/{b.Capacity}";
            var lance=c.GetComponent<BastionLaserTracker>();
            system.text=c.Sieged?(b.Laser<=0?(lance&&lance.Target?"LANCE LOCKED":"NO LANCE TARGET"):$"LANCE RECHARGE {b.Laser:0.0}s")+(aegis?$"  /  AEGIS {b.DefenseTime:0.0}s":""):b.RedlineTime>0?$"BLUE SHIFT {b.RedlineTime:0.0}s":c.Boosting?"BOOST ACTIVE":b.Thruster.Exhausted?"THRUSTERS EXHAUSTED":"FLIGHT SYSTEMS READY";
            system.color=aegis?Gold:Ready;
            var utility=c.GetComponent<BastionUtility>();
            if(utility&&utility.Active)
                system.text=utility.Kind==BastionUtilityKind.Wingburst?"HEAVENFALL RUSH / MISSILE SWARM":"WIDOW ARRAY / DEPLOYING";
            if(c.Sieged&&utility&&!utility.Active&&utility.ActiveMines>0)system.text+=$"  /  MINES {utility.ActiveMines}/{BastionMineRules.MaximumMines}";
            if(utility&&Time.unscaledTime<utility.ChainFeedbackUntil)
            {system.text=$"CHARGED BURST ×{utility.ChainFeedbackCount}  /  +{BastionUtilityUpgrades.MineChargeBonus(c.Bank):0.#}%";system.color=Color.white;}
            int nextCost=NextUpgradeCost(b);
            bool complete=nextCost==int.MaxValue,paused=c.Sieged&&(!BastionController.RunTimerAdvancing||BastionEngineeringPause.Paused);
            shownFab=Mathf.Lerp(shownFab,complete||nextCost<=0?1:Mathf.Clamp01(b.Fabrication/nextCost),blend);factoryFill.Set(shownFab,BastionResourceKind.Fabrication);
            currency.text=$"{Mathf.FloorToInt(b.Fabrication)}<size=10> FAB</size>";
            // LastCombat belongs to the host; display the synchronized rate
            // range instead of guessing its combat state on a remote client.
            factoryRate.text=!c.Sieged?"STANDBY":paused?"PAUSED":$"{BastionBank.IdleFabricationPerMinute}–{BastionBank.CombatFabricationPerMinute}/min";
            factoryStatus.text=complete?"ALL SYSTEMS UPGRADED":b.Fabrication>=nextCost?"UPGRADE AVAILABLE":$"NEXT UPGRADE  {nextCost} FAB";
            factoryStatus.color=!complete&&b.Fabrication>=nextCost?Ready:Quiet;key.text=$"[{BastionPlugin.EngineeringKey.Value}] ENGINEERING";
            if(Time.unscaledTime<c.NoticeUntil){system.text=c.Notice;system.color=Gold;}
            if(Time.unscaledTime>=nextLayout){nextLayout=Time.unscaledTime+.25f;Position();}if(!open)return;
            header.text=$"ENGINEERING   <size=19><color=#91D766>{Mathf.FloorToInt(b.Fabrication)} FABRICATION</color></size>";
            notice.text=BastionEngineeringPause.Paused?(c.Sieged?"SOLO PAUSED / Choose your upgrades. Close Engineering to resume.":"SOLO PAUSED / Close Engineering and deploy to purchase upgrades."):!c.Sieged?$"FLIGHT / Deploy within {B.Get(S.DeployHeight):0.#}m of solid ground to purchase.":!BastionController.RunTimerAdvancing?"RUN TIMER PAUSED / Fabrication income paused.":"Point prices rise with rank. Follow linked prerequisites; Blueprints needs one Hangar point.";
            Fill(fabFill,984,nextCost==int.MaxValue||nextCost<=0?1:b.Fabrication/nextCost);
            UpdateEngineering();
        }
        private static void CommonFrame(Transform target,ref Transform frame)
        {if(!target)return;if(!frame)frame=target.parent;while(frame&&!target.IsChildOf(frame))frame=frame.parent;}
        private RectTransform CombatFrame()
        {
            Transform frame=null;
            if(hud.skillIcons!=null)foreach(var icon in hud.skillIcons)if(icon)CommonFrame(icon.transform,ref frame);
            if(hud.equipmentIcons!=null)foreach(var icon in hud.equipmentIcons)if(icon)CommonFrame(icon.transform,ref frame);
            return frame as RectTransform??parent;
        }
        private HudPlacement.Box Bounds(RectTransform rect,RectTransform space)
        {
            rect.GetWorldCorners(corners);var p=space.InverseTransformPoint(corners[0]);var bounds=new HudPlacement.Box(p.x,p.y,p.x,p.y);
            for(int i=1;i<4;i++){p=space.InverseTransformPoint(corners[i]);bounds.Left=Mathf.Min(bounds.Left,p.x);bounds.Right=Mathf.Max(bounds.Right,p.x);bounds.Bottom=Mathf.Min(bounds.Bottom,p.y);bounds.Top=Mathf.Max(bounds.Top,p.y);}
            return bounds;
        }
        private void Include(RectTransform rect,RectTransform space,ref bool found,ref HudPlacement.Box bounds,ref float angle)
        {if(!rect||!rect.gameObject.activeInHierarchy)return;var b=Bounds(rect,space);if(!found){bounds=b;angle=Mathf.Clamp(Mathf.DeltaAngle(space.eulerAngles.z,rect.eulerAngles.z),-12,12);found=true;}else{bounds.Left=Mathf.Min(bounds.Left,b.Left);bounds.Right=Mathf.Max(bounds.Right,b.Right);bounds.Bottom=Mathf.Min(bounds.Bottom,b.Bottom);bounds.Top=Mathf.Max(bounds.Top,b.Top);}}
        private void AlignFactory()
        {
            if(!factory||!root)return;
            float width=HudPlacement.FactoryWidth*HudPlacement.FactoryScale,height=HudPlacement.FactoryHeight*HudPlacement.FactoryScale;
            factory.localScale=Vector3.one*HudPlacement.FactoryScale;
            factory.anchoredPosition=new Vector2(-root.rect.width*.5f-HudPlacement.ModuleGap-width*.5f,-root.rect.height*.5f+height*.5f);
        }
        private void Position()
        {
            var frame=CombatFrame();if(root.parent!=frame)root.SetParent(frame,false);
            bool found=false;var bounds=new HudPlacement.Box();float angle=0;
            if(hud.skillIcons!=null)foreach(var icon in hud.skillIcons)if(icon)Include(icon.transform as RectTransform,frame,ref found,ref bounds,ref angle);
            if(hud.equipmentIcons!=null)foreach(var icon in hud.equipmentIcons)if(icon)Include(icon.transform as RectTransform,frame,ref found,ref bounds,ref angle);
            var r=parent.rect;panel.localScale=Vector3.one*Mathf.Min(1,Mathf.Min((r.width-32)/1040,(r.height-32)/680));
            // Inherit the skills' actual plane and scale. Flattening its world
            // bounds into mainContainer discarded native yaw/depth and drifted.
            var viewport=Bounds(parent,frame);float scale=(found?Mathf.Max(.01f,(bounds.Right-bounds.Left)/HudPlacement.CombatWidth):1)*(BastionPresentationSettings.HudScale?.Value??1);
            float width=HudPlacement.CombatWidth*scale,height=HudPlacement.CombatHeight*scale;
            float x=viewport.Right-width*.5f-24,y=viewport.Bottom+235;
            if(found)HudPlacement.AboveSkills(viewport,bounds,width,height,angle,out x,out y);
            root.localScale=Vector3.one*scale;root.localRotation=Quaternion.Euler(0,0,angle);root.localPosition=new Vector3(x,y,0);
            AlignFactory();
        }
        private void OnDisable(){SetOpen(false);if(root)root.gameObject.SetActive(false);if(factory)factory.gameObject.SetActive(false);}
        private void OnDestroy(){SetOpen(false);if(root)Destroy(root.gameObject);if(factory)Destroy(factory.gameObject);if(panel)Destroy(panel.gameObject);}
    }
}
