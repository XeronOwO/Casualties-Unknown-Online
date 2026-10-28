using System;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.ProjectionHealth;
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
public sealed class PlayerInteractionService : IPlayerInteractionControl, ISessionReset, IDisposable
{
	private readonly PlayerInventoryTakeService _take;
	private readonly RemotePlacementService _placement;
	private readonly ISessionControl _session;
	private readonly PlayerItemGiveService _give;
	private readonly PlayerRemoteInventoryIntentService _remoteInventory;
	private readonly PlayerCarryService _carry;
	private readonly PlayerKernelCarryProjection _carryKernelProjection;
	private readonly PlayerHealService _heal;
	private readonly PlayerItemUseService _itemUse;
	private readonly PlayerPushService _push;
	private readonly PlayerInteractionKernelProjection _resultProjection;
	private readonly IMedicalOperationControl _medicalOperations;
	public IMedicalOperationControl MedicalOperations => _medicalOperations;




	public event Action<PlayerInventoryTransferMsg>? TransferReceived
	{
		add => _take.TransferReceived += value;
		remove => _take.TransferReceived -= value;
	}

	public ConsoleAnswer GiveItem(ulong targetSteamId, string definitionId, int slot, string stateText) =>
		_give.Give(targetSteamId, definitionId, slot, stateText);

	public void HandleGiveRequest(ulong sender, PlayerInventoryGiveRequestMsg msg) =>
		_give.HandleRequest(sender, msg);

	public void FireGiveAnswered(PlayerInventoryGiveResultMsg msg) =>
		_give.FireAnswered(msg);

	public event Action<PlayerInventoryGiveResultMsg>? GiveAnswered
	{
		add => _give.Answered += value;
		remove => _give.Answered -= value;
	}

	public event Action<RemoteInventoryIntentMsg>? RemoteInventoryIntentReceived
	{
		add => _remoteInventory.IntentReceived += value;
		remove => _remoteInventory.IntentReceived -= value;
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

	public void FireUseRefused() => _itemUse.FireUseRefused();

	public void NotifyUseRefused(RefusalReason reason) => _itemUse.NotifyUseRefused(reason);

	public event Action? UseRefused
	{
		add => _itemUse.UseRefused += value;
		remove => _itemUse.UseRefused -= value;
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
		ILimbUseSemantics limbUseSemantics,
		IConsumeSemantics consumeSemantics,
		IWearSemantics wearSemantics,
		ISolidFoodSemantics solidFoodSemantics,
		ISessionNotices notices,
		ILocalizationService localization,
		ITimeSource time,
		ItemKernelAuthority kernelAuthority,
		IMedicalOperationControl medicalOperations,
		ProjectionHealthCoordinator projectionHealth,
		IItemGrantSource itemCreation,
		IItemStateLanguage itemStateLanguage,
		IMemberCommandRules memberCommandRules,
		IItemContainerFit itemContainerFit,

		ILogger<PlayerInteractionService> log,
		IRemotePlacementResolver? placementResolver = null)
	{
		_medicalOperations = medicalOperations;
		_session = session;

		var access = new PlayerCharacterAccess(session, characters);
		var resultAuthority = new PlayerInteractionResultAuthority(kernelAuthority);
		_take = new PlayerInventoryTakeService(session, sender, access, items, hostRules, visibility, kernelAuthority, resultAuthority, itemContainerFit, log);
		var givenReturn = new GivenItemReturnService(session, items, access, _take, resultAuthority, log);
		_placement = new RemotePlacementService(session, sender, _take, givenReturn, placementResolver, time, log);
		session.SessionEnded += ResetSessionState;
		_give = new PlayerItemGiveService(session, sender, _take, itemCreation, itemStateLanguage, memberCommandRules, localization, log);
		_itemUse = new PlayerItemUseService(session, sender, access, items, visibility, limbUseSemantics, consumeSemantics, wearSemantics, solidFoodSemantics, kernelAuthority, resultAuthority, notices, localization, log);
		_itemUse.BindToSession(); // the admitted-eat table is session-scoped
		_remoteInventory = new PlayerRemoteInventoryIntentService(session, sender, access, hostRules, visibility, _itemUse, _take, _placement, time, log);
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
			projectionHealth,
			log);
		_heal = new PlayerHealService(session, sender, access, items, visibility, kernelAuthority, resultAuthority, log);
		_resultProjection = new PlayerInteractionKernelProjection(kernelAuthority, this, session, log);
		_push = new PlayerPushService(session, sender, access, entities, _carry, time, visibility, log);
	}

