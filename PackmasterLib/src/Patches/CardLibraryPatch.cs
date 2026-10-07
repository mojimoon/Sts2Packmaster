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
				var ownPool = character.CardPool;
				poolFilters[filterButton] = c => ReferenceEquals(c.Pool, ownPool);
				cardPoolFilters[character] = filterButton;
				filterButton.Connect(NCardPoolFilter.SignalName.Toggled,
					Callable.From<NCardPoolFilter>(f => updateFilter!.Invoke(__instance, new[] { f })));
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
			}
			Log.Info($"[PackmasterLib] Card library: injected controls for {characters.Count} pack character(s).");
		}
		catch (Exception e)
		{
			Log.Error($"[PackmasterLib] CardLibraryPatch failed: {e}");
		}
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
