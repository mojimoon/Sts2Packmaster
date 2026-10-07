using Sts2Packmaster.Lib.Api;
using MegaCrit.Sts2.addons.mega_text;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using Sts2Packmaster.Lib.Core;
using Sts2Packmaster.Lib.Ui;

namespace Sts2Packmaster.Lib.Patches;

/// <summary>
/// Injects, for every registered pack character:
///  - a pool filter button so their cards can be browsed under "their" tab,
///  - a "by pack" sort button,
/// and registers the library grid for pack-name search.
/// </summary>
[HarmonyPatch]
internal static class CardLibraryPatch
{
	private const string PoolToggleScene = "res://scenes/screens/card_library/library_pool_toggle.tscn";
	private const string SortButtonScene = "res://scenes/screens/card_library/library_sort_button.tscn";

	[HarmonyPatch(typeof(NCardLibrary), "_Ready")]
	[HarmonyPostfix]
	private static void InjectPackControls(NCardLibrary __instance)
	{
		try
		{
			var characters = PackRegistry.GetPackCharacters();
			if (characters.Count == 0)
			{
				return;
			}
			var grid = __instance.GetNode<NCardLibraryGrid>("%CardGrid");
			CardLibrarySearchPatch.GridOwners.Add(grid, __instance);

			var poolFilters = (Dictionary<NCardPoolFilter, Func<CardModel, bool>>)AccessTools
				.Field(typeof(NCardLibrary), "_poolFilters")!.GetValue(__instance)!;
			var cardPoolFilters = (Dictionary<CharacterModel, NCardPoolFilter>)AccessTools
				.Field(typeof(NCardLibrary), "_cardPoolFilters")!.GetValue(__instance)!;
			var updateFilter = AccessTools.Method(typeof(NCardLibrary), "UpdateCardPoolFilter");
			var lastHovered = AccessTools.Field(typeof(NCardLibrary), "_lastHoveredControl");

			var poolContainer = __instance.GetNode<Control>("Sidebar/MarginContainer/TopVBox/PoolFilters");
			var topVBox = __instance.GetNode<Control>("Sidebar/MarginContainer/TopVBox");

			foreach (var character in characters)
			{
				// --- pool filter button ---
				var filterButton = ResourceLoader.Load<PackedScene>(PoolToggleScene).Instantiate<NCardPoolFilter>();
				poolContainer.AddChild(filterButton);
				if (filterButton.GetNodeOrNull<TextureRect>("Image") is { } image)
				{
					image.Texture = character.CharacterSelectIcon;
				}
				filterButton.Loc = new LocString("card_library", "PACKMASTER_LIB.pool.tip");
				// The character's tab lists its own cards plus every pack card, including vanilla cards
				// referenced by packs (STS1 shows pack cards under the Packmaster tab).
				var ownPool = character.CardPool;
				var registration = PackRegistry.GetRegistration(character)!;
				var characterCards = PackRegistry.GetCharacterCards(registration).ToHashSet();
				poolFilters[filterButton] = c => !PackRegistry.IsPreviewCard(c) && (ReferenceEquals(c.Pool, ownPool) || characterCards.Contains(c));
				cardPoolFilters[character] = filterButton;
				filterButton.Connect(NCardPoolFilter.SignalName.Toggled, Callable.From<NCardPoolFilter>(f =>
				{
					// Pack names are drawn above cards while this character's tab is shown.
					PackDisplayContext.LibraryRegistration = f.IsSelected ? registration : null;
					PackLibraryFilter.Refresh(__instance);
					updateFilter!.Invoke(__instance, new[] { f });
				}));
				filterButton.Connect(Control.SignalName.FocusEntered,
					Callable.From(() => lastHovered!.SetValue(__instance, filterButton)));

				// --- sort by pack button (one per library, so only for the first character) ---
				if (characters.IndexOf(character) != 0)
				{
					continue;
				}
				var sortButton = ResourceLoader.Load<PackedScene>(SortButtonScene).Instantiate<NCardViewSortButton>();
				topVBox.AddChild(sortButton);
				topVBox.MoveChild(sortButton, topVBox.GetNode("CardTypeModule").GetIndex());
				sortButton.SetLabel(PackRegistry.ResolveLocKey("gameplay_ui:PACKMASTER_LIB.sort.pack"));
				sortButton.IsDescending = false;
				sortButton.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ => OnPackSortClicked(__instance, sortButton)));
				sortButton.Connect(Control.SignalName.FocusEntered,
					Callable.From(() => lastHovered!.SetValue(__instance, sortButton)));
				PackLibraryFilter.Attach(__instance, topVBox, sortButton);
			}
			Log.Info($"[PackmasterLib] Card library: injected controls for {characters.Count} pack character(s).");
		}
		catch (Exception e)
		{
			Log.Error($"[PackmasterLib] CardLibraryPatch failed: {e}");
		}
	}

	[HarmonyPatch(typeof(NCardLibrary), nameof(NCardLibrary.OnSubmenuClosed))]
	[HarmonyPostfix]
	private static void ClearDisplayContext()
	{
		PackDisplayContext.LibraryRegistration = null;
		PackLibraryFilter.Reset();
	}

	/// <summary>Mirrors NCardLibrary.OnCardTypeSort (IsDescending is read post-toggle).</summary>
	private static void OnPackSortClicked(NCardLibrary library, NCardViewSortButton button)
	{
		var priority = (List<SortingOrders>)AccessTools.Field(typeof(NCardLibrary), "_sortingPriority")!.GetValue(library)!;
		priority.Remove(PackSorting.Ascending);
		priority.Remove(PackSorting.Descending);
		priority.Insert(0, button.IsDescending ? PackSorting.Descending : PackSorting.Ascending);
		if (Traverse.Create(library).Method("DisplayCards").GetValue() is Task task)
		{
			TaskHelper.RunSafely(task);
		}
	}
}

