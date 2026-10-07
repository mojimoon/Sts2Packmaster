using System.Text;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using Sts2Packmaster.Lib.Api;
using Sts2Packmaster.Lib.Core;
using Sts2Packmaster.Lib.Patches;
using Sts2Packmaster.Lib.Ui;

namespace Sts2Packmaster.VanillaPacks;

/// <summary>
/// Integration test, triggered by the --packmastertest launch argument (muted). It drives the game
/// like a player: character select → embark → pack setup screen (back/Esc, gating, save/load, drafts,
/// confirm) → Neow, then checks the library pack filter, the top bar button, the pack pool and
/// persistence; plus pure checks of drafting rules, weights, custom drawers, shared packs and loc.
/// Without --headless it also saves screenshots to user://packtest_*.png.
/// </summary>
internal static class PackmasterAutoTest
{
	public static bool Requested => CommandLineHelper.HasArg("packmastertest");

	private static readonly StringBuilder Report = new();
	private static int _failures;
	private static int _checks;

	public static async Task Run()
	{
		try
		{
			await NGame.Instance!.GameStartupComplete;
			await RunAllChecks();
		}
		catch (Exception e)
		{
			Report.AppendLine($"FATAL: {e}");
			_failures++;
		}
		Log.Info($"[PackmasterLib-AutoTest] RESULT: {_checks} checks, {_failures} failures\n{Report}");
		await Task.Delay(2000);
		NGame.Instance?.Quit();
	}

	private static PackCharacterRegistration Reg => VanillaPacksEntry.Registration!;

	private static async Task RunAllChecks()
	{
		var character = ModelDb.Character<VanillaSlinger>();
		StaticChecks(character);
		LocChecks();
		ResolverChecks();
		DraftRuleChecks();
		DrawerChecks();
		SharedPackChecks();
		var menu = await MainMenu();
		if (menu == null)
		{
			return;
		}
		await CharacterSelectAndSettingsChecks(menu, character);
		var player = await EmbarkAndSetup(menu, character);
		if (player == null)
		{
			return;
		}
		await LibraryFilterChecks(player, character);
		await TopBarChecks(player);
		PoolChecks(player);
		await CardVisualChecks(player);
		PersistenceChecks(player);
	}

	// ---------------------------------------------------------------- registry / pools / loc

	private static void StaticChecks(CharacterModel character)
	{
		Yes(ModelDb.AllCharacters.Contains(character), "VanillaSlinger in ModelDb.AllCharacters");
		Yes(Reg.Packs.Count == 11, $"11 packs registered (got {Reg.Packs.Count})");
		foreach (var pack in Reg.Packs)
		{
			var cards = PackRegistry.GetPackCards(pack).Where(c => c.MultiplayerConstraint != CardMultiplayerConstraint.MultiplayerOnly).ToList();
			var types = new[] { CardType.Attack, CardType.Skill, CardType.Power }.All(t => cards.Count(c => c.Type == t) >= 2);
			var rarities = new[] { CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare }.All(r => cards.Count(c => c.Rarity == r) >= 2);
			Yes(cards.Count >= 10 && types && rarities, $"pack '{pack.Id}': {cards.Count} singleplayer cards, 2+ of each type and rarity", quiet: true);
			Yes(pack.Author == "MegaCrit", $"pack '{pack.Id}' author is MegaCrit", quiet: true);
		}
		Yes(true, "all 11 packs have STS1 depth (10+ cards, 2+ of each type/rarity) and author MegaCrit");
		var all = Reg.Packs.SelectMany(PackRegistry.GetPackCards).ToList();
		Yes(all.Count == all.Distinct().Count(), "no card is in two packs of the same character");
		var pool = ModelDb.CardPool<VanillaSlingerCardPool>();
		Yes(pool.AllCards.Count() == 2 + 11, "own pool = 2 starters + 11 previews");
		Yes(ReferenceEquals(ModelDb.Card<Bloodletting>().Pool, ModelDb.CardPool<IroncladCardPool>()), "vanilla pack card keeps its original pool");
		var unlocked = pool.GetUnlockedCards(SaveManager.Instance.GenerateUnlockStateFromProgress(), CardMultiplayerConstraint.None).ToList();
		Yes(all.All(unlocked.Contains) && unlocked.All(c => !PackRegistry.IsPreviewCard(c)), "outside a run the pool = all unlocked packs, no previews");
		Yes(unlocked.Any(c => c.Rarity == CardRarity.Ancient && !ArchaicTooth.TranscendenceCards.Contains(c)), "Dusty Tome has an Ancient card to give");
	}

