using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Sts2Packmaster.Lib.Api;

/// <summary>
/// Base class for cards that belong to a pack but reuse vanilla card art.
/// Subclasses copy the vanilla card's behavior (vanilla card classes are sealed) and point
/// <see cref="SourcePoolTitle"/>/<see cref="SourcePortraitEntry"/> at the original art.
/// </summary>
public abstract class PackCardModel : CardModel
{
	/// <summary>The pool folder of the original art, e.g. "ironclad".</summary>
	protected abstract string SourcePoolTitle { get; }

	/// <summary>The original card's atlas entry, e.g. "twin_strike".</summary>
	protected abstract string SourcePortraitEntry { get; }

	public override string PortraitPath => ImageHelper.GetImagePath($"atlases/card_atlas.sprites/{SourcePoolTitle}/{SourcePortraitEntry}.tres");

	public override string BetaPortraitPath => ImageHelper.GetImagePath($"atlases/card_atlas.sprites/colorless/beta.tres");

	protected override string PortraitPngPath => ImageHelper.GetImagePath($"packed/card_portraits/colorless/beta.png");

	protected PackCardModel(int energyCost, CardType type, CardRarity rarity, TargetType targetType, bool shouldShowInCardLibrary = true)
		: base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
	{
	}
}

/// <summary>
/// Base class for the "pick a pack" preview card of a pack. Shows the pack name (title) and the
/// pack summary / star ratings (description) provided through localization.
/// </summary>
public abstract class PackPreviewCard : CardModel
{
	protected PackPreviewCard()
		: base(0, CardType.Skill, CardRarity.Basic, TargetType.Self, shouldShowInCardLibrary: false)
	{
	}

	public override bool CanBeGeneratedInCombat => false;

	public override bool CanBeGeneratedByModifiers => false;

	public override int MaxUpgradeLevel => 0;

	/// <summary>The cover card's art (see <see cref="PackDefinition.CoverCardType"/>), else the beta placeholder.</summary>
	public override string PortraitPath =>
		Sts2Packmaster.Lib.Core.PackRegistry.GetCoverCard(this)?.PortraitPath
		?? ImageHelper.GetImagePath("atlases/card_atlas.sprites/colorless/beta.tres");

	public override string BetaPortraitPath => ImageHelper.GetImagePath("atlases/card_atlas.sprites/colorless/beta.tres");

	protected override string PortraitPngPath => ImageHelper.GetImagePath("packed/card_portraits/colorless/beta.png");

	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		// Preview cards are never played.
		return Task.CompletedTask;
	}
}
