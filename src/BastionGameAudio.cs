using System;
using System.Globalization;
using System.Reflection;
using RoR2;
using RoR2.ConVar;
using UnityEngine;

namespace IvoryBastion
{
    internal static class BastionGameAudio
    {
        private static readonly FieldInfo master=typeof(AudioManager).GetField("cvVolumeMaster",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
        private static readonly FieldInfo effects=typeof(AudioManager).GetField("cvVolumeSfx",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
        private static float nextPoll,gain=1;
        internal static float Gain
        {
            get
            {
                if(BastionPresentationSettings.FollowGameVolume?.Value==false)return 1;
                if(Time.unscaledTime<nextPoll)return gain;
                nextPoll=Time.unscaledTime+.2f;
                try{gain=Percent(master)*Percent(effects);}catch{gain=1;}
                return gain;
            }
        }
        private static float Percent(FieldInfo field)
        {
            var setting=field?.GetValue(null) as BaseConVar;
            return setting!=null&&float.TryParse(setting.GetString(),NumberStyles.Float,CultureInfo.InvariantCulture,out float value)?Mathf.Clamp01(value/100):1;
        }
    }
}
