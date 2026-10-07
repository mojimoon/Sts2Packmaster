using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Modding;
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
			Packs = new List<PackDefinition>
			{
				new PackDefinition
				{
					Id = "strikes",
					NameKey = "cards:PACK_STRIKES_PREVIEW.title",
					DescriptionKey = "cards:pack_strikes_preview.description",
					Author = "Moon",
					CardTypes = new List<Type>
					{
						typeof(PackStrike), typeof(PackTwinStrike), typeof(PackPommelStrike),
						typeof(PackIronWave), typeof(PackAnger), typeof(PackThunderclap), typeof(PackBash),
					},
					PreviewCardType = typeof(PackStrikesPreview),
				},
				new PackDefinition
				{
					Id = "defends",
					NameKey = "cards:PACK_DEFENDS_PREVIEW.title",
					DescriptionKey = "cards:pack_defends_preview.description",
					Author = "Moon",
					CardTypes = new List<Type>
					{
						typeof(PackDefend), typeof(PackShrugItOff), typeof(PackBackflip),
						typeof(PackArmaments), typeof(PackFlameBarrier), typeof(PackSecondWind), typeof(PackEscapePlan),
					},
					PreviewCardType = typeof(PackDefendsPreview),
				},
				new PackDefinition
				{
					Id = "powers",
					NameKey = "cards:PACK_POWERS_PREVIEW.title",
					DescriptionKey = "cards:pack_powers_preview.description",
					Author = "Moon",
					CardTypes = new List<Type>
					{
						typeof(PackInflame), typeof(PackRupture), typeof(PackBattleTrance),
						typeof(PackBloodletting), typeof(PackJuggernaut), typeof(PackDemonForm), typeof(PackCorruption),
					},
					PreviewCardType = typeof(PackPowersPreview),
				},
				new PackDefinition
				{
					Id = "tricks",
					NameKey = "cards:PACK_TRICKS_PREVIEW.title",
					DescriptionKey = "cards:pack_tricks_preview.description",
					Author = "Moon",
					CardTypes = new List<Type>
					{
						typeof(PackNeutralize), typeof(PackPrepared), typeof(PackAcrobatics),
						typeof(PackExpertise), typeof(PackReflex), typeof(PackHeadbutt),
					},
					PreviewCardType = typeof(PackTricksPreview),
				},
			},
			ExtraPoolCardTypes = Array.Empty<Type>(),
			DefaultSlots = new[] { "strikes", "random", "choice", "choice", "random" },
		};

		PackmasterApi.RegisterCharacter(Registration);
		new Harmony("sts2.moon.vanillapacks").PatchAll(typeof(VanillaPacksEntry).Assembly);
		Log.Info("[VanillaPacks] initialized.");
		if (PackmasterAutoTest.Requested)
		{
			TaskHelper.RunSafely(PackmasterAutoTest.Run());
		}
	}
}
