using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.Logging;

using CasualtiesUnknownOnline.GameAdapter.Items;

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

	internal void ReportUnresolved(Item? dragItem)
	{
		RemoteBackpackView.EndProxyDrag();
		domains.Log.LogWarning("[RemoteIntent] refused the release of {Item}: the display proxy carries no authoritative instance id/owner — the drag is cancelled before the native body can mutate it.",
			dragItem != null ? dragItem.id : "null"); // Unity object — ==
	}

	/// <summary>
	/// A release of this client's OWN item onto another player's displayed inventory. When it can be classified it
	/// becomes the give-direction intent — the mirror of the take direction, where the item's owner is the ACTOR
	/// and the destination body is the other player — and when it cannot, the release keeps its refusal and the line
	/// that names why, which is what the acceptance table reads. It answers whether the intent went out, because the
	/// release bracket keeps the drag-opened backpack only for a give that actually happened.
	///
	/// <para>
	/// Both branches exist because the native body must not run either way: the container branch would load THIS
	/// client's item into the other player's display proxy and leave it to the clone rebuild (the reported loss),
	/// and a body slot of the clone would have driven the native slot sequence against the LOCAL body with the
	/// clone's own slot index.
	/// </para>
	/// </summary>
	internal bool ReportLocalReleaseOntoProxy(Item? dragItem, ulong targetOwnerSteamId, string target, ulong targetItemInstanceId, int targetSlotIndex)
	{
		var local = domains.Session.LocalSteamId;

		// A view-only pack takes no hand-over either: "view only" means every operation,
		// including giving into it. The gesture is refused before the wire, with the same
		// red line the take direction gets.
		if (RemoteBackpackView.IsReadOnly)
		{
			domains.Log.LogInformation("[RemoteIntent] refused the give of this client's own item {Item} onto {Owner}'s backpack: host rule RemoteBackpackAccess=unconscious_or_dead makes it view only.",
				dragItem != null ? dragItem.id : "null", // Unity object — ==
				targetOwnerSteamId);
			domains.BackpackNotice.Show(domains.Localization.T("remote_backpack.gesture_refused"));
			return false;
		}

		// The giver's own identity comes from the item's OWN component, never from the display-proxy marker: this
		// branch exists precisely because the dragged item is not a proxy. An item with no identity cannot be named
		// on the wire, and 0 is this family's own "unbound, never operable" answer.
		var itemId = dragItem != null ? OperationTrace.IdOf(dragItem) : 0; // Unity object — ==
		if (itemId == 0 || targetOwnerSteamId == 0 || targetOwnerSteamId == local)
		{
			domains.Log.LogWarning("[RemoteIntent] refused the release of this client's own item {Item} onto {Target} (owner {Owner}): it cannot be named to that player — the drag is cancelled before the native body can act on the displayed inventory, and the item stays where it is.",
				dragItem != null ? dragItem.id : "null", // Unity object — ==
				target,
				targetOwnerSteamId);
			return false;
		}

		// The two destinations are mutually exclusive operands: a pointer on an item means "into that container",
		// and a pointer on a ring slot means "onto that body slot".
		var intent = new RemoteDragIntent
		{
			Kind = RemoteInventoryIntentKind.GiveToBody,
			ItemInstanceId = itemId,
			TargetBodySteamId = targetOwnerSteamId,
			TargetSlotIndex = targetSlotIndex,
			TargetContainerInstanceId = targetItemInstanceId,
		};

		Send(intent, local);
		return true;
	}

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

		// The double-Tab release closes the remote view BEFORE it releases, so the flag
		// captured at pickup is what survives to this point. Either way a view-only pack
		// produces no intent: the host would refuse it, and the actor is told here.
		var readOnly = RemoteBackpackView.ReadOnlyDrag || RemoteBackpackView.IsReadOnly;
		RemoteBackpackView.EndProxyDrag();
		if (readOnly)
		{
			domains.Log.LogInformation("[RemoteIntent] refused the release of item {Item} (owner {Owner}): the remote backpack is view only under host rule RemoteBackpackAccess=unconscious_or_dead, so no intent was sent.",
				outcome.DraggedItemId, outcome.OwnerSteamId);
			domains.BackpackNotice.Show(domains.Localization.T("remote_backpack.gesture_refused"));
			return;
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
		if (RemoteBackpackView.ReadOnlyDrag || RemoteBackpackView.IsReadOnly)
		{
			// A continuous tick arrives every frame; the notice's own repeat window keeps
			// this to one line, and no intent leaves the client.
			domains.BackpackNotice.Show(domains.Localization.T("remote_backpack.gesture_refused"));
			return;
		}

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

	private void Send(RemoteDragIntent intent, RemoteDragOutcome outcome) => Send(intent, outcome.OwnerSteamId);

	/// <summary>
	/// One intent on the wire, from its owner. The window carries the owner for the drag-classified intents, and
	/// the release seam passes its own for the give direction — where the item belongs to the ACTOR rather than to
	/// the player the gesture pointed at.
	/// </summary>
	private void Send(RemoteDragIntent intent, ulong ownerSteamId)
	{
		var msg = new RemoteInventoryIntentMsg
		{
			Kind = intent.Kind,
			OwnerSteamId = ownerSteamId,
			ItemInstanceId = intent.ItemInstanceId,
			TargetContainerInstanceId = intent.TargetContainerInstanceId,
			TargetSlotIndex = intent.TargetSlotIndex,
			TargetBodySteamId = intent.TargetBodySteamId,
			TargetLimbIndex = intent.TargetLimbIndex,
			Amount = intent.Amount,
			TargetItemInstanceId = intent.TargetItemInstanceId,
			TargetTraderPosition = intent.TargetTraderPosition,
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
			domains.Log.LogInformation("[RemoteIntent] {Kind} captured for item {Item} of {Owner} (container {Container}, slot {Slot}, body {Body}, limb {Limb}, target item {TargetItem}, trader {Trader}).",
				msg.Kind, msg.ItemInstanceId, msg.OwnerSteamId, msg.TargetContainerInstanceId, msg.TargetSlotIndex, msg.TargetBodySteamId, msg.TargetLimbIndex, msg.TargetItemInstanceId, msg.TargetTraderPosition is { } trader ? $"({trader.X}, {trader.Y})" : "none");
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
