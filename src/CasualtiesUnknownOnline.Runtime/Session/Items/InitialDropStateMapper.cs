using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Pure mapping from the initial-drop wire entries to the world-item
/// projection. Block-break and destructive-trap/building-death drops carry the
/// same full transient spawn state (fresh flag, velocity, rotation and angular
/// velocity); one mapper keeps the two families symmetric and prevents the
/// projection from silently zeroing the initial phase again.
/// </summary>
public static class InitialDropStateMapper
{
	public static WorldItem ToWorldItem(BlockDropEntryMsg drop) => new(
		drop.ItemId,
		drop.Item,
		drop.Position.ToNetVector2(),
		drop.Velocity.ToNetVector2(),
		0,
		drop.Rotation,
		drop.FreshItemDrop,
		AngularVelocity: drop.AngularVelocity);

	public static WorldItem ToWorldItem(TrapDropEntryMsg drop) => new(
		drop.ItemId,
		drop.Item,
		drop.Position.ToNetVector2(),
		drop.Velocity.ToNetVector2(),
		0,
		drop.Rotation,
		drop.FreshItemDrop,
		AngularVelocity: drop.AngularVelocity);
}