	private static void LocChecks()
	{
		var languages = new[] { "deu", "eng", "esp", "fra", "ind", "ita", "jpn", "kor", "pol", "ptb", "rus", "spa", "tha", "tur", "zhs", "zht" };
		var keys = new[]
		{
			"panel.title", "allpacks", "count", "slot", "slot.random", "slot.choice", "slot.none", "sort.pack", "filter.all",
			"preview.type", "setup.chosen", "setup.choose", "setup.ratings", "setup.author", "setup.confirm_hint", "setup.hidden_hint",
			"summary.title", "summary.offense", "summary.tags", "topbar.title", "topbar.desc", "topbar.pool", "run.desc",
			"settings.group", "settings.oneframe", "settings.multinone", "settings.unlockall", "settings.autochoice", "settings.excludeall",
			"tag.None", "tag.Doom", "tag.Generation",
		};
		foreach (var language in languages)
		{
			var tables = PackRegistry.SnapshotLocTablesFor(language);
			var ui = tables.GetValueOrDefault("gameplay_ui") ?? new();
			var missing = keys.Where(k => !ui.ContainsKey("PACKMASTER_LIB." + k)).ToList();
			Yes(missing.Count == 0, $"[{language}] library UI strings complete{(missing.Count > 0 ? ": missing " + string.Join(",", missing) : "")}", quiet: true);
			Yes(tables.GetValueOrDefault("card_library")?.ContainsKey("PACKMASTER_LIB.pool.tip") == true, $"[{language}] library tip", quiet: true);
			Yes(tables.GetValueOrDefault("cards")?.ContainsKey("PAIN_PACK_PREVIEW.title") == true, $"[{language}] demo pack names (zh/en, others English)", quiet: true);
		}
		Yes(true, "library UI strings present in all 16 languages, demo pack names zh/en with English fallback");
		Yes(PackRegistry.SnapshotLocTablesFor("jpn")["gameplay_ui"]["PACKMASTER_LIB.setup.choose"] != PackRegistry.SnapshotLocTablesFor("eng")["gameplay_ui"]["PACKMASTER_LIB.setup.choose"],
			"non-English languages are translated, not English copies");
	}

	// ---------------------------------------------------------------- drafting rules

	private static void ResolverChecks()
	{
		string[][] configs =
		{
			new[] { "random", "random", "random", "random", "choice", "choice", "choice" },
			new[] { "pain", "random", "choice", "choice", "random" },
			new[] { "none", "choice", "frost", "random", "choice" },
		};
		for (var seed = 0; seed < 60; seed++)
		{
			foreach (var tokens in configs)
			{
				var r = PackResolver.Resolve(Reg, tokens, false, new Rng((ulong)(seed + 7)));
				Yes(r.Selected.Distinct().Count() == r.Selected.Count, "resolver: no duplicate packs", quiet: true);
				Yes(r.ChoiceSlots == tokens.Count(t => t == "choice"), "resolver: choice slots counted", quiet: true);
				foreach (var t in tokens.Where(t => t is not ("random" or "choice" or "none")))
				{
					Yes(r.Selected.Any(p => p.Id == t), $"resolver: fixed '{t}' honored", quiet: true);
				}
			}
		}
		Yes(true, "resolver invariants over 60 seeds x 3 configs");

		Yes(Reg.UnlockedPacks.Count == 11, "all packs unlocked by default");
		Reg.IsPackUnlocked = p => p.Id != "pain";
		Yes(Enumerable.Range(0, 30).All(n =>
		{
			var r = PackResolver.Resolve(Reg, configs[0], false, new Rng((ulong)n));
			return r.Selected.All(p => p.Id != "pain")
				&& PackResolver.Offer(Reg, r.Selected, Array.Empty<PackDefinition>(), 0, new Rng((ulong)n)).All(p => p.Id != "pain");
		}), "IsPackUnlocked: a locked pack is never offered");
		PackmasterSettings.UnlockAllPacks = true;
		Yes(Reg.UnlockedPacks.Count == 11, "dev setting 'unlock all packs' overrides IsPackUnlocked");
		PackmasterSettings.UnlockAllPacks = false;
		Reg.IsPackUnlocked = null;

		var noneTokens = new[] { "none", "none", "none", "random" };
		Yes(PackResolver.Resolve(Reg, noneTokens, false, new Rng(7)).Selected.Count == 3, "multiple None off: extra None slots roll random");
		PackmasterSettings.AllowMultipleNone = true;
		Yes(PackResolver.Resolve(Reg, noneTokens, false, new Rng(7)).Selected.Count == 1, "multiple None on: every None slot stays empty");
		PackmasterSettings.AllowMultipleNone = false;
	}

