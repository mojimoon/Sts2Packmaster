using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models.Cards;
using Sts2Packmaster.Lib.Api;

namespace Sts2Packmaster.VanillaPacks;

[ModInitializer(nameof(Init))]
public static class VanillaPacksEntry
{
	public static PackCharacterRegistration? Registration { get; private set; }

	private static readonly string[] VanillaLanguages =
	{
		"deu", "eng", "esp", "fra", "ind", "ita", "jpn", "kor", "pol", "ptb", "rus", "spa", "tha", "tur", "zhs", "zht",
	};

	// Every "characters" key the game reads for a character (see CharacterModel / events).
	private static readonly Dictionary<string, string> CharacterEn = new()
	{
		["VANILLA_SLINGER.title"] = "The Vanilla Slinger",
		["VANILLA_SLINGER.titleObject"] = "The Vanilla Slinger",
		["VANILLA_SLINGER.description"] = "Packmaster demo character.\nBuilds a deck from card packs of vanilla cards.",
		["VANILLA_SLINGER.pronounSubject"] = "they",
		["VANILLA_SLINGER.pronounObject"] = "them",
		["VANILLA_SLINGER.pronounPossessive"] = "theirs",
		["VANILLA_SLINGER.possessiveAdjective"] = "their",
		["VANILLA_SLINGER.bestiaryQuote"] = "Not in any of my packs yet.",
		["VANILLA_SLINGER.aromaPrinciple"] = "[sine]Every pack has its place.[/sine]",
		["VANILLA_SLINGER.goldMonologue"] = "[sine]Enough gold for a few more packs...[/sine]",
		["VANILLA_SLINGER.eventDeathPrevention"] = "Not before I open my next pack.",
		["VANILLA_SLINGER.cardsModifierTitle"] = "Vanilla Slinger Cards",
		["VANILLA_SLINGER.cardsModifierDescription"] = "Vanilla Slinger cards will now appear in rewards and shops.",
		["VANILLA_SLINGER.banter.alive.endTurnPing"] = "Hurry up.",
		["VANILLA_SLINGER.banter.dead.endTurnPing"] = "...",
	};

	private static readonly Dictionary<string, string> CharacterZhs = new()
	{
		["VANILLA_SLINGER.title"] = "原版卡包师",
		["VANILLA_SLINGER.titleObject"] = "原版卡包师",
		["VANILLA_SLINGER.description"] = "卡包大师演示角色。\n用原版卡组成的卡包构筑牌组。",
		["VANILLA_SLINGER.pronounSubject"] = "他",
		["VANILLA_SLINGER.pronounObject"] = "他",
		["VANILLA_SLINGER.pronounPossessive"] = "他的",
		["VANILLA_SLINGER.possessiveAdjective"] = "他的",
		["VANILLA_SLINGER.bestiaryQuote"] = "我的卡包里还没有它。",
		["VANILLA_SLINGER.aromaPrinciple"] = "[sine]每个卡包都有它的位置。[/sine]",
		["VANILLA_SLINGER.goldMonologue"] = "[sine]够再买几包卡了……[/sine]",
		["VANILLA_SLINGER.eventDeathPrevention"] = "下一包还没拆呢。",
		["VANILLA_SLINGER.cardsModifierTitle"] = "原版卡包师卡牌",
		["VANILLA_SLINGER.cardsModifierDescription"] = "原版卡包师的卡牌现在会出现在奖励和商店中。",
		["VANILLA_SLINGER.banter.alive.endTurnPing"] = "快点。",
		["VANILLA_SLINGER.banter.dead.endTurnPing"] = "……",
	};

