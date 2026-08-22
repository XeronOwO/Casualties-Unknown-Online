using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Direct player-to-player interaction domain — currently the "take a carried
/// item from another in-world player" operation and the "carry/release another
/// player" operation. The host is the cross-player authority for both: it
/// validates against its authoritative character snapshots (and, for carry,
/// against the current carry relation), moves/records the state, and sends the
/// authoritative results to the involved/affected members. The receiving Game
/// Adapter applies the local body mutation inside a RemoteApply scope (take) or
/// drives the carried local body from the carrier's state (carry).
/// No pump and no mutable session state outside the host-owned carry relation —
/// the service only reacts to calls and messages.
/// </summary>
public sealed partial class PlayerInteractionService : IPlayerInteractionControl, IDisposable
{
	private readonly ISessionControl _session;
	private readonly PacketSender _sender;
	private readonly ICharacterDataControl _characters;
	private readonly IItemControl _items;
	private readonly ILogger<PlayerInteractionService> _log;

	/// <summary>Host-owned carry table: carried SteamId → carrier SteamId.</summary>
	private readonly Dictionary<ulong, ulong> _carriedBy = [];

	/// <summary>Host-owned carry table: carrier SteamId → carried SteamId (kept in lockstep for O(1) lookups).</summary>
	private readonly Dictionary<ulong, ulong> _carrying = [];

	public event Action<PlayerInventoryTransferMsg>? TransferReceived;

	public event Action<PlayerCarryStateMsg>? CarryStateChanged;

	public PlayerInteractionService(
		ISessionControl session,
		PacketSender sender,
		ICharacterDataControl characters,
		IItemControl items,
		ILogger<PlayerInteractionService> log)
	{
		_session = session;
		_sender = sender;
		_characters = characters;
		_items = items;
		_log = log;

		_session.SessionEnded += OnSessionEnded;
		_session.MemberRemoved += OnMemberRemoved;
		_session.RemoteSceneChanged += OnRemoteSceneChanged;
	}

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

	// ---- Carry / release ----

