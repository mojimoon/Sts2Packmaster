namespace Sts2Packmaster.Lib.Api;

/// <summary>How a single pack slot is filled at the start of a run.</summary>
public enum PackSlotKind
{
	/// <summary>A random pack from the ones not already chosen.</summary>
	Random,
	/// <summary>The player picks 1 of 3 candidate packs at the start of the run (via the Neow option).</summary>
	ChoiceOf3,
	/// <summary>The slot stays empty (reduces the effective pack count).</summary>
	None,
	/// <summary>Exactly this pack (<see cref="FixedPackId"/>).</summary>
	Fixed
}