/// <summary>
/// "Filter by pack" dropdown in the card library sidebar, under the "Pack" sort button. Both only show
/// while a pack character's tab is selected, and the dropdown lists that character's packs only.
/// </summary>
public static class PackLibraryFilter
{
	private const string DropdownScene = "res://scenes/ui/test_dropdown.tscn";
	private const string ItemScene = "res://scenes/ui/dropdown_item.tscn";

	/// <summary>The pack the library is filtered to (null = all packs).</summary>
	public static PackDefinition? SelectedPack { get; private set; }

	private sealed class Controls
	{
		public required NCardViewSortButton Sort;
		public required Control Holder;
		public required NDropdown Dropdown;
		public PackCharacterRegistration? ShownFor;
	}

	private static readonly ConditionalWeakTable<NCardLibrary, Controls> ByLibrary = new();

	internal static void Attach(NCardLibrary library, Control parent, NCardViewSortButton sortButton)
	{
		var holder = new Control { Name = "PackmasterPackFilter", CustomMinimumSize = new Vector2(0, 48), MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 5 };
		parent.AddChild(holder);
		parent.MoveChild(holder, sortButton.GetIndex() + 1);
		var scene = ResourceLoader.Load<PackedScene>(DropdownScene).Instantiate<Control>();
		holder.AddChild(scene);
		scene.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		var dropdown = scene.GetNode<NDropdown>("Dropdown");
		dropdown.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		void Fit()
		{
			scene.Position = Vector2.Zero;
			scene.Size = holder.Size;
			dropdown.Position = new Vector2(6, 4);
			dropdown.Size = new Vector2(Math.Max(120, holder.Size.X - 12), 40);
			dropdown.GetNode<Control>("%DropdownContainer").Size = new Vector2(dropdown.Size.X, dropdown.GetNode<Control>("%DropdownContainer").Size.Y);
		}
		holder.Connect(Control.SignalName.Resized, Callable.From(Fit));
		Fit();
		ClearItems(dropdown); // the dropdown scene ships with placeholder items
		var controls = new Controls { Sort = sortButton, Holder = holder, Dropdown = dropdown };
		ByLibrary.AddOrUpdate(library, controls);
		Refresh(library);
	}

	/// <summary>Show/hide and repopulate for the currently selected pack character tab.</summary>
	internal static void Refresh(NCardLibrary library)
	{
		if (!ByLibrary.TryGetValue(library, out var c))
		{
			return;
		}
		var registration = PackDisplayContext.LibraryRegistration;
		c.Sort.Visible = c.Holder.Visible = registration != null;
		if (registration == c.ShownFor)
		{
			return;
		}
		c.ShownFor = registration;
		SelectedPack = null;
		var items = ClearItems(c.Dropdown);
		SetCurrent(c.Dropdown, Loc("filter.all"));
		if (registration == null)
		{
			return;
		}
		var packs = new List<PackDefinition?> { null };
		packs.AddRange(registration.Packs);
		foreach (var pack in packs)
		{
			var item = ResourceLoader.Load<PackedScene>(ItemScene).Instantiate<NDropdownItem>();
			items.AddChild(item);
			var text = pack == null ? Loc("filter.all") : PackRegistry.GetPackName(pack);
			item.Text = text;
			item.Connect(NDropdownItem.SignalName.Selected, Callable.From<NDropdownItem>(_ => Select(library, pack, text)));
		}
		Traverse.Create(c.Dropdown.GetNode("%DropdownContainer")).Method("RefreshLayout").GetValue();
	}

	private static Control ClearItems(NDropdown dropdown)
	{
		var items = dropdown.GetNode<Control>("%DropdownContainer").GetNode<Control>("VBoxContainer");
		foreach (var child in items.GetChildren())
		{
			items.RemoveChild(child);
			child.QueueFree();
		}
		return items;
	}

	/// <summary>Filter the library to <paramref name="pack"/> (null = all packs) and redisplay.</summary>
	public static void Select(NCardLibrary library, PackDefinition? pack, string? text = null)
	{
		SelectedPack = pack;
		if (ByLibrary.TryGetValue(library, out var c))
		{
			SetCurrent(c.Dropdown, text ?? (pack == null ? Loc("filter.all") : PackRegistry.GetPackName(pack)));
			Traverse.Create(c.Dropdown).Method("CloseDropdown").GetValue();
		}
		if (Traverse.Create(library).Method("DisplayCards").GetValue() is Task task)
		{
			TaskHelper.RunSafely(task);
		}
	}

	internal static void Reset() => SelectedPack = null;

	/// <summary>Applied by the grid filter: true when the card passes the pack filter.</summary>
	internal static bool Passes(CardModel card) =>
		SelectedPack == null || PackRegistry.GetPackOf(card, PackDisplayContext.LibraryRegistration) == SelectedPack;

	private static void SetCurrent(NDropdown dropdown, string text) =>
		dropdown.GetNode<MegaLabel>("CurrentOption/Label").SetTextAutoSize(text);

	private static string Loc(string key) => PackRegistry.ResolveLocKey("gameplay_ui:PACKMASTER_LIB." + key);
}