	/// <summary>Simulates the drafts of the default config: 4 random + 3 drafts over 11 packs.</summary>
	private static void DraftRuleChecks()
	{
		var defaultConfig = Reg.DefaultSlots;
		var fullOffers = true;
		var previousRoundRespected = true;
		for (var seed = 0; seed < 200; seed++)
		{
			var r = PackResolver.Resolve(Reg, defaultConfig, false, new Rng((ulong)seed));
			var selected = r.Selected.ToList();
			var last = new List<PackDefinition>();
			for (var round = 0; round < r.ChoiceSlots; round++)
			{
				var offer = PackResolver.Offer(Reg, selected, last, round, new Rng((ulong)(seed * 31 + round)));
				fullOffers &= offer.Count == 3;
				var eligibleOutsideLast = Reg.Packs.Count(p => !selected.Contains(p) && !last.Contains(p));
				if (eligibleOutsideLast >= 3)
				{
					previousRoundRespected &= !offer.Intersect(last).Any();
				}
				selected.Add(offer[(seed + round) % offer.Count]);
				last = offer;
			}
		}
		Yes(fullOffers, "default config (4 random + 3 drafts, 11 packs): every draft round offers 3 packs");
		Yes(previousRoundRespected, "default rule: the previous round's leftovers sit the next round out");

		// STS1 developer rule: no unpicked pack returns until the pool runs out.
		var sts1 = true;
		for (var seed = 0; seed < 100; seed++)
		{
			var selected = new List<PackDefinition>();
			var unpicked = new List<PackDefinition>();
			for (var round = 0; round < 6; round++)
			{
				var offer = PackResolver.Offer(Reg, selected, unpicked, round, new Rng((ulong)(seed * 17 + round)));
				var fresh = Reg.Packs.Count(p => !selected.Contains(p) && !unpicked.Contains(p));
				if (fresh >= 3)
				{
					sts1 &= !offer.Intersect(unpicked).Any();
				}
				if (offer.Count == 0)
				{
					break;
				}
				var pick = offer[0];
				selected.Add(pick);
				unpicked.AddRange(offer.Where(p => p != pick));
			}
		}
		Yes(sts1, "STS1 rule (dev setting): unpicked packs never return while fresh ones remain");
	}

	private sealed class LeftGuaranteeDrawer : IPackDrawer
	{
		public required string GuaranteedId;

		public PackDefinition DrawRandomSlot(PackDrawContext context) => WeightedPackDrawer.Instance.DrawRandomSlot(context);

		public IReadOnlyList<PackDefinition> DrawChoiceOffer(PackDrawContext context, int count)
		{
			// "The left pack is guaranteed": put the target first when it is available.
			var target = context.Candidates.FirstOrDefault(p => p.Id == GuaranteedId);
			var rest = WeightedPackDrawer.Draw(context.Candidates.Where(p => p != target).ToList(), target == null ? count : count - 1, context.Rng);
			return target == null ? rest : new[] { target }.Concat(rest).ToList();
		}
	}

	private static void DrawerChecks()
	{
		PackDefinition Fake(string id, double weight) => new() { Id = id, NameKey = id, DescriptionKey = id, CardTypes = Array.Empty<Type>(), Weight = weight };
		var zero = Fake("w0", 0);
		var one = Fake("w1", 1);
		var nine = Fake("w9", 9);
		var rng = new Rng(42);
		var counts = new Dictionary<string, int> { ["w0"] = 0, ["w1"] = 0, ["w9"] = 0 };
		for (var i = 0; i < 2000; i++)
		{
			counts[WeightedPackDrawer.Draw(new[] { zero, one, nine }, 1, rng)[0].Id]++;
		}
		Yes(counts["w0"] == 0 && counts["w9"] > 1600 && counts["w9"] < 1980, $"weights: 0 never drawn, 9:1 ratio respected ({counts["w1"]}/{counts["w9"]})");
		Yes(WeightedPackDrawer.Draw(new[] { zero }, 1, rng).Single() == zero, "weight 0 still drawn when nothing else is left");

		Reg.Drawer = new LeftGuaranteeDrawer { GuaranteedId = "doom" };
		var guaranteed = Enumerable.Range(0, 50).All(n =>
		{
			var offer = PackResolver.Offer(Reg, Array.Empty<PackDefinition>(), Array.Empty<PackDefinition>(), 0, new Rng((ulong)n));
			return offer.Count == 3 && offer[0].Id == "doom";
		});
		Reg.Drawer = null;
		Yes(guaranteed, "custom IPackDrawer: the left pack of every offer is the guaranteed one");
	}

