using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using Sts2Packmaster.Lib.Api;

namespace Sts2Packmaster.VanillaPacks;

// ---------------------------------------------------------------------------
// Strikes pack: vanilla attack cards, behavior copied from the (sealed) originals.
// ---------------------------------------------------------------------------

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

public sealed class PackTwinStrike : PackCardModel
{
	protected override string SourcePoolTitle => "ironclad";
	protected override string SourcePortraitEntry => "twin_strike";

	protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag> { CardTag.Strike };
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar> { new DamageVar(5m, ValueProp.Move) };

	public PackTwinStrike() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target);
		await DamageCmd.Attack(DynamicVars.Damage.BaseValue).WithHitCount(2).FromCard(this, cardPlay)
			.Targeting(cardPlay.Target).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
	}

	protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2m);
}

public sealed class PackPommelStrike : PackCardModel
{
	protected override string SourcePoolTitle => "ironclad";
	protected override string SourcePortraitEntry => "pommel_strike";

	protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag> { CardTag.Strike };
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>
	{
		new DamageVar(9m, ValueProp.Move),
		new CardsVar(1),
	};

	public PackPommelStrike() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target);
		await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3").Execute(choiceContext);
		await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
	}

	protected override void OnUpgrade()
	{
		DynamicVars.Damage.UpgradeValueBy(1m);
		DynamicVars.Cards.UpgradeValueBy(1m);
	}
}

public sealed class PackIronWave : PackCardModel
{
	protected override string SourcePoolTitle => "ironclad";
	protected override string SourcePortraitEntry => "iron_wave";

	public override bool GainsBlock => true;
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>
	{
		new DamageVar(5m, ValueProp.Move),
		new BlockVar(5m, ValueProp.Move),
	};

	public PackIronWave() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target);
		await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
		await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_flying_slash").Execute(choiceContext);
	}

	protected override void OnUpgrade()
	{
		DynamicVars.Damage.UpgradeValueBy(2m);
		DynamicVars.Block.UpgradeValueBy(2m);
	}
}

public sealed class PackAnger : PackCardModel
{
	protected override string SourcePoolTitle => "ironclad";
	protected override string SourcePortraitEntry => "anger";

	protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag> { CardTag.Strike };
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar> { new DamageVar(6m, ValueProp.Move) };

	public PackAnger() : base(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target);
		await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
		CardModel card = CreateClone();
		CardCmd.PreviewCardPileAdd(await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Discard, Owner), 2.2f);
	}

	protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2m);
}

public sealed class PackThunderclap : PackCardModel
{
	protected override string SourcePoolTitle => "ironclad";
	protected override string SourcePortraitEntry => "thunderclap";

	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>
	{
		new DamageVar(4m, ValueProp.Move),
		new PowerVar<VulnerablePower>(1m),
	};
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new List<IHoverTip> { HoverTipFactory.FromPower<VulnerablePower>() };

	public PackThunderclap() : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies) { }

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).TargetingAllOpponents(CombatState)
			.WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
		await PowerCmd.Apply<VulnerablePower>(choiceContext, CombatState?.HittableEnemies, DynamicVars.Vulnerable.BaseValue, Owner.Creature, this);
	}

	protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}

public sealed class PackBash : PackCardModel
{
	protected override string SourcePoolTitle => "ironclad";
	protected override string SourcePortraitEntry => "bash";

	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>
	{
		new DamageVar(8m, ValueProp.Move),
		new PowerVar<VulnerablePower>(2m),
	};
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new List<IHoverTip> { HoverTipFactory.FromPower<VulnerablePower>() };

	public PackBash() : base(2, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy) { }

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target);
		await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3").Execute(choiceContext);
		await PowerCmd.Apply<VulnerablePower>(choiceContext, cardPlay.Target, DynamicVars.Vulnerable.BaseValue, Owner.Creature, this);
	}

	protected override void OnUpgrade()
	{
		DynamicVars.Damage.UpgradeValueBy(2m);
		DynamicVars.Vulnerable.UpgradeValueBy(1m);
	}
}