	/// <summary>Online UI entry: the local player starts carrying another player.</summary>
	public void SendCarryStartRequest(ulong targetSteamId)
	{
		if (!_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		var msg = new PlayerCarryStartRequestMsg { TargetSteamId = targetSteamId };
		if (_session.Role == SessionRole.Host)
		{
			HandleCarryStartRequest(_session.LocalSteamId, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.PlayerCarryStartRequest, msg);
		}
	}

	/// <summary>Online UI entry: the local player releases the player they carry.</summary>
	public void SendCarryStopRequest(ulong carriedSteamId)
	{
		if (!_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		var msg = new PlayerCarryStopRequestMsg { CarriedSteamId = carriedSteamId };
		if (_session.Role == SessionRole.Host)
		{
			HandleCarryStopRequest(_session.LocalSteamId, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.PlayerCarryStopRequest, msg);
		}
	}

	/// <summary>Host only: a carry-start request arrived — the guest→host wire and the host's own UI share this path.</summary>
	public void HandleCarryStartRequest(ulong sender, PlayerCarryStartRequestMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		var carrier = sender;
		var carried = msg.TargetSteamId;
		if (carrier == carried || carrier == 0 || carried == 0)
		{
			return;
		}

		if (!IsInWorld(carrier) || !IsInWorld(carried))
		{
			_log.LogWarning("[Carry] refused: {Carrier} or {Carried} is not in-world.", carrier, carried);
			return;
		}

		// Cooperative default: only an unconscious or dead body can be carried.
		// The Online UI surfaces the button only in that state; the host
		// re-checks the authoritative snapshot here.
		var target = GetCharacterData(carried);
		if (target?.Health is not { } health || (health.Conscious && health.Alive))
		{
			_log.LogInformation("[Carry] refused: {Carried} is conscious/alive and not carryable.", carried);
			return;
		}

		var carrierData = GetCharacterData(carrier);
		if (carrierData?.Health is not { } carrierHealth || !carrierHealth.Conscious || !carrierHealth.Alive)
		{
			_log.LogInformation("[Carry] refused: {Carrier} is not conscious/alive and cannot carry.", carrier);
			return;
		}

		if (TryGetCarrier(carrier, out _) || TryGetCarried(carrier, out _)
			|| TryGetCarrier(carried, out _) || TryGetCarried(carried, out _))
		{
			_log.LogInformation("[Carry] refused: {Carrier} or {Carried} already participates in a carry relation.", carrier, carried);
			return;
		}

		_carriedBy[carried] = carrier;
		_carrying[carrier] = carried;
		_log.LogInformation("[Carry] {Carrier} starts carrying {Carried}.", carrier, carried);
		PublishCarryState(new PlayerCarryStateMsg
		{
			CarrierSteamId = carrier,
			CarriedSteamId = carried,
		});
	}

	/// <summary>Host only: a carry-stop request arrived.</summary>
	public void HandleCarryStopRequest(ulong sender, PlayerCarryStopRequestMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		var carrier = sender;
		var carried = msg.CarriedSteamId;
		if (carrier == 0 || carried == 0 || !_carrying.TryGetValue(carrier, out var current) || current != carried)
		{
			_log.LogWarning("[Carry] stop refused: {Carrier} is not carrying {Carried}.", carrier, carried);
			return;
		}

		_carriedBy.Remove(carried);
		_carrying.Remove(carrier);
		_log.LogInformation("[Carry] {Carrier} stops carrying {Carried}.", carrier, carried);
		PublishCarryState(new PlayerCarryStateMsg
		{
			CarrierSteamId = carrier,
			CarriedSteamId = 0,
		});
	}

	/// <summary>Wire handler path: a carry-state broadcast arrived — update the local mirror and surface it for the Game Adapter/UI.</summary>
	public void FireCarryStateReceived(PlayerCarryStateMsg msg)
	{
		ApplyCarryState(msg);
		CarryStateChanged?.Invoke(msg);
	}

	/// <summary>Read-only UI mirror: who currently carries the given player, if any.</summary>
	public bool TryGetCarrier(ulong carriedSteamId, out ulong carrierSteamId) =>
		_carriedBy.TryGetValue(carriedSteamId, out carrierSteamId);

	/// <summary>Read-only UI mirror: whom the given player currently carries, if any.</summary>
	public bool TryGetCarried(ulong carrierSteamId, out ulong carriedSteamId) =>
		_carrying.TryGetValue(carrierSteamId, out carriedSteamId);

	private void PublishCarryState(PlayerCarryStateMsg msg)
	{
		// The host applies its own side locally; every guest receives the same
		// authoritative state (including the two participants).
		ApplyCarryState(msg);
		CarryStateChanged?.Invoke(msg);
		_sender.SendToAll(_session.Members
			.Where(m => m.SteamId != _session.LocalSteamId)
			.Select(m => m.SteamId), NetMsg.PlayerCarryState, msg);
	}

	private void ApplyCarryState(PlayerCarryStateMsg msg)
	{
		if (msg.CarriedSteamId == 0)
		{
			if (_carrying.TryGetValue(msg.CarrierSteamId, out var oldCarried))
			{
				_carriedBy.Remove(oldCarried);
				_carrying.Remove(msg.CarrierSteamId);
			}

			return;
		}

		_carriedBy[msg.CarriedSteamId] = msg.CarrierSteamId;
		_carrying[msg.CarrierSteamId] = msg.CarriedSteamId;
	}


	// ---- Session cleanup (host-owned carry table + guest mirror) ----

	private void OnSessionEnded()
	{
		_carriedBy.Clear();
		_carrying.Clear();
	}

	private void OnMemberRemoved(ulong steamId) => ClearIfInvolved(steamId);

	private void OnRemoteSceneChanged(ulong steamId, bool inWorld)
	{
		if (!inWorld)
		{
			ClearIfInvolved(steamId);
		}
	}

	private void ClearIfInvolved(ulong steamId)
	{
		var releasedCarrier = 0UL;
		ulong? releasedCarried = null;

		if (_carrying.TryGetValue(steamId, out var oldCarried))
		{
			_carriedBy.Remove(oldCarried);
			_carrying.Remove(steamId);
			releasedCarrier = steamId;
			releasedCarried = oldCarried;
		}

		if (_carriedBy.TryGetValue(steamId, out var oldCarrier))
		{
			_carrying.Remove(oldCarrier);
			_carriedBy.Remove(steamId);
			releasedCarrier = oldCarrier;
			releasedCarried = steamId;
		}

		if (releasedCarrier != 0 && releasedCarried is { } carried)
		{
			_log.LogInformation("[Carry] cleaned up relation involving {SteamId}.", steamId);
			if (_session.Role == SessionRole.Host)
			{
				PublishCarryState(new PlayerCarryStateMsg
				{
					CarrierSteamId = releasedCarrier,
					CarriedSteamId = 0,
				});
			}
		}
	}

	/// <summary>Unsubscribe from session lifecycle events.</summary>
	public void Dispose()
	{
		_session.SessionEnded -= OnSessionEnded;
		_session.MemberRemoved -= OnMemberRemoved;
		_session.RemoteSceneChanged -= OnRemoteSceneChanged;
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

	private static CharacterLimbMsg CloneLimb(CharacterLimbMsg limb) => new()
	{
		Index = limb.Index,
		SkinHealth = limb.SkinHealth,
		MuscleHealth = limb.MuscleHealth,
		Broken = limb.Broken,
		Dislocated = limb.Dislocated,
		Splinted = limb.Splinted,
		Infected = limb.Infected,
		InfectionAmount = limb.InfectionAmount,
		BleedAmount = limb.BleedAmount,
		DisinfectionTime = limb.DisinfectionTime,
		Pain = limb.Pain,
		DislocationTimer = limb.DislocationTimer,
		BoneHealTimer = limb.BoneHealTimer,
		BlockedBleeding = limb.BlockedBleeding,
		Shrapnel = limb.Shrapnel,
		FurBloodAmount = limb.FurBloodAmount,
		BandageSlowAmount = limb.BandageSlowAmount,
		SkinHealAmount = limb.SkinHealAmount,
		Dismembered = limb.Dismembered,
	};
}
