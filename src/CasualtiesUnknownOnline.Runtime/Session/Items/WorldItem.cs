using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// One entry in the authoritative world-item table: a runtime-generated item
/// currently in the world (not inside a player's inventory — those live in the
/// character data domain). Item carries the full item state (the
/// character-save shape) so any receiver can materialize the object exactly.
/// ParentItemId is 0 for items lying free in the world, else the instance id
/// of the containing world container item.
/// </summary>
public readonly record struct WorldItem(ulong ItemId, CharacterItemMsg Item, NetVector2 Pos, NetVector2 Vel,
	ulong ParentItemId, float Rotation, bool FreshItemDrop, NetVector2 ParentPosition = default, float AngularVelocity = 0f)
{
	public ItemSnapshotEntryMsg ToSnapshotEntryMsg() => new()
	{
		ItemId = ItemId,
		Item = Item,
		Position = Pos.ToNetVector2Msg(),
		Velocity = Vel.ToNetVector2Msg(),
		ParentItemId = ParentItemId,
		Rotation = Rotation,
		FreshItemDrop = FreshItemDrop,
		ParentPosition = ParentPosition.ToNetVector2Msg(),
		AngularVelocity = AngularVelocity,
	};
}
