using System.Text;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using Sts2Packmaster.Lib.Core;

namespace Sts2Packmaster.VanillaPacks;

/// <summary>
/// packgive <packId> : add every card of a pack to your deck (sandbox fun).
/// packtest reward [n] : generate n combat rewards and verify every card comes from the selected packs.
/// </summary>
public class VanillaPackTestCmd : AbstractConsoleCmd
{
	public override string CmdName => "packgive";

	public override string Args => "<packId>";

	public override string Description => "Add all cards of a VanillaPacks pack to your deck.";

	public override bool IsNetworked => false;

	public override CmdResult Process(Player? issuingPlayer, string[] args)
	{
		if (args.Length < 1 || issuingPlayer == null || !RunManager.Instance.IsInProgress)
		{
			return new CmdResult(success: false, "Usage (in run): packgive <strikes|defends|powers|tricks>");
		}
		var registration = PackRegistry.GetRegistration(issuingPlayer.Character);
		if (registration == null)
		{
			return new CmdResult(success: false, "Current character is not a pack character.");
		}
		var pack = registration.Packs.FirstOrDefault(p => p.Id == args[0]);
		if (pack == null)
		{
			return new CmdResult(success: false, $"Unknown pack '{args[0]}'. Packs: {string.Join(", ", registration.Packs.Select(p => p.Id))}");
		}
		var cards = pack.CardTypes.Select(t => PackRegistry.GetCard(t)).Where(c => c != null).Select(c => c!).ToList();
		var report = new StringBuilder();
		foreach (var card in cards)
		{
			var mutable = issuingPlayer.RunState.CreateCard(card, issuingPlayer);
			report.AppendLine(mutable.Id.Entry);
		}
		var added = CardPileCmdAddAll(issuingPlayer, cards);
		return new CmdResult(added, success: true, $"Added {cards.Count} cards from '{pack.Id}':\n{report}");
	}

	private static System.Threading.Tasks.Task CardPileCmdAddAll(Player player, List<CardModel> cards)
	{
		return MegaCrit.Sts2.Core.Commands.CardPileCmd.Add(cards.Select(c => player.RunState.CreateCard(c, player)).ToList(), PileType.Deck);
	}
}

/// <summary>Verifies that reward generation only produces cards from the selected packs.</summary>
public class VanillaPackRewardTestCmd : AbstractConsoleCmd
{
	public override string CmdName => "packtestreward";

	public override string Args => "[rounds:int=10]";

	public override string Description => "Generate card rewards and verify they only contain selected-pack cards.";

	public override bool IsNetworked => false;

	public override CmdResult Process(Player? issuingPlayer, string[] args)
	{
		if (issuingPlayer == null || !RunManager.Instance.IsInProgress)
		{
			return new CmdResult(success: false, "Use inside a run.");
		}
		var player = issuingPlayer;
		var state = PackState.Get(player);
		if (state == null)
		{
			return new CmdResult(success: false, $"{player.Character.Id.Entry} has no pack state (not a pack character or modifier missing).");
		}
		var rounds = args.Length > 0 && int.TryParse(args[0], out var n) ? Math.Max(1, n) : 10;
		var selected = PackState.GetSelectedCardIds(player)!;
		var options = CardCreationOptions.ForRoom(player, RoomType.Monster).WithFlags(CardCreationFlags.IsFromCombat);
		var failures = 0;
		var total = 0;
		var report = new StringBuilder();
		for (var i = 0; i < rounds; i++)
		{
			foreach (var result in CardFactory.CreateForReward(player, 3, options))
			{
				total++;
				if (!selected.Contains(result.Card.Id))
				{
					failures++;
					report.AppendLine($"FAIL: reward card {result.Card.Id} is not in selected packs");
				}
			}
		}
		report.AppendLine($"packtestreward: {total} cards over {rounds} rewards, {failures} failures; selected packs: [{string.Join(", ", state.Selected.Select(p => p.Id))}]");
		return new CmdResult(success: failures == 0, report.ToString());
	}
}
