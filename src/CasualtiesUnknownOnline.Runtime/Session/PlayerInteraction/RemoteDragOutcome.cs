using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// One closed release bracket: the intents the native branch produced, the
/// native calls this stage could not name (each one skipped, never executed on a
/// proxy), the proxy they belong to, and — when the release produced no intent —
/// which classified native no-op it was, so only a gesture CUO has never seen is
/// reported as unclassified.
/// </summary>
internal readonly record struct RemoteDragOutcome(
	IReadOnlyList<RemoteDragIntent> Intents,
	IReadOnlyList<string> Refusals,
	ulong DraggedItemId,
	ulong OwnerSteamId,
	RemoteDragNoOp NoOp)
{
	/// <summary>True when the release produced neither an intent nor a refusal.</summary>
	internal bool ProducedNothing => Intents.Count == 0 && Refusals.Count == 0;

	/// <summary>True when the release produced nothing and is not one of the classified native no-ops — the case that must be logged as an unknown gesture.</summary>
	internal bool IsUnclassified => ProducedNothing && NoOp == RemoteDragNoOp.None;
}
