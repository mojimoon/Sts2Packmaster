using Godot;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using Sts2Packmaster.Lib.Core;

namespace Sts2Packmaster.Lib.Ui;

/// <summary>
/// The collapsible pack-configuration panel on the right side of the character select screen.
/// Visible only when a registered pack character is selected, in singleplayer.
/// Edits the same config as the settings screen section (<see cref="PackSettingsSection"/>).
/// </summary>
internal static class PackConfigPanel
{
	private const string SortButtonScene = "res://scenes/screens/card_library/library_sort_button.tscn";

	private sealed class PanelState
	{
		public required Control Root;
		public required VBoxContainer Body;
	}

	private static readonly ConditionalWeakTable<NCharacterSelectScreen, PanelState> Panels = new();

	private const float RowScale = 0.7f;
	private const int FontSize = 20;
	private const float Width = 440f;

	public static void Attach(NCharacterSelectScreen screen)
	{
		if (Panels.TryGetValue(screen, out _))
		{
			return;
		}
		// Fixed width, anchored top-right; height follows the content, so collapsing shrinks the whole panel
		// and up to ~10 rows stay above the embark button.
		var style = new StyleBoxFlat { BgColor = new Color(0.055f, 0.085f, 0.105f, 0.9f) };
		style.SetContentMarginAll(8);
		style.SetCornerRadiusAll(6);
		var root = new PanelContainer
		{
			Name = "PackmasterLibPanel",
			AnchorLeft = 1f,
			AnchorRight = 1f,
			AnchorTop = 0.06f,
			AnchorBottom = 0.06f,
			OffsetLeft = -Width - 24,
			OffsetRight = -24,
			MouseFilter = Control.MouseFilterEnum.Stop,
			Visible = false,
		};
		root.AddThemeStyleboxOverride("panel", style);
		screen.AddChild(root);

		var outer = new VBoxContainer();
		outer.AddThemeConstantOverride("separation", 4);
		root.AddChild(outer);
		var body = new VBoxContainer();
		body.AddThemeConstantOverride("separation", 0);

		var header = ResourceLoader.Load<PackedScene>(SortButtonScene).Instantiate<NCardViewSortButton>();
		header.CustomMinimumSize = new Vector2(0, 44);
		outer.AddChild(header);
		header.SetLabel(PackConfigRows.Loc("panel.title"));
		header.IsDescending = false;
		header.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ =>
		{
			body.Visible = !body.Visible;
			header.IsDescending = !body.Visible;
			FitHeight(root);
		}));
		outer.AddChild(body);

		Panels.Add(screen, new PanelState { Root = root, Body = body });
		Log.Info("[PackmasterLib] Character select panel attached.");
	}

	/// <summary>Shrink to the content's minimum height (a Control never shrinks on its own).</summary>
	private static void FitHeight(Control root) => Callable.From(() => root.Size = root.Size with { Y = 0 }).CallDeferred();

	public static void OnCharacterSelected(NCharacterSelectScreen screen, CharacterModel character)
	{
		if (!Panels.TryGetValue(screen, out var state))
		{
			return;
		}
		var registration = PackRegistry.GetRegistration(character);
		var inMultiplayer = screen.Lobby != null && screen.Lobby.NetService.Type != NetGameType.Singleplayer;
		state.Root.Visible = registration != null && !inMultiplayer;
		if (!state.Root.Visible)
		{
			return;
		}
		// Rebuild from the saved config every time (it may have been edited in the settings screen).
		foreach (var child in state.Body.GetChildren())
		{
			state.Body.RemoveChild(child);
			child.QueueFree();
		}
		PackConfigRows.Build(state.Body, registration!, RowScale, FontSize, () => FitHeight(state.Root));
		state.Body.Visible = true;
		FitHeight(state.Root);
	}
}
