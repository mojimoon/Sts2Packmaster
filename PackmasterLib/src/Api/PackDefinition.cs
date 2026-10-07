using MegaCrit.Sts2.Core.Models;

namespace Sts2Packmaster.Lib.Api;

/// <summary>
/// Declares one card pack. Packs are registered per character via
/// <see cref="PackCharacterRegistration.Packs"/> during the mod initializer.
/// </summary>
public sealed class PackDefinition
{
	/// <summary>Stable identifier used in save data (per-character unique). Lowercase snake_case recommended.</summary>
	public required string Id { get; init; }

	/// <summary>
	/// Localization key resolving to the display name, e.g. <c>"cards:my_pack_preview.title"</c>.
	/// Provide the value through <see cref="PackmasterApi.AddLoc"/> (or a pck localization table).
	/// </summary>
	public required string NameKey { get; init; }

	/// <summary>
	/// Localization key resolving to the pack description shown on the pack preview card.
	/// Mod authors should include their star ratings here (shown during the pick-a-pack screen).
	/// </summary>
	public required string DescriptionKey { get; init; }

	/// <summary>Author shown in logs.</summary>
	public string Author = "";

	/// <summary>The card models contained in this pack (must be registered with ModelDb, i.e. live in your mod assembly).</summary>
	public required IReadOnlyList<Type> CardTypes { get; init; }

	/// <summary>
	/// Optional preview card type shown in the "pick 1 of 3" screen. Should be a CardModel subclass registered
	/// with ModelDb whose title/description are the pack name/summary. If omitted, the pack cannot appear in
	/// ChoiceOf3 slots.
	/// </summary>
	public Type? PreviewCardType { get; init; }

	public override string ToString() => Id;
}
