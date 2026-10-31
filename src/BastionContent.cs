using System;
using System.IO;
using System.Linq;
using System.Reflection;
using EntityStates;
using R2API;
using RoR2;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.AddressableAssets;
namespace IvoryBastion
{
    public sealed class BastionSkillDef:SkillDef
    {
        public BastionAction Action;
        public override bool IsReady(GenericSkill s)
        {
            var c=s.characterBody?s.characterBody.GetComponent<BastionController>():null;
            return c&&(c.Ready(Action)||c.Requests?.AllowInput(Action)==true);
        }
        public override bool CanExecute(GenericSkill s)=>IsReady(s)&&base.CanExecute(s);
        public override void OnFixedUpdate(GenericSkill s,float dt)
        {
            var c=s.characterBody?s.characterBody.GetComponent<BastionController>():null;if(!c)return;
            if(Action==BastionAction.Boost)s.OverrideMaxStock(c.Bank.Thruster.Capacity);
            s.stock=Action==BastionAction.Laser?(IsReady(s)?1:0):c.Bank.Stock(Action);
            if(s.stock==0&&c.Requests?.AllowInput(Action)==true)s.stock=1;
            s.rechargeStopwatch=Mathf.Max(0,s.CalculateFinalRechargeInterval()-c.Cooldown(Action));
        }
        public override int GetMaxStock(GenericSkill s)=>Action==BastionAction.Boost?(s.characterBody?s.characterBody.GetComponent<BastionController>()?.Bank.ThrusterBaseCapacity??3:3):base.GetMaxStock(s);
        public override float GetRechargeInterval(GenericSkill s)
        {
            var c=s.characterBody?s.characterBody.GetComponent<BastionController>():null;
            return c?c.Interval(Action):baseRechargeInterval;
        }
    }
    internal static class BastionContent
    {
        internal static GameObject BodyPrefab;
        internal static readonly GameObject[] UnitMasters=new GameObject[4];
        internal static Sprite Portrait;
        internal static BuffDef Anchored,Overcharged,Redline,FlightShield,AegisBarrier;
        internal static readonly SkillDef[] Mobile=new SkillDef[4],Siege=new SkillDef[4];
        internal static T Load<T>(string key) where T:UnityEngine.Object=>Addressables.LoadAssetAsync<T>(key).WaitForCompletion();
        internal static void Create()
        {
            Portrait=Icon("portrait");Anchored=Buff("Anchored","buff_deployed");Overcharged=Buff("Overcharged","buff_aegis");Overcharged.buffColor=new Color(1,.82f,.12f);Redline=Buff("Blue Shift","buff_shift");
            FlightShield=Buff("Seraph Shield","buff_seraphshield");FlightShield.buffColor=Color.white;
            AegisBarrier=Buff("Aegis Barrier","buff_aegisbarrier");AegisBarrier.buffColor=Color.white;
            LanguageAPI.Add(AegisBarrier.name,"Aegis Barrier");
            LanguageAPI.Add(AegisBarrier.name+"_DESCRIPTION","Independent, decaying Aegis barrier. Absorbs damage before Seraph Shield. Hits stopped by Aegis do not delay Seraph recharge.");
            LanguageAPI.Add(FlightShield.name,"Seraph Shield");
            LanguageAPI.Add(FlightShield.name+"_DESCRIPTION","Independent shield with its own charge, damage reduction and recharge. Barrier absorbs damage first. Item shields and healing do not refill it.");
            Language();
            ContentAddition.AddEntityState<BastionBoost>(out _);ContentAddition.AddEntityState<BastionMain>(out _);ContentAddition.AddEntityState<BastionPrimary>(out _);
            ContentAddition.AddEntityState<BastionShot>(out _);ContentAddition.AddEntityState<BastionSecondary>(out _);
            ContentAddition.AddEntityState<BastionSpecial>(out _);ContentAddition.AddEntityState<BastionTransform>(out _);
            ContentAddition.AddEntityState<BastionPurchase>(out _);ContentAddition.AddEntityState<BastionDeath>(out _);
            ContentAddition.AddEntityState<BastionRequestBatch>(out _);
            ContentAddition.AddEntityState<BastionUtilityState>(out _);
            BodyPrefab=Load<GameObject>("RoR2/Base/Huntress/HuntressBody.prefab").InstantiateClone("IvoryBastionBody",true);
            var b=BodyPrefab.GetComponent<CharacterBody>();b.baseNameToken="BASTION_NAME";b.subtitleNameToken="BASTION_SUBTITLE";b.portraitIcon=Portrait.texture;
            b.bodyColor=new Color(.8f,.9f,1f);b.baseMaxHealth=100;b.levelMaxHealth=30;b.baseArmor=0;b.baseRegen=1;b.levelRegen=.2f;
            b.baseDamage=12;b.levelDamage=2.4f;b.baseMoveSpeed=14;b.sprintingSpeedMultiplier=20f/14;b.baseJumpPower=12;b.baseJumpCount=1;b.autoCalculateLevelStats=true;
            foreach(var f in BodyPrefab.GetComponentsInChildren<FootstepHandler>(true))f.enabled=false;
            var oldTracker=BodyPrefab.GetComponent<HuntressTracker>();if(oldTracker)UnityEngine.Object.DestroyImmediate(oldTracker);
            var death=BodyPrefab.GetComponent<CharacterDeathBehavior>();if(death)death.deathState=BastionStateType.For<BastionDeath>();
            // The hurtbox is body-owned so its dimensions do not inherit model
            // scaling. Do not leave a detached corpse with cached dead hurtboxes.
            var locator=BodyPrefab.GetComponent<ModelLocator>();locator.dontReleaseModelOnDeath=true;locator.preserveModel=false;locator.noCorpse=true;
            BodyPrefab.AddComponent<BastionController>();BodyPrefab.AddComponent<BastionLaserTracker>();BodyPrefab.AddComponent<BastionNeedleTracker>();BodyPrefab.AddComponent<BastionEngine>();BodyPrefab.AddComponent<BastionAegisVisual>();BodyPrefab.AddComponent<BastionShieldVisual>();BodyPrefab.AddComponent<BastionThrusterVisual>();BodyPrefab.AddComponent<BastionSiegeThrusters>();BastionModel.Build(b.modelLocator?b.modelLocator.modelTransform.gameObject:BodyPrefab.GetComponent<ModelLocator>().modelTransform.gameObject,b);
            foreach(var machine in BodyPrefab.GetComponents<EntityStateMachine>())if(machine.customName=="Body")machine.mainStateType=BastionStateType.For<BastionMain>();
            var boostMachine=BodyPrefab.AddComponent<EntityStateMachine>();boostMachine.customName="Thruster";
            boostMachine.initialStateType=boostMachine.mainStateType=new SerializableEntityStateType(typeof(Idle));
            var requestMachine=BodyPrefab.AddComponent<EntityStateMachine>();requestMachine.customName="BastionRequests";
            requestMachine.initialStateType=requestMachine.mainStateType=new SerializableEntityStateType(typeof(Idle));
            BodyPrefab.AddComponent<BastionRequests>();BodyPrefab.AddComponent<BastionUpgradeSync>();
            BodyPrefab.AddComponent<BastionUtility>();BodyPrefab.AddComponent<BastionTransformInput>();BodyPrefab.AddComponent<BastionUtilityVisual>();
            var utilityMachine=BodyPrefab.AddComponent<EntityStateMachine>();utilityMachine.customName="SeraphUtility";
            utilityMachine.initialStateType=utilityMachine.mainStateType=BastionStateType.For<Idle>();
            var transformMachine=BodyPrefab.AddComponent<EntityStateMachine>();transformMachine.customName="SeraphTransform";
            transformMachine.initialStateType=transformMachine.mainStateType=BastionStateType.For<Idle>();
            BodyPrefab.AddComponent<BastionBalanceBody>();
            var network=BodyPrefab.GetComponent<NetworkStateMachine>();
            var machines=typeof(NetworkStateMachine).GetField("stateMachines",BindingFlags.NonPublic|BindingFlags.Instance);
            machines.SetValue(network,((EntityStateMachine[])machines.GetValue(network)).Concat(new[]{boostMachine,requestMachine,utilityMachine,transformMachine}).ToArray());
            Mobile[0]=Skill<BastionPrimary>(BastionAction.Pistol,"NEEDLE","pistol",0,"Weapon",false);
            Mobile[1]=Skill<BastionBoost>(BastionAction.Boost,"BOOST","slip",2,"Thruster",true);
            Mobile[2]=UtilitySkill("WINGBURST","wingburst",10,2);
            Mobile[3]=Skill<BastionSpecial>(BastionAction.Redline,"REDLINE","redline",40,"Weapon",true);
            Siege[0]=Skill<BastionPrimary>(BastionAction.Laser,"LANCE","laser",5,"Weapon",false);
            Siege[1]=Skill<BastionSecondary>(BastionAction.Protocol,"PROTOCOL","protocol",18,"Weapon",true);
            Siege[2]=UtilitySkill("MINES","mines",24);
            Siege[3]=Skill<BastionSpecial>(BastionAction.Saturation,"SATURATION","saturation",60,"Weapon",true);
            var sl=BodyPrefab.GetComponent<SkillLocator>();Assign(sl.primary,Mobile[0]);Assign(sl.secondary,Mobile[1]);Assign(sl.utility,Mobile[2]);Assign(sl.special,Mobile[3]);
            sl.passiveSkill.enabled=true;sl.passiveSkill.skillNameToken="BASTION_PASSIVE_NAME";sl.passiveSkill.skillDescriptionToken="BASTION_PASSIVE_DESCRIPTION";sl.passiveSkill.keywordToken="KEYWORD_BASTION_FACTORY";sl.passiveSkill.icon=Icon("factory");
            ContentAddition.AddBody(BodyPrefab);
            var master=LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterMasters/HuntressMonsterMaster").InstantiateClone("IvoryBastionMonsterMaster",true);
            master.GetComponent<CharacterMaster>().bodyPrefab=BodyPrefab;ContentAddition.AddMaster(master);
            var display=Load<GameObject>("RoR2/Base/Huntress/HuntressDisplay.prefab").InstantiateClone("IvoryBastionDisplay",false);
            var cm=display.GetComponentInChildren<CharacterModel>();BastionModel.Build(cm?cm.gameObject:display);
            (cm?cm.gameObject:display).AddComponent<BastionDisplayShowcase>();
            var sd=ScriptableObject.CreateInstance<SurvivorDef>();sd.cachedName="IvoryBastion";sd.bodyPrefab=BodyPrefab;sd.displayPrefab=display;
            sd.descriptionToken="BASTION_DESCRIPTION";sd.outroFlavorToken="BASTION_OUTRO";sd.mainEndingEscapeFailureFlavorToken="BASTION_FAILURE";sd.primaryColor=b.bodyColor;sd.desiredSortPosition=103;
            ContentAddition.AddSurvivorDef(sd);CreateUnits();
        }
        private static void CreateUnits()
        {
            var originalMaster=LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterMasters/Drone1Master");
            for(int i=0;i<4;i++)
            {
                var master=originalMaster.InstantiateClone("Bastion"+(BastionUnitKind)i+"Master",true);
                var mb=originalMaster.GetComponent<CharacterMaster>().bodyPrefab.InstantiateClone("Bastion"+(BastionUnitKind)i+"Body",true);
                var b=mb.GetComponent<CharacterBody>();b.baseNameToken="BASTION_UNIT_"+i;b.baseMaxHealth=400;b.levelMaxHealth=0;b.baseDamage=9;b.levelDamage=0;b.baseArmor=BastionUnitRules.Armor((BastionUnitKind)i);b.levelArmor=0;
                var u=mb.AddComponent<BastionUnit>();u.Kind=(BastionUnitKind)i;mb.AddComponent<BastionBalanceBody>();
                foreach(var driver in master.GetComponents<RoR2.CharacterAI.AISkillDriver>())driver.skillSlot=SkillSlot.None;
                master.GetComponent<CharacterMaster>().bodyPrefab=mb;UnitMasters[i]=master;ContentAddition.AddBody(mb);ContentAddition.AddMaster(master);
            }
        }
        private static SkillDef Skill<T>(BastionAction action,string token,string icon,float cooldown,string machine,bool press) where T:EntityState
        {
            var s=ScriptableObject.CreateInstance<BastionSkillDef>();s.Action=action;((ScriptableObject)s).name=s.skillName="Bastion"+token;
            s.skillNameToken="BASTION_"+token+"_NAME";s.skillDescriptionToken="BASTION_"+token+"_DESCRIPTION";s.icon=Icon(icon);
            s.activationState=BastionStateType.For<T>();s.activationStateMachineName=machine;
            // Primary owns its tap/hold timer and firing loop. Repeated held-input activation
            // must not interrupt it; secondary, special and transformation still can.
            s.interruptPriority=action==BastionAction.Pistol||action==BastionAction.Laser?InterruptPriority.Any:InterruptPriority.Skill;
            s.baseRechargeInterval=cooldown;s.baseMaxStock=action==BastionAction.Boost?3:1;s.requiredStock=0;s.stockToConsume=0;s.rechargeStock=1;
            s.fullRestockOnAssign=false;s.mustKeyPress=press;s.cancelSprintingOnActivation=false;s.canceledFromSprinting=false;s.isCombatSkill=action!=BastionAction.Transform;
            s.keywordTokens=BastionBalanceText.Keywords(token);ContentAddition.AddSkillDef(s);return s;
        }
        private static SkillDef UtilitySkill(string token,string icon,float cooldown,int stock=1)
        {
            var s=ScriptableObject.CreateInstance<BastionUtilitySkillDef>();((ScriptableObject)s).name=s.skillName="Bastion"+token;
            s.skillNameToken="BASTION_"+token+"_NAME";s.skillDescriptionToken="BASTION_"+token+"_DESCRIPTION";s.icon=Icon(icon);
            s.activationState=BastionStateType.For<BastionUtilityState>();s.activationStateMachineName="SeraphUtility";
            s.interruptPriority=InterruptPriority.Skill;s.baseRechargeInterval=cooldown;s.baseMaxStock=stock;s.requiredStock=0;s.stockToConsume=0;s.rechargeStock=1;
            s.fullRestockOnAssign=false;s.mustKeyPress=true;s.cancelSprintingOnActivation=false;s.canceledFromSprinting=false;s.isCombatSkill=true;
            s.keywordTokens=BastionBalanceText.Keywords(token);ContentAddition.AddSkillDef(s);return s;
        }
        private static void Assign(GenericSkill slot,SkillDef skill)
        {
            var f=ScriptableObject.CreateInstance<SkillFamily>();((ScriptableObject)f).name=skill.skillName+"Family";f.variants=new[]{new SkillFamily.Variant{skillDef=skill,viewableNode=new ViewablesCatalog.Node(skill.skillName,false,null)}};
            typeof(GenericSkill).GetField("_skillFamily",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(slot,f);ContentAddition.AddSkillFamily(f);
        }
        private static BuffDef Buff(string name,string icon){var b=ScriptableObject.CreateInstance<BuffDef>();b.name="Bastion"+name;b.buffColor=Color.white;b.iconSprite=Icon(icon);ContentAddition.AddBuffDef(b);return b;}
        private static readonly System.Collections.Generic.Dictionary<string,Sprite> icons=new System.Collections.Generic.Dictionary<string,Sprite>();
        internal static Sprite Icon(string name)
        {
            if(icons.TryGetValue(name,out var cached))return cached;
            using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("IvoryBastion.assets."+name+".png"))
            {
                if(s==null)throw new FileNotFoundException(name);var a=new byte[s.Length];s.Read(a,0,a.Length);
                // Mipmapped artwork stays stable when the native HUD tilts and
                // minifies it. Animation atlases keep their existing filtering
                // so mip generation cannot leak neighboring effect frames.
                bool mipmaps=!name.StartsWith("missile_")&&!name.StartsWith("thruster_");
                var t=new Texture2D(2,2,TextureFormat.RGBA32,mipmaps){filterMode=mipmaps?FilterMode.Trilinear:FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                ImageConversion.LoadImage(t,a);if(mipmaps)t.anisoLevel=4;
                var sprite=Sprite.Create(t,new Rect(0,0,t.width,t.height),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
                t.Apply(mipmaps,true);icons[name]=sprite;return sprite;
            }
        }
        private static void Language()
        {
            LanguageAPI.Add("BASTION_NAME","Ivory Bastion");LanguageAPI.Add("BASTION_SUBTITLE","The Unfolding Arsenal");
            LanguageAPI.Add("BASTION_DESCRIPTION","A small aerial skirmisher unfolds into a missile fortress.\n\n< ! Mobile: rapid pistols, held Thruster Boost, Heavenfall Rush micro-missile dashes and Blue Shift overdrive.\n\n< ! Siege: laser cannon, homing missiles, walking spider mines and Skyfall warheads. Aegis protects and accelerates missile reload.\n\n< ! C switches forms; Shift fires your current form's utility. Both utilities recharge independently in the background.\n\n< ! Produce Fabrication while deployed and the run timer advances. Buy lasting upgrades through the Engineering panel.");
            LanguageAPI.Add("BASTION_OUTRO","...and so she left, carrying an army within.");LanguageAPI.Add("BASTION_FAILURE","...and so she vanished, the last battery falling silent.");
            // Safe fallback text; the owned overlay below supplies current,
            // host-configured values without duplicating balance defaults here.
            LanguageAPI.Add("BASTION_PASSIVE_NAME","Autonomous Foundry");LanguageAPI.Add("BASTION_PASSIVE_DESCRIPTION",BastionBalanceText.FactorySummary);
            LanguageAPI.Add("KEYWORD_BASTION_FACTORY","<style=cKeywordName>Engineering</style><style=cSub>Press V (default) to open Engineering. While deployed, spend Fabrication on ranked Siege, Defense and Mobile upgrade paths with unique mastery bonuses. Purchases last the run.</style>\n\n<style=cKeywordName>Fabrication</style><style=cSub>Earn currency while deployed. Combat and nearby kills increase income. Siege also reloads missiles and deploys drones.</style>\n\n<style=cKeywordName>Seraph Shield</style><style=cSub>An independent shield protects both forms. It scales with maximum health, reduces damage to its charge and recharges when protected. Barrier protects recharge.</style>");
            Token("NEEDLE","Needlefire","Agile. Fire aim-assisted rounds that replenish missile stock on damaging hits.");
            Token("BOOST","Thruster Boost","Hold secondary to boost while steering and firing. Release to recharge fuel.");
            Token("DEPLOY","Deploy Fortress","Transform into an immobile fortress near solid ground. Aim freely while deployed.");
            Token("PACK","Pack Up","Return to mobile form.");
            Token("REDLINE","Blue Shift","Temporarily increase attack speed, flight speed and fuel recharge. Restore fuel on activation.");
            Token("LANCE","Ion Lance / Missile Battery","Tap to fire an auto-targeted piercing beam and impact blast. Hold to fire homing missiles, including periodic heavy missiles.");
            Token("PROTOCOL","Aegis Protocol","Gain barrier, damage reduction, healing over time and faster missile reload. Stacks with Seraph Shield.");
            Token("SATURATION","Skyfall Barrage","Launch independently supplied sky-diving warheads at nearby enemies. Hits detonate attached Widow Array mines.");
            for(int i=0;i<4;i++)LanguageAPI.Add("BASTION_UNIT_"+i,new[]{"Bulwark Sentinel","Needle Gunner","Arc Interceptor","Field Repairer"}[i]);
            // Register both forms before the survivor menu can request their text.
            BastionBalanceText.Refresh();
        }
        private static void Token(string token,string name,string description){LanguageAPI.Add("BASTION_"+token+"_NAME",name);LanguageAPI.Add("BASTION_"+token+"_DESCRIPTION",description);}
    }
}
