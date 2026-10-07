using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using Sts2Packmaster.Lib.Api;
using Sts2Packmaster.Lib.Core;

namespace Sts2Packmaster.Lib.Patches;

/// <summary>Which pack character a card is being shown for (STS1: "in a Packmaster run or library tab").</summary>
public static class PackDisplayContext
{
	/// <summary>Set while the card library shows a pack character's pool filter.</summary>
	public static PackCharacterRegistration? LibraryRegistration { get; internal set; }

	/// <summary>The pack character whose packs apply to this card on screen, if any.</summary>
	public static PackCharacterRegistration? For(CardModel card)
	{
		if (card.IsMutable)
		{
			// Owner is null for display copies (setup screen, pool view); fall back to the local player.
			var owner = (MegaCrit.Sts2.Core.Entities.Players.Player?)card.Owner;
			if (owner != null)
			{
				return PackRegistry.GetRegistration(owner.Character);
			}
		}
		var run = CardPoolPatch.CurrentRun;
		if (run != null)
		{
			return PackRegistry.GetRegistration(LocalContext.GetMe(run)?.Character);
		}
		return LibraryRegistration;
	}

	/// <summary>The pack this card belongs to in the current display context.</summary>
	public static PackDefinition? PackOf(CardModel card) => PackRegistry.GetPackOf(card, For(card));
}

/// <summary>
/// Card visuals for pack cards:
///  - the pack name above the card (STS1 RenderBaseGameCardPackTopTextPatches),
///  - "Pack" as the type line of pack preview cards,
///  - optional unified frame (<see cref="PackmasterSettings.OneFrameMode"/>).
/// </summary>
[HarmonyPatch]
internal static class CardVisualPatch
{
	public const string PackNameLabelName = "PackmasterPackName";

	private static readonly AccessTools.FieldRef<NCard, MegaLabel> TypeLabel = AccessTools.FieldRefAccess<NCard, MegaLabel>("_typeLabel");

	[HarmonyPatch(typeof(NCard), "Reload")]
	[HarmonyPostfix]
	private static void ShowPackName(NCard __instance)
	{
		try
		{
			if (!__instance.IsNodeReady())
			{
				return; // _Ready calls Reload again once the card's nodes exist
			}
			var model = __instance.Model;
			var label = __instance.Body.GetNodeOrNull<MegaLabel>(PackNameLabelName);
			var pack = model == null || __instance.Visibility != ModelVisibility.Visible ? null : PackDisplayContext.PackOf(model);
			if (pack == null)
			{
				if (label != null)
				{
					label.Visible = false;
				}
				return;
			}
			if (label == null)
			{
				// STS1 draws the pack name on the frame's top edge in small light text, taking no extra space.
				label = new MegaLabel
				{
					Name = PackNameLabelName,
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center,
					MouseFilter = Control.MouseFilterEnum.Ignore,
					Position = new Vector2(-115, -218),
					Size = new Vector2(230, 26),
					MaxFontSize = 21,
					MinFontSize = 13,
				};
				label.AddThemeFontOverride("font", ResourceLoader.Load<Font>("res://themes/kreon_regular_shared.tres"));
				label.AddThemeFontSizeOverride("font_size", 21);
				label.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));
				label.AddThemeColorOverride("font_outline_color", new Color(0.08f, 0.07f, 0.06f, 0.9f));
				label.AddThemeConstantOverride("outline_size", 6);
				__instance.Body.AddChild(label);
			}
			label.SetTextAutoSize(PackRegistry.GetPackName(pack));
			label.Visible = true;
		}
		catch (Exception e)
		{
			Log.Error($"[PackmasterLib] ShowPackName failed: {e}");
		}
	}

	[HarmonyPatch(typeof(NCard), "UpdateTypePlaque")]
	[HarmonyPostfix]
	private static void PreviewTypeLine(NCard __instance)
	{
		if (PackRegistry.IsPreviewCard(__instance.Model))
		{
			TypeLabel(__instance).SetTextAutoSize(PackRegistry.ResolveLocKey("gameplay_ui:PACKMASTER_LIB.preview.type"));
		}
	}

	/// <summary>Pack previews have no cost (STS1 previews are unplayable cards without an orb).</summary>
	[HarmonyPatch(typeof(NCard), "UpdateEnergyCostVisuals")]
	[HarmonyPostfix]
	private static void PreviewHasNoCost(NCard __instance)
	{
		if (PackRegistry.IsPreviewCard(__instance.Model))
		{
			foreach (var field in new[] { "_energyIcon", "_energyLabel", "_unplayableEnergyIcon" })
			{
				if (AccessTools.Field(typeof(NCard), field).GetValue(__instance) is CanvasItem item)
				{
					item.Visible = false;
				}
			}
		}
	}

	[HarmonyPatch(typeof(CardModel), nameof(CardModel.VisualCardPool), MethodType.Getter)]
	[HarmonyPostfix]
	private static void OneFrame(CardModel __instance, ref CardPoolModel __result)
	{
		if (!PackmasterSettings.OneFrameMode)
		{
			return;
		}
		var registration = PackDisplayContext.For(__instance);
		if (registration != null && PackRegistry.GetPackOf(__instance, registration) != null
			&& PackRegistry.GetCharacter(registration) is { } character)
		{
			__result = character.CardPool;
		}
	}
}
