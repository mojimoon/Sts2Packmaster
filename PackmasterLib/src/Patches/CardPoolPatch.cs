using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using Sts2Packmaster.Lib.Core;

namespace Sts2Packmaster.Lib.Patches;

/// <summary>
/// The pack pool. Every vanilla card source of "your class's cards" (rewards, shops, events, potions,
/// random-card effects, Dusty Tome...) goes through <see cref="CardPoolModel.GetUnlockedCards"/> of the
/// character's pool. For pack characters it returns the extras (starters, ancients) plus the cards of
/// the run's selected packs — the STS2 equivalent of STS1 Packmaster rebuilding the card pools.
/// Outside a run (or for a player without pack state) it returns every unlocked pack's cards.
/// </summary>
[HarmonyPatch]
internal static class CardPoolPatch
{
	private static readonly Func<RunManager, RunState?> CurrentState =
		AccessTools.MethodDelegate<Func<RunManager, RunState?>>(AccessTools.PropertyGetter(typeof(RunManager), "State"));

	/// <summary>The run in progress (null on the main menu).</summary>
	public static RunState? CurrentRun => CurrentState(RunManager.Instance);

	[HarmonyPatch(typeof(CardPoolModel), nameof(CardPoolModel.GetUnlockedCards))]
	[HarmonyPostfix]
	private static void UsePackPool(CardPoolModel __instance, UnlockState unlockState, CardMultiplayerConstraint multiplayerConstraint, ref IEnumerable<CardModel> __result)
	{
		try
		{
			var registration = PackRegistry.GetRegistrationForPool(__instance);
			if (registration == null)
			{
				return;
			}
			var player = CurrentRun?.Players.FirstOrDefault(p =>
				ReferenceEquals(p.UnlockState, unlockState) && ReferenceEquals(p.Character.CardPool, __instance));
			var state = PackState.Get(player);
			var packCards = state != null
				? state.SelectedCards()
				: registration.UnlockedPacks.SelectMany(PackRegistry.GetPackCards);
			// Own non-pack cards that survived the pool's own epoch filter (starters etc.), extras, pack cards.
			var cards = __result
				.Where(c => !PackRegistry.IsPreviewCard(c) && PackRegistry.GetPackOf(c, registration) == null)
				.Concat(PackRegistry.GetExtraCards(registration))
				.Concat(packCards)
				.Distinct()
				.ToList();
			switch (multiplayerConstraint)
			{
				case CardMultiplayerConstraint.MultiplayerOnly:
					cards.RemoveAll(c => c.MultiplayerConstraint == CardMultiplayerConstraint.SingleplayerOnly);
					break;
				case CardMultiplayerConstraint.SingleplayerOnly:
					cards.RemoveAll(c => c.MultiplayerConstraint == CardMultiplayerConstraint.MultiplayerOnly);
					break;
			}
			__result = cards;
		}
		catch (Exception e)
		{
			Log.Error($"[PackmasterLib] CardPoolPatch failed: {e}");
		}
	}

	/// <summary>
	/// Transforms draw from <c>original.Pool</c>; a vanilla pack card's pool is its original
	/// character's, so for pack characters transform within the run's pack pool instead (STS1 behavior).
	/// </summary>
	[HarmonyPatch(typeof(CardFactory), nameof(CardFactory.GetDefaultTransformationOptions))]
	[HarmonyPostfix]
	private static void TransformWithinPacks(CardModel original, bool isInCombat, ref IEnumerable<CardModel> __result)
	{
		try
		{
			var owner = original.IsMutable ? original.Owner : null;
			if (owner == null || PackState.Get(owner) == null)
			{
				return;
			}
			// Same branch as vanilla: quest/event/ancient/token cards transform into colorless cards, and
			// (like STS1) colorless cards stay colorless; everything else transforms within the packs.
			var usesOwnPool = original.Type != CardType.Quest && original.Rarity is not (CardRarity.Event or CardRarity.Ancient or CardRarity.Token);
			if (!usesOwnPool || original.Pool.IsColorless)
			{
				return;
			}
			var options = owner.Character.CardPool.GetUnlockedCards(owner.UnlockState, original.RunState.CardMultiplayerConstraint);
			__result = (IEnumerable<CardModel>)AccessTools.Method(typeof(CardFactory), "GetFilteredTransformationOptions")
				.Invoke(null, new object[] { original, options, isInCombat })!;
		}
		catch (Exception e)
		{
			Log.Error($"[PackmasterLib] TransformWithinPacks failed: {e}");
		}
	}
}

/// <summary>
/// Keeps the storage-only <see cref="PackRunModifier"/> invisible where a modifier changes behavior:
/// Neow shows only modifier options whenever RunState.Modifiers is non-empty (which removed Neow's
/// rewards), and the top bar draws an icon per modifier.
/// </summary>
[HarmonyPatch]
internal static class HiddenModifierPatch
{
	private static readonly Action<RunState, IReadOnlyList<ModifierModel>> SetModifiers =
		AccessTools.MethodDelegate<Action<RunState, IReadOnlyList<ModifierModel>>>(AccessTools.PropertySetter(typeof(RunState), nameof(RunState.Modifiers)));

	private static IReadOnlyList<ModifierModel>? Hide(IRunState? runState)
	{
		if (runState is not RunState state || !state.Modifiers.OfType<PackRunModifier>().Any())
		{
			return null;
		}
		var original = state.Modifiers;
		SetModifiers(state, original.Where(m => m is not PackRunModifier).ToList());
		return original;
	}

	private static void Restore(IRunState? runState, IReadOnlyList<ModifierModel>? original)
	{
		if (original != null && runState is RunState state)
		{
			SetModifiers(state, original);
		}
	}

	[HarmonyPatch(typeof(Neow), "GenerateInitialOptions")]
	[HarmonyPrefix]
	private static void NeowOptionsPrefix(Neow __instance, out IReadOnlyList<ModifierModel>? __state) => __state = Hide(__instance.Owner?.RunState);

	[HarmonyPatch(typeof(Neow), "GenerateInitialOptions")]
	[HarmonyFinalizer]
	private static void NeowOptionsFinalizer(Neow __instance, IReadOnlyList<ModifierModel>? __state) => Restore(__instance.Owner?.RunState, __state);

	[HarmonyPatch(typeof(Neow), nameof(Neow.InitialDescription), MethodType.Getter)]
	[HarmonyPrefix]
	private static void NeowTextPrefix(Neow __instance, out IReadOnlyList<ModifierModel>? __state) => __state = Hide(__instance.Owner?.RunState);

	[HarmonyPatch(typeof(Neow), nameof(Neow.InitialDescription), MethodType.Getter)]
	[HarmonyFinalizer]
	private static void NeowTextFinalizer(Neow __instance, IReadOnlyList<ModifierModel>? __state) => Restore(__instance.Owner?.RunState, __state);

	[HarmonyPatch(typeof(NTopBar), nameof(NTopBar.Initialize))]
	[HarmonyPrefix]
	private static void TopBarPrefix(IRunState runState, out IReadOnlyList<ModifierModel>? __state) => __state = Hide(runState);

	[HarmonyPatch(typeof(NTopBar), nameof(NTopBar.Initialize))]
	[HarmonyFinalizer]
	private static void TopBarFinalizer(IRunState runState, IReadOnlyList<ModifierModel>? __state) => Restore(runState, __state);
}