	public void SendTakeRequest(ulong ownerSteamId, ulong itemInstanceId) =>
		_take.SendTakeRequest(ownerSteamId, itemInstanceId);

	public void HandleTakeRequest(ulong sender, PlayerInventoryTakeRequestMsg msg) =>
		_take.HandleTakeRequest(sender, msg);

	public void SendRemoteInventoryIntent(RemoteInventoryIntentMsg msg) =>
		_remoteInventory.SendRemoteInventoryIntent(msg);

	public void HandleRemoteInventoryIntentRequest(ulong sender, RemoteInventoryIntentMsg msg) =>
		_remoteInventory.HandleRemoteInventoryIntentRequest(sender, msg);

	public void FireRemoteInventoryIntentReceived(RemoteInventoryIntentMsg msg) =>
		_remoteInventory.FireRemoteInventoryIntentReceived(msg);

	public void HandlePlacementQuery(ulong sender, PlayerPlacementQueryMsg msg) =>
		_placement.HandleQuery(sender, msg);

	public void HandlePlacementAnswer(ulong sender, PlayerPlacementAnswerMsg msg) =>
		_placement.HandleAnswer(sender, msg);

	public void SendPlacementOutcome(ulong itemInstanceId, bool applied, int slot, ulong containerInstanceId, string reason) =>
		_placement.SendOutcome(itemInstanceId, applied, slot, containerInstanceId, reason);

	public void HandlePlacementOutcome(ulong sender, PlayerPlacementOutcomeMsg msg) =>
		_placement.HandleOutcome(sender, msg);

	/// <summary>Per frame: the placement service refuses a question whose deadline passed.</summary>
	public void Update() => _placement.Tick();

	/// <summary>The one session-teardown stage: the give's unanswered placement questions die with the world.</summary>
	public void ResetSessionState() => _placement.ResetSessionState();

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

	public void SendHealRequest(ulong targetSteamId, ulong itemInstanceId = 0, int targetLimbIndex = -1) =>
		_heal.SendHealRequest(targetSteamId, itemInstanceId, targetLimbIndex);

	public void HandleHealRequest(ulong sender, PlayerHealRequestMsg msg) =>
		_heal.HandleHealRequest(sender, msg);

	public void FireHealReceived(PlayerHealResultMsg msg) =>
		_heal.FireHealReceived(msg);

	public void SendUseRequest(ulong targetSteamId, ulong itemInstanceId = 0, int targetLimbIndex = -1, float doseMl = 0f) =>
		_itemUse.SendUseRequest(targetSteamId, itemInstanceId, targetLimbIndex, doseMl);

	public void HandleUseRequest(ulong sender, PlayerItemUseRequestMsg msg) =>
		_itemUse.HandleUseRequest(sender, msg);

	public void FireUseReceived(PlayerItemUseResultMsg msg) =>
		_itemUse.FireUseReceived(msg);

	public void SendItemActionOutcome(ulong itemInstanceId, float condition, bool consumed, bool becameLimbComponent = false, string? replacementItemId = null, float replacementCondition = 0f) =>
		_itemUse.SendItemActionOutcome(itemInstanceId, condition, consumed, becameLimbComponent, replacementItemId, replacementCondition);

	public void HandleItemActionOutcome(ulong sender, PlayerItemActionOutcomeMsg msg) =>
		_itemUse.HandleItemActionOutcome(sender, msg);

	public void SendPushRequest(ulong targetSteamId) =>
		_push.SendPushRequest(targetSteamId);

	public void HandlePushRequest(ulong sender, PlayerPushRequestMsg msg) =>
		_push.HandlePushRequest(sender, msg);

	public void FirePushReceived(PlayerPushResultMsg msg) =>
		_push.FirePushReceived(msg);

	public void Dispose()
	{
		_session.SessionEnded -= ResetSessionState;
		_itemUse.Dispose();
		_carry.Dispose();
		_carryKernelProjection.Dispose();
		_resultProjection.Dispose();
		_push.Dispose();
	}
}
