using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using Sts2Packmaster.Lib.Api;
using Sts2Packmaster.Lib.Core;

namespace Sts2Packmaster.Lib.Ui;

/// <summary>
/// Start-of-run pack setup screen, a port of STS1 Packmaster's PackSetupScreen: the run's fixed and
/// random packs are shown on top, draft rounds ("choice of 3") below one at a time (the picked pack
/// flies up, the others leave), and once every round is done the packs gather in the middle and the
/// confirm button appears. Hovering a pack shows its summary (ratings + tags) and credits.
/// The back button (or Esc) hides the screen to look at Neow or the map and brings it back; picks
/// are only saved once confirmed. Opened over the first room by <see cref="PackSetupTrigger"/>.
/// </summary>
public sealed class PackSetupScreen
{
	public const string NodeName = "PackmasterSetupScreen";

	private const float CardW = 300f;
	private const float CardH = 422f;
	private const float ChosenScale = 0.6f;
	private const float ChosenHoverScale = 0.75f;
	private const float ChoiceScale = 0.8f;
	private const float ChoiceHoverScale = 0.9f;
	private const float ConfirmScale = 0.75f;

	private static PackSetupScreen? _current;

	private readonly Player _player;
	private readonly PlayerPackState _state;
	private readonly Control _root;
	private readonly Control _content;
	private readonly MegaLabel _chosenLabel;
	private readonly MegaLabel _choiceLabel;
	private readonly MegaLabel _hiddenHint;
	private readonly NConfirmButton _confirm;
	private readonly NBackButton _back;
	private readonly Dictionary<PackDefinition, Control> _slots = new();
	private readonly List<PackDefinition> _choiceSet = new();
	private bool _busy;
	private bool _confirming;

	/// <summary>The open screen (null when closed).</summary>
	public static PackSetupScreen? Current => _current;

	public static bool IsOpen => _current != null && GodotObject.IsInstanceValid(_current._root);

	public Control Root => _root;

	public IReadOnlyList<PackDefinition> CurrentChoices => _choiceSet;

	public bool IsConfirming => _confirming;

	/// <summary>False while hidden with the back button.</summary>
	public bool IsShown => _content.Visible;

	public NConfirmButton ConfirmButton => _confirm;

	public NBackButton BackButton => _back;

	/// <summary>Open the setup screen for <paramref name="player"/> on top of the run UI (or show it again).</summary>
	public static PackSetupScreen? Open(Player player)
	{
		if (IsOpen)
		{
			_current!.SetShown(true);
			return _current;
		}
		var state = PackState.Get(player);
		var host = NRun.Instance?.GlobalUi;
		if (state == null || host == null)
		{
			return null;
		}
		_current = new PackSetupScreen(player, state, host);
		Log.Info($"[PackmasterLib] Pack setup screen opened ({state.Selected.Count} packs, {state.ChoicesLeft} draft round(s)).");
		return _current;
	}

