using System;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Direct player-interaction heal half (partial of
/// <see cref="PlayerInteractionService"/>). The host validates the healer and
/// target against its authoritative character snapshots, consumes a carried
/// medical item, applies the healing effect to the target's worst limb and
/// sends the two participants one authoritative result.
/// </summary>
public sealed partial class PlayerInteractionService
{
	/// <summary>An authoritative cross-player heal result arrived — the Game Adapter applies the local participant half.</summary>
	public event Action<PlayerHealResultMsg>? HealReceived;

	/// <summary>Online UI entry: the local player uses one carried medical item on another player (0 = host auto-select).</summary>
	public void SendHealRequest(ulong targetSteamId, ulong itemInstanceId = 0)
	{
		if (!_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		var msg = new PlayerHealRequestMsg
		{
			TargetSteamId = targetSteamId,
			ItemInstanceId = itemInstanceId,
		};

		if (_session.Role == SessionRole.Host)
		{
			HandleHealRequest(_session.LocalSteamId, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.PlayerHealRequest, msg);
		}
	}

	/// <summary>Host only: a heal request arrived — the guest→host wire and the host's own UI share this path.</summary>
	public void HandleHealRequest(ulong sender, PlayerHealRequestMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		var healer = sender;
		var target = msg.TargetSteamId;
		if (healer == target || healer == 0 || target == 0)
		{
			return;
		}

		if (!IsInWorld(healer) || !IsInWorld(target))
		{
			_log.LogWarning("[Heal] refused: {Healer} or {Target} is not in-world.", healer, target);
			return;
		}

		var healerData = GetCharacterData(healer);
		var targetData = GetCharacterData(target);
		if (healerData is null || targetData is null)
		{
			_log.LogWarning("[Heal] refused: no character snapshot for {Healer}/{Target}.", healer, target);
			return;
		}

		if (healerData.Health is not { } healerHealth || !healerHealth.Conscious || !healerHealth.Alive)
		{
			_log.LogInformation("[Heal] refused: {Healer} is not conscious/alive and cannot heal.", healer);
			return;
		}

		if (targetData.Health is not { } targetHealth || !targetHealth.Alive)
		{
			_log.LogInformation("[Heal] refused: {Target} is not alive (this slice has no CPR).", target);
			return;
		}

		if (targetData.Limbs.Count == 0)
		{
			_log.LogWarning("[Heal] refused: {Target} has no limb data.", target);
			return;
		}

		var itemIndex = FindHealItemIndex(healerData, msg.ItemInstanceId);
		if (itemIndex < 0)
		{
			_log.LogWarning("[Heal] refused: {Healer} has no usable medical item (requested {ItemId}).", healer, msg.ItemInstanceId);
			return;
		}

		var originalItem = healerData.Items[itemIndex];
		if (!RemoteHealProfiles.TryGet(originalItem.ItemId, out var profile))
		{
			_log.LogWarning("[Heal] refused: {ItemId} is not in the heal-item profile set.", originalItem.ItemId);
			return;
		}

		var limbIndex = RemoteHealApplication.PickMostInjuredLimb(targetData.Limbs);
		if (limbIndex < 0)
		{
			_log.LogWarning("[Heal] refused: {Target} has no healable limb.", target);
			return;
		}

		var newHealerData = CloneCharacter(healerData);
		var consumed = CloneItem(newHealerData.Items[itemIndex]);
		consumed.Condition -= profile.ConditionCost;
		var destroyed = consumed.Condition <= 0f;
		if (destroyed)
		{
			newHealerData.Items.RemoveAll(i => i.InstanceId == originalItem.InstanceId);
		}
		else
		{
			newHealerData.Items[itemIndex] = consumed;
		}

		var newTargetData = CloneCharacter(targetData);
		var healedLimb = CloneLimb(newTargetData.Limbs[limbIndex]);
		RemoteHealApplication.Apply(healedLimb, profile);
		newTargetData.Limbs[limbIndex] = healedLimb;

		SaveCharacterData(healer, newHealerData);
		SaveCharacterData(target, newTargetData);

		if (healer != _session.LocalSteamId)
		{
			if (destroyed)
			{
				_items.RemoveTransferredItem(healer, originalItem.InstanceId);
			}
			else
			{
				_items.UpdateTransferredItem(healer, originalItem.InstanceId, CloneItem(consumed));
			}
		}

		_log.LogInformation(
			"[Heal] {Healer} heals {Target} with {ItemId} (id {InstanceId}) on limb {Limb}; item destroyed={Destroyed}.",
			healer, target, originalItem.ItemId, originalItem.InstanceId, limbIndex, destroyed);
		PublishHeal(new PlayerHealResultMsg
		{
			HealerSteamId = healer,
			TargetSteamId = target,
			ItemInstanceId = originalItem.InstanceId,
			ItemDestroyed = destroyed,
			ItemConditionAfter = destroyed ? 0f : consumed.Condition,
			HealedLimbIndex = limbIndex,
			Health = newTargetData.Health,
			Limbs = [.. newTargetData.Limbs],
		});
	}

	/// <summary>Wire handler path: a heal result arrived — surface it for the Game Adapter.</summary>
	public void FireHealReceived(PlayerHealResultMsg msg) => HealReceived?.Invoke(msg);

	private void PublishHeal(PlayerHealResultMsg msg)
	{
		// The host applies its own participant half locally; guest participants
		// receive their authoritative body mutation directly.
		HealReceived?.Invoke(msg);
		if (msg.HealerSteamId != _session.LocalSteamId)
		{
			_sender.Send(msg.HealerSteamId, NetMsg.PlayerHealResult, msg);
		}

		if (msg.TargetSteamId != _session.LocalSteamId)
		{
			_sender.Send(msg.TargetSteamId, NetMsg.PlayerHealResult, msg);
		}
	}

	private static int FindHealItemIndex(CharacterDataMsg data, ulong itemInstanceId)
	{
		for (var i = 0; i < data.Items.Count; i++)
		{
			var item = data.Items[i];
			if (item.SlotIndex < 0 || item.InstanceId == 0)
			{
				continue;
			}

			if (itemInstanceId != 0)
			{
				if (item.InstanceId == itemInstanceId)
				{
					return i;
				}
			}
			else if (RemoteHealProfiles.IsHealItem(item.ItemId))
			{
				return i;
			}
		}

		return -1;
	}
}
