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

	/// <summary>
	/// Refuse an intent AND answer the client that performed it. Every refusal on this path answers, because a
	/// gesture whose only referee is the host leaves the actor's screen identical to one that silently did nothing.
	/// The reason is the SAME sentence the host logs — two wordings for one refusal is how the two ends stop
	/// agreeing — and a refusal of the host's OWN gesture sends nothing: that client already printed it.
	/// </summary>
	private void Refuse(RemoteInventoryIntentMsg msg, ulong requester, string reason)
	{
		_log.LogWarning("[RemoteIntent] refused {Kind}: {Reason}", msg.Kind, reason);
		if (requester == _session.LocalSteamId)
		{
			return;
		}

		_sender.Send(requester, NetMsg.InventoryIntentRefused, new PlayerInventoryIntentRefusedMsg
		{
			Kind = (int)msg.Kind,
			ItemInstanceId = msg.ItemInstanceId,
			Reason = reason,
		});
	}

	/// <summary>Host only: a native inventory intent arrived — the guest→host wire and the host's own client share this path.</summary>
	public void HandleRemoteInventoryIntentRequest(ulong requester, RemoteInventoryIntentMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		var owner = msg.OwnerSteamId;

		// Whether the owner is the player being operated ON or the player DOING the operating is a property of the
		// direction, not of the family: every kind here operates on another player's inventory, except GiveToBody,
		// where the sender gives their OWN item away and the destination body names the other player. Scoping the
		// guard by direction rather than dropping it keeps the rule it exists for — a client may not name itself as
		// the owner of an item it does not carry — while letting the mirror direction through.
		var ownerIsTheSender = msg.Kind == RemoteInventoryIntentKind.GiveToBody;
		if (owner == 0 || msg.ItemInstanceId == 0 || (!ownerIsTheSender && owner == requester))
		{
			_log.LogWarning("[RemoteIntent] refused {Kind}: invalid owner/requester/item ({Owner}/{Requester}/{Item}).",
				msg.Kind, owner, requester, msg.ItemInstanceId);
			return;
		}

		if (!_hostRules.AllowRemoteInventoryTake)
		{
			Refuse(msg, requester, "host has disabled remote inventory manipulation (AllowRemoteInventoryTake=false).");
			return;
		}

		// Both ends of the move have to be in the world: for the take direction that is the item's owner and the
		// requester, and for the give direction it is the sender and the DESTINATION body — which is a different
		// player from the requester, so it is checked separately rather than assumed.
		if (!_characters.IsInWorld(owner) || !_characters.IsInWorld(requester)
			|| (ownerIsTheSender && !_characters.IsInWorld(msg.TargetBodySteamId)))
		{
			Refuse(msg, requester, $"{owner}, {requester} or the destination body {msg.TargetBodySteamId} is not in-world.");
			return;
		}

		var source = _characters.GetCharacterData(owner);
		if (source is null)
		{
			Refuse(msg, requester, $"no character snapshot for {owner}.");
			return;
		}

		// The ownership fact, not the inventory contents: the item must still be
		// reported as one of the owner's carried items. Where it sits inside that
		// tree is the owner's business, and the owner's native guard re-reads the
		// live scene anyway.
		if (!TryFindItem(source.Items, msg.ItemInstanceId, out var original))
		{
			Refuse(msg, requester, $"{owner} has no item instance {msg.ItemInstanceId}.");
			return;
		}

		if (!TryValidateOperands(msg, source, original, requester, out var refusal))
		{
			Refuse(msg, requester, refusal);
			return;
		}

		if (!_arbitration.TryAdmit(requester, msg.ItemInstanceId, _time.NowMs, out var holder))
		{
			// A competing continuous intent would write this line on every frame it is
			// held, so it keeps the frequency rule too: Debug for a continuous kind,
			// Information for a discrete one.
			if (msg.Kind.IsContinuousGesture())
			{
				_log.LogDebug("[RemoteIntent] refused {Kind} for item {Item}: first-writer-wins — {Holder} is already operating it.",
					msg.Kind, msg.ItemInstanceId, holder);
			}
			else
			{
				_log.LogInformation("[RemoteIntent] refused {Kind} for item {Item}: first-writer-wins — {Holder} is already operating it.",
					msg.Kind, msg.ItemInstanceId, holder);
			}

			return;
		}

		if (msg.Kind.IsContinuousGesture())
		{
			// The while-dragging drain tick arrives every frame: its accepted path
			// logs at Debug and only a refusal keeps the Information line, which is
			// the level rule for a high-frequency trigger.
			_log.LogDebug("[RemoteIntent] {Requester} → {Owner} {Kind} (item {Item}, amount {Amount}).",
				requester, owner, msg.Kind, msg.ItemInstanceId, msg.Amount);
		}
		else
		{
			_log.LogInformation("[RemoteIntent] {Requester} → {Owner} {Kind} (item {Item}, container {Container}, slot {Slot}, body {Body}, limb {Limb}, target item {TargetItem}, trader {Trader}).",
				requester, owner, msg.Kind, msg.ItemInstanceId, msg.TargetContainerInstanceId, msg.TargetSlotIndex, msg.TargetBodySteamId, msg.TargetLimbIndex, msg.TargetItemInstanceId,
				msg.TargetTraderPosition is { } trader ? $"({trader.X}, {trader.Y})" : "none");
		}

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

			// The same custody transfer read the other way round: the item belongs to the REQUESTER, who is giving
			// it away, and the destination body is the other player's. It runs the identical host path with the two
			// ends swapped, so both directions of one hand-over agree about custody by construction.
			case RemoteInventoryIntentKind.GiveToBody:
				if (_take.HandleRemoteGiveToBody(requester, msg.TargetBodySteamId, msg.ItemInstanceId, msg.TargetSlotIndex, msg.TargetContainerInstanceId) is { } takeRefusal)
				{
					Refuse(msg, requester, takeRefusal);
				}

				break;
			case RemoteInventoryIntentKind.ApplyToLimb:
				_itemUse.HandleRemoteHeldItemUse(requester, owner, msg.ItemInstanceId, msg.TargetLimbIndex, msg.Amount);
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
			case RemoteInventoryIntentKind.MoveContainerChildren:
				// The operand contract is the same for both: the item is the moved
				// item for the first kind and the SOURCE container for the second, and
				// neither may name itself as the destination.
				if (msg.TargetContainerInstanceId == 0 || msg.TargetContainerInstanceId == msg.ItemInstanceId)
				{
					refusal = $"invalid target container {msg.TargetContainerInstanceId}.";
					return false;
				}

				refusal = string.Empty;
				return true;
			case RemoteInventoryIntentKind.Drain:
				// The amount is the drain tick's operand. A non-finite value would
				// poison the owner's liquid stacks through the native call, and a
				// negative one would add liquid; a large finite amount is no more than
				// the gesture can legitimately do over time, so it is not bounded here.
				if (float.IsNaN(msg.Amount) || float.IsInfinity(msg.Amount) || msg.Amount < 0f)
				{
					refusal = $"unusable drain amount {msg.Amount}.";
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
			case RemoteInventoryIntentKind.GiveToBody:
				// The same hand-over read the other way round: the owner IS the requester (checked above, where the
				// item must be one of the owner's carried items) and the destination is the OTHER player. A
				// destination equal to the requester is a hand-over to oneself, refused here observably, the same
				// way the take direction refuses a third body.
				if (msg.TargetBodySteamId == 0 || msg.TargetBodySteamId == requester)
				{
					refusal = $"destination body {msg.TargetBodySteamId} is the requester {requester} itself.";
					return false;
				}

				// The destination is ONE operand, and the user's ruling names both shapes of it: a ring slot of the
				// displayed backpack puts the item on that body, and an ITEM shown in it puts the item INTO that
				// container. Naming both is refused rather than letting one of them silently win. Whether the named
				// item really is a container, and whether it has room, is the RECEIVING client's native decision.
				if (msg.TargetSlotIndex >= 0 && msg.TargetContainerInstanceId != 0)
				{
					refusal = $"ambiguous destination: slot {msg.TargetSlotIndex} and container {msg.TargetContainerInstanceId} were both named.";
					return false;
				}

				if (msg.TargetSlotIndex < 0 && msg.TargetContainerInstanceId == 0)
				{
					refusal = "no destination: neither a body slot nor a container was named.";
					return false;
				}

				// The same self-containment rule the container moves already enforce, kept with the other operand
				// checks rather than in the data path: it needs nothing but the message.
				if (msg.TargetContainerInstanceId != 0 && msg.TargetContainerInstanceId == msg.ItemInstanceId)
				{
					refusal = $"target container {msg.TargetContainerInstanceId} is the item being given.";
					return false;
				}

				// A slot that does not exist is NOT refused here: the host path substitutes the first empty slot and
				// logs the substitution ("destination slot … is not free …; using slot … instead"). A container the
				// receiver does not carry IS refused there, by name, against the receiver's own snapshot — this
				// static check cannot see that snapshot.
				refusal = string.Empty;
				return true;
			case RemoteInventoryIntentKind.ApplyToLimb:
				// -1 is legal AT THIS LAYER — the release names no limb when the wound view is
				// not readable — and whether it may be SERVED is the host chain's decision: the
				// families whose limb IS the action refuse it there (decision 246), while the
				// injection chain's -1 stays a legal auto-select.
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
			case RemoteInventoryIntentKind.UseItem:
			case RemoteInventoryIntentKind.WearItem:
			case RemoteInventoryIntentKind.UnloadBattery:
				// One item operand, and the ownership fact above already named it: the
				// native call reads nothing else the host could check.
				refusal = string.Empty;
				return true;
			case RemoteInventoryIntentKind.CombineItems:
			case RemoteInventoryIntentKind.LoadBattery:
				// The two-item kinds name a second item, and it must be one of the
				// OWNER's own items — the same ownership fact, checked for the second
				// operand. What the native guard then makes of the pair (combineable,
				// a slot that takes that battery) is the owner's business.
				if (msg.TargetItemInstanceId == 0 || msg.TargetItemInstanceId == msg.ItemInstanceId)
				{
					refusal = $"invalid target item {msg.TargetItemInstanceId}.";
					return false;
				}

				if (!TryFindItem(owner.Items, msg.TargetItemInstanceId, out _))
				{
					refusal = $"the owner has no item instance {msg.TargetItemInstanceId}.";
					return false;
				}

				refusal = string.Empty;
				return true;
			case RemoteInventoryIntentKind.GiveToTrader:
				// The trader is an operand because the owner has no trader open: a
				// missing or unusable position would make the owner resolve a trader
				// that is not the one the requester pointed at, so it is refused here.
				if (msg.TargetTraderPosition is not { } trader)
				{
					refusal = "the trader hand-in carries no trader position.";
					return false;
				}

				if (float.IsNaN(trader.X) || float.IsInfinity(trader.X)
					|| float.IsNaN(trader.Y) || float.IsInfinity(trader.Y))
				{
					refusal = $"unusable trader position ({trader.X}, {trader.Y}).";
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
