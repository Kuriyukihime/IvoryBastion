using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using EntityStates;
using IvoryBastion;
using Mono.Cecil;
static class Program
{
    static int count;
    static void Check(bool value,string why){count++;if(!value)throw new Exception("State binding: "+why);}
    static Type Define(string version)
    {
        var assembly=AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("IvoryStateResolutionProbe"){Version=Version.Parse(version)},AssemblyBuilderAccess.Run);
        return assembly.DefineDynamicModule("State").DefineType("IvoryBastion.SameNamedState",System.Reflection.TypeAttributes.Public,typeof(EntityState)).CreateType();
    }
    static void Main(string[] args)
    {
        var cache=typeof(SerializableEntityStateType).GetField("_stateType",BindingFlags.Instance|BindingFlags.NonPublic);
        Check(cache!=null&&cache.FieldType==typeof(Type),"installed native struct exposes the expected Type cache");
        var old=Define("0.22.0.0");var current=Define("0.25.0.0");
        Check(old.FullName==current.FullName&&old!=current,"two loaded versions have matching state names but different identities");
        for(int i=0;i<100;i++)
        {
            var state=BastionStateType.For(i%2==0?current:old);var expected=i%2==0?current:old;
            Check(ReferenceEquals(state.stateType,expected),"native getter retains the exact requested assembly/type");
            var copy=state;Check(ReferenceEquals(copy.stateType,expected),"skill-definition/state-machine struct copies retain their binding");
        }
        Check(BastionStateType.For<Idle>().stateType==typeof(Idle),"native state remains valid");
        bool invalid=false;try{BastionStateType.For(typeof(string));}catch(ArgumentException){invalid=true;}Check(invalid,"invalid state types cannot poison native cache");
        string root=Path.GetFullPath(args[0]);
        using var mod=AssemblyDefinition.ReadAssembly(Path.Combine(root,"bin/Release/netstandard2.1/IvoryBastion.dll"));
        var transformInput=mod.MainModule.Types.Single(t=>t.Name=="BastionTransformInput").Methods.Single(m=>m.Name=="Update");
        Check(transformInput.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="BastionHotkey"&&m.Name=="Pressed"),"shipped transformation accepts other held gameplay inputs");
        Check(!transformInput.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="KeyboardShortcut"&&m.Name=="IsDown"),"strict chord matching cannot suppress transformation");
        var controller=mod.MainModule.Types.Single(t=>t.Name=="BastionController");
        Check(controller.Methods.Single(m=>m.Name=="Awake").Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="BastionBodyStates"&&m.Name=="Bind"),"every spawned/revived body rebinds after Unity serialization");
        var content=mod.MainModule.Types.Single(t=>t.Name=="BastionContent");
        var skill=content.Methods.Single(m=>m.Name=="Skill").Body.Instructions;
        Check(skill.Any(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="BastionStateType"&&m.Name=="For"),"all eight skill definitions use exact-type binding");
        Check(!skill.Any(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="SerializableEntityStateType"&&m.Name==".ctor"),"skill factory cannot fall back to ambiguous string-only construction");
        var bind=mod.MainModule.Types.Single(t=>t.Name=="BastionBodyStates").Methods.Single(m=>m.Name=="Bind").Body.Instructions;
        Check(bind.Any(i=>i.Operand is GenericInstanceMethod g&&g.GenericArguments.Any(t=>t.Name=="BastionMain"))&&bind.Any(i=>i.Operand is GenericInstanceMethod g&&g.GenericArguments.Any(t=>t.Name=="BastionDeath")),"live locomotion and revival/death states both bind to active assembly");
        Console.WriteLine($"PASS: {count} real native state identity, live-body binding and transformation input-priority checks.");
    }
}
