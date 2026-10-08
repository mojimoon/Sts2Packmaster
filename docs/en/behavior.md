# In-game behavior

[中文](../zh/behavior.md) · [Getting started](getting-started.md) · [API reference](api.md)

What PackmasterLib does in game, and the rules your packs are subject to. It follows STS1's
The Packmaster unless noted.

## Character select: pack config

Selecting a pack character shows a **pack config panel** at the top right (click the title to
collapse it):

- **All Packs**: every unlocked pack is in the run, no draft (STS1 *All Packs* mode).
- **Pack slots**: 3–10 slots.
- Each slot: **Random**, **Choice of 3**, **None**, or a specific pack.

The config is saved per character in `user://PackmasterLib/config.json`. Until the player changes
it, the character's `DefaultSlots` apply. Multiplayer always uses `DefaultSlots`.

Only the first **None** slot counts unless *Allow multiple "None" slots* is enabled; extra ones
roll a random pack.

## Run start: slot resolution

When the run starts:

1. **Fixed** slots take their pack (unknown, locked or duplicate packs are skipped).
2. **Random** slots draw from the unlocked packs not taken yet (`Drawer.DrawRandomSlot`).
3. **Choice** slots are counted; each becomes one draft round on the setup screen.

All random draws use the run seed, so the same seed and config give the same packs.

## The pack draft screen

The draft screen opens on entering the first room, before Neow:

- Top: **Current Packs** — the fixed and random packs.
- Middle: **Draft a Pack!** — this round's offer (`ChoiceSize`, default 3).
- Hovering a pack shows its **Pack Summary** (star ratings and tags); the author is under each
  card. *Show pack ratings* (bottom center) hides the stars.
- **Back** (bottom left) or **Esc** hides the screen to look at Neow or the map; press it again
  to return. Clicking a Neow option or map node before confirming brings the screen back.
- When every round is done the packs are centered; **Confirm** continues. Neow's options are
  unaffected.

**Save & quit before confirming is allowed**: picks stay in memory until Confirm and are not
saved. After loading, the draft starts over and the same picks produce the same offers.

The screen is skipped in *All Packs* mode, in multiplayer (choices resolved deterministically
from the seed) and with the dev setting *Skip pack setup*.

### Draft rules

Each round offers unlocked packs that have a preview card and are not in the run yet.

- **Default (STS1)**: a pack that was offered and not picked is not offered again until the
  pool runs out. If fewer than `ChoiceSize` fresh packs remain, passed-over packs fill the offer.
- **Dev setting *Relaxed drafting*** (`ExcludeOnlyLastRound`): only the previous round's unpicked
  packs sit out, so a passed-over pack can return two rounds later.

Offers are generated lazily and deterministically from (run seed, player, round, packs picked so
far). An offer is empty only when no pack is left.

## The run's card pool

The character's pool is **the cards of the selected packs** plus `ExtraPoolCardTypes`:

- card rewards, shops, events, potions and every "random card" effect only use selected packs;
- **transforms** stay inside the run's packs (colorless cards still transform into colorless);
- Dusty Tome picks an Ancient from `ExtraPoolCardTypes`;
- vanilla cards in packs keep their original frame, unless *One frame for all* is on.

This is why packs need depth: see the pack design rules in
[Getting started](getting-started.md#9-pack-design-rules-important).

## Cards

Cards that belong to one of the character's packs show the **pack name** on the top edge of the
card frame (white text with a dark outline), in the run, in rewards, in the library and in pool
views.

## Top bar: packs button

A packs button sits at the top right during a run. Hover lists the run's packs; click opens the
**run's card pool** (no starters; sorted by pack → rarity → name). Close with ✓ or Esc.

## Card library

With a pack character's filter selected, the library shows all its pack cards (vanilla ones
included) with pack names, and the sidebar gets:

- a **Pack** sort button;
- a **pack filter** dropdown listing that character's packs.

The search box also matches pack names.

## Settings

*Settings → General → Packmaster* (collapsible):

| Option | Meaning |
|---|---|
| One frame for all | Pack cards use the pack character's frame and energy icon. |
| Allow multiple "None" slots | Several None slots are allowed (risky with tiny pools). |
| [Dev] Unlock every pack | Ignore character mods' unlock rules. |
| [Dev] Skip pack setup | Choice slots resolve randomly; no draft screen. |
| [Dev] Relaxed drafting | See [draft rules](#draft-rules). |

Stored in `user://PackmasterLib/settings.json`.

## Saves

The run's pack data (config, selected packs, rounds left, confirmed flag) is stored in a hidden
run modifier inside the normal run save, so continuing a run and multiplayer sync
keep working. The modifier is hidden from Neow and the top bar.

## Multiplayer

Each player has their own packs. Multiplayer uses each character's `DefaultSlots`, draft rounds
are resolved deterministically from the seed, and there is no draft screen.

## Fonts and languages

The library's UI uses the game's `MegaLabel` and per-language font substitution, so CJK and other
scripts render correctly (and font mods apply). Its text ships in all 16 game languages; pack
names and descriptions come from the character mod.

## Automated test (VanillaPacks)

VanillaPacks contains an end-to-end test that plays like a user (character select → embark →
three draft rounds → confirm → Neow) with sound muted, plus many unit checks:

```
SlayTheSpire2.exe --packmastertest --force-steam=off              # windowed, saves screenshots to user://packtest_*.png
SlayTheSpire2.exe --headless --packmastertest --force-steam=off   # headless
```

Then search the game log for `PackmasterLib-AutoTest`; the last line is
`RESULT: <n> checks, <m> failures`.
