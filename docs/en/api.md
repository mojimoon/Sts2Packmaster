# API reference

[中文](../zh/api.md) · [Getting started](getting-started.md) · [In-game behavior](behavior.md)

Namespaces: `Sts2Packmaster.Lib.Api` (public API), `Sts2Packmaster.Lib.Core` (registry and run
state queries). Everything below is public and safe to call from other mods.

## PackmasterApi

| Member | Description |
|---|---|
| `RegisterCharacter(PackCharacterRegistration)` | Registers a pack character. Call in your `[ModInitializer]`. Lazy: card types are resolved once ModelDb is ready. |
| `AddLoc(table, entries)` | Adds English (`eng`) localization entries. |
| `AddLoc(table, language, entries)` | Adds entries for one language code. Merged into the game's table whenever that language loads. Common tables: `cards`, `characters`, `gameplay_ui`, `card_library`, `card_selection`. |
| `IsPackCharacter(CharacterModel)` | Is the character registered with PackmasterLib? |
| `GetPacks(CharacterModel)` | The character's packs (empty if not a pack character). |

## PackCharacterRegistration

| Property | Default | Description |
|---|---|---|
| `CharacterType` *(required)* | | Your `CharacterModel` subclass. |
| `Packs` *(required)* | | The character's `PackDefinition`s. |
| `DefaultSlots` *(required)* | | Slot tokens used until the player configures the character, and always in multiplayer. 3–10 entries. Tokens: `"random"`, `"choice"`, `"none"` or a pack id (constants in `PackSlotToken`). |
| `ExtraPoolCardTypes` | empty | Cards in the character's pool that belong to no pack: starters and Ancients (include ≥ 1 non-transcendence Ancient for Dusty Tome). Own or vanilla types. |
| `ChoiceSize` | 3 | Packs offered per draft round. |
| `Drawer` | `null` | Custom `IPackDrawer`; `null` = `WeightedPackDrawer`. Settable at any time. |
| `IsPackUnlocked` | `null` | `Func<PackDefinition, bool>` unlock rule; `null` = all unlocked. Settable at any time, by any mod. |
| `UnlockedPacks` | | Read-only: packs that currently pass `IsPackUnlocked` (all packs when the *Unlock every pack* dev setting is on). |

Locked packs are not offered by random or choice slots, are not part of *All Packs* mode and are
not listed in the character-select config. A fixed slot naming a locked pack is skipped.

## PackDefinition

| Property | Default | Description |
|---|---|---|
| `Id` *(required)* | | Stable id stored in saves and configs; unique per character. Lowercase snake_case recommended. **Do not rename after release.** |
| `NameKey` *(required)* | | Display name: `"table:key"` loc key or plain text. |
| `DescriptionKey` *(required)* | | Description: `"table:key"` or plain text. |
| `CardTypes` *(required)* | | The pack's cards: own, vanilla or other-mod `CardModel` types. |
| `Author` | `""` | Shown under the preview card and in its tooltip. |
| `CreditsKey` | `null` | Optional credits line in the tooltip (loc key or text). |
| `PreviewCardType` | `null` | `PackPreviewCard` subclass for the draft screen. Without it the pack is never offered in a draft. |
| `CoverCardType` | first Rare | Card whose portrait the preview card shows. |
| `Weight` | 1.0 | Draw weight for the default drawer. `0` = drawn only when nothing else is left. |
| `Summary` | empty | `PackSummary` shown in the tooltip. |

The same `PackDefinition` instance can be put in several characters' `Packs` (shared packs). Each
character draws from its own list; a card can belong to different packs for different characters,
but to at most one pack per character.

## PackSummary and PackTags

`PackSummary { Offense, Defense, Support, Frontload, Scaling }` are 0–5 star ratings (STS1's pack
summary), plus `Tags` — each tag is a `PackTags` constant (translated by the library), a
`"table:key"` loc key or plain text.

`PackTags`: `Strength`, `Exhaust`, `Orbs`, `Discard`, `Debuffs`, `Attacks`, `Tokens`, `Powers`,
`Block`, `Poison`, `Shivs`, `Doom`, `Summon`, `Stars`, `Forge`, `SelfDamage`, `Draw`, `Energy`,
`Generation` (`PackTags.All` lists them).

Players can hide the ratings with *Show pack ratings* on the draft screen.

## Drawing: IPackDrawer

```csharp
public interface IPackDrawer
{
    PackDefinition DrawRandomSlot(PackDrawContext context);                              // one pack for a "random" slot
    IReadOnlyList<PackDefinition> DrawChoiceOffer(PackDrawContext context, int count);   // a draft offer, left to right
}
```

`PackDrawContext`:

| Property | Description |
|---|---|
| `Registration` | The character. |
| `Candidates` | Packs you may return (unlocked, not in the run, excluding passed-over packs per the draft rule). |
| `Selected` | Packs already in the run, in order. |
| `Round` | 0-based draft round; `-1` for a random slot. |
| `Rng` | The only randomness you may use. Draws must be deterministic for save/load and multiplayer. |

