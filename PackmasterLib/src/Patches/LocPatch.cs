using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using Sts2Packmaster.Lib.Core;

namespace Sts2Packmaster.Lib.Patches;

/// <summary>
/// Merges loc entries registered through PackmasterApi.AddLoc into the game's tables on every
/// language (re)load. This gives mods localization without shipping a pck.
/// </summary>
[HarmonyPatch]
internal static class LocPatch
{
	[HarmonyPatch(typeof(LocManager), "SetLanguageInternal")]
	[HarmonyPostfix]
	private static void InjectModLoc(LocManager __instance)
	{
		try
		{
			var tables = Traverse.Create(__instance).Field("_tables").GetValue<Dictionary<string, LocTable>>();
			if (tables == null)
			{
				return;
			}
			var injected = 0;
			foreach (var (table, entries) in PackRegistry.SnapshotLocTablesFor(__instance.Language))
			{
				if (!tables.TryGetValue(table, out var locTable))
				{
					locTable = new LocTable(table, new Dictionary<string, string>());
					tables[table] = locTable;
				}
				locTable.MergeWith(entries);
				injected += entries.Count;
			}
			Log.Info($"[PackmasterLib] Injected {injected} loc entries for language '{__instance.Language}'.");
		}
		catch (Exception e)
		{
			Log.Error($"[PackmasterLib] LocPatch failed: {e}");
		}
	}
}
