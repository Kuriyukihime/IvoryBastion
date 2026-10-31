using System;
using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
namespace IvoryBastion
{
    internal static class BastionEngineeringPause
    {
        private static readonly HashSet<object> menus=new HashSet<object>();
        private static Run pausedRun;
        private static bool held,watching;
        private static float resumeScale;
        private static bool Solo=>Run.instance&&RoR2Application.isInSinglePlayer&&!Application.isBatchMode;
        private static bool NativePause=>PauseManager.PauseScreenIsOpen()||PauseStopController.instance&&PauseStopController.instance.isPaused;
        internal static bool Paused=>held&&Solo&&Run.instance==pausedRun&&Time.timeScale==0;
        internal static void SetOpen(object menu,bool open)
        {
            if(menu==null)return;
            if(open)menus.Add(menu);else menus.Remove(menu);
            if(!watching&&(menus.Count>0||held)){RoR2Application.onUpdate+=Tick;watching=true;}
            Tick();
        }
        private static void Tick()
        {
            bool sameRun=pausedRun&&Run.instance==pausedRun;
            if(held&&(!sameRun||!Solo||menus.Count==0))
            {
                // Native Escape can open its own pause during this menu's last
                // frame. Wait for that screen to release before restoring our
                // previous speed; its saved speed may itself be our zero.
                if(sameRun&&Solo&&NativePause)return;
                if(Time.timeScale==0)Time.timeScale=resumeScale;
                held=false;pausedRun=null;
                if(!sameRun)menus.Clear();
            }
            if(!held&&menus.Count>0&&Solo&&!NativePause&&Time.timeScale>0&&!float.IsInfinity(Time.timeScale))
            {resumeScale=Time.timeScale;pausedRun=Run.instance;held=true;Time.timeScale=0;}
            else if(held&&sameRun&&Solo&&menus.Count>0&&!NativePause&&Time.timeScale>0&&!float.IsInfinity(Time.timeScale))
            {resumeScale=Time.timeScale;Time.timeScale=0;}
            if(watching&&menus.Count==0&&!held){RoR2Application.onUpdate-=Tick;watching=false;}
        }
        internal static bool HandlePurchase(object menu,BastionController c,int branch,int choice,bool loadout)
        {
            if(!Paused||!menus.Contains(menu)||!NetworkServer.active)return false;
            // FixedUpdate intentionally stops while the world is paused. Solo
            // purchases use the same host-owned bank validation immediately;
            // multiplayer always retains the original reliable request path.
            if(!c||!c.Alive||!c.Body.hasEffectiveAuthority||!c.Sieged||c.Deploying)return true;
            bool bought=loadout?c.Bank.SetUnit(branch,choice,true):c.Bank.Buy(branch,choice,true);
            if(bought){c.Body.MarkAllStatsDirty();c.Emit(BastionFxKind.Upgrade,c.Body.corePosition,Vector3.up);c.SendStatus();}
            return true;
        }
    }
}
