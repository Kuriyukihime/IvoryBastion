using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mono.Cecil;
static class NativeChecks
{
    static string[] Calls(MethodDefinition m)=>m.Body.Instructions.Where(i=>i.Operand is MethodReference).Select(i=>{var r=(MethodReference)i.Operand;return r.DeclaringType.Name+"::"+r.Name;}).ToArray();
    internal static void Run(string root)
    {
        using var game=AssemblyDefinition.ReadAssembly(@"X:\SteamLibrary\steamapps\common\Risk of Rain 2\Risk of Rain 2_Data\Managed\RoR2.dll");
        var native=game.MainModule.Types.Single(t=>t.FullName=="RoR2.NetworkStateMachine");
        var receive=native.Methods.Single(m=>m.Name=="HandleSetEntityState");var receiveCalls=Calls(receive);
        RuntimeChecks.Check(receiveCalls.Contains("NetworkConnection::get_clientOwnedObjects")&&receiveCalls.Any(c=>c.EndsWith("::Contains")),"installed native receiver checks the sending client's body ownership");
        var send=native.Methods.Single(m=>m.Name=="SendSetEntityState");var sendCalls=Calls(send);
        RuntimeChecks.Check(sendCalls.Contains("NetworkServer::get_active")&&sendCalls.Contains("NetworkBehaviour::get_hasAuthority")&&sendCalls.Contains("NetworkConnection::SendWriter"),"request channel retains native server/authority gate and transport");
        using var mod=AssemblyDefinition.ReadAssembly(Path.Combine(root,"bin/Release/netstandard2.1/IvoryBastion.dll"));
        TypeDefinition Type(string name)=>mod.MainModule.Types.Single(t=>t.Name==name);
        using(var baseline=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"scripts/network_checks/balance-schema3-ordinals.json"))))
        {
            var fields=Type("BastionSetting").Fields.Where(f=>f.HasConstant).ToArray();
            int oldCount=baseline.RootElement.GetArrayLength();
            foreach(var key in baseline.RootElement.EnumerateArray())
            {
                string name=key.GetProperty("name").GetString();int ordinal=key.GetProperty("ordinal").GetInt32();
                RuntimeChecks.Check(fields.Any(f=>f.Name==name&&Convert.ToInt32(f.Constant)==ordinal),"schema3 wire ordinal preserved for "+name);
            }
            string[] appended={"UpgradedMineDeployRadius","UpgradedMineCount","UpgradedMineChainBonus","UpgradedMineBlastRadius","UpgradedWingburstDistance","UpgradedWingburstShots","UpgradedWingburstCooldown"};
            RuntimeChecks.Check(fields.Length==oldCount+appended.Length,"schema4 only appends the seven utility upgrade options");
            for(int i=0;i<appended.Length;i++)RuntimeChecks.Check(fields.Any(f=>f.Name==appended[i]&&Convert.ToInt32(f.Constant)==oldCount+i),"new key appended after the old wire prefix: "+appended[i]);
        }
        var create=Type("BastionContent").Methods.Single(m=>m.Name=="Create");
        RuntimeChecks.Check(create.Body.Instructions.Any(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Ldstr&&(string)i.Operand=="BastionRequests"),"dedicated request FSM is built into the real body prefab");
        RuntimeChecks.Check(create.Body.Instructions.Any(i=>i.Operand is GenericInstanceMethod m&&m.Name=="AddEntityState"&&m.GenericArguments.Any(t=>t.Name=="BastionRequestBatch")),"request state is in the network entity catalog");
        RuntimeChecks.Check(Type("BastionRequestBatch").BaseType.FullName=="EntityStates.EntityState"&&Calls(Type("BastionRequestBatch").Methods.Single(m=>m.Name=="OnEnter")).Contains("NetworkServer::get_active"),"batch executes only through a native state with a server gate");
        foreach(string name in new[]{"BastionShot","BastionSecondary","BastionSpecial","BastionTransform","BastionPurchase"})
        {
            var onEnter=Type(name).Methods.Single(m=>m.Name=="OnEnter");var calls=Calls(onEnter);
            RuntimeChecks.Check(calls.Contains("EntityState::get_isAuthority")&&calls.Contains("BastionRequests::Submit"),name+" submits once from input authority");
            RuntimeChecks.Check(!calls.Any(c=>c=="BastionController::Shoot"||c=="BastionController::ActivateSecondary"||c=="BastionController::ActivateSpecial"||c=="BastionController::SetSiege"),name+" mirrored animation cannot independently execute the ability");
        }
        var shoot=Calls(Type("BastionController").Methods.Single(m=>m.Name=="Shoot"));
        RuntimeChecks.Check(shoot.Contains("NetworkServer::get_active")&&shoot.Contains("BastionLaserTracker::Find")&&shoot.Contains("BastionCombat::Bullet"),"host still owns shot origin, lock-on and native damage/item interactions");
        foreach(string name in new[]{"SetSiege","ActivateSecondary","ActivateSpecial","FixedUpdate"})
            RuntimeChecks.Check(Calls(Type("BastionController").Methods.Single(m=>m.Name==name)).Contains("NetworkServer::get_active"),"configured "+name+" retains host-only execution");
        RuntimeChecks.Check(!Calls(Type("BastionTransform").Methods.Single(m=>m.Name=="OnExit")).Contains("BastionController::SetSiege"),"animation exit cannot revert/commit a host transformation");
    }
}
