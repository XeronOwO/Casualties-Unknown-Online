using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The give's RETURN half — the design's residual (<c>todo/placement-outcome-never-returns.md</c>): the receiving
/// client answered a destination, the host committed it, and the receiver's LIVE body then cannot take it. The
/// receiver refuses to substitute and reports the failure; this service moves the row back to the giver in both
/// snapshots and publishes the return transfer, so the item ends where the user's 2026-10-22 ruling asks it to
/// stay — with the giver. The return carries the <see cref="PlayerInventoryTransferMsg.Returned"/> flag, so the
/// giver's client drops the item at its feet rather than destroying it when its own live body has no room.
/// <para>
/// Its own class rather than another arm of <see cref="PlayerInventoryTakeService"/>: the take core is the forward
/// custody (validation, the two snapshots, the destination rungs) and it is already at its size limit, while this
/// is one narrow reversal with its own failure answer. Both go through the kernel's transfer record, so a return
/// is a transfer like any other and the two sides converge from it. The tree walking and the free-slot search are
/// the take core's own helpers — one implementation of each, not a second copy.
/// </para>
/// </summary>
internal sealed class GivenItemReturnService(
	ISessionControl session,
	IItemControl items,
	PlayerCharacterAccess characters,
	PlayerInventoryTakeService take,
	PlayerInteractionResultAuthority resultAuthority,
	ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly IItemControl _items = items;
	private readonly PlayerCharacterAccess _characters = characters;
	private readonly PlayerInventoryTakeService _take = take;
	private readonly PlayerInteractionResultAuthority _resultAuthority = resultAuthority;
	private readonly ILogger _log = log;

	/// <summary>
	/// Host only: move one handed-over item back from the receiver to the giver and publish the return. A return
	/// the host cannot make (the item is gone, or the giver has no free slot) is answered with its reason; the
	/// caller logs it, and the return does not run. The item is never destroyed by this path: when the giver's
	/// own body has no room, the return flag makes that client drop it at its feet.
	/// </summary>
	internal string? Return(ulong receiver, ulong giver, ulong itemId)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return "the return is not a host action in this session.";
		}

		var fromData = _characters.GetCharacterData(receiver);
		var toData = _characters.GetCharacterData(giver);
		if (fromData is null || toData is null)
		{
			return "the return names a player the host no longer holds.";
		}

		var newFrom = PlayerCharacterAccess.CloneCharacter(fromData);
		var newTo = PlayerCharacterAccess.CloneCharacter(toData);
		if (!PlayerInventoryTakeService.TryFindAndRemove(newFrom.Items, itemId, out var item))
		{
			return $"{receiver} no longer holds item {itemId}, so there is nothing to return.";
		}

		var slot = PlayerCharacterAccess.FirstEmptySlot(newTo);
		if (slot < 0)
		{
			return $"{giver} has no free slot to return item {itemId} to.";
		}

		var returned = PlayerCharacterAccess.CloneItem(item);
		returned.SlotIndex = slot;
		newTo.Items.Add(returned);

		_characters.SaveCharacterData(receiver, newFrom);
		_characters.SaveCharacterData(giver, newTo);

		var ledger = PlayerCharacterAccess.CloneItem(returned);
		if (receiver != _session.LocalSteamId)
		{
			_items.RemoveTransferredItem(receiver, itemId);
		}

		if (giver != _session.LocalSteamId)
		{
			_items.AdoptTransferredItem(giver, itemId, ledger);
		}
		else
		{
			_take.CommitCarriedToHost(itemId, ledger);
		}

		_log.LogWarning("[Placement] returned {ItemId} (id {InstanceId}) to {Giver} after {Receiver}'s exact placement failed.",
			item.ItemId, itemId, giver, receiver);
		PublishReturn(receiver, giver, returned);
		return null;
	}

	private void PublishReturn(ulong receiver, ulong giver, CharacterItemMsg returned)
	{
		if (!_resultAuthority.TryRecordPlayerInventoryTransfer(
			_session.LocalSteamId,
			receiver,
			giver,
			PlayerInteractionKernelCodec.FromCharacterItem(returned),
			targetParentItemId: 0,
			PlayerInventoryDestination.BodySlot,
			exactDestination: false,
			returned: true,
			out _,
			out var rejection))
		{
			_log.LogWarning("[Placement] kernel rejected the return {From} -> {To}: {Reason} ({Message}).",
				receiver, giver, rejection!.Reason, rejection.Message);
		}
	}
}
