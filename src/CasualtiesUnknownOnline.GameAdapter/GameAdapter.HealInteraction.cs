using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.Logging;
using UnityEngine;
using IGameAdapter = CasualtiesUnknownOnline.Runtime.GameAdapter.IGameAdapter;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Direct player-interaction heal apply side (partial of <see cref="GameAdapter"/>).
/// The host sends one authoritative <see cref="PlayerHealResultMsg"/> to each
/// participant; this half consumes the local healer's item and/or applies the
/// target's post-heal body/limb state inside a RemoteApply scope, then triggers
/// an immediate character-snapshot re-report so the host save and every peer's
/// clone learn the real result in the same run.
/// </summary>
public sealed partial class GameAdapter
{
	private void OnPlayerHealReceived(PlayerHealResultMsg msg)
	{
		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
		if (body == null) // Unity object — ==
		{
			_log.LogWarning("[PlayerInteraction] heal result received but the local body is not ready — skipped.");
			return;
		}

		var changed = false;
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			if (msg.HealerSteamId == _session.LocalSteamId)
			{
				var item = FindCarriedItemById(body, msg.ItemInstanceId);
				if (item == null) // Unity object — ==
				{
					_log.LogWarning("[Heal] local healer item {ItemId} not found — consumed state skipped.", msg.ItemInstanceId);
				}
				else
				{
					if (msg.ItemDestroyed)
					{
						Object.Destroy(item.gameObject);
						_log.LogInformation("[Heal] local item {ItemId} destroyed by cross-player heal.", msg.ItemInstanceId);
					}
					else
					{
						item.condition = msg.ItemConditionAfter;
						_log.LogInformation("[Heal] local item {ItemId} condition set to {Condition:F2} by cross-player heal.", msg.ItemInstanceId, msg.ItemConditionAfter);
					}

					changed = true;
				}
			}

			if (msg.TargetSteamId == _session.LocalSteamId && msg.Health is { } health)
			{
				_characterDataSync.ApplyHealState(body, health, msg.Limbs);
				_log.LogInformation("[Heal] local body healed by {Healer} (limb {Limb}).", msg.HealerSteamId, msg.HealedLimbIndex);
				changed = true;
			}
		}

		if (changed)
		{
			_characterDataSync.ReportInventoryChanged(body);
		}
	}

	bool IGameAdapter.HasLocalHealItem() => HasLocalHealItem();

	private bool HasLocalHealItem()
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
