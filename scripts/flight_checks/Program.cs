using System;
using System.IO;
using System.Linq;
using IvoryBastion;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEngine;

static class Program
{
    static int checks;
    static void Check(bool value,string why){checks++;if(!value)throw new Exception("Flight: "+why);}
    static float Tick(BastionMain state,int ticks)
    {float height=0;for(int i=0;i<ticks;i++){state.HandleMovements();state.characterMotor.PreMove(state.characterBody.moveSpeed,Time.fixedDeltaTime);height+=state.characterMotor.velocity.y*Time.fixedDeltaTime;}return height;}
    static void Main(string[] args)
    {
        string root=Path.GetFullPath(args[0]);
        using(var game=AssemblyDefinition.ReadAssembly(@"X:\SteamLibrary\steamapps\common\Risk of Rain 2\Risk of Rain 2_Data\Managed\RoR2.dll"))
        {
            var type=game.MainModule.Types.Single(t=>t.FullName=="RoR2.CharacterMotor");
            var pre=type.Methods.Single(m=>m.Name=="PreMove").Body.Instructions;
            Check(pre.Any(i=>i.Operand is MethodReference m&&m.Name=="get_useGravity"),"native motor gates gravity before modifying it");
            var modify=type.Methods.Single(m=>m.Name=="ModifyGravity").Body.Instructions;
            Check(modify.Any(i=>i.Operand is FieldReference f&&f.Name=="useCustomGravity")&&modify.Any(i=>i.Operand is FieldReference f&&f.Name=="CustomGravity"),"installed custom gravity can replace a zero-scaled gravity value");
            Check(type.Methods.Single(m=>m.Name=="set_gravityParameters").Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="CheckShouldUseGravity"),"native gravity grant updates cached gravity flag");
        }
        // Reproduce the weakness of scale-only flight against current native gravity.
        var legacy=new RoR2.CharacterMotor{gravityScale=0,airControl=1};legacy.flightParameters=new(){channeledFlightGranterCount=1};
        legacy.PreMove(14,.02f);Check(legacy.velocity.y<0,"scale-only gravity suppression permits falling with custom gravity");
        foreach(bool custom in new[]{false,true})foreach(bool grounded in new[]{false,true})foreach(bool authority in new[]{false,true})
        {
            var state=new BastionMain{isAuthority=authority};var motor=state.characterMotor;
            motor.useCustomGravity=custom;motor.Motor.GroundingStatus.IsStableOnGround=grounded;
            motor.Motor.LastGroundingStatus.IsStableOnGround=grounded;
            state.OnEnter();state.inputBank.jump.down=true;state.jumpInputReceived=true;
            float rise=Tick(state,100);
            if(authority)
            {
                Check(rise>20,"jump input ascends from ground/air with default/custom gravity on host/client authority");
                Check(!motor.useGravity&&motor.isFlying,"native flight and anti-gravity enabled together");
                Check(!state.jumpInputReceived,"flight suppresses repeated normal jump impulses");
                Check(motor.Motor.Ungrounds==(grounded?1:0),"only one takeoff unground; no per-tick upward drift");
                state.inputBank.jump.down=false;Tick(state,150);float drift=Tick(state,250);
                Check(Math.Abs(drift)<.001,"idle hover stays at constant altitude after settling");
                state.moveVector=Vector3.forward;state.aimDirection=new Vector3(0,1,1);Check(Tick(state,60)>5,"aim-directed flight climbs while moving");
                state.Controller.Sieged=true;Check(Math.Abs(Tick(state,20))<.001&&motor.velocity.sqrMagnitude==0,"siege remains immobile");
                state.Controller.Sieged=false;state.Controller.Deploying=true;Check(Math.Abs(Tick(state,20))<.001,"transformation holds position");
                state.Controller.Deploying=false;state.inputBank.jump.down=true;Check(Tick(state,20)>1,"packing restores immediate ascent");
                state.moveVector=Vector3.zero;state.inputBank.jump.down=false;state.aimDirection=new Vector3(0,1,1);state.Controller.Boosting=true;
                Check(Tick(state,20)>1,"boost can lift with no movement input");
            }
            else Check(motor.flightParameters.channeledFlightGranterCount==0&&motor.gravityParameters.channeledAntiGravityGranterCount==0&&motor.Motor.Ungrounds==0,"remote replicas never take movement authority");
            state.OnExit();Check(motor.gravityScale==1&&motor.airControl==.25f&&motor.Motor.GroundSolving,"exit restores motor settings");
            Check(motor.gravityParameters.channeledAntiGravityGranterCount==0&&motor.flightParameters.channeledFlightGranterCount==0,"exit returns all owned grants");
        }
        var shared=new BastionMain();shared.characterMotor.flightParameters=new(){channeledFlightGranterCount=2};shared.characterMotor.gravityParameters=new(){channeledAntiGravityGranterCount=3};
        shared.OnEnter();Tick(shared,1000);shared.OnExit();shared.OnExit();
        Check(shared.characterMotor.flightParameters.channeledFlightGranterCount==2&&shared.characterMotor.gravityParameters.channeledAntiGravityGranterCount==3,"long flight/repeated exit preserves other states' grants");
        shared.OnEnter();shared.inputBank.jump.down=true;Check(Tick(shared,20)>1,"revived/re-entered main state can ascend");shared.OnExit();
        Console.WriteLine($"PASS: {checks} production-flight and native-gravity regression checks.");
    }
}
