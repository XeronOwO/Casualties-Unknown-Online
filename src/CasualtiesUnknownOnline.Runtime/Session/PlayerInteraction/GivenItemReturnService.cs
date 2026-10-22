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
/// stay — with the giver.
/// <para>
/// Its own class rather than another arm of <see cref="PlayerInventoryTakeService"/>: the take core is the forward
/// custody (validation, the two snapshots, the destination rungs) and it is already at its size limit, while this
/// is one narrow reversal with its own failure answer. Both go through the kernel's transfer record, so a return
/// is a transfer like any other and the two sides converge from it.
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
	/// the host cannot make (the item is gone, or the giver has no free slot) is answered with its reason, and
	/// the caller logs the failed return; the next report converges the two sides from wherever the item is.
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

		if (!TryFindItem(fromData, itemId, out var item) || item is null)
		{
			return $"{receiver} no longer holds item {itemId}, so there is nothing to return.";
		}

		var newFrom = PlayerCharacterAccess.CloneCharacter(fromData);
		var newTo = PlayerCharacterAccess.CloneCharacter(toData);
		RemoveItem(newFrom, itemId);
		var slot = FirstFreeSlot(newTo);
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
			out _,
			out var rejection))
		{
			_log.LogWarning("[Placement] kernel rejected the return {From} -> {To}: {Reason} ({Message}).",
				receiver, giver, rejection!.Reason, rejection.Message);
		}
	}

	/// <summary>The item's row under this body, searched through the containers it holds (a given item may have landed nested).</summary>
	private static bool TryFindItem(CharacterDataMsg data, ulong itemId, out CharacterItemMsg? found)
	{
		for (var i = 0; i < data.Items.Count; i++)
		{
			var item = data.Items[i];
			if (item.InstanceId == itemId)
			{
				found = item;
				return true;
			}

			if (TryFindItem(item, itemId, out found))
			{
				return true;
			}
		}

		found = null;
		return false;
	}

	private static bool TryFindItem(CharacterItemMsg parent, ulong itemId, out CharacterItemMsg? found)
	{
		for (var i = 0; i < parent.Contents.Count; i++)
		{
			var item = parent.Contents[i];
			if (item.InstanceId == itemId)
			{
				found = item;
				return true;
			}

			if (TryFindItem(item, itemId, out found))
			{
				return true;
			}
		}

		found = null;
		return false;
	}

	private static void RemoveItem(CharacterDataMsg data, ulong itemId)
	{
		for (var i = 0; i < data.Items.Count; i++)
		{
			if (data.Items[i].InstanceId == itemId)
			{
				data.Items.RemoveAt(i);
				return;
			}

			if (RemoveItem(data.Items[i], itemId))
			{
				return;
			}
		}
	}

	private static bool RemoveItem(CharacterItemMsg parent, ulong itemId)
	{
		for (var i = 0; i < parent.Contents.Count; i++)
		{
			if (parent.Contents[i].InstanceId == itemId)
			{
				parent.Contents.RemoveAt(i);
				return true;
			}

			if (RemoveItem(parent.Contents[i], itemId))
			{
				return true;
			}
		}

		return false;
	}

	private static int FirstFreeSlot(CharacterDataMsg data)
	{
		var count = data.SlotCount > 0 ? data.SlotCount : 3;
		for (var slot = 0; slot < count; slot++)
		{
			var occupied = false;
			foreach (var item in data.Items)
			{
				if (item.SlotIndex == slot)
				{
					occupied = true;
					break;
				}
			}

			if (!occupied)
			{
				return slot;
			}
		}

		return -1;
	}
}