	private static void SharedPackChecks()
	{
		// A second character (vanilla Ironclad, test only) shares two of the demo's packs.
		var shared = new PackCharacterRegistration
		{
			CharacterType = typeof(Ironclad),
			Packs = new[] { Reg.Packs[0], Reg.Packs[1], Reg.Packs[2], Reg.Packs[3] },
			DefaultSlots = new[] { "random", "random", "choice" },
		};
		PackmasterApi.RegisterCharacter(shared);
		try
		{
			var card = PackRegistry.GetPackCards(Reg.Packs[0])[0];
			Yes(PackRegistry.GetRegistration(ModelDb.Character<Ironclad>()) == shared, "shared: second character registered");
			Yes(PackRegistry.GetPackOf(card, Reg) == Reg.Packs[0] && PackRegistry.GetPackOf(card, shared) == Reg.Packs[0], "shared: a card of a shared pack belongs to that pack for both characters");
			var r = PackResolver.Resolve(shared, shared.DefaultSlots, false, new Rng(5));
			var offer = PackResolver.Offer(shared, r.Selected, Array.Empty<PackDefinition>(), 0, new Rng(5));
			Yes(r.Selected.Concat(offer).All(shared.Packs.Contains) && r.Selected.Count == 2 && offer.Count == 2,
				"shared: each character draws from its own pack list");
			var unlocked = ModelDb.CardPool<IroncladCardPool>().GetUnlockedCards(SaveManager.Instance.GenerateUnlockStateFromProgress(), CardMultiplayerConstraint.None).ToList();
			Yes(shared.Packs.SelectMany(PackRegistry.GetPackCards).All(unlocked.Contains), "shared: pack cards reach the second character's pool");
			Yes(PackRegistry.GetPackCards(Reg.Packs[0]).Count == Reg.Packs[0].CardTypes.Count, "shared: pack card list unchanged for the first character");
		}
		finally
		{
			PackRegistry.Unregister(shared);
		}
		Yes(PackRegistry.GetRegistration(ModelDb.Character<Ironclad>()) == null, "shared: test registration removed");
	}

	// ---------------------------------------------------------------- menus

	private static async Task<NMainMenu?> MainMenu()
	{
		for (var i = 0; i < 100 && NGame.Instance!.MainMenu == null; i++)
		{
			await Task.Delay(200);
		}
		var menu = NGame.Instance!.MainMenu;
		Yes(menu != null, "main menu loaded");
		if (menu == null)
		{
			return null;
		}
		await Task.Delay(5000); // let the main menu's background "Common" preload finish, like a player would
		if (NGame.Instance.FindChildren("*", "", true, false).OfType<NEarlyAccessDisclaimer>().FirstOrDefault() is { } disclaimer)
		{
			await disclaimer.CloseScreen();
			await Task.Delay(500);
		}
		return menu;
	}

