using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState;
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
	ItemKernelAuthority kernelAuthority,
	PlayerInteractionResultAuthority resultAuthority,
	ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly PlayerCharacterAccess _characters = characters;
	private readonly IItemControl _items = items;
	private readonly IHostRules _hostRules = hostRules;
	private readonly IPlayerInteractionVisibility _visibility = visibility;
	private readonly ItemKernelAuthority _kernelAuthority = kernelAuthority;
	private readonly PlayerInteractionResultAuthority _resultAuthority = resultAuthority;
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
		else
		{
			CommitCarriedToHost(msg.ItemInstanceId, transferred);
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
	/// Make the kernel own the host's resulting carried item after a cross-player
	/// take. Guest recipients already go through the transfer table's
	/// adopt path; the host has no transfer-table row, so this keeps the item
	/// kernel authoritative when the host is the recipient.
	/// </summary>
	private void CommitCarriedToHost(ulong itemId, CharacterItemMsg item)
	{
		var current = _kernelAuthority.FindItem(itemId);
		if (current is null)
		{
			_kernelAuthority.TrySpawnCarried(_session.LocalSteamId, itemId, item.ItemId, item, out _, out var rejection);
			if (rejection is not null)
			{
				_log.LogWarning("[Take] host item spawn rejected {ItemId}: {Reason} ({Message}).",
					itemId, rejection.Reason, rejection.Message);
			}

			return;
		}

		if (!_kernelAuthority.TryTransfer(
			_session.LocalSteamId,
			itemId,
			new ActorId(_session.LocalSteamId),
			item,
			out _,
			out var transferRejection))
		{
			_log.LogWarning("[Take] host item transfer rejected {ItemId}: {Reason} ({Message}).",
				itemId, transferRejection!.Reason, transferRejection.Message);
		}
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

	/// <summary>Kernel projection path: a transfer result event arrived — surface it for the Game Adapter.</summary>
	public void FireTransferReceived(PlayerInventoryTransferMsg msg) => TransferReceived?.Invoke(msg);

	private void PublishTransfer(PlayerInventoryTransferMsg msg)
	{
		// The kernel is the single authority. The committed result event is
		// broadcast through KernelEnvelope and the PlayerInteractionKernelProjection
		// raises this same event on the host (BatchCommitted) and guests
		// (BatchApplied); no legacy direct result wire remains.
		if (!_resultAuthority.TryRecordPlayerInventoryTransfer(
			_session.LocalSteamId,
			msg.FromSteamId,
			msg.ToSteamId,
			PlayerInteractionKernelCodec.FromCharacterItem(msg.Item!),
			out _,
			out var rejection))
		{
			_log.LogWarning("[Take] kernel result rejected {From} -> {To}: {Reason} ({Message}).",
				msg.FromSteamId, msg.ToSteamId, rejection!.Reason, rejection.Message);
		}
	}
}
