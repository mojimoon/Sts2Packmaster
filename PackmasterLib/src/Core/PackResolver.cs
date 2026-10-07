using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Random;
using Sts2Packmaster.Lib.Api;

namespace Sts2Packmaster.Lib.Core;

/// <summary>Result of resolving a slot configuration. Also used by the test console command.</summary>
public sealed class PackResolution
{
	/// <summary>Packs granted immediately, in resolution order.</summary>
	public readonly List<PackDefinition> Selected = new();

	/// <summary>Choice-of-3 slots awaiting player input, in slot order.</summary>
	public readonly List<List<PackDefinition>> PendingChoices = new();

	/// <summary>Human-readable trace of every decision (for tests/logs).</summary>
	public readonly List<string> Trace = new();

	/// <summary>Slots that had to be skipped because no packs were left.</summary>
	public readonly List<int> SkippedSlots = new();
}

/// <summary>
/// Pure, deterministic slot-resolution logic shared by the run modifier and the test command.
/// </summary>
public static class PackResolver
{
	public static PackResolution Resolve(PackCharacterRegistration registration, IReadOnlyList<string> slotTokens, bool allMode, Rng rng)
	{
		var result = new PackResolution();
		var allPacks = registration.UnlockedPacks.ToList();
		if (!PackmasterSettings.AllowMultipleNone)
		{
			// STS1 rule: one "none" at most; extra ones roll a random pack instead.
			var firstNone = slotTokens.ToList().IndexOf(PackSlotToken.None);
			slotTokens = slotTokens.Select((t, i) => t == PackSlotToken.None && i != firstNone ? PackSlotToken.Random : t).ToList();
		}
		var pool = new List<PackDefinition>(allPacks);

		if (allMode)
		{
			result.Selected.AddRange(allPacks);
			result.Trace.Add($"all-packs mode: selected {allPacks.Count} packs");
			return result;
		}

		// Fixed/random slots first, choice slots last, so choice candidates never include a pack
		// a later slot takes.
		var order = Enumerable.Range(0, slotTokens.Count)
			.OrderBy(i => PackSlotToken.KindOf(slotTokens[i], out _) == PackSlotKind.ChoiceOf3 ? 1 : 0)
			.ToList();
		foreach (var i in order)
		{
			var token = slotTokens[i];
			var kind = PackSlotToken.KindOf(token, out var fixedPackId);
			switch (kind)
			{
				case PackSlotKind.None:
					result.Trace.Add($"slot {i + 1}: none");
					break;
				case PackSlotKind.Fixed:
				{
					var pack = allPacks.FirstOrDefault(p => p.Id == fixedPackId);
					if (pack == null)
					{
						result.Trace.Add($"slot {i + 1}: fixed '{fixedPackId}' not found or locked (skipped)");
						result.SkippedSlots.Add(i);
						break;
					}
					if (result.Selected.Contains(pack))
					{
						result.Trace.Add($"slot {i + 1}: fixed '{pack.Id}' already selected (skipped)");
						result.SkippedSlots.Add(i);
						break;
					}
					result.Selected.Add(pack);
					pool.Remove(pack);
					result.Trace.Add($"slot {i + 1}: fixed '{pack.Id}'");
					break;
				}
				case PackSlotKind.Random:
				{
					if (pool.Count == 0)
					{
						result.Trace.Add($"slot {i + 1}: random but pool empty (skipped)");
						result.SkippedSlots.Add(i);
						break;
					}
					var pack = pool[rng.NextInt(pool.Count)];
					result.Selected.Add(pack);
					pool.Remove(pack);
					result.Trace.Add($"slot {i + 1}: random -> '{pack.Id}'");
					break;
				}
				case PackSlotKind.ChoiceOf3:
				{
					if (pool.Count == 0)
					{
						result.Trace.Add($"slot {i + 1}: choice but pool empty (skipped)");
						result.SkippedSlots.Add(i);
						break;
					}
					// Only packs with a preview card can be offered.
					var offerable = pool.Where(p => p.PreviewCardType != null).ToList();
					if (offerable.Count == 0)
					{
						// No previews anywhere: fall back to a random pick so the slot still works.
						var fallback = pool[rng.NextInt(pool.Count)];
						result.Selected.Add(fallback);
						pool.Remove(fallback);
						result.Trace.Add($"slot {i + 1}: choice but no preview cards -> random '{fallback.Id}'");
						break;
					}
					var candidates = TakeRandom(offerable, Math.Min(3, offerable.Count), rng);
					result.PendingChoices.Add(candidates);
					result.Trace.Add($"slot {i + 1}: choice of {candidates.Count} [{string.Join(", ", candidates.Select(c => c.Id))}]");
					break;
				}
			}
		}
		return result;
	}

	/// <summary>
	/// Candidates for the next pending choice, minus packs selected since it was rolled (two choice
	/// slots may share candidates). Redraws from the unselected packs if none are left; null = skip the slot.
	/// </summary>
	public static List<PackDefinition>? NextCandidates(PackCharacterRegistration registration, List<PackDefinition> rolled, IReadOnlyCollection<PackDefinition> selected, Rng rng)
	{
		var candidates = rolled.Where(p => !selected.Contains(p)).ToList();
		if (candidates.Count > 0)
		{
			return candidates;
		}
		var rest = registration.UnlockedPacks.Where(p => p.PreviewCardType != null && !selected.Contains(p)).ToList();
		return rest.Count == 0 ? null : TakeRandom(rest, Math.Min(3, rest.Count), rng);
	}

	private static List<T> TakeRandom<T>(IReadOnlyList<T> source, int count, Rng rng)
	{
		var list = source.ToList();
		// Fisher-Yates using only the shared Rng surface so every peer converges.
		for (var i = list.Count - 1; i > 0; i--)
		{
			var j = rng.NextInt(i + 1);
			(list[i], list[j]) = (list[j], list[i]);
		}
		return list.Take(count).ToList();
	}
}
