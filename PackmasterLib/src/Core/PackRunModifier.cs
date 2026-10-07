using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using Sts2Packmaster.Lib.Api;

namespace Sts2Packmaster.Lib.Core;

/// <summary>
/// Hidden run modifier that carries the pack configuration and selected packs of every pack
/// character in the run. Persisted automatically through the game's modifier save channel
/// (SerializableRun -> SavedProperties), including multiplayer packets.
/// </summary>
public class PackRunModifier : ModifierModel
{
	/// <summary>"playerId:charEntry:allFlag:slot,slot,...;..." — written at run creation.</summary>
	[SavedProperty]
	public string PackConfig { get; set; } = "";

	/// <summary>"playerId:charEntry:packId,packId;..." — updated as packs are chosen.</summary>
	[SavedProperty]
	public string PackSelected { get; set; } = "";

	public override LocString Title => new LocString("gameplay_ui", "PACKMASTER_LIB.neow.title");

	public override LocString Description => new LocString("gameplay_ui", "PACKMASTER_LIB.neow.desc");

	// Shown in the run's top bar; reuse the vanilla Draft icon instead of the missing-icon fallback.
	protected override string IconPath => MegaCrit.Sts2.Core.Helpers.ImageHelper.GetImagePath("packed/modifiers/draft.png");

	protected override void AfterRunCreated(RunState runState)
	{
		if (string.IsNullOrWhiteSpace(PackConfig))
		{
			return;
		}
		PackState.InitializeRun(runState, PackConfig, runState.Rng.UpFront);
	}

	protected override void AfterRunLoaded(RunState runState)
	{
		if (string.IsNullOrWhiteSpace(PackConfig))
		{
			return;
		}
		PackState.LoadRun(runState, PackConfig, PackSelected, runState.Rng.UpFront);
		PackSelected = PackState.EncodeSelected(runState);
	}

	/// <summary>
	/// Presented as the Neow option whenever the owner still has unresolved pick-a-pack slots.
	/// The game shows modifier options exclusively when present, mirroring STS1's pack setup.
	/// </summary>
	public override Func<Task>? GenerateNeowOption(EventModel eventModel)
	{
		var player = eventModel.Owner;
		var state = PackState.Get(player);
		if (state == null || state.PendingChoices.Count == 0)
		{
			return null;
		}
		return async () =>
		{
			var runState = (RunState)player.RunState;
			while (state.PendingChoices.Count > 0)
			{
				var candidates = state.TakeNextChoice(runState.Rng.Niche);
				if (candidates == null)
				{
					continue;
				}
				var previews = new List<CardModel>();
				foreach (var pack in candidates)
				{
					var preview = pack.PreviewCardType == null ? null : PackRegistry.GetCard(pack.PreviewCardType);
					if (preview != null)
					{
						previews.Add(preview);
					}
				}
				if (previews.Count == 0)
				{
					// Author provided no preview cards: resolve randomly.
					var pick = candidates[RngShared.NextInt(runState, candidates.Count)];
					state.Selected.Add(pick);
					Log.Info($"[PackmasterLib] Choice slot had no preview cards; randomly took '{pick.Id}'.");
					continue;
				}
				var prefs = new CardSelectorPrefs(new LocString("card_selection", "PACKMASTER_LIB.choice.prompt"), 1)
				{
					Cancelable = false,
				};
				var chosen = (await CardSelectCmd.FromSimpleGrid(new BlockingPlayerChoiceContext(), previews, player, prefs)).ToList();
				PackDefinition? picked = null;
				if (chosen.Count > 0)
				{
					picked = candidates.FirstOrDefault(p => p.PreviewCardType != null && PackRegistry.GetCard(p.PreviewCardType) == chosen[0]);
				}
				picked ??= candidates[0];
				state.Selected.Add(picked);
				PackSelected = PackState.EncodeSelected(runState);
				Log.Info($"[PackmasterLib] Player {player.NetId} picked pack '{picked.Id}' ({state.PendingChoices.Count} choice slot(s) left).");
			}
		};
	}
}

/// <summary>RNG helpers that keep the modifier code testable.</summary>
internal static class RngShared
{
	public static int NextInt(RunState runState, int maxExclusive) => runState.Rng.Niche.NextInt(maxExclusive);
}
