namespace Sts2Packmaster.VanillaPacks;

/// <summary>Demo localization ("cards" table): starter copies + pack preview cards. zh-CN and English.</summary>
public static class VanillaPackLoc
{
	// Pack preview entries: Id.Entry of the preview class → (name, description).
	private static readonly (string Entry, string NameEn, string DescEn, string NameZhs, string DescZhs)[] Packs =
	{
		("PAIN_PACK_PREVIEW", "Pain Ignorance", "Trade your own HP for Strength and Energy.",
			"疼痛无视", "以自身生命为代价，换取力量与能量。"),
		("IRON_WALL_PACK_PREVIEW", "Iron Wall", "Stack Block, then turn it into damage.",
			"铜墙铁壁", "层层叠加格挡，再把格挡化为伤害。"),
		("DOOM_PACK_PREVIEW", "Doom Approaches", "Pile up Doom until enemies perish at the end of the turn.",
			"灾厄将至", "不断叠加灾厄，让敌人在回合结束时殒命。"),
		("BONE_FRIENDS_PACK_PREVIEW", "Bone Friends", "Summon and empower Osty to fight for you.",
			"骸骨之友", "召唤并强化奥斯提，让他替你战斗。"),
		("POISON_PACK_PREVIEW", "Toxic Outbreak", "Poison everything and let it tick.",
			"毒性爆发", "给敌人层层施毒，让毒伤不断累积。"),
		("PHANTOM_BLADES_PACK_PREVIEW", "Phantom Blades", "Flood the hand with Shivs and win by numbers.",
			"幻影之刃", "制造大量小刀，以数量取胜。"),
		("CHAOS_PACK_PREVIEW", "Primordial Chaos", "Conjure colorless and random cards out of thin air.",
			"混沌之初", "凭空生成无色牌与随机卡牌，混沌之中自有秩序。"),
		("KINGDOM_ARMS_PACK_PREVIEW", "Kingdom's Arms", "Forge and swing the Sovereign Blade.",
			"王国兵器", "铸造并挥舞君王之剑。"),
		("STARLIGHT_PACK_PREVIEW", "Starlight", "Gather Stars, then spend them all at once.",
			"星辰之力", "积攒星辉，再一口气释放。"),
		("LIGHTNING_PACK_PREVIEW", "Lightning Storm", "Channel Lightning and keep zapping.",
			"闪电风暴", "生成闪电充能球，持续轰击敌人。"),
		("FROST_PACK_PREVIEW", "Frost Fortress", "Channel Frost and outlast the enemy.",
			"冰霜堡垒", "生成冰霜充能球，以坚固的防御拖垮敌人。"),
	};

	public static readonly IReadOnlyDictionary<string, string> CardsEn = Build(english: true);

	public static readonly IReadOnlyDictionary<string, string> CardsZhs = Build(english: false);

	private static Dictionary<string, string> Build(bool english)
	{
		var d = new Dictionary<string, string>
		{
			["PACK_STRIKE.title"] = english ? "Strike" : "打击",
			["PACK_STRIKE.description"] = english ? "Deal {Damage:diff()} damage." : "造成{Damage:diff()}点伤害。",
			["PACK_DEFEND.title"] = english ? "Defend" : "防御",
			["PACK_DEFEND.description"] = english ? "Gain {Block:diff()} [gold]Block[/gold]." : "获得{Block:diff()}点[gold]格挡[/gold]。",
		};
		foreach (var p in Packs)
		{
			d[p.Entry + ".title"] = english ? p.NameEn : p.NameZhs;
			d[p.Entry + ".description"] = english ? p.DescEn : p.DescZhs;
		}
		return d;
	}
}
