using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// Orders the reconnect restore's writes so a guest replaying the journal can always
/// apply them: the kernel's terminal player facts are projected over the snapshot, a
/// newly adopted transport id gets its player row before any batch names it as an item
/// owner, and only then do the item re-key and the item arbitration run. The guest
/// applies batches in commit order and refuses a carried item whose owner it has not
/// learned yet, so the item batch used to stall its replay on the first revision after
/// the reconnect and turn it into an endless range request (2026-11-20, A2).
/// </summary>
public sealed class CharacterReconnectRestore(
	PlayerKernelRestoreProjection playerKernelRestore,
	PlayerKernelLimbProjection playerLimbKernel,
	IItemControl items,
	ILogger<CharacterReconnectRestore> log)
{
	private readonly PlayerKernelRestoreProjection _playerKernelRestore = playerKernelRestore;
	private readonly PlayerKernelLimbProjection _playerLimbKernel = playerLimbKernel;
	private readonly IItemControl _items = items;
	private readonly ILogger<CharacterReconnectRestore> _log = log;

	/// <summary>
	/// Prepare the snapshot for the restore send. <paramref name="adoptedFrom"/> is the
	/// previous transport id when this reconnect adopted that id's saved character; the
	/// item re-key commits under <paramref name="steamId"/>, so that id's player status
	/// batch is committed first.
	/// </summary>
	public void Prepare(ulong steamId, CharacterDataMsg data, ulong? adoptedFrom)
	{
		// The kernel is the authority for terminal player facts; project it over the
		// snapshot before anything reads the snapshot back into the kernel.
		_playerKernelRestore.Apply(steamId, data);

		if (adoptedFrom is { } oldPeer)
		{
			// The adopted transport id is new to the kernel: create its player row from
			// the projected snapshot now, so the re-key is never the first batch a guest
			// replaying the journal meets that names this player.
			_playerLimbKernel.SyncFromCharacterData(steamId, data);
			_items.ReassignOwner(oldPeer, steamId);
		}

		DropItemsAnotherOwnerHolds(steamId, data);
		MergeTransferredItems(steamId, data);
	}

	/// <summary>
	/// The snapshot is the guest's last report, and a reconnect may follow an item
	/// it handed over: the host's take moved the kernel row to the host, and the
	/// transfer-table entry went with the guest's body. The kernel is the authority
	/// for ownership, so drop every snapshot item it records as another owner's
	/// before the restore; an instance id it has never judged is kept (a pickup
	/// moments before the disconnect is exactly what this restore is for). The item
	/// still lives inside its container, so the walk is recursive.
	/// </summary>
	private void DropItemsAnotherOwnerHolds(ulong steamId, CharacterDataMsg data)
	{
		var dropped = new List<ulong>();
		var removed = RemoveItemsAnotherOwnerHolds(steamId, data.Items, dropped);
		if (removed > 0)
		{
			_log.LogInformation("[Character] the restore of {Peer} omits {Count} item(s) the kernel records as another owner's: {Items}.",
				steamId, removed, string.Join(", ", dropped));
		}
	}

	private int RemoveItemsAnotherOwnerHolds(ulong steamId, List<CharacterItemMsg> items, List<ulong> dropped)
	{
		var removed = 0;
		for (var index = items.Count - 1; index >= 0; index--)
		{
			var item = items[index];
			if (item.InstanceId != 0
				&& _items.FindJudgedItem(item.InstanceId) is { } row
				&& IsOwnerKeyed(row.Location)
				&& row.Location.Owner.Value != steamId)
			{
				items.RemoveAt(index);
				dropped.Add(item.InstanceId);
				removed++;
				continue;
			}

			removed += RemoveItemsAnotherOwnerHolds(steamId, item.Contents, dropped);
		}

		return removed;
	}

	private static bool IsOwnerKeyed(ItemLocation location) =>
		location.Kind is ItemLocationKind.Carried or ItemLocationKind.Contained or ItemLocationKind.Applied;

	/// <summary>
	/// Merge the host's authoritative ownership record (the transfer table — what the
	/// arbitration moved into the guest's hands, never overwritten by the guest's own
	/// reports) over the guest's last snapshot, and name both halves of the result: how
	/// many entries updated a node the snapshot already carried, and how many had to be
	/// appended because it carried the id nowhere. The rules live in
	/// <see cref="TransferTableRestoreMerge"/>.
	/// </summary>
	private void MergeTransferredItems(ulong steamId, CharacterDataMsg data)
	{
		var transferred = _items.GetTransferredItems(steamId);
		if (transferred.Count == 0)
		{
			return;
		}

		var outcome = TransferTableRestoreMerge.Apply(data, transferred);
		_log.LogInformation("Merged {Matched} transfer-table items onto the restore of {Peer} ({Appended} appended — the snapshot did not carry them; {Unplaced} unplaceable; {Total} items total).",
			outcome.Matched, steamId, outcome.Appended, outcome.Unplaced, data.Items.Count);
		if (outcome.Unplaced > 0)
		{
			_log.LogWarning("The restore of {Peer} cannot place {Unplaced} transfer-table item(s): the snapshot does not carry them anywhere and their entries state no body placement (a container content's slot is its parent's, and the table cannot state the parent — an item moved into a container after the last 1 Hz report). They are dropped by name, not silently.",
				steamId, outcome.Unplaced);
		}
	}
}
