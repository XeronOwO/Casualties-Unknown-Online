using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Where a cross-player use's post-item state LANDS — the commit side of
/// <see cref="PlayerItemUseService"/>, split out at the architecture line gate.
/// Every family that changes the item under a use ends here (the wearables it
/// placed, the topical and drink families' committed drain, and the solid-food
/// family's outcome report), so none of them can drift from the others:
/// <list type="bullet">
/// <item>the item's OWNER's authoritative character snapshot, which is what the
/// host restores from and what the peers' clones are rendered from;</item>
/// <item>a GUEST owner's transfer table — the record the host arbitrates future
/// reports against and merges back on a reconnect;</item>
/// <item>the kernel, when the OWNER is the host (its local object is the fact,
/// so the kernel record is what follows it).</item>
/// </list>
/// </summary>
internal sealed class PlayerItemUseCommit(
	ISessionControl session,
	PlayerCharacterAccess characters,
	IItemControl items,
	ItemKernelAuthority kernelAuthority,
	ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly PlayerCharacterAccess _characters = characters;
	private readonly IItemControl _items = items;
	private readonly ItemKernelAuthority _kernelAuthority = kernelAuthority;
	private readonly ILogger _log = log;

	/// <summary>
	/// Both halves in the order the one-shot chain has always used them, for the
	/// caller that has nothing to write in between (the cross-player eat, whose
	/// outcome carries no target-side state at all).
	/// </summary>
	internal void CommitItemAfterUse(ulong owner, ulong itemInstanceId, CharacterItemMsg newItem, bool destroyed, bool movedToTheTarget)
	{
		SaveOwnerSnapshot(owner, itemInstanceId, newItem, destroyed);
		SyncItemRecord(owner, itemInstanceId, newItem, destroyed, movedToTheTarget);
	}

	/// <summary>
	/// The owner's authoritative character snapshot with the post-use item in it.
	/// Split from <see cref="SyncItemRecord"/> because the one-shot chain writes the
	/// TARGET's snapshot between the two and that order is part of its behaviour: the
	/// version that ran both halves in one call moved the target's save after the item
	/// record's sync, which no family had ever done.
	/// </summary>
	internal void SaveOwnerSnapshot(ulong owner, ulong itemInstanceId, CharacterItemMsg newItem, bool destroyed)
	{
		if (_characters.GetCharacterData(owner) is not { } ownerData)
		{
			_log.LogWarning("[ItemUse] cannot commit item {ItemId} — no character snapshot for {Owner}.", itemInstanceId, owner);
			return;
		}

		var newOwnerData = PlayerCharacterAccess.CloneCharacter(ownerData);
		if (destroyed)
		{
			CarriedItemUseTree.Remove(newOwnerData.Items, itemInstanceId);
		}
		else
		{
			CarriedItemUseTree.Replace(newOwnerData.Items, itemInstanceId, newItem);
		}

		_characters.SaveCharacterData(owner, newOwnerData);
	}

	/// <summary>
	/// The item's own record: the guest transfer table (the host arbitrates future
	/// reports against it) or the kernel (the host is the owner). <paramref name="movedToTheTarget"/>
	/// marks the one family whose use MOVES the item instead of consuming it (a
	/// wearable the target now wears): its owner-side record is removed like a
	/// consumed item's, but the kernel must not be told the item was consumed.
	/// </summary>
	internal void SyncItemRecord(ulong owner, ulong itemInstanceId, CharacterItemMsg newItem, bool destroyed, bool movedToTheTarget)
	{
		if (owner != _session.LocalSteamId)
		{
			if (destroyed)
			{
				_items.RemoveTransferredItem(owner, itemInstanceId);
				if (!movedToTheTarget)
				{
					DestroyKernelCarriedItemIfPresent(itemInstanceId);
				}
			}
			else
			{
				_items.UpdateTransferredItem(owner, itemInstanceId, PlayerCharacterAccess.CloneItem(newItem));
			}

			return;
		}

		SyncHostItemAfterUse(itemInstanceId, newItem, destroyed);
	}

	/// <summary>
	/// Make the kernel own a wearable that crossed to the host target. Guest
	/// targets go through the transfer-table adopt path; the host has no
	/// transfer-table row, so this keeps the item kernel authoritative when the
	/// host becomes the owner.
	/// </summary>
	internal void CommitWornItemToHost(ulong itemId, CharacterItemMsg item)
	{
		var current = _kernelAuthority.FindItem(itemId);
		if (current is null)
		{
			_kernelAuthority.TrySpawnCarried(_session.LocalSteamId, itemId, item.ItemId, item, out _, out _);
			return;
		}

		_kernelAuthority.TryTransfer(
			_session.LocalSteamId,
			itemId,
			new ActorId(_session.LocalSteamId),
			item,
			out _,
			out _);
	}

	/// <summary>Remove a guest-owned item from the kernel when a cross-player use consumed it (non-wearable).</summary>
	private void DestroyKernelCarriedItemIfPresent(ulong itemId)
	{
		if (_kernelAuthority.FindItem(itemId) is not null)
		{
			_kernelAuthority.TryDestroy(_session.LocalSteamId, itemId, TerminalKind.Consumed, out _, out _);
		}
	}

	/// <summary>
	/// Keep the item kernel authoritative when the host is the owner: a destroyed
	/// host item is removed from the kernel when known; a surviving host item is
	/// spawned/updated with the post-use state.
	/// </summary>
	private void SyncHostItemAfterUse(ulong itemId, CharacterItemMsg item, bool destroyed)
	{
		var current = _kernelAuthority.FindItem(itemId);
		if (destroyed)
		{
			if (current is not null)
			{
				_kernelAuthority.TryDestroy(_session.LocalSteamId, itemId, TerminalKind.Consumed, out _, out _);
			}

			return;
		}

		if (current is null)
		{
			_kernelAuthority.TrySpawnCarried(_session.LocalSteamId, itemId, item.ItemId, item, out _, out _);
		}
		else
		{
			_kernelAuthority.TryUpdateState(_session.LocalSteamId, itemId, item, out _, out _);
		}
	}
}