	public static void Init()
	{
		// Mod-side localization (cards + previews): zh + en translations; every other vanilla
		// language falls back to the English entries so no key is ever missing.
		PackmasterApi.AddLoc("cards", "zhs", VanillaPackLoc.CardsZhs);
		PackmasterApi.AddLoc("characters", "zhs", CharacterZhs);
		foreach (var language in VanillaLanguages)
		{
			if (language != "zhs")
			{
				PackmasterApi.AddLoc("cards", language, VanillaPackLoc.CardsEn);
				PackmasterApi.AddLoc("characters", language, CharacterEn);
			}
		}

		Registration = new PackCharacterRegistration
		{
			CharacterType = typeof(VanillaSlinger),
			// Vanilla cards referenced directly (they keep their own frame/art), 10-14 cards each with
			// 2+ Attacks/Skills/Powers and 2+ Commons/Uncommons/Rares, so no roll ever runs dry.
			Packs = new List<PackDefinition>
			{
				Pack("pain", typeof(PainPackPreview), typeof(CrimsonMantle), Summary(4, 1, 2, 3, 4, PackTags.SelfDamage, PackTags.Strength),
					typeof(Breakthrough), typeof(BloodWall), typeof(Hemokinesis), typeof(Spite), typeof(Bloodletting),
					typeof(Rupture), typeof(Inferno), typeof(TearAsunder), typeof(Offering), typeof(Brand), typeof(CrimsonMantle)),
				Pack("iron_wall", typeof(IronWallPackPreview), typeof(Barricade), Summary(2, 5, 1, 3, 4, PackTags.Block),
					typeof(BodySlam), typeof(IronWave), typeof(ShrugItOff), typeof(TrueGrit), typeof(FlameBarrier),
					typeof(Colossus), typeof(StoneArmor), typeof(Impervious), typeof(Barricade), typeof(Juggernaut), typeof(Unmovable)),
				Pack("doom", typeof(DoomPackPreview), typeof(EndOfDays), Summary(4, 2, 2, 2, 5, PackTags.Doom, PackTags.Debuffs),
					typeof(BlightStrike), typeof(Scourge), typeof(NegativePulse), typeof(NoEscape), typeof(Deathbringer), typeof(Shroud),
					typeof(Countdown), typeof(TimesUp), typeof(Misery), typeof(EndOfDays), typeof(Oblivion), typeof(ReaperForm)),
				Pack("bone_friends", typeof(BoneFriendsPackPreview), typeof(NecroMastery), Summary(3, 3, 2, 3, 4, PackTags.Summon),
					typeof(Poke), typeof(Snap), typeof(Flatten), typeof(Afterlife), typeof(PullAggro), typeof(SicEm), typeof(HighFive),
					typeof(Spur), typeof(Calcify), typeof(Friendship), typeof(Squeeze), typeof(Reanimate), typeof(NecroMastery)),
				Pack("poison", typeof(PoisonPackPreview), typeof(Envenom), Summary(3, 1, 2, 1, 5, PackTags.Poison, PackTags.Debuffs),
					typeof(PoisonedStab), typeof(DeadlyPoison), typeof(Snakebite), typeof(Strangle), typeof(BouncingFlask), typeof(Haze),
					typeof(BubbleBubble), typeof(NoxiousFumes), typeof(Accelerant), typeof(CorrosiveWave), typeof(Outbreak), typeof(Envenom)),
				Pack("phantom_blades", typeof(PhantomBladesPackPreview), typeof(PhantomBlades), Summary(4, 2, 1, 3, 3, PackTags.Shivs, PackTags.Attacks),
					typeof(LeadingStrike), typeof(Finisher), typeof(BladeDance), typeof(CloakAndDagger), typeof(HiddenDaggers), typeof(UpMySleeve),
					typeof(BladeOfInk), typeof(StormOfSteel), typeof(KnifeTrap), typeof(Accuracy), typeof(InfiniteBlades), typeof(PhantomBlades), typeof(FanOfKnives)),
				Pack("chaos", typeof(ChaosPackPreview), typeof(BundleOfJoy), Summary(2, 2, 4, 3, 3, PackTags.Generation),
					typeof(CollisionCourse), typeof(Begone), typeof(Quasar), typeof(ManifestAuthority), typeof(JackOfAllTrades), typeof(Discovery),
					typeof(SpectrumShift), typeof(PillarOfCreation), typeof(BundleOfJoy), typeof(Arsenal), typeof(Jackpot), typeof(Calamity)),
				Pack("kingdom_arms", typeof(KingdomArmsPackPreview), typeof(SwordSage), Summary(4, 2, 1, 2, 4, PackTags.Forge),
					typeof(WroughtInWar), typeof(RefineBlade), typeof(SpoilsOfBattle), typeof(Conqueror), typeof(SummonForth), typeof(Bulwark),
					typeof(Furnace), typeof(Parry), typeof(BeatIntoShape), typeof(TheSmith), typeof(SeekingEdge), typeof(SwordSage)),
				Pack("starlight", typeof(StarlightPackPreview), typeof(Genesis), Summary(3, 2, 2, 2, 4, PackTags.Stars),
					typeof(SolarStrike), typeof(GatherLight), typeof(Glow), typeof(HiddenCache), typeof(ShiningStrike), typeof(Radiate),
					typeof(RoyalGamble), typeof(BlackHole), typeof(ChildOfTheStars), typeof(Genesis), typeof(SevenStars)),
				Pack("lightning", typeof(LightningPackPreview), typeof(Thunder), Summary(4, 1, 2, 3, 3, PackTags.Orbs),
					typeof(BallLightning), typeof(Barrage), typeof(LightningRod), typeof(TeslaCoil), typeof(Tempest), typeof(Fusion),
					typeof(Storm), typeof(Thunder), typeof(Capacitor), typeof(Voltaic), typeof(MeteorStrike)),
				Pack("frost", typeof(FrostPackPreview), typeof(Glacier), Summary(2, 5, 2, 2, 4, PackTags.Orbs, PackTags.Block),
					typeof(ColdSnap), typeof(Coolheaded), typeof(Leap), typeof(Refract), typeof(Glacier), typeof(Chill),
					typeof(Coolant), typeof(Hailstorm), typeof(Loop), typeof(IceLance), typeof(Defragment)),
			},
			// Not in any pack (STS1 basics): starters, plus Ancient cards so Dusty Tome has something to give.
			ExtraPoolCardTypes = new[]
			{
				typeof(PackStrike), typeof(PackDefend), typeof(Bash), typeof(Neutralize),
				typeof(Corruption), typeof(WraithForm), typeof(BiasedCognition), typeof(ForbiddenGrimoire), typeof(TheSealedThrone),
			},
			// STS1 default is 7 packs: here 4 random + 3 drafted.
			DefaultSlots = new[] { "random", "random", "random", "random", "choice", "choice", "choice" },
		};

		PackmasterApi.RegisterCharacter(Registration);
		new Harmony("sts2.moon.vanillapacks").PatchAll(typeof(VanillaPacksEntry).Assembly);
		Log.Info("[VanillaPacks] initialized.");
		if (PackmasterAutoTest.Requested)
		{
			TaskHelper.RunSafely(PackmasterAutoTest.Run());
		}
	}

	private static PackDefinition Pack(string id, Type preview, Type cover, PackSummary summary, params Type[] cards)
	{
		var entry = StringHelper.Slugify(preview.Name);
		return new PackDefinition
		{
			Id = id,
			NameKey = $"cards:{entry}.title",
			DescriptionKey = $"cards:{entry}.description",
			Author = "Moon",
			CardTypes = cards,
			PreviewCardType = preview,
			CoverCardType = cover,
			Summary = summary,
		};
	}

	private static PackSummary Summary(int offense, int defense, int support, int frontload, int scaling, params string[] tags) => new()
	{
		Offense = offense,
		Defense = defense,
		Support = support,
		Frontload = frontload,
		Scaling = scaling,
		Tags = tags,
	};
}