	private static async Task CharacterSelectAndSettingsChecks(NMainMenu menu, CharacterModel character)
	{
		var stack = menu.SubmenuStack;
		PackConfigStore.Save(PackRegistry.RegistrationKey(Reg), PackSlotConfig.FromDefault(Reg));
		var select = await OpenCharacterSelect(menu, character);
		var panel = select.GetNodeOrNull<Control>("PackmasterLibPanel");
		Yes(panel is { Visible: true } && Paginators(panel).Count == 12, "pack config panel on character select");
		Yes(panel != null && panel.FindChildren("*", "", true, false).OfType<MegaCrit.Sts2.addons.mega_text.MegaLabel>().Any(l => l.Text == PackRegistry.ResolveLocKey("gameplay_ui:PACKMASTER_LIB.allpacks")),
			"panel labels are MegaLabels (game font substitution)");
		await Screenshot("1_charselect");
		stack.Pop();
		await Task.Delay(500);

		menu.OpenSettingsMenu();
		await Task.Delay(1500);
		var group = stack.FindChild(PackSettingsSection.GroupName, true, false) as Control;
		var options = stack.FindChild(PackSettingsSection.OptionsName, true, false) as Control;
		if (group != null && options != null)
		{
			var groupButton = group.GetNode<NButton>("PackmasterLibGroupButton");
			groupButton.EmitSignal(NClickableControl.SignalName.Released, groupButton);
			await Task.Delay(300);
			var toggles = Paginators(options);
			Yes(options.Visible && toggles.Count == 5, $"settings group: 5 options incl. the STS1 drafting dev option (got {toggles.Count})");
			if (toggles.Count == 5)
			{
				toggles[4].PageRight();
				Yes(PackmasterSettings.ExcludeAllUnpicked, "settings toggle writes PackmasterSettings.ExcludeAllUnpicked");
				toggles[4].PageLeft();
			}
			var scroller = stack.FindChildren("*", "", true, false).OfType<NScrollableContainer>().First();
			AccessTools.Field(scroller.GetType(), "_targetDragPosY").SetValue(scroller, -group.Position.Y + 150);
			await Task.Delay(1000);
			await Screenshot("2_settings");
			groupButton.EmitSignal(NClickableControl.SignalName.Released, groupButton);
		}
		else
		{
			Yes(false, "settings group present");
		}
		stack.Pop();
		await Task.Delay(500);
	}

	private static async Task<NCharacterSelectScreen> OpenCharacterSelect(NMainMenu menu, CharacterModel character)
	{
		var select = menu.SubmenuStack.GetSubmenuType<NCharacterSelectScreen>();
		select.InitializeSingleplayer();
		menu.SubmenuStack.Push(select);
		await Task.Delay(1500);
		(select.FindChild(character.Id.Entry + "_button", true, false) as NCharacterSelectButton)?.Select();
		await Task.Delay(1500);
		return select;
	}

	// ---------------------------------------------------------------- the real start of a run

