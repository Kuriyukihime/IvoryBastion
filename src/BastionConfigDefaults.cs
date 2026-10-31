using BepInEx.Configuration;

namespace IvoryBastion
{
    internal static class BastionConfigDefaults
    {
        internal static void Reset(ConfigFile settings)
        {
            if(settings==null)return;
            bool saveOnSet=settings.SaveOnConfigSet;
            try
            {
                settings.SaveOnConfigSet=false;
                foreach(var pair in settings)pair.Value.BoxedValue=pair.Value.DefaultValue;
                settings.Save();
            }
            finally{settings.SaveOnConfigSet=saveOnSet;}
        }
    }
}
