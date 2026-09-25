using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// One closed drag bracket: the intents the native branch produced, the
/// native calls this stage could not name (each one skipped, never executed on a
/// proxy), the proxy they belong to, which kind of bracket it was, and — for a
/// release that produced no intent — which classified native no-op it was, so only
/// a gesture CUO has never seen is reported as unclassified.
/// </summary>
internal readonly record struct RemoteDragOutcome(
	IReadOnlyList<RemoteDragIntent> Intents,
	IReadOnlyList<string> Refusals,
	ulong DraggedItemId,
	ulong OwnerSteamId,
	RemoteDragNoOp NoOp,
	RemoteDragWindowKind Kind)
{
	/// <summary>True when the bracket produced neither an intent nor a refusal.</summary>
	internal bool ProducedNothing => Intents.Count == 0 && Refusals.Count == 0;

	/// <summary>
	/// True when a RELEASE produced nothing and is not one of the classified native
	/// no-ops — the case that must be logged as an unknown gesture. A while-dragging
	/// frame is never that case: most frames of a drag carry no tick at all.
	/// </summary>
	internal bool IsUnclassified =>
		Kind == RemoteDragWindowKind.Release && ProducedNothing && NoOp == RemoteDragNoOp.None;
}
