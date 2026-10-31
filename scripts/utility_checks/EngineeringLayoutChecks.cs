// Execute the production Engineering partial with a recording UI. Geometry and
// strings are real; this is not a Unity/TMP rendering or input-device test.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using IvoryBastion;
namespace UnityEngine
{
    public struct Vector2{public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
    public struct Color{public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color white=>new(1,1,1);}
    public sealed class RectTransform:Transform
    {public GameObject gameObject=new();public Vector2 anchoredPosition,sizeDelta;public RectTransform Parent;public string Kind,Name,Asset;public Color Tint;public TMPro.TMP_Text Text;public UI.Button Button;}
}
namespace TMPro
{
    public sealed class TMP_Text
    {public UnityEngine.RectTransform rectTransform;public string text;public float fontSize,fontSizeMin,fontSizeMax;public bool enableWordWrapping;public UnityEngine.Color color;}
}
namespace UnityEngine.UI
{
    public class Image{public string sprite;public Color color;}
    public sealed class Button{public Transform transform;public Image targetGraphic=new();public bool interactable;public Action Click;}
}
namespace IvoryBastion
{
    using UnityEngine;using UnityEngine.UI;using TMPro;
    internal static class BastionContent{internal static string Icon(string name)=>name;}
    public sealed partial class BastionHud
    {
        private BastionController c;private RectTransform panel;
        internal readonly List<RectTransform> Records=new();
        private static readonly Color Quiet=new(.56f,.68f,.79f),Gold=new(1,.78f,.29f),Ready=new(.55f,.88f,1);
        private static readonly string[] UnitNames={"BULWARK","GUNNER","INTERCEPTOR","REPAIRER"},UnitAssets={"guardian","gunner","interceptor","repair"};
        private RectTransform Rect(string name,Transform parent,float x,float y,float width,float height)
        {var r=new RectTransform{Parent=parent as RectTransform,Name=name,anchoredPosition=new(x,y),sizeDelta=new(width,height),Kind="rect"};Records.Add(r);return r;}
        private TMP_Text Label(Transform parent,string text,float x,float y,float width,float height,float size,Color color,bool wrap=false)
        {var r=Rect("Label",parent,x,y,width,height);r.Kind="text";return r.Text=new TMP_Text{rectTransform=r,text=text,fontSize=size,fontSizeMin=size-1,fontSizeMax=size,color=color,enableWordWrapping=wrap};}
        private Image Image(Transform parent,float x,float y,float width,float height,Color color,string asset=null)
        {var r=Rect("Image",parent,x,y,width,height);r.Kind="image";r.Tint=color;r.Asset=asset;return new(){color=color,sprite=asset};}
        private Button Button(Transform parent,string text,float x,float y,float width,float height,Action click,out TMP_Text label)
        {var r=Rect("Button",parent,x,y,width,height);r.Kind="button";r.Button=new(){Click=click,transform=r};label=Label(r,text,0,0,width-14,height,14,Color.white);return r.Button;}
        private void Purchase(int branch,int choice,bool loadout){}
        private void SetOpen(bool value){}
        internal void Prepare(BastionController controller)
        {c=controller;panel=Rect("Engineering",null,0,0,1040,680);BuildEngineering();}
        internal void Page(int page){SelectUpgradePage(page);UpdateEngineering();}
    }
}
internal static class EngineeringLayoutChecks
{
    private static int checks;
    private static void Check(bool pass,string why){checks++;if(!pass)throw new Exception("Engineering layout: "+why);}
    private static (float X,float Y,bool Active) Position(UnityEngine.RectTransform r)
    {float x=0,y=0;bool active=true;for(var p=r;p!=null;p=p.Parent){x+=p.anchoredPosition.x;y+=p.anchoredPosition.y;active&=p.gameObject.activeSelf;}return(x,y,active);}
    internal static void Run(string root)
    {
        var go=new UnityEngine.GameObject();var body=go.AddComponent<RoR2.CharacterBody>();var controller=go.AddComponent<BastionController>();controller.Body=body;controller.Sieged=true;controller.Bank.Upgrades.Active=true;controller.Bank.Fabrication=1000;
        var menu=new BastionHud();menu.Prepare(controller);var pages=new List<object>();
        foreach(bool full in new[]{false,true})
        {
            foreach(var d in BastionUpgradeTree.Nodes)controller.Bank.Upgrades.Points[(int)d.Id]=(byte)(full?d.Maximum:0);
            for(int page=0;page<3;page++)
            {
                menu.Page(page);var visible=menu.Records.Where(r=>Position(r).Active).ToArray();
                var cards=visible.Where(r=>r.Name.StartsWith("Upgrade ")).ToArray();Check(cards.Length==(page==0?8:4),"page shows exactly its own nodes");
                foreach(var card in cards)
                {
                    var p=Position(card);Check(p.X-card.sizeDelta.x/2>=-510&&p.X+card.sizeDelta.x/2<=510&&p.Y+card.sizeDelta.y/2<195&&p.Y-card.sizeDelta.y/2>=-294,"node stays inside content, clear of tabs and close row");
                    foreach(var other in cards.Where(r=>r!=card)){var q=Position(other);Check(Math.Abs(p.X-q.X)>=(card.sizeDelta.x+other.sizeDelta.x)/2||Math.Abs(p.Y-q.Y)>=(card.sizeDelta.y+other.sizeDelta.y)/2,"cards do not overlap");}
                    foreach(var text in visible.Where(r=>r.Text!=null&&r.Parent==card))Check(text.sizeDelta.y>=20&&text.sizeDelta.x>=200,"node text has usable width/height");
                }
                Check(visible.Count(r=>r.Kind=="button")==(page==0?15:8),"tabs, node purchases and close/loadout controls present");
                pages.Add(new{Page=page,Full=full,Elements=visible.Select(r=>{var p=Position(r);var tint=r.Text?.color??r.Tint;return new{r.Kind,r.Name,r.Asset,X=p.X,Y=p.Y,Width=r.sizeDelta.x,Height=r.sizeDelta.y,Text=r.Text?.text,Size=r.Text?.fontSize??0,MinSize=r.Text?.fontSizeMin??0,Wrap=r.Text?.enableWordWrapping??false,Color=new[]{tint.r,tint.g,tint.b,tint.a},Enabled=r.Button?.interactable??true};}).ToArray()});
            }
        }
        Directory.CreateDirectory(Path.Combine(root,".build"));File.WriteAllText(Path.Combine(root,".build","engineering-layout.json"),JsonSerializer.Serialize(pages,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS: {checks} production Engineering page geometry/content checks; exported .build/engineering-layout.json (UI stand-ins).");
    }
}
