using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using Sts2Packmaster.Lib.Core;

namespace Sts2Packmaster.Lib.Api;

/// <summary>
/// Public entry point for character mod authors.
/// Call <see cref="RegisterCharacter"/> (and optionally <see cref="AddLoc"/>) during your
/// <c>[ModInitializer]</c> method. Everything is lazy: nothing touches ModelDb until the game
/// has finished initializing it.
/// </summary>
public static class PackmasterApi
{
	/// <summary>
	/// Register a card-pack character. Must be called before the run starts (mod initializer is the right place).
	/// </summary>
	public static void RegisterCharacter(PackCharacterRegistration registration)
	{
		PackRegistry.Register(registration);
		Log.Info($"[PackmasterLib] Registered pack character '{registration.CharacterType.Name}' with {registration.Packs.Count} packs.");
	}

	/// <summary>
	/// Inject english localization entries without shipping a pck. Merged into the game's tables
	/// whenever that language loads. Common tables: "cards", "characters", "gameplay_ui",
	/// "card_library", "card_selection". Call during your mod initializer.
	/// </summary>
	public static void AddLoc(string table, IReadOnlyDictionary<string, string> entries)
	{
		AddLoc(table, "eng", entries);
	}

	/// <summary>Inject localization entries for a specific language code (e.g. "zhs", "eng").</summary>
	public static void AddLoc(string table, string language, IReadOnlyDictionary<string, string> entries)
	{
		PackRegistry.AddLoc(table, language, entries);
	}

	/// <summary>Is this character registered as a pack character?</summary>
	public static bool IsPackCharacter(CharacterModel character) => PackRegistry.GetRegistration(character) != null;

	/// <summary>The packs registered for a character (empty if not a pack character).</summary>
	public static IReadOnlyList<PackDefinition> GetPacks(CharacterModel character)
	{
		return PackRegistry.GetRegistration(character)?.Packs ?? Array.Empty<PackDefinition>();
	}
}
