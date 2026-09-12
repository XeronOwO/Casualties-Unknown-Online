using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The container-sync writer: reconcile one container's authoritative kernel
/// children against a recursive wire/save-shaped container report. Each
/// contained child is its own kernel item — a missing child is spawned, a known
/// one is updated in place, and a child that left the container is destroyed.
///
/// Split out of <see cref="ItemKernelAuthority"/> (the 600-line architecture
/// gate) as a responsibility of its own: walking a reported container tree and
/// writing the difference is a use case OVER the authority's command surface,
/// not part of what the authority owns (the kernel, the run epoch and the
/// operation counter).
/// </summary>
internal static class ItemContainerSyncWriter
{
	internal static void Sync(ItemKernelAuthority authority, ulong actor, ulong parentItemId, CharacterItemMsg parent, ActorId owner)
	{
		var desired = new HashSet<ulong>();
		SyncChildren(authority, actor, parentItemId, parent, owner, desired);

		var stale = authority.QueryItems().Values
			.Where(i => i.Location.Kind == ItemLocationKind.Contained
				&& i.Location.ParentItemId == parentItemId
				&& !desired.Contains(i.Identity.InstanceId))
			.Select(i => i.Identity.InstanceId)
			.ToList();
		foreach (var staleId in stale)
		{
			TryDestroyExternal(authority, actor, staleId, TerminalKind.ReplacedBy);
		}
	}

	private static void SyncChildren(ItemKernelAuthority authority, ulong actor, ulong parentItemId, CharacterItemMsg parent, ActorId owner, HashSet<ulong> desired)
	{
		foreach (var child in parent.Contents)
		{
			if (child.InstanceId == 0)
			{
				continue;
			}

			desired.Add(child.InstanceId);
			var current = authority.FindItem(child.InstanceId);
			if (current is null)
			{
				var location = ItemLocation.Contained(owner, parentItemId);
				TrySpawnExternal(authority, actor, new ItemIdentity(child.InstanceId, child.ItemId), location, child);
			}
			else if (current.Value.Location.Kind == ItemLocationKind.Contained
				&& current.Value.Location.ParentItemId == parentItemId)
			{
				TryUpdateStateExternal(authority, actor, child.InstanceId, child);
			}

			SyncChildren(authority, actor, child.InstanceId, child, owner, desired);
		}
	}

	private static void TrySpawnExternal(ItemKernelAuthority authority, ulong actor, ItemIdentity identity, ItemLocation location, CharacterItemMsg item)
	{
		var command = new SpawnItemCommand(
			authority.NextOperationId(),
			new ActorId(actor),
			authority.CurrentRunEpoch,
			AuthorityKind.OwnerPredictedHostValidated,
			identity,
			location,
			0,
			ItemKernelAuthority.ToKernelData(item));
		authority.TryExecuteCommand(command, actor, out _, out _);
	}

	private static void TryUpdateStateExternal(ItemKernelAuthority authority, ulong actor, ulong itemId, CharacterItemMsg item)
	{
		var current = authority.FindItem(itemId);
		if (current is null)
		{
			return;
		}

		var command = new UpdateItemStateCommand(
			authority.NextOperationId(),
			new ActorId(actor),
			authority.CurrentRunEpoch,
			AuthorityKind.OwnerPredictedHostValidated,
			itemId,
			ItemKernelAuthority.ToKernelData(item),
			current.Value.Revision);
		authority.TryExecuteCommand(command, actor, out _, out _);
	}

	private static void TryDestroyExternal(ItemKernelAuthority authority, ulong actor, ulong itemId, TerminalKind kind)
	{
		var current = authority.FindItem(itemId);
		if (current is null)
		{
			return;
		}

		var command = new DestroyItemCommand(
			authority.NextOperationId(),
			new ActorId(actor),
			authority.CurrentRunEpoch,
			AuthorityKind.HostOnly,
			itemId,
			kind,
			current.Value.Revision);
		authority.TryExecuteCommand(command, actor, out _, out _);
	}
}
