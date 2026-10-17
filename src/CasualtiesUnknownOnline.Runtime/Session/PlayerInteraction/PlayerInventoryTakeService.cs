using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
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
	IItemContainerFit containerFit,
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
	private readonly IItemContainerFit _containerFit = containerFit;
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

		if (!_visibility.HasLineOfSight(_session.LocalSteamId, ownerSteamId))
		{
			_log.LogInformation("[Take] refused locally: {To} cannot see {From} on this client.", _session.LocalSteamId, ownerSteamId);
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
	public void HandleTakeRequest(ulong sender, PlayerInventoryTakeRequestMsg msg) =>
		HandleTakeCore(sender, msg.OwnerSteamId, msg.ItemInstanceId, allowConsciousSource: false);

	/// <summary>
	/// Host-only remote-backpack Tab-transfer path. The remote backpack is an
	/// explicit co-op inventory surface, so transferring an item to the opener's
	/// own inventory is allowed for a conscious remote owner as long as
	/// <c>AllowRemoteInventoryTake</c> is enabled; the Online UI's unconscious-only
	/// take rules are not applied to this dedicated gesture.
	/// </summary>
	public void HandleRemoteBackpackTake(ulong requester, ulong owner, ulong itemId) =>
		HandleTakeCore(requester, owner, itemId, allowConsciousSource: true, requestedSlot: -1);

	/// <summary>
	/// Host-only native-intent half of <c>TransferToBody</c>: the requester's
	/// own R9 slot release becomes one custody transfer whose destination slot
	/// is the slot that release named. The destination body's occupying item is
	/// dropped by the destination's own native apply (R9's first step), so the
	/// host names the slot and falls back to the first free one — observably —
	/// only when the named slot cannot take the item.
	/// </summary>
	public string? HandleRemoteBackpackTransferToBody(ulong requester, ulong owner, ulong itemId, int targetSlot) =>
		HandleTakeCore(requester, owner, itemId, allowConsciousSource: true, requestedSlot: targetSlot);

	/// <summary>
	/// Host-only native-intent half of <c>GiveToBody</c> — the same custody transfer as
	/// <see cref="HandleRemoteBackpackTransferToBody"/> with the two ends swapped: the item belongs to the player who
	/// is giving it away, and it lands on the other player's body, in the slot their release named. Running the one
	/// core the other direction is what makes the two halves agree about custody rather than merely resemble each
	/// other.
	/// </summary>
	public string? HandleRemoteGiveToBody(ulong giver, ulong receiver, ulong itemId, int targetSlot, ulong targetContainerId) =>
		HandleTakeCore(sender: receiver, owner: giver, itemId, allowConsciousSource: true, requestedSlot: targetSlot, requestedContainerId: targetContainerId);

	private string? HandleTakeCore(
		ulong sender,
		ulong owner,
		ulong itemId,
		bool allowConsciousSource,
		int requestedSlot = -1,
		ulong requestedContainerId = 0)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return null; // not this host's business at all — nothing was asked of the requester's gesture
		}

		var from = owner;
		var to = sender;
		if (from == to || from == 0 || to == 0 || itemId == 0)
		{
			return $"malformed transfer (from {from}, to {to}, item id {itemId}).";
		}

		if (!_hostRules.AllowRemoteInventoryTake)
		{
			return "host has disabled cross-player inventory take (rule AllowRemoteInventoryTake=false).";
		}

		if (!_characters.IsInWorld(from) || !_characters.IsInWorld(to))
		{
			return $"{from} or {to} is not in-world.";
		}

		var source = _characters.GetCharacterData(from);
		var target = _characters.GetCharacterData(to);
		if (source is null || target is null)
		{
			return $"no character snapshot for {from}/{to}.";
		}

		// The game's direct-interaction rule (KrokMP-compatible default): a
		// conscious player's inventory is not takeable through the normal
		// Online UI take path. Only an unconscious or dead body can be
		// searched/taken from; the host re-checks the authoritative snapshot.
		// The remote-backpack Tab-transfer path is an explicit exception when
		// the caller selected it.
		if (!allowConsciousSource
			&& (source.Health is not { } health
				|| (health.Conscious && health.Alive)))
		{
			return $"{from} is conscious/alive and not takeable (or has no health snapshot).";
		}

		var newSource = PlayerCharacterAccess.CloneCharacter(source);
		if (!TryFindAndRemove(newSource.Items, itemId, out var original))
		{
			return $"{from} has no item instance {itemId}.";
		}

		if (original.SlotIndex < 0)
		{
			// Worn items are excluded in this slice — the character restore path
			// handles them separately and the Online UI only offers slot items.
			return $"item {original.ItemId} (instance {itemId}) is worn (slot {original.SlotIndex}) — only backpack/hand slot items are takeable in this slice.";
		}

		var newTarget = PlayerCharacterAccess.CloneCharacter(target);
		var transferred = PlayerCharacterAccess.CloneItem(original);

		// The destination is ONE operand: a ring slot names a body slot, and an ITEM shown in the receiver's
		// inventory names that container. A container destination lands INSIDE that item's Contents, and the
		// receiving client loads it there through its own Container rules — so whether it fits is its own
		// decision, never this one's.
		if (requestedContainerId != 0)
		{
			var container = FindCarriedItem(newTarget.Items, requestedContainerId);
			if (container is null)
			{
				return $"{to} carries no item {requestedContainerId} to receive {original.ItemId} (id {itemId}).";
			}

			transferred.SlotIndex = container.SlotIndex; // a container child carries its PARENT's slot
			container.Contents.Add(transferred);
		}
		else
		{
			var targetSlot = ResolveDestinationSlot(target, to, requestedSlot);
			if (targetSlot < 0)
			{
				return $"{to} has no empty inventory slot.";
			}

			transferred.SlotIndex = targetSlot; // the host picks a concrete empty slot; the recipient's immediate re-report confirms it
			newTarget.Items.Add(transferred);
		}

		_characters.SaveCharacterData(from, newSource);
		_characters.SaveCharacterData(to, newTarget);

		// Guest ownership records drive use/slot/drop arbitration and the
		// reconnect restore merge — move the entry with the transfer. A CONTAINER destination makes that entry
		// state "in a container" (-1) rather than a slot: the snapshot's own convention is the parent's slot, but
		// the transfer table and the kernel read -1 as "carried, no slot of its own", and an entry that claimed a
		// slot would put the item in that slot on every clone and in the kernel's own item view — the container and
		// its child competing for one slot.
		var ledger = requestedContainerId != 0 ? PlayerCharacterAccess.CloneItem(transferred) : transferred;
		if (requestedContainerId != 0)
		{
			ledger.SlotIndex = -1;
		}

		if (from != _session.LocalSteamId)
		{
			_items.RemoveTransferredItem(from, itemId);
		}

		if (to != _session.LocalSteamId)
		{
			_items.AdoptTransferredItem(to, itemId, ledger);
		}
		else
		{
			CommitCarriedToHost(itemId, ledger);
		}

		_log.LogInformation("[Take] {To} takes {ItemId} (id {InstanceId}) from {From} (remote-backpack allowConscious={AllowConscious}, container {ContainerId}).",
			to, original.ItemId, itemId, from, allowConsciousSource, requestedContainerId);
		PublishTransfer(new PlayerInventoryTransferMsg
		{
			FromSteamId = from,
			ToSteamId = to,
			Item = transferred,
			TargetParentItemId = requestedContainerId,
			Destination = requestedContainerId != 0 ? PlayerInventoryDestination.Container : PlayerInventoryDestination.BodySlot,
		});

		return null; // applied
	}

	/// <summary>
	/// Host only: deliver a CREATED item to one player — the tail of <see cref="HandleTakeCore"/> with no
	/// source body. The console <c>/give</c> verb creates the fact (definition, instance id and properties)
	/// and calls this; everything from here on is the custody path a cross-player transfer already takes:
	/// the destination's character snapshot, the recipient's ledger entry (or the host's own item
	/// authority), and ONE published transfer whose receiving client applies the rung this chose.
	///
	/// <para>
	/// The destination is the LADDER the user ruled on 2026-10-17: (1) a free slot on the player's own body,
	/// (2) a container the body CARRIES with enough remaining weight, (3) a container the body WEARS, and
	/// (4) a world drop at the player's own position. The operator's named slot is a preference only: an
	/// occupied or out-of-range one falls back to the first free slot, with the substitution logged WHEN a
	/// free slot exists — with none, the ladder's next rungs answer and the delivery's own sentence carries
	/// the truth. The
	/// container rungs are answered by <see cref="IItemContainerFit"/> — the container's weight rule is the
	/// game's own — and the same id travels in the transfer so the receiving client loads the item into THAT
	/// container. A drop is not a carried item: the recipient's snapshot and transfer table are left alone,
	/// and its own client materializes the item into the world where it stands.
	/// </para>
	///
	/// <para>
	/// The published transfer names NO source (<c>FromSteamId = 0</c>) because no body gave the item up:
	/// nothing is removed on any client, and only the recipient's projection fires.
	/// </para>
	/// </summary>
	internal ItemDelivery DeliverCreatedItem(ulong to, CharacterItemMsg item, int requestedSlot = -1)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return ItemDelivery.Refused("This client is not the host of a live session world, so it cannot deliver a created item.");
		}

		if (to == 0 || item.InstanceId == 0)
		{
			return ItemDelivery.Refused($"malformed give (target {to}, item id {item.InstanceId}).");
		}

		if (!_characters.IsInWorld(to))
		{
			return ItemDelivery.Refused($"{to} is not in-world, so /give cannot reach its inventory.");
		}

		var target = _characters.GetCharacterData(to);
		if (target is null)
		{
			return ItemDelivery.Refused($"No character snapshot for {to}, so /give cannot place an item on it.");
		}

		var newTarget = PlayerCharacterAccess.CloneCharacter(target);

		// Rung 1: a free slot on the body (the named one when the operator's preference is usable).
		var slot = ResolveDestinationSlot(target, to, requestedSlot, "[Give]");
		if (slot >= 0)
		{
			item.SlotIndex = slot;
			newTarget.Items.Add(item);
			return Carry(to, newTarget, item, PlayerInventoryDestination.BodySlot, slot, 0, null, requestedSlot);
		}

		// Rungs 2 and 3: a container the body carries, then one it wears. The candidates are top-level
		// snapshot items in that order; which of them is a container and how much room it has is the
		// adapter's answer, because it is the game's own rule.
		var candidates = ContainerCandidates(target);
		if (candidates.Count > 0 && _containerFit.TryFindContainer(candidates, item, out var containerId))
		{
			var container = FindCarriedItem(newTarget.Items, containerId);
			if (container is null)
			{
				// The clone carries the same tree the candidates came from; an id the port answered that is not in
				// it is a broken answer, and the honest response is the ladder's last rung rather than a throw
				// inside a console/network path.
				_log.LogWarning("[Give] the container port answered {ContainerId}, which the recipient's own clone does not carry — answering the drop rung.", containerId);
				return Drop(to, item);
			}

			item.SlotIndex = container.SlotIndex; // a container child carries its PARENT's slot
			container.Contents.Add(item);
			return Carry(to, newTarget, item, PlayerInventoryDestination.Container, container.SlotIndex, containerId, container.ItemId, requestedSlot);
		}

		return Drop(to, item);
	}

	/// <summary>The ladder's last rung: the item is not carried — no snapshot row, no table row — and the recipient's client drops and reports it.</summary>
	private ItemDelivery Drop(ulong to, CharacterItemMsg item)
	{
		item.SlotIndex = -1;
		_log.LogInformation("[Give] {To} receives created {ItemId} (id {InstanceId}) as a world drop at its own position (the ladder's last rung).",
			to, item.ItemId, item.InstanceId);
		PublishTransfer(new PlayerInventoryTransferMsg
		{
			FromSteamId = 0,
			ToSteamId = to,
			Item = item,
			Destination = PlayerInventoryDestination.Drop,
		});
		return new ItemDelivery(true, null, PlayerInventoryDestination.Drop, -1, 0, null);
	}

	/// <summary>
	/// The carried half of the ladder: write the recipient's snapshot, record the ledger entry (or the
	/// host's own authority row) and publish the one transfer that names this rung.
	/// </summary>
	private ItemDelivery Carry(
		ulong to,
		CharacterDataMsg newTarget,
		CharacterItemMsg item,
		PlayerInventoryDestination destination,
		int slot,
		ulong containerId,
		string? containerDefinitionId,
		int requestedSlot)
	{
		_characters.SaveCharacterData(to, newTarget);

		var ledger = PlayerCharacterAccess.CloneItem(item);
		if (destination == PlayerInventoryDestination.Container)
		{
			// A container child carries its parent's slot in the snapshot, while the transfer table and
			// the kernel read -1 as "carried, no slot of its own" — an entry that claimed a slot would put
			// the item in that slot on every clone and in the kernel's own view, competing with the
			// container for one slot.
			ledger.SlotIndex = -1;
		}

		if (to != _session.LocalSteamId)
		{
			_items.AdoptTransferredItem(to, item.InstanceId, ledger);
		}
		else
		{
			CommitCarriedToHost(item.InstanceId, ledger);
		}

		_log.LogInformation(
			"[Give] {To} receives created {ItemId} (id {InstanceId}) {Where} (requested slot {Requested}).",
			to, item.ItemId, item.InstanceId, Describe(destination, slot, containerId), requestedSlot);
		PublishTransfer(new PlayerInventoryTransferMsg
		{
			FromSteamId = 0,
			ToSteamId = to,
			Item = item,
			TargetParentItemId = containerId,
			Destination = destination,
		});

		return new ItemDelivery(true, null, destination, slot, containerId, containerDefinitionId);
	}

	/// <summary>The placement a log line names: a body slot, or the container's id.</summary>
	private static string Describe(PlayerInventoryDestination destination, int slot, ulong containerId) =>
		destination == PlayerInventoryDestination.Container
			? $"inside container {containerId} (parent slot {slot})"
			: $"in slot {slot}";

	/// <summary>
	/// The ladder's candidates in rung order: the body's carried containers first (a slot of their own),
	/// then the worn ones. The adapter filters them to containers that can hold the item; the order is the
	/// ladder's, so the first fit is the rung the answer names.
	/// </summary>
	private static List<CharacterItemMsg> ContainerCandidates(CharacterDataMsg target)
	{
		var candidates = new List<CharacterItemMsg>(target.Items.Count);
		foreach (var item in target.Items)
		{
			if (item.SlotIndex >= 0)
			{
				candidates.Add(item);
			}
		}

		foreach (var item in target.Items)
		{
			if (item.SlotIndex < 0)
			{
				candidates.Add(item);
			}
		}

		return candidates;
	}

	/// <summary>
	/// One item in a character snapshot, found by instance id anywhere in the tree — the destination of a container
	/// hand-over is named by id alone, and whether that item really is a container is the RECEIVING client's native
	/// decision, not this snapshot's.
	/// </summary>
	private static CharacterItemMsg? FindCarriedItem(List<CharacterItemMsg> items, ulong instanceId)
	{
		foreach (var item in items)
		{
			if (item.InstanceId == instanceId)
			{
				return item;
			}

			if (FindCarriedItem(item.Contents, instanceId) is { } nested)
			{
				return nested;
			}
		}

		return null;
	}

	/// <summary>
	/// The destination slot of a custody transfer: the slot the native release
	/// named when the recipient's own report shows it free, otherwise the first
	/// free slot, with the substitution logged — the item must never be lost to
	/// an unusable slot.
	/// </summary>
	private int ResolveDestinationSlot(CharacterDataMsg target, ulong recipient, int requestedSlot, string logTag = "[Take]")
	{
		var count = target.SlotCount > 0 ? target.SlotCount : 3;
		var occupied = false;
		foreach (var item in target.Items)
		{
			if (item.SlotIndex == requestedSlot)
			{
				occupied = true;
				break;
			}
		}

		if (requestedSlot >= 0 && requestedSlot < count && !occupied)
		{
			return requestedSlot;
		}

		var fallback = PlayerCharacterAccess.FirstEmptySlot(target);
		if (requestedSlot >= 0 && fallback >= 0)
		{
			_log.LogInformation("{Tag} destination slot {Slot} is not free for {To}; using slot {Fallback} instead.",
				logTag, requestedSlot, recipient, fallback);
		}

		return fallback;
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
			msg.TargetParentItemId,
			msg.Destination,
			out _,
			out var rejection))
		{
			_log.LogWarning("[Take] kernel result rejected {From} -> {To}: {Reason} ({Message}).",
				msg.FromSteamId, msg.ToSteamId, rejection!.Reason, rejection.Message);
		}
	}
}
