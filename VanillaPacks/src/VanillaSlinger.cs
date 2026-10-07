using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Models.Relics;
using Sts2Packmaster.Lib.Api;

namespace Sts2Packmaster.VanillaPacks;

/// <summary>
/// Demo pack character. Reuses Ironclad's visuals/icons/sfx via RedirectedCharacterModel
/// (no custom art needed); relic/potion pools are shared with Ironclad by author choice.
/// </summary>
public sealed class VanillaSlinger : RedirectedCharacterModel
{
	protected override string AssetSourceEntry => "ironclad";

	public override Color NameColor => new Color("8a6f4d");

	public override CharacterGender Gender => CharacterGender.Neutral;

	protected override CharacterModel? UnlocksAfterRunAs => null;

	public override int StartingHp => 72;

	public override int StartingGold => 99;

	public override CardPoolModel CardPool => ModelDb.CardPool<VanillaSlingerCardPool>();

	public override RelicPoolModel RelicPool => ModelDb.RelicPool<IroncladRelicPool>();

	public override PotionPoolModel PotionPool => ModelDb.PotionPool<IroncladPotionPool>();

	public override IEnumerable<CardModel> StartingDeck => new List<CardModel>
	{
		ModelDb.Card<PackStrike>(),
		ModelDb.Card<PackStrike>(),
		ModelDb.Card<PackStrike>(),
		ModelDb.Card<PackStrike>(),
		ModelDb.Card<PackDefend>(),
		ModelDb.Card<PackDefend>(),
		ModelDb.Card<PackDefend>(),
		ModelDb.Card<PackDefend>(),
		ModelDb.Card<MegaCrit.Sts2.Core.Models.Cards.Bash>(),
		ModelDb.Card<MegaCrit.Sts2.Core.Models.Cards.Neutralize>(),
	};

	public override IReadOnlyList<RelicModel> StartingRelics => new List<RelicModel> { ModelDb.Relic<BurningBlood>() };

	public override float AttackAnimDelay => 0.15f;

	public override float CastAnimDelay => 0.25f;

	public override Color MapDrawingColor => new Color("8a6f4d");

	public override List<string> GetArchitectAttackVfx() => new List<string>();
}
