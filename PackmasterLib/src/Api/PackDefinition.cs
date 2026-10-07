namespace Sts2Packmaster.Lib.Api;

/// <summary>
/// Declares one card pack. Packs are registered per character via
/// <see cref="PackCharacterRegistration.Packs"/> during the mod initializer.
/// <para>
/// Pool depth: the game rolls rewards, shops and "random card" effects only from the selected
/// packs, so thin packs can starve those rolls (e.g. a 3-card reward with only 2 eligible cards,
/// or a "random Power" effect with no Power in the pool), which can crash the game. Like STS1
/// Packmaster, aim for ~10+ cards per pack with at least 2 Attacks, 2 Skills and 2 Powers, and at
/// least 2 Common, 2 Uncommon and 2 Rare cards. The library logs a warning for packs below that.
/// </para>
/// </summary>
public sealed class PackDefinition
{
	/// <summary>Stable identifier used in save data (per-character unique). Lowercase snake_case recommended.</summary>
	public required string Id { get; init; }

	/// <summary>
	/// Localization key resolving to the display name, e.g. <c>"cards:MY_PACK_PREVIEW.title"</c>
	/// ("table:key"; plain text without a colon is used as-is).
	/// </summary>
	public required string NameKey { get; init; }

	/// <summary>Localization key of the pack description (shown on the pack preview card).</summary>
	public required string DescriptionKey { get; init; }

	/// <summary>Author shown on the pack preview card and in its tooltip.</summary>
	public string Author = "";

	/// <summary>Optional credits line (loc key or plain text) shown in the preview tooltip.</summary>
	public string? CreditsKey { get; init; }

	/// <summary>
	/// The cards of this pack: any CardModel types registered with ModelDb — your own cards or
	/// vanilla/other-mod cards referenced directly (they keep their original pool, frame and art,
	/// like base-game cards in STS1 Packmaster). A card should belong to at most one pack per character.
	/// </summary>
	public required IReadOnlyList<Type> CardTypes { get; init; }

	/// <summary>
	/// Preview card shown on the pack setup screen (a <see cref="PackPreviewCard"/> subclass in your
	/// mod; its title/description should be the pack name/description). Packs without one can still
	/// be fixed/random but are never offered in "choice of 3" slots.
	/// </summary>
	public Type? PreviewCardType { get; init; }

	/// <summary>Card whose portrait the preview card shows (default: the pack's first Rare card).</summary>
	public Type? CoverCardType { get; init; }

	/// <summary>Draw weight for random slots and choice offers (default drawer). Default 1; 0 = only when nothing else is left.</summary>
	public double Weight { get; init; } = 1.0;

	/// <summary>Ratings and tags shown when hovering the pack (STS1 "pack summary").</summary>
	public PackSummary Summary { get; init; } = new();

	public override string ToString() => Id;
}

/// <summary>
/// STS1 Packmaster's pack summary: five 0-5 star ratings plus tags, shown in the pack tooltip.
/// </summary>
public sealed class PackSummary
{
	public int Offense { get; init; }
	public int Defense { get; init; }
	public int Support { get; init; }
	public int Frontload { get; init; }
	public int Scaling { get; init; }

	/// <summary>
	/// Tags: one of the library tags (<see cref="PackTags"/>), a loc key ("table:key") or plain text.
	/// </summary>
	public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}

/// <summary>Tags the library ships translations for (STS1 tags + STS2 mechanics).</summary>
public static class PackTags
{
	public const string Strength = "Strength";
	public const string Exhaust = "Exhaust";
	public const string Orbs = "Orbs";
	public const string Discard = "Discard";
	public const string Debuffs = "Debuffs";
	public const string Attacks = "Attacks";
	public const string Tokens = "Tokens";
	public const string Powers = "Powers";
	public const string Block = "Block";
	public const string Poison = "Poison";
	public const string Shivs = "Shivs";
	public const string Doom = "Doom";
	public const string Summon = "Summon";
	public const string Stars = "Stars";
	public const string Forge = "Forge";
	public const string SelfDamage = "SelfDamage";
	public const string Draw = "Draw";
	public const string Energy = "Energy";
	public const string Generation = "Generation";

	public static readonly IReadOnlyList<string> All = new[]
	{
		Strength, Exhaust, Orbs, Discard, Debuffs, Attacks, Tokens, Powers, Block, Poison, Shivs,
		Doom, Summon, Stars, Forge, SelfDamage, Draw, Energy, Generation,
	};
}
