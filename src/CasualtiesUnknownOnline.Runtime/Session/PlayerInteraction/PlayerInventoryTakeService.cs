using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The cross-player inventory-take operation. The host is the authority: it
/// validates against its authoritative character snapshots, moves the item
/// between those snapshots, updates the guest transfer table when a guest
/// participates, and sends the authoritative body mutation to the two
/// participants. It has no mutable session state — it only reacts to calls and
/// messages.
/// </summary>
internal sealed class PlayerInventoryTakeService(
	ISessionControl session,
	PacketSender sender,
	PlayerCharacterAccess characters,
	IItemControl items,
	IHostRules hostRules,
	IPlayerInteractionVisibility visibility,
	ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly PlayerCharacterAccess _characters = characters;
	private readonly IItemControl _items = items;
	private readonly IHostRules _hostRules = hostRules;
	private readonly IPlayerInteractionVisibility _visibility = visibility;
	private readonly ILogger _log = log;

	/// <summary>An authoritative cross-player inventory transfer arrived — the Game Adapter applies the body mutation.</summary>
	public event Action<PlayerInventoryTransferMsg>? TransferReceived;

	/// <summary>Online UI entry: the local player takes one item from another player.</summary>
	public void SendTakeRequest(ulong ownerSteamId, ulong itemInstanceId)
	{
		if (!_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		var msg = new PlayerInventoryTakeRequestMsg
		{
			OwnerSteamId = ownerSteamId,
			ItemInstanceId = itemInstanceId,
		};

		if (_session.Role == SessionRole.Host)
		{
			HandleTakeRequest(_session.LocalSteamId, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.PlayerInventoryTakeRequest, msg);
		}
	}

	/// <summary>Host only: a take request arrived — the guest→host wire and the host's own UI share this path.</summary>
	public void HandleTakeRequest(ulong sender, PlayerInventoryTakeRequestMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		var from = msg.OwnerSteamId;
		var to = sender;
		if (from == to || from == 0 || to == 0 || msg.ItemInstanceId == 0)
		{
			return;
		}

		if (!_hostRules.AllowRemoteInventoryTake)
		{
			_log.LogInformation("[Take] refused: host has disabled cross-player inventory take (rule AllowRemoteInventoryTake=false).");
			return;
		}

		if (!_characters.IsInWorld(from) || !_characters.IsInWorld(to))
		{
			_log.LogWarning("[Take] refused: {From} or {To} is not in-world.", from, to);
			return;
		}

		if (!_visibility.HasLineOfSight(to, from))
		{
			_log.LogInformation("[Take] refused: {To} cannot see {From}.",
				to, from);
			return;
		}

		var source = _characters.GetCharacterData(from);
		var target = _characters.GetCharacterData(to);
		if (source is null || target is null)
		{
			_log.LogWarning("[Take] refused: no character snapshot for {From}/{To}.", from, to);
			return;
		}

		// The game's direct-interaction rule (KrokMP-compatible default): a
		// conscious player's inventory is not takeable. Only an unconscious or
		// dead body can be searched/taken from; the Online UI surfaces the
		// button only in that state, and the host re-checks the authoritative
		// snapshot here.
		if (source.Health is not { } health || (health.Conscious && health.Alive))
		{
			_log.LogInformation("[Take] refused: {From} is conscious/alive and not takeable.", from);
			return;
		}

		var newSource = PlayerCharacterAccess.CloneCharacter(source);
		if (!TryFindAndRemove(newSource.Items, msg.ItemInstanceId, out var original))
		{
			_log.LogWarning("[Take] refused: {From} has no item instance {ItemId}.", from, msg.ItemInstanceId);
			return;
		}

		if (original.SlotIndex < 0)
		{
			// Worn items are excluded in this slice — the character restore path
			// handles them separately and the Online UI only offers slot items.
			_log.LogInformation("[Take] refused: item {ItemId} is worn (slot {Slot}) — only backpack/hand slot items are takeable in this slice.", msg.ItemInstanceId, original.SlotIndex);
			return;
		}

		var targetSlot = PlayerCharacterAccess.FirstEmptySlot(target);
		if (targetSlot < 0)
		{
			_log.LogWarning("[Take] refused: {To} has no empty inventory slot.", to);
			return;
		}

		var newTarget = PlayerCharacterAccess.CloneCharacter(target);
		var transferred = PlayerCharacterAccess.CloneItem(original);
		transferred.SlotIndex = targetSlot; // the host picks a concrete empty slot; the recipient's immediate re-report confirms it
		newTarget.Items.Add(transferred);

		_characters.SaveCharacterData(from, newSource);
		_characters.SaveCharacterData(to, newTarget);

		// Guest ownership records drive use/slot/drop arbitration and the
		// reconnect restore merge — move the entry with the transfer.
		if (from != _session.LocalSteamId)
		{
			_items.RemoveTransferredItem(from, msg.ItemInstanceId);
		}

		if (to != _session.LocalSteamId)
		{
			_items.AdoptTransferredItem(to, msg.ItemInstanceId, transferred);
		}

		_log.LogInformation("[Take] {To} takes {ItemId} (id {InstanceId}) from {From}.", to, original.ItemId, msg.ItemInstanceId, from);
		PublishTransfer(new PlayerInventoryTransferMsg
		{
			FromSteamId = from,
			ToSteamId = to,
			Item = transferred,
		});
	}

	/// <summary>
	/// Remove one carried item from a character snapshot's item tree. Container
	/// contents are recursive, so the cross-player take operation must be able
	/// to lift an item out of any depth — not just the top-level body slots.
	/// Operates on the caller's already-cloned tree; never mutates the live
	/// snapshot directly.
	/// </summary>
	private static bool TryFindAndRemove(List<CharacterItemMsg> items, ulong instanceId, out CharacterItemMsg removed)
	{
		for (var i = 0; i < items.Count; i++)
		{
			if (items[i].InstanceId == instanceId)
			{
				removed = items[i];
				items.RemoveAt(i);
				return true;
			}

			if (TryFindAndRemove(items[i].Contents, instanceId, out removed))
			{
				return true;
			}
		}

		removed = null!;
		return false;
	}

	/// <summary>Wire handler path: a transfer message arrived — surface it for the Game Adapter.</summary>
	public void FireTransferReceived(PlayerInventoryTransferMsg msg) => TransferReceived?.Invoke(msg);

	private void PublishTransfer(PlayerInventoryTransferMsg msg)
	{
		// The host applies its own participant half locally; guest participants
		// receive their authoritative body mutation directly.
		TransferReceived?.Invoke(msg);
		if (msg.FromSteamId != _session.LocalSteamId)
		{
			_sender.Send(msg.FromSteamId, NetMsg.PlayerInventoryTransfer, msg);
		}

		if (msg.ToSteamId != _session.LocalSteamId)
		{
			_sender.Send(msg.ToSteamId, NetMsg.PlayerInventoryTransfer, msg);
		}
	}
}
