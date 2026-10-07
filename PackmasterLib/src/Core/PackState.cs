using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using Sts2Packmaster.Lib.Api;

namespace Sts2Packmaster.Lib.Core;

/// <summary>Per-player pack state for one run.</summary>
public sealed class PlayerPackState
{
	public required ulong PlayerId;
	public required PackCharacterRegistration Registration;
	public bool AllMode;
	public List<string> SlotTokens = new();
	public readonly List<PackDefinition> Selected = new();
	public readonly List<List<PackDefinition>> PendingChoices = new();

	/// <summary>Pops the next pending choice slot; returns its still-available candidates (null = nothing left, slot skipped).</summary>
	public List<PackDefinition>? TakeNextChoice(MegaCrit.Sts2.Core.Random.Rng rng)
	{
		var rolled = PendingChoices[0];
		PendingChoices.RemoveAt(0);
		return PackResolver.NextCandidates(Registration, rolled, Selected, rng);
	}

	public HashSet<ModelId> SelectedCardIds()
	{
		var ids = new HashSet<ModelId>();
		foreach (var pack in Selected)
		{
			foreach (var type in pack.CardTypes)
			{
				var card = PackRegistry.GetCard(type);
				if (card != null)
				{
					ids.Add(card.Id);
				}
			}
		}
		return ids;
	}
}

/// <summary>
/// In-memory, run-scoped state. The durable copy lives in <see cref="PackRunModifier"/>'s
/// SavedProperty strings; this class mirrors it for fast access and holds the not-yet-chosen
/// "pick 1 of 3" candidate lists (which are intentionally not saved).
/// </summary>
public static class PackState
{
	private sealed class RunEntry
	{
		public readonly Dictionary<ulong, PlayerPackState> ByPlayer = new();
	}

	private static readonly ConditionalWeakTable<RunState, RunEntry> Runs = new();

	public static PlayerPackState? Get(RunState? runState, ulong playerId)
	{
		if (runState == null || !Runs.TryGetValue(runState, out var entry))
		{
			return null;
		}
		return entry.ByPlayer.TryGetValue(playerId, out var state) ? state : null;
	}

	public static PlayerPackState? Get(Player player) => Get(player.RunState as RunState, player.NetId);

	public static bool HasPendingChoices(Player player)
	{
		return Get(player)?.PendingChoices.Count > 0;
	}

	/// <summary>Card ids the player may obtain rewards from; null when pack logic is not active for this player.</summary>
	public static HashSet<ModelId>? GetSelectedCardIds(Player player)
	{
		var state = Get(player);
		return state?.SelectedCardIds();
	}

	/// <summary>
	/// Build state for a new run from the modifier's config string. Resolves fixed/random slots and
	/// creates the choice candidates deterministically from the given rng.
	/// </summary>
	public static void InitializeRun(RunState runState, string configString, Rng rng)
	{
		var entry = Runs.GetOrCreateValue(runState);
		foreach (var seg in DecodeSegments(configString))
		{
			var (playerId, charEntry, allMode, tokens) = seg;
			var registration = FindRegistration(charEntry);
			if (registration == null)
			{
				Log.Warn($"[PackmasterLib] Config references unknown character '{charEntry}', skipping.");
				continue;
			}
			var player = runState.Players.FirstOrDefault(p => p.NetId == playerId);
			if (player == null)
			{
				continue;
			}
			var resolution = PackResolver.Resolve(registration, tokens, allMode, rng);
			var state = new PlayerPackState
			{
				PlayerId = playerId,
				Registration = registration,
				AllMode = allMode,
				SlotTokens = tokens.ToList(),
			};
			state.Selected.AddRange(resolution.Selected);
			state.PendingChoices.AddRange(resolution.PendingChoices);
			entry.ByPlayer[playerId] = state;
			Log.Info($"[PackmasterLib] {player.Character.Id.Entry} packs resolved: [{string.Join(", ", state.Selected.Select(p => p.Id))}]"
				+ (state.PendingChoices.Count > 0 ? $" + {state.PendingChoices.Count} choice slot(s)" : ""));
		}
		// Local developer setting: singleplayer only, so multiplayer peers never diverge.
		if (PackmasterSettings.AutoResolveChoices && runState.Players.Count == 1)
		{
			foreach (var playerId in entry.ByPlayer.Keys.ToList())
			{
				ResolvePending(runState, playerId, "AutoResolveChoices setting");
			}
		}
	}

