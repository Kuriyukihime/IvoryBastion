using System;
using IvoryBastion;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
internal static class EngineeringPauseChecks
{
    private static int count;
    private static void Check(bool value,string why){count++;if(!value)throw new Exception("Engineering pause: "+why);}
    private static void Frame()=>RoR2Application.onUpdate?.Invoke();
    internal static void Run()
    {
        object first=new(),second=new();
        NetworkServer.active=true;NetworkServer.dontListen=false;RoR2Application.isInLocalMultiPlayer=false;RoR2.Run.instance=new();Application.isBatchMode=false;Time.timeScale=1;
        BastionEngineeringPause.SetOpen(first,true);Frame();Check(Time.timeScale==1&&!BastionEngineeringPause.Paused,"multiplayer host, even alone, cannot be paused by Engineering");BastionEngineeringPause.SetOpen(first,false);
        NetworkServer.active=false;BastionEngineeringPause.SetOpen(first,true);Frame();Check(Time.timeScale==1,"remote client never changes time scale");BastionEngineeringPause.SetOpen(first,false);
        NetworkServer.active=true;NetworkServer.dontListen=true;RoR2Application.isInLocalMultiPlayer=true;BastionEngineeringPause.SetOpen(first,true);Check(Time.timeScale==1,"local multiplayer excluded");BastionEngineeringPause.SetOpen(first,false);RoR2Application.isInLocalMultiPlayer=false;
        Application.isBatchMode=true;BastionEngineeringPause.SetOpen(first,true);Check(Time.timeScale==1,"batch/dedicated presentation excluded");BastionEngineeringPause.SetOpen(first,false);Application.isBatchMode=false;
        RoR2.Run.instance=null;BastionEngineeringPause.SetOpen(first,true);Check(Time.timeScale==1,"no run cannot acquire a pause");BastionEngineeringPause.SetOpen(first,false);RoR2.Run.instance=new();
        Time.timeScale=.75f;BastionEngineeringPause.SetOpen(first,true);Check(Time.timeScale==0&&BastionEngineeringPause.Paused,"solo opening pauses immediately");
        for(int i=0;i<100;i++){Time.unscaledTime+=.02f;Frame();Check(Time.timeScale==0,"unscaled frames keep the world paused");}
        var go=new GameObject();var body=go.AddComponent<CharacterBody>();body.hasEffectiveAuthority=true;var c=go.AddComponent<BastionController>();c.Body=body;c.Sieged=true;c.Bank.Upgrades.Active=true;c.Bank.Fabrication=100;
        Check(BastionEngineeringPause.HandlePurchase(first,c,100,0,false),"paused solo handles purchase without a physics tick");Check(c.Bank.Upgrades.Points[0]==1&&c.Bank.Fabrication==80&&c.StatusSends==1,"actual host bank grants one point and spends once while paused");
        Check(BastionEngineeringPause.HandlePurchase(first,c,101,0,false)&&c.Bank.Upgrades.Points[1]==0&&c.Bank.Fabrication==80,"paused purchases still enforce prerequisites");
        Check(!BastionEngineeringPause.HandlePurchase(new object(),c,100,0,false),"an unrelated menu cannot use the paused purchase path");
        c.Body.hasEffectiveAuthority=false;Check(BastionEngineeringPause.HandlePurchase(first,c,100,0,false)&&c.Bank.Upgrades.Points[0]==1,"non-owned body cannot buy");c.Body.hasEffectiveAuthority=true;
        c.Sieged=false;BastionEngineeringPause.HandlePurchase(first,c,100,0,false);Check(c.Bank.Upgrades.Points[0]==1,"paused flight still cannot buy");c.Sieged=true;
        c.Bank.Upgrades.Points[6]=1;c.Bank.Upgrades.Points[7]=1;Check(BastionEngineeringPause.HandlePurchase(first,c,0,1,true)&&c.Bank.Loadout[0]==1,"drone loadout changes apply immediately while paused");
        BastionEngineeringPause.SetOpen(second,true);BastionEngineeringPause.SetOpen(first,false);Check(Time.timeScale==0,"another live Engineering view keeps its pause");BastionEngineeringPause.SetOpen(second,false);Check(Time.timeScale==.75f&&!BastionEngineeringPause.Paused,"last close restores original nonstandard speed");
        Check(RoR2Application.onUpdate==null,"closing removes the unscaled observer");
        // Borrowing an existing pause must never unpause its owner.
        Time.timeScale=0;PauseManager.ScreenOpen=true;BastionEngineeringPause.SetOpen(first,true);BastionEngineeringPause.SetOpen(first,false);Check(Time.timeScale==0,"pre-existing native pause is preserved");PauseManager.ScreenOpen=false;Time.timeScale=1;
        // Native Escape can save our zero as its own pre-pause speed.
        BastionEngineeringPause.SetOpen(first,true);PauseStopController.instance=new(){isPaused=true};PauseManager.ScreenOpen=true;BastionEngineeringPause.SetOpen(first,false);Frame();Check(Time.timeScale==0&&RoR2Application.onUpdate!=null,"close defers restoration while Escape pause is active");
        PauseStopController.instance.isPaused=false;PauseManager.ScreenOpen=false;Frame();Check(Time.timeScale==1&&RoR2Application.onUpdate==null,"native Escape close cannot leave the game stuck at zero");PauseStopController.instance=null;
        BastionEngineeringPause.SetOpen(first,true);Time.timeScale=.5f;Frame();Check(Time.timeScale==0,"open menu reacquires a changed game speed");BastionEngineeringPause.SetOpen(first,false);Check(Time.timeScale==.5f,"closing respects updated external game speed");
        Time.timeScale=1;BastionEngineeringPause.SetOpen(first,true);NetworkServer.dontListen=false;Frame();Check(Time.timeScale==1&&!BastionEngineeringPause.Paused,"entering multiplayer immediately releases our solo pause");float before=c.Bank.Fabrication;Check(!BastionEngineeringPause.HandlePurchase(first,c,100,0,false)&&c.Bank.Fabrication==before,"multiplayer bypass is impossible and leaves reliable request handling to HUD");BastionEngineeringPause.SetOpen(first,false);
        NetworkServer.dontListen=true;BastionEngineeringPause.SetOpen(first,true);RoR2.Run.instance=new();Frame();Check(Time.timeScale==1&&RoR2Application.onUpdate==null,"run replacement cleans up the old pause");BastionEngineeringPause.SetOpen(first,false);
        Check(!BastionEngineeringPause.HandlePurchase(first,c,100,0,false),"closed Engineering cannot use local purchase shortcut");
        RoR2.Run.instance=null;NetworkServer.dontListen=false;Time.timeScale=1;Time.unscaledTime=0;
        Console.WriteLine($"PASS: {count} solo Engineering pause, immediate validated purchases, multiplayer exclusion and nested-pause cleanup checks.");
    }
}
