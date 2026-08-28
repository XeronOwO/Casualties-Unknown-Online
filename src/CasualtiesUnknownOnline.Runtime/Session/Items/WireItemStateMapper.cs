using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Converts between the legacy world-item snapshot DTOs and the Phase C
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

	public static WireWorldItemState ToWire(ItemSnapshotEntryMsg entry) => new()
	{
		Identity = new WireItemIdentity { InstanceId = entry.ItemId, DefinitionId = entry.Item.ItemId },
		Data = KernelWireMapper.ToWireData(ItemKernelAuthority.ToKernelData(entry.Item)),
		X = entry.Position.X,
		Y = entry.Position.Y,
		VelX = entry.Velocity.X,
		VelY = entry.Velocity.Y,
		ParentItemId = entry.ParentItemId,
		Rotation = entry.Rotation,
		FreshItemDrop = entry.FreshItemDrop,
		ParentX = entry.ParentPosition?.X ?? 0f,
		ParentY = entry.ParentPosition?.Y ?? 0f,
		AngularVelocity = entry.AngularVelocity,
		SlotIndex = entry.SlotIndex,
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

	public static ItemSnapshotEntryMsg ToSnapshotEntry(WireWorldItemState state)
	{
		var item = ToCharacterItem(state);
		return new ItemSnapshotEntryMsg
		{
			ItemId = state.Identity.InstanceId,
			Item = item,
			Position = new NetVector2Msg(state.X, state.Y),
			Velocity = new NetVector2Msg(state.VelX, state.VelY),
			ParentItemId = state.ParentItemId,
			Rotation = state.Rotation,
			FreshItemDrop = state.FreshItemDrop,
			ParentPosition = new NetVector2Msg(state.ParentX, state.ParentY),
			AngularVelocity = state.AngularVelocity,
			SlotIndex = state.SlotIndex,
		};
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
