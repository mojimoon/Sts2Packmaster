using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using Sts2Packmaster.Lib.Core;

namespace Sts2Packmaster.Lib.Patches;

/// <summary>Custom SortingOrders values used by the "sort by pack" button.</summary>
internal static class PackSorting
{
	/// <summary>By pack, ascending. (0-9 are vanilla values.)</summary>
	public const SortingOrders Ascending = (SortingOrders)100;

	/// <summary>By pack, descending.</summary>
	public const SortingOrders Descending = (SortingOrders)101;
}

/// <summary>
/// Implements the "by pack" ordering by intercepting NCardGrid.SetCards: when the requested
/// primary sort is the pack order we sort the cards ourselves (pack -> rarity -> name, mirroring
/// STS1's packSort) and neutralize the game's own sort with SortingOrders.Ascending.
/// </summary>
[HarmonyPatch]
internal static class CardGridPackSortPatch
{
	[HarmonyPatch(typeof(NCardGrid), nameof(NCardGrid.SetCards))]
	[HarmonyPrefix]
	private static void ApplyPackSort(
		ref IReadOnlyList<CardModel> cardsToDisplay,
		List<SortingOrders> sortingPriority)
	{
		try
		{
			if (sortingPriority == null || sortingPriority.Count == 0)
			{
				return;
			}
			var first = sortingPriority[0];
			var descending = first == PackSorting.Descending;
			if (first != PackSorting.Ascending && !descending)
			{
				return;
			}
			var list = cardsToDisplay.ToList();
			list.Sort((a, b) =>
			{
				var pack = PackRegistry.GetPackSortIndex(a).CompareTo(PackRegistry.GetPackSortIndex(b));
				if (pack != 0)
				{
					return descending ? -pack : pack;
				}
				var rarity = a.Rarity.CompareTo(b.Rarity);
				if (rarity != 0)
				{
					return rarity;
				}
				return string.Compare(a.Title, b.Title, StringComparison.Ordinal);
			});
			cardsToDisplay = list;
			sortingPriority[0] = SortingOrders.Ascending;
		}
		catch (Exception e)
		{
			Log.Error($"[PackmasterLib] CardGridPackSortPatch failed: {e}");
		}
	}
}

/// <summary>Public hooks for mod-side integration tests.</summary>
public static class PackmasterTestHooks
{
	public static bool IsGridRegistered(NCardLibraryGrid grid) => CardLibrarySearchPatch.GridOwners.TryGetValue(grid, out _);
}

/// <summary>
/// Lets pack names act as search terms in the card library: wraps the incoming filter of
/// NCardLibraryGrid.FilterCards with an OR-match on the owning library's search text.
/// </summary>
[HarmonyPatch]
internal static class CardLibrarySearchPatch
{
	// NCardLibrary -> its grid (populated by CardLibraryPatch when the library is built).
	internal static readonly ConditionalWeakTable<NCardLibraryGrid, NCardLibrary> GridOwners = new();

	[HarmonyPatch(typeof(NCardLibraryGrid), nameof(NCardLibraryGrid.FilterCards),
		new[] { typeof(Func<CardModel, bool>), typeof(List<SortingOrders>) })]
	[HarmonyPrefix]
	private static void AllowPackNameSearch(NCardLibraryGrid __instance, ref Func<CardModel, bool> filter)
	{
		try
		{
			if (!GridOwners.TryGetValue(__instance, out var library))
			{
				return;
			}
			var searchBar = AccessTools.Field(typeof(NCardLibrary), "_searchBar")?.GetValue(library) as NSearchBar;
			var query = searchBar?.Text?.Trim().ToLowerInvariant();
			if (string.IsNullOrEmpty(query))
			{
				return;
			}
			var inner = filter;
			filter = c => inner(c) || PackRegistry.MatchPackName(c, query);
		}
		catch (Exception e)
		{
			Log.Error($"[PackmasterLib] CardLibrarySearchPatch failed: {e}");
		}
	}
}
