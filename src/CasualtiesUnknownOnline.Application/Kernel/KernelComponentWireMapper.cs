using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Wire;

namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// The component-field wire vocabulary: ONE spelling of the
/// <see cref="ItemComponentState"/> conversion, shared by the item-data mapping
/// and the limb mapping on both sides of the layer boundary. The Runtime used to
/// carry its own copy of this conversion beside the kernel mapper's, so a field
/// added to the kernel component had two places to land in and one of them could
/// be forgotten; the kernel mapper, the limb vocabulary and the Runtime's
/// player-interaction mapper all call this one.
/// </summary>
public static class KernelComponentWireMapper
{
	public static WireComponentState ToWire(ItemComponentState component) =>
		new()
		{
			TypeName = component.TypeName,
			Fields = [.. component.Fields.Select(ToWire)],
		};

	public static ItemComponentState FromWire(WireComponentState component) =>
		new(
			component.TypeName,
			[.. component.Fields.Select(FromWire)]);

	private static WireComponentField ToWire(ItemComponentField field) =>
		new()
		{
			Name = field.Name,
			Kind = (int)field.Kind,
			FloatValue = field.FloatValue,
			IntValue = field.IntValue,
			BoolValue = field.BoolValue,
			StringValue = field.StringValue,
			StringList = [.. field.StringList],
		};

	private static ItemComponentField FromWire(WireComponentField field) =>
		new(
			field.Name,
			(ItemComponentFieldKind)field.Kind,
			field.FloatValue,
			field.IntValue,
			field.BoolValue,
			field.StringValue,
			field.StringList);
}
