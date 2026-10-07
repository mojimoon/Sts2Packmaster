using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using MegaCrit.Sts2.Core.ValueProps;
using Sts2Packmaster.Lib.Api;

namespace Sts2Packmaster.VanillaPacks;

// ---------------------------------------------------------------------------
// Tricks pack: cheap skills, draw/discard tools, behavior copied from the originals.
// ---------------------------------------------------------------------------

public sealed class PackNeutralize : PackCardModel
{
	protected override string SourcePoolTitle => "silent";
	protected override string SourcePortraitEntry => "neutralize";

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new List<IHoverTip> { HoverTipFactory.FromPower<WeakPower>() };
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>
	{
		new DamageVar(3m, ValueProp.Move),
		new PowerVar<WeakPower>(1m),
	};

	public PackNeutralize() : base(0, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy) { }

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target);
		NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(NThinSliceVfx.Create(cardPlay.Target));
		float delay = Owner.Character.AttackAnimDelay;
		if (SaveManager.Instance.PrefsSave.FastMode == FastModeType.Normal)
		{
			delay += 0.2f;
		}
		await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).Targeting(cardPlay.Target)
			.WithAttackerAnim("Attack", delay)
			.Execute(choiceContext);
		await PowerCmd.Apply<WeakPower>(choiceContext, cardPlay.Target, DynamicVars.Weak.BaseValue, Owner.Creature, this);
	}

	protected override void OnUpgrade()
	{
		DynamicVars.Damage.UpgradeValueBy(2m);
		DynamicVars.Weak.UpgradeValueBy(1m);
	}
}

public sealed class PackPrepared : PackCardModel
{
	protected override string SourcePoolTitle => "silent";
	protected override string SourcePortraitEntry => "prepared";

	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar> { new CardsVar(1) };

	public PackPrepared() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) { }

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		int cardCount = DynamicVars.Cards.IntValue;
		await CardPileCmd.Draw(choiceContext, cardCount, Owner);
		await CardCmd.Discard(choiceContext,
			await CardSelectCmd.FromHandForDiscard(choiceContext, Owner, new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, cardCount), null, this));
	}

	protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}

public sealed class PackAcrobatics : PackCardModel
{
	protected override string SourcePoolTitle => "silent";
	protected override string SourcePortraitEntry => "acrobatics";

	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar> { new CardsVar(3) };

	public PackAcrobatics() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
		CardModel discarded = (await CardSelectCmd.FromHandForDiscard(choiceContext, Owner,
			new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 1), null, this)).FirstOrDefault();
		if (discarded != null)
		{
			await CardCmd.Discard(choiceContext, discarded);
		}
	}

	protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}

public sealed class PackExpertise : PackCardModel
{
	protected override string SourcePoolTitle => "silent";
	protected override string SourcePortraitEntry => "expertise";

	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar> { new CardsVar(2) };

	public PackExpertise() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		foreach (var drawn in await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner))
		{
			CardCmd.ApplySingleTurnRetain(drawn);
		}
	}

	protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}

public sealed class PackReflex : PackCardModel
{
	protected override string SourcePoolTitle => "silent";
	protected override string SourcePortraitEntry => "reflex";

	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar> { new CardsVar(2) };
	public override IEnumerable<CardKeyword> CanonicalKeywords => new List<CardKeyword> { CardKeyword.Sly };

	public PackReflex() : base(3, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.AttackAnimDelay);
		await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
	}

	protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}

public sealed class PackHeadbutt : PackCardModel
{
	protected override string SourcePoolTitle => "ironclad";
	protected override string SourcePortraitEntry => "headbutt";

	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar> { new DamageVar(9m, ValueProp.Move) };

	public PackHeadbutt() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target);
		await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3").Execute(choiceContext);
		CardModel picked = (await CardSelectCmd.FromCombatPile(prefs: new CardSelectorPrefs(SelectionScreenPrompt, 1),
			context: choiceContext, pile: PileType.Discard.GetPile(Owner), player: Owner)).FirstOrDefault();
		if (picked != null)
		{
			await CardPileCmd.Add(picked, PileType.Draw, CardPilePosition.Top);
		}
	}

	protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}
