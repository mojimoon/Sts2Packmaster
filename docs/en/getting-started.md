# Getting started: your first pack character

[中文](../zh/getting-started.md) · [API reference](api.md) · [In-game behavior](behavior.md)

This guide walks through making a card-pack character with **PackmasterLib**, using the
`VanillaPacks` demo mod in this repository as the working example. Everything shown here is
taken from `VanillaPacks/src/` — open it side by side.

## 1. What you need

- Slay the Spire 2 (v0.111.0 or later) and the .NET 9 SDK.
- The source of this repository (clone or download the zip from GitHub). You build
  `PackmasterLib.dll` from it, or copy the dll from an installed PackmasterLib mod.
- Basic STS2 modding knowledge: a mod is a folder in `<game>/mods/<ModId>/` containing
  `<ModId>.json` (manifest) and `<ModId>.dll`, with a static class marked `[ModInitializer]`.

PackmasterLib only uses the game's own modding API and Harmony; it has no other dependencies.

## 2. Project setup

The repository's `Directory.Build.props` references `sts2.dll`, `0Harmony.dll` and
`GodotSharp.dll` from the game folder. Set your game folder once:

```
dotnet build -p:Sts2Dir="C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2"
```

or set the `STS2_DIR` environment variable. `build-and-install.ps1` builds both mods and copies
them into `<game>/mods/` (`-GameDir`, `-Dotnet` parameters or `STS2_DIR` / `STS2_DOTNET`).

In your own mod, reference PackmasterLib **without copying it** (the game loads the one from the
PackmasterLib mod folder):

```xml
<!-- inside this repository -->
<ProjectReference Include="..\PackmasterLib\PackmasterLib.csproj">
  <Private>false</Private>
</ProjectReference>

<!-- or, in a separate repository -->
<Reference Include="PackmasterLib">
  <HintPath>path\to\PackmasterLib.dll</HintPath>
  <Private>false</Private>
</Reference>
```

Declare the dependency in your manifest so the game loads PackmasterLib first:

```json
{
  "id": "MyPackCharacter",
  "name": "My Pack Character",
  "author": "Me",
  "description": "...",
  "version": "0.1.0",
  "min_game_version": "0.111.0",
  "has_pck": false,
  "has_dll": true,
  "dependencies": [
    { "id": "PackmasterLib", "min_version": "0.1.0" }
  ],
  "affects_gameplay": true
}
```

On the Steam Workshop, also add PackmasterLib as a *Required Item* of your item.

## 3. The character

A pack character is an ordinary `CharacterModel` subclass in your assembly (ModelDb registers it
automatically). If you have no art yet, derive from `RedirectedCharacterModel` to reuse a vanilla
character's visuals, icons, map marker and sounds:

```csharp
public sealed class VanillaSlinger : RedirectedCharacterModel
{
    protected override string AssetSourceEntry => "ironclad";   // reuse Ironclad's assets

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
        ModelDb.Card<PackStrike>(), /* ... */ ModelDb.Card<Bash>(), ModelDb.Card<Neutralize>(),
    };
    public override IReadOnlyList<RelicModel> StartingRelics => new List<RelicModel> { ModelDb.Relic<BurningBlood>() };
    // ... the remaining abstract members (animation delays, map color, architect vfx)
}
```

The character also needs its `characters` localization (title, description, pronouns, banter…);
see `CharacterEn` in `VanillaPacksEntry.cs` for the full list of keys the game reads.

## 4. The card pool

The pool's `GenerateAllCards()` must return **only cards from your own assembly** — the library
computes that list for you:

```csharp
public sealed class VanillaSlingerCardPool : CardPoolModel
{
    public override string Title => "vanilla_slinger";
    public override string EnergyColorName => "colorless";
    public override string CardFrameMaterialPath => "card_frame_colorless";
    public override Color DeckEntryCardColor => new Color("8a6f4d");
    public override bool IsColorless => false;

    protected override CardModel[] GenerateAllCards() =>
        VanillaPacksEntry.Registration == null
            ? Array.Empty<CardModel>()
            : PackRegistry.GetPoolCards(VanillaPacksEntry.Registration).ToArray();

    // keep the pack preview cards out of the unlocked pool
    protected override IEnumerable<CardModel> FilterThroughEpochs(UnlockState unlockState, IEnumerable<CardModel> cards) =>
        cards.Where(c => !PackRegistry.IsPreviewCard(c));
}
```

Why: the game treats the first pool containing a card as that card's pool (frame, energy icon,
library tab). Vanilla or other-mod cards used in your packs therefore stay in their original pool
and keep their original frame; PackmasterLib adds them to your character's pool at runtime,
filtered by the packs chosen for the run. The pool's frame (`CardFrameMaterialPath`) is what
the *One frame for all* setting applies to every pack card.

## 5. Cards

Pack cards can be:

- **Your own cards** — normal `CardModel` subclasses.
- **Vanilla or other-mod cards** — reference the type directly (`typeof(Bloodletting)`). They keep
  their pool, frame and art, exactly like base-game cards in STS1's Packmaster.
- **Copies of vanilla cards with vanilla art** — vanilla card classes are sealed, so copy the
  behavior into a `PackCardModel` subclass and point it at the original portrait. The demo's
  starter cards do this:

