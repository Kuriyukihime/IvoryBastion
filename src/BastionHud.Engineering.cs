using System;
using RoR2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace IvoryBastion
{
    public sealed partial class BastionHud
    {
        private readonly RectTransform[] upgradePages=new RectTransform[3];
        private readonly TMP_Text[] upgradeNames=new TMP_Text[16],upgradeEffects=new TMP_Text[16],upgradeBonuses=new TMP_Text[16],upgradeBuyLabels=new TMP_Text[16],unitLabels=new TMP_Text[3];
        private readonly Image[] upgradeEdges=new Image[16],unitArt=new Image[3];
        private readonly Button[] upgradeBuys=new Button[16],upgradeTabs=new Button[3],unitButtons=new Button[3];
        private int upgradePage;
        private static int NextUpgradeCost(BastionBank bank)
        {
            int result=int.MaxValue;
            for(int i=0;i<BastionUpgradeTree.Count;i++)if(BastionUpgradeTree.Available(bank.Upgrades,i)&&bank.Upgrades.Points[i]<BastionUpgradeTree.Nodes[i].Maximum)result=Math.Min(result,BastionUpgradeTree.Cost(bank.Upgrades,i));
            return result;
        }
        private void BuildEngineering()
        {
            string[] names={"SIEGE","DEFENSE","MOBILE"};
            string[] descriptions={"Specialize your missiles, Ion Lance, Widow Array and drone complement.","Shared defenses protect both forms. Aegis effects apply while deployed.","Flight, thrusters, Heavenfall Rush and Blue Shift. Defense is shared."};
            for(int group=0;group<3;group++)
            {
                int selected=group;upgradeTabs[group]=Button(panel,names[group],(group-1)*335,215,320,36,()=>SelectUpgradePage(selected),out _);
                upgradePages[group]=Rect(names[group]+" upgrade paths",panel,0,-25,1020,410);
                Label(upgradePages[group],descriptions[group],0,205,984,24,12,Quiet);
            }
            string[] lanes={"MISSILES","ION LANCE","WIDOW ARRAY","DRONES"};
            for(int lane=0;lane<4;lane++)
            {
                float x=-375+lane*250;Label(upgradePages[0],lanes[lane],x,178,232,21,13,Ready);
                EngineeringCard(lane*2,upgradePages[0],x,85,232,165);
                EngineeringLink(upgradePages[0],x,2,x,-15,lane*2+1);
                EngineeringCard(lane*2+1,upgradePages[0],x,-100,232,165);
            }
            Label(upgradePages[0],"DRONE LOADOUT / click an active slot to cycle unlocked roles",0,-202,984,20,11,Quiet);
            for(int i=0;i<3;i++)
            {
                int slot=i;float x=(i-1)*330;
                unitButtons[i]=Button(upgradePages[0],"",x,-239,310,48,()=>CycleUnit(slot),out unitLabels[i]);
                unitLabels[i].rectTransform.anchoredPosition=new Vector2(26,0);unitLabels[i].rectTransform.sizeDelta=new Vector2(234,46);
                unitArt[i]=Image(unitButtons[i].transform,-120,0,58,43,Color.white,"hud_unit_guardian");
            }
            for(int lane=0;lane<2;lane++)
            {
                float x=lane==0?-250:250;Label(upgradePages[1],lane==0?"SERAPH SHIELD / BOTH FORMS":"AEGIS FORTIFICATION",x,178,454,21,13,Ready);
                EngineeringCard(8+lane*2,upgradePages[1],x,80,454,175);
                EngineeringLink(upgradePages[1],x,-8,x,-25,9+lane*2);
                EngineeringCard(9+lane*2,upgradePages[1],x,-114,454,175);
            }
            Label(upgradePages[1],"Shield capacity and Hardening work in both forms. Shield and Aegis damage reduction stack multiplicatively.",0,-240,944,36,12,Quiet,true);
            EngineeringCard(12,upgradePages[2],0,100,360,130);
            EngineeringLink(upgradePages[2],0,35,-250,17,13);EngineeringLink(upgradePages[2],0,35,250,17,14);
            EngineeringCard(13,upgradePages[2],-250,-48,454,130);EngineeringCard(14,upgradePages[2],250,-48,454,130);
            EngineeringLink(upgradePages[2],-250,-113,-250,-120,15);EngineeringCard(15,upgradePages[2],-250,-190,454,140);
            Label(upgradePages[2],"PATHS & MASTERY\n\nFill the linked parent before advancing. Every point improves its own system; maxing a node unlocks the gold mastery effect.\n\nAll points and mastery progress persist for this run.",250,-190,426,140,13,Quiet,true);
            Button(panel,"CLOSE",405,-314,174,33,()=>SetOpen(false),out _);
            Label(panel,"Points persist for this run. Deploy to purchase or change drone loadout.",-101,-314,760,28,12,Quiet);
            SelectUpgradePage(0);
        }
        private void EngineeringCard(int id,Transform page,float x,float y,float width,float height)
        {
            var card=Rect("Upgrade "+BastionUpgradeTree.Nodes[id].Name,page,x,y,width,height);
            Image(card,0,0,width,height,new Color(.032f,.059f,.089f,.98f));
            upgradeEdges[id]=Image(card,-width*.5f+1,0,2,height,Quiet);
            upgradeNames[id]=Label(card,"",7,height*.5f-17,width-26,20,12.5f,Color.white);
            upgradeEffects[id]=Label(card,"",7,height*.5f-45,width-26,32,12,Quiet,true);
            float bonusHeight=height>=160?48:36;
            upgradeBonuses[id]=Label(card,"",7,-height*.5f+33+bonusHeight*.5f,width-26,bonusHeight,11.5f,Gold,true);
            upgradeBuys[id]=Button(card,"",0,-height*.5f+17,width-22,26,()=>Purchase(BastionUpgradeTree.PurchaseOffset+id,0,false),out upgradeBuyLabels[id]);
            upgradeBuyLabels[id].enableWordWrapping=true;upgradeBuyLabels[id].fontSize=12;upgradeBuyLabels[id].fontSizeMin=10;upgradeBuyLabels[id].fontSizeMax=12;
        }
        private void EngineeringLink(Transform page,float fromX,float fromY,float toX,float toY,int child)
        {
            // Orthogonal connectors keep prerequisites readable without crossing cards.
            float middle=(fromY+toY)*.5f;Color tint=new Color(.3f,.47f,.62f,.65f);
            Image(page,fromX,(fromY+middle)*.5f,2,Math.Abs(fromY-middle),tint);
            Image(page,(fromX+toX)*.5f,middle,Math.Max(2,Math.Abs(fromX-toX)),2,tint);
            Image(page,toX,(middle+toY)*.5f,2,Math.Abs(middle-toY),tint);
        }
        private void SelectUpgradePage(int selected)
        {
            upgradePage=selected;
            for(int i=0;i<3;i++){upgradePages[i].gameObject.SetActive(i==selected);upgradeTabs[i].targetGraphic.color=i==selected?new Color(.18f,.36f,.51f):new Color(.07f,.16f,.25f);}
        }
        private void CycleUnit(int slot)
        {
            if(!c)return;int current=c.Bank.Loadout[slot];
            for(int step=1;step<=4;step++){int next=(current+step)%4;if(c.Bank.CanUseUnit(next)){Purchase(slot,next,true);return;}}
        }
        private void UpdateEngineering()
        {
            var bank=c.Bank;var state=bank.Upgrades;
            foreach(var d in BastionUpgradeTree.Nodes)
            {
                int i=(int)d.Id,rank=state.Points[i];bool complete=rank>=d.Maximum,available=BastionUpgradeTree.Available(state,i);
                upgradeNames[i].text=$"{d.Name} <size=11>{rank}/{d.Maximum}  /  T{d.Tier}</size>";
                upgradeEffects[i].text=BastionUpgradeText.Effect(d.Id,bank);
                upgradeBonuses[i].text=(complete?"<color=#FFE8A0>MASTERY ACTIVE</color> / ":"MAX RANK / ")+d.Bonus;
                upgradeBonuses[i].color=complete?Gold:new Color(.69f,.62f,.43f);
                upgradeEdges[i].color=complete?Gold:available?Ready:Quiet;
                int cost=BastionUpgradeTree.Cost(state,i);
                upgradeBuyLabels[i].text=complete?"MASTERED":!available?$"REQUIRES {BastionUpgradeTree.Nodes[d.Parent].Name} {d.RequiredPoints}/{BastionUpgradeTree.Nodes[d.Parent].Maximum}":$"+1 POINT / {cost} FAB";
                upgradeBuys[i].interactable=c.Sieged&&!c.Deploying&&!complete&&available&&bank.Fabrication>=cost;
            }
            if(upgradePage!=0)return;
            for(int i=0;i<3;i++)
            {
                int kind=Mathf.Clamp(bank.Loadout[i],0,3);bool unlocked=bank.Slots>i;
                unitLabels[i].text=unlocked?$"SLOT {i+1} / {UnitNames[kind]}  ›\n<size=10>{BastionUnitRules.RoleSummary((BastionUnitKind)kind)}</size>":$"SLOT {i+1} / LOCKED";
                unitArt[i].sprite=BastionContent.Icon("hud_unit_"+UnitAssets[kind]);unitArt[i].color=unlocked?Color.white:new Color(.3f,.35f,.4f);
                unitButtons[i].interactable=c.Sieged&&!c.Deploying&&unlocked&&bank.CanUseUnit(1);
            }
        }
    }
}
