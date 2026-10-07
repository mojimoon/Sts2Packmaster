using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.ValueProps;
using Sts2Packmaster.Lib.Api;

namespace Sts2Packmaster.VanillaPacks;

// ---------------------------------------------------------------------------
// Defends pack: vanilla block cards, behavior copied from the (sealed) originals.
// ---------------------------------------------------------------------------

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

public sealed class PackShrugItOff : PackCardModel
{
	protected override string SourcePoolTitle => "ironclad";
	protected override string SourcePortraitEntry => "shrug_it_off";

	public override bool GainsBlock => true;
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>
	{
		new BlockVar(8m, ValueProp.Move),
		new CardsVar(1),
	};

	public PackShrugItOff() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
		await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
	}

	protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3m);
}

public sealed class PackBackflip : PackCardModel
{
	protected override string SourcePoolTitle => "silent";
	protected override string SourcePortraitEntry => "backflip";

	public override bool GainsBlock => true;
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>
	{
		new BlockVar(5m, ValueProp.Move),
		new CardsVar(2),
	};

	public PackBackflip() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
		await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
	}

	protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3m);
}

public sealed class PackArmaments : PackCardModel
{
	protected override string SourcePoolTitle => "ironclad";
	protected override string SourcePortraitEntry => "armaments";

	public override bool GainsBlock => true;
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar> { new BlockVar(5m, ValueProp.Move) };

	public PackArmaments() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
		if (IsUpgraded)
		{
			foreach (var card in PileType.Hand.GetPile(Owner).Cards.Where(c => c.IsUpgradable))
			{
				CardCmd.Upgrade(card);
			}
			return;
		}
		CardModel upgraded = await CardSelectCmd.FromHandForUpgrade(choiceContext, Owner, this);
		if (upgraded != null)
		{
			CardCmd.Upgrade(upgraded);
		}
	}
}

public sealed class PackFlameBarrier : PackCardModel
{
	protected override string SourcePoolTitle => "defect";
	protected override string SourcePortraitEntry => "flame_barrier";

	private const string DamageBackKey = "DamageBack";

	public override bool GainsBlock => true;
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>
	{
		new BlockVar(12m, ValueProp.Move),
		new DynamicVar(DamageBackKey, 4m),
	};

	public PackFlameBarrier() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		NFireBurningVfx child = NFireBurningVfx.Create(Owner.Creature, 0.75f, goingRight: false);
		NCombatRoom.Instance?.CombatVfxContainer.AddChild(child);
		await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
		await PowerCmd.Apply<FlameBarrierPower>(choiceContext, Owner.Creature, DynamicVars[DamageBackKey].BaseValue, Owner.Creature, this);
	}

	protected override void OnUpgrade()
	{
		DynamicVars.Block.UpgradeValueBy(4m);
		DynamicVars[DamageBackKey].UpgradeValueBy(2m);
	}
}

public sealed class PackSecondWind : PackCardModel
{
	protected override string SourcePoolTitle => "ironclad";
	protected override string SourcePortraitEntry => "second_wind";

	public override bool GainsBlock => true;
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar> { new BlockVar(5m, ValueProp.Move) };
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new List<IHoverTip> { HoverTipFactory.FromKeyword(CardKeyword.Exhaust) };

	public PackSecondWind() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);
		foreach (var card in GetCards().ToList())
		{
			await CardCmd.Exhaust(choiceContext, card);
			await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
		}
	}

	protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(2m);

	private IEnumerable<CardModel> GetCards()
	{
		CardPile pile = PileType.Hand.GetPile(Owner);
		return pile.Cards.Where(c => c.Type != CardType.Attack);
	}
}

public sealed class PackEscapePlan : PackCardModel
{
	protected override string SourcePoolTitle => "silent";
	protected override string SourcePortraitEntry => "escape_plan";

	public override bool GainsBlock => true;
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar> { new BlockVar(3m, ValueProp.Move) };

	public PackEscapePlan() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		CardModel drawn = (await CardPileCmd.Draw(choiceContext, 1m, Owner)).FirstOrDefault();
		if (drawn != null && drawn.Type == CardType.Skill)
		{
			await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
		}
	}

	protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(2m);
}
