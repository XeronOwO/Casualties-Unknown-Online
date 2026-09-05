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
			if (msg.FromSteamId == domains.Session.LocalSteamId
				&& msg.ToSteamId == domains.Session.LocalSteamId
				&& msg.TargetParentItemId != 0
				&& msg.Item is { } sameOwnerItem)
			{
				// Same-owner container move: re-home the existing real item instead
				// of Destroy + RestoreContent. The destroy+rebuild path leaves the
				// old object alive until the end of the frame, so the immediate
				// re-report captures two children with the same instance id and the
				// container weight/display doubles for one frame.
				if (TryMoveExistingItemToLocalContainer(body, sameOwnerItem, msg.TargetParentItemId))
				{
					changed = true;
				}
				else
				{
					RemoveCarriedItemFromLocalBody(body, sameOwnerItem.InstanceId);
					AddCarriedItemToLocalContainer(body, sameOwnerItem, msg.TargetParentItemId);
					changed = true;
				}
			}
			else
			{
				if (msg.FromSteamId == domains.Session.LocalSteamId)
				{
					RemoveCarriedItemFromLocalBody(body, msg.Item?.InstanceId ?? 0);
					changed = true;
				}

				if (msg.ToSteamId == domains.Session.LocalSteamId && msg.Item is { } item)
				{
					if (msg.TargetParentItemId != 0)
					{
						AddCarriedItemToLocalContainer(body, item, msg.TargetParentItemId);
					}
					else
					{
						AddCarriedItemToLocalBody(body, item);
					}

					changed = true;
				}
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

		// Use the remote carrier's RENDER clone as the anchor when it exists:
		// that clone is already smoothed by SessionStatePump, so the local rider
		// follows the same visual carrier position the player sees instead of
		// stepping to the raw 20 Hz buffer. The entity buffer is the fallback
		// before the clone exists (or if the carrier is not in the render table).
		if (domains.Renderer.TryGetRemoteBody(driver.CarrierSteamId, out var carrierBody)
			&& carrierBody != null) // Unity object — ==
		{
			CarriedBodyPlacement.ApplyRidePose(
				localBody,
				carrierBody.transform.position,
				carrierBody.isRight,
				carrierBody.crouching,
				carrierBody.rb.velocity,
				carrierBody.targetLookPos);
			domains.Run.RefreshLocalBodyState();
			return;
		}

		var carrier = domains.Entities.GetRemotePlayer(driver.CarrierSteamId);
		if (carrier is null)
		{
			return;
		}

		CarriedBodyPlacement.ApplyRidePose(
			localBody,
			new Vector3(carrier.Position.X, carrier.Position.Y, 0f),
			carrier.IsRight,
			carrier.Crouching,
			new Vector2(carrier.Velocity.X, carrier.Velocity.Y),
			new Vector2(carrier.LookPos.X, carrier.LookPos.Y));
		domains.Run.RefreshLocalBodyState();
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
				var item = CarriedItemLocator.FindById(body, msg.ItemInstanceId);
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
				var item = CarriedItemLocator.FindById(body, msg.ItemInstanceId);
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
					TimedBodyEffectApply.Apply(body, msg.TimedBodyEffects, domains.Log);
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

	public bool HasLocalHealItem()
	{
		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body == null) // Unity object — ==
		{
			return false;
		}

		// Recursive full-subtree scan: a heal item inside a carried container is
		// just as requestable as one in a direct slot, and the rest of this file
		// now resolves carried items recursively.
		foreach (var item in body.GetComponentsInChildren<Item>(true))
		{
			if (item == null || item.GetComponentInParent<RemoteCloneRender>() != null) // Unity objects — ==
			{
				continue;
			}

			if (RemoteHealProfiles.IsHealItem(item.id))
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

		// Recursive full-subtree scan (same reason as HasLocalHealItem): the
		// selector must not miss heal items inside a carried container.
		foreach (var item in body.GetComponentsInChildren<Item>(true))
		{
			if (item == null || item.GetComponentInParent<RemoteCloneRender>() != null) // Unity objects — ==
			{
				continue;
			}

			if (!RemoteHealProfiles.IsHealItem(item.id))
			{
				continue;
			}

			var id = item.GetComponent<ItemInstanceId>();
			if (id != null && id.Id != 0) // Unity object — ==
			{
				result.Add(new LocalHealItem(id.Id, item.id));
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
			var carrier = domains.Entities.GetRemotePlayer(msg.CarrierSteamId);
			var releasePosition = carrier is not null
				? new Vector3(carrier.Position.X, carrier.Position.Y, 0f)
				: (Vector3?)null;
			if (releasePosition is { } pos)
			{
				body.transform.position = pos;
			}

			// Empty the carrier id BEFORE the deferred Destroy: Unity keeps the
			// component until the end of the frame, and the render-proxy patches
			// decide by active-carrier state. Without this, one more proxy frame
			// can run after RestoreLocalBody and re-freeze the body/limbs.
			driver.CarrierSteamId = 0;
			CarriedBodyPlacement.RestoreLocalBody(body);
			Object.Destroy(driver);
			domains.Log.LogInformation("[Carry] local body released by {Carrier}; physics restored.", msg.CarrierSteamId);
		}
	}

	private static void RemoveCarriedItemFromLocalBody(Body body, ulong instanceId)
	{
		if (instanceId == 0)
		{
			return;
		}

		// A carried item may live at any depth: a top-level slot, a worn limb,
		// or inside a carried container. The authoritative transfer can lift an
		// item out of a nested container, so the local body removal must search
		// the whole carried-item subtree, not just direct slot/limb children.
		foreach (var item in body.GetComponentsInChildren<Item>(true))
		{
			if (TryDestroyById(item, instanceId))
			{
				return;
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

	private bool TryMoveExistingItemToLocalContainer(Body body, CharacterItemMsg item, ulong parentItemId)
	{
		var source = CarriedItemLocator.FindById(body, item.InstanceId);
		var parent = CarriedItemLocator.FindById(body, parentItemId);
		if (source == null || parent == null) // Unity objects — ==
		{
			return false;
		}

		var container = parent.GetComponent<Container>();
		if (container == null) // Unity object — ==
		{
			return false;
		}

		var oldContainer = source.transform.parent != null
			? source.transform.parent.GetComponent<Container>() // Unity object — ==
			: null;
		if (oldContainer != null) // Unity object — ==
		{
			oldContainer.UnloadItem(source);
		}

		container.LoadItem(source);
		if (source.transform.parent != container.transform) // Unity object — ==
		{
			domains.Log.LogWarning("[PlayerInteraction] same-owner container move failed: {ItemId} (id {InstanceId}) did not land in {Parent} ({ParentId}).",
				item.ItemId, item.InstanceId, parent.id, parentItemId);
			return false;
		}

		domains.Log.LogInformation("[PlayerInteraction] re-homed existing {ItemId} (id {InstanceId}) into container {Parent} ({ParentId}).",
			item.ItemId, item.InstanceId, parent.id, parentItemId);
		return true;
	}

	private void AddCarriedItemToLocalContainer(Body body, CharacterItemMsg item, ulong parentItemId)
	{
		var parent = CarriedItemLocator.FindById(body, parentItemId);
		if (parent == null) // Unity object — ==
		{
			domains.Log.LogWarning("[PlayerInteraction] cannot place {ItemId} (id {InstanceId}) into container {Parent} — parent not found on the local body; falling back to a slot.",
				item.ItemId, item.InstanceId, parentItemId);
			AddCarriedItemToLocalBody(body, item);
			return;
		}

		var container = parent.GetComponent<Container>();
		if (container == null) // Unity object — ==
		{
			domains.Log.LogWarning("[PlayerInteraction] cannot place {ItemId} (id {InstanceId}) into {Parent} — target is not a container; falling back to a slot.",
				item.ItemId, item.InstanceId, parentItemId);
			AddCarriedItemToLocalBody(body, item);
			return;
		}

		ItemStateCodec.RestoreContent(parent, container, item);
		domains.Log.LogInformation("[PlayerInteraction] placed {ItemId} (id {InstanceId}) into container {Parent} ({ParentId}).",
			item.ItemId, item.InstanceId, parent.id, parentItemId);
	}

}
