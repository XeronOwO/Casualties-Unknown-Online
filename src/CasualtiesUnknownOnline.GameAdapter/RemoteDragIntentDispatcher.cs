using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Turns one closed drag bracket into wire intents. The window already decided
/// what the native branch did; this class owns the wire shape, the observability
/// of every refused or unclassified gesture, and the send. A release that produced
/// neither an intent nor a refusal is the unclassified case — a native gesture CUO
/// has never seen must be visible, never a silent no-op — while the classified
/// native no-ops (R1, R7, R14) are not reported as unknown gestures.
///
/// A while-dragging frame has no unclassified case: it is one frame of a
/// continuous gesture, and a frame the player was not draining on is simply a
/// frame. Its refusals are reported once per dragged item, because the same tick
/// repeats sixty times a second and a repeated refusal must not become sixty lines.
/// </summary>
internal sealed class RemoteDragIntentDispatcher(GameAdapterDomains domains)
{
	private readonly HashSet<string> _reportedFrameRefusals = [];
	private ulong _reportedFrameItemId;

	internal ulong LocalSteamId => domains.Session.LocalSteamId;

	internal void ReportUnresolved(Item? dragItem) =>
		domains.Log.LogWarning("[RemoteIntent] refused the release of {Item}: the display proxy carries no authoritative instance id/owner — the drag is cancelled before the native body can mutate it.",
			dragItem != null ? dragItem.id : "null"); // Unity object — ==

	internal void ReportGestureNotCarried(string gesture) =>
		domains.Log.LogInformation("[RemoteIntent] refused {Gesture} — that intent arrives in a later stage; no proxy was mutated.", gesture);

	/// <summary>
	/// A native mutation on a display proxy that no bracket took: the call is skipped
	/// and the proxy stays untouched. The caller rate-limits it per dragged proxy,
	/// because a continuous call would otherwise write one line per frame.
	/// </summary>
	internal void ReportProxyNotOperable(Item item, string what) =>
		domains.Log.LogInformation("[RemoteIntent] refused {What} on {Item}: that display proxy is not the item this client is operating (no authoritative identity, or no open bracket) — the native call was skipped, no proxy was mutated.",
			what, item.id);

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
			Send(intent, outcome);
		}
	}

	/// <summary>
	/// One closed while-dragging frame — a single frame of the native
	/// while-dragging body. The bracket only opens for a display proxy with a
	/// resolved identity, so there is nothing to discard and nothing to classify.
	/// </summary>
	internal void EmitWhileDraggingFrame(RemoteDragOutcome outcome)
	{
		ReportFrameRefusalsOnce(outcome);
		if (outcome.OwnerSteamId == 0 || outcome.DraggedItemId == 0)
		{
			return;
		}

		foreach (var intent in outcome.Intents)
		{
			Send(intent, outcome);
		}
	}

	private void Send(RemoteDragIntent intent, RemoteDragOutcome outcome)
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
			Amount = intent.Amount,
		};

		if (intent.Kind.IsContinuousGesture())
		{
			// The trigger frequency decides the level: this one runs every frame
			// while the gesture is held, so its happy path is a Debug line.
			domains.Log.LogDebug("[RemoteIntent] {Kind} captured for item {Item} of {Owner} (amount {Amount}).",
				msg.Kind, msg.ItemInstanceId, msg.OwnerSteamId, msg.Amount);
		}
		else
		{
			domains.Log.LogInformation("[RemoteIntent] {Kind} captured for item {Item} of {Owner} (container {Container}, slot {Slot}, body {Body}, limb {Limb}).",
				msg.Kind, msg.ItemInstanceId, msg.OwnerSteamId, msg.TargetContainerInstanceId, msg.TargetSlotIndex, msg.TargetBodySteamId, msg.TargetLimbIndex);
		}

		domains.PlayerInteraction.SendRemoteInventoryIntent(msg);
	}

	/// <summary>
	/// The while-dragging bracket runs every frame, so the same refusal would be
	/// written sixty times a second. Each distinct refusal is reported once per
	/// dragged proxy — the rate limit the release path gets for free from being a
	/// one-shot gesture.
	/// </summary>
	private void ReportFrameRefusalsOnce(RemoteDragOutcome outcome)
	{
		if (outcome.DraggedItemId != _reportedFrameItemId)
		{
			_reportedFrameItemId = outcome.DraggedItemId;
			_reportedFrameRefusals.Clear();
		}

		foreach (var refusal in outcome.Refusals)
		{
			if (!_reportedFrameRefusals.Add(refusal))
			{
				continue;
			}

			domains.Log.LogInformation("[RemoteIntent] refused {Refusal} — the native call was skipped, no proxy was mutated (item {Item}, owner {Owner}).",
				refusal, outcome.DraggedItemId, outcome.OwnerSteamId);
		}
	}
}