```csharp
public sealed class PackStrike : PackCardModel
{
    protected override string SourcePoolTitle => "ironclad";
    protected override string SourcePortraitEntry => "strike_ironclad";
    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    protected override IEnumerable<DynamicVar> CanonicalVars => new List<DynamicVar> { new DamageVar(6m, ValueProp.Move) };

    public PackStrike() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}
```

**Cards outside every pack** (STS1's "basics") go in `ExtraPoolCardTypes`: starting deck cards and
**at least one Ancient card** that is not a transcendence upgrade. Dusty Tome gives a random
Ancient from the character's pool and fails if there is none. Basic/Ancient rarity keeps these
cards out of normal reward rolls.

## 6. Pack preview cards

Each pack needs a preview card for the draft screen — an empty subclass is enough:

```csharp
public sealed class PainPackPreview : PackPreviewCard { }
```

Its title and description are the pack's name and description; its portrait is the pack's cover
card (`CoverCardType`, default: the pack's first Rare). The model id is derived from the class
name (`PainPackPreview` → `PAIN_PACK_PREVIEW`), so its loc keys are
`PAIN_PACK_PREVIEW.title` / `PAIN_PACK_PREVIEW.description` in the `cards` table. A pack without a
preview card still works for fixed and random slots but is never offered in a draft.

## 7. Registering

Register in your mod initializer:

```csharp
[ModInitializer(nameof(Init))]
public static class MyEntry
{
    public static PackCharacterRegistration? Registration { get; private set; }

    public static void Init()
    {
        PackmasterApi.AddLoc("cards", "eng", MyLoc.CardsEn);
        PackmasterApi.AddLoc("cards", "zhs", MyLoc.CardsZhs);
        PackmasterApi.AddLoc("characters", "eng", MyLoc.CharacterEn);

        Registration = new PackCharacterRegistration
        {
            CharacterType = typeof(VanillaSlinger),
            Packs = new List<PackDefinition>
            {
                new PackDefinition
                {
                    Id = "pain",
                    NameKey = "cards:PAIN_PACK_PREVIEW.title",
                    DescriptionKey = "cards:PAIN_PACK_PREVIEW.description",
                    Author = "MegaCrit",
                    PreviewCardType = typeof(PainPackPreview),
                    CoverCardType = typeof(CrimsonMantle),
                    CardTypes = new[]
                    {
                        typeof(Breakthrough), typeof(BloodWall), typeof(Hemokinesis), typeof(Spite),
                        typeof(Bloodletting), typeof(Rupture), typeof(Inferno), typeof(TearAsunder),
                        typeof(Offering), typeof(Brand), typeof(CrimsonMantle),
                    },
                    Summary = new PackSummary
                    {
                        Offense = 4, Defense = 1, Support = 2, Frontload = 3, Scaling = 4,
                        Tags = new[] { PackTags.SelfDamage, PackTags.Strength },
                    },
                },
                // ... more packs
            },
            ExtraPoolCardTypes = new[] { typeof(PackStrike), typeof(PackDefend), typeof(Bash), typeof(Neutralize),
                                         typeof(Corruption), typeof(WraithForm) /* Ancients */ },
            DefaultSlots = new[] { "random", "random", "random", "random", "choice", "choice", "choice" },
        };
        PackmasterApi.RegisterCharacter(Registration);
    }
}
```

Keep the registration in a static property: your card pool needs it in `GenerateAllCards()`.

## 8. Localization

`PackmasterApi.AddLoc(table, language, entries)` merges entries into the game's localization
tables without a `.pck`. Language codes are the game's: `eng`, `zhs`, `zht`, `jpn`, `kor`, `deu`,
`fra`, `spa`, `esp`, `ita`, `pol`, `ptb`, `rus`, `tha`, `tur`, `ind`.

- Card and character text is read by the game from the table of the **current** language. Add
  your English entries to every language you do not translate, or players of those languages see
  raw keys. The demo loops over all 16 codes (`VanillaLanguages` in `VanillaPacksEntry.cs`).
- Pack names, descriptions and tags resolved by the library fall back to `eng` automatically.
- Keys use the `"table:key"` form in `NameKey`, `DescriptionKey`, `CreditsKey` and custom tags;
  text without a colon is shown as-is. If you ship a `.pck` with your own tables, the same keys
  work.

PackmasterLib's own UI text ships in all 16 languages.

## 9. Pack design rules (important)

Rewards, shops and effects like "add a random Attack" draw **only from the selected packs**. A
thin pool can leave such a roll with nothing to pick, which can crash the game. Follow STS1
Packmaster's guidelines:

- **≥ 10 cards per pack**;
- **≥ 2 Attacks, ≥ 2 Skills, ≥ 2 Powers**;
- **≥ 2 Commons, ≥ 2 Uncommons, ≥ 2 Rares**;
- `MultiplayerOnly` cards disappear in single player and do not count;
- a card belongs to **at most one pack per character**.

The library logs `Pack '<id>' is thin` when a pack is below these numbers — check the game log
(`%APPDATA%\SlayTheSpire2\logs\godot.log`) after your first launch.

## 10. Test it

1. Build and install, start the game, enable both mods and restart.
2. Pick your character: the pack config panel appears top-right on the character select screen.
3. Embark: the pack draft screen opens before Neow.
4. In a run, open the console (`~`): `packtest state` dumps and validates the run's pack state;
   `packtest resolve [seed]` checks slot resolution for every registered character.

See [In-game behavior](behavior.md) for what players see and [API reference](api.md) for every
option.
