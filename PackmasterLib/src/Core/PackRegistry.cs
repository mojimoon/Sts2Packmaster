using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using Sts2Packmaster.Lib.Api;

namespace Sts2Packmaster.Lib.Core;

/// <summary>
/// Central registry of pack characters + lazy resolution of their ModelDb instances.
/// Registrations happen during mod initializers (before ModelDb.Init); model resolution happens
/// on first use after the game booted, and is cached from then on.
/// </summary>
public static class PackRegistry
{
	private sealed class Entry
	{
		public required PackCharacterRegistration Registration;
		public CharacterModel? Character;
		public bool Resolved;
		public readonly Dictionary<Type, CardModel> CardsByType = new();
		public readonly Dictionary<CardModel, PackDefinition> PackOfCard = new();
		public readonly Dictionary<CardModel, PackDefinition> Previews = new();
		public readonly Dictionary<PackDefinition, List<CardModel>> PackCards = new();
		/// <summary>Cards defined in the character mod's assembly: they live in the character's own pool.</summary>
		public readonly List<CardModel> OwnPoolCards = new();
		/// <summary>Every non-preview card of the character (extras + all pack cards).</summary>
		public readonly List<CardModel> CharacterCards = new();
		public readonly List<CardModel> ExtraCards = new();
	}

	private static readonly object Lock = new();
	private static readonly List<Entry> Entries = new();
	private static readonly Dictionary<string, Dictionary<string, Dictionary<string, string>>> LocTables = new();

	// ---------------------------------------------------------------- registration

	public static void Register(PackCharacterRegistration registration)
	{
		lock (Lock)
		{
			if (Entries.Any(e => e.Registration.CharacterType == registration.CharacterType))
			{
				Log.Warn($"[PackmasterLib] Character {registration.CharacterType.Name} registered twice, ignoring.");
				return;
			}
			var entry = new Entry { Registration = registration };
			Entries.Add(entry);
			foreach (var pack in registration.Packs)
			{
				foreach (var cardType in pack.CardTypes)
				{
					AddCardType(entry, cardType, pack);
				}
				if (pack.PreviewCardType != null)
				{
					AddCardType(entry, pack.PreviewCardType, pack);
				}
			}
			foreach (var extra in registration.ExtraPoolCardTypes)
			{
				AddCardType(entry, extra, null);
			}
		}
	}

	/// <summary>Remove a registration (tests only).</summary>
	internal static bool Unregister(PackCharacterRegistration registration)
	{
		lock (Lock)
		{
			return Entries.RemoveAll(e => e.Registration == registration) > 0;
		}
	}

	private static void AddCardType(Entry entry, Type cardType, PackDefinition? pack)
	{
		if (!typeof(CardModel).IsAssignableFrom(cardType))
		{
			throw new ArgumentException($"[PackmasterLib] {cardType.Name} is not a CardModel subclass.");
		}
		entry.CardsByType[cardType] = null!; // placeholder until resolved
	}

	public static void AddLoc(string table, string language, IReadOnlyDictionary<string, string> entries)
	{
		lock (Lock)
		{
			if (!LocTables.TryGetValue(table, out var langs))
			{
				langs = new Dictionary<string, Dictionary<string, string>>();
				LocTables[table] = langs;
			}
			if (!langs.TryGetValue(language, out var dict))
			{
				dict = new Dictionary<string, string>();
				langs[language] = dict;
			}
			foreach (var (key, value) in entries)
			{
				dict[key] = value;
			}
		}
	}

	// ---------------------------------------------------------------- lookup

	public static IReadOnlyList<PackCharacterRegistration> Registrations
	{
		get
		{
			lock (Lock)
			{
				return Entries.Select(e => e.Registration).ToList();
			}
		}
	}

	public static PackCharacterRegistration? GetRegistration(CharacterModel? character)
	{
		if (character == null)
		{
			return null;
		}
		lock (Lock)
		{
			foreach (var e in Entries)
			{
				if (EnsureResolved(e) && e.Character == character)
				{
					return e.Registration;
				}
			}
		}
		return null;
	}

	/// <summary>All resolved pack characters (empty until ModelDb has initialized them).</summary>
	public static List<CharacterModel> GetPackCharacters()
	{
		var result = new List<CharacterModel>();
		lock (Lock)
		{
			foreach (var e in Entries)
			{
				if (EnsureResolved(e))
				{
					result.Add(e.Character!);
				}
			}
		}
		return result;
	}

