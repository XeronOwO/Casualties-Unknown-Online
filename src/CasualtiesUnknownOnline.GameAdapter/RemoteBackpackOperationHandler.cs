using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Remote-backpack native gesture routing. It maps display-proxy drag releases
/// to host-authoritative semantic requests and keeps the bridge under the
/// architecture line gate: the bridge only forwards the IPatchBridge calls,
/// this handler owns the proxy-identity extraction and request construction.
/// </summary>
internal sealed class RemoteBackpackOperationHandler(GameAdapterDomains domains)
{
	internal bool TryDrop(Item dragItem)
	{
		if (!TryGetRemoteProxyIdentity(dragItem, out var owner, out var itemId))
		{
			return false;
		}

		domains.PlayerInteraction.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
		{
			Kind = RemoteInventoryOperationKind.Drop,
			OwnerSteamId = owner,
			ItemInstanceId = itemId,
		});
		domains.Log.LogInformation("[BackpackView] requested drop of {ItemId} (id {InstanceId}) from {Owner}.",
			dragItem.id, itemId, owner);
		return true;
	}

	internal bool TryMoveToContainer(Item dragItem, Item targetContainer)
	{
		if (!TryGetRemoteProxyIdentity(dragItem, out var owner, out var itemId))
		{
			return false;
		}

		if (!TryGetRemoteProxyIdentity(targetContainer, out var containerOwner, out var targetId) || containerOwner != owner)
		{
			domains.Log.LogWarning("[BackpackView] refused container move: target {Target} is not a remote proxy of the same owner ({Owner}).",
				targetContainer?.id ?? "null", owner);
			return false;
		}

		domains.PlayerInteraction.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
		{
			Kind = RemoteInventoryOperationKind.MoveToContainer,
			OwnerSteamId = owner,
			ItemInstanceId = itemId,
			TargetContainerInstanceId = targetId,
		});
		domains.Log.LogInformation("[BackpackView] requested move of {ItemId} (id {InstanceId}) into container {Target} for {Owner}.",
			dragItem.id, itemId, targetId, owner);
		return true;
	}

	internal bool TryPour(Item dragItem)
	{
		if (!TryGetRemoteProxyIdentity(dragItem, out var owner, out var itemId))
		{
			return false;
		}

		domains.PlayerInteraction.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
		{
			Kind = RemoteInventoryOperationKind.Pour,
			OwnerSteamId = owner,
			ItemInstanceId = itemId,
		});
		domains.Log.LogInformation("[BackpackView] requested pour of {ItemId} (id {InstanceId}) from {Owner}.",
			dragItem.id, itemId, owner);
		return true;
	}

	internal bool TryCombine(Item dragItem, Item target)
	{
		if (!TryGetRemoteProxyIdentity(dragItem, out var owner, out var itemId)
			|| !TryGetRemoteProxyIdentity(target, out var targetOwner, out var targetId)
			|| targetOwner != owner
			|| targetId == 0
			|| targetId == itemId)
		{
			domains.Log.LogWarning("[BackpackView] refused combine: target {Target} is not a valid remote proxy of the same owner ({Owner}).",
				target?.id ?? "null", owner);
			return false;
		}

		domains.PlayerInteraction.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
		{
			Kind = RemoteInventoryOperationKind.Combine,
			OwnerSteamId = owner,
			ItemInstanceId = itemId,
			TargetItemInstanceId = targetId,
		});
		domains.Log.LogInformation("[BackpackView] requested combine of {ItemId} (id {InstanceId}) with {Target} for {Owner}.",
			dragItem.id, itemId, targetId, owner);
		return true;
	}

	internal bool TryUse(Item dragItem)
	{
		if (!TryGetRemoteProxyIdentity(dragItem, out var owner, out var itemId))
		{
			return false;
		}

		domains.PlayerInteraction.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
		{
			Kind = RemoteInventoryOperationKind.Use,
			OwnerSteamId = owner,
			ItemInstanceId = itemId,
		});
		domains.Log.LogInformation("[BackpackView] requested use of {ItemId} (id {InstanceId}) for {Owner}.",
			dragItem.id, itemId, owner);
		return true;
	}

	internal bool TryWear(Item dragItem)
	{
		if (!TryGetRemoteProxyIdentity(dragItem, out var owner, out var itemId))
		{
			return false;
		}

		domains.PlayerInteraction.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
		{
			Kind = RemoteInventoryOperationKind.Wear,
			OwnerSteamId = owner,
			ItemInstanceId = itemId,
		});
		domains.Log.LogInformation("[BackpackView] requested wear of {ItemId} (id {InstanceId}) for {Owner}.",
			dragItem.id, itemId, owner);
		return true;
	}

	internal bool TryLoadBattery(Item dragItem, Item target)
	{
		if (!TryGetSameOwnerPair(dragItem, target, out var owner, out var itemId, out var targetId))
		{
			return false;
		}

		domains.PlayerInteraction.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
		{
			Kind = RemoteInventoryOperationKind.BatteryLoad,
			OwnerSteamId = owner,
			ItemInstanceId = itemId,
			TargetItemInstanceId = targetId,
		});
		domains.Log.LogInformation("[BackpackView] requested battery load of {ItemId} (id {InstanceId}) into {Target} for {Owner}.",
			dragItem.id, itemId, targetId, owner);
		return true;
	}

	internal bool TryUnloadBattery(Item dragItem, Item target)
	{
		if (!TryGetSameOwnerPair(dragItem, target, out var owner, out var itemId, out var targetId))
		{
			return false;
		}

		domains.PlayerInteraction.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
		{
			Kind = RemoteInventoryOperationKind.BatteryUnload,
			OwnerSteamId = owner,
			ItemInstanceId = itemId,
			TargetItemInstanceId = targetId,
		});
		domains.Log.LogInformation("[BackpackView] requested battery unload of {ItemId} (id {InstanceId}) for {Owner}.",
			dragItem.id, itemId, owner);
		return true;
	}

	internal bool TryToggleFavorite(Item item)
	{
		if (!TryGetRemoteProxyIdentity(item, out var owner, out var itemId) || itemId == 0)
		{
			return false;
		}

		domains.PlayerInteraction.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
		{
			Kind = RemoteInventoryOperationKind.FavoriteToggle,
			OwnerSteamId = owner,
			ItemInstanceId = itemId,
		});
		domains.Log.LogInformation("[BackpackView] requested favourite toggle of {ItemId} (id {InstanceId}) for {Owner}.",
			item.id, itemId, owner);
		return true;
	}

	internal bool TryMoveToSlot(Item dragItem, int targetSlot)
	{
		if (targetSlot < 0 || !TryGetRemoteProxyIdentity(dragItem, out var owner, out var itemId))
		{
			return false;
		}

		domains.PlayerInteraction.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
		{
			Kind = RemoteInventoryOperationKind.MoveToSlot,
			OwnerSteamId = owner,
			ItemInstanceId = itemId,
			TargetSlotIndex = targetSlot,
		});
		domains.Log.LogInformation("[BackpackView] requested move of {ItemId} (id {InstanceId}) to slot {Slot} for {Owner}.",
			dragItem.id, itemId, targetSlot, owner);
		return true;
	}

	internal bool TryTransferToLocal(Item dragItem)
	{
		if (!TryGetRemoteProxyIdentity(dragItem, out var owner, out var itemId) || owner == 0)
		{
			return false;
		}

		domains.PlayerInteraction.SendTakeRequest(owner, itemId);
		domains.Log.LogInformation("[BackpackView] requested Tab-switch transfer of {ItemId} (id {InstanceId}) from {Owner} to the local backpack.",
			dragItem.id, itemId, owner);
		return true;
	}

	private static bool TryGetSameOwnerPair(
		Item dragItem,
		Item target,
		out ulong owner,
		out ulong itemId,
		out ulong targetId)
	{
		owner = 0;
		itemId = 0;
		targetId = 0;
		if (!TryGetRemoteProxyIdentity(dragItem, out owner, out itemId)
			|| !TryGetRemoteProxyIdentity(target, out var targetOwner, out targetId)
			|| targetOwner != owner
			|| targetId == 0
			|| targetId == itemId)
		{
			owner = 0;
			itemId = 0;
			targetId = 0;
			return false;
		}

		return true;
	}

	private static bool TryGetRemoteProxyIdentity(Item item, out ulong owner, out ulong itemId)
	{
		owner = 0;
		itemId = 0;
		if (item == null || item.GetComponent<RemoteCloneRender>() == null) // Unity objects — ==
		{
			return false;
		}

		var marker = item.GetComponent<RemoteInventoryItemId>();
		if (marker != null && marker.Id != 0) // Unity object — ==
		{
			owner = marker.OwnerSteamId;
			itemId = marker.Id;
			return true;
		}

		var legacy = item.GetComponent<ItemInstanceId>();
		if (legacy != null && legacy.Id != 0) // Unity object — ==
		{
			itemId = legacy.Id;
			return true;
		}

		return false;
	}
}
