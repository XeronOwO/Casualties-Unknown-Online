using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The local apply side of direct player interaction: cross-player inventory
/// transfer, carry/release and heals. The host sends one authoritative message
/// to each participant; this class mutates the LOCAL body (the only body this
/// process simulates) inside a RemoteApply scope and triggers the immediate
/// re-report so every peer's clone learns the real result in the same run.
/// It also owns the carry-follow transform update and the Online UI heal-item
/// projection.
/// </summary>
internal sealed class PlayerInteractionApply(GameAdapterDomains domains)
{
	private PlayerCarryStateMsg? _pendingCarryState;

	public void OnPlayerInventoryTransfer(PlayerInventoryTransferMsg msg)
	{
		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body == null) // Unity object — ==
		{
			domains.Log.LogWarning("[PlayerInteraction] transfer received but the local body is not ready — skipped (1 Hz snapshot will not apply this authoritative fact; a retry is required).");
			return;
		}

		var changed = false;
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			if (msg.FromSteamId == domains.Session.LocalSteamId)
			{
				RemoveCarriedItemFromLocalBody(body, msg.Item?.InstanceId ?? 0);
				changed = true;
			}

			if (msg.ToSteamId == domains.Session.LocalSteamId && msg.Item is { } item)
			{
				AddCarriedItemToLocalBody(body, item);
				changed = true;
			}
		}

		if (changed)
		{
			domains.CharacterDataSync.ReportInventoryChanged(body);
		}
	}

	public void OnCarryStateChanged(PlayerCarryStateMsg msg)
	{
		_pendingCarryState = msg;
		var body = domains.Run.LocalBody; // Unity object — ==
		if (body == null)
		{
			return;
		}

		ApplyCarryStateToBody(body, msg);
		_pendingCarryState = null;
	}

	public void UpdateCarriedBody(Body? localBody)
	{
		if (localBody == null) // Unity object — ==
		{
			return;
		}

		if (_pendingCarryState is { } pending)
		{
			ApplyCarryStateToBody(localBody, pending);
			_pendingCarryState = null;
		}

		var driver = localBody.GetComponent<CarriedBodyDriver>();
		if (driver == null || driver.CarrierSteamId == 0)
		{
			return;
		}

		var carrier = domains.Entities.GetRemotePlayer(driver.CarrierSteamId);
		if (carrier is null)
		{
			return;
		}

		var side = carrier.IsRight ? -1f : 1f;
		var up = carrier.Crouching ? 0.5f : 0.9f;
		var offset = new Vector2(0.35f * side, up);
		localBody.transform.position = new Vector3(carrier.Position.X + offset.x, carrier.Position.Y + offset.y, 0f);
		localBody.rb.velocity = new Vector2(carrier.Velocity.X, carrier.Velocity.Y);
		localBody.isRight = carrier.IsRight;
		localBody.standing = false;
		localBody.moveDir = Vector2.zero;
	}

	public void OnPlayerHealReceived(PlayerHealResultMsg msg)
	{
		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body == null) // Unity object — ==
		{
			domains.Log.LogWarning("[PlayerInteraction] heal result received but the local body is not ready — skipped.");
			return;
		}

		var changed = false;
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			if (msg.HealerSteamId == domains.Session.LocalSteamId)
			{
				var item = FindCarriedItemById(body, msg.ItemInstanceId);
				if (item == null) // Unity object — ==
				{
					domains.Log.LogWarning("[Heal] local healer item {ItemId} not found — consumed state skipped.", msg.ItemInstanceId);
				}
				else
				{
					if (msg.ItemDestroyed)
					{
						Object.Destroy(item.gameObject);
						domains.Log.LogInformation("[Heal] local item {ItemId} destroyed by cross-player heal.", msg.ItemInstanceId);
					}
					else
					{
						item.condition = msg.ItemConditionAfter;
						domains.Log.LogInformation("[Heal] local item {ItemId} condition set to {Condition:F2} by cross-player heal.", msg.ItemInstanceId, msg.ItemConditionAfter);
					}

					changed = true;
				}
			}

			if (msg.TargetSteamId == domains.Session.LocalSteamId && msg.Health is { } health)
			{
				domains.CharacterDataSync.ApplyHealState(body, health, msg.Limbs);
				domains.Log.LogInformation("[Heal] local body healed by {Healer} (limb {Limb}).", msg.HealerSteamId, msg.HealedLimbIndex);
				changed = true;
			}
		}

		if (changed)
		{
			domains.CharacterDataSync.ReportInventoryChanged(body);
		}
	}

	public void OnPlayerItemUseReceived(PlayerItemUseResultMsg msg)
	{
		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body == null) // Unity object — ==
		{
			domains.Log.LogWarning("[PlayerInteraction] item-use result received but the local body is not ready — skipped.");
			return;
		}

		var changed = false;
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			if (msg.UserSteamId == domains.Session.LocalSteamId)
			{
				var item = FindCarriedItemById(body, msg.ItemInstanceId);
				if (item == null) // Unity object — ==
				{
					domains.Log.LogWarning("[ItemUse] local user item {ItemId} not found — consumed state skipped.", msg.ItemInstanceId);
				}
				else if (msg.ItemDestroyed)
				{
					Object.Destroy(item.gameObject);
					domains.Log.LogInformation("[ItemUse] local item {ItemId} destroyed by cross-player use.", msg.ItemInstanceId);
					changed = true;
				}
				else if (msg.ItemAfter is { } after)
				{
					item.condition = after.Condition;
					ItemStateCodec.RestoreLiquids(item, after.Liquids);
					ItemStateCodec.RestoreComponentStates(item, after.Components);
					domains.Log.LogInformation("[ItemUse] local item {ItemId} condition set to {Condition:F2} by cross-player use.", msg.ItemInstanceId, after.Condition);
					changed = true;
				}
			}

			if (msg.TargetSteamId == domains.Session.LocalSteamId)
			{
				if (msg.WornItem is { } worn)
				{
					domains.CharacterDataSync.RestoreWearable(worn, body);
					domains.Log.LogInformation("[ItemUse] local body wears {ItemId} from {User}.", worn.ItemId, msg.UserSteamId);
					changed = true;
				}
				else if (msg.Health is { } health)
				{
					domains.CharacterDataSync.ApplyHealState(body, health, msg.Limbs);
					TimedLimbEffectApply.Apply(body, msg.TimedEffects, domains.Log);
					domains.Log.LogInformation("[ItemUse] local body received a consumable from {User}.", msg.UserSteamId);
					changed = true;
				}
			}
		}

		if (changed)
		{
			domains.CharacterDataSync.ReportInventoryChanged(body);
		}
	}

	public bool HasLocalUseItem()
	{
		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body == null) // Unity object — ==
		{
			return false;
		}

		foreach (var slot in body.slots)
		{
			if (slot != null && HasLocalUseItemChild(slot.transform)) // Unity object — ==
			{
				return true;
			}
		}

		return false;
	}

	public IReadOnlyList<LocalUseItem> GetLocalUseItems()
	{
		var result = new List<LocalUseItem>();
		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body == null) // Unity object — ==
		{
			return result;
		}

		// Only inventory slots are requestable: the host's use finder skips
		// worn items (SlotIndex < 0), so a selector that lists worn items would
		// only produce refused requests.
		foreach (var slot in body.slots)
		{
			if (slot == null) // Unity object — ==
			{
				continue;
			}

			for (var c = 0; c < slot.transform.childCount; c++)
			{
				var item = slot.transform.GetChild(c).GetComponent<Item>();
				if (item == null || !IsLocalUseItem(item)) // Unity object — ==
				{
					continue;
				}

				var id = item.GetComponent<ItemInstanceId>();
				if (id != null && id.Id != 0) // Unity object — ==
				{
					result.Add(new LocalUseItem(id.Id, item.id));
				}
			}
		}

		return result;
	}

	public bool HasLocalHealItem()
	{
		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body == null) // Unity object — ==
		{
			return false;
		}

		foreach (var slot in body.slots)
		{
			if (slot != null && HasHealItemChild(slot.transform)) // Unity object — ==
			{
				return true;
			}
		}

		foreach (var limb in body.limbs)
		{
			if (limb != null && HasHealItemChild(limb.transform)) // Unity object — ==
			{
				return true;
			}
		}

		return false;
	}

	public IReadOnlyList<LocalHealItem> GetLocalHealItems()
	{
		var result = new List<LocalHealItem>();
		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body == null) // Unity object — ==
		{
			return result;
		}

		// Only inventory slots are requestable: the host's heal finder skips
		// worn items (SlotIndex < 0), so a selector that lists worn items would
		// only produce refused requests.
		foreach (var slot in body.slots)
		{
			if (slot == null) // Unity object — ==
			{
				continue;
			}

			for (var c = 0; c < slot.transform.childCount; c++)
			{
				var item = slot.transform.GetChild(c).GetComponent<Item>();
				if (item == null || !RemoteHealProfiles.IsHealItem(item.id)) // Unity object — ==
				{
					continue;
				}

				var id = item.GetComponent<ItemInstanceId>();
				if (id != null && id.Id != 0) // Unity object — ==
				{
					result.Add(new LocalHealItem(id.Id, item.id));
				}
			}
		}

		return result;
	}

	private void ApplyCarryStateToBody(Body body, PlayerCarryStateMsg msg)
	{
		var driver = body.GetComponent<CarriedBodyDriver>();
		if (msg.CarriedSteamId == domains.Session.LocalSteamId)
		{
			if (driver == null) // Unity object — ==
			{
				driver = body.gameObject.AddComponent<CarriedBodyDriver>();
			}

			driver.CarrierSteamId = msg.CarrierSteamId;
			body.standing = false;
			domains.Log.LogInformation("[Carry] local body is carried by {Carrier}.", msg.CarrierSteamId);
			return;
		}

		// A release for the relation this side was carrying (or a stale state
		// for a different carrier) clears the driver if this local body was the
		// carried half of that same relation.
		if (driver != null && driver.CarrierSteamId == msg.CarrierSteamId && msg.CarriedSteamId == 0)
		{
			Object.Destroy(driver);
			domains.Log.LogInformation("[Carry] local body released by {Carrier}.", msg.CarrierSteamId);
		}
	}

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
				domains.Log.LogWarning("[PlayerInteraction] cannot place {ItemId} (id {InstanceId}) — no empty inventory slot.", item.ItemId, item.InstanceId);
				return;
			}

			slot = fallback;
		}

		item.SlotIndex = slot;
		ItemStateCodec.RestoreItem(item, body);
		domains.Log.LogInformation("[PlayerInteraction] placed {ItemId} (id {InstanceId}) in local slot {Slot}.", item.ItemId, item.InstanceId, slot);
	}

	private static bool HasHealItemChild(Transform parent)
	{
		for (var c = 0; c < parent.childCount; c++)
		{
			var item = parent.GetChild(c).GetComponent<Item>();
			if (item != null && RemoteHealProfiles.IsHealItem(item.id)) // Unity object — ==
			{
				return true;
			}
		}

		return false;
	}

	private static bool HasLocalUseItemChild(Transform parent)
	{
		for (var c = 0; c < parent.childCount; c++)
		{
			var item = parent.GetChild(c).GetComponent<Item>();
			if (item != null && IsLocalUseItem(item)) // Unity object — ==
			{
				return true;
			}
		}

		return false;
	}

	private static bool IsLocalUseItem(Item item)
	{
		if (item == null || item.condition <= 0f) // Unity object — ==
		{
			return false;
		}

		if (RemoteWearCatalog.IsWearItem(item.id))
		{
			return true;
		}

		if (RemoteConsumeCatalog.IsFoodItem(item.id))
		{
			return true;
		}

		if (RemoteMedicineCatalog.IsInjectableItem(item.id))
		{
			var medicine = item.GetComponent<WaterContainerItem>();
			if (medicine == null || medicine.CurrentTotal <= 0f) // Unity object — ==
			{
				return false;
			}

			foreach (var liquid in medicine.stack)
			{
				if (!RemoteMedicineCatalog.IsSupportedMedicineLiquid(liquid.liquidId))
				{
					return false;
				}
			}

			return true;
		}

		if (RemoteTopicalCatalog.IsTopicalItem(item.id))
		{
			var topical = item.GetComponent<WaterContainerItem>();
			if (topical == null || topical.CurrentTotal <= 0f) // Unity object — ==
			{
				return false;
			}

			foreach (var liquid in topical.stack)
			{
				if (!RemoteTopicalCatalog.IsSupportedTopicalLiquid(liquid.liquidId))
				{
					return false;
				}
			}

			return true;
		}

		if (RemoteLimbToolCatalog.IsToolItem(item.id))
		{
			return true;
		}

		var water = item.GetComponent<WaterContainerItem>();
		if (water == null || water.CurrentTotal <= 0f) // Unity object — ==
		{
			return false;
		}

		foreach (var liquid in water.stack)
		{
			if (!RemoteConsumeCatalog.IsKnownLiquid(liquid.liquidId))
			{
				return false;
			}
		}

		return true;
	}

	private static Item? FindCarriedItemById(Body body, ulong instanceId)
	{
		foreach (var slot in body.slots)
		{
			if (slot != null) // Unity object — ==
			{
				for (var c = 0; c < slot.transform.childCount; c++)
				{
					if (TryGetCarriedById(slot.transform.GetChild(c).GetComponent<Item>(), instanceId, out var item))
					{
						return item;
					}
				}
			}
		}

		foreach (var limb in body.limbs)
		{
			if (limb != null) // Unity object — ==
			{
				for (var c = 0; c < limb.transform.childCount; c++)
				{
					if (TryGetCarriedById(limb.transform.GetChild(c).GetComponent<Item>(), instanceId, out var item))
					{
						return item;
					}
				}
			}
		}

		return null;
	}

	private static bool TryGetCarriedById(Item? item, ulong instanceId, out Item result)
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
