using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using MegaCrit.Sts2.addons.mega_text;
using Sts2Packmaster.Lib.Api;

namespace Sts2Packmaster.Lib.Ui;

/// <summary>
/// Settings → General: a collapsible "Packmaster" group (same pattern as AutoAnthony: a cloned
/// Modding row whose button expands/collapses the options) holding the library-wide options of
/// <see cref="PackmasterSettings"/>. Per-character slot config lives on the character-select panel.
/// </summary>
[HarmonyPatch]
internal static class PackSettingsSection
{
	public const string GroupName = "PackmasterLibGroup";
	public const string OptionsName = "PackmasterLibOptions";

	[HarmonyPatch(typeof(NSettingsScreen), "_Ready")]
	[HarmonyPostfix]
	private static void Inject(NSettingsScreen __instance)
	{
		try
		{
			var panel = __instance.GetNode<NSettingsPanel>("%GeneralSettings");
			var content = panel.Content;
			var modding = content.GetNodeOrNull<Control>("Modding");
			if (modding == null || content.GetNodeOrNull(GroupName) != null)
			{
				return;
			}
			void Relayout()
			{
				panel.Call("RefreshSize");
				panel.Call("UpdateNavigation");
			}

			var options = new VBoxContainer { Name = OptionsName, Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
			options.AddThemeConstantOverride("separation", 0);
			AddToggle(options, "oneframe", () => PackmasterSettings.OneFrameMode, v => PackmasterSettings.OneFrameMode = v);
			AddToggle(options, "multinone", () => PackmasterSettings.AllowMultipleNone, v => PackmasterSettings.AllowMultipleNone = v);
			AddToggle(options, "unlockall", () => PackmasterSettings.UnlockAllPacks, v => PackmasterSettings.UnlockAllPacks = v);
			AddToggle(options, "autochoice", () => PackmasterSettings.AutoResolveChoices, v => PackmasterSettings.AutoResolveChoices = v);
			AddToggle(options, "excludeall", () => PackmasterSettings.ExcludeAllUnpicked, v => PackmasterSettings.ExcludeAllUnpicked = v);

			// Group row = a clone of the Modding row ("label .... [button]"), without its signal wiring.
			var group = (Control)modding.Duplicate((int)(Node.DuplicateFlags.Groups | Node.DuplicateFlags.Scripts | Node.DuplicateFlags.UseInstantiation));
			group.Name = GroupName;
			group.Visible = true;
			SetOwner(group, group);
			var button = group.GetNode<NOpenModdingScreenButton>("ModdingButton");
			button.Name = "PackmasterLibGroupButton";

			var divider = new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = new Color(0.909804f, 0.862745f, 0.745098f, 0.25098f), MouseFilter = Control.MouseFilterEnum.Ignore };
			var index = modding.GetIndex() + 1;
			content.AddChild(divider);
			content.MoveChild(divider, index);
			content.AddChild(group);
			content.MoveChild(group, index + 1);
			content.AddChild(options);
			content.MoveChild(options, index + 2);

			group.GetNode<MegaRichTextLabel>("Label").Text = PackConfigRows.Loc("settings.group");
			void SetButtonLabel() => button.GetNode<MegaLabel>("Label").SetTextAutoSize(PackConfigRows.Loc(options.Visible ? "settings.collapse" : "settings.expand"));
			button.Enable();
			SetButtonLabel();
			button.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ =>
			{
				options.Visible = !options.Visible;
				SetButtonLabel();
				Relayout();
			}));
			Relayout();
			Log.Info("[PackmasterLib] Settings group added.");
		}
		catch (Exception e)
		{
			Log.Error($"[PackmasterLib] Failed to add settings group: {e}");
		}
	}

	private static void AddToggle(Container parent, string key, Func<bool> get, Action<bool> set)
	{
		PackConfigRows.AddRow(parent, PackConfigRows.Loc("settings." + key), PackConfigRows.OnOff(), get() ? 1 : 0, i => set(i == 1));
	}

	private static void SetOwner(Node root, Node owner)
	{
		foreach (var child in root.GetChildren())
		{
			child.Owner = owner;
			SetOwner(child, owner);
		}
	}
}
