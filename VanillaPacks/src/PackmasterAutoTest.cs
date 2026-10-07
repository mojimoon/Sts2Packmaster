using System.Text;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using Sts2Packmaster.Lib.Api;
using Sts2Packmaster.Lib.Core;
using Sts2Packmaster.Lib.Ui;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace Sts2Packmaster.VanillaPacks;

/// <summary>
/// Headless integration test, triggered by the --packmastertest launch argument.
/// Boots a real run with the pack character inside the real game, asserts every pack-related
/// invariant, then quits. All results are written to the game log.
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
		await Task.Delay(3000);
		NGame.Instance?.Quit();
	}

	private static async Task RunAllChecks()
	{
		// --- 1. registry / ModelDb integration ---
		var character = ModelDb.Character<VanillaSlinger>();
		Yes(character != null, "VanillaSlinger registered in ModelDb");
		Yes(ModelDb.AllCharacters.Contains(character), "VanillaSlinger present in ModelDb.AllCharacters");
		Yes(SaveManager.Instance.GenerateUnlockStateFromProgress().Characters.Contains(character), "VanillaSlinger unlocked for fresh save");
		var packs = PackmasterApi.GetPacks(character);
		Yes(packs.Count == 4, $"4 packs registered (got {packs.Count})");

		await UiChecks(character!);

		var pool = ModelDb.CardPool<VanillaSlingerCardPool>();
		var poolCards = pool.AllCards.ToList();
		Yes(poolCards.Count == 31, $"pool has 31 cards (got {poolCards.Count})");
		Yes(poolCards.All(c => ReferenceEquals(c.Pool, pool)), "every pack card resolves CardModel.Pool to the VanillaSlinger pool");
		var unlocked = pool.GetUnlockedCards(SaveManager.Instance.GenerateUnlockStateFromProgress(), CardMultiplayerConstraint.None).ToList();
		Yes(unlocked.All(c => !PackRegistry.IsPreviewCard(c)), "preview cards excluded from reward candidates (GetUnlockedCards)");
		Yes(unlocked.Count == 27, $"reward candidates = 27 (got {unlocked.Count})");
		Yes(ModelDb.AllCards.Contains(ModelDb.Card<PackStrike>()), "pack cards present in ModelDb.AllCards");

		// --- 2. pure resolver invariants over many seeds/configs ---
		var packIds = packs.Select(p => p.Id).ToHashSet();
		for (var i = 0; i < 50; i++)
		{
			var rng = new MegaCrit.Sts2.Core.Random.Rng(1000UL + (ulong)i);
			string[][] configs =
			{
				new[] { "strikes", "random", "choice", "choice", "random" },
				new[] { "random", "random", "random" },
				new[] { "none", "choice", "tricks", "random", "choice", "none", "none" },
				new[] { "powers", "defends", "random", "random" },
			};
			foreach (var tokens in configs)
			{
				var resolution = PackResolver.Resolve(VanillaPacksEntry.Registration!, tokens, allMode: false, rng);
				var ids = resolution.Selected.Select(p => p.Id).ToList();
				Yes(ids.Count == ids.Distinct().Count(), "resolver: no duplicate packs", quiet: true);
				Yes(ids.All(packIds.Contains), "resolver: only known packs selected", quiet: true);
				foreach (var token in tokens)
				{
					if (token is not ("random" or "choice" or "none"))
					{
						Yes(ids.Contains(token), $"resolver: fixed slot '{token}' honored", quiet: true);
					}
				}
				Yes(resolution.PendingChoices.Count <= tokens.Count(t => t == "choice"), "resolver: pending choices <= choice slots", quiet: true);
			}
			var all = PackResolver.Resolve(VanillaPacksEntry.Registration!, Array.Empty<string>(), allMode: true, rng);
			Yes(all.Selected.Count == 4 && all.PendingChoices.Count == 0, "resolver: all-packs mode selects everything", quiet: true);
		}
		Yes(true, $"resolver invariants over 50 seeds x 5 configs ({_checks} checks so far)");

		// --- 3. real run with the pack character ---
		var runState = await NGame.Instance.StartNewSingleplayerRun(
			character, shouldSave: false, ActModel.GetDefaultList().ToList(), Array.Empty<ModifierModel>(), "packtest", GameMode.Standard, 0);
		Yes(RunManager.Instance.IsInProgress, "run started");

		var modifier = runState.Modifiers.OfType<PackRunModifier>().FirstOrDefault();
		Yes(modifier != null, "PackRunModifier attached by RunStartPatch");
		if (modifier == null)
		{
			return;
		}
		var player = runState.Players[0];
		var state = PackState.Get(player);
		Yes(state != null, "PackState built for player");
		if (state == null)
		{
			return;
		}
		Log.Info($"[PackmasterLib-AutoTest] resolved packs: [{string.Join(", ", state.Selected.Select(p => p.Id))}], pending: {state.PendingChoices.Count}, config: '{modifier.PackConfig}'");
		Yes(state.Selected.Any(p => p.Id == "strikes"), "default config: fixed 'strikes' slot honored");
		Yes(state.PendingChoices.Count == 2, $"default config: 2 choice slots pending (got {state.PendingChoices.Count})");
		var expectedCardCount = state.Selected.Sum(p => p.CardTypes.Count);
		Yes(state.SelectedCardIds().Count == expectedCardCount,
			$"selected packs contribute exactly {expectedCardCount} cards (fixed+random resolved, {state.PendingChoices.Count} choice slots pending)");

		// --- 4. reward generation restricted to selected packs ---
		var options = CardCreationOptions.ForRoom(player, RoomType.Monster).WithFlags(CardCreationFlags.IsFromCombat);
		var selectedIds = state.SelectedCardIds();
		var generated = 0;
		for (var i = 0; i < 10; i++)
		{
			foreach (var result in CardFactory.CreateForReward(player, 3, options))
			{
				generated++;
				Yes(state.SelectedCardIds().Contains(result.Card.Id), $"reward card {result.Card.Id} belongs to selected packs", quiet: generated <= 3);
			}
		}
		Yes(generated == 30, $"30 reward cards generated (got {generated})");

		// rarity distribution sanity: selected pack is strikes-only (7 cards incl. basics) - roll must not throw
		Yes(true, "reward rarity rolling survived 10 rounds");

		// --- 5. unresolved choice slots fallback (no Neow in fresh save) ---
		PackState.ResolvePending(runState, player.NetId, "AutoTest fallback");
		Yes(state.PendingChoices.Count == 0, "fallback resolved all pending choice slots");
		Yes(state.Selected.Count == 4, $"5 slots over 4 packs -> all 4 packs, no more (got {state.Selected.Count})");
		Yes(state.Selected.Distinct().Count() == state.Selected.Count, $"no duplicate packs after choice fallback [{string.Join(", ", state.Selected.Select(p => p.Id))}]");
		selectedIds = state.SelectedCardIds();
		Yes(selectedIds.Count >= 7, "card pool grew after resolving choices");
		for (var i = 0; i < 5; i++)
		{
			foreach (var result in CardFactory.CreateForReward(player, 3, options))
			{
				Yes(selectedIds.Contains(result.Card.Id), "post-fallback reward within selected packs", quiet: true);
			}
		}
		Yes(true, "post-fallback rewards still filtered");

		// --- 6. persistence round-trip through the modifier's SavedProperties ---
		var serializable = modifier.ToSerializable();
		Yes(serializable.Props != null, "modifier props serialized");
		var restored = ModifierModel.FromSerializable(serializable) as PackRunModifier;
		Yes(restored != null, "modifier restored from serialized form");
		if (restored != null)
		{
			Yes(restored.PackConfig == modifier.PackConfig, "PackConfig survives round-trip");
			Yes(restored.PackSelected == modifier.PackSelected, "PackSelected survives round-trip");
		}

		// --- 7. card library injection ---
		var library = MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary.NCardLibrary.Create();
		Yes(library != null, "card library screen instantiated");
		if (library != null)
		{
			library.Initialize(runState);
			NGame.Instance.RootSceneContainer.AddChild(library);
			var poolFilters = HarmonyLib.AccessTools.Field(typeof(MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary.NCardLibrary), "_poolFilters").GetValue(library)
				as Dictionary<MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary.NCardPoolFilter, Func<CardModel, bool>>;
			Yes(poolFilters != null && poolFilters.Count == 9, $"library pool filters = 9 (got {poolFilters?.Count})");
			var cardPoolFilters = HarmonyLib.AccessTools.Field(typeof(MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary.NCardLibrary), "_cardPoolFilters").GetValue(library)
				as Dictionary<CharacterModel, MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary.NCardPoolFilter>;
			Yes(cardPoolFilters != null && cardPoolFilters.ContainsKey(character), "pack character has a library pool filter (no KeyNotFound)");
			var hasPackSortButton = library.GetNode("Sidebar/MarginContainer/TopVBox").GetChildren()
				.OfType<MegaCrit.Sts2.Core.Nodes.CommonUi.NCardViewSortButton>()
				.Any(b => PackRegistry.ResolveLocKey("gameplay_ui:PACKMASTER_LIB.sort.pack").StartsWith(b.GetNode<MegaCrit.Sts2.addons.mega_text.MegaLabel>("%Label").Text));
			Yes(hasPackSortButton, "'by pack' sort button injected into the library sidebar");
			var grid = library.GetNode<MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary.NCardLibraryGrid>("%CardGrid");
			Yes(Sts2Packmaster.Lib.Patches.PackmasterTestHooks.IsGridRegistered(grid), "library grid registered for pack-name search");
			library.OnSubmenuOpened();
			await Task.Delay(800); // let the game's deferred DisplayCards run first
			grid.FilterCards(_ => true, new List<MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary.SortingOrders>
			{
				(MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary.SortingOrders)100,
				MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary.SortingOrders.AlphabetAscending,
			});
			Log.Info($"[PackmasterLib-AutoTest] immediately after FilterCards: {string.Join(", ", grid.VisibleCards.Take(5).Select(c => c.Id.Entry))}");
			grid.SetCards(poolCards.Where(c => !PackRegistry.IsPreviewCard(c)).ToList(), PileType.None,
				new List<MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary.SortingOrders>
				{
					(MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary.SortingOrders)100,
					MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary.SortingOrders.AlphabetAscending,
				}, null);
			Log.Info($"[PackmasterLib-AutoTest] after direct SetCards (27 cards): {string.Join(", ", grid.VisibleCards.Take(7).Select(c => c.Id.Entry))}");
			await Task.Delay(600);
			Log.Info($"[PackmasterLib-AutoTest] 600ms later: {string.Join(", ", grid.VisibleCards.Take(7).Select(c => c.Id.Entry))}");
			var first = grid.VisibleCards.FirstOrDefault();
			Yes(first != null && first.Rarity == CardRarity.Basic && PackRegistry.GetPackOf(first)?.Id == "strikes",
				$"pack sort leads with first pack's basic cards (got {first?.Id.Entry})");
			var firstEight = grid.VisibleCards.Take(8).Select(c => PackRegistry.GetPackOf(c)?.Id).ToList();
			Yes(firstEight.Take(7).All(id => id == "strikes") && firstEight[7] == "defends",
				"pack sort groups all strikes-pack cards before later packs");
			var visiblePackCards = grid.VisibleCards.Count(c => c.Pool == pool);
			Yes(visiblePackCards == 27, $"unfiltered pack view shows all 27 visible cards (previews hidden, got {visiblePackCards})");
			Log.Info($"[PackmasterLib-AutoTest] pack-sorted first cards: {string.Join(", ", grid.VisibleCards.Take(8).Select(c => c.Id.Entry))}");

			library.QueueFree();
		}

		// --- 8. localization: lib ships all vanilla languages; demo zh+en with English fallback ---
		var languages = new[] { "deu", "eng", "esp", "fra", "ind", "ita", "jpn", "kor", "pol", "ptb", "rus", "spa", "tha", "tur", "zhs", "zht" };
		foreach (var lang in languages)
		{
			var tables = PackRegistry.SnapshotLocTablesFor(lang);
			Yes(tables.TryGetValue("gameplay_ui", out var gui) && gui.ContainsKey("PACKMASTER_LIB.panel.title"),
				$"lib loc present for '{lang}'", quiet: true);
			Yes(tables.TryGetValue("card_selection", out var cs) && cs.ContainsKey("PACKMASTER_LIB.choice.prompt"),
				$"choice prompt loc present for '{lang}'", quiet: true);
			Yes(tables.TryGetValue("card_library", out var cl) && cl.ContainsKey("PACKMASTER_LIB.pool.tip"),
				$"library tip loc present for '{lang}'", quiet: true);
			Yes(tables.TryGetValue("cards", out var cards) && cards.ContainsKey("PACK_STRIKE.title"),
				$"demo card loc present for '{lang}'", quiet: true);
		}
		Yes(true, $"lib loc present for all {languages.Length} vanilla languages");
		Yes(true, $"demo card loc present for all {languages.Length} vanilla languages (en fallback)");
		Yes(PackRegistry.SnapshotLocTablesFor("deu")["gameplay_ui"]["PACKMASTER_LIB.panel.title"] == "Kartenpacks",
			"german lib translation merged");
		Yes(PackRegistry.SnapshotLocTablesFor("jpn")["gameplay_ui"]["PACKMASTER_LIB.panel.title"] == "カードパック設定",
			"japanese lib translation merged");
		Yes(PackRegistry.SnapshotLocTablesFor("zhs")["gameplay_ui"]["PACKMASTER_LIB.panel.title"] == "卡包配置",
			"chinese lib translation merged");
		Yes(PackRegistry.SnapshotLocTablesFor("deu")["cards"]["PACK_STRIKE.title"] == "Strike",
			"demo falls back to English for non-zh languages");
		Yes(PackRegistry.SnapshotLocTablesFor("zhs")["cards"]["PACK_STRIKE.title"] != "Strike",
			"demo uses Chinese for zhs");
		var liveTitle = PackRegistry.ResolveLocKey("gameplay_ui:PACKMASTER_LIB.panel.title");
		var liveCard = PackRegistry.ResolveLocKey("cards:PACK_STRIKE.title");
		Yes(liveTitle.Length > 0 && liveTitle != "gameplay_ui:PACKMASTER_LIB.panel.title", $"live lib loc resolves (='{liveTitle}')");
		Yes(liveCard.Length > 0 && liveCard != "cards:PACK_STRIKE.title", $"live card loc resolves (='{liveCard}')");
	}

	/// <summary>What a player clicks: select the character, use the panel, use the settings group.</summary>
	private static async Task UiChecks(CharacterModel character)
	{
		foreach (var path in character.AssetPathsCharacterSelect.Concat(character.AssetPaths))
		{
			Yes(ResourceLoader.Exists(path), $"redirected asset exists: {path}", quiet: true);
		}
		Yes(true, "redirected character assets exist");
		Yes(new LocString("characters", character.CharacterSelectTitle).Exists(), "character title loc exists");

		var registration = VanillaPacksEntry.Registration!;
		var key = PackRegistry.RegistrationKey(registration);
		var savedConfig = PackConfigStore.Load(key);

		// Pack unlock API (default: all unlocked) + developer override.
		Yes(registration.UnlockedPacks.Count == 4, "all packs unlocked by default");
		registration.IsPackUnlocked = p => p.Id != "powers";
		var lockedRuns = Enumerable.Range(0, 30).Select(n => PackResolver.Resolve(registration, new[] { "random", "random", "random", "choice" }, false, new MegaCrit.Sts2.Core.Random.Rng((ulong)n))).ToList();
		Yes(lockedRuns.All(r => r.Selected.All(p => p.Id != "powers") && r.PendingChoices.All(c => c.All(p => p.Id != "powers"))), "locked pack never offered");
		Yes(PackResolver.Resolve(registration, Array.Empty<string>(), true, new MegaCrit.Sts2.Core.Random.Rng(1)).Selected.Count == 3, "all-packs mode = unlocked packs only");
		PackmasterSettings.UnlockAllPacks = true;
		Yes(registration.UnlockedPacks.Count == 4, "dev setting 'unlock all packs' overrides the rule");
		PackmasterSettings.UnlockAllPacks = false;
		registration.IsPackUnlocked = null;

		// STS1 "allow multiple None".
		var noneTokens = new[] { "none", "none", "none", "random" };
		Yes(PackResolver.Resolve(registration, noneTokens, false, new MegaCrit.Sts2.Core.Random.Rng(7)).Selected.Count == 3, "multiple None off: extra None slots roll random");
		PackmasterSettings.AllowMultipleNone = true;
		Yes(PackResolver.Resolve(registration, noneTokens, false, new MegaCrit.Sts2.Core.Random.Rng(7)).Selected.Count == 1, "multiple None on: every None slot stays empty");
		PackmasterSettings.AllowMultipleNone = false;

		for (var i = 0; i < 100 && NGame.Instance!.MainMenu == null; i++)
		{
			await Task.Delay(200);
		}
		var menu = NGame.Instance!.MainMenu;
		Yes(menu != null, "main menu loaded");
		if (menu == null)
		{
			return;
		}
		await Task.Delay(5000); // let the main menu's background "Common" preload finish, like a player would
		// Fresh test profile shows the first-launch early-access popup; close it like a player would.
		if (NGame.Instance.FindChild("*", true, false) != null
			&& NGame.Instance.FindChildren("*", "", true, false).OfType<MegaCrit.Sts2.Core.Nodes.Screens.MainMenu.NEarlyAccessDisclaimer>().FirstOrDefault() is { } disclaimer)
		{
			await disclaimer.CloseScreen();
			await Task.Delay(500);
		}
		var stack = menu.SubmenuStack;

		// STS1 default: 7 packs.
		PackConfigStore.Save(key, new PackSlotConfig { Slots = Enumerable.Repeat("random", 7).ToList() });
		var select = stack.GetSubmenuType<NCharacterSelectScreen>();
		select.InitializeSingleplayer();
		stack.Push(select);
		await Task.Delay(1500);
		var button = select.FindChild(character.Id.Entry + "_button", true, false) as NCharacterSelectButton;
		Yes(button != null, "character select button exists");
		button?.Select();
		await Task.Delay(1500);
		Yes(select.Lobby.LocalPlayer.character == character, "VanillaSlinger is selected on the character select screen");
		var panel = select.GetNodeOrNull<Control>("PackmasterLibPanel");
		Yes(panel is { Visible: true }, "pack config panel visible on character select");
		if (panel != null)
		{
			Yes(Paginators(panel).Count == 12, "panel has 12 paginator rows");
			var confirm = select.GetNode<Control>("ConfirmButton").GetGlobalRect();
			var rect = panel.GetGlobalRect();
			Yes(!rect.Intersects(confirm), $"7-pack panel does not cover the embark button (panel {rect}, button {confirm})");
			Yes(rect.End.X <= select.GetViewportRect().Size.X, "panel inside the screen");
			var labels = panel.FindChildren("*", "Label", true, false).OfType<Label>().Where(l => l.IsVisibleInTree()).ToList();
			Yes(labels.All(l => l.GetMinimumSize().X <= l.Size.X + 1 || l.ClipText), "panel labels clip instead of overlapping");
			await Screenshot("charselect_7packs");

			var header = panel.FindChildren("*", "", true, false).OfType<MegaCrit.Sts2.Core.Nodes.CommonUi.NCardViewSortButton>().First();
			header.EmitSignal(MegaCrit.Sts2.Core.Nodes.GodotExtensions.NClickableControl.SignalName.Released, header);
			await Task.Delay(300);
			Yes(panel.Size.Y < 80, $"collapsed panel shrinks to its header (height {panel.Size.Y})");
			await Screenshot("charselect_collapsed");
			header.EmitSignal(MegaCrit.Sts2.Core.Nodes.GodotExtensions.NClickableControl.SignalName.Released, header);
			await Task.Delay(300);
			Yes(panel.Size.Y > 200, "expanding restores the panel");
		}
		stack.Pop();
		await Task.Delay(500);
		if (savedConfig != null)
		{
			PackConfigStore.Save(key, savedConfig);
		}
		else
		{
			PackConfigStore.Save(key, PackSlotConfig.FromDefault(registration));
		}

		menu.OpenSettingsMenu();
		await Task.Delay(1500);
		var group = stack.FindChild(PackSettingsSection.GroupName, true, false) as Control;
		var options = stack.FindChild(PackSettingsSection.OptionsName, true, false) as Control;
		Yes(group is { Visible: true } && options != null, "settings screen has the Packmaster group");
		if (group != null && options != null)
		{
			Yes(!options.Visible, "settings group starts collapsed");
			var groupButton = group.GetNode<MegaCrit.Sts2.Core.Nodes.GodotExtensions.NButton>("PackmasterLibGroupButton");
			groupButton.EmitSignal(MegaCrit.Sts2.Core.Nodes.GodotExtensions.NClickableControl.SignalName.Released, groupButton);
			await Task.Delay(300);
			Yes(options.Visible, "group button expands the options");
			var toggles = Paginators(options);
			Yes(toggles.Count == 3, $"settings group has 3 options (got {toggles.Count})");
			if (toggles.Count == 3)
			{
				toggles[0].PageRight();
				Yes(PackmasterSettings.AllowMultipleNone, "settings toggle writes PackmasterSettings");
				toggles[0].PageLeft();
				Yes(!PackmasterSettings.AllowMultipleNone, "settings toggle restores");
			}
			// Scroll the group into view for the screenshot.
			var scroller = stack.FindChildren("*", "", true, false).OfType<MegaCrit.Sts2.Core.Nodes.GodotExtensions.NScrollableContainer>().First();
			HarmonyLib.AccessTools.Field(scroller.GetType(), "_targetDragPosY").SetValue(scroller, -group.Position.Y + 150);
			await Task.Delay(1000);
			await Screenshot("settings_group");
			groupButton.EmitSignal(MegaCrit.Sts2.Core.Nodes.GodotExtensions.NClickableControl.SignalName.Released, groupButton);
			await Task.Delay(300);
			Yes(!options.Visible, "group button collapses the options");
		}
		stack.Pop();
		await Task.Delay(500);
	}

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

	private static List<NPaginator> Paginators(Node root) =>
		root.GetChildren().SelectMany(c => c is NPaginator p ? new List<NPaginator> { p } : Paginators(c)).ToList();

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
