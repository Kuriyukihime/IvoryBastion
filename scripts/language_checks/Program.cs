using System;
using System.Globalization;
using System.IO;
using System.Linq;
using IvoryBastion;
using Mono.Cecil;
using Mono.Cecil.Cil;
using R2API;

internal static class Program
{
    private static int checks;
    private static void Check(bool value,string message)
    {
        checks++;
        if(!value)throw new Exception("Language regression: "+message);
    }
    private static string Text(string suffix)=>LanguageAPI.Get("BASTION_"+suffix);
    private static void Contains(string suffix,string expected,string message)
    {
        string stem=suffix.EndsWith("_DESCRIPTION",StringComparison.Ordinal)?suffix.Substring(0,suffix.Length-12):"";
        string details=Text(suffix)+string.Join("\n\n",BastionBalanceText.Keywords(stem).Where(k=>k.StartsWith("KEYWORD_BASTION_",StringComparison.Ordinal)).Select(LanguageAPI.Get));
        Check(details.Contains(expected,StringComparison.Ordinal),message);
    }

    private static void Main(string[] args)
    {
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
        string root=Path.GetFullPath(args.Length>0?args[0]:".");
        using var assembly=AssemblyDefinition.ReadAssembly(Path.Combine(root,"bin/Release/netstandard2.1/IvoryBastion.dll"));
        var content=assembly.MainModule.Types.Single(t=>t.Name=="BastionContent");
        var language=content.Methods.Single(m=>m.Name=="Language");
        var create=content.Methods.Single(m=>m.Name=="Create");
        Check(create.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.FullName==language.FullName),"content creation registers language before a run exists");
        Check(language.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="BastionBalanceText"&&m.Name=="Refresh"),"startup language registration refreshes dynamic utility tokens");
        Check(language.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldstr&&((string)i.Operand).Contains("Heavenfall Rush")),"survivor overview uses the current mobile utility name");
        Check(create.Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.DeclaringType.Name=="PassiveSkill"&&f.Name=="keywordToken"),"passive is wired to the native keyword popup field");
        Check(create.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldstr&&(string)i.Operand=="KEYWORD_BASTION_FACTORY"),"passive uses its own Engineering/Fabrication/shield popup");
        foreach(var factoryMethod in new[]{"Skill","UtilitySkill"})Check(content.Methods.Single(m=>m.Name==factoryMethod).Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="BastionBalanceText"&&m.Name=="Keywords"),"both skill factories assign native detail keywords: "+factoryMethod);
        using(var native=AssemblyDefinition.ReadAssembly("X:/SteamLibrary/steamapps/common/Risk of Rain 2/Risk of Rain 2_Data/Managed/RoR2.dll"))
        {
            var selection=native.MainModule.Types.Single(t=>t.FullName=="RoR2.UI.CharacterSelectController");
            Check(selection.Methods.Single(m=>m.Name=="BuildSkillStripDisplayData").Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.DeclaringType.Name=="PassiveSkill"&&f.Name=="keywordToken"),"installed survivor screen reads passive keyword text");
            Check(selection.Methods.Single(m=>m.Name=="RebuildStrip").Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.DeclaringType.Name=="HGButton"&&f.Name=="hoverToken"),"installed skill row sends keyword explanations to the native hover panel");
        }

        // Replay the production method's constant registrations. This catches
        // old base descriptions masking dynamic text with first-write-wins Add.
        var instructions=language.Body.Instructions;
        for(int i=0;i<instructions.Count;i++)
        {
            if(instructions[i].Operand is not MethodReference method)continue;
            if(method.DeclaringType.Name=="LanguageAPI"&&method.Name=="Add"&&method.Parameters.Count==2&&i>=2&&
                instructions[i-2].OpCode==OpCodes.Ldstr&&instructions[i-1].OpCode==OpCodes.Ldstr)
                LanguageAPI.Add((string)instructions[i-2].Operand,(string)instructions[i-1].Operand);
            else if(method.DeclaringType.Name=="BastionContent"&&method.Name=="Token"&&i>=3&&
                instructions[i-3].OpCode==OpCodes.Ldstr&&instructions[i-2].OpCode==OpCodes.Ldstr&&instructions[i-1].OpCode==OpCodes.Ldstr)
            {
                string stem="BASTION_"+(string)instructions[i-3].Operand;
                LanguageAPI.Add(stem+"_NAME",(string)instructions[i-2].Operand);
                LanguageAPI.Add(stem+"_DESCRIPTION",(string)instructions[i-1].Operand);
            }
        }
        LanguageAPI.Add("UNRELATED_BASE","untouched base");
        var external=LanguageAPI.AddOverlay(new System.Collections.Generic.Dictionary<string,string>{{"UNRELATED_OVERLAY","untouched overlay"}});
        LanguageAPI.Add("FIRST_WRITE_TEST","first");LanguageAPI.Add("FIRST_WRITE_TEST","second");
        Check(LanguageAPI.Get("FIRST_WRITE_TEST")=="first","stand-in models Add as first-write-wins");

        // This production refresh is the same one reached by Create/Language,
        // with no Run, networking, body spawn or Unity update involved.
        BastionBalance.Reset();BastionBalanceText.Refresh();
        Check(Text("PASSIVE_DESCRIPTION").IndexOf("Engineering",StringComparison.Ordinal)<Text("PASSIVE_DESCRIPTION").IndexOf("Seraph Shield",StringComparison.Ordinal),"Engineering stays ahead of shield information in the compact passive");
        Contains("PASSIVE_DESCRIPTION","V (default)","the Engineering control remains visible without opening a popup");
        var factory=LanguageAPI.Get("KEYWORD_BASTION_FACTORY");
        Check(factory.Contains("<style=cKeywordName>Engineering</style>")&&factory.Contains("<style=cKeywordName>Fabrication</style>")&&factory.Contains("<style=cKeywordName>Seraph Shield</style>"),"native popup has three named keyword sections");
        Check(factory.Contains("17/min idle")&&factory.Contains("42/min in combat")&&factory.Contains("15% max health mobile / 50% siege"),"fresh-menu keyword details resolve current economy and shield defaults");
        Check(factory.Contains("7s mobile / 4s siege")&&factory.Contains("Barrier protects recharge"),"shield detail is retained in the popup rather than the skill row");
        foreach(string stem in new[]{"PASSIVE","NEEDLE","BOOST","WINGBURST","REDLINE","LANCE","PROTOCOL","MINES","SATURATION"})
            foreach(string key in BastionBalanceText.Keywords(stem).Where(k=>k.StartsWith("KEYWORD_BASTION_",StringComparison.Ordinal)))Check(LanguageAPI.Get(key)!=key&&LanguageAPI.Get(key).Contains("<style=cKeywordName>"),"every assigned custom keyword resolves in the fresh menu: "+key);
        foreach(string stem in new[]{"NEEDLE","BOOST","WINGBURST","REDLINE","LANCE","PROTOCOL","MINES","SATURATION","PASSIVE"})
        foreach(string suffix in new[]{"NAME","DESCRIPTION"})
        {
            string token="BASTION_"+stem+"_"+suffix;
            Check(!string.IsNullOrWhiteSpace(LanguageAPI.Get(token))&&LanguageAPI.Get(token)!=token,"fresh menu resolves "+token);
        }
        Check(Text("WINGBURST_NAME")=="Heavenfall Rush"&&Text("MINES_NAME")=="Widow Array","fresh menu names both form utilities correctly");
        Contains("WINGBURST_DESCRIPTION","15m","mobile utility default range is visible before a run");
        Contains("WINGBURST_DESCRIPTION","32 homing","mobile utility default missile count is visible before a run");
        Contains("WINGBURST_DESCRIPTION","40% damage each","skirmish micro-missile damage appears in fresh menu");
        Contains("WINGBURST_DESCRIPTION","Two charges","menu describes two charges");
        Contains("WINGBURST_DESCRIPTION","10s recharge","menu describes ten-second recharge per charge");
        Contains("WINGBURST_DESCRIPTION","4m radius","fresh menu explains wider real micro explosions");
        Contains("MINES_DESCRIPTION","24 spider mines","siege utility default mine count is visible before a run");
        Contains("MINES_DESCRIPTION","three staggered rings reaching 15m","fresh menu explains the wider three-ring field");
        Contains("MINES_DESCRIPTION","60s lifetime after landing","fresh menu shows the sixty-second grounded lifetime");
        Contains("MINES_DESCRIPTION","within 30m","fresh menu shows increased target detection");
        Contains("MINES_DESCRIPTION","pursue up to 50m","fresh menu shows increased pursuit budget");
        Contains("MINES_DESCRIPTION","attach to mark","description explains enemy-mounted marks");
        Contains("MINES_DESCRIPTION","Only your missiles or Ion Lance hitting","description excludes autonomous mine damage");
        Contains("MINES_DESCRIPTION","hitting the marked target","external weapon must hit the marked enemy");
        Contains("MINES_DESCRIPTION","Heavenfall and Skyfall also trigger marks","both missile utilities are included in the mark combo");
        Contains("MINES_DESCRIPTION","Each charged burst","description identifies charged detonation damage");
        Contains("MINES_DESCRIPTION","90% damage in 8m","fresh menu gives actual charged blast damage and radius");
        Contains("MINES_DESCRIPTION","Maximum attached: 5 normal / 8 elite / 12 boss","fresh menu explains the shared per-target cap");
        Contains("MINES_DESCRIPTION","48 maximum","fresh menu gives the shared active-mine limit");
        Contains("MINES_DESCRIPTION","Mine blasts never trigger other mines","no mine-to-mine activation is explicit");
        Check(!Text("MINES_DESCRIPTION").Contains("stop and arm on the ground")&&!Text("MINES_DESCRIPTION").Contains("After 0.8s"),"obsolete autonomous ground-fuse wording is absent");
        Contains("NEEDLE_DESCRIPTION","80%","dynamic defaults replace stale static primary damage");
        Contains("SATURATION_DESCRIPTION","500%","dynamic defaults replace stale static ultimate damage");
        Check(BastionBalanceText.UpgradeCards[0,0].Contains("Widow field 18m"),"Ordnance I advertises the larger mine field");
        Check(BastionBalanceText.UpgradeCards[0,1].Contains("Widow Array 30 mines"),"Ordnance II advertises the extra mines");
        Check(BastionBalanceText.UpgradeChoice(0,0).Contains("105% per detonation")&&BastionBalanceText.UpgradeChoice(0,1).Contains("Mine blasts 10m"),"exclusive Ordnance choices advertise their different mine bonuses");
        Check(BastionBalanceText.UpgradeCards[3,0].Contains("Rush 18m")&&BastionBalanceText.UpgradeCards[3,1].Contains("40 micro-missiles")&&BastionBalanceText.UpgradeCards[3,2].Contains("Rush recharge 8s"),"all Skirmisher ranks advertise their Rush bonus");
        Check(new[]{"NEEDLE","BOOST","DEPLOY","PACK","WINGBURST","REDLINE","LANCE","PROTOCOL","MINES","SATURATION"}.All(stem=>!Text(stem+"_DESCRIPTION").Contains("upgrade",StringComparison.OrdinalIgnoreCase)),"active skill descriptions leave upgrade details in Engineering; the passive introduces that menu");
        Check(!Text("MINES_DESCRIPTION").Contains("Ordnance"),"mine skill description leaves its branch explanation in Engineering");
        Check(LanguageAPI.OverlayCount==2,"one Bastion overlay coexists with unrelated overlay");

        var profile=BastionBalance.Defaults();
        profile[(int)BastionSetting.NeedleDamage]=130;
        profile[(int)BastionSetting.WingburstDistance]=36;
        profile[(int)BastionSetting.WingburstShots]=40;
        profile[(int)BastionSetting.WingburstDamage]=135;
        profile[(int)BastionSetting.WingburstRadius]=5.5f;
        profile[(int)BastionSetting.WingburstCooldown]=14;
        profile[(int)BastionSetting.MineCount]=8;
        profile[(int)BastionSetting.MineChainBonus]=75;
        profile[(int)BastionSetting.MineBlastRadius]=4;
        profile[(int)BastionSetting.MineLifetime]=35;
        profile[(int)BastionSetting.MineTriggerRange]=18;
        profile[(int)BastionSetting.MineWalkDistance]=24;
        profile[(int)BastionSetting.SkyfallDamage]=750;
        profile[(int)BastionSetting.MissileCapacity]=36;
        profile[(int)BastionSetting.UpgradedMineDeployRadius]=22;
        profile[(int)BastionSetting.UpgradedMineCount]=33;
        profile[(int)BastionSetting.UpgradedMineChainBonus]=100;
        profile[(int)BastionSetting.UpgradedMineBlastRadius]=12;
        profile[(int)BastionSetting.UpgradedWingburstDistance]=50;
        profile[(int)BastionSetting.UpgradedWingburstShots]=48;
        profile[(int)BastionSetting.UpgradedWingburstCooldown]=9;
        profile[(int)BastionSetting.MobileShield]=22;
        profile[(int)BastionSetting.SiegeShield]=62;
        profile[(int)BastionSetting.IdleFabrication]=18;
        profile[(int)BastionSetting.CombatFabrication]=40;
        profile[(int)BastionSetting.AllyKillsReward]=0;
        Check(BastionBalance.Apply(profile),"custom host profile accepted");
        BastionBalanceText.Refresh();
        factory=LanguageAPI.Get("KEYWORD_BASTION_FACTORY");
        Check(factory.Contains("18/min idle")&&factory.Contains("40/min in combat")&&factory.Contains("22% max health mobile / 62% siege"),"popup details refresh with the active host profile");
        Check(!factory.Contains("including ally kills"),"popup respects the host's ally-reward setting");
        Contains("NEEDLE_DESCRIPTION","130%","host primary damage replaces menu default");
        Contains("WINGBURST_DESCRIPTION","36m","host rush range replaces menu default");
        Contains("WINGBURST_DESCRIPTION","40 homing","host missile count replaces menu default");
        Contains("WINGBURST_DESCRIPTION","135% damage each","host micro-missile damage replaces assault menu default");
        Contains("WINGBURST_DESCRIPTION","5.5m radius","host micro-missile blast radius replaces assault menu default");
        Contains("WINGBURST_DESCRIPTION","14s recharge","host recharge replaces skirmish menu default");
        Contains("WINGBURST_DESCRIPTION","Two charges","host profile keeps two utility charges");
        Contains("MINES_DESCRIPTION","8 spider mines","host mine count replaces menu default");
        Contains("MINES_DESCRIPTION","105% damage in 4m","host mark bonus and blast radius refresh actual combined damage");
        Contains("MINES_DESCRIPTION","35s lifetime after landing","custom host lifetime refreshes its description");
        Contains("MINES_DESCRIPTION","within 18m","host detection range refreshes independently of blast radius");
        Contains("MINES_DESCRIPTION","pursue up to 24m","host pursuit range replaces default budget");
        Contains("SATURATION_DESCRIPTION","750%","host ultimate damage replaces menu default");
        Check(BastionBalanceText.UpgradeCards[0,0].Contains("36 ->"),"upgrade cards refresh with active profile");
        Check(BastionBalanceText.UpgradeCards[0,0].Contains("22m")&&BastionBalanceText.UpgradeCards[0,1].Contains("33 mines"),"host mine field and count bonuses replace upgrade-card defaults");
        Check(BastionBalanceText.UpgradeChoice(0,0).Contains("120% per detonation")&&BastionBalanceText.UpgradeChoice(0,1).Contains("12m"),"host mine specialization bonuses refresh both choices");
        Check(BastionBalanceText.UpgradeCards[3,0].Contains("Rush 50m")&&BastionBalanceText.UpgradeCards[3,1].Contains("48 micro-missiles")&&BastionBalanceText.UpgradeCards[3,2].Contains("9s"),"host Rush upgrade values replace defaults in every rank");

        profile[(int)BastionSetting.WingburstDistance]=18;
        profile[(int)BastionSetting.WingburstDamage]=80;
        profile[(int)BastionSetting.WingburstRadius]=3;
        profile[(int)BastionSetting.WingburstCooldown]=12;
        profile[(int)BastionSetting.MineCount]=16;
        profile[(int)BastionSetting.MineLifetime]=45;
        profile[(int)BastionSetting.MineTriggerRange]=12;
        profile[(int)BastionSetting.MineWalkDistance]=16;
        Check(BastionBalance.Apply(profile),"second custom profile accepted");
        BastionBalanceText.Refresh();
        Contains("WINGBURST_DESCRIPTION","18m","repeated host profile refresh replaces previous range");
        Contains("WINGBURST_DESCRIPTION","80% damage each","repeated host profile replaces previous micro-missile damage");
        Contains("WINGBURST_DESCRIPTION","3m radius","repeated host profile replaces previous micro-missile radius");
        Contains("WINGBURST_DESCRIPTION","12s recharge","repeated host profile replaces previous utility recharge");
        Contains("MINES_DESCRIPTION","16 spider mines","repeated host profile refresh replaces previous count");
        Contains("MINES_DESCRIPTION","45s lifetime after landing","second host profile replaces the previous lifetime text");
        Contains("MINES_DESCRIPTION","within 12m","repeated host profile replaces prior detection range");
        Contains("MINES_DESCRIPTION","pursue up to 16m","repeated host profile replaces prior pursuit range");
        for(int i=0;i<50;i++)BastionBalanceText.Refresh();
        Check(LanguageAPI.OverlayCount==2,"repeated refreshes do not accumulate or erase overlays");
        Check(LanguageAPI.Get("UNRELATED_BASE")=="untouched base"&&LanguageAPI.Get("UNRELATED_OVERLAY")=="untouched overlay","refresh preserves unrelated additions and overlays");

        BastionBalance.Reset();BastionBalanceText.Refresh();
        Contains("WINGBURST_DESCRIPTION","15m","run end restores default utility range");
        Contains("WINGBURST_DESCRIPTION","40% damage each","run end restores skirmish default micro-missile damage");
        Contains("WINGBURST_DESCRIPTION","4m radius","run end restores skirmish default micro-missile radius");
        Contains("WINGBURST_DESCRIPTION","10s recharge","run end restores default ten-second utility recharge");
        Contains("WINGBURST_DESCRIPTION","Two charges","run end preserves two default utility charges");
        Contains("MINES_DESCRIPTION","24 spider mines","run end restores default utility count");
        Contains("MINES_DESCRIPTION","60s lifetime after landing","run end restores default grounded lifetime text");
        Contains("MINES_DESCRIPTION","within 30m","run end restores default detection range");
        Contains("MINES_DESCRIPTION","pursue up to 50m","run end restores default pursuit budget");
        Contains("MINES_DESCRIPTION","90% damage in 8m","run end restores default charged detonation values");
        Contains("NEEDLE_DESCRIPTION","80%","run end restores default primary description");
        Contains("SATURATION_DESCRIPTION","500%","run end restores default ultimate description");
        Check(LanguageAPI.OverlayCount==2,"reset retains exactly one owned overlay");
        external.Remove();
        Check(LanguageAPI.OverlayCount==1&&LanguageAPI.Get("UNRELATED_OVERLAY")=="UNRELATED_OVERLAY","external overlay remains independently removable");
        Check(Text("WINGBURST_NAME")=="Heavenfall Rush","removing unrelated overlay leaves Bastion text intact");
        Check(LanguageAPI.Get("KEYWORD_BASTION_FACTORY").Contains("17/min idle"),"keyword details restore defaults when the run ends");
        var preview=new {passiveKeyword=LanguageAPI.Get("KEYWORD_BASTION_FACTORY"),skills=new[]{"PASSIVE","NEEDLE","BOOST","WINGBURST","REDLINE","LANCE","PROTOCOL","MINES","SATURATION","DEPLOY","PACK"}.Select(stem=>new{stem,name=Text(stem+"_NAME"),description=Text(stem+"_DESCRIPTION"),keywords=BastionBalanceText.Keywords(stem).Where(k=>k.StartsWith("KEYWORD_BASTION_",StringComparison.Ordinal)).Select(LanguageAPI.Get)})};
        File.WriteAllText(Path.Combine(root,".build","skill-text-preview.json"),System.Text.Json.JsonSerializer.Serialize(preview,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS: {checks} startup, both-form language, first-write-wins, host-profile refresh and overlay-isolation checks.");
    }
}
