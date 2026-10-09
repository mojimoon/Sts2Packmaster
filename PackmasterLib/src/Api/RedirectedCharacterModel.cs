using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace Sts2Packmaster.Lib.Api;

/// <summary>
/// Base class for pack characters without custom art: reuses another (vanilla) character's
/// visuals, icons, backgrounds and sfx. Non-virtual asset paths on CharacterModel are redirected
/// by <c>CharacterRedirectPatch</c>; virtual ones are overridden here.
/// </summary>
public abstract class RedirectedCharacterModel : CharacterModel
{
	/// <summary>The Id.Entry of the character whose assets to reuse, e.g. "ironclad".</summary>
	protected abstract string AssetSourceEntry { get; }

	/// <summary>Entry used by the redirect patch for non-virtual paths.</summary>
	public string AssetRedirectEntry => AssetSourceEntry;

	protected override string IconPath => SceneHelper.GetScenePath($"ui/character_icons/{AssetSourceEntry}_icon");

	protected override string CharacterSelectIconPath => ImageHelper.GetImagePath($"packed/character_select/char_select_{AssetSourceEntry}.png");

	protected override string CharacterSelectLockedIconPath => ImageHelper.GetImagePath($"packed/character_select/char_select_{AssetSourceEntry}_locked.png");

	protected override string MapMarkerPath => ImageHelper.GetImagePath($"packed/map/icons/map_marker_{AssetSourceEntry}.png");

	public override string CharacterSelectSfx => $"event:/sfx/characters/{AssetSourceEntry}/{AssetSourceEntry}_select";

	/// <summary>The character whose assets are reused (null if <see cref="AssetSourceEntry"/> matches none).</summary>
	public CharacterModel? AssetSource =>
		ModelDb.AllCharacters.FirstOrDefault(c => string.Equals(c.Id.Entry, AssetSourceEntry, StringComparison.OrdinalIgnoreCase));

	/// <summary>Attack VFX against the Architect: the source character's.</summary>
	public override List<string> GetArchitectAttackVfx() => AssetSource?.GetArchitectAttackVfx() ?? new List<string> { "vfx/vfx_attack_slash" };

	// Vanilla only ships a few wipe events (Defect/Regent/Necrobinder also use ironclad's).
	public override string CharacterTransitionSfx => "event:/sfx/ui/wipe_ironclad";
}
