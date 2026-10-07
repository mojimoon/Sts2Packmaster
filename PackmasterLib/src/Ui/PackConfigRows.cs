using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using Sts2Packmaster.Lib.Api;
using Sts2Packmaster.Lib.Core;

namespace Sts2Packmaster.Lib.Ui;

/// <summary>
/// Builds the "label  &lt; value &gt;" rows that edit one character's pack config, using the game's
/// own settings paginator. Shared by the settings screen section and the character-select panel;
/// every change is saved to <see cref="PackConfigStore"/> immediately.
/// </summary>
[HarmonyPatch]
internal static class PackConfigRows
{
	public const int MinSlots = 3;
	public const int MaxSlots = 10;

	private const string PaginatorScene = "res://scenes/screens/paginator.tscn";
	private static readonly Color DividerColor = new(0.909804f, 0.862745f, 0.745098f, 0.25098f);

	private static readonly ConditionalWeakTable<NPaginator, Action<int>> Callbacks = new();
	private static readonly AccessTools.FieldRef<NPaginator, List<string>> OptionsRef = AccessTools.FieldRefAccess<NPaginator, List<string>>("_options");
	private static readonly AccessTools.FieldRef<NPaginator, int> IndexRef = AccessTools.FieldRefAccess<NPaginator, int>("_currentIndex");
	private static readonly AccessTools.FieldRef<NPaginator, MegaLabel> LabelRef = AccessTools.FieldRefAccess<NPaginator, MegaLabel>("_label");

	public static string Loc(string key) => PackRegistry.ResolveLocKey("gameplay_ui:PACKMASTER_LIB." + key);

	/// <summary>Adds the rows for <paramref name="registration"/> to <paramref name="parent"/>.</summary>
	/// <param name="onLayoutChanged">Called when rows are shown/hidden (slot count changed).</param>
	public static void Build(Container parent, PackCharacterRegistration registration, float scale = 1f, int fontSize = 26, Action? onLayoutChanged = null)
	{
		var key = PackRegistry.RegistrationKey(registration);
		var config = PackConfigStore.Load(key) ?? PackSlotConfig.FromDefault(registration);
		config.Normalize(MinSlots, MaxSlots);
		void Save() => PackConfigStore.Save(key, config);

		AddRow(parent, Loc("allpacks"), OnOff(), config.AllPacks ? 1 : 0, i =>
		{
			config.AllPacks = i == 1;
			Save();
		}, scale, fontSize);

		var slotRows = new List<Control>();
		void RefreshSlots()
		{
			for (var i = 0; i < slotRows.Count; i++)
			{
				slotRows[i].Visible = i < config.Slots.Count;
			}
		}

		var counts = Enumerable.Range(MinSlots, MaxSlots - MinSlots + 1).Select(n => n.ToString()).ToList();
		AddRow(parent, Loc("count"), counts, config.Slots.Count - MinSlots, i =>
		{
			config.SetSlotCount(i + MinSlots);
			Save();
			RefreshSlots();
			onLayoutChanged?.Invoke();
		}, scale, fontSize);

		var tokens = new List<string> { PackSlotToken.Random, PackSlotToken.Choice, PackSlotToken.None };
		var options = new List<string> { Loc("slot.random"), Loc("slot.choice"), Loc("slot.none") };
		foreach (var pack in registration.UnlockedPacks)
		{
			tokens.Add(pack.Id);
			options.Add(PackRegistry.GetPackName(pack));
		}
		for (var s = 0; s < MaxSlots; s++)
		{
			var slot = s;
			var current = slot < config.Slots.Count ? Math.Max(0, tokens.IndexOf(config.Slots[slot])) : 0;
			slotRows.Add(AddRow(parent, Loc("slot").Replace("{Num}", (slot + 1).ToString()), options, current, i =>
			{
				config.SetSlotCount(Math.Max(config.Slots.Count, slot + 1));
				config.Slots[slot] = tokens[i];
				Save();
			}, scale, fontSize));
		}
		RefreshSlots();
	}

