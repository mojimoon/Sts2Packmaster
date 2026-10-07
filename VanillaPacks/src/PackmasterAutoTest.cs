using System.Text;
using Godot;
using MegaCrit.Sts2.Core.Context;
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
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
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
/// like a player: character select → embark → pack setup screen (draft, confirm) → Neow, then checks
/// the top bar button, the pack pool (rewards, random generation, transforms, Dusty Tome) and save
/// persistence. Without --headless it also saves screenshots to user://packtest_*.png.
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
		ResolverChecks();
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
		await TopBarChecks(player);
		PoolChecks(player);
		await CardVisualChecks(player);
		await PersistenceChecks(player);
	}

	// ---------------------------------------------------------------- 1. registry / pools

	private static void StaticChecks(CharacterModel character)
	{
		Yes(ModelDb.AllCharacters.Contains(character), "VanillaSlinger in ModelDb.AllCharacters");
		Yes(SaveManager.Instance.GenerateUnlockStateFromProgress().Characters.Contains(character), "VanillaSlinger unlocked for a fresh save");
		Yes(Reg.Packs.Count == 11, $"11 packs registered (got {Reg.Packs.Count})");
		foreach (var pack in Reg.Packs)
		{
			var cards = PackRegistry.GetPackCards(pack);
			var types = new[] { CardType.Attack, CardType.Skill, CardType.Power }.All(t => cards.Count(c => c.Type == t) >= 2);
			var rarities = new[] { CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare }.All(r => cards.Count(c => c.Rarity == r) >= 2);
			Yes(cards.Count == pack.CardTypes.Count && cards.Count >= 10 && types && rarities,
				$"pack '{pack.Id}': {cards.Count} cards, 2+ of each type and rarity");
			Yes(PackRegistry.GetPreviewCard(pack) != null && PackRegistry.GetCoverCard(PackRegistry.GetPreviewCard(pack)!) != null,
				$"pack '{pack.Id}' has a preview card with cover art", quiet: true);
		}
		var all = Reg.Packs.SelectMany(PackRegistry.GetPackCards).ToList();
		Yes(all.Count == all.Distinct().Count(), "no card is in two packs");

		var pool = ModelDb.CardPool<VanillaSlingerCardPool>();
		var own = pool.AllCards.ToList();
		Yes(own.Count == 2 + 11, $"own pool = 2 starters + 11 previews (got {own.Count})");
		Yes(ReferenceEquals(ModelDb.Card<Bloodletting>().Pool, ModelDb.CardPool<IroncladCardPool>()), "vanilla pack card keeps its original pool (Bloodletting → Ironclad)");
		Yes(ReferenceEquals(ModelDb.Card<JackOfAllTrades>().Pool, ModelDb.CardPool<ColorlessCardPool>()), "colorless pack card keeps the colorless pool");
		Yes(ReferenceEquals(ModelDb.Card<PackStrike>().Pool, pool), "starter copy lives in the character pool");

		var unlocked = pool.GetUnlockedCards(SaveManager.Instance.GenerateUnlockStateFromProgress(), CardMultiplayerConstraint.None).ToList();
		Yes(unlocked.All(c => !PackRegistry.IsPreviewCard(c)), "preview cards never in GetUnlockedCards");
		Yes(all.All(unlocked.Contains), "outside a run, GetUnlockedCards = every unlocked pack's cards");
		Yes(unlocked.Any(c => c.Rarity == CardRarity.Ancient && !ArchaicTooth.TranscendenceCards.Contains(c)), "Dusty Tome has an Ancient card to give");
	}

	private static void ResolverChecks()
	{
		string[][] configs =
		{
			new[] { "random", "random", "random", "random", "choice", "choice", "choice" },
			new[] { "pain", "random", "choice", "choice", "random" },
			new[] { "choice", "choice", "choice", "choice", "choice", "choice", "choice", "choice", "choice", "choice" },
			new[] { "none", "choice", "frost", "random", "choice" },
		};
		for (var seed = 0; seed < 60; seed++)
		{
			foreach (var tokens in configs)
			{
				var r = PackResolver.Resolve(Reg, tokens, false, new MegaCrit.Sts2.Core.Random.Rng((ulong)(seed + 7)));
				var offered = r.PendingChoices.SelectMany(c => c).ToList();
				Yes(r.Selected.Distinct().Count() == r.Selected.Count, "resolver: no duplicate packs", quiet: true);
				Yes(offered.Distinct().Count() == offered.Count && !offered.Any(r.Selected.Contains), "resolver: STS1 — a pack is offered at most once and never when owned", quiet: true);
				Yes(r.PendingChoices.All(c => c.Count is > 0 and <= 3), "resolver: 1-3 candidates per choice", quiet: true);
				foreach (var t in tokens.Where(t => t is not ("random" or "choice" or "none")))
				{
					Yes(r.Selected.Any(p => p.Id == t), $"resolver: fixed '{t}' honored", quiet: true);
				}
			}
		}
		Yes(true, "resolver invariants over 60 seeds x 4 configs");
		var allTen = PackResolver.Resolve(Reg, configs[2], false, new MegaCrit.Sts2.Core.Random.Rng(3));
		Yes(allTen.PendingChoices.Count == 4 && allTen.PendingChoices.Sum(c => c.Count) == 11,
			$"10 choice slots over 11 packs: 4 drafts (3+3+3+2), then the pool is empty (STS1 removes offered packs) (got {allTen.PendingChoices.Count})");

		Yes(Reg.UnlockedPacks.Count == 11, "all packs unlocked by default");
		Reg.IsPackUnlocked = p => p.Id != "pain";
		Yes(Enumerable.Range(0, 30).All(n =>
		{
			var r = PackResolver.Resolve(Reg, configs[0], false, new MegaCrit.Sts2.Core.Random.Rng((ulong)n));
			return r.Selected.Concat(r.PendingChoices.SelectMany(c => c)).All(p => p.Id != "pain");
		}), "IsPackUnlocked: a locked pack is never offered");
		PackmasterSettings.UnlockAllPacks = true;
		Yes(Reg.UnlockedPacks.Count == 11, "dev setting 'unlock all packs' overrides IsPackUnlocked");
		PackmasterSettings.UnlockAllPacks = false;
		Reg.IsPackUnlocked = null;

		var noneTokens = new[] { "none", "none", "none", "random" };
		Yes(PackResolver.Resolve(Reg, noneTokens, false, new MegaCrit.Sts2.Core.Random.Rng(7)).Selected.Count == 3, "multiple None off: extra None slots roll random");
		PackmasterSettings.AllowMultipleNone = true;
		Yes(PackResolver.Resolve(Reg, noneTokens, false, new MegaCrit.Sts2.Core.Random.Rng(7)).Selected.Count == 1, "multiple None on: every None slot stays empty");
		PackmasterSettings.AllowMultipleNone = false;
	}

	// ---------------------------------------------------------------- 2. menus

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
		var key = PackRegistry.RegistrationKey(Reg);
		PackConfigStore.Save(key, PackSlotConfig.FromDefault(Reg));
		var select = await OpenCharacterSelect(menu, character);
		var panel = select.GetNodeOrNull<Control>("PackmasterLibPanel");
		Yes(panel is { Visible: true }, "pack config panel visible on character select");
		if (panel != null)
		{
			Yes(Paginators(panel).Count == 12, "panel has 12 paginator rows");
			var confirm = select.GetNode<Control>("ConfirmButton").GetGlobalRect();
			Yes(!panel.GetGlobalRect().Intersects(confirm), "default 7-pack panel does not cover the embark button");
			await Screenshot("1_charselect");
		}
		stack.Pop();
		await Task.Delay(500);

		menu.OpenSettingsMenu();
		await Task.Delay(1500);
		var group = stack.FindChild(PackSettingsSection.GroupName, true, false) as Control;
		var options = stack.FindChild(PackSettingsSection.OptionsName, true, false) as Control;
		Yes(group is { Visible: true } && options is { Visible: false }, "settings: collapsed Packmaster group present");
		if (group != null && options != null)
		{
			var groupButton = group.GetNode<NButton>("PackmasterLibGroupButton");
			groupButton.EmitSignal(NClickableControl.SignalName.Released, groupButton);
			await Task.Delay(300);
			var toggles = Paginators(options);
			Yes(options.Visible && toggles.Count == 4, $"settings group expands to 4 options (got {toggles.Count})");
			if (toggles.Count == 4)
			{
				toggles[0].PageRight();
				Yes(PackmasterSettings.OneFrameMode, "settings toggle writes PackmasterSettings (one frame)");
				toggles[0].PageLeft();
				Yes(!PackmasterSettings.OneFrameMode, "settings toggle restores");
			}
			var scroller = stack.FindChildren("*", "", true, false).OfType<NScrollableContainer>().First();
			HarmonyLib.AccessTools.Field(scroller.GetType(), "_targetDragPosY").SetValue(scroller, -group.Position.Y + 150);
			await Task.Delay(1000);
			await Screenshot("2_settings");
			groupButton.EmitSignal(NClickableControl.SignalName.Released, groupButton);
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
		var button = select.FindChild(character.Id.Entry + "_button", true, false) as NCharacterSelectButton;
		button?.Select();
		await Task.Delay(1500);
		Yes(select.Lobby.LocalPlayer.character == character, "VanillaSlinger selected on the character select screen", quiet: true);
		return select;
	}

	// ---------------------------------------------------------------- 3. the real start of a run

	private static async Task<Player?> EmbarkAndSetup(NMainMenu menu, CharacterModel character)
	{
		// The fresh test profile has not revealed Neow yet; reveal it so the first room is Neow,
		// which is what the "setup screen vs. Neow rewards" checks are about.
		SaveManager.Instance.ObtainEpochOverride(MegaCrit.Sts2.Core.Timeline.EpochModel.GetId<MegaCrit.Sts2.Core.Timeline.Epochs.NeowEpoch>(), EpochState.Revealed);
		var select = await OpenCharacterSelect(menu, character);
		var embark = select.GetNode<NButton>("ConfirmButton");
		embark.EmitSignal(NClickableControl.SignalName.Released, embark);
		for (var i = 0; i < 150 && PackSetupScreen.Current == null; i++)
		{
			await Task.Delay(200);
			// Fresh profile: the first embark asks "enable tutorials?" — answer "no" like a player would.
			if (NGame.Instance!.FindChildren("*", "", true, false).OfType<MegaCrit.Sts2.Core.Nodes.Ftue.NAcceptTutorialsFtue>().FirstOrDefault() is { } ftue)
			{
				HarmonyLib.AccessTools.Method(ftue.GetType(), "NoTutorials").Invoke(ftue, new object?[] { null });
				Log.Info("[PackmasterLib-AutoTest] answered the first-run tutorials popup");
			}
		}
		var screen = PackSetupScreen.Current;
		Yes(screen != null, "embarking opens the pack setup screen in the first room");
		var run = CardPoolPatch.CurrentRun;
		var player = run == null ? null : LocalContext.GetMe(run);
		if (screen == null || player == null)
		{
			return null;
		}
		var state = PackState.Get(player)!;
		var modifier = run!.Modifiers.OfType<PackRunModifier>().Single();
		Yes(state.Selected.Count == 4 && state.PendingChoices.Count == 3, $"default setup: 4 random packs on top, 3 drafts (got {state.Selected.Count}+{state.PendingChoices.Count})");
		await Task.Delay(1200);
		Yes(screen.CurrentChoices.Count == 3 && !screen.IsConfirming, "first draft shows 3 candidate packs");
		await Screenshot("3_setup_draft");

		// Hover a candidate: STS1 summary tooltip.
		var hovered = screen.CurrentChoices[0];
		var hitbox = screen.Root.GetNode<Control>($"Pack_{hovered.Id}/Hitbox");
		hitbox.EmitSignal(Control.SignalName.MouseEntered);
		await Task.Delay(500);
		var tipText = string.Join("\n", PackSetupScreen.SummaryTips(hovered).OfType<MegaCrit.Sts2.Core.HoverTips.HoverTip>().Select(t => t.Description));
		Yes(tipText.Contains('★') && tipText.Contains(PackRegistry.ResolveLocKey("gameplay_ui:PACKMASTER_LIB.tag." + hovered.Summary.Tags[0])),
			"hover tooltip shows star ratings and tags");
		await Screenshot("4_setup_hover");
		hitbox.EmitSignal(Control.SignalName.MouseExited);

		for (var round = 0; round < 3; round++)
		{
			var candidates = screen.CurrentChoices.ToList();
			var pick = candidates[round % candidates.Count];
			screen.Click(pick);
			Yes(state.Selected.Contains(pick) && candidates.Where(c => c != pick).All(c => !state.Selected.Contains(c)),
				$"draft {round + 1}: picked '{pick.Id}', the other candidates were not added");
			Yes(modifier.PackSelected.Contains(pick.Id), $"draft {round + 1} persisted to the run save", quiet: true);
			await Task.Delay(1400);
		}
		Yes(screen.IsConfirming && state.PendingChoices.Count == 0 && state.Selected.Count == 7, $"all drafts done: 7 packs, confirm step (got {state.Selected.Count})");
		Yes(state.Selected.Distinct().Count() == 7, "7 distinct packs");
		await Screenshot("5_setup_confirm");
		Yes(PackState.NeedsSetup(player), "setup not done before confirming");
		screen.ConfirmButton.EmitSignal(NClickableControl.SignalName.Released, screen.ConfirmButton);
		await Task.Delay(800);
		Yes(!PackSetupScreen.IsOpen && !PackState.NeedsSetup(player) && modifier.PackSetupDone.Contains(player.NetId.ToString()), "confirm closes the screen and marks setup done");

		// Neow must keep its own rewards (the storage modifier is hidden from it).
		if (NEventRoom.Instance is { } room)
		{
			var ev = (EventModel)HarmonyLib.AccessTools.Field(typeof(NEventRoom), "_event").GetValue(room)!;
			var opts = ev.CurrentOptions;
			Log.Info($"[PackmasterLib-AutoTest] first room event {ev.Id.Entry}, options: {string.Join(", ", opts.Select(o => o.TextKey))}");
			Yes(opts.Count >= 2 && opts.All(o => !o.TextKey.Contains("PACK_RUN_MODIFIER")), $"Neow offers its normal rewards ({opts.Count} options)");
			await Screenshot("6_neow");
		}
		else
		{
			Yes(true, "first room is not an event (no Neow in this profile)");
		}
		return player;
	}

	// ---------------------------------------------------------------- 4. top bar + pool view

	private static async Task TopBarChecks(Player player)
	{
		var button = PackTopBarButton.Instance;
		Yes(button != null && GodotObject.IsInstanceValid(button), "packs button added to the top bar");
		if (button == null)
		{
			return;
		}
		var viewport = button.GetViewportRect().Size;
		Yes(button.GetGlobalRect().Position.X > viewport.X * 0.6f, "packs button sits in the top-right group");
		Yes(NRun.Instance!.GlobalUi.TopBar.FindChildren("*", "", true, false).OfType<MegaCrit.sts2.Core.Nodes.TopBar.NTopBarModifier>().Count() == 0,
			"no modifier icon in the top bar");
		button.EmitSignal(Control.SignalName.MouseEntered);
		await Task.Delay(400);
		await Screenshot("7_topbar_hover");
		button.EmitSignal(Control.SignalName.MouseExited);

		PackTopBarButton.TogglePoolView();
		await Task.Delay(1200);
		var view = NCapstoneContainer.Instance?.CurrentCapstoneScreen as NSimpleCardsViewScreen;
		var expected = PackTopBarButton.PoolCards(player);
		Yes(view != null, "clicking the button opens the pool view");
		Yes(expected.Count > 0 && expected.All(c => c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare)
			&& !expected.Any(c => c is PackStrike or PackDefend), $"pool view = {expected.Count} reward cards of the 7 packs (no starters)");
		await Screenshot("8_pool_view");
		PackTopBarButton.TogglePoolView();
		await Task.Delay(600);
		Yes(NCapstoneContainer.Instance?.CurrentCapstoneScreen == null, "clicking again closes the pool view");
	}

	// ---------------------------------------------------------------- 5. pack pool everywhere

	private static void PoolChecks(Player player)
	{
		var state = PackState.Get(player)!;
		var selected = state.SelectedCards().ToHashSet();
		var unlocked = player.Character.CardPool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint).ToList();
		selected.RemoveWhere(c => c.MultiplayerConstraint == CardMultiplayerConstraint.MultiplayerOnly);
		var unselected = Reg.Packs.Except(state.Selected).SelectMany(PackRegistry.GetPackCards).ToList();
		Yes(selected.All(unlocked.Contains) && !unselected.Any(unlocked.Contains), "in-run pool = selected packs only");

		var options = CardCreationOptions.ForRoom(player, RoomType.Monster);
		var rewards = Enumerable.Range(0, 20).SelectMany(_ => CardFactory.CreateForReward(player, 3, options)).ToList();
		Yes(rewards.Count == 60 && rewards.All(r => selected.Contains(ModelDb.GetById<CardModel>(r.Card.Id))),
			"60 combat reward cards, all from the selected packs");
		var shop = CardCreationOptions.ForRoom(player, RoomType.Shop);
		Yes(CardFactory.CreateForReward(player, 5, shop).All(r => selected.Contains(ModelDb.GetById<CardModel>(r.Card.Id))), "shop cards from the selected packs");
		foreach (var type in new[] { CardType.Attack, CardType.Skill, CardType.Power })
		{
			// "Add a random Attack/Skill/Power" effects (Infernal Blade, potions...) need each type in the pool.
			Yes(CardFactory.FilterForCombat(unlocked).Count(c => c.Type == type) >= 2,
				$"random {type} generation has candidates");
		}
		var common = state.Selected.SelectMany(PackRegistry.GetPackCards).First(c => c.Rarity == CardRarity.Common);
		var owned = player.RunState.CreateCard(common, player);
		var transforms = CardFactory.GetDefaultTransformationOptions(owned, isInCombat: false).ToList();
		Yes(transforms.Count > 0 && transforms.All(selected.Contains),
			$"transforming a vanilla pack card stays within the packs ({transforms.Count} options)");
	}

	private static async Task CardVisualChecks(Player player)
	{
		var state = PackState.Get(player)!;
		var pack = state.Selected[0];
		var card = player.RunState.CreateCard(PackRegistry.GetPackCards(pack)[0], player);
		var node = NCard.Create(card)!;
		NRun.Instance!.GlobalUi.AddChild(node);
		node.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
		var label = node.Body.GetNodeOrNull<Label>(CardVisualPatch.PackNameLabelName);
		Yes(label is { Visible: true } && label.Text == PackRegistry.GetPackName(pack), $"pack name '{label?.Text}' drawn above the card");
		Yes(ReferenceEquals(card.VisualCardPool, card.Pool), "default: vanilla card keeps its own frame");
		PackmasterSettings.OneFrameMode = true;
		Yes(ReferenceEquals(card.VisualCardPool, player.Character.CardPool), "one-frame mode: pack card uses the pack character's frame");
		PackmasterSettings.OneFrameMode = false;
		node.QueueFree();
		Yes(PackDisplayContext.For(ModelDb.Card<Bloodletting>()) == Reg, "in a pack run, owner-less card displays use the run's packs");
		await Task.CompletedTask;
	}

	// ---------------------------------------------------------------- 6. save/load

	private static async Task PersistenceChecks(Player player)
	{
		var run = (RunState)player.RunState;
		var modifier = run.Modifiers.OfType<PackRunModifier>().Single();
		var restored = (PackRunModifier)ModifierModel.FromSerializable(modifier.ToSerializable());
		Yes(restored.PackSelected == modifier.PackSelected && restored.PackSetupDone == modifier.PackSetupDone && restored.PackConfig == modifier.PackConfig,
			"pack state survives the modifier save round-trip");
		var before = PackState.Get(player)!.Selected.Select(p => p.Id).ToList();
		PackState.LoadRun(run, restored);
		Yes(PackState.Get(player)!.Selected.Select(p => p.Id).SequenceEqual(before) && !PackState.NeedsSetup(player), "LoadRun restores the same packs");

		// Save taken in the middle of the setup screen: pending drafts come back and the screen reopens.
		var midSetup = (PackRunModifier)ModifierModel.FromSerializable(modifier.ToSerializable());
		var spare = Reg.Packs.Except(PackState.Get(player)!.Selected).Take(3).Select(p => p.Id);
		midSetup.PackPending = $"{player.NetId}:{string.Join("|", spare)}";
		midSetup.PackSetupDone = "";
		PackState.LoadRun(run, midSetup);
		Yes(PackState.NeedsSetup(player) && PackState.Get(player)!.PendingChoices.Single().Count == 3, "mid-setup save restores the pending draft");
		PackSetupTrigger.TryOpen();
		await Task.Delay(1000);
		Yes(PackSetupScreen.Current is { CurrentChoices.Count: 3 }, "setup screen reopens with the saved draft");
		PackSetupScreen.Current?.Click(PackSetupScreen.Current.CurrentChoices[0]);
		await Task.Delay(1500);
		PackSetupScreen.Current?.Confirm();
		await Task.Delay(500);
		Yes(!PackSetupScreen.IsOpen && PackState.Get(player)!.Selected.Count == 8, "resumed setup completes");
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
