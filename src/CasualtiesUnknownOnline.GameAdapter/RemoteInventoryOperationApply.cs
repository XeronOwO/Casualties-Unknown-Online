using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The owner-side apply half of native remote-backpack operations. The host has
/// validated the request and delivered one <see cref="RemoteInventoryApplyMsg"/>
/// to the player who owns the real body. This handler executes the exact native
/// body/item operation on that local body (never on a remote display proxy),
/// then triggers the immediate character re-report and, for actions the native
/// hooks do not already carry, sends the affected item's authoritative state to
/// the host so the kernel/fact tables follow the owner's scene.
/// </summary>
internal sealed class RemoteInventoryOperationApply(GameAdapterDomains domains)
{
	private readonly ILogger _log = domains.Log;

	public void Apply(RemoteInventoryApplyMsg msg)
	{
		if (!domains.Session.SessionActive || msg.OwnerSteamId != domains.Session.LocalSteamId)
		{
			return;
		}

		var body = domains.Run.LocalBody;
		if (body == null) // Unity object — ==
		{
			_log.LogWarning("[RemoteApply] native {Kind} skipped: local body is not ready.", msg.Kind);
			return;
		}

		var source = CarriedItemLocator.FindById(body, msg.ItemInstanceId);
		if (source == null) // Unity object — ==
		{
			_log.LogWarning("[RemoteApply] native {Kind} skipped: item {ItemId} not found on the local body.",
				msg.Kind, msg.ItemInstanceId);
			return;
		}

		var target = msg.TargetItemInstanceId != 0
			? CarriedItemLocator.FindById(body, msg.TargetItemInstanceId)
			: null;
		if (NeedsTarget(msg.Kind) && target == null) // Unity object — ==
		{
			_log.LogWarning("[RemoteApply] native {Kind} skipped: target item {TargetId} not found on the local body.",
				msg.Kind, msg.TargetItemInstanceId);
			return;
		}

		var reportStateOnly = false;
		switch (msg.Kind)
		{
			case RemoteInventoryOperationKind.Combine:
				body.CombineItems(target!, source);
				break;
			case RemoteInventoryOperationKind.Use:
				body.UseItem(source);
				break;
			case RemoteInventoryOperationKind.Wear:
				body.WearWearable(source);
				break;
			case RemoteInventoryOperationKind.BatteryLoad:
				target!.battery.LoadBattery(source);
				reportStateOnly = true;
				break;
			case RemoteInventoryOperationKind.BatteryUnload:
				target!.battery.UnloadBattery(false);
				reportStateOnly = true;
				break;
			case RemoteInventoryOperationKind.FavoriteToggle:
				source.favourited = !source.favourited;
				reportStateOnly = true;
				break;
			case RemoteInventoryOperationKind.MoveToSlot:
				ApplyMoveToSlot(body, source, msg.TargetSlotIndex);
				break;
			default:
				_log.LogWarning("[RemoteApply] native {Kind} is not handled by the owner apply side.", msg.Kind);
				return;
		}

		// The owner's own scene changed; the immediate character re-report makes
		// every clone (including the remote-backpack viewer) converge without
		// waiting for the next 1 Hz tick.
		domains.CharacterDataSync.ReportInventoryChanged(body);

		if (reportStateOnly)
		{
			ReportAffectedState(source);
			if (target != null) // Unity object — ==
			{
				ReportAffectedState(target);
			}
		}
	}

	private void ApplyMoveToSlot(Body body, Item source, int targetSlot)
	{
		if (targetSlot < 0 || targetSlot >= body.slots.Length)
		{
			_log.LogWarning("[RemoteApply] move-to-slot skipped: slot {Slot} is outside the local body.",
				targetSlot);
			return;
		}

		var sourceSlot = ItemStateCodec.SlotOf(source);
		Item? swappedTarget = null;
		if (sourceSlot >= 0 && sourceSlot < body.slots.Length && body.HoldingItem(sourceSlot))
		{
			var targetItem = body.GetItem(targetSlot);
			if (targetItem != null) // Unity object — ==
			{
				if (sourceSlot != targetSlot)
				{
					body.SwapSlots(sourceSlot, targetSlot);
				}

				return;
			}

			body.DropItem(source);
		}
		else
		{
			// A source inside a container has no direct slot/limb home. The
			// native drag path lets Body.PickUpItem unload it from the container
			// inside the guarded path; the owner-side apply does the same and
			// only handles the occupied-slot swap beforehand.
			var sourceContainer = source.transform.parent != null
				? source.transform.parent.GetComponent<Container>() // Unity object — ==
				: null;
			var targetItem = body.GetItem(targetSlot);
			if (targetItem != null)
			{
				// For a container-source slot move, the occupying slot item goes
				// into the source's old container (the native swap direction for
				// a container→slot move). If the container cannot accept it,
				// refuse without unloading the source so the item never becomes
				// an unsynchronized orphan.
				if (sourceContainer == null || targetItem.transform.parent == sourceContainer.transform)
				{
					_log.LogWarning("[RemoteApply] move-to-slot refused: target slot {Slot} is occupied and the container source cannot swap with the occupying item.",
						targetSlot);
					return;
				}

				sourceContainer.LoadItem(targetItem);
				if (targetItem.transform.parent != sourceContainer.transform) // Unity object — ==
				{
					_log.LogWarning("[RemoteApply] move-to-slot refused: target slot {Slot} is occupied and the container could not hold the occupying item.",
						targetSlot);
					return;
				}

				swappedTarget = targetItem;
			}

			// Do NOT unload the source here. Body.PickUpItem performs its own
			// container-unload inside the native guarded path: it only unloads
			// after the slot/hand/distance checks pass, so a refused move never
			// orphans the item out of its container.
		}

		body.PickUpItem(source, targetSlot, force: true);

		// If the swap's PickUpItem did not land (e.g. a source item that can
		// only be held in hands was aimed at a non-hand slot), restore the
		// occupying item to the slot instead of leaving it embedded in the
		// container while the source remains there too.
		if (swappedTarget != null && body.GetItem(targetSlot) != source) // Unity object — ==
		{
			_log.LogWarning("[RemoteApply] move-to-slot source did not land in slot {Slot}; restoring the swapped occupying item.",
				targetSlot);
			body.PickUpItem(swappedTarget, targetSlot, force: true);
		}
	}

	private void ReportAffectedState(Item item)
	{
		if (item == null) // Unity object — ==
		{
			return;
		}

		var id = item.GetComponent<ItemInstanceId>()?.Id ?? 0;
		if (id == 0)
		{
			return;
		}

		var capture = ItemStateCodec.CaptureItem(item, ItemStateCodec.SlotOf(item));
		if (domains.Session.Role == SessionRole.Host)
		{
			domains.Items.SendItemCarriedSync(domains.Session.LocalSteamId, capture);
		}
		else
		{
			domains.Items.SendItemUse(id, capture);
		}

		_log.LogInformation("[RemoteApply] reported native item state for {ItemId} (id {InstanceId}).",
			item.id, id);
	}

	private static bool NeedsTarget(RemoteInventoryOperationKind kind) =>
		kind is RemoteInventoryOperationKind.Combine
			or RemoteInventoryOperationKind.BatteryLoad
			or RemoteInventoryOperationKind.BatteryUnload;
}
