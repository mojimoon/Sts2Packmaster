using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace Sts2Packmaster.Lib.Core;

/// <summary>
/// Hidden run modifier used purely as storage: it carries the pack configuration and pack state of
/// every pack character in the run through the game's modifier save channel (SerializableRun ->
/// SavedProperties, including multiplayer packets). It is invisible to Neow and the top bar
/// (<see cref="Sts2Packmaster.Lib.Patches.HiddenModifierPatch"/>), so Neow keeps its normal rewards;
/// pack selection happens on the separate pack setup screen.
/// </summary>
public class PackRunModifier : ModifierModel
{
	/// <summary>"playerId:charEntry:allFlag:slot,slot,...;..." — written at run creation.</summary>
	[SavedProperty]
	public string PackConfig { get; set; } = "";

	/// <summary>"playerId:charEntry:packId,packId;..." — the run's pack pool.</summary>
	[SavedProperty]
	public string PackSelected { get; set; } = "";

	/// <summary>"playerId:a|b|c/d|e|f;..." — choice slots not picked yet, with their candidates.</summary>
	[SavedProperty]
	public string PackPending { get; set; } = "";

	/// <summary>"playerId,playerId" — players whose pack setup screen is done.</summary>
	[SavedProperty]
	public string PackSetupDone { get; set; } = "";

	public override LocString Title => new LocString("gameplay_ui", "PACKMASTER_LIB.topbar.title");

	public override LocString Description => new LocString("gameplay_ui", "PACKMASTER_LIB.neow.desc");

	// Still listed in run history; reuse the vanilla Draft icon instead of the missing-icon fallback.
	protected override string IconPath => MegaCrit.Sts2.Core.Helpers.ImageHelper.GetImagePath("packed/modifiers/draft.png");

	protected override void AfterRunCreated(RunState runState)
	{
		if (!string.IsNullOrWhiteSpace(PackConfig))
		{
			PackState.InitializeRun(runState, PackConfig, runState.Rng.UpFront);
		}
	}

	protected override void AfterRunLoaded(RunState runState)
	{
		if (!string.IsNullOrWhiteSpace(PackConfig))
		{
			PackState.LoadRun(runState, this);
		}
	}
}