The library sanitizes the result: packs outside `Candidates` and duplicates are dropped and
missing ones are filled by the default drawer. Subclass `WeightedPackDrawer` (methods are
virtual) or call the static `WeightedPackDrawer.Draw(candidates, count, rng)` to reuse weighted
sampling without replacement.

Example — the left pack of each offer comes from a guaranteed list:

```csharp
sealed class PityDrawer : WeightedPackDrawer
{
    public override IReadOnlyList<PackDefinition> DrawChoiceOffer(PackDrawContext ctx, int count)
    {
        var first = ctx.Candidates.FirstOrDefault(p => p.Id == "my_signature_pack");
        if (first == null) return base.DrawChoiceOffer(ctx, count);
        var rest = Draw(ctx.Candidates.Where(p => p != first).ToList(), count - 1, ctx.Rng);
        return new[] { first }.Concat(rest).ToList();
    }
}

registration.Drawer = new PityDrawer();
```

## Card base classes

| Class | Description |
|---|---|
| `PackPreviewCard` | Base for a pack's preview card. An empty subclass is enough. Title/description come from loc (`<ID>.title` / `<ID>.description` in `cards`), portrait from the cover card. Hidden from the library, never generated, never played. |
| `PackCardModel` | Base for your own cards that reuse vanilla art: override `SourcePoolTitle` (e.g. `"ironclad"`) and `SourcePortraitEntry` (e.g. `"strike_ironclad"`). |
| `RedirectedCharacterModel` | Base for a character without its own art: override `AssetSourceEntry` (e.g. `"ironclad"`) to reuse that character's icons, select screen art, map marker, visuals and sounds. |

## PackmasterSettings

Static properties, saved to `user://PackmasterLib/settings.json`
(`%APPDATA%\SlayTheSpire2\PackmasterLib\settings.json`). Shown in *Settings → General →
Packmaster*, except `HideSummaries` (toggle on the draft screen).

| Setting | Default | Description |
|---|---|---|
| `OneFrameMode` | off | STS1 *One Frame For All*: pack cards use the pack character's frame and energy icon. |
| `AllowMultipleNone` | off | STS1 *Allow selecting None multiple times*. Off: only the first `none` slot counts, others roll random. |
| `HideSummaries` | off | Hide star ratings in pack tooltips. |
| `UnlockAllPacks` | off | Dev: ignore `IsPackUnlocked`. |
| `AutoResolveChoices` | off | Dev: resolve choice slots randomly, skip the draft screen. |
| `ExcludeOnlyLastRound` | off | Dev: relaxed draft rule (see [behavior](behavior.md#draft-rules)). |

## Queries

`PackRegistry` (`Sts2Packmaster.Lib.Core`):

| Member | Description |
|---|---|
| `Registrations` | All registered characters. |
| `GetRegistration(CharacterModel?)` | Registration of a character, or `null`. |
| `GetPackCharacters()` | The `CharacterModel`s of all pack characters. |
| `GetPoolCards(registration)` | Cards of your own assembly for `GenerateAllCards()`. |
| `GetCharacterCards(registration)` | Every card of the character: all packs plus extras. |
| `GetExtraCards(registration)` | The `ExtraPoolCardTypes` cards. |
| `GetPackCards(pack)` | Canonical cards of a pack. |
| `GetPackOf(card, registration)` | The pack a card belongs to for that character. |
| `GetPreviewCard(pack)`, `IsPreviewCard(card)`, `GetPreviewPack(card)` | Preview card lookups. |
| `GetPackName(pack)` | Localized pack name. |
| `ResolveLocKey("table:key")` | Resolves a library-style loc key (injected entries, then game tables). |

`PackState` — the run's packs per player:

| Member | Description |
|---|---|
| `PackState.Get(player)` | `PlayerPackState` or `null` (not a pack character). |
| `PackState.NeedsSetup(player)` | The draft screen is not confirmed yet. |
| `PackState.GetSelectedCardIds(player)` | Ids of every card in the run's packs. |
| `PlayerPackState.Selected` | Packs in the run, in order. |
| `PlayerPackState.ChoicesLeft` | Draft rounds left. |
| `PlayerPackState.SetupDone` | Draft confirmed. |
| `PlayerPackState.CurrentOffer()` | The current round's offer. |
| `PlayerPackState.SelectedCards()` | Canonical cards of the selected packs. |

Treat `PlayerPackState` as read-only; picking packs goes through the draft screen.

`PackDisplayContext.For(card)` returns the pack character whose packs apply to a card on screen
(card owner, the library's pack-character filter, or the local player).

## Console commands

| Command | Mod | Description |
|---|---|---|
| `packtest resolve [seed]` | PackmasterLib | Checks slot resolution invariants for every registered character. |
| `packtest state` | PackmasterLib | Dumps and validates the current run's pack state. |
| `packgive <packId>` | VanillaPacks | Adds every card of a pack to the deck. |
| `packtestreward [rounds]` | VanillaPacks | Generates card rewards and checks they only contain cards from selected packs. |