	private static async Task<Player?> EmbarkAndSetup(NMainMenu menu, CharacterModel character)
	{
		// The fresh test profile has not revealed Neow; reveal it so the first room is Neow.
		SaveManager.Instance.ObtainEpochOverride(MegaCrit.Sts2.Core.Timeline.EpochModel.GetId<MegaCrit.Sts2.Core.Timeline.Epochs.NeowEpoch>(), EpochState.Revealed);
		var select = await OpenCharacterSelect(menu, character);
		var embark = select.GetNode<NButton>("ConfirmButton");
		embark.EmitSignal(NClickableControl.SignalName.Released, embark);
		for (var i = 0; i < 150 && PackSetupScreen.Current == null; i++)
		{
			await Task.Delay(200);
			if (NGame.Instance!.FindChildren("*", "", true, false).OfType<MegaCrit.Sts2.Core.Nodes.Ftue.NAcceptTutorialsFtue>().FirstOrDefault() is { } ftue)
			{
				AccessTools.Method(ftue.GetType(), "NoTutorials").Invoke(ftue, new object?[] { null });
			}
		}
		var screen = PackSetupScreen.Current;
		var run = CardPoolPatch.CurrentRun;
		var player = run == null ? null : LocalContext.GetMe(run);
		Yes(screen != null && player != null, "embarking opens the pack setup screen in the first room");
		if (screen == null || player == null)
		{
			return null;
		}
		var state = PackState.Get(player)!;
		var modifier = run!.Modifiers.OfType<PackRunModifier>().Single();
		await Task.Delay(1200);
		Yes(state.Selected.Count == 4 && screen.CurrentChoices.Count == 3, "4 random packs on top, first draft offers 3");
		await Screenshot("3_setup_draft");

		var hovered = screen.CurrentChoices[0];
		var hitbox = screen.Root.GetNode<Control>($"Content/Pack_{hovered.Id}/Hitbox");
		hitbox.EmitSignal(Control.SignalName.MouseEntered);
		await Task.Delay(500);
		await Screenshot("4_setup_hover");
		hitbox.EmitSignal(Control.SignalName.MouseExited);

		// Back button hides the screen (look at Neow), Neow options are gated until confirmed.
		var neowEvent = NEventRoom.Instance == null ? null : (EventModel)AccessTools.Field(typeof(NEventRoom), "_event").GetValue(NEventRoom.Instance)!;
		Yes(neowEvent?.Id.Entry == "NEOW", "the first room is Neow");
		screen.BackButton.EmitSignal(NClickableControl.SignalName.Released, screen.BackButton);
		await Task.Delay(600);
		Yes(!screen.IsShown, "back button hides the setup screen");
		await Screenshot("5_setup_hidden_neow");
		if (neowEvent != null)
		{
			var option = neowEvent.CurrentOptions[0];
			NEventRoom.Instance!.OptionButtonClicked(option, 0);
			await Task.Delay(500);
			Yes(screen.IsShown && !option.WasChosen, "choosing a Neow option before confirming brings the setup screen back instead");
		}
		// Esc: the back button owns the top of the hotkey stack while the screen is up.
		var released = (Dictionary<StringName, List<Action>>)AccessTools.Field(typeof(NHotkeyManager), "_hotkeyReleasedBindings").GetValue(NHotkeyManager.Instance)!;
		Yes(released.TryGetValue(MegaInput.pauseAndBack, out var escBindings) && escBindings.LastOrDefault()?.Target == screen.BackButton,
			"Esc is bound to the setup screen's back button (not the pause menu)");
		Yes(released.TryGetValue(MegaInput.viewDeckAndTabLeft, out var deckBindings) && deckBindings.LastOrDefault()?.Target is not NButton,
			"other hotkeys (deck) are blocked while the screen is up");
		screen.SetShown(false);
		Yes(released.TryGetValue(MegaInput.viewDeckAndTabLeft, out var hiddenDeck) && hiddenDeck.LastOrDefault()?.Target is MegaCrit.Sts2.Core.Nodes.TopBar.NTopBarDeckButton,
			"while hidden, the deck/map hotkeys work again");
		screen.SetShown(true);

		// Save/load before confirming: picks are not saved; the same picks give the same offers.
		var firstOffer = screen.CurrentChoices.Select(p => p.Id).ToList();
		var pick1 = screen.CurrentChoices[1];
		screen.Click(pick1);
		await Task.Delay(1400);
		var secondOffer = screen.CurrentChoices.Select(p => p.Id).ToList();
		Yes(!modifier.PackSelected.Contains(pick1.Id) && modifier.PackChoicesLeft.EndsWith(":3"), "an unconfirmed pick is not written to the save");
		Yes(secondOffer.Count == 3 && !secondOffer.Intersect(firstOffer).Any(), "round 2 does not repeat round 1's packs");
		var saved = (PackRunModifier)ModifierModel.FromSerializable(modifier.ToSerializable());
		screen.CloseWithoutConfirming();
		PackState.LoadRun(run, saved);
		state = PackState.Get(player)!;
		PackSetupTrigger.TryOpen();
		await Task.Delay(1400);
		screen = PackSetupScreen.Current!;
		Yes(state.Selected.Count == 4 && screen.CurrentChoices.Select(p => p.Id).SequenceEqual(firstOffer), "after save/load the setup restarts with the same first offer");
		screen.Click(screen.CurrentChoices.First(p => p.Id == pick1.Id));
		await Task.Delay(1400);
		Yes(screen.CurrentChoices.Select(p => p.Id).SequenceEqual(secondOffer), "the same pick leads to the same second offer");

		for (var round = 1; round < 3; round++)
		{
			Yes(screen.CurrentChoices.Count == 3, $"draft {round + 1} offers 3 packs");
			var candidates = screen.CurrentChoices.ToList();
			var pick = candidates[round % candidates.Count];
			screen.Click(pick);
			Yes(state.Selected.Contains(pick) && candidates.Where(c => c != pick).All(c => !state.Selected.Contains(c)), $"draft {round + 1}: picked '{pick.Id}'", quiet: true);
			await Task.Delay(1400);
		}
		Yes(screen.IsConfirming && state.Selected.Count == 7 && state.Selected.Distinct().Count() == 7, "7 distinct packs, confirm step");
		await Screenshot("6_setup_confirm");
		screen.ConfirmButton.EmitSignal(NClickableControl.SignalName.Released, screen.ConfirmButton);
		await Task.Delay(800);
		Yes(!PackSetupScreen.IsOpen && !PackState.NeedsSetup(player) && modifier.PackSelected.Split(',').Length == 7, "confirm saves the 7 packs and closes the screen");
		if (neowEvent != null)
		{
			Yes(neowEvent.CurrentOptions.Count >= 2 && neowEvent.CurrentOptions.All(o => !o.TextKey.Contains("PACK_RUN_MODIFIER")), "Neow keeps its normal rewards");
			await Screenshot("7_neow");
		}
		return player;
	}

