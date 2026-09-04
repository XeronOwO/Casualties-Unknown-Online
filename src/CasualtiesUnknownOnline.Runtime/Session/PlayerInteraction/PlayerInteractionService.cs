using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The direct player-to-player interaction domain coordinator. It is a thin
/// composition facade over the authoritative operations: inventory take,
/// carry/release, heal, consumable use, and push/shove. The host is the
/// cross-player authority for all of them; each operation owns its own
/// validation/state/event surface so the domain can grow without turning this
/// class into a mixed god-object.
/// </summary>
public sealed class PlayerInteractionService : IPlayerInteractionControl, IDisposable
{
	private readonly PlayerInventoryTakeService _take;
	private readonly PlayerRemoteInventoryService _remoteInventory;
	private readonly PlayerCarryService _carry;
	private readonly PlayerKernelCarryProjection _carryKernelProjection;
	private readonly PlayerHealService _heal;
	private readonly PlayerItemUseService _itemUse;
	private readonly PlayerPushService _push;
	private readonly PlayerInteractionKernelProjection _resultProjection;

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

	public event Action<PlayerPushResultMsg>? PushReceived
	{
		add => _push.PushReceived += value;
		remove => _push.PushReceived -= value;
	}

	public PlayerInteractionService(
		ISessionControl session,
		PacketSender sender,
		ICharacterDataControl characters,
		IItemControl items,
		IEntitySyncControl entities,
		IHostRules hostRules,
		IPlayerInteractionVisibility visibility,
		ITimeSource time,
		ItemKernelAuthority kernelAuthority,
		ILogger<PlayerInteractionService> log)
	{
		var access = new PlayerCharacterAccess(session, characters);
		var resultAuthority = new PlayerInteractionResultAuthority(kernelAuthority);
		_take = new PlayerInventoryTakeService(session, sender, access, items, hostRules, visibility, kernelAuthority, resultAuthority, log);
		_remoteInventory = new PlayerRemoteInventoryService(session, sender, access, items, hostRules, visibility, kernelAuthority, resultAuthority, log);
		_carry = new PlayerCarryService(
			session,
			sender,
			access,
			visibility,
			kernelAuthority,
			log);
		_carryKernelProjection = new PlayerKernelCarryProjection(
			kernelAuthority,
			_carry,
			session,
			log);
		_heal = new PlayerHealService(session, sender, access, items, visibility, kernelAuthority, resultAuthority, log);
		_itemUse = new PlayerItemUseService(session, sender, access, items, visibility, kernelAuthority, resultAuthority, log);
		_resultProjection = new PlayerInteractionKernelProjection(kernelAuthority, this, session, log);
		_push = new PlayerPushService(session, sender, access, entities, _carry, time, visibility, log);
	}

	public void SendTakeRequest(ulong ownerSteamId, ulong itemInstanceId) =>
		_take.SendTakeRequest(ownerSteamId, itemInstanceId);

	public void HandleTakeRequest(ulong sender, PlayerInventoryTakeRequestMsg msg) =>
		_take.HandleTakeRequest(sender, msg);

	public void SendRemoteInventoryOperation(RemoteInventoryOperationRequestMsg msg) =>
		_remoteInventory.SendRemoteInventoryOperation(msg);

	public void HandleRemoteInventoryOperation(ulong sender, RemoteInventoryOperationRequestMsg msg) =>
		_remoteInventory.HandleRemoteInventoryOperation(sender, msg);

	public void FireTransferReceived(PlayerInventoryTransferMsg msg) =>
		_take.FireTransferReceived(msg);

	public void SendCarryStartRequest(ulong targetSteamId) =>
		_carry.SendCarryStartRequest(targetSteamId);

	public void SendPiggybackRequest(ulong targetSteamId) =>
		_carry.SendPiggybackRequest(targetSteamId);

	public void SendCarryOnBackRequest(ulong targetSteamId) =>
		_carry.SendCarryOnBackRequest(targetSteamId);

	public void SendCarryStopRequest(ulong carriedSteamId) =>
		_carry.SendCarryStopRequest(carriedSteamId);

	public void HandleCarryStartRequest(ulong sender, PlayerCarryStartRequestMsg msg) =>
		_carry.HandleCarryStartRequest(sender, msg);

	public void HandleCarryStopRequest(ulong sender, PlayerCarryStopRequestMsg msg) =>
		_carry.HandleCarryStopRequest(sender, msg);

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

	public void SendPushRequest(ulong targetSteamId) =>
		_push.SendPushRequest(targetSteamId);

	public void HandlePushRequest(ulong sender, PlayerPushRequestMsg msg) =>
		_push.HandlePushRequest(sender, msg);

	public void FirePushReceived(PlayerPushResultMsg msg) =>
		_push.FirePushReceived(msg);

	public void Dispose()
	{
		_carry.Dispose();
		_carryKernelProjection.Dispose();
		_resultProjection.Dispose();
		_push.Dispose();
	}
}
