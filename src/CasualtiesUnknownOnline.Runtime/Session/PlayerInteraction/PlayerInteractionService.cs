using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The direct player-to-player interaction domain coordinator. It is a thin
/// composition facade over the three authoritative operations: inventory take,
/// carry/release, and heal. The host is the cross-player authority for all
/// three; each operation owns its own validation/state/event surface so the
/// domain can grow without turning this class into a mixed god-object.
/// </summary>
public sealed class PlayerInteractionService : IPlayerInteractionControl, IDisposable
{
	private readonly PlayerInventoryTakeService _take;
	private readonly PlayerCarryService _carry;
	private readonly PlayerHealService _heal;
	private readonly PlayerItemUseService _itemUse;

	public event Action<PlayerInventoryTransferMsg>? TransferReceived
	{
		add => _take.TransferReceived += value;
		remove => _take.TransferReceived -= value;
	}

	public event Action<PlayerCarryStateMsg>? CarryStateChanged
	{
		add => _carry.CarryStateChanged += value;
		remove => _carry.CarryStateChanged -= value;
	}

	public event Action<PlayerHealResultMsg>? HealReceived
	{
		add => _heal.HealReceived += value;
		remove => _heal.HealReceived -= value;
	}

	public event Action<PlayerItemUseResultMsg>? UseReceived
	{
		add => _itemUse.UseReceived += value;
		remove => _itemUse.UseReceived -= value;
	}

	public PlayerInteractionService(
		ISessionControl session,
		PacketSender sender,
		ICharacterDataControl characters,
		IItemControl items,
		ILogger<PlayerInteractionService> log)
	{
		var access = new PlayerCharacterAccess(session, characters);
		_take = new PlayerInventoryTakeService(session, sender, access, items, log);
		_carry = new PlayerCarryService(session, sender, access, log);
		_heal = new PlayerHealService(session, sender, access, items, log);
		_itemUse = new PlayerItemUseService(session, sender, access, items, log);
	}

	public void SendTakeRequest(ulong ownerSteamId, ulong itemInstanceId) =>
		_take.SendTakeRequest(ownerSteamId, itemInstanceId);

	public void HandleTakeRequest(ulong sender, PlayerInventoryTakeRequestMsg msg) =>
		_take.HandleTakeRequest(sender, msg);

	public void FireTransferReceived(PlayerInventoryTransferMsg msg) =>
		_take.FireTransferReceived(msg);

	public void SendCarryStartRequest(ulong targetSteamId) =>
		_carry.SendCarryStartRequest(targetSteamId);

	public void SendPiggybackRequest(ulong targetSteamId) =>
		_carry.SendPiggybackRequest(targetSteamId);

	public void SendCarryStopRequest(ulong carriedSteamId) =>
		_carry.SendCarryStopRequest(carriedSteamId);

	public void HandleCarryStartRequest(ulong sender, PlayerCarryStartRequestMsg msg) =>
		_carry.HandleCarryStartRequest(sender, msg);

	public void HandleCarryStopRequest(ulong sender, PlayerCarryStopRequestMsg msg) =>
		_carry.HandleCarryStopRequest(sender, msg);

	public void FireCarryStateReceived(PlayerCarryStateMsg msg) =>
		_carry.FireCarryStateReceived(msg);

	public bool TryGetCarrier(ulong carriedSteamId, out ulong carrierSteamId) =>
		_carry.TryGetCarrier(carriedSteamId, out carrierSteamId);

	public bool TryGetCarried(ulong carrierSteamId, out ulong carriedSteamId) =>
		_carry.TryGetCarried(carrierSteamId, out carriedSteamId);

	public void SendHealRequest(ulong targetSteamId, ulong itemInstanceId = 0) =>
		_heal.SendHealRequest(targetSteamId, itemInstanceId);

	public void HandleHealRequest(ulong sender, PlayerHealRequestMsg msg) =>
		_heal.HandleHealRequest(sender, msg);

	public void FireHealReceived(PlayerHealResultMsg msg) =>
		_heal.FireHealReceived(msg);

	public void SendUseRequest(ulong targetSteamId, ulong itemInstanceId = 0) =>
		_itemUse.SendUseRequest(targetSteamId, itemInstanceId);

	public void HandleUseRequest(ulong sender, PlayerItemUseRequestMsg msg) =>
		_itemUse.HandleUseRequest(sender, msg);

	public void FireUseReceived(PlayerItemUseResultMsg msg) =>
		_itemUse.FireUseReceived(msg);

	public void Dispose() => _carry.Dispose();
}
