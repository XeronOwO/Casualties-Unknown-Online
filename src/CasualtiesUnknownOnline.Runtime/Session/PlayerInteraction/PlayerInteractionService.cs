using System;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Direct player-to-player interaction domain — first slice: take a carried
/// item from another in-world player. The host is the cross-player authority.
/// It validates the item against its authoritative character snapshots (the
/// host's own cached snapshot + every guest's saved report), moves the item
/// between those snapshots, updates the guest transfer table where a guest is
/// a participant, and sends each participant one authoritative body mutation.
/// The receiving Game Adapter applies the mutation inside a RemoteApply scope
/// and re-reports its character snapshot immediately, so the clone renderers
/// and the host's saved data converge on the real local slot in the same run.
/// No pump and no mutable session state — it only reacts to calls and messages.
/// </summary>
public sealed class PlayerInteractionService(
	ISessionControl session,
	PacketSender sender,
	ICharacterDataControl characters,
	IItemControl items,
	ILogger<PlayerInteractionService> log) : IPlayerInteractionControl, IDisposable
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly ICharacterDataControl _characters = characters;
	private readonly IItemControl _items = items;
	private readonly ILogger<PlayerInteractionService> _log = log;

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

		if (!IsInWorld(from) || !IsInWorld(to))
		{
			_log.LogWarning("[Take] refused: {From} or {To} is not in-world.", from, to);
			return;
		}

		var source = GetCharacterData(from);
		var target = GetCharacterData(to);
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

		var idx = source.Items.FindIndex(i => i.InstanceId == msg.ItemInstanceId);
		if (idx < 0)
		{
			_log.LogWarning("[Take] refused: {From} has no item instance {ItemId}.", from, msg.ItemInstanceId);
			return;
		}

		var original = source.Items[idx];
		if (original.SlotIndex < 0)
		{
			// Worn items are excluded in this slice — the character restore path
			// handles them separately and the Online UI only offers slot items.
			_log.LogInformation("[Take] refused: item {ItemId} is worn (slot {Slot}) — only backpack/hand slot items are takeable in this slice.", msg.ItemInstanceId, original.SlotIndex);
			return;
		}

		var targetSlot = FirstEmptySlot(target);
		if (targetSlot < 0)
		{
			_log.LogWarning("[Take] refused: {To} has no empty inventory slot.", to);
			return;
		}

		var newSource = CloneCharacter(source);
		var newTarget = CloneCharacter(target);
		newSource.Items.RemoveAll(i => i.InstanceId == msg.ItemInstanceId);

		var transferred = CloneItem(original);
		transferred.SlotIndex = targetSlot; // the host picks a concrete empty slot; the recipient's immediate re-report confirms it
		newTarget.Items.Add(transferred);

		SaveCharacterData(from, newSource);
		SaveCharacterData(to, newTarget);

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

	private CharacterDataMsg? GetCharacterData(ulong steamId) =>
		steamId == _session.LocalSteamId
			? _characters.GetHostCharacterData()
			: _characters.GetSavedCharacter(steamId);

	private void SaveCharacterData(ulong steamId, CharacterDataMsg data)
	{
		if (steamId == _session.LocalSteamId)
		{
			_characters.SaveHostCharacterData(data);
		}
		else
		{
			_characters.SaveCharacterData(steamId, data);
		}
	}

	private bool IsInWorld(ulong steamId) =>
		steamId == _session.LocalSteamId
			? _session.LocalInWorld
			: _session.TryGetMember(steamId, out var member) && member.InWorld;

	/// <summary>
	/// The first unoccupied backpack/hand slot in a character snapshot. SlotCount
	/// is carried by v26 snapshots; a 0 from an older peer falls back to the
	/// game's known minimum slot count (3) rather than refusing every transfer.
	/// </summary>
	private static int FirstEmptySlot(CharacterDataMsg data)
	{
		var count = data.SlotCount > 0 ? data.SlotCount : 3;
		var occupied = data.Items.Where(i => i.SlotIndex >= 0).Select(i => i.SlotIndex).ToHashSet();
		for (var slot = 0; slot < count; slot++)
		{
			if (!occupied.Contains(slot))
			{
				return slot;
			}
		}

		return -1;
	}

	private static CharacterDataMsg CloneCharacter(CharacterDataMsg source) => new()
	{
		Skills = source.Skills,
		Health = source.Health,
		Limbs = source.Limbs,
		Items = [.. source.Items],
		HandSlot = source.HandSlot,
		OwnerSteamId = source.OwnerSteamId,
		Position = source.Position,
	};

	private static CharacterItemMsg CloneItem(CharacterItemMsg item) => new()
	{
		InstanceId = item.InstanceId,
		ItemId = item.ItemId,
		Condition = item.Condition,
		SlotIndex = item.SlotIndex,
		Favourited = item.Favourited,
		Components = item.Components,
		Contents = item.Contents,
		Liquids = item.Liquids,
	};

	public void Dispose()
	{
	}
}
