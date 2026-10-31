using System;
using System.Collections.Generic;

namespace R2API
{
    // Match installed R2API.Language 1.1.0: Add never replaces a token;
    // overlays are ordered, replaceable and independent of base additions.
    public static class LanguageAPI
    {
        private static readonly Dictionary<string,string> custom=new Dictionary<string,string>();
        private static readonly List<LanguageOverlay> overlays=new List<LanguageOverlay>();
        internal static int OverlayCount=>overlays.Count;

        public static void Add(string key,string value)
        {
            if(key==null||value==null)throw new ArgumentNullException();
            if(!custom.ContainsKey(key))custom.Add(key,value);
        }

        public static LanguageOverlay AddOverlay(Dictionary<string,string> tokens)=>new LanguageOverlay(tokens);

        internal static string Get(string token)
        {
            for(int i=overlays.Count-1;i>=0;i--)
                if(overlays[i].Tokens.TryGetValue(token,out string value))return value;
            return custom.TryGetValue(token,out string baseline)?baseline:token;
        }

        public sealed class LanguageOverlay
        {
            internal readonly Dictionary<string,string> Tokens;
            internal LanguageOverlay(Dictionary<string,string> tokens)
            {
                Tokens=new Dictionary<string,string>(tokens);
                overlays.Add(this);
            }
            public void Remove()=>overlays.Remove(this);
        }
    }
}
