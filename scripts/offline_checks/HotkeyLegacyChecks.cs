using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using IvoryBastion;
using UnityEngine;

internal static class HotkeyLegacyChecks
{
    private sealed class InputFrame:IInputSystem
    {
        internal readonly HashSet<KeyCode> Held=new(),Down=new();
        public Vector3 mousePosition=>default;
        public Vector2 mouseScrollDelta=>default;
        public bool mousePresent=>true;
        public bool anyKey=>Held.Count>0;
        public bool anyKeyDown=>Down.Count>0;
        public IEnumerable<KeyCode> SupportedKeyCodes=>(KeyCode[])Enum.GetValues(typeof(KeyCode));
        public bool GetKey(KeyCode key)=>Held.Contains(key);
        public bool GetKeyDown(KeyCode key)=>Down.Contains(key);
        public bool GetKeyUp(KeyCode key)=>false;
        public bool GetKey(string key)=>GetKey((KeyCode)Enum.Parse(typeof(KeyCode),key,true));
        public bool GetKeyDown(string key)=>GetKeyDown((KeyCode)Enum.Parse(typeof(KeyCode),key,true));
        public bool GetKeyUp(string key)=>false;
        public bool GetMouseButton(int index)=>GetKey(KeyCode.Mouse0+index);
        public bool GetMouseButtonDown(int index)=>GetKeyDown(KeyCode.Mouse0+index);
        public bool GetMouseButtonUp(int index)=>false;
        public void ResetInputAxes(){Held.Clear();Down.Clear();}
    }

    internal static void Run()
    {
        int count=0;
        Action<bool,string> check=(valid,why)=>{count++;if(!valid)throw new Exception("Transformation input: "+why);};
        var input=new InputFrame();var transform=new KeyboardShortcut(KeyCode.C);
        var heldKeys=new[]{KeyCode.W,KeyCode.A,KeyCode.S,KeyCode.D,KeyCode.Space,KeyCode.LeftShift,KeyCode.LeftControl,KeyCode.LeftAlt,KeyCode.Mouse0,KeyCode.Mouse1,KeyCode.R,KeyCode.V};
        for(int mask=0;mask<(1<<heldKeys.Length);mask++)
        {
            input.ResetInputAxes();input.Held.Add(KeyCode.C);input.Down.Add(KeyCode.C);
            for(int bit=0;bit<heldKeys.Length;bit++)if((mask&(1<<bit))!=0)input.Held.Add(heldKeys[bit]);
            check(BastionHotkey.Pressed(transform,input),"transformation wins over simultaneous movement, boost, attacks and modifier keys: "+mask);
            input.Down.Clear();
            check(!BastionHotkey.Pressed(transform,input),"holding the transformation key does not retrigger it: "+mask);
        }
        input.ResetInputAxes();input.Down.Add(KeyCode.C);input.Held.Add(KeyCode.W);input.Held.Add(KeyCode.LeftShift);
        var chord=new KeyboardShortcut(KeyCode.C,KeyCode.LeftControl);
        check(!BastionHotkey.Pressed(chord,input),"an explicitly configured modifier is still required");
        input.Held.Add(KeyCode.LeftControl);
        check(BastionHotkey.Pressed(chord,input),"configured chord accepts additional movement and sprint inputs");
        check(!BastionHotkey.Pressed(KeyboardShortcut.Empty,input),"unbound transformation remains inactive");
        check(!BastionHotkey.Pressed(transform,null),"missing input provider cannot trigger transformation");
        input.ResetInputAxes();input.Down.Add(KeyCode.Mouse4);input.Held.Add(KeyCode.W);input.Held.Add(KeyCode.Mouse0);
        check(BastionHotkey.Pressed(new KeyboardShortcut(KeyCode.Mouse4),input),"a mouse-bound transformation also works while firing and moving");
        Console.WriteLine("PASS: "+count+" transformation input checks with the installed BepInEx shortcut/input interfaces; no game launched.");
    }
}