	// ---------------------------------------------------------------- library pack filter

	private static async Task LibraryFilterChecks(Player player, CharacterModel character)
	{
		var library = NCardLibrary.Create()!;
		library.Initialize(player.RunState);
		NRun.Instance!.GlobalUi.AddChild(library);
		library.OnSubmenuOpened();
		await Task.Delay(1000);
		var holder = library.FindChild("PackmasterPackFilter", true, false) as Control;
		var sort = holder == null ? null : holder.GetParent().GetChild(holder.GetIndex() - 1) as NCardViewSortButton;
		Yes(holder != null && sort != null && !holder.Visible && !sort.Visible, "pack sort/filter hidden until a pack character's tab is selected");
		var filters = (Dictionary<CharacterModel, NCardPoolFilter>)AccessTools.Field(typeof(NCardLibrary), "_cardPoolFilters").GetValue(library)!;
		filters[character].IsSelected = true; // what a click does (OnRelease), then the signal
		filters[character].EmitSignal(NCardPoolFilter.SignalName.Toggled, filters[character]);
		await Task.Delay(800);
		Yes(holder is { Visible: true } && sort is { Visible: true }, "pack sort/filter shown for the pack character");
		var dropdown = holder!.FindChildren("*", "", true, false).OfType<NDropdown>().First();
		var items = dropdown.FindChildren("*", "", true, false).OfType<NDropdownItem>().ToList();
		Yes(items.Count == Reg.Packs.Count + 1, $"filter lists 'all packs' + this character's {Reg.Packs.Count} packs (got {items.Count})");
		var pack = Reg.Packs[4];
		items[5].EmitSignal(NDropdownItem.SignalName.Selected, items[5]);
		await Task.Delay(1000);
		var grid = library.GetNode<NCardLibraryGrid>("%CardGrid");
		var visible = grid.VisibleCards.ToList();
		Yes(PackLibraryFilter.SelectedPack == pack && visible.Count == PackRegistry.GetPackCards(pack).Count && visible.All(c => PackRegistry.GetPackOf(c, Reg) == pack),
			$"selecting '{pack.Id}' shows exactly its {visible.Count} cards");
		Traverse.Create(dropdown).Method("OpenDropdown").GetValue();
		await Task.Delay(500);
		await Screenshot("8_library_filter");
		Traverse.Create(dropdown).Method("CloseDropdown").GetValue();
		PackLibraryFilter.Select(library, null);
		await Task.Delay(800);
		Yes(grid.VisibleCards.Count() > visible.Count, "'all packs' clears the filter");
		library.OnSubmenuClosed();
		await Task.Delay(1500); // let the grid's async layout finish before freeing
		library.QueueFree();
		await Task.Delay(300);
	}

	// ---------------------------------------------------------------- top bar + pool

	private static async Task TopBarChecks(Player player)
	{
		var button = PackTopBarButton.Instance;
		Yes(button != null && button.GetGlobalRect().Position.X > button.GetViewportRect().Size.X * 0.6f, "packs button in the top-right group");
		if (button == null)
		{
			return;
		}
		PackTopBarButton.TogglePoolView();
		await Task.Delay(1200);
		Yes(NCapstoneContainer.Instance?.CurrentCapstoneScreen is NSimpleCardsViewScreen, "clicking the button opens the pool view");
		await Screenshot("9_pool_view");
		PackTopBarButton.TogglePoolView();
		await Task.Delay(600);
	}

