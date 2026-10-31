using System.Collections.Generic;
using System.Linq;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IvoryBastion
{
    internal static class BastionShieldStatus
    {
        internal static void Install(){if(!Application.isBatchMode)On.RoR2.UI.HealthBar.UpdateHealthbar+=Updated;}
        internal static void Remove()
        {
            On.RoR2.UI.HealthBar.UpdateHealthbar-=Updated;
            foreach(var view in BastionShieldStatusView.Instances.ToArray())if(view)Object.Destroy(view);
        }
        private static void Updated(On.RoR2.UI.HealthBar.orig_UpdateHealthbar orig,HealthBar bar,float delta)
        {
            orig(bar,delta);
            var source=bar.source;var c=source&&source.body?source.body.GetComponent<BastionController>():null;
            var hud=bar.GetComponentInParent<HUD>();
            var view=bar.GetComponent<BastionShieldStatusView>();
            if(c&&bar.barContainer&&hud&&hud.healthBar==bar&&hud.mainContainer)
            {if(!view)view=bar.gameObject.AddComponent<BastionShieldStatusView>();view.Bind(bar,c,hud);view.Refresh();}
            else if(view)view.Hide();
        }
    }
    // These host-owned reserves live outside native health-bar fills and masks.
    public sealed class BastionShieldStatusView:MonoBehaviour
    {
        internal static readonly List<BastionShieldStatusView> Instances=new List<BastionShieldStatusView>();
        private HealthBar bar;private HUD hud;private BastionController owner;
        private RectTransform root,parent,frame;private BastionShieldStatusGraphic shield,aegis;
        private TMP_Text shieldText,aegisText;private readonly Vector3[] corners=new Vector3[4];
        private readonly List<HudPlacement.Box> obstacles=new List<HudPlacement.Box>(3);
        private VerticalLayoutGroup reservedLayout;private RectTransform reservedRect;private Vector2 reservedOffset;
        private void OnEnable(){Instances.Add(this);Canvas.willRenderCanvases+=Refresh;}
        private void OnDisable(){Instances.Remove(this);Canvas.willRenderCanvases-=Refresh;Hide();}
        internal void Bind(HealthBar target,BastionController controller,HUD targetHud)
        {
            if(bar==target&&owner==controller&&hud==targetHud&&root&&frame==target.barContainer&&parent==frame.parent)return;
            Hide();if(root)Destroy(root.gameObject);
            bar=target;owner=controller;hud=targetHud;frame=bar.barContainer;parent=frame.parent as RectTransform;
            if(!parent)return;
            var obj=new GameObject("Seraph independent defense gauges",typeof(RectTransform));obj.layer=parent.gameObject.layer;
            root=(RectTransform)obj.transform;root.SetParent(parent,false);root.anchorMin=root.anchorMax=root.pivot=Vector2.zero;
            root.sizeDelta=new Vector2(400,BastionDefenseGaugeLayout.Height);
            obj.AddComponent<LayoutElement>().ignoreLayout=true;
            shield=Track("Seraph Shield - white and blue",0,9,false);
            aegis=Track("Aegis Barrier - yellow and black",11,5,true);
            var font=hud.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.font)?.font??TMP_Settings.defaultFontAsset;
            shieldText=Label("Seraph charge",0,.6f,font,new Color(.88f,.95f,1));
            aegisText=Label("Aegis charge",.6f,1,font,new Color(1,.83f,.27f));aegisText.alignment=TextAlignmentOptions.Right;
        }
        private BastionShieldStatusGraphic Track(string name,float y,float height,bool barrier)
        {
            var obj=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer));obj.layer=root.gameObject.layer;
            var rect=(RectTransform)obj.transform;rect.SetParent(root,false);rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.right;
            rect.pivot=new Vector2(.5f,0);rect.anchoredPosition=new Vector2(0,y);rect.sizeDelta=new Vector2(0,height);
            var background=obj.AddComponent<BastionSmoothImage>();background.color=barrier?new Color(.17f,.13f,.055f,.95f):new Color(.08f,.17f,.27f,.96f);background.raycastTarget=false;
            var fill=new GameObject("Independent reserve fill",typeof(RectTransform),typeof(CanvasRenderer));fill.layer=obj.layer;
            var fillRect=(RectTransform)fill.transform;fillRect.SetParent(rect,false);fillRect.anchorMin=Vector2.zero;fillRect.anchorMax=Vector2.one;
            fillRect.offsetMin=Vector2.one*.75f;fillRect.offsetMax=-Vector2.one*.75f;
            var graphic=fill.AddComponent<BastionShieldStatusGraphic>();graphic.raycastTarget=false;graphic.Aegis=barrier;return graphic;
        }
        private TMP_Text Label(string name,float left,float right,TMP_FontAsset font,Color tint)
        {
            var obj=new GameObject(name,typeof(RectTransform));obj.layer=root.gameObject.layer;
            var rect=(RectTransform)obj.transform;rect.SetParent(root,false);rect.anchorMin=new Vector2(left,0);rect.anchorMax=new Vector2(right,0);
            rect.pivot=new Vector2(.5f,0);rect.anchoredPosition=new Vector2(0,18);rect.sizeDelta=new Vector2(0,12);
            var text=obj.AddComponent<TextMeshProUGUI>();text.font=font;text.fontSize=10;text.color=tint;text.enableWordWrapping=false;
            text.alignment=TextAlignmentOptions.Left;text.raycastTarget=false;text.overflowMode=TextOverflowModes.Ellipsis;return text;
        }
        private HudPlacement.Box Bounds(RectTransform rect)
        {
            rect.GetWorldCorners(corners);var p=frame.InverseTransformPoint(corners[0]);
            var box=new HudPlacement.Box(p.x,p.y,p.x,p.y);
            for(int i=1;i<4;i++){p=frame.InverseTransformPoint(corners[i]);box.Left=Mathf.Min(box.Left,p.x);box.Right=Mathf.Max(box.Right,p.x);box.Bottom=Mathf.Min(box.Bottom,p.y);box.Top=Mathf.Max(box.Top,p.y);}
            return box;
        }
        private void ReserveSlot()
        {
            if(reservedLayout)return;
            // The native two-row layout owns level/XP above health. Enlarging
            // both its height and spacing equally preserves each native row's
            // size and bottom edge, while moving level/XP and buffs up together.
            var container=parent.parent as RectTransform;
            var group=container?container.GetComponent<VerticalLayoutGroup>():null;
            if(!group||!group.isActiveAndEnabled||group.reverseArrangement||container.childCount!=2||
               parent.GetSiblingIndex()!=1||!hud.expBar||!hud.expBar.transform.IsChildOf(container))return;
            reservedLayout=group;reservedRect=container;
            reservedOffset=new Vector2(0,BastionDefenseGaugeLayout.ReservedSpace*container.pivot.y);
            group.spacing+=BastionDefenseGaugeLayout.ReservedSpace;
            container.sizeDelta+=new Vector2(0,BastionDefenseGaugeLayout.ReservedSpace);
            container.anchoredPosition+=reservedOffset;
            LayoutRebuilder.ForceRebuildLayoutImmediate(container);
        }
        private void ReleaseSlot()
        {
            if(reservedLayout)reservedLayout.spacing-=BastionDefenseGaugeLayout.ReservedSpace;
            if(reservedRect)
            {
                reservedRect.sizeDelta-=new Vector2(0,BastionDefenseGaugeLayout.ReservedSpace);
                reservedRect.anchoredPosition-=reservedOffset;
                LayoutRebuilder.MarkLayoutForRebuild(reservedRect);
            }
            reservedLayout=null;reservedRect=null;reservedOffset=Vector2.zero;
        }
        private void Avoid(RectTransform rect)
        {if(rect&&rect.gameObject.activeInHierarchy)obstacles.Add(Bounds(rect));}
        internal void Refresh()
        {
            if(!bar||!hud||!owner||!bar.source||bar.source.body!=owner.Body||!root||!parent||!frame){Hide();return;}
            bool active=owner.Alive&&(owner.AbilityShield.Capacity>0||owner.AegisBarrier.Capacity>0);
            if(!active){Hide();return;}if(!root.gameObject.activeSelf)root.gameObject.SetActive(true);
            ReserveSlot();obstacles.Clear();
            Avoid(hud.expBar?hud.expBar.GetComponent<RectTransform>():null);
            Avoid(hud.levelText?hud.levelText.GetComponent<RectTransform>():null);
            Avoid(hud.buffDisplay?hud.buffDisplay.GetComponent<RectTransform>():null);
            var health=frame.rect;
            var layout=BastionDefenseGaugeLayout.AboveHealth(new HudPlacement.Box(health.xMin,health.yMin,health.xMax,health.yMax),obstacles);
            root.localRotation=frame.localRotation;root.localScale=frame.localScale;
            root.localPosition=parent.InverseTransformPoint(frame.TransformPoint(new Vector3(layout.Left,layout.Bottom,0)));
            root.sizeDelta=new Vector2(layout.Right-layout.Left,BastionDefenseGaugeLayout.Height);
            shield.Fraction=owner.AbilityShield.Capacity>0?owner.AbilityShield.Charge/owner.AbilityShield.Capacity:0;
            aegis.Fraction=owner.AegisBarrier.Capacity>0?owner.AegisBarrier.Charge/owner.AegisBarrier.Capacity:0;
            string state=shield.Restoring?"  RESTORING":owner.AbilityShield.Charge<=0?"  OFFLINE":owner.Sieged&&owner.Bank.DefenseTime>0?"  FORTIFIED":"";
            shieldText.text=$"SERAPH  {Mathf.CeilToInt(owner.AbilityShield.Charge)} / {Mathf.CeilToInt(owner.AbilityShield.Capacity)}"+state;
            aegisText.text=owner.AegisBarrier.Charge>0?$"AEGIS  {Mathf.CeilToInt(owner.AegisBarrier.Charge)}"+(owner.Sieged&&owner.Bank.DefenseTime>0?$"  {owner.Bank.DefenseTime:0.0}s":""):"AEGIS  —";
            aegisText.alpha=owner.AegisBarrier.Charge>0?1:.45f;
        }
        internal void Hide(){if(root)root.gameObject.SetActive(false);ReleaseSlot();}
        private void LateUpdate()=>Refresh();
        private void OnDestroy(){ReleaseSlot();if(root)Destroy(root.gameObject);}
    }
    public sealed class BastionShieldStatusGraphic:MaskableGraphic
    {
        private readonly BastionShieldGaugeGeometry geometry=new BastionShieldGaugeGeometry();
        private readonly BastionShieldGaugeGeometry trailGeometry=new BastionShieldGaugeGeometry();
        private readonly BastionReadoutHistory history=new BastionReadoutHistory();
        private float fraction,trail,feather=1;internal bool Aegis;
        internal bool Restoring=>history.Rising&&fraction>0&&fraction<.999f;
        internal float Fraction
        {set{history.Update(value,Time.unscaledTime);float nextTrail=BastionPresentationSettings.DefenseDamageTrail?.Value==false?history.Fraction:history.Trail;if(Mathf.Abs(history.Fraction-fraction)<.00001f&&Mathf.Abs(nextTrail-trail)<.00001f)return;fraction=history.Fraction;trail=nextTrail;SetVerticesDirty();}}
        private void LateUpdate()
        {float next=BastionUiAntialias.Feather(rectTransform,canvas);if(Mathf.Abs(next-feather)>.025f){feather=next;SetVerticesDirty();}}
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();var r=GetPixelAdjustedRect();feather=BastionUiAntialias.Feather(rectTransform,canvas);geometry.Update(r.width,r.height,fraction,Aegis,feather);
            if(trail>fraction+.0001f)
            {
                trailGeometry.Update(r.width,r.height,trail,Aegis,feather);
                for(int i=0;i<trailGeometry.Vertices.Count;i++)
                {var p=trailGeometry.Vertices[i];Color c=trailGeometry.Colors[i];c.a*=.27f;helper.AddVert(new Vector3(r.xMin+p.x,r.yMin+p.y),c,Vector2.zero);}
                for(int i=0;i<trailGeometry.Triangles.Count;i+=3)helper.AddTriangle(trailGeometry.Triangles[i],trailGeometry.Triangles[i+1],trailGeometry.Triangles[i+2]);
            }
            int offset=helper.currentVertCount;
            for(int i=0;i<geometry.Vertices.Count;i++)
            {var p=geometry.Vertices[i];helper.AddVert(new Vector3(r.xMin+p.x,r.yMin+p.y),geometry.Colors[i],Vector2.zero);}
            for(int i=0;i<geometry.Triangles.Count;i+=3)helper.AddTriangle(offset+geometry.Triangles[i],offset+geometry.Triangles[i+1],offset+geometry.Triangles[i+2]);
        }
    }
}
