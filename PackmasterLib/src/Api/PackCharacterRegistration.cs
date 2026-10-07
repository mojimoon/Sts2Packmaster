namespace Sts2Packmaster.Lib.Api;

/// <summary>
/// A slot token as written in <see cref="PackCharacterRegistration.DefaultSlots"/> and the character-select config.
/// Valid tokens: <c>"random"</c>, <c>"choice"</c>, <c>"none"</c>, or a pack id.
/// </summary>
public static class PackSlotToken
{
	public const string Random = "random";
	public const string Choice = "choice";
	public const string None = "none";

	public static PackSlotKind KindOf(string token, out string? fixedPackId)
	{
		switch (token)
		{
			case Random:
				fixedPackId = null;
				return PackSlotKind.Random;
			case Choice:
				fixedPackId = null;
				return PackSlotKind.ChoiceOf3;
			case None:
				fixedPackId = null;
				return PackSlotKind.None;
			default:
				fixedPackId = token;
				return PackSlotKind.Fixed;
		}
	}
}

/// <summary>
/// Registers a character as a "pack character" with PackmasterLib. Call
/// <see cref="PackmasterApi.RegisterCharacter"/> during your mod initializer.
/// </summary>
public sealed class PackCharacterRegistration
{
	/// <summary>Your CharacterModel subclass (must be in your mod assembly so ModelDb auto-registers it).</summary>
	public required Type CharacterType { get; init; }

	/// <summary>The card packs offered for this character.</summary>
	public required IReadOnlyList<PackDefinition> Packs { get; init; }

	/// <summary>
	/// Cards in the character's pool that belong to no pack, like STS1 Packmaster's basics: starting
	/// deck cards and Ancient cards (Dusty Tome picks a random non-transcendence Ancient from the
	/// pool and crashes if there is none, so register at least one). Own or vanilla types.
	/// Basic/Ancient rarity keeps them out of normal reward rolls.
	/// </summary>
	public IReadOnlyList<Type> ExtraPoolCardTypes { get; init; } = Array.Empty<Type>();

	/// <summary>
	/// Default slot configuration used when the player has not configured anything (and in multiplayer).
	/// Tokens: "random" / "choice" / "none" / a pack id. 3-10 entries.
	/// </summary>
	public required string[] DefaultSlots { get; init; }

	/// <summary>
	/// Pack unlock rule. Null (default) = every pack is unlocked. Locked packs are not offered by
	/// random/choice slots, all-packs mode or the config UI. Settable any time, so other mods can
	/// change it too; the developer setting <see cref="PackmasterSettings.UnlockAllPacks"/> overrides it.
	/// </summary>
	public Func<PackDefinition, bool>? IsPackUnlocked { get; set; }

	/// <summary>The packs currently unlocked (see <see cref="IsPackUnlocked"/>).</summary>
	public IReadOnlyList<PackDefinition> UnlockedPacks =>
		PackmasterSettings.UnlockAllPacks || IsPackUnlocked == null ? Packs : Packs.Where(IsPackUnlocked).ToList();
}