	/// <summary>Instance of a registered card type, or null if not registered / not yet resolvable.</summary>
	public static CardModel? GetCard(Type cardType)
	{
		lock (Lock)
		{
			foreach (var e in Entries)
			{
				if (!e.CardsByType.ContainsKey(cardType))
				{
					continue;
				}
				EnsureResolved(e);
				return e.CardsByType.TryGetValue(cardType, out var model) ? model : null;
			}
		}
		return null;
	}

	/// <summary>
	/// Cards for the character's own CardPoolModel.GenerateAllCards(): only card types defined in the
	/// character mod's assembly (own pack cards, starters, previews). Vanilla/other-mod cards referenced
	/// by packs stay in their original pool (so they keep their frame) and are served through
	/// CardPoolModel.GetUnlockedCards by the library (see CardPoolPatch).
	/// </summary>
	public static List<CardModel> GetPoolCards(PackCharacterRegistration registration)
	{
		var e = Find(registration);
		return e == null ? new List<CardModel>() : e.OwnPoolCards.ToList();
	}

	/// <summary>Every card of the character except preview cards: extras (starters/ancients) + all pack cards.</summary>
	public static List<CardModel> GetCharacterCards(PackCharacterRegistration registration)
	{
		var e = Find(registration);
		return e == null ? new List<CardModel>() : e.CharacterCards.ToList();
	}

	/// <summary>Non-pack pool cards (starters, ancients) of the character.</summary>
	public static List<CardModel> GetExtraCards(PackCharacterRegistration registration)
	{
		var e = Find(registration);
		return e == null ? new List<CardModel>() : e.ExtraCards.ToList();
	}

	/// <summary>The (resolved) cards of a pack.</summary>
	public static IReadOnlyList<CardModel> GetPackCards(PackDefinition pack)
	{
		lock (Lock)
		{
			foreach (var e in Entries)
			{
				if (EnsureResolved(e) && e.PackCards.TryGetValue(pack, out var cards))
				{
					return cards;
				}
			}
		}
		return Array.Empty<CardModel>();
	}

	/// <summary>The pack a card belongs to for the given character (null for extras / unrelated cards).</summary>
	public static PackDefinition? GetPackOf(CardModel? card, PackCharacterRegistration? registration)
	{
		if (card == null || registration == null)
		{
			return null;
		}
		var e = Find(registration);
		return e != null && e.PackOfCard.TryGetValue(Canonical(card), out var pack) ? pack : null;
	}

	/// <summary>The pack a card belongs to for any pack character (first registration wins).</summary>
	public static PackDefinition? GetPackOf(CardModel? card)
	{
		if (card == null)
		{
			return null;
		}
		var canonical = Canonical(card);
		lock (Lock)
		{
			foreach (var e in Entries)
			{
				if (EnsureResolved(e) && e.PackOfCard.TryGetValue(canonical, out var pack))
				{
					return pack;
				}
			}
		}
		return null;
	}

	/// <summary>Is this card a pack preview card?</summary>
	public static bool IsPreviewCard(CardModel? card) => GetPreviewPack(card) != null;

	/// <summary>The pack a preview card represents.</summary>
	public static PackDefinition? GetPreviewPack(CardModel? card)
	{
		if (card == null)
		{
			return null;
		}
		var canonical = Canonical(card);
		lock (Lock)
		{
			foreach (var e in Entries)
			{
				if (EnsureResolved(e) && e.Previews.TryGetValue(canonical, out var pack))
				{
					return pack;
				}
			}
		}
		return null;
	}

	/// <summary>The preview card of a pack (null if the pack has none).</summary>
	public static CardModel? GetPreviewCard(PackDefinition pack) =>
		pack.PreviewCardType == null ? null : GetCard(pack.PreviewCardType);

	/// <summary>The cover card of a preview card's pack (explicit CoverCardType, else first Rare, else first card).</summary>
	public static CardModel? GetCoverCard(CardModel previewCard)
	{
		var pack = GetPreviewPack(previewCard);
		if (pack == null)
		{
			return null;
		}
		if (pack.CoverCardType != null && GetCard(pack.CoverCardType) is { } cover)
		{
			return cover;
		}
		var cards = GetPackCards(pack);
		return cards.FirstOrDefault(c => c.Rarity == CardRarity.Rare) ?? cards.FirstOrDefault();
	}

	/// <summary>The character model of a registration (null until ModelDb is ready).</summary>
	public static CharacterModel? GetCharacter(PackCharacterRegistration registration) => Find(registration)?.Character;

	/// <summary>The registration whose character uses this card pool, if any.</summary>
	public static PackCharacterRegistration? GetRegistrationForPool(CardPoolModel? pool)
	{
		if (pool == null)
		{
			return null;
		}
		lock (Lock)
		{
			foreach (var e in Entries)
			{
				if (EnsureResolved(e) && ReferenceEquals(e.Character!.CardPool, pool))
				{
					return e.Registration;
				}
			}
		}
		return null;
	}

