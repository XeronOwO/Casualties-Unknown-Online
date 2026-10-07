using CasualtiesUnknownOnline.GameAdapter.Patches;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The topical chain's operator-side measurement, Part B of
/// <c>mod-cross-player-native-semantics</c>: the operator's own client runs the
/// item's OWN native <c>useLimbAction</c>, and the ml its delegate hands to
/// <c>WaterContainerItem.ApplyToLimb</c> becomes the dose the host commits.
/// <para>
/// No table can carry that amount: it is an <c>ldc.r4</c> literal inside each
/// item's delegate (<c>ApplyToLimb(limb, 10f)</c> for paincream, disinfectant and
/// spraybottle, <c>20f</c> for woundglue), so the native call itself is the only
/// runtime source. Unlike the syringe family the vanilla topical delegate is ONE
/// synchronous call — no minigame, no per-frame rate — so the capture lives only
/// for the length of that call instead of a whole session.
/// </para>
/// <para>
/// The diverted call is SWALLOWED: the host owns the drain and the patient's
/// client owns the effect, so the operator's own item is never drawn here and the
/// displayed body copy is never mutated. Every other application — the player's
/// own limb, an unrelated native flow — falls through to the untouched original.
/// </para>
/// <para>
/// The delegate must deliver synchronously. That holds for every item that can
/// reach this chain today: a topical container is a vanilla <c>LiquidItemInfo</c>
/// whose delegate is one <c>ApplyToLimb</c> call, and the mod API cannot author a
/// limb action at all yet (<c>ModItemDefinition</c> has no limb-use behaviour —
/// Part 3 A of the ceiling ticket), so no deferred delegate is reachable. A
/// delegate that applied nothing is refused by name rather than reported as a
/// zero dose.
/// </para>
/// </summary>
internal static class RemoteTopicalUseHandler
{
	private static Capture? _capture;

	/// <summary>
	/// Measure one topical use: run the item's own native limb action against
	/// <paramref name="limb"/> and read back the ml its delegate applied. False —
	/// with a named reason — when the item is not this chain's business, has no
	/// native limb action, has no container, or its delegate delivered nothing.
	/// <para>
	/// The family check is repeated HERE rather than left to the callers: a caller
	/// that skipped it would run an injectable carrier's limb action with the
	/// injection divert closed, which drains the operator's own item and applies
	/// the effect locally. The three call sites guard too, so this branch should
	/// never fire in production — reaching it means a caller lost its guard.
	/// </para>
	/// </summary>
	internal static bool TryMeasure(
		Item dragItem,
		Limb limb,
		ILimbUseSemantics semantics,
		ILogger log,
		out float doseMl)
	{
		doseMl = 0f;
		if (dragItem == null || limb == null) // Unity objects — ==
		{
			return false;
		}

		if (!LocalUseItemEligibility.IsTopicalRemoteItem(dragItem, semantics))
		{
			log.LogWarning("[ItemUse] refused topical measurement: {ItemId} is not a topical container this chain owns.", dragItem.id);
			return false;
		}

		var container = dragItem.GetComponent<WaterContainerItem>();
		if (container == null) // Unity object — ==
		{
			log.LogWarning("[ItemUse] refused topical use: {ItemId} has no WaterContainerItem to draw from.", dragItem.id);
			return false;
		}

		if (!Item.GlobalItems.TryGetValue(dragItem.id, out var info) || info?.useLimbAction is null)
		{
			log.LogWarning("[ItemUse] refused topical use: {ItemId} has no native limb action to run.", dragItem.id);
			return false;
		}

		// The window is saved and restored, not cleared: a nested measurement (none
		// is reachable today — the window contains only the item's own delegate)
		// would otherwise disarm its outer window and let that outer native call
		// drain the local item.
		var previous = _capture;
		var capture = new Capture(container, limb);
		_capture = capture;
		try
		{
			using (NativeLimbActionScope.Enter(limb.body))
			{
				info.useLimbAction(limb, dragItem);
			}
		}
		finally
		{
			_capture = previous;
		}

		if (capture.DoseMl <= 0f)
		{
			log.LogWarning("[ItemUse] refused topical use: {ItemId}'s own native limb action applied nothing to the treated limb.", dragItem.id);
			return false;
		}

		doseMl = capture.DoseMl;
		log.LogDebug("[ItemUse] measured {Dose:F3} ml from {ItemId}'s own native limb action.", doseMl, dragItem.id);
		return true;
	}

	/// <summary>
	/// One native topical call reached <c>WaterContainerItem.ApplyToLimb</c>
	/// while this client was measuring an item's own limb action. The ml is the
	/// dose the game itself computed, so it becomes the request's dose and the
	/// original call is swallowed: the host commits the drain, the patient's
	/// client applies the effect, and the display-only body copy is never
	/// mutated. Returns false for every application this measurement does not
	/// own.
	/// </summary>
	internal static bool TryDivertApplyToLimb(WaterContainerItem container, Limb limb, float amount)
	{
		var capture = _capture;
		if (capture is null || amount <= 0f || !capture.Owns(container, limb))
		{
			return false;
		}

		capture.DoseMl += amount;
		return true;
	}

	private sealed class Capture(WaterContainerItem container, Limb limb)
	{
		internal float DoseMl { get; set; }

		/// <summary>True when this native application is this measurement's own: the container the item's limb action was given, and the very limb it was given.</summary>
		internal bool Owns(WaterContainerItem other, Limb otherLimb) =>
			ReferenceEquals(container, other) && ReferenceEquals(limb, otherLimb);
	}
}
