using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.ValueProps;
using Sts2Packmaster.Lib.Api;

namespace Sts2Packmaster.VanillaPacks;

// ---------------------------------------------------------------------------
// Powers pack: ramping powers and enablers, behavior copied from the originals.
// ---------------------------------------------------------------------------

public sealed class PackInflame : PackCardModel
{
	protected override string SourcePoolTitle => "ironclad";
	protected override string SourcePortraitEntry => "inflame";

	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar> { new PowerVar<StrengthPower>(2m) };
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new List<IHoverTip> { HoverTipFactory.FromPower<StrengthPower>() };
	protected override IEnumerable<string> ExtraRunAssetPaths => NGroundFireVfx.AssetPaths;

	public PackInflame() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		NPowerUpVfx.CreateNormal(Owner.Creature);
		await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, DynamicVars["StrengthPower"].BaseValue, Owner.Creature, this);
	}

	public override async Task OnEnqueuePlayVfx(Creature? target)
	{
		NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(NGroundFireVfx.Create(Owner.Creature));
		await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
	}

	protected override void OnUpgrade() => DynamicVars["StrengthPower"].UpgradeValueBy(1m);
}

public sealed class PackRupture : PackCardModel
{
	protected override string SourcePoolTitle => "ironclad";
	protected override string SourcePortraitEntry => "rupture";

	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar> { new PowerVar<StrengthPower>(1m) };
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new List<IHoverTip> { HoverTipFactory.FromPower<StrengthPower>() };

	public PackRupture() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await PowerCmd.Apply<RupturePower>(choiceContext, Owner.Creature, DynamicVars.Strength.BaseValue, Owner.Creature, this);
	}

	protected override void OnUpgrade() => DynamicVars.Strength.UpgradeValueBy(1m);
}

public sealed class PackBattleTrance : PackCardModel
{
	protected override string SourcePoolTitle => "ironclad";
	protected override string SourcePortraitEntry => "battle_trance";

	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar> { new CardsVar(3) };

	public PackBattleTrance() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
		await PowerCmd.Apply<NoDrawPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
	}

	protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}

public sealed class PackBloodletting : PackCardModel
{
	protected override string SourcePoolTitle => "ironclad";
	protected override string SourcePortraitEntry => "bloodletting";

	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar>
	{
		new HpLossVar(3m),
		new EnergyVar(2),
	};
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new List<IHoverTip> { EnergyHoverTip };

	public PackBloodletting() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);
		VfxCmd.PlayOnCreatureCenter(Owner.Creature, "vfx/vfx_bloody_impact");
		await CreatureCmd.Damage(choiceContext, Owner.Creature, DynamicVars.HpLoss.BaseValue,
			ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move, this, cardPlay);
		await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
	}

	protected override void OnUpgrade() => DynamicVars.Energy.UpgradeValueBy(1m);
}

public sealed class PackJuggernaut : PackCardModel
{
	protected override string SourcePoolTitle => "ironclad";
	protected override string SourcePortraitEntry => "juggernaut";

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new List<IHoverTip> { HoverTipFactory.Static(StaticHoverTip.Block) };
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar> { new PowerVar<JuggernautPower>(6m) };

	public PackJuggernaut() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
		await PowerCmd.Apply<JuggernautPower>(choiceContext, Owner.Creature, DynamicVars["JuggernautPower"].BaseValue, Owner.Creature, this);
	}

	protected override void OnUpgrade() => DynamicVars["JuggernautPower"].UpgradeValueBy(2m);
}

public sealed class PackDemonForm : PackCardModel
{
	protected override string SourcePoolTitle => "ironclad";
	protected override string SourcePortraitEntry => "demon_form";

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new List<IHoverTip> { HoverTipFactory.FromPower<StrengthPower>() };
	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar> { new PowerVar<StrengthPower>(3m) };

	public PackDemonForm() : base(3, CardType.Power, CardRarity.Rare, TargetType.Self) { }

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
		await PowerCmd.Apply<DemonFormPower>(choiceContext, Owner.Creature, DynamicVars["StrengthPower"].BaseValue, Owner.Creature, this);
	}

	protected override void OnUpgrade() => DynamicVars["StrengthPower"].UpgradeValueBy(1m);
}

public sealed class PackCorruption : PackCardModel
{
	protected override string SourcePoolTitle => "ironclad";
	protected override string SourcePortraitEntry => "corruption";

	private const string PowerVarName = "Power";

	protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar> { new DynamicVar(PowerVarName, 1m) };
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new List<IHoverTip> { HoverTipFactory.FromKeyword(CardKeyword.Exhaust) };

	public PackCorruption() : base(3, CardType.Power, CardRarity.Ancient, TargetType.Self) { }

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		NPowerUpVfx.CreateNormal(Owner.Creature);
		await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
		await PowerCmd.Apply<CorruptionPower>(choiceContext, Owner.Creature, DynamicVars[PowerVarName].BaseValue, Owner.Creature, this);
	}

	protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