	private PackSetupScreen(Player player, PlayerPackState state, Control host)
	{
		_player = player;
		_state = state;
		_root = new Control { Name = NodeName, MouseFilter = Control.MouseFilterEnum.Stop };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		host.AddChild(_root);
		_content = new Control { Name = "Content", MouseFilter = Control.MouseFilterEnum.Ignore };
		_content.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(_content);

		var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.85f), MouseFilter = Control.MouseFilterEnum.Ignore };
		backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_content.AddChild(backdrop);

		_chosenLabel = HeaderLabel(Loc("setup.chosen"));
		_choiceLabel = HeaderLabel(Loc("setup.choose"));

		// STS1 "Show pack ratings" toggle, bottom center.
		var toggleBox = new VBoxContainer { Size = new Vector2(640, 60), Position = new Vector2((Size.X - 640) / 2, Size.Y - 80) };
		_content.AddChild(toggleBox);
		PackConfigRows.AddRow(toggleBox, Loc("setup.ratings"), PackConfigRows.OnOff(), PackmasterSettings.HideSummaries ? 0 : 1,
			i => PackmasterSettings.HideSummaries = i == 0, 0.8f, 24);

		// Block every other hotkey (deck, map, pause...) while the screen is up; the back and confirm
		// buttons register after the block, so Esc/Enter reach them (the hotkey manager is a stack).
		NHotkeyManager.Instance?.AddBlockingScreen(_root);
		_confirm = ResourceLoader.Load<PackedScene>("res://scenes/ui/confirm_button.tscn").Instantiate<NConfirmButton>();
		_content.AddChild(_confirm);
		_confirm.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ => Confirm()));
		_back = ResourceLoader.Load<PackedScene>("res://scenes/ui/back_button.tscn").Instantiate<NBackButton>();
		_root.AddChild(_back);
		_back.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ => SetShown(!IsShown)));
		_back.Enable();

		// Shown above the back button while the screen is hidden (clear of Neow's options on the right).
		_hiddenHint = PackConfigRows.Label(Loc("setup.hidden_hint"), 22);
		_hiddenHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_hiddenHint.HorizontalAlignment = HorizontalAlignment.Center;
		_hiddenHint.Position = new Vector2(20, Size.Y - 470);
		_hiddenHint.Size = new Vector2(420, 90);
		_hiddenHint.Visible = false;
		var hintBg = new ColorRect { Color = new Color(0, 0, 0, 0.7f), MouseFilter = Control.MouseFilterEnum.Ignore, ShowBehindParent = true };
		hintBg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_hiddenHint.AddChild(hintBg);
		_root.AddChild(_hiddenHint);

		foreach (var pack in _state.Selected)
		{
			var slot = AddSlot(pack, ChosenScale);
			slot.Position = new Vector2(0, -CardH);
		}
		LayoutChosen(animate: true);
		if (_state.CurrentOffer().Count > 0)
		{
			StartChoice();
		}
		else
		{
			EnterConfirming();
		}
	}

	private Vector2 Size => _root.GetViewportRect().Size;

	private float ChosenY => 110f + 40f + CardH * ChosenScale / 2f;

	private float ChoiceY => ChosenY + CardH * ChosenScale / 2f + 150f + CardH * ChoiceScale / 2f;

	private static string Loc(string key) => PackRegistry.ResolveLocKey("gameplay_ui:PACKMASTER_LIB." + key);

	private MegaLabel HeaderLabel(string text)
	{
		var label = PackConfigRows.Label(text, 30);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.Size = new Vector2(Size.X, 50);
		_content.AddChild(label);
		return label;
	}

	/// <summary>Hide (look at Neow / the map) or show the screen again. Picks stay in memory.</summary>
	public void SetShown(bool shown)
	{
		if (shown == IsShown)
		{
			return;
		}
		NHoverTipSet.Clear();
		_content.Visible = shown;
		_hiddenHint.Visible = !shown;
		_root.MouseFilter = shown ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
		var hotkeys = NHotkeyManager.Instance;
		if (shown)
		{
			hotkeys?.AddBlockingScreen(_root);
			Reregister(_back);
			if (_confirming)
			{
				Reregister(_confirm);
			}
		}
		else
		{
			hotkeys?.RemoveBlockingScreen(_root);
		}
	}

	/// <summary>Move a button's hotkey bindings to the top of the hotkey stack.</summary>
	private static void Reregister(NButton button)
	{
		AccessTools.Method(typeof(NButton), "UnregisterHotkeys").Invoke(button, null);
		AccessTools.Method(typeof(NButton), "RegisterHotkeys").Invoke(button, null);
	}

	// ---------------------------------------------------------------- pack slots

	/// <summary>A pack preview card + author line + hitbox, positioned by its center.</summary>
	private Control AddSlot(PackDefinition pack, float scale)
	{
		var slot = new Control { Name = "Pack_" + pack.Id, MouseFilter = Control.MouseFilterEnum.Ignore, Scale = Vector2.One * scale };
		_content.AddChild(slot);
		var preview = PackRegistry.GetPreviewCard(pack);
		if (preview != null && NCard.Create(preview) is { } card)
		{
			slot.AddChild(card);
			card.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
		}
		else
		{
			var panel = new ColorRect { Color = new Color(0.12f, 0.12f, 0.16f), Position = new Vector2(-CardW / 2, -CardH / 2), Size = new Vector2(CardW, CardH) };
			slot.AddChild(panel);
			var name = PackConfigRows.Label(PackRegistry.GetPackName(pack), 36);
			name.HorizontalAlignment = HorizontalAlignment.Center;
			name.Position = new Vector2(-CardW / 2, -30);
			name.Size = new Vector2(CardW, 60);
			slot.AddChild(name);
		}
		if (!string.IsNullOrEmpty(pack.Author))
		{
			var author = PackConfigRows.Label(Loc("setup.author").Replace("{0}", pack.Author), 30);
			author.HorizontalAlignment = HorizontalAlignment.Center;
			author.Position = new Vector2(-CardW / 2 - 40, CardH / 2 + 6);
			author.Size = new Vector2(CardW + 80, 44);
			slot.AddChild(author);
		}
		var hitbox = new Control { Name = "Hitbox", Position = new Vector2(-CardW / 2, -CardH / 2), Size = new Vector2(CardW, CardH), MouseFilter = Control.MouseFilterEnum.Stop };
		slot.AddChild(hitbox);
		hitbox.Connect(Control.SignalName.MouseEntered, Callable.From(() => OnHover(pack, true)));
		hitbox.Connect(Control.SignalName.MouseExited, Callable.From(() => OnHover(pack, false)));
		hitbox.Connect(Control.SignalName.GuiInput, Callable.From<InputEvent>(e =>
		{
			if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
			{
				Click(pack);
			}
		}));
		_slots[pack] = slot;
		return slot;
	}

	private void OnHover(PackDefinition pack, bool hovered)
	{
		if (!_slots.TryGetValue(pack, out var slot) || !GodotObject.IsInstanceValid(slot))
		{
			return;
		}
		var hitbox = slot.GetNode<Control>("Hitbox");
		var inChoice = _choiceSet.Contains(pack);
		var scale = inChoice
			? (hovered ? ChoiceHoverScale : ChoiceScale)
			: _confirming ? (hovered ? ConfirmScale * 1.1f : ConfirmScale) : (hovered ? ChosenHoverScale : ChosenScale);
		slot.CreateTween().TweenProperty(slot, "scale", Vector2.One * scale, 0.12);
		slot.ZIndex = hovered ? 10 : 0;
		NHoverTipSet.Remove(hitbox);
		if (hovered)
		{
			NHoverTipSet.CreateAndShow(hitbox, SummaryTips(pack), slot.GlobalPosition.X > Size.X * 0.6f ? HoverTipAlignment.Left : HoverTipAlignment.Right);
		}
	}

	/// <summary>STS1 PackSummaryDisplay: ratings (unless hidden) + tags, then credits.</summary>
	public static List<IHoverTip> SummaryTips(PackDefinition pack)
	{
		var lines = new List<string>();
		var s = pack.Summary;
		if (!PackmasterSettings.HideSummaries)
		{
			lines.Add(Rating("summary.offense", s.Offense));
			lines.Add(Rating("summary.defense", s.Defense));
			lines.Add(Rating("summary.support", s.Support));
			lines.Add(Rating("summary.frontload", s.Frontload));
			lines.Add(Rating("summary.scaling", s.Scaling));
		}
		var tags = s.Tags.Count == 0
			? Loc("tag.None")
			: string.Join(Loc("summary.separator"), s.Tags.Select(t => "[gold]" + TagName(t) + "[/gold]"));
		lines.Add(Loc("summary.tags").Replace("{0}", tags));
		var tips = new List<IHoverTip>
		{
			new HoverTip(new LocString("gameplay_ui", "PACKMASTER_LIB.summary.title"), string.Join("\n", lines)),
		};
		if (pack.CreditsKey != null)
		{
			tips.Add(new HoverTip(new LocString("gameplay_ui", "PACKMASTER_LIB.summary.credits"), PackRegistry.ResolveLocKey(pack.CreditsKey)));
		}
		return tips;
	}

	private static string Rating(string key, int stars)
	{
		stars = Math.Clamp(stars, 0, 5);
		var text = "[gold]" + new string('★', stars) + "[/gold]" + new string('☆', 5 - stars);
		return Loc(key).Replace("{0}", text);
	}

	private static string TagName(string tag)
	{
		if (tag.Contains(':'))
		{
			return PackRegistry.ResolveLocKey(tag);
		}
		var key = "tag." + tag;
		var text = Loc(key);
		return text == "PACKMASTER_LIB." + key ? tag : text;
	}

	// ---------------------------------------------------------------- flow

	private void StartChoice()
	{
		_choiceSet.Clear();
		_choiceSet.AddRange(_state.CurrentOffer());
		if (_choiceSet.Count == 0)
		{
			EnterConfirming();
			return;
		}
		_chosenLabel.Position = new Vector2(0, 100);
		_choiceLabel.Position = new Vector2(0, ChoiceY - CardH * ChoiceScale / 2f - 70f);
		_chosenLabel.Visible = _choiceLabel.Visible = true;
		var spacing = CardW * ChoiceScale + 100f;
		var x = Size.X / 2f - (_choiceSet.Count - 1) / 2f * spacing;
		foreach (var pack in _choiceSet)
		{
			var slot = AddSlot(pack, ChoiceScale);
			slot.Position = new Vector2(x, Size.Y + CardH);
			slot.CreateTween().TweenProperty(slot, "position", new Vector2(x, ChoiceY), 0.5).SetTrans(Tween.TransitionType.Circ).SetEase(Tween.EaseType.Out);
			x += spacing;
		}
	}

	/// <summary>Click on a pack: picks it when it is one of the current choices.</summary>
	public void Click(PackDefinition pack)
	{
		if (_busy || _confirming || !IsShown || !_choiceSet.Contains(pack))
		{
			return;
		}
		_busy = true;
		NHoverTipSet.Clear();
		PackState.Pick(_player, pack);
		_choiceSet.Remove(pack);
		foreach (var other in _choiceSet)
		{
			var slot = _slots[other];
			_slots.Remove(other);
			var tween = slot.CreateTween();
			tween.TweenProperty(slot, "position:y", Size.Y + CardH, 0.4).SetTrans(Tween.TransitionType.Circ).SetEase(Tween.EaseType.In);
			tween.TweenCallback(Callable.From(slot.QueueFree));
		}
		_choiceSet.Clear();
		_slots[pack].Scale = Vector2.One * ChosenScale;
		LayoutChosen(animate: true);
		var timer = _root.GetTree().CreateTimer(0.45);
		timer.Connect(SceneTreeTimer.SignalName.Timeout, Callable.From(() =>
		{
			_busy = false;
			if (_state.CurrentOffer().Count > 0)
			{
				StartChoice();
			}
			else
			{
				EnterConfirming();
			}
		}));
	}

	/// <summary>Chosen packs in a centered row (fixed/random first, then picks).</summary>
	private void LayoutChosen(bool animate)
	{
		var packs = _state.Selected.Where(_slots.ContainsKey).ToList();
		var scale = _confirming ? ConfirmScale : ChosenScale;
		var spacing = Math.Min(CardW * scale + (_confirming ? 30f : 25f), (Size.X - 160f) / Math.Max(1, packs.Count));
		var y = _confirming ? Size.Y / 2f - 30f : ChosenY;
		var x = Size.X / 2f - (packs.Count - 1) / 2f * spacing;
		foreach (var pack in packs)
		{
			var slot = _slots[pack];
			var target = new Vector2(x, y);
			if (animate)
			{
				var tween = slot.CreateTween().SetParallel();
				tween.TweenProperty(slot, "position", target, 0.45).SetTrans(Tween.TransitionType.Circ).SetEase(Tween.EaseType.Out);
				tween.TweenProperty(slot, "scale", Vector2.One * scale, 0.45);
			}
			else
			{
				slot.Position = target;
				slot.Scale = Vector2.One * scale;
			}
			x += spacing;
		}
	}

	private void EnterConfirming()
	{
		_confirming = true;
		_choiceLabel.Visible = false;
		_chosenLabel.SetTextAutoSize(Loc("setup.confirm_hint"));
		_chosenLabel.Position = new Vector2(0, 110);
		_chosenLabel.Visible = true;
		LayoutChosen(animate: true);
		_confirm.Enable();
	}

	/// <summary>Drop the screen without confirming (tests: simulates quitting before a save/load).</summary>
	internal void CloseWithoutConfirming()
	{
		NHotkeyManager.Instance?.RemoveBlockingScreen(_root);
		NHoverTipSet.Clear();
		_root.QueueFree();
		_current = null;
	}

	/// <summary>Confirm: the pack pool becomes final and is saved; the run continues with Neow.</summary>
	public void Confirm()
	{
		if (!_confirming || !IsShown)
		{
			return;
		}
		NHoverTipSet.Clear();
		PackState.CompleteSetup(_player);
		NHotkeyManager.Instance?.RemoveBlockingScreen(_root);
		_confirm.Disable();
		_back.Disable();
		var tween = _root.CreateTween();
		tween.TweenProperty(_root, "modulate:a", 0f, 0.3);
		tween.TweenCallback(Callable.From(_root.QueueFree));
		_current = null;
		PackTopBarButton.Flash();
	}
}

