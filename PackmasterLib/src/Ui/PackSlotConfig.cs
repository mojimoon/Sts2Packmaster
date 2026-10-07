using Sts2Packmaster.Lib.Api;

namespace Sts2Packmaster.Lib.Ui;

/// <summary>The player's pack configuration for one character, edited in the character-select panel.</summary>
public sealed class PackSlotConfig
{
	public bool AllPacks;

	/// <summary>Slot tokens: "random" / "choice" / "none" / a pack id. Length 3-10.</summary>
	public List<string> Slots = new();

	public static PackSlotConfig FromDefault(PackCharacterRegistration registration)
	{
		return new PackSlotConfig
		{
			AllPacks = false,
			Slots = registration.DefaultSlots.ToList(),
		};
	}

	public void Normalize(int minSlots, int maxSlots)
	{
		while (Slots.Count < minSlots)
		{
			Slots.Add(PackSlotToken.Random);
		}
		while (Slots.Count > maxSlots)
		{
			Slots.RemoveAt(Slots.Count - 1);
		}
	}

	/// <summary>Grow (pad with random) or shrink the slot list to exactly <paramref name="count"/>.</summary>
	public void SetSlotCount(int count)
	{
		while (Slots.Count < count)
		{
			Slots.Add(PackSlotToken.Random);
		}
		while (Slots.Count > count)
		{
			Slots.RemoveAt(Slots.Count - 1);
		}
	}
}
