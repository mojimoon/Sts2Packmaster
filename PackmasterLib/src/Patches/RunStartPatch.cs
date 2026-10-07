using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using Sts2Packmaster.Lib.Core;
using Sts2Packmaster.Lib.Ui;

namespace Sts2Packmaster.Lib.Patches;

/// <summary>
/// Appends the hidden PackRunModifier when a run contains pack characters. This is the single
/// choke point for every new-run path (singleplayer, multiplayer, debug bootstrap).
/// </summary>
[HarmonyPatch]
internal static class RunStartPatch
{
	[HarmonyPatch(typeof(RunState), nameof(RunState.CreateForNewRun))]
	[HarmonyPrefix]
	private static void AttachPackModifier(IReadOnlyList<Player> players, ref IReadOnlyList<ModifierModel> modifiers)
	{
		try
		{
			if (TestMode.IsOn)
			{
				return;
			}
			var setups = new List<(ulong playerId, string charEntry, bool allMode, IEnumerable<string> slots)>();
			var isMultiplayer = players.Count > 1;
			foreach (var player in players)
			{
				var registration = PackRegistry.GetRegistration(player.Character);
				if (registration == null)
				{
					continue;
				}
				var key = PackRegistry.RegistrationKey(registration);
				// Multiplayer: configs are not synced between clients, so everyone uses the default.
				var config = isMultiplayer
					? PackSlotConfig.FromDefault(registration)
					: PackConfigStore.Load(key) ?? PackSlotConfig.FromDefault(registration);
				setups.Add((player.NetId, key, config.AllPacks, config.Slots));
			}
			if (setups.Count == 0)
			{
				return;
			}
			var modifier = PackRegistry.CreateRunModifier();
			if (modifier == null)
			{
				Log.Warn("[PackmasterLib] PackRunModifier was not registered with ModelDb; packs disabled this run.");
				return;
			}
			modifier.PackConfig = PackState.EncodeConfig(setups);
			modifier.PackSelected = "";
			Log.Info($"[PackmasterLib] Attaching pack setup for {setups.Count} player(s). Config: {modifier.PackConfig}");
			modifiers = modifiers.Concat(new[] { (ModifierModel)modifier }).ToList();
		}
		catch (Exception e)
		{
			Log.Error($"[PackmasterLib] RunStartPatch failed: {e}");
		}
	}
}

/// <summary>Injects registered pack characters into ModelDb.AllCharacters (auto-unlocks them).</summary>
[HarmonyPatch]
internal static class ModelDbPatch
{
	[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.AllCharacters), MethodType.Getter)]
	[HarmonyPostfix]
	private static void AddPackCharacters(ref IEnumerable<CharacterModel> __result)
	{
		var characters = PackRegistry.GetPackCharacters();
		if (characters.Count == 0)
		{
			return;
		}
		var existing = __result.ToList();
		__result = existing.Concat(characters.Where(c => !existing.Contains(c)));
	}
}

/// <summary>
/// Boss kills look up "{CHARACTER}2_EPOCH" etc., which throws for characters without a timeline.
/// Pack characters have no epochs, so skip it for them.
/// </summary>
[HarmonyPatch]
internal static class CharEpochPatch
{
	[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Saves.Managers.ProgressSaveManager), "ObtainCharUnlockEpoch")]
	[HarmonyPrefix]
	private static bool SkipForPackCharacters(Player localPlayer) => PackRegistry.GetRegistration(localPlayer.Character) == null;
}

