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

	/// <summary>Packs in this run's pool (fixed + random first, then picks in order).</summary>
	public readonly List<PackDefinition> Selected = new();

	/// <summary>Remaining "choice of 3" slots, each with its candidates (rolled at run start, saved).</summary>
	public readonly List<List<PackDefinition>> PendingChoices = new();

	/// <summary>The start-of-run pack setup screen has been confirmed (or skipped).</summary>
	public bool SetupDone;

	/// <summary>Pops the next pending choice slot; returns its still-available candidates (null = nothing left, slot skipped).</summary>
	public List<PackDefinition>? TakeNextChoice(Rng rng)
	{
		var rolled = PendingChoices[0];
		PendingChoices.RemoveAt(0);
		return PackResolver.NextCandidates(Registration, rolled, Selected, rng);
	}

	/// <summary>Cards of the selected packs.</summary>
	public List<CardModel> SelectedCards() => Selected.SelectMany(PackRegistry.GetPackCards).ToList();

	public HashSet<ModelId> SelectedCardIds() => SelectedCards().Select(c => c.Id).ToHashSet();
}

/// <summary>
/// Run-scoped pack state. The durable copy lives in <see cref="PackRunModifier"/>'s SavedProperty
/// strings (selected packs, pending choice candidates, setup-done flags), so saving and quitting
/// in the middle of the pack setup screen resumes with the same choices.
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

	public static PlayerPackState? Get(Player? player) => player == null ? null : Get(player.RunState as RunState, player.NetId);

	public static bool HasPendingChoices(Player player) => Get(player)?.PendingChoices.Count > 0;

	public static bool NeedsSetup(Player? player) => Get(player) is { SetupDone: false };

	/// <summary>Card ids the player may obtain rewards from; null when pack logic is not active for this player.</summary>
	public static HashSet<ModelId>? GetSelectedCardIds(Player player) => Get(player)?.SelectedCardIds();

	/// <summary>
	/// Build state for a new run from the modifier's config string. Resolves fixed/random slots and
	/// rolls the choice candidates deterministically from the given rng (same on every peer).
	/// </summary>
	public static void InitializeRun(RunState runState, string configString, Rng rng)
	{
		var entry = Runs.GetOrCreateValue(runState);
		var multiplayer = runState.Players.Count > 1;
		foreach (var (playerId, charEntry, allMode, tokens) in DecodeSegments(configString))
		{
			var registration = FindRegistration(charEntry);
			var player = runState.Players.FirstOrDefault(p => p.NetId == playerId);
			if (registration == null || player == null)
			{
				Log.Warn($"[PackmasterLib] Config references unknown character/player '{charEntry}'/{playerId}, skipping.");
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
			foreach (var line in resolution.Trace)
			{
				Log.Info($"[PackmasterLib]   {line}");
			}
			Log.Info($"[PackmasterLib] {player.Character.Id.Entry} packs resolved: [{string.Join(", ", state.Selected.Select(p => p.Id))}]"
				+ (state.PendingChoices.Count > 0 ? $" + {state.PendingChoices.Count} choice slot(s)" : ""));

			// STS1 skips the setup screen in all-packs mode. Multiplayer has no synced pick UI, so
			// choices resolve deterministically (same rng on every peer) and the screen is skipped.
			// The developer setting skips it in singleplayer.
			if (allMode || multiplayer || PackmasterSettings.AutoResolveChoices)
			{
				ResolvePendingInternal(state, rng, multiplayer ? "Multiplayer" : allMode ? "All packs mode" : "AutoResolveChoices setting");
				state.SetupDone = true;
			}
		}
		Persist(runState);
	}

	/// <summary>Rebuild state on save load from the modifier's saved strings.</summary>
	public static void LoadRun(RunState runState, PackRunModifier modifier)
	{
		var entry = Runs.GetOrCreateValue(runState);
		var selected = DecodeIdLists(modifier.PackSelected);
		var pending = DecodePending(modifier.PackPending);
		var done = modifier.PackSetupDone.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(s => ulong.TryParse(s, out var v) ? v : 0UL).ToHashSet();
		foreach (var (playerId, charEntry, allMode, tokens) in DecodeSegments(modifier.PackConfig))
		{
			var registration = FindRegistration(charEntry);
			if (registration == null || runState.Players.All(p => p.NetId != playerId))
			{
				continue;
			}
			var state = new PlayerPackState
			{
				PlayerId = playerId,
				Registration = registration,
				AllMode = allMode,
				SlotTokens = tokens.ToList(),
				// Saves written before the setup-screen rework have no done flags: treat them as done.
				SetupDone = done.Contains(playerId) || string.IsNullOrEmpty(modifier.PackPending) && string.IsNullOrEmpty(modifier.PackSetupDone),
			};
			if (selected.TryGetValue(playerId, out var ids))
			{
				state.Selected.AddRange(ids.Select(id => registration.Packs.FirstOrDefault(p => p.Id == id)).OfType<PackDefinition>());
			}
			if (pending.TryGetValue(playerId, out var slots))
			{
				foreach (var slot in slots)
				{
					state.PendingChoices.Add(slot.Select(id => registration.Packs.FirstOrDefault(p => p.Id == id)).OfType<PackDefinition>().ToList());
				}
			}
			entry.ByPlayer[playerId] = state;
			Log.Info($"[PackmasterLib] Loaded packs for player {playerId}: [{string.Join(", ", state.Selected.Select(p => p.Id))}], "
				+ $"{state.PendingChoices.Count} pending choice(s), setup {(state.SetupDone ? "done" : "pending")}.");
		}
	}

	/// <summary>The player picked <paramref name="pack"/> for the current choice slot.</summary>
	public static void Pick(Player player, PackDefinition pack)
	{
		var state = Get(player) ?? throw new InvalidOperationException("No pack state for player.");
		if (state.PendingChoices.Count == 0 || !state.PendingChoices[0].Contains(pack))
		{
			throw new InvalidOperationException($"'{pack.Id}' is not a candidate of the current choice.");
		}
		state.PendingChoices.RemoveAt(0);
		state.Selected.Add(pack);
		Log.Info($"[PackmasterLib] Player {player.NetId} picked pack '{pack.Id}' ({state.PendingChoices.Count} choice(s) left).");
		Persist((RunState)player.RunState);
	}

	/// <summary>The pack setup screen was confirmed.</summary>
	public static void CompleteSetup(Player player)
	{
		var state = Get(player) ?? throw new InvalidOperationException("No pack state for player.");
		if (state.PendingChoices.Count > 0)
		{
			ResolvePendingInternal(state, ((RunState)player.RunState).Rng.Niche, "Setup confirmed with open choices");
		}
		state.SetupDone = true;
		Log.Info($"[PackmasterLib] Player {player.NetId} pack setup complete: [{string.Join(", ", state.Selected.Select(p => p.Id))}]");
		Persist((RunState)player.RunState);
	}

	/// <summary>Randomly resolve any remaining choice slots (tests / developer tools).</summary>
	public static void ResolvePending(RunState runState, ulong playerId, string reason)
	{
		var state = Get(runState, playerId);
		if (state == null)
		{
			return;
		}
		ResolvePendingInternal(state, runState.Rng.Niche, reason);
		Persist(runState);
	}

	private static void ResolvePendingInternal(PlayerPackState state, Rng rng, string reason)
	{
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
	}

	/// <summary>Write the in-memory state back into the run modifier's saved strings.</summary>
	public static void Persist(RunState runState)
	{
		var modifier = runState.Modifiers.OfType<PackRunModifier>().FirstOrDefault();
		if (modifier == null || !Runs.TryGetValue(runState, out var entry))
		{
			return;
		}
		var states = entry.ByPlayer.Values.ToList();
		modifier.PackSelected = string.Join(";", states.Select(s =>
			$"{s.PlayerId}:{PackRegistry.RegistrationKey(s.Registration)}:{string.Join(",", s.Selected.Select(p => p.Id))}"));
		modifier.PackPending = string.Join(";", states.Where(s => s.PendingChoices.Count > 0).Select(s =>
			$"{s.PlayerId}:{string.Join("/", s.PendingChoices.Select(c => string.Join("|", c.Select(p => p.Id))))}"));
		modifier.PackSetupDone = string.Join(",", states.Where(s => s.SetupDone).Select(s => s.PlayerId));
	}

	// ---------------------------------------------------------------- encoding

	/// <summary>One config segment per player: "playerId:charEntry:allFlag:slot,slot,...".</summary>
	public static string EncodeConfig(IEnumerable<(ulong playerId, string charEntry, bool allMode, IEnumerable<string> slots)> setups)
	{
		return string.Join(";", setups.Select(s =>
			$"{s.playerId}:{s.charEntry}:{(s.allMode ? 1 : 0)}:{string.Join(",", s.slots)}"));
	}

	public static List<(ulong playerId, string charEntry, bool allMode, List<string> tokens)> DecodeSegments(string configString)
	{
		var result = new List<(ulong, string, bool, List<string>)>();
		foreach (var raw in Split(configString, ';'))
		{
			var parts = raw.Split(':', 4);
			if (parts.Length != 4 || !ulong.TryParse(parts[0], out var pid))
			{
				Log.Warn($"[PackmasterLib] Bad config segment '{raw}'");
				continue;
			}
			result.Add((pid, parts[1], parts[2] == "1", Split(parts[3], ',').ToList()));
		}
		return result;
	}

	/// <summary>"pid:char:a,b,c;..." → pid → [a,b,c].</summary>
	private static Dictionary<ulong, List<string>> DecodeIdLists(string value)
	{
		var result = new Dictionary<ulong, List<string>>();
		foreach (var raw in Split(value, ';'))
		{
			var parts = raw.Split(':', 3);
			if (parts.Length == 3 && ulong.TryParse(parts[0], out var pid))
			{
				result[pid] = Split(parts[2], ',').ToList();
			}
		}
		return result;
	}

	/// <summary>"pid:a|b|c/d|e|f;..." → pid → [[a,b,c],[d,e,f]].</summary>
	private static Dictionary<ulong, List<List<string>>> DecodePending(string value)
	{
		var result = new Dictionary<ulong, List<List<string>>>();
		foreach (var raw in Split(value, ';'))
		{
			var parts = raw.Split(':', 2);
			if (parts.Length == 2 && ulong.TryParse(parts[0], out var pid))
			{
				result[pid] = Split(parts[1], '/').Select(slot => Split(slot, '|').ToList()).ToList();
			}
		}
		return result;
	}

	private static string[] Split(string? value, char separator) =>
		string.IsNullOrWhiteSpace(value)
			? Array.Empty<string>()
			: value.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

	private static PackCharacterRegistration? FindRegistration(string charEntry) =>
		PackRegistry.Registrations.FirstOrDefault(reg => PackRegistry.RegistrationKey(reg) == charEntry);
}
