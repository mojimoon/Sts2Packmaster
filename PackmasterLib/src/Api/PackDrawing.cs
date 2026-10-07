using MegaCrit.Sts2.Core.Random;

namespace Sts2Packmaster.Lib.Api;

/// <summary>What a <see cref="IPackDrawer"/> may draw from.</summary>
public sealed class PackDrawContext
{
	public required PackCharacterRegistration Registration { get; init; }

	/// <summary>Packs that may be drawn (unlocked, not in the run yet, minus excluded offers).</summary>
	public required IReadOnlyList<PackDefinition> Candidates { get; init; }

	/// <summary>Packs already in the run's pool, in order.</summary>
	public required IReadOnlyList<PackDefinition> Selected { get; init; }

	/// <summary>0-based draft round for choice offers; -1 when drawing a random slot.</summary>
	public required int Round { get; init; }

	/// <summary>The rng to use. Use only this one: draws must be deterministic (save/load, multiplayer).</summary>
	public required Rng Rng { get; init; }
}

/// <summary>
/// Pack drawing algorithm. Set <see cref="PackCharacterRegistration.Drawer"/> to customize, e.g. "the
/// left pack of every offer is guaranteed by a pity counter". Return values are sanitized by the
/// library (unknown/duplicate packs are dropped and missing ones filled with the default drawer).
/// </summary>
public interface IPackDrawer
{
	/// <summary>One pack for a "random" slot.</summary>
	PackDefinition DrawRandomSlot(PackDrawContext context);

	/// <summary>
	/// <paramref name="count"/> distinct packs for a "choice of 3" offer, in display order (left to right).
	/// </summary>
	IReadOnlyList<PackDefinition> DrawChoiceOffer(PackDrawContext context, int count);
}

/// <summary>Default drawer: weighted random by <see cref="PackDefinition.Weight"/>, without replacement.</summary>
public class WeightedPackDrawer : IPackDrawer
{
	public static readonly WeightedPackDrawer Instance = new();

	public virtual PackDefinition DrawRandomSlot(PackDrawContext context) => Draw(context.Candidates, 1, context.Rng)[0];

	public virtual IReadOnlyList<PackDefinition> DrawChoiceOffer(PackDrawContext context, int count) => Draw(context.Candidates, count, context.Rng);

	/// <summary>Weighted draw of up to <paramref name="count"/> distinct packs (weight &lt;= 0 never drawn unless nothing else is left).</summary>
	public static List<PackDefinition> Draw(IReadOnlyList<PackDefinition> candidates, int count, Rng rng)
	{
		var pool = candidates.ToList();
		var result = new List<PackDefinition>();
		while (result.Count < count && pool.Count > 0)
		{
			var total = pool.Sum(p => Math.Max(0.0, p.Weight));
			PackDefinition pick;
			if (total <= 0)
			{
				pick = pool[rng.NextInt(pool.Count)];
			}
			else
			{
				var roll = rng.NextDouble() * total;
				pick = pool[^1];
				foreach (var p in pool)
				{
					roll -= Math.Max(0.0, p.Weight);
					if (roll < 0)
					{
						pick = p;
						break;
					}
				}
			}
			result.Add(pick);
			pool.Remove(pick);
		}
		return result;
	}
}
