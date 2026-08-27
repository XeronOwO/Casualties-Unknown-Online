using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The Phase B world-item projection. It is the only code path that writes the
/// legacy <see cref="WorldItemTable"/> projection after an accepted kernel
/// command. Services may read the table as a cache, but they must ask the
/// projection to mutate it.
/// </summary>
internal sealed class ItemProjection(ItemKernelAuthority authority, WorldItemTable worldTable)
{
	private readonly ItemKernelAuthority _authority = authority;
	private readonly WorldItemTable _worldTable = worldTable;

	public bool ApplySpawn(ulong actor, ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, float rotation, bool freshItemDrop, float angularVelocity)
	{
		if (!_authority.TrySpawn(actor, new ItemIdentity(itemId, item.ItemId), ItemLocation.World(pos.X, pos.Y), item, out _, out _))
		{
			return false;
		}

		_worldTable.Set(itemId, new WorldItem(itemId, item, pos, vel, 0, rotation, freshItemDrop, AngularVelocity: angularVelocity));
		return true;
	}

	public bool ApplyPickup(ulong actor, ulong itemId)
	{
		if (!_authority.TryPickup(actor, itemId, new ActorId(actor), out _, out _))
		{
			return false;
		}

		_worldTable.Remove(itemId);
		return true;
	}

	public bool ApplyDrop(ulong actor, ulong itemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, ulong parentItemId, float rotation, float angularVelocity, NetVector2 parentPos)
	{
		if (!_authority.TryDrop(actor, itemId, ItemLocation.World(pos.X, pos.Y, parentItemId), item, out _, out var rejection))
		{
			// A drop of an item the kernel has never seen (e.g. a guest's
			// starting-supply carried item) is a native observation: it enters
			// World directly. This is the accept-first authority model.
			if (rejection?.Reason == RejectionReason.UnknownAggregate
				&& _authority.TrySpawn(actor, new ItemIdentity(itemId, item.ItemId), ItemLocation.World(pos.X, pos.Y, parentItemId), item, out _, out _))
			{
				_worldTable.Set(itemId, new WorldItem(itemId, item, pos, vel, parentItemId, rotation, false, parentPos, angularVelocity));
				return true;
			}

			return false;
		}

		_worldTable.Set(itemId, new WorldItem(itemId, item, pos, vel, parentItemId, rotation, false, parentPos, angularVelocity));
		return true;
	}

	public bool ApplyDestroy(ulong actor, ulong itemId, TerminalKind kind = TerminalKind.Destroyed)
	{
		if (!_authority.TryDestroy(actor, itemId, kind, out _, out _))
		{
			return false;
		}

		_worldTable.Remove(itemId);
		return true;
	}

	public bool ApplyCooked(ulong actor, ulong sourceItemId, ulong cookedItemId, CharacterItemMsg item, NetVector2 pos, NetVector2 vel, float rotation, float angularVelocity)
	{
		// The source may not be in the kernel yet (a host cooker report is a
		// native observation; the source fact may have been created before the
		// authority started tracking it). Accept-first: ignore an unknown source
		// and still publish the cooked item.
		_authority.TryDestroy(actor, sourceItemId, TerminalKind.ReplacedBy, out _, out _);

		if (!_authority.TrySpawn(actor, new ItemIdentity(cookedItemId, item.ItemId), ItemLocation.World(pos.X, pos.Y), item, out _, out _))
		{
			return false;
		}

		_worldTable.Remove(sourceItemId);
		_worldTable.Set(cookedItemId, new WorldItem(cookedItemId, item, pos, vel, 0, rotation, false, AngularVelocity: angularVelocity));
		return true;
	}

	public bool ApplyRegisterIfAbsent(ulong actor, WorldItem item)
	{
		if (_worldTable.ContainsKey(item.ItemId))
		{
			return false;
		}

		if (!_authority.TrySpawn(actor, new ItemIdentity(item.ItemId, item.Item.ItemId), ItemLocation.World(item.Pos.X, item.Pos.Y, item.ParentItemId), item.Item, out _, out _))
		{
			return false;
		}

		_worldTable.Set(item.ItemId, item);
		return true;
	}

	public bool ApplyUpdateState(ulong actor, ulong itemId, CharacterItemMsg item)
	{
		if (!_authority.TryUpdateState(actor, itemId, item, out _, out _))
		{
			return false;
		}

		if (_worldTable.TryGetValue(itemId, out var existing))
		{
			_worldTable.Set(itemId, existing with
			{
				Item = item,
			});
		}

		return true;
	}

	public bool ApplyTransfer(ulong actor, ulong itemId, ActorId newOwner, CharacterItemMsg? item)
	{
		if (!_authority.TryTransfer(actor, itemId, newOwner, item, out _, out _))
		{
			return false;
		}

		return true;
	}

	/// <summary>High-frequency position/condition refresh on the world projection. This is a stream-facing projection write, not an authority command.</summary>
	public void ApplyRefresh(ulong itemId, NetVector2 pos, NetVector2 vel, float rotation, float condition)
	{
		if (!_worldTable.TryGetValue(itemId, out var w))
		{
			return;
		}

		w.Item.Condition = condition;
		_worldTable.Set(itemId, w with { Pos = pos, Vel = vel, Rotation = rotation });
	}

	/// <summary>Clear the world projection (run/layer reset). The authority is reset separately.</summary>
	public void Clear() => _worldTable.Clear();
}
