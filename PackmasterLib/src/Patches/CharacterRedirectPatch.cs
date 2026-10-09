using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using Sts2Packmaster.Lib.Api;

namespace Sts2Packmaster.Lib.Patches;

/// <summary>
/// CharacterModel builds many asset paths from the character's Id with non-virtual properties.
/// For RedirectedCharacterModel instances those paths are rewritten to point at the source
/// character's assets, so a mod character without art reuses vanilla visuals end to end.
/// </summary>
[HarmonyPatch]
internal static class CharacterRedirectPatch
{
	private static readonly string[] RedirectedProperties =
	{
		"VisualsPath",                   // private
		"IconTexturePath",               // private
		"IconOutlineTexturePath",        // private
		"ArmPointingTexturePath",        // private
		"ArmRockTexturePath",            // private
		"ArmPaperTexturePath",           // private
		"ArmScissorsTexturePath",        // private
		"CharacterSelectBg",             // public non-virtual
		"CharacterSelectTransitionPath", // public non-virtual
		"TrailPath",                     // public non-virtual
		"EnergyCounterPath",             // public non-virtual
		"RestSiteAnimPath",              // public non-virtual
		"MerchantAnimPath",              // public non-virtual
		"AttackSfx",                     // public non-virtual
		"CastSfx",                       // public non-virtual
		"DeathSfx",                      // public non-virtual
	};

	[HarmonyPatch]
	private static IEnumerable<MethodBase> TargetMethods()
	{
		foreach (var name in RedirectedProperties)
		{
			var getter = AccessTools.PropertyGetter(typeof(CharacterModel), name);
			if (getter != null)
			{
				yield return getter;
			}
			else
			{
				Log.Warn($"[PackmasterLib] CharacterRedirectPatch: CharacterModel.{name} getter not found (game update?), skipping.");
			}
		}
	}

	[HarmonyPostfix]
	private static void RedirectPostfix(CharacterModel __instance, ref string __result)
	{
		if (__instance is not RedirectedCharacterModel redirected || string.IsNullOrEmpty(__result))
		{
			return;
		}
		var entry = __instance.Id.Entry.ToLowerInvariant();
		if (entry == redirected.AssetRedirectEntry)
		{
			return;
		}
		// Plain replace: paths look like "char_select_bg_vanilla_slinger", where regex \b never matches after '_'.
		__result = __result.Replace(entry, redirected.AssetRedirectEntry);
	}
}

/// <summary>
/// Ancient dialogues are keyed by character. A redirected character has none of its own, and the
/// Architect (no character-agnostic lines) would end up with no dialogue and crash in WinRun, so
/// fall back to the dialogues of the character whose assets it reuses.
/// </summary>
[HarmonyPatch(typeof(AncientDialogueSet), nameof(AncientDialogueSet.GetValidDialogues))]
internal static class RedirectedDialoguePatch
{
	private static void Postfix(AncientDialogueSet __instance, ModelId characterId, int charVisits, int totalVisits,
		bool allowAnyCharacterDialogues, ref IEnumerable<AncientDialogue> __result)
	{
		if (__result.Any() || ModelDb.GetByIdOrNull<CharacterModel>(characterId) is not RedirectedCharacterModel { AssetSource: { } source })
		{
			return;
		}
		__result = __instance.GetValidDialogues(source.Id, charVisits, totalVisits, allowAnyCharacterDialogues);
	}
}
