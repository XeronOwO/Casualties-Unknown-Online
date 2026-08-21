using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Direct player-interaction apply side (partial of <see cref="GameAdapter"/>).
/// The host sends one authoritative <see cref="PlayerInventoryTransferMsg"/> to
/// each participant; this half mutates the LOCAL body (the only body this
/// process simulates) inside a RemoteApply scope — no local report echo — and
/// then triggers an immediate character-snapshot re-report so the host's save
/// and every peer's clone learn the real local slot in the same run.
/// </summary>
public sealed partial class GameAdapter
{
	private void OnPlayerInventoryTransfer(PlayerInventoryTransferMsg msg)
	{
		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body == null) // Unity object — ==
		{
			_log.LogWarning("[PlayerInteraction] transfer received but the local body is not ready — skipped (1 Hz snapshot will not apply this authoritative fact; a retry is required).");
			return;
		}

		var changed = false;
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			if (msg.FromSteamId == _session.LocalSteamId)
			{
				RemoveCarriedItemFromLocalBody(body, msg.Item?.InstanceId ?? 0);
				changed = true;
			}

			if (msg.ToSteamId == _session.LocalSteamId && msg.Item is { } item)
			{
				AddCarriedItemToLocalBody(body, item);
				changed = true;
			}
		}

		if (changed)
		{
			_characterDataSync.ReportInventoryChanged(body);
		}
	}

	/// <summary>
	/// Remove one carried item from the local body by instance id (slots, then
	/// worn limb parents). Container contents are intentionally NOT searched:
	/// the take slice transfers a top-level carried item only; taking a whole
	/// container carries its contents with the item. Destroy runs inside
	/// RemoteApply, so the generic destroy report stays silent.
	/// </summary>
	private static void RemoveCarriedItemFromLocalBody(Body body, ulong instanceId)
	{
		if (instanceId == 0)
		{
			return;
		}

		foreach (var slot in body.slots)
		{
			if (slot == null) // Unity object — ==
			{
				continue;
			}

			for (var c = slot.transform.childCount - 1; c >= 0; c--)
			{
				if (TryDestroyById(slot.transform.GetChild(c).GetComponent<Item>(), instanceId))
				{
					return;
				}
			}
		}

		foreach (var limb in body.limbs)
		{
			if (limb == null) // Unity object — ==
			{
				continue;
			}

			for (var c = limb.transform.childCount - 1; c >= 0; c--)
			{
				if (TryDestroyById(limb.transform.GetChild(c).GetComponent<Item>(), instanceId))
				{
					return;
				}
			}
		}
	}

	private static bool TryDestroyById(Item item, ulong instanceId)
	{
		if (item == null) // Unity object — ==
		{
			return false;
		}

		var id = item.GetComponent<ItemInstanceId>();
		if (id != null && id.Id == instanceId) // Unity object — ==
		{
			Object.Destroy(item.gameObject);
			return true;
		}

		return false;
	}

	/// <summary>
	/// Add one carried item to the local body. The host picks a concrete empty
	/// slot from the latest character snapshot, so that slot is preferred; if
	/// the live body disagrees (a stale snapshot or a slot the player filled in
	/// the meantime), the local body's own <see cref="Body.FirstEmptySlot"/> is
	/// the fallback and the immediate re-report carries the real slot back. If
	/// no slot is empty the item cannot be placed and the transfer is logged —
	/// this slice never silently drops it.
	/// </summary>
	private void AddCarriedItemToLocalBody(Body body, CharacterItemMsg item)
	{
		int slot;
		if (item.SlotIndex >= 0
			&& item.SlotIndex < body.slots.Length
			&& !body.HoldingItem(item.SlotIndex))
		{
			slot = item.SlotIndex;
		}
		else
		{
			var empty = body.FirstEmptySlot();
			if (empty is not { } fallback)
			{
				_log.LogWarning("[PlayerInteraction] cannot place {ItemId} (id {InstanceId}) — no empty inventory slot.", item.ItemId, item.InstanceId);
				return;
			}

			slot = fallback;
		}

		item.SlotIndex = slot;
		ItemStateCodec.RestoreItem(item, body);
		_log.LogInformation("[PlayerInteraction] placed {ItemId} (id {InstanceId}) in local slot {Slot}.", item.ItemId, item.InstanceId, slot);
	}
}
