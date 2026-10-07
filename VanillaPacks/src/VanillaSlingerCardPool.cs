using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Unlocks;
using Sts2Packmaster.Lib.Core;

namespace Sts2Packmaster.VanillaPacks;

/// <summary>
/// Card pool of the Vanilla Slinger. Lists every pack card + preview (the frame/energy colors
/// are author choices; here we reuse Ironclad's). Preview cards are filtered out of reward rolls.
/// </summary>
public sealed class VanillaSlingerCardPool : CardPoolModel
{
	public override string Title => "vanilla_slinger";

	public override string EnergyColorName => "ironclad";

	public override string CardFrameMaterialPath => "card_frame_red";

	public override Color DeckEntryCardColor => new Color("8a6f4d");

	public override bool IsColorless => false;

	protected override CardModel[] GenerateAllCards()
	{
		return VanillaPacksEntry.Registration == null
			? Array.Empty<CardModel>()
			: PackRegistry.GetPoolCards(VanillaPacksEntry.Registration).ToArray();
	}

	protected override IEnumerable<CardModel> FilterThroughEpochs(UnlockState unlockState, IEnumerable<CardModel> cards)
	{
		return cards.Where(c => !PackRegistry.IsPreviewCard(c));
	}
}
