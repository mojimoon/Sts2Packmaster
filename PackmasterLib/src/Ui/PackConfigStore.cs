using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace Sts2Packmaster.Lib.Ui;

/// <summary>
/// Persists the per-character pack configuration edited on the character-select panel.
/// Stored at user://PackmasterLib/config.json so it survives restarts.
/// </summary>
public static class PackConfigStore
{
	private const string Path = "user://PackmasterLib/config.json";

	private static readonly JsonSerializerOptions Options = new()
	{
		WriteIndented = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	};

	private static Dictionary<string, StoredConfig>? _cache;
	private static bool _loaded;

	private sealed class StoredConfig
	{
		public bool allPacks { get; set; }

		public List<string> slots { get; set; } = new();
	}

	public static PackSlotConfig? Load(string characterKey)
	{
		EnsureLoaded();
		if (_cache!.TryGetValue(characterKey, out var stored))
		{
			return new PackSlotConfig
			{
				AllPacks = stored.allPacks,
				Slots = stored.slots.ToList(),
			};
		}
		return null;
	}

	public static void Save(string characterKey, PackSlotConfig config)
	{
		EnsureLoaded();
		_cache![characterKey] = new StoredConfig
		{
			allPacks = config.AllPacks,
			slots = config.Slots.ToList(),
		};
		try
		{
			DirAccess.MakeDirRecursiveAbsolute("user://PackmasterLib");
			using var file = Godot.FileAccess.Open(Path, Godot.FileAccess.ModeFlags.Write);
			file?.StoreString(JsonSerializer.Serialize(_cache, Options));
		}
		catch (Exception e)
		{
			Log.Warn($"[PackmasterLib] Failed to save pack config: {e.Message}");
		}
	}

	private static void EnsureLoaded()
	{
		if (_loaded)
		{
			return;
		}
		_loaded = true;
		_cache = new Dictionary<string, StoredConfig>();
		try
		{
			if (!Godot.FileAccess.FileExists(Path))
			{
				return;
			}
			using var file = Godot.FileAccess.Open(Path, Godot.FileAccess.ModeFlags.Read);
			var text = file?.GetAsText();
			if (!string.IsNullOrWhiteSpace(text))
			{
				_cache = JsonSerializer.Deserialize<Dictionary<string, StoredConfig>>(text) ?? new Dictionary<string, StoredConfig>();
			}
		}
		catch (Exception e)
		{
			Log.Warn($"[PackmasterLib] Failed to load pack config: {e.Message}");
		}
	}
}
