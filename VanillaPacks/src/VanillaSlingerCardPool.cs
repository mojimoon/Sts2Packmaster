using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Unlocks;
using Sts2Packmaster.Lib.Core;

namespace Sts2Packmaster.VanillaPacks;

/// <summary>
/// Card pool of the Vanilla Slinger. GenerateAllCards lists only the mod's own cards (starter copies,
/// pack previews); the vanilla pack cards stay in their original pools and are added to this pool's
/// GetUnlockedCards by PackmasterLib. The colorless frame is what "One frame for all" applies.
/// </summary>
public sealed class VanillaSlingerCardPool : CardPoolModel
{
	public override string Title => "vanilla_slinger";

	public override string EnergyColorName => "colorless";

	public override string CardFrameMaterialPath => "card_frame_colorless";

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
