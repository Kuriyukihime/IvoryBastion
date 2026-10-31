using BepInEx.Configuration;

namespace IvoryBastion
{
    // Local presentation only. These are deliberately outside host balance sync.
    internal static class BastionPresentationSettings
    {
        internal static ConfigEntry<float> DialogueVolume, EffectsVolume, AlertVolume, HudScale;
        internal static ConfigEntry<float> DialogueSpacing,DialogueRepeatCooldown;
        internal static ConfigEntry<bool> FollowGameVolume, DefenseDamageTrail;
        internal static void Bind(ConfigFile config)
        {
            DialogueVolume=config.Bind("Audio","DialogueVolume",1f,new ConfigDescription("Voice-line volume relative to Mod Volume. Existing voice performances are unchanged.",new AcceptableValueRange<float>(0,1)));
            DialogueSpacing=config.Bind("Audio","DialogueSpacing",8f,new ConfigDescription("Minimum seconds between announcements heard locally. Voices never overlap, even at zero. Suppressed lines are skipped.",new AcceptableValueRange<float>(0,60)));
            DialogueRepeatCooldown=config.Bind("Audio","DialogueRepeatCooldown",30f,new ConfigDescription("Minimum seconds before the same survivor repeats a skill announcement. Applies to every voice line.",new AcceptableValueRange<float>(0,180)));
            EffectsVolume=config.Bind("Audio","EffectsVolume",1f,new ConfigDescription("Weapon and engine volume relative to Mod Volume. Dialogue and shield warnings have priority in a crowded mix.",new AcceptableValueRange<float>(0,1)));
            AlertVolume=config.Bind("Audio","AlertVolume",1f,new ConfigDescription("Shield-warning volume relative to Mod Volume.",new AcceptableValueRange<float>(0,1)));
            FollowGameVolume=config.Bind("Audio","FollowGameVolume",true,"Follow Risk of Rain 2's Master and Sound Effects sliders in addition to Mod Volume.");
            HudScale=config.Bind("Presentation","CombatHudScale",1f,new ConfigDescription("Scale the combat and fabrication modules together while preserving their native skill-row alignment.",new AcceptableValueRange<float>(.8f,1.25f)));
            DefenseDamageTrail=config.Bind("Presentation","DefenseDamageTrail",true,"Briefly retain the spent part of each independent defense bar, making actual absorbed damage easier to read.");
        }
    }
}
