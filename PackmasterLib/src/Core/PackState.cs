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
	public required ulong RunSeed;
	public bool AllMode;
	public List<string> SlotTokens = new();

	/// <summary>Packs in this run's pool (fixed + random first, then picks in order).</summary>
	public readonly List<PackDefinition> Selected = new();

	/// <summary>Draft rounds left on the setup screen.</summary>
	public int ChoicesLeft;

	/// <summary>Draft rounds done so far (in memory; reset by save/load until the setup is confirmed).</summary>
	public int Round;

	/// <summary>The previous round's offer.</summary>
	public List<PackDefinition> LastOffer = new();

	/// <summary>Every pack offered and not picked this setup.</summary>
	public readonly List<PackDefinition> Unpicked = new();

	private List<PackDefinition>? _currentOffer;

	/// <summary>The start-of-run pack setup screen has been confirmed (or skipped).</summary>
	public bool SetupDone;

	/// <summary>
	/// The current draft round's offer (empty when no round is left or no pack is left to offer).
	/// Deterministic: the same picks after a save/load lead to the same offers.
	/// </summary>
	public IReadOnlyList<PackDefinition> CurrentOffer()
	{
		while (_currentOffer == null && ChoicesLeft > 0)
		{
			var excluded = PackmasterSettings.ExcludeOnlyLastRound ? LastOffer : (IReadOnlyCollection<PackDefinition>)Unpicked;
			_currentOffer = PackResolver.Offer(Registration, Selected, excluded, Round, OfferRng());
			if (_currentOffer.Count == 0)
			{
				ChoicesLeft = 0; // pool exhausted: remaining slots are skipped
			}
		}
		return _currentOffer ?? (IReadOnlyList<PackDefinition>)Array.Empty<PackDefinition>();
	}

	private Rng OfferRng() =>
		new(RunSeed, $"packmaster/{PlayerId}/{Round}/{string.Join(",", Selected.Select(p => p.Id))}");

	/// <summary>Pick a pack of the current offer (in memory only).</summary>
	public void Pick(PackDefinition pack)
	{
		var offer = CurrentOffer();
		if (!offer.Contains(pack))
		{
			throw new InvalidOperationException($"'{pack.Id}' is not offered in this round.");
		}
		Selected.Add(pack);
		Unpicked.AddRange(offer.Where(p => p != pack));
		LastOffer = offer.ToList();
		_currentOffer = null;
		ChoicesLeft--;
		Round++;
	}

	/// <summary>Resolve every remaining round with a deterministic random pick.</summary>
	public void PickRemainingRandomly(string reason)
	{
		while (CurrentOffer() is { Count: > 0 } offer)
		{
			var pick = offer[OfferRng().NextInt(offer.Count)];
			Pick(pick);
			Log.Info($"[PackmasterLib] {reason}: randomly took '{pick.Id}'.");
		}
	}

	/// <summary>Cards of the selected packs.</summary>
	public List<CardModel> SelectedCards() => Selected.SelectMany(PackRegistry.GetPackCards).ToList();

	public HashSet<ModelId> SelectedCardIds() => SelectedCards().Select(c => c.Id).ToHashSet();
}

