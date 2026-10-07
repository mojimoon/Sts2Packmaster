using MegaCrit.Sts2.Core.Random;
using Sts2Packmaster.Lib.Api;

namespace Sts2Packmaster.Lib.Core;

/// <summary>Result of resolving a slot configuration at run start.</summary>
public sealed class PackResolution
{
	/// <summary>Packs granted immediately (fixed, then random), in resolution order.</summary>
	public readonly List<PackDefinition> Selected = new();

	/// <summary>Number of "choice of N" slots to draft on the setup screen.</summary>
	public int ChoiceSlots;

	/// <summary>Human-readable trace of every decision (for tests/logs).</summary>
	public readonly List<string> Trace = new();

	/// <summary>Slots that had to be skipped (unknown/locked fixed pack, empty pool).</summary>
	public readonly List<int> SkippedSlots = new();
}

/// <summary>Deterministic pack drawing shared by the run state, the setup screen and the tests.</summary>
public static class PackResolver
{
	public static IPackDrawer DrawerOf(PackCharacterRegistration registration) => registration.Drawer ?? WeightedPackDrawer.Instance;

	/// <summary>Resolve fixed and random slots; choice slots are only counted (offers are drawn per round).</summary>
	public static PackResolution Resolve(PackCharacterRegistration registration, IReadOnlyList<string> slotTokens, bool allMode, Rng rng)
	{
		var result = new PackResolution();
		var allPacks = registration.UnlockedPacks.ToList();
		if (allMode)
		{
			result.Selected.AddRange(allPacks);
			result.Trace.Add($"all-packs mode: selected {allPacks.Count} packs");
			return result;
		}
		if (!PackmasterSettings.AllowMultipleNone)
		{
			// STS1 rule: one "none" at most; extra ones roll a random pack instead.
			var firstNone = slotTokens.ToList().IndexOf(PackSlotToken.None);
			slotTokens = slotTokens.Select((t, i) => t == PackSlotToken.None && i != firstNone ? PackSlotToken.Random : t).ToList();
		}
		// Fixed slots first so random slots never take a pack a fixed slot names.
		var order = Enumerable.Range(0, slotTokens.Count)
			.OrderBy(i => PackSlotToken.KindOf(slotTokens[i], out _) == PackSlotKind.Fixed ? 0 : 1)
			.ToList();
		foreach (var i in order)
		{
			var kind = PackSlotToken.KindOf(slotTokens[i], out var fixedPackId);
			switch (kind)
			{
				case PackSlotKind.None:
					result.Trace.Add($"slot {i + 1}: none");
					break;
				case PackSlotKind.Fixed:
				{
					var pack = allPacks.FirstOrDefault(p => p.Id == fixedPackId);
					if (pack == null || result.Selected.Contains(pack))
					{
						result.Trace.Add($"slot {i + 1}: fixed '{fixedPackId}' unknown, locked or duplicate (skipped)");
						result.SkippedSlots.Add(i);
						break;
					}
					result.Selected.Add(pack);
					result.Trace.Add($"slot {i + 1}: fixed '{pack.Id}'");
					break;
				}
				case PackSlotKind.Random:
				{
					var candidates = allPacks.Where(p => !result.Selected.Contains(p)).ToList();
					if (candidates.Count == 0)
					{
						result.Trace.Add($"slot {i + 1}: random but pool empty (skipped)");
						result.SkippedSlots.Add(i);
						break;
					}
					var context = new PackDrawContext { Registration = registration, Candidates = candidates, Selected = result.Selected.ToList(), Round = -1, Rng = rng };
					var pack = DrawerOf(registration).DrawRandomSlot(context);
					if (!candidates.Contains(pack))
					{
						pack = WeightedPackDrawer.Instance.DrawRandomSlot(context);
					}
					result.Selected.Add(pack);
					result.Trace.Add($"slot {i + 1}: random -> '{pack.Id}'");
					break;
				}
				case PackSlotKind.ChoiceOf3:
					result.ChoiceSlots++;
					result.Trace.Add($"slot {i + 1}: choice");
					break;
			}
		}
		return result;
	}

	/// <summary>
	/// The offer of one draft round. Candidates are unlocked packs with a preview card that are not in the
	/// run yet. By default (STS1) every pack offered and not picked stays out; with
	/// <see cref="PackmasterSettings.ExcludeOnlyLastRound"/> only the previous round's leftovers do. Either way
	/// excluded packs fill in when there would otherwise be fewer than <see cref="PackCharacterRegistration.ChoiceSize"/>.
	/// Empty when no pack is left.
	/// </summary>
	public static List<PackDefinition> Offer(PackCharacterRegistration registration, IReadOnlyList<PackDefinition> selected,
		IReadOnlyCollection<PackDefinition> excluded, int round, Rng rng)
	{
		var size = Math.Max(1, registration.ChoiceSize);
		var eligible = registration.UnlockedPacks.Where(p => p.PreviewCardType != null && !selected.Contains(p)).ToList();
		var preferred = eligible.Where(p => !excluded.Contains(p)).ToList();
		var offer = DrawSanitized(registration, preferred, selected, round, rng, Math.Min(size, preferred.Count));
		if (offer.Count < size)
		{
			var rest = eligible.Where(p => !offer.Contains(p)).ToList();
			offer.AddRange(DrawSanitized(registration, rest, selected, round, rng, Math.Min(size - offer.Count, rest.Count)));
		}
		return offer;
	}

	private static List<PackDefinition> DrawSanitized(PackCharacterRegistration registration, List<PackDefinition> candidates,
		IReadOnlyList<PackDefinition> selected, int round, Rng rng, int count)
	{
		if (count <= 0)
		{
			return new List<PackDefinition>();
		}
		var context = new PackDrawContext { Registration = registration, Candidates = candidates, Selected = selected, Round = round, Rng = rng };
		var drawn = DrawerOf(registration).DrawChoiceOffer(context, count).Where(candidates.Contains).Distinct().Take(count).ToList();
		if (drawn.Count < count)
		{
			drawn.AddRange(WeightedPackDrawer.Draw(candidates.Where(p => !drawn.Contains(p)).ToList(), count - drawn.Count, rng));
		}
		return drawn;
	}
}
