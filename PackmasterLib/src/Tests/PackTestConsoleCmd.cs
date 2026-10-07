using System.Text;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using Sts2Packmaster.Lib.Api;
using Sts2Packmaster.Lib.Core;

namespace Sts2Packmaster.Lib.Tests;

/// <summary>
/// packtest resolve [seed] : verifies slot resolution invariants for every registered pack character.
/// packtest state         : dumps the current run's pack state and validates it.
/// </summary>
public class PackTestConsoleCmd : AbstractConsoleCmd
{
	public override string CmdName => "packtest";

	public override string Args => "resolve [seed:int] | state";

	public override string Description => "Validate PackmasterLib pack resolution logic.";

	public override bool IsNetworked => false;

	public override CmdResult Process(Player? issuingPlayer, string[] args)
	{
		if (args.Length == 0 || args[0] == "resolve")
		{
			var seed = args.Length > 1 && ulong.TryParse(args[1], out var s) ? s : 12345UL;
			return TestResolve(seed);
		}
		if (args[0] == "state")
		{
			return DumpState(issuingPlayer);
		}
		return new CmdResult(success: false, "Usage: packtest resolve [seed] | packtest state");
	}

	private static CmdResult TestResolve(ulong seed)
	{
		var report = new StringBuilder();
		var registrations = PackmasterApiRegistrations();
		if (registrations.Count == 0)
		{
			return new CmdResult(success: false, "No pack characters registered (is a pack mod loaded?).");
		}
		var failures = 0;
		var checks = 0;
		foreach (var registration in registrations)
		{
			var charName = PackRegistry.RegistrationKey(registration);
			var packs = registration.Packs.Select(p => p.Id).ToHashSet();
			for (var i = 0; i < 100; i++)
			{
				var rng = new Rng(seed + (ulong)i * 7919UL);
				foreach (var slots in SampleSlotConfigs(registration))
				{
					var result = PackResolver.Resolve(registration, slots, allMode: false, rng);
					// 1. no duplicates, all valid
					var ids = result.Selected.Select(p => p.Id).ToList();
					checks++;
					if (ids.Count != ids.Distinct().Count())
					{
						failures++;
						report.AppendLine($"FAIL {charName}: duplicate packs {string.Join(",", ids)}");
					}
					checks++;
					if (ids.Any(id => !packs.Contains(id)))
					{
						failures++;
						report.AppendLine($"FAIL {charName}: unknown pack in {string.Join(",", ids)}");
					}
					// 2. fixed slots honored (order-wise: each fixed token's pack is selected)
					var tokenPacks = slots.Where(t => t != PackSlotToken.Random && t != PackSlotToken.Choice && t != PackSlotToken.None)
						.Where(t => packs.Contains(t)).ToList();
					checks++;
					if (tokenPacks.Any(t => !ids.Contains(t)))
					{
						failures++;
						report.AppendLine($"FAIL {charName}: fixed slot '{tokenPacks.First(t => !ids.Contains(t))}' missing from {string.Join(",", ids)}");
					}
					// 3. count invariant: selected + choice-slots == slots - none - invalid
					var expectedPending = slots.Count(t => t == PackSlotToken.Choice);
					checks++;
					if (result.PendingChoices.Count > expectedPending)
					{
						failures++;
						report.AppendLine($"FAIL {charName}: pending choices {result.PendingChoices.Count} > {expectedPending}");
					}
					// 4. candidates are valid, unique and pack-preview-backed
					foreach (var candidates in result.PendingChoices)
					{
						checks++;
						if (candidates.Count < 1 || candidates.Count > 3 || candidates.Select(c => c.Id).Distinct().Count() != candidates.Count)
						{
							failures++;
							report.AppendLine($"FAIL {charName}: bad candidate list {string.Join(",", candidates.Select(c => c.Id))}");
						}
					}
					// 5. all-packs mode
					var allResult = PackResolver.Resolve(registration, slots, allMode: true, rng);
					checks++;
					if (allResult.Selected.Count != registration.Packs.Count || allResult.PendingChoices.Count != 0)
					{
						failures++;
						report.AppendLine($"FAIL {charName}: all-packs mode wrong ({allResult.Selected.Count}/{registration.Packs.Count})");
					}
				}
			}
			report.AppendLine($"{charName}: packs=[{string.Join(", ", packs)}] default=[{string.Join(",", registration.DefaultSlots)}]");
		}
		var summary = $"packtest resolve: {checks} checks, {failures} failures over {registrations.Count} character(s)\n" + report;
		return new CmdResult(success: failures == 0, summary.ToString());
	}

	private static IEnumerable<List<string>> SampleSlotConfigs(PackCharacterRegistration registration)
	{
		yield return registration.DefaultSlots.ToList();
		yield return Enumerable.Repeat(PackSlotToken.Random, 3).ToList();
		yield return Enumerable.Repeat(PackSlotToken.Choice, 4).ToList();
		yield return Enumerable.Repeat(PackSlotToken.None, 5).ToList();
		var tokens = registration.Packs.Select(p => p.Id).ToList();
		if (tokens.Count > 0)
		{
			yield return tokens.Take(2).Concat(new[] { PackSlotToken.Choice, PackSlotToken.Random, PackSlotToken.None, PackSlotToken.Random, PackSlotToken.Choice }).ToList();
		}
	}

	private static CmdResult DumpState(Player? player)
	{
		if (player == null || !RunManager.Instance.IsInProgress)
		{
			return new CmdResult(success: false, "No run in progress.");
		}
		var report = new StringBuilder();
		var failures = 0;
		foreach (var p in player.RunState.Players)
		{
			var state = PackState.Get(p);
			if (state == null)
			{
				report.AppendLine($"{p.Character.Id.Entry}: (no pack state)");
				continue;
			}
			var ids = state.SelectedCardIds();
			report.AppendLine($"{p.Character.Id.Entry}: selected=[{string.Join(", ", state.Selected.Select(x => x.Id))}] pending={state.PendingChoices.Count} cards={ids.Count}");
			// Every card in the pool of this character must belong to exactly one selected pack or be a preview/extra.
			var ownPoolCards = p.Character.CardPool.AllCards.Where(c => PackRegistry.GetPackOf(c) != null).ToList();
			foreach (var card in ownPoolCards)
			{
				var pack = PackRegistry.GetPackOf(card);
				if (pack != null && !state.Selected.Contains(pack))
				{
					failures++;
					report.AppendLine($"FAIL: pool card {card.Id} belongs to unselected pack {pack.Id}");
					break;
				}
			}
		}
		MegaCrit.Sts2.Core.Logging.Log.Info($"[PackmasterLib] {report}");
		return new CmdResult(success: failures == 0, report.ToString());
	}

	private static IReadOnlyList<PackCharacterRegistration> PackmasterApiRegistrations()
	{
		return PackRegistry.Registrations;
	}
}
