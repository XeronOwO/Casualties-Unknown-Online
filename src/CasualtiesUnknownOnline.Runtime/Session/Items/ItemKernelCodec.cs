using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Pure conversions between kernel item state and the wire/save-shaped
/// character-item message. Kept separate from <see cref="ItemKernelAuthority"/>
/// so the authority stays under the architecture line gate.
/// </summary>
public static class ItemKernelCodec
{
	public static CharacterItemMsg ToCharacterItem(ItemState state) =>
		new()
		{
			InstanceId = state.Identity.InstanceId,
			ItemId = state.Identity.DefinitionId,
			Condition = state.Data.Condition,
			SlotIndex = state.Data.SlotIndex,
			Favourited = state.Data.Favourited,
			Liquids = [.. state.Data.Liquids.Select(l => new LiquidStackMsg { LiquidId = l.LiquidId, Amount = l.Amount })],
			Components = [.. state.Data.Components.Select(ToWireComponent)],
		};

	public static ItemData ToKernelData(CharacterItemMsg item) =>
		new(
			item.Condition,
			item.Favourited,
			item.SlotIndex,
			[.. item.Liquids.Select(l => new ItemLiquidStack(l.LiquidId, l.Amount))],
			[.. item.Components.Select(ToKernelComponent)]);

	private static ComponentStateMsg ToWireComponent(ItemComponentState state) =>
		new()
		{
			TypeName = state.TypeName,
			Fields = [.. state.Fields.Select(f => new ComponentFieldMsg
			{
				Name = f.Name,
				Kind = (int)f.Kind,
				FloatValue = f.FloatValue,
				IntValue = f.IntValue,
				BoolValue = f.BoolValue,
				StringValue = f.StringValue,
				StringList = [.. f.StringList],
			})],
		};

	private static ItemComponentState ToKernelComponent(ComponentStateMsg state) =>
		new(
			state.TypeName,
			[.. state.Fields.Select(f => new ItemComponentField(
				f.Name,
				(ItemComponentFieldKind)f.Kind,
				f.FloatValue,
				f.IntValue,
				f.BoolValue,
				f.StringValue,
				f.StringList))]);
}
