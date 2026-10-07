using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using Sts2Packmaster.Lib.Ui;

namespace Sts2Packmaster.Lib.Patches;

/// <summary>Attaches the pack config panel to the character select screen.</summary>
[HarmonyPatch]
internal static class CharacterSelectPatch
{
	[HarmonyPatch(typeof(NCharacterSelectScreen), "_Ready")]
	[HarmonyPostfix]
	private static void AttachPanel(NCharacterSelectScreen __instance)
	{
		try
		{
			PackConfigPanel.Attach(__instance);
		}
		catch (Exception e)
		{
			Log.Error($"[PackmasterLib] Failed to attach pack config panel: {e}");
		}
	}

	[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter))]
	[HarmonyPostfix]
	private static void RefreshPanel(NCharacterSelectScreen __instance, CharacterModel characterModel)
	{
		try
		{
			PackConfigPanel.OnCharacterSelected(__instance, characterModel);
		}
		catch (Exception e)
		{
			Log.Error($"[PackmasterLib] Failed to refresh pack config panel: {e}");
		}
	}
}