	private static Entry? Find(PackCharacterRegistration registration)
	{
		lock (Lock)
		{
			var e = Entries.FirstOrDefault(x => x.Registration == registration);
			return e != null && EnsureResolved(e) ? e : null;
		}
	}

	private static CardModel Canonical(CardModel card) => card.IsCanonical ? card : ModelDb.GetById<CardModel>(card.Id);

	/// <summary>
	/// Global pack ordering index used by "sort by pack": (character registration order, pack order).
	/// Cards outside any pack sort last.
	/// </summary>
	public static int GetPackSortIndex(CardModel card)
	{
		lock (Lock)
		{
			for (var ci = 0; ci < Entries.Count; ci++)
			{
				var e = Entries[ci];
				if (!EnsureResolved(e) || !e.PackOfCard.TryGetValue(Canonical(card), out var pack))
				{
					continue;
				}
				var packs = e.Registration.Packs;
				for (var pi = 0; pi < packs.Count; pi++)
				{
					if (packs[pi] == pack)
					{
						return ci * 1000 + pi;
					}
				}
			}
		}
		return int.MaxValue;
	}

	/// <summary>Does the card's pack name contain the (lowercased) query?</summary>
	public static bool MatchPackName(CardModel card, string loweredQuery)
	{
		var pack = GetPackOf(card);
		if (pack == null)
		{
			return false;
		}
		var name = GetPackName(pack);
		return name.ToLowerInvariant().Contains(loweredQuery);
	}

	/// <summary>Localized pack display name ("table:key" syntax, falling back to game loc).</summary>
	public static string GetPackName(PackDefinition pack)
	{
		return ResolveLocKey(pack.NameKey);
	}

	public static string ResolveLocKey(string key)
	{
		var idx = key.IndexOf(':');
		if (idx <= 0)
		{
			return key;
		}
		var table = key[..idx];
		var entry = key[(idx + 1)..];
		var raw = ResolveFromInjected(table, entry, LocManager.Instance?.Language);
		if (raw != null)
		{
			return raw;
		}
		// Fall back to the game's own localization (for authors shipping a pck).
		try
		{
			var loc = LocString.GetIfExists(table, entry);
			var text = loc?.GetRawText();
			if (!string.IsNullOrWhiteSpace(text))
			{
				return text!;
			}
		}
		catch
		{
			// LocManager not ready or table missing.
		}
		return entry;
	}

	/// <summary>A fresh mutable PackRunModifier (null if ModelDb hasn't registered it yet).</summary>
	internal static PackRunModifier? CreateRunModifier()
	{
		lock (Lock)
		{
			var canonical = ResolveModel<PackRunModifier>(typeof(PackRunModifier));
			return canonical?.ToMutable() as PackRunModifier;
		}
	}

	/// <summary>Stable key for save data: the character's Id entry (e.g. "vanilla_slinger").</summary>
	public static string RegistrationKey(PackCharacterRegistration registration)
	{
		lock (Lock)
		{
			var entry = Entries.FirstOrDefault(e => e.Registration == registration);
			if (entry != null && EnsureResolved(entry) && entry.Character != null)
			{
				return entry.Character.Id.Entry;
			}
		}
		return Slug(registration.CharacterType.Name);
	}

	private static string? ResolveFromInjected(string table, string entry, string? language)
	{
		lock (Lock)
		{
			if (LocTables.TryGetValue(table, out var langs))
			{
				if (language != null && langs.TryGetValue(language, out var dict) && dict.TryGetValue(entry, out var value))
				{
					return value;
				}
				if (langs.TryGetValue("eng", out var eng) && eng.TryGetValue(entry, out var engValue))
				{
					return engValue;
				}
				foreach (var any in langs.Values)
				{
					if (any.TryGetValue(entry, out var anyValue))
					{
						return anyValue;
					}
				}
			}
		}
		return null;
	}

	/// <summary>Injected loc entries for a language (consumed by LocPatch on every language load).</summary>
	public static Dictionary<string, Dictionary<string, string>> SnapshotLocTablesFor(string language)
	{
		var result = new Dictionary<string, Dictionary<string, string>>();
		lock (Lock)
		{
			foreach (var (table, langs) in LocTables)
			{
				if (langs.TryGetValue(language, out var dict))
				{
					result[table] = new Dictionary<string, string>(dict);
				}
			}
		}
		return result;
	}

	// ---------------------------------------------------------------- resolution

