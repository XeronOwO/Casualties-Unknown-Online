using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The block-break drop chain: the drops of a LOCALLY broken block are
/// registered into the authoritative table (the wire report itself travels
/// inside BlockDamagedMsg — the drops ride the break, never as standalone
/// spawn reports), and a break with drops that was APPLIED (host: the report's
/// break was accepted; guest: the host's accepted relay arrived) materializes
/// every drop. The breaker itself is excluded from the relay — its local drops
/// are the original, already on the ground. Split out of ItemService when the
/// 600-line gate demanded it — the table state stays with ItemService (asked
/// through a narrow registration surface), this domain owns the chain.
/// </summary>
public sealed class BlockDropSync(ISessionControl session, ItemService items)
{
	private readonly ISessionControl _session = session;
	private readonly ItemService _items = items;

	/// <summary>Host/solo: record the drops of a LOCALLY broken block into the
	/// authoritative table. The local drop objects already exist — this only
	/// registers, never materializes. Guests have no table and never call.</summary>
	public void RegisterBlockDrops(IReadOnlyList<BlockDropEntryMsg> drops)
	{
		if (_session.Role == SessionRole.Guest || drops.Count == 0)
		{
			return;
		}

		foreach (var drop in drops)
		{
			_items.RegisterWorldItemIfAbsent(drop.ItemId, ToWorldItem(drop));
		}
	}

	/// <summary>
	/// A break with drops was APPLIED — register (host only) and materialize
	/// every drop.
	/// </summary>
	public void FireBlockDropsReceived(ulong sender, IReadOnlyList<BlockDropEntryMsg> drops)
	{
		if (drops.Count == 0)
		{
			return;
		}

		foreach (var drop in drops)
		{
			if (_session.Role == SessionRole.Host)
			{
				_items.RegisterWorldItemIfAbsent(drop.ItemId, ToWorldItem(drop));
			}

			_items.FireItemSpawned(ToWorldItem(drop));
		}
	}

	private static WorldItem ToWorldItem(BlockDropEntryMsg drop) => new(
		drop.ItemId, drop.Item, drop.Position.ToNetVector2(), drop.Velocity.ToNetVector2(),
		0, drop.Rotation, drop.FreshItemDrop, AngularVelocity: drop.AngularVelocity);
}
