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

		var source = FindCarriedItemById(body, msg.ItemInstanceId);
		if (source == null) // Unity object — ==
		{
			_log.LogWarning("[RemoteApply] native {Kind} skipped: item {ItemId} not found on the local body.",
				msg.Kind, msg.ItemInstanceId);
			return;
		}

		var target = msg.TargetItemInstanceId != 0
			? FindCarriedItemById(body, msg.TargetItemInstanceId)
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

		body.PickUpItem(source, targetSlot, force: true);
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

	private static Item? FindCarriedItemById(Body body, ulong instanceId)
	{
		if (instanceId == 0)
		{
			return null;
		}

		foreach (var slot in body.slots)
		{
			if (slot == null) // Unity object — ==
			{
				continue;
			}

			for (var c = 0; c < slot.transform.childCount; c++)
			{
				if (TryGetById(slot.transform.GetChild(c).GetComponent<Item>(), instanceId, out var item))
				{
					return item;
				}
			}
		}

		foreach (var limb in body.limbs)
		{
			if (limb == null) // Unity object — ==
			{
				continue;
			}

			for (var c = 0; c < limb.transform.childCount; c++)
			{
				if (TryGetById(limb.transform.GetChild(c).GetComponent<Item>(), instanceId, out var item))
				{
					return item;
				}
			}
		}

		return null;
	}

	private static bool TryGetById(Item? item, ulong instanceId, out Item result)
	{
		result = null!;
		if (item == null || instanceId == 0) // Unity object — ==
		{
			return false;
		}

		var id = item.GetComponent<ItemInstanceId>();
		if (id != null && id.Id == instanceId) // Unity object — ==
		{
			result = item;
			return true;
		}

		return false;
	}
}
