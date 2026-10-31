using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace IvoryBastion
{
    internal static class BastionHotkey
    {
        // Gameplay commands allow other held inputs. KeyboardShortcut.IsDown
        // requires an exact chord and can reject C while moving or sprinting.
        internal static bool Pressed(KeyboardShortcut binding,IInputSystem input)
        {
            if(input==null||binding.MainKey==KeyCode.None||!input.GetKeyDown(binding.MainKey))return false;
            foreach(var modifier in binding.Modifiers)if(!input.GetKey(modifier))return false;
            return true;
        }
    }
}
