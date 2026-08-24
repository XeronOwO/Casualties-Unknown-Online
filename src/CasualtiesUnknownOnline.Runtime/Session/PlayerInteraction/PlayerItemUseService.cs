using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The cross-player consumable-use operation (first slice: drink/food). The
/// host validates the user and target against its authoritative character
/// snapshots, consumes or drains a carried item, applies the curated
/// target-side body effect and sends the two participants one authoritative
/// result. It has no mutable session state — it only reacts to calls and
/// messages.
/// </summary>
internal sealed class PlayerItemUseService(
	ISessionControl session,
	PacketSender sender,
	PlayerCharacterAccess characters,
	IItemControl items,
	ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly PlayerCharacterAccess _characters = characters;
	private readonly IItemControl _items = items;
	private readonly ILogger _log = log;

	/// <summary>An authoritative cross-player consumable use result arrived — the Game Adapter applies the local participant half.</summary>
	public event Action<PlayerItemUseResultMsg>? UseReceived;

	/// <summary>Online UI entry: the local player uses one carried consumable on another player (0 = host auto-select).</summary>
	public void SendUseRequest(ulong targetSteamId, ulong itemInstanceId = 0)
	{
		if (!_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		var msg = new PlayerItemUseRequestMsg
		{
			TargetSteamId = targetSteamId,
			ItemInstanceId = itemInstanceId,
		};

		if (_session.Role == SessionRole.Host)
		{
			HandleUseRequest(_session.LocalSteamId, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.PlayerItemUseRequest, msg);
		}
	}

	/// <summary>Host only: a use request arrived — the guest→host wire and the host's own UI share this path.</summary>
	public void HandleUseRequest(ulong sender, PlayerItemUseRequestMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		var user = sender;
		var target = msg.TargetSteamId;
		if (user == target || user == 0 || target == 0)
		{
			return;
		}

		if (!_characters.IsInWorld(user) || !_characters.IsInWorld(target))
		{
			_log.LogWarning("[ItemUse] refused: {User} or {Target} is not in-world.", user, target);
			return;
		}

		var userData = _characters.GetCharacterData(user);
		var targetData = _characters.GetCharacterData(target);
		if (userData is null || targetData is null)
		{
			_log.LogWarning("[ItemUse] refused: no character snapshot for {User}/{Target}.", user, target);
			return;
		}

		if (userData.Health is not { } userHealth || !userHealth.Conscious || !userHealth.Alive)
		{
			_log.LogInformation("[ItemUse] refused: {User} is not conscious/alive and cannot use an item.", user);
			return;
		}

		if (targetData.Health is not { } targetHealth || !targetHealth.Conscious || !targetHealth.Alive)
		{
			_log.LogInformation("[ItemUse] refused: {Target} is not conscious/alive and cannot receive a consumable.", target);
			return;
		}

		var itemIndex = FindUseItemIndex(userData, msg.ItemInstanceId);
		if (itemIndex < 0)
		{
			_log.LogWarning("[ItemUse] refused: {User} has no usable consumable (requested {ItemId}).", user, msg.ItemInstanceId);
			return;
		}

		var originalItem = userData.Items[itemIndex];
		if (!IsActuallyUsable(originalItem))
		{
			_log.LogWarning("[ItemUse] refused: {ItemId} (id {InstanceId}) is empty or not in the catalog.", originalItem.ItemId, originalItem.InstanceId);
			return;
		}

		var newUserData = PlayerCharacterAccess.CloneCharacter(userData);
		var newItem = PlayerCharacterAccess.CloneItem(originalItem);
		var newTargetData = PlayerCharacterAccess.CloneCharacter(targetData);
		var destroyed = false;

		if (RemoteConsumeApplication.TryCreateDrinkPlan(originalItem.Liquids, out var drinkPlan))
		{
			RemoteConsumeApplication.ApplyDrink(newTargetData.Health!, drinkPlan);
			ApplyDrain(newItem, drinkPlan);
		}
		else if (RemoteConsumeCatalog.TryGetFood(originalItem.ItemId, out var food))
		{
			RemoteConsumeApplication.ApplyFood(newTargetData.Health!, food);
			newItem.Condition -= food.ConditionCost;
			destroyed = newItem.Condition <= 0f;
		}
		else
		{
			_log.LogWarning("[ItemUse] refused: {ItemId} (id {InstanceId}) is not in the remote-consumable catalog.", originalItem.ItemId, originalItem.InstanceId);
			return;
		}

		if (destroyed)
		{
			newUserData.Items.RemoveAll(i => i.InstanceId == originalItem.InstanceId);
		}
		else
		{
			newUserData.Items[itemIndex] = newItem;
		}

		_characters.SaveCharacterData(user, newUserData);
		_characters.SaveCharacterData(target, newTargetData);

		if (user != _session.LocalSteamId)
		{
			if (destroyed)
			{
				_items.RemoveTransferredItem(user, originalItem.InstanceId);
			}
			else
			{
				_items.UpdateTransferredItem(user, originalItem.InstanceId, PlayerCharacterAccess.CloneItem(newItem));
			}
		}

		_log.LogInformation(
			"[ItemUse] {User} used {ItemId} (id {InstanceId}) on {Target}; destroyed={Destroyed}.",
			user, originalItem.ItemId, originalItem.InstanceId, target, destroyed);

		PublishUse(new PlayerItemUseResultMsg
		{
			UserSteamId = user,
			TargetSteamId = target,
			ItemInstanceId = originalItem.InstanceId,
			ItemDestroyed = destroyed,
			ItemAfter = destroyed ? null : PlayerCharacterAccess.CloneItem(newItem),
			Health = newTargetData.Health,
			Limbs = [.. newTargetData.Limbs],
		});
	}

	/// <summary>Wire handler path: a use result arrived — surface it for the Game Adapter.</summary>
	public void FireUseReceived(PlayerItemUseResultMsg msg) => UseReceived?.Invoke(msg);

	private void PublishUse(PlayerItemUseResultMsg msg)
	{
		// The host applies its own participant half locally; guest participants
		// receive their authoritative body/item mutation directly.
		UseReceived?.Invoke(msg);
		if (msg.UserSteamId != _session.LocalSteamId)
		{
			_sender.Send(msg.UserSteamId, NetMsg.PlayerItemUseResult, msg);
		}

		if (msg.TargetSteamId != _session.LocalSteamId)
		{
			_sender.Send(msg.TargetSteamId, NetMsg.PlayerItemUseResult, msg);
		}
	}

	private static void ApplyDrain(CharacterItemMsg item, IReadOnlyList<LiquidStackMsg> drinkPlan)
	{
		var originalTotal = item.Liquids.Sum(s => s.Amount);
		var after = new List<LiquidStackMsg>(item.Liquids.Count);
		for (var i = 0; i < item.Liquids.Count; i++)
		{
			var consumed = i < drinkPlan.Count ? drinkPlan[i].Amount : 0f;
			after.Add(new LiquidStackMsg
			{
				LiquidId = item.Liquids[i].LiquidId,
				Amount = Math.Max(0f, item.Liquids[i].Amount - consumed),
			});
		}

		after.RemoveAll(s => s.Amount < 0.5f);
		var afterTotal = after.Sum(s => s.Amount);
		item.Liquids = after;

		// The wire item has no Capacity field; condition is total/capacity for a
		// WaterContainerItem. Reconstruct the proportional condition from the
		// original total/condition ratio so the local item update and the host
		// record agree without needing the game's LiquidItemInfo table.
		item.Condition = originalTotal > 0f && item.Condition > 0f
			? afterTotal * item.Condition / originalTotal
			: 0f;
	}

	private static int FindUseItemIndex(CharacterDataMsg data, ulong itemInstanceId)
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
			else if (IsActuallyUsable(item))
			{
				return i;
			}
		}

		return -1;
	}

	private static bool IsActuallyUsable(CharacterItemMsg item)
	{
		if (item.Condition <= 0f && RemoteConsumeCatalog.IsFoodItem(item.ItemId))
		{
			return false;
		}

		return RemoteConsumeCatalog.IsFoodItem(item.ItemId)
			|| RemoteConsumeApplication.TryCreateDrinkPlan(item.Liquids, out _);
	}
}