	internal static string Slug(string name)
	{
		// Mirrors StringHelper.Slugify: CamelCase -> UPPER_SNAKE (Id.Entry is uppercase).
		var sb = new System.Text.StringBuilder();
		foreach (var c in name)
		{
			if (char.IsUpper(c))
			{
				if (sb.Length > 0)
				{
					sb.Append('_');
				}
				sb.Append(c);
			}
			else
			{
				sb.Append(char.ToUpperInvariant(c));
			}
		}
		return sb.ToString();
	}

	private static bool EnsureResolved(Entry entry)
	{
		if (entry.Resolved)
		{
			return entry.Character != null;
		}
		entry.Resolved = true; // don't retry every access; models appear after ModelDb.Init
		try
		{
			var character = ResolveModel<CharacterModel>(entry.Registration.CharacterType);
			if (character == null)
			{
				return false;
			}
			entry.Character = character;
			var ownAssembly = entry.Registration.CharacterType.Assembly;
			foreach (var (type, _) in entry.CardsByType.ToList())
			{
				var card = ResolveModel<CardModel>(type);
				if (card == null)
				{
					entry.CardsByType.Remove(type);
					continue;
				}
				entry.CardsByType[type] = card;
				if (type.Assembly == ownAssembly)
				{
					entry.OwnPoolCards.Add(card);
				}
			}
			foreach (var pack in entry.Registration.Packs)
			{
				var cards = new List<CardModel>();
				foreach (var type in pack.CardTypes)
				{
					if (!entry.CardsByType.TryGetValue(type, out var card))
					{
						continue;
					}
					if (entry.PackOfCard.TryGetValue(card, out var other))
					{
						Log.Warn($"[PackmasterLib] {type.Name} is in packs '{other.Id}' and '{pack.Id}'; keeping '{other.Id}'.");
						continue;
					}
					entry.PackOfCard[card] = pack;
					cards.Add(card);
				}
				entry.PackCards[pack] = cards;
				if (pack.PreviewCardType != null && entry.CardsByType.TryGetValue(pack.PreviewCardType, out var preview))
				{
					entry.Previews[preview] = pack;
				}
				ValidateDepth(entry.Registration, pack, cards);
			}
			foreach (var type in entry.Registration.ExtraPoolCardTypes)
			{
				if (entry.CardsByType.TryGetValue(type, out var card) && !entry.ExtraCards.Contains(card))
				{
					entry.ExtraCards.Add(card);
				}
			}
			entry.CharacterCards.AddRange(entry.ExtraCards);
			foreach (var card in entry.PackCards.Values.SelectMany(c => c))
			{
				if (!entry.CharacterCards.Contains(card))
				{
					entry.CharacterCards.Add(card);
				}
			}
			return true;
		}
		catch (Exception e)
		{
			Log.Warn($"[PackmasterLib] Failed to resolve {entry.Registration.CharacterType.Name}: {e.Message}");
			return false;
		}
	}

	/// <summary>STS1-style pack depth guidance: ~10 cards, 2+ of each type and of each reward rarity.</summary>
	private static void ValidateDepth(PackCharacterRegistration registration, PackDefinition pack, List<CardModel> cards)
	{
		var problems = new List<string>();
		// Multiplayer-only cards vanish from singleplayer pools, so they don't count toward depth.
		cards = cards.Where(c => c.MultiplayerConstraint != CardMultiplayerConstraint.MultiplayerOnly).ToList();
		if (cards.Count < 10)
		{
			problems.Add($"{cards.Count} cards (<10)");
		}
		foreach (var type in new[] { CardType.Attack, CardType.Skill, CardType.Power })
		{
			var n = cards.Count(c => c.Type == type);
			if (n < 2)
			{
				problems.Add($"{n} {type}");
			}
		}
		foreach (var rarity in new[] { CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare })
		{
			var n = cards.Count(c => c.Rarity == rarity);
			if (n < 2)
			{
				problems.Add($"{n} {rarity}");
			}
		}
		if (problems.Count > 0)
		{
			Log.Warn($"[PackmasterLib] Pack '{pack.Id}' of {registration.CharacterType.Name} is thin ({string.Join(", ", problems)}); "
				+ "thin pools can starve rewards and random-card effects. See PackDefinition docs.");
		}
	}

	private static T? ResolveModel<T>(Type type) where T : AbstractModel
	{
		try
		{
			// ModelDb.Get(Type) is private but is exactly the lookup we need.
			var result = Traverse.Create(typeof(ModelDb)).Method("Get", typeof(Type)).GetValue<AbstractModel>(type);
			return result as T;
		}
		catch (Exception e)
		{
			Log.Warn($"[PackmasterLib] Could not resolve model {type.Name}: {e.Message}");
			return null;
		}
	}
}