	/// <summary>
	/// Rebuild state on save load. Pending choice candidates cannot be recovered; any leftover
	/// choice slots are resolved randomly (this can only happen if a save is taken mid-Neow).
	/// </summary>
	public static void LoadRun(RunState runState, string configString, string selectedString, Rng rng)
	{
		var entry = Runs.GetOrCreateValue(runState);
		var selected = DecodeSelected(selectedString);
		foreach (var seg in DecodeSegments(configString))
		{
			var (playerId, charEntry, allMode, tokens) = seg;
			var registration = FindRegistration(charEntry);
			if (registration == null || !selected.TryGetValue(playerId, out var selectedIds))
			{
				continue;
			}
			var player = runState.Players.FirstOrDefault(p => p.NetId == playerId);
			if (player == null)
			{
				continue;
			}
			var state = new PlayerPackState
			{
				PlayerId = playerId,
				Registration = registration,
				AllMode = allMode,
				SlotTokens = tokens.ToList(),
			};
			foreach (var packId in selectedIds)
			{
				var pack = registration.Packs.FirstOrDefault(p => p.Id == packId);
				if (pack != null)
				{
					state.Selected.Add(pack);
				}
			}
			// If a choice slot was never resolved (save taken during Neow), pick randomly now.
			var resolution = PackResolver.Resolve(registration, tokens, allMode, rng);
			if (resolution.PendingChoices.Count > 0)
			{
				foreach (var candidates in resolution.PendingChoices)
				{
					var pick = candidates[rng.NextInt(candidates.Count)];
					state.Selected.Add(pick);
					Log.Info($"[PackmasterLib] Loaded save with unresolved choice slot; randomly took '{pick.Id}'.");
				}
			}
			entry.ByPlayer[playerId] = state;
		}
	}

	/// <summary>
	/// Randomly resolve any remaining choice slots (used when the Neow option was never offered,
	/// e.g. the Neow epoch is locked). Updates the run modifier's saved string.
	/// </summary>
	public static void ResolvePending(RunState runState, ulong playerId, string reason)
	{
		var state = Get(runState, playerId);
		if (state == null || state.PendingChoices.Count == 0)
		{
			return;
		}
		var rng = runState.Rng.Niche;
		while (state.PendingChoices.Count > 0)
		{
			var candidates = state.TakeNextChoice(rng);
			if (candidates == null)
			{
				continue;
			}
			var pick = candidates[rng.NextInt(candidates.Count)];
			state.Selected.Add(pick);
			Log.Info($"[PackmasterLib] {reason}: randomly took '{pick.Id}'.");
		}
		var modifier = runState.Modifiers.OfType<PackRunModifier>().FirstOrDefault();
		if (modifier != null)
		{
			modifier.PackSelected = EncodeSelected(runState);
		}
	}

	/// <summary>Serialize current selections back into the modifier string.</summary>
	public static string EncodeSelected(RunState runState)
	{
		if (!Runs.TryGetValue(runState, out var entry))
		{
			return "";
		}
		var parts = entry.ByPlayer.Values.Select(s =>
			$"{s.PlayerId}:{PackRegistry.RegistrationKey(s.Registration)}:{string.Join(",", s.Selected.Select(p => p.Id))}");
		return string.Join(";", parts);
	}

	// ---------------------------------------------------------------- config encoding

	/// <summary>One config segment per player: "playerId:charEntry:allFlag:slot,slot,...".</summary>
	public static string EncodeConfig(IEnumerable<(ulong playerId, string charEntry, bool allMode, IEnumerable<string> slots)> setups)
	{
		return string.Join(";", setups.Select(s =>
			$"{s.playerId}:{s.charEntry}:{(s.allMode ? 1 : 0)}:{string.Join(",", s.slots)}"));
	}

	public static List<(ulong playerId, string charEntry, bool allMode, List<string> tokens)> DecodeSegments(string configString)
	{
		var result = new List<(ulong, string, bool, List<string>)>();
		if (string.IsNullOrWhiteSpace(configString))
		{
			return result;
		}
		foreach (var raw in configString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			var parts = raw.Split(':', 4);
			if (parts.Length != 4 || !ulong.TryParse(parts[0], out var pid))
			{
				Log.Warn($"[PackmasterLib] Bad config segment '{raw}'");
				continue;
			}
			var tokens = parts[3].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
			result.Add((pid, parts[1], parts[2] == "1", tokens));
		}
		return result;
	}

	private static Dictionary<ulong, List<string>> DecodeSelected(string selectedString)
	{
		var result = new Dictionary<ulong, List<string>>();
		if (string.IsNullOrWhiteSpace(selectedString))
		{
			return result;
		}
		foreach (var raw in selectedString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			var parts = raw.Split(':', 3);
			if (parts.Length < 3 || !ulong.TryParse(parts[0], out var pid))
			{
				continue;
			}
			result[pid] = parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
		}
		return result;
	}

	private static PackCharacterRegistration? FindRegistration(string charEntry)
	{
		foreach (var reg in PackRegistry.Registrations)
		{
			if (PackRegistry.RegistrationKey(reg) == charEntry)
			{
				return reg;
			}
		}
		return null;
	}
}