/// <summary>
/// Run-scoped pack state. The durable copy lives in <see cref="PackRunModifier"/>'s SavedProperty
/// strings. Draft picks are kept in memory until the setup is confirmed, so saving and quitting during
/// the setup screen restarts the setup (STS1-style save/load), with the same offers for the same picks.
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

	public static bool NeedsSetup(Player? player) => Get(player) is { SetupDone: false };

	/// <summary>Card ids the player may obtain rewards from; null when pack logic is not active for this player.</summary>
	public static HashSet<ModelId>? GetSelectedCardIds(Player player) => Get(player)?.SelectedCardIds();

	/// <summary>Build state for a new run: resolves fixed/random slots deterministically (same on every peer).</summary>
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
				RunSeed = runState.Rng.Seed,
				AllMode = allMode,
				SlotTokens = tokens.ToList(),
				ChoicesLeft = resolution.ChoiceSlots,
			};
			state.Selected.AddRange(resolution.Selected);
			entry.ByPlayer[playerId] = state;
			foreach (var line in resolution.Trace)
			{
				Log.Info($"[PackmasterLib]   {line}");
			}
			Log.Info($"[PackmasterLib] {player.Character.Id.Entry} packs resolved: [{string.Join(", ", state.Selected.Select(p => p.Id))}]"
				+ (state.ChoicesLeft > 0 ? $" + {state.ChoicesLeft} draft round(s)" : ""));

			// STS1 skips the setup screen in all-packs mode. Multiplayer has no synced pick UI, so drafts
			// resolve deterministically (same seed on every peer). The developer setting skips it in singleplayer.
			if (allMode || multiplayer || PackmasterSettings.AutoResolveChoices)
			{
				state.PickRemainingRandomly(multiplayer ? "Multiplayer" : allMode ? "All packs mode" : "AutoResolveChoices setting");
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
		var choicesLeft = DecodeCounts(modifier.PackChoicesLeft);
		var legacyPending = DecodeCounts(modifier.PackPending, legacy: true);
		var done = Split(modifier.PackSetupDone, ',').Select(s => ulong.TryParse(s, out var v) ? v : 0UL).ToHashSet();
		// Saves from before the setup screen existed have no flags at all: treat them as done.
		var preSetupSave = string.IsNullOrEmpty(modifier.PackSetupDone) && string.IsNullOrEmpty(modifier.PackChoicesLeft) && string.IsNullOrEmpty(modifier.PackPending);
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
				RunSeed = runState.Rng.Seed,
				AllMode = allMode,
				SlotTokens = tokens.ToList(),
				SetupDone = done.Contains(playerId) || preSetupSave,
				ChoicesLeft = choicesLeft.TryGetValue(playerId, out var n) ? n : legacyPending.GetValueOrDefault(playerId),
			};
			if (state.SetupDone)
			{
				state.ChoicesLeft = 0;
			}
			if (selected.TryGetValue(playerId, out var ids))
			{
				state.Selected.AddRange(ids.Select(id => registration.Packs.FirstOrDefault(p => p.Id == id)).OfType<PackDefinition>());
			}
			entry.ByPlayer[playerId] = state;
			Log.Info($"[PackmasterLib] Loaded packs for player {playerId}: [{string.Join(", ", state.Selected.Select(p => p.Id))}], "
				+ $"{state.ChoicesLeft} draft round(s) left, setup {(state.SetupDone ? "done" : "pending")}.");
		}
	}

	/// <summary>The player picked <paramref name="pack"/> in the current draft round (not saved until confirmed).</summary>
	public static void Pick(Player player, PackDefinition pack)
	{
		var state = Get(player) ?? throw new InvalidOperationException("No pack state for player.");
		state.Pick(pack);
		Log.Info($"[PackmasterLib] Player {player.NetId} picked pack '{pack.Id}' ({state.ChoicesLeft} round(s) left).");
	}

	/// <summary>The pack setup screen was confirmed: the pack pool becomes final and is saved.</summary>
	public static void CompleteSetup(Player player)
	{
		var state = Get(player) ?? throw new InvalidOperationException("No pack state for player.");
		state.PickRemainingRandomly("Setup confirmed with open rounds");
		state.SetupDone = true;
		Log.Info($"[PackmasterLib] Player {player.NetId} pack setup complete: [{string.Join(", ", state.Selected.Select(p => p.Id))}]");
		Persist((RunState)player.RunState);
	}

	/// <summary>Randomly resolve any remaining rounds (tests / developer tools).</summary>
	public static void ResolvePending(RunState runState, ulong playerId, string reason)
	{
		Get(runState, playerId)?.PickRemainingRandomly(reason);
	}

	/// <summary>Write the saved state into the run modifier. Unconfirmed drafts are not written.</summary>
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
		modifier.PackChoicesLeft = string.Join(";", states.Select(s => $"{s.PlayerId}:{s.ChoicesLeft}"));
		modifier.PackPending = "";
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

	/// <summary>"pid:n;..." → pid → n. Legacy v0.2 "pid:a|b|c/d|e|f" → number of rounds.</summary>
	private static Dictionary<ulong, int> DecodeCounts(string value, bool legacy = false)
	{
		var result = new Dictionary<ulong, int>();
		foreach (var raw in Split(value, ';'))
		{
			var parts = raw.Split(':', 2);
			if (parts.Length == 2 && ulong.TryParse(parts[0], out var pid))
			{
				result[pid] = legacy ? Split(parts[1], '/').Length : int.TryParse(parts[1], out var n) ? n : 0;
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