	public static string[] OnOff() => new[]
	{
		new LocString("settings_ui", "VSYNC_OFF").GetFormattedText(),
		new LocString("settings_ui", "VSYNC_ON").GetFormattedText(),
	};

	/// <summary>A divider + row: label (clipped, never overlapping) on the left, paginator on the right.</summary>
	public static Control AddRow(Container parent, string text, IReadOnlyList<string> options, int index, Action<int> onChanged, float scale = 1f, int fontSize = 26)
	{
		var wrapper = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		wrapper.AddThemeConstantOverride("separation", 0);
		wrapper.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = DividerColor, MouseFilter = Control.MouseFilterEnum.Ignore });

		var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		margin.AddThemeConstantOverride("margin_left", (int)(12 * scale));
		margin.AddThemeConstantOverride("margin_right", (int)(12 * scale));
		var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", (int)(8 * scale));
		var label = Label(text, fontSize);
		label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		row.AddChild(label);
		// The paginator scene has fixed-size art; scale it inside a holder that reserves the scaled size.
		var holder = new Control { CustomMinimumSize = new Vector2(324, 64) * scale, MouseFilter = Control.MouseFilterEnum.Ignore };
		var paginator = Paginator(options, index, onChanged);
		paginator.Scale = Vector2.One * scale;
		holder.AddChild(paginator);
		row.AddChild(holder);
		margin.AddChild(row);
		wrapper.AddChild(margin);

		parent.AddChild(wrapper);
		return wrapper;
	}

	/// <summary>
	/// A text label that renders like the game's own: a <see cref="MegaLabel"/>, which applies the game's
	/// per-language font substitution (CJK etc., and whatever font mods hook there). Fixed font size with
	/// clipping: MegaLabel's auto-shrink measures before container layout and would stick at its minimum.
	/// </summary>
	public static MegaLabel Label(string text, int size)
	{
		var label = new MegaLabel
		{
			Text = text,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ClipText = true,
			AutoSizeEnabled = false,
		};
		label.AddThemeFontOverride("font", ResourceLoader.Load<Font>("res://themes/kreon_regular_shared.tres"));
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", new Color(1f, 0.96f, 0.886f));
		return label;
	}

	/// <summary>
	/// A plain NPaginator wearing the vanilla paginator scene's children (the vanilla subclasses each
	/// hard-wire one setting). Index changes are routed to <paramref name="onChanged"/> by the patch below.
	/// </summary>
	private static NPaginator Paginator(IReadOnlyList<string> options, int index, Action<int> onChanged)
	{
		var template = ResourceLoader.Load<PackedScene>(PaginatorScene).Instantiate<Control>();
		var paginator = new NPaginator
		{
			Name = "PackPaginator",
			CustomMinimumSize = new Vector2(324, 64),
			FocusMode = Control.FocusModeEnum.All,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		// Remember which nodes the scene root owned: "%Label"/"%VfxLabel" must be re-owned by the paginator.
		var owned = new List<Node>();
		CollectOwned(template, template, owned);
		foreach (var child in template.GetChildren())
		{
			template.RemoveChild(child);
			paginator.AddChild(child);
		}
		foreach (var node in owned)
		{
			node.Owner = paginator;
		}
		template.QueueFree();

		OptionsRef(paginator).AddRange(options);
		IndexRef(paginator) = Math.Clamp(index, 0, options.Count - 1);
		Callbacks.Add(paginator, onChanged);
		paginator.Ready += () => LabelRef(paginator).SetTextAutoSize(options[IndexRef(paginator)]);
		return paginator;
	}

	private static void CollectOwned(Node node, Node owner, List<Node> result)
	{
		foreach (var child in node.GetChildren())
		{
			if (child.Owner == owner)
			{
				result.Add(child);
			}
			CollectOwned(child, owner, result);
		}
	}

	[HarmonyPatch(typeof(NPaginator), "OnIndexChanged")]
	[HarmonyPostfix]
	private static void OnIndexChanged(NPaginator __instance, int index)
	{
		if (Callbacks.TryGetValue(__instance, out var callback))
		{
			LabelRef(__instance).SetTextAutoSize(OptionsRef(__instance)[index]);
			callback(index);
		}
	}
}
