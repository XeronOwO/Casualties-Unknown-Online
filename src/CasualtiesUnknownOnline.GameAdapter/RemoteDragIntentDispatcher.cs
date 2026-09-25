using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Turns one closed release window into wire intents. The window already
/// decided what the native branch did; this class owns the wire shape, the
/// observability of every refused or unclassified gesture, and the send. A
/// release that produced neither an intent nor a refusal is the unclassified
/// case — a native gesture CUO has never seen must be visible, never a silent
/// no-op — while the classified native no-ops (R1, R7, R14) are not reported as
/// unknown gestures.
/// </summary>
internal sealed class RemoteDragIntentDispatcher(GameAdapterDomains domains)
{
	internal ulong LocalSteamId => domains.Session.LocalSteamId;

	internal void ReportUnresolved(Item? dragItem) =>
		domains.Log.LogWarning("[RemoteIntent] refused the release of {Item}: the display proxy carries no authoritative instance id/owner — the drag is cancelled before the native body can mutate it.",
			dragItem != null ? dragItem.id : "null"); // Unity object — ==

	internal void ReportGestureNotCarried(string gesture) =>
		domains.Log.LogInformation("[RemoteIntent] refused {Gesture} — that intent arrives in a later stage; no proxy was mutated.", gesture);

	internal void Emit(RemoteDragOutcome outcome)
	{
		foreach (var refusal in outcome.Refusals)
		{
			domains.Log.LogInformation("[RemoteIntent] refused {Refusal} — the native call was skipped, no proxy was mutated (item {Item}, owner {Owner}).",
				refusal, outcome.DraggedItemId, outcome.OwnerSteamId);
		}

		if (outcome.OwnerSteamId == 0 || outcome.DraggedItemId == 0)
		{
			// The refusals above are the only "why" this release has, so they are
			// logged before the window is discarded.
			domains.Log.LogWarning("[RemoteIntent] discarded a closed release window: item {Item} has no resolvable owner.",
				outcome.DraggedItemId);
			return;
		}

		if (outcome.IsUnclassified)
		{
			domains.Log.LogInformation("[RemoteIntent] the release of item {Item} (owner {Owner}) produced no intent — unclassified native gesture.",
				outcome.DraggedItemId, outcome.OwnerSteamId);
		}

		foreach (var intent in outcome.Intents)
		{
			var msg = new RemoteInventoryIntentMsg
			{
				Kind = intent.Kind,
				OwnerSteamId = outcome.OwnerSteamId,
				ItemInstanceId = intent.ItemInstanceId,
				TargetContainerInstanceId = intent.TargetContainerInstanceId,
				TargetSlotIndex = intent.TargetSlotIndex,
				TargetBodySteamId = intent.TargetBodySteamId,
				TargetLimbIndex = intent.TargetLimbIndex,
			};

			domains.Log.LogInformation("[RemoteIntent] {Kind} captured for item {Item} of {Owner} (container {Container}, slot {Slot}, body {Body}, limb {Limb}).",
				msg.Kind, msg.ItemInstanceId, msg.OwnerSteamId, msg.TargetContainerInstanceId, msg.TargetSlotIndex, msg.TargetBodySteamId, msg.TargetLimbIndex);
			domains.PlayerInteraction.SendRemoteInventoryIntent(msg);
		}
	}
}