	private static void PoolChecks(Player player)
	{
		var state = PackState.Get(player)!;
		var selected = state.SelectedCards().Where(c => c.MultiplayerConstraint != CardMultiplayerConstraint.MultiplayerOnly).ToHashSet();
		var unlocked = player.Character.CardPool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint).ToList();
		var unselected = Reg.Packs.Except(state.Selected).SelectMany(PackRegistry.GetPackCards).ToList();
		Yes(selected.All(unlocked.Contains) && !unselected.Any(unlocked.Contains), "in-run pool = selected packs only");
		var options = CardCreationOptions.ForRoom(player, RoomType.Monster);
		var rewards = Enumerable.Range(0, 20).SelectMany(_ => CardFactory.CreateForReward(player, 3, options)).ToList();
		Yes(rewards.All(r => selected.Contains(ModelDb.GetById<CardModel>(r.Card.Id))), "60 combat reward cards, all from the selected packs");
		foreach (var type in new[] { CardType.Attack, CardType.Skill, CardType.Power })
		{
			Yes(CardFactory.FilterForCombat(unlocked).Count(c => c.Type == type) >= 2, $"random {type} generation has candidates", quiet: true);
		}
		var owned = player.RunState.CreateCard(state.SelectedCards().First(c => c.Rarity == CardRarity.Common), player);
		Yes(CardFactory.GetDefaultTransformationOptions(owned, isInCombat: false).All(selected.Contains), "transforms stay within the packs");
	}

	private static async Task CardVisualChecks(Player player)
	{
		var state = PackState.Get(player)!;
		var pack = state.Selected[0];
		var card = player.RunState.CreateCard(PackRegistry.GetPackCards(pack)[0], player);
		var node = NCard.Create(card)!;
		NRun.Instance!.GlobalUi.AddChild(node);
		node.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
		var label = node.Body.GetNodeOrNull<MegaCrit.Sts2.addons.mega_text.MegaLabel>(CardVisualPatch.PackNameLabelName);
		Yes(label is { Visible: true } && label.Text == PackRegistry.GetPackName(pack) && Math.Abs(label.Position.Y + label.Size.Y / 2 + 205) <= 6,
			$"pack name '{label?.Text}' on the frame's top edge (y {label?.Position.Y})");
		PackmasterSettings.OneFrameMode = true;
		Yes(ReferenceEquals(card.VisualCardPool, player.Character.CardPool), "one-frame mode uses the pack character's frame");
		PackmasterSettings.OneFrameMode = false;
		node.QueueFree();
		await Task.CompletedTask;
	}

	private static void PersistenceChecks(Player player)
	{
		var run = (RunState)player.RunState;
		var modifier = run.Modifiers.OfType<PackRunModifier>().Single();
		var before = PackState.Get(player)!.Selected.Select(p => p.Id).ToList();
		PackState.LoadRun(run, (PackRunModifier)ModifierModel.FromSerializable(modifier.ToSerializable()));
		Yes(PackState.Get(player)!.Selected.Select(p => p.Id).SequenceEqual(before) && !PackState.NeedsSetup(player), "a confirmed setup survives save/load");
	}

	// ---------------------------------------------------------------- helpers

	private static List<NPaginator> Paginators(Node root) =>
		root.GetChildren().SelectMany(c => c is NPaginator p ? new List<NPaginator> { p } : Paginators(c)).ToList();

	private static async Task Screenshot(string name)
	{
		if (DisplayServer.GetName() == "headless")
		{
			return;
		}
		await NGame.Instance!.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		var path = ProjectSettings.GlobalizePath($"user://packtest_{name}.png");
		NGame.Instance.GetViewport().GetTexture().GetImage().SavePng(path);
		Log.Info($"[PackmasterLib-AutoTest] screenshot: {path}");
	}

	private static void Yes(bool condition, string label, bool quiet = false)
	{
		_checks++;
		if (!condition)
		{
			_failures++;
			Report.AppendLine($"FAIL: {label}");
		}
		else if (!quiet)
		{
			Report.AppendLine($"PASS: {label}");
		}
	}
}

/// <summary>Auto-test runs are silent: every master-volume write becomes 0 while --packmastertest is set.</summary>
[HarmonyLib.HarmonyPatch]
internal static class AutoTestMute
{
	[HarmonyLib.HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Audio.NAudioManager), nameof(MegaCrit.Sts2.Core.Nodes.Audio.NAudioManager.SetMasterVol))]
	[HarmonyLib.HarmonyPrefix]
	private static void Fmod(ref float volume)
	{
		if (PackmasterAutoTest.Requested)
		{
			volume = 0f;
		}
	}

	[HarmonyLib.HarmonyPatch(typeof(MegaCrit.Sts2.Core.Audio.Debug.NDebugAudioManager), nameof(MegaCrit.Sts2.Core.Audio.Debug.NDebugAudioManager.SetMasterAudioVolume))]
	[HarmonyLib.HarmonyPrefix]
	private static void Godot(ref float linearVolume)
	{
		if (PackmasterAutoTest.Requested)
		{
			linearVolume = 0f;
		}
	}
}
