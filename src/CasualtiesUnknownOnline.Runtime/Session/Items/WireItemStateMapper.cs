using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Converts between the world-item runtime model and the Phase C
/// <see cref="WireWorldItemState"/> state-stream payload. This is a projection
/// seam only: it never touches kernel authority state.
/// </summary>
internal static class WireItemStateMapper
{
	public static WireWorldItemState ToWire(WorldItem item) => new()
	{
		Identity = KernelWireMapper.ToWireIdentity(new ItemIdentity(item.ItemId, item.Item.ItemId)),
		Data = KernelWireMapper.ToWireData(ItemKernelAuthority.ToKernelData(item.Item)),
		X = item.Pos.X,
		Y = item.Pos.Y,
		VelX = item.Vel.X,
		VelY = item.Vel.Y,
		ParentItemId = item.ParentItemId,
		Rotation = item.Rotation,
		FreshItemDrop = item.FreshItemDrop,
		ParentX = item.ParentPosition.X,
		ParentY = item.ParentPosition.Y,
		AngularVelocity = item.AngularVelocity,
	};

	public static WorldItem ToWorldItem(WireWorldItemState state)
	{
		var item = ToCharacterItem(state);
		return new WorldItem(
			state.Identity.InstanceId,
			item,
			new NetVector2(state.X, state.Y),
			new NetVector2(state.VelX, state.VelY),
			state.ParentItemId,
			state.Rotation,
			state.FreshItemDrop,
			new NetVector2(state.ParentX, state.ParentY),
			state.AngularVelocity);
	}

	private static CharacterItemMsg ToCharacterItem(WireWorldItemState state)
	{
		var data = KernelWireMapper.FromWireData(state.Data);
		var identity = KernelWireMapper.FromWireIdentity(state.Identity);
		var itemState = new ItemState(identity, 0, ItemLocation.World(state.X, state.Y, state.ParentItemId))
		{
			Data = data,
		};
		return ItemKernelAuthority.ToCharacterItem(itemState);
	}
}
