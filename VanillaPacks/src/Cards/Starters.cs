using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.ValueProps;
using Sts2Packmaster.Lib.Api;

namespace Sts2Packmaster.VanillaPacks;

// Starter cards of the Vanilla Slinger. Like STS1 Packmaster's basics they belong to no pack;
// they live in the character's own pool (own frame) and reuse Ironclad's art.

public sealed class PackStrike : PackCardModel
{
	protected override string SourcePoolTitle => "ironclad";
	protected override string SourcePortraitEntry => "strike_ironclad";

	protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag> { CardTag.Strike };
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar> { new DamageVar(6m, ValueProp.Move) };

	public PackStrike() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy) { }

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
			.FromCard(this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
	}

	protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}

public sealed class PackDefend : PackCardModel
{
	protected override string SourcePoolTitle => "ironclad";
	protected override string SourcePortraitEntry => "defend_ironclad";

	public override bool GainsBlock => true;
	protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag> { CardTag.Defend };
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar> { new BlockVar(5m, ValueProp.Move) };

	public PackDefend() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self) { }

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
	}

	protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3m);
}
