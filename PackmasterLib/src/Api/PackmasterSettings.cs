using System.Text.Json;
using MegaCrit.Sts2.Core.Logging;

namespace Sts2Packmaster.Lib.Api;

/// <summary>
/// Library-wide options edited in Settings → General → Packmaster (STS1 Packmaster's mod config
/// plus a few developer switches). Stored at user://PackmasterLib/settings.json.
/// </summary>
public static class PackmasterSettings
{
	private const string Path = "user://PackmasterLib/settings.json";

	private sealed class Data
	{
		public bool AllowMultipleNone { get; set; }
		public bool UnlockAllPacks { get; set; }
		public bool AutoResolveChoices { get; set; }
	}

	private static Data? _data;

	private static Data Current => _data ??= Load();

	/// <summary>STS1 "Allow selecting None multiple times". Off: only the first "none" slot counts, extra ones roll random.</summary>
	public static bool AllowMultipleNone
	{
		get => Current.AllowMultipleNone;
		set { Current.AllowMultipleNone = value; Save(); }
	}

	/// <summary>Developer: every pack counts as unlocked regardless of <see cref="PackCharacterRegistration.IsPackUnlocked"/>.</summary>
	public static bool UnlockAllPacks
	{
		get => Current.UnlockAllPacks;
		set { Current.UnlockAllPacks = value; Save(); }
	}

	/// <summary>Developer: "choice of 3" slots resolve randomly at run start (no pick-a-pack screen).</summary>
	public static bool AutoResolveChoices
	{
		get => Current.AutoResolveChoices;
		set { Current.AutoResolveChoices = value; Save(); }
	}

	private static Data Load()
	{
		try
		{
			if (Godot.FileAccess.FileExists(Path))
			{
				using var file = Godot.FileAccess.Open(Path, Godot.FileAccess.ModeFlags.Read);
				return JsonSerializer.Deserialize<Data>(file.GetAsText()) ?? new Data();
			}
		}
		catch (Exception e)
		{
			Log.Warn($"[PackmasterLib] Failed to load settings: {e.Message}");
		}
		return new Data();
	}

	private static void Save()
	{
		try
		{
			Godot.DirAccess.MakeDirRecursiveAbsolute("user://PackmasterLib");
			using var file = Godot.FileAccess.Open(Path, Godot.FileAccess.ModeFlags.Write);
			file?.StoreString(JsonSerializer.Serialize(Current));
		}
		catch (Exception e)
		{
			Log.Warn($"[PackmasterLib] Failed to save settings: {e.Message}");
		}
	}
}
