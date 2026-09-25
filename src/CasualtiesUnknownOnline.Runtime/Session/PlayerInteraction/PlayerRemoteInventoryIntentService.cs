using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Host half of the native remote-inventory intents. The viewer's own client
/// produced the intent from the game's drag pipeline; this service decides only
/// what is genuinely the host's: the session permission policy, membership, the
/// ownership fact, the destination body, first-writer-wins arbitration between
/// competing peers, and the forward to the client that owns the real items. It
/// never models inventory contents and never edits a clone of the owner's
/// character data — the owner's native call is the single implementation of the
/// operation, and its own report is the authoritative result.
/// </summary>
internal sealed class PlayerRemoteInventoryIntentService(
	ISessionControl session,
	PacketSender sender,
	PlayerCharacterAccess characters,
	IHostRules hostRules,
	IPlayerInteractionVisibility visibility,
	PlayerItemUseService itemUse,
	PlayerInventoryTakeService take,
	ITimeSource time,
	ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly PlayerCharacterAccess _characters = characters;
	private readonly IHostRules _hostRules = hostRules;
	private readonly IPlayerInteractionVisibility _visibility = visibility;
	private readonly PlayerItemUseService _itemUse = itemUse;
	private readonly PlayerInventoryTakeService _take = take;
	private readonly ITimeSource _time = time;
	private readonly ILogger _log = log;

	/// <summary>One item is being operated on by this requester until the lease expires (first-writer-wins).</summary>
	private readonly RemoteIntentArbitration _arbitration = new();

	/// <summary>A host-validated native intent must be replayed on the owner's own local body.</summary>
	public event Action<RemoteInventoryIntentMsg>? IntentReceived;

	public void FireRemoteInventoryIntentReceived(RemoteInventoryIntentMsg msg) => IntentReceived?.Invoke(msg);

	/// <summary>Viewer side: report one intent captured from the local drag release.</summary>
	public void SendRemoteInventoryIntent(RemoteInventoryIntentMsg msg)
	{
		if (!_session.SessionActive || !_session.LocalInWorld)
		{
			_log.LogDebug("[RemoteIntent] {Kind} not sent: the local player is not in a live session world (active {Active}, in world {InWorld}).",
				msg.Kind, _session.SessionActive, _session.LocalInWorld);
			return;
		}

		// The actor's own client judges whether the target is visible from its
		// own view; the host never judges a peer's sight from streamed positions.
		if (!_visibility.HasLineOfSight(_session.LocalSteamId, msg.OwnerSteamId))
		{
			_log.LogInformation("[RemoteIntent] refused locally {Kind}: {Requester} cannot see {Owner} on this client.",
				msg.Kind, _session.LocalSteamId, msg.OwnerSteamId);
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			HandleRemoteInventoryIntentRequest(_session.LocalSteamId, msg);
			return;
		}

		_sender.Send(_session.HostSteamId, NetMsg.RemoteInventoryIntentRequest, msg);
	}

	/// <summary>Host only: a native inventory intent arrived — the guest→host wire and the host's own client share this path.</summary>
	public void HandleRemoteInventoryIntentRequest(ulong requester, RemoteInventoryIntentMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		var owner = msg.OwnerSteamId;
		if (owner == 0 || owner == requester || msg.ItemInstanceId == 0)
		{
			_log.LogWarning("[RemoteIntent] refused {Kind}: invalid owner/requester/item ({Owner}/{Requester}/{Item}).",
				msg.Kind, owner, requester, msg.ItemInstanceId);
			return;
		}

		if (!_hostRules.AllowRemoteInventoryTake)
		{
			_log.LogInformation("[RemoteIntent] refused {Kind}: host has disabled remote inventory manipulation (AllowRemoteInventoryTake=false).",
				msg.Kind);
			return;
		}

		if (!_characters.IsInWorld(owner) || !_characters.IsInWorld(requester))
		{
			_log.LogWarning("[RemoteIntent] refused {Kind}: {Owner} or {Requester} is not in-world.", msg.Kind, owner, requester);
			return;
		}

		var source = _characters.GetCharacterData(owner);
		if (source is null)
		{
			_log.LogWarning("[RemoteIntent] refused {Kind}: no character snapshot for {Owner}.", msg.Kind, owner);
			return;
		}

		// The ownership fact, not the inventory contents: the item must still be
		// reported as one of the owner's carried items. Where it sits inside that
		// tree is the owner's business, and the owner's native guard re-reads the
		// live scene anyway.
		if (!TryFindItem(source.Items, msg.ItemInstanceId, out var original))
		{
			_log.LogWarning("[RemoteIntent] refused {Kind}: {Owner} has no item instance {Item}.",
				msg.Kind, owner, msg.ItemInstanceId);
			return;
		}

		if (!TryValidateOperands(msg, source, original, requester, out var refusal))
		{
			_log.LogWarning("[RemoteIntent] refused {Kind}: {Reason}", msg.Kind, refusal);
			return;
		}

		if (!_arbitration.TryAdmit(requester, msg.ItemInstanceId, _time.NowMs, out var holder))
		{
			_log.LogInformation("[RemoteIntent] refused {Kind} for item {Item}: first-writer-wins — {Holder} is already operating it.",
				msg.Kind, msg.ItemInstanceId, holder);
			return;
		}

		_log.LogInformation("[RemoteIntent] {Requester} → {Owner} {Kind} (item {Item}, container {Container}, slot {Slot}, body {Body}, limb {Limb}).",
			requester, owner, msg.Kind, msg.ItemInstanceId, msg.TargetContainerInstanceId, msg.TargetSlotIndex, msg.TargetBodySteamId, msg.TargetLimbIndex);

		switch (msg.Kind)
		{
			// The two intents with no single native call behind them keep their
			// landed host-authoritative paths: the cross-player custody transfer
			// moves the item fact between the two character snapshots and tells
			// both participants to apply their half, and the limb application
			// consumes the owner's item for the requester's own body.
			case RemoteInventoryIntentKind.TransferToBody:
				_take.HandleRemoteBackpackTransferToBody(requester, owner, msg.ItemInstanceId, msg.TargetSlotIndex);
				break;
			case RemoteInventoryIntentKind.ApplyToLimb:
				_itemUse.HandleRemoteHeldItemUse(requester, owner, msg.ItemInstanceId, msg.TargetLimbIndex);
				break;
			default:
				Forward(owner, msg);
				break;
		}
	}

	/// <summary>The owner's own client replays the call; a remote owner receives the forwarded intent.</summary>
	private void Forward(ulong owner, RemoteInventoryIntentMsg msg)
	{
		if (owner == _session.LocalSteamId)
		{
			FireRemoteInventoryIntentReceived(msg);
			return;
		}

		_sender.Send(owner, NetMsg.RemoteInventoryIntent, msg);
	}

	/// <summary>The operands each intent kind needs, checked against the owner's own report and the requester identity.</summary>
	private static bool TryValidateOperands(
		RemoteInventoryIntentMsg msg,
		CharacterDataMsg owner,
		CharacterItemMsg item,
		ulong requester,
		out string refusal)
	{
		switch (msg.Kind)
		{
			case RemoteInventoryIntentKind.DropItem:
			case RemoteInventoryIntentKind.DropWearable:
			case RemoteInventoryIntentKind.TakeOutOfContainer:
				refusal = string.Empty;
				return true;
			case RemoteInventoryIntentKind.MoveIntoContainer:
				if (msg.TargetContainerInstanceId == 0 || msg.TargetContainerInstanceId == msg.ItemInstanceId)
				{
					refusal = $"invalid target container {msg.TargetContainerInstanceId}.";
					return false;
				}

				refusal = string.Empty;
				return true;
			case RemoteInventoryIntentKind.PickUpToSlot:
			case RemoteInventoryIntentKind.SwapSlots:
				var slotCount = owner.SlotCount > 0 ? owner.SlotCount : 3;
				if (msg.TargetSlotIndex < 0 || msg.TargetSlotIndex >= slotCount)
				{
					refusal = $"target slot {msg.TargetSlotIndex} is outside the {slotCount}-slot inventory of the owner.";
					return false;
				}

				refusal = string.Empty;
				return true;
			case RemoteInventoryIntentKind.TransferToBody:
				// The host is the only role holding both peer identities: a
				// destination body that is neither the owner nor the requester
				// (a remote-to-remote handoff) is refused here, observably.
				if (msg.TargetBodySteamId != requester)
				{
					refusal = $"destination body {msg.TargetBodySteamId} is neither the owner nor the requester {requester}.";
					return false;
				}

				if (msg.TargetSlotIndex < -1)
				{
					refusal = $"invalid destination slot {msg.TargetSlotIndex}.";
					return false;
				}

				refusal = string.Empty;
				return true;
			case RemoteInventoryIntentKind.ApplyToLimb:
				// -1 is the landed flow's "pick the most injured limb" request.
				if (msg.TargetLimbIndex < -1)
				{
					refusal = $"invalid target limb {msg.TargetLimbIndex}.";
					return false;
				}

				// The landed held-remote-item flow covers carried slot items; a
				// worn item keeps the scope it had before this rework.
				if (item.SlotIndex < 0)
				{
					refusal = $"item {msg.ItemInstanceId} is worn (slot {item.SlotIndex}) — the held-item flow covers carried slot items only.";
					return false;
				}

				refusal = string.Empty;
				return true;
			default:
				refusal = $"unknown intent kind {msg.Kind}.";
				return false;
		}
	}

	private static bool TryFindItem(List<CharacterItemMsg> items, ulong instanceId, out CharacterItemMsg found)
	{
		foreach (var item in items)
		{
			if (item.InstanceId == instanceId)
			{
				found = item;
				return true;
			}

			if (TryFindItem(item.Contents, instanceId, out found))
			{
				return true;
			}
		}

		found = null!;
		return false;
	}
}