/// <summary>
/// Opens the setup screen whenever the local player enters a room with an unfinished pack setup —
/// the first room (Neow) of a new run, or the restored room after loading a save taken before the
/// setup was confirmed. STS1 opened it at the Neow event the same way.
/// </summary>
public static class PackSetupTrigger
{
	private static bool _subscribed;

	internal static void Subscribe()
	{
		if (_subscribed)
		{
			return;
		}
		_subscribed = true;
		MegaCrit.Sts2.Core.Runs.RunManager.Instance.RoomEntered += () => Callable.From(() => TryOpen()).CallDeferred();
	}

	/// <summary>Open (or show again) the screen if the local player still has to set up packs.</summary>
	public static bool TryOpen()
	{
		try
		{
			var run = Sts2Packmaster.Lib.Patches.CardPoolPatch.CurrentRun;
			var me = run == null ? null : MegaCrit.Sts2.Core.Context.LocalContext.GetMe(run);
			if (me != null && PackState.NeedsSetup(me))
			{
				return PackSetupScreen.Open(me) != null;
			}
		}
		catch (Exception e)
		{
			Log.Error($"[PackmasterLib] Failed to open pack setup screen: {e}");
		}
		return false;
	}
}

/// <summary>
/// While the pack setup is unfinished, the run cannot move on: choosing a Neow option or a map node
/// brings the setup screen back instead (the player may still look at both through the back button).
/// </summary>
[HarmonyPatch]
internal static class PackSetupGatePatch
{
	[HarmonyPatch(typeof(NEventRoom), nameof(NEventRoom.OptionButtonClicked))]
	[HarmonyPrefix]
	private static bool EventOption() => !PackSetupTrigger.TryOpen();

	[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.OnMapPointSelectedLocally))]
	[HarmonyPrefix]
	private static bool MapPoint() => !PackSetupTrigger.TryOpen();
}
