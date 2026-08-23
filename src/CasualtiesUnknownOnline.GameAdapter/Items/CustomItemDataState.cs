using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The persistent multiplayer state hidden inside <c>CustomItemBehaviour.data</c>.
/// The array itself is an <c>object[]</c> and therefore unsupported by the generic
/// <see cref="ItemStateCodec"/> field codec; the states it carries are a mix of
/// frame-level transients and real gameplay facts. This class gives the gameplay
/// facts an explicit synthetic component-field face so they travel with the
/// existing item-state paths instead of being silently dropped.
///
/// Current state: <c>liquidcentrifuge</c> cooldown — <c>data[0]</c> is a float
/// seconds-remaining gate for the use action (Item.cs:5667-5689). It must ride
/// item state or a transferred/reconnected centrifuge immediately becomes usable
/// again on the receiving side.
/// </summary>
internal static class CustomItemDataState
{
	internal const string LiquidCentrifugeItemId = "liquidcentrifuge";
	internal const string CooldownFieldName = "cooldown";

	internal static ComponentFieldMsg? CaptureLiquidCentrifugeCooldown(string itemId, object[]? data)
	{
		if (itemId != LiquidCentrifugeItemId)
		{
			return null;
		}

		// The native Start initializes data[0] to 0f; emit the synthetic field
		// even before Start has run so every capture has the same wire face.
		var value = data is { Length: > 0 } && data[0] is float f ? f : 0f;
		return new ComponentFieldMsg
		{
			Name = CooldownFieldName,
			Kind = SaveableFieldKind.Float,
			FloatValue = value,
		};
	}

	internal static bool IsLiquidCentrifugeCooldownField(string itemId, ComponentFieldMsg field) =>
		itemId == LiquidCentrifugeItemId
			&& field.Name == CooldownFieldName
			&& field.Kind == SaveableFieldKind.Float;

	internal static object[] WithLiquidCentrifugeCooldown(string itemId, object[]? data, float value)
	{
		if (itemId != LiquidCentrifugeItemId)
		{
			return data ?? [];
		}

		if (data is { Length: > 0 })
		{
			data[0] = value;
			return data;
		}

		return new object[] { value };
	}
}
