using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.TopBar;
using MegaCrit.Sts2.Core.Runs;
using Sts2Packmaster.Lib.Core;
using Sts2Packmaster.Lib.Patches;

namespace Sts2Packmaster.Lib.Ui;

/// <summary>
/// Top-right "packs" button for pack characters (STS1 CurrentRunCardsTopPanelItem): hovering lists
/// the run's packs, clicking shows the run's card pool (Common/Uncommon/Rare pack cards, no
/// starters), sorted by pack, rarity and name. It is a vanilla deck button scene whose behavior is
/// swapped by the patches below (matched by node name).
/// </summary>
[HarmonyPatch]
public static class PackTopBarButton
{
	public const string NodeName = "PackmasterPacksButton";

	private static NSimpleCardsViewScreen? _poolScreen;

	private static bool IsOurs(Node node) => node.Name == NodeName;

	/// <summary>The button in the current run's top bar (null if absent).</summary>
	public static NTopBarDeckButton? Instance { get; private set; }

	[HarmonyPatch(typeof(NTopBar), nameof(NTopBar.Initialize))]
	[HarmonyPostfix]
	private static void AddButton(NTopBar __instance, IRunState runState)
	{
		try
		{
			var me = LocalContext.GetMe(runState);
			if (me == null || PackRegistry.GetRegistration(me.Character) == null)
			{
				return;
			}
			var right = __instance.GetNode<Control>("RightAlignedStuff");
			var button = ResourceLoader.Load<PackedScene>("res://scenes/ui/top_bar/top_bar_deck_button.tscn").Instantiate<NTopBarDeckButton>();
			button.Name = NodeName;
			right.AddChild(button);
			right.MoveChild(button, right.GetNode("Map").GetIndex());
			button.GetNode<Control>("DeckCardCount").Visible = false;
			button.GetNodeOrNull<Control>("HotkeyIcon")?.Set("visible", false);
			if (button.GetNodeOrNull<TextureRect>("Control/Icon") is { } icon)
			{
				icon.Texture = ResourceLoader.Load<Texture2D>("res://images/packed/modifiers/draft.png");
				icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
				icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			}
			Instance = button;
		}
		catch (Exception e)
		{
			Log.Error($"[PackmasterLib] Failed to add packs top bar button: {e}");
		}
	}

	private static IHoverTip Tip(MegaCrit.Sts2.Core.Entities.Players.Player me)
	{
		var state = PackState.Get(me);
		var names = state == null ? "" : string.Join("\n", state.Selected.Select(p => PackRegistry.GetPackName(p)));
		var text = PackRegistry.ResolveLocKey("gameplay_ui:PACKMASTER_LIB.topbar.desc").Replace("{0}", names);
		return new HoverTip(new LocString("gameplay_ui", "PACKMASTER_LIB.topbar.title"), text);
	}

	/// <summary>The run's pool view (STS1 order: pack, rarity, name; only reward rarities).</summary>
	public static List<CardModel> PoolCards(MegaCrit.Sts2.Core.Entities.Players.Player me)
	{
		var state = PackState.Get(me);
		if (state == null)
		{
			return new List<CardModel>();
		}
		var result = new List<CardModel>();
		foreach (var pack in state.Selected)
		{
			result.AddRange(PackRegistry.GetPackCards(pack)
				.Where(c => c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare)
				.OrderBy(c => c.Rarity)
				.ThenBy(c => c.Title, StringComparer.CurrentCulture));
		}
		return result;
	}

	public static void TogglePoolView()
	{
		if (NCapstoneContainer.Instance?.CurrentCapstoneScreen is { } current && current == _poolScreen)
		{
			NCapstoneContainer.Instance.Close();
			return;
		}
		var me = LocalContext.GetMe(CardPoolPatch.CurrentRun);
		if (me == null)
		{
			return;
		}
		var cards = PoolCards(me).Select(c => new CardPileAddResult { success = true, cardAdded = c.ToMutable() }).ToList();
		_poolScreen = NSimpleCardsViewScreen.ShowScreen(cards, new LocString("gameplay_ui", "PACKMASTER_LIB.topbar.pool")) as NSimpleCardsViewScreen;
	}

	/// <summary>Pulse the button (after the pack setup is confirmed).</summary>
	public static void Flash()
	{
		if (Instance == null || !GodotObject.IsInstanceValid(Instance))
		{
			return;
		}
		var tween = Instance.CreateTween();
		tween.TweenProperty(Instance, "scale", Vector2.One * 1.35f, 0.2).SetTrans(Tween.TransitionType.Back);
		tween.TweenProperty(Instance, "scale", Vector2.One, 0.4);
	}

	// ---------------------------------------------------------------- behavior swap for our instance

	[HarmonyPatch(typeof(NTopBarDeckButton), "OnRelease")]
	[HarmonyPrefix]
	private static bool OnRelease(NTopBarDeckButton __instance)
	{
		if (!IsOurs(__instance))
		{
			return true;
		}
		TogglePoolView();
		AccessTools.Method(typeof(NTopBarButton), "UpdateScreenOpen").Invoke(__instance, null);
		return false;
	}

	[HarmonyPatch(typeof(NTopBarDeckButton), "IsOpen")]
	[HarmonyPrefix]
	private static bool IsOpen(NTopBarDeckButton __instance, ref bool __result)
	{
		if (!IsOurs(__instance))
		{
			return true;
		}
		__result = _poolScreen != null && NCapstoneContainer.Instance?.CurrentCapstoneScreen == _poolScreen;
		return false;
	}

	/// <summary>The deck button unsubscribes from its deck pile here; ours never had one.</summary>
	[HarmonyPatch(typeof(NTopBarDeckButton), nameof(NTopBarDeckButton._Notification))]
	[HarmonyPrefix]
	private static bool Notification(NTopBarDeckButton __instance) => !IsOurs(__instance);

	/// <summary>Replace the deck tooltip ("Deck (D)") with the run's pack list.</summary>
	[HarmonyPatch(typeof(NTopBarDeckButton), "OnFocus")]
	[HarmonyPostfix]
	private static void OnFocus(NTopBarDeckButton __instance)
	{
		if (!IsOurs(__instance) || LocalContext.GetMe(CardPoolPatch.CurrentRun) is not { } me)
		{
			return;
		}
		NHoverTipSet.Remove(__instance);
		var tips = NHoverTipSet.CreateAndShow(__instance, Tip(me));
		tips?.SetGlobalPosition(__instance.GlobalPosition + new Vector2(__instance.Size.X - tips.Size.X, __instance.Size.Y + 20f));
	}

	/// <summary>No deck hotkey on ours.</summary>
	[HarmonyPatch(typeof(NTopBarDeckButton), "Hotkeys", MethodType.Getter)]
	[HarmonyPrefix]
	private static bool Hotkeys(NTopBarDeckButton __instance, ref string[] __result)
	{
		if (!IsOurs(__instance))
		{
			return true;
		}
		__result = Array.Empty<string>();
		return false;
	}
}
