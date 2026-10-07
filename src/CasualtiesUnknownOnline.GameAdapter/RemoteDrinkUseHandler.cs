using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The drink chain's operator-side measurement, Part B of
/// <c>mod-cross-player-native-semantics</c>: the operator's own client runs the
/// item's OWN native <c>ItemInfo.useAction</c>, and the ml its delegate hands to
/// <c>WaterContainerItem.Drink</c> becomes the dose the host commits.
/// <para>
/// No table can carry that amount: it is an <c>ldc.r4</c> literal inside each
/// item's delegate (<c>Drink(body, 100f, "drink")</c> for a water bottle,
/// <c>20f</c> for naltrexone, <c>5f</c> for sleeping pills), so the native call
/// itself is the only runtime source. Like the topical family — and unlike the
/// syringe family, whose delegate is a per-frame minigame — the vanilla delegate
/// is ONE synchronous call, so the capture lives only for the length of that
/// call.
/// </para>
/// <para>
/// The delegate runs against the AFFECTED body, not the operator's own: an item's
/// use action may READ the body it is handed (mindwipe's item-level
/// <c>totalHappiness</c>/<c>brainHealth</c>/<c>strokeAmount</c> gate is the
/// vanilla instance), and only the drinking player's own picture may answer that.
/// The diverted call is SWALLOWED, so nothing is applied to that body here — the
/// host owns the drain and the patient's own client owns the effect.
/// </para>
/// <para>
/// The delegate must deliver synchronously, and it must be a drink. That holds
/// for every vanilla item the game can use as a drink; the item class alone
/// cannot prove it (a liquid centrifuge is a usable <c>LiquidItemInfo</c> too),
/// so a delegate that reaches no <c>Drink</c> call is refused by name — with its
/// own action having already run on this client, which the ticket records as the
/// family's limit rather than hides.
/// </para>
/// </summary>
internal static class RemoteDrinkUseHandler
{
	private static Capture? _capture;

	/// <summary>
	/// Measure one drink: run the item's own native use action against
	/// <paramref name="drinker"/> and read back the ml its delegate drank. False —
	/// with a named reason — when the item is not this chain's business, has no
	/// container or no native use action, or its delegate delivered nothing.
	/// </summary>
	internal static bool TryMeasure(
		Item dragItem,
		Body drinker,
		IConsumeSemantics semantics,
		ILogger log,
		out float doseMl)
	{
		doseMl = 0f;
		if (dragItem == null || drinker == null) // Unity objects — ==
		{
			return false;
		}

		if (!LocalUseItemEligibility.IsDrinkRemoteItem(dragItem, semantics))
		{
			log.LogWarning("[ItemUse] refused drink measurement: {ItemId} is not a drink container this chain owns.", dragItem.id);
			return false;
		}

		var container = dragItem.GetComponent<WaterContainerItem>();
		if (container == null) // Unity object — ==
		{
			log.LogWarning("[ItemUse] refused drink use: {ItemId} has no WaterContainerItem to drink from.", dragItem.id);
			return false;
		}

		if (!Item.GlobalItems.TryGetValue(dragItem.id, out var info) || info?.useAction is null)
		{
			log.LogWarning("[ItemUse] refused drink use: {ItemId} has no native use action to run.", dragItem.id);
			return false;
		}

		// The window is saved and restored, not cleared: a nested measurement (none
		// is reachable today — the window contains only the item's own delegate)
		// would otherwise disarm its outer window and let that outer native call
		// drain the local item.
		var previous = _capture;
		var capture = new Capture(container);
		_capture = capture;
		try
		{
			info.useAction(drinker, dragItem);
		}
		finally
		{
			_capture = previous;
		}

		if (capture.DoseMl <= 0f)
		{
			log.LogWarning("[ItemUse] refused drink use: {ItemId}'s own native use action reached no water-container drink — its own action has already run on this client.", dragItem.id);
			return false;
		}

		doseMl = capture.DoseMl;
		log.LogDebug("[ItemUse] measured {Dose:F3} ml from {ItemId}'s own native use action.", doseMl, dragItem.id);
		return true;
	}

	/// <summary>
	/// One native drink reached <c>WaterContainerItem.Drink</c> while this client
	/// was measuring an item's own use action. The ml is the dose the game itself
	/// computed, so it becomes the request's dose and the original call is
	/// swallowed: the host commits the drain, the patient's client runs each
	/// liquid's own <c>onDrink</c>, and the operator's own item is never drawn.
	/// Returns false for every application this measurement does not own.
	/// </summary>
	internal static bool TryDivertDrink(WaterContainerItem container, float amount)
	{
		var capture = _capture;
		if (capture is null || amount <= 0f || !capture.Owns(container))
		{
			return false;
		}

		capture.DoseMl += amount;
		return true;
	}

	private sealed class Capture(WaterContainerItem container)
	{
		internal float DoseMl { get; set; }

		/// <summary>True when this native drink is this measurement's own: the very container the item's use action was given.</summary>
		internal bool Owns(WaterContainerItem other) => ReferenceEquals(container, other);
	}
}
