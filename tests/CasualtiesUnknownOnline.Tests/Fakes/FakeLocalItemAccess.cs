using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// The local scene behind the item-data write verb, as a test's own dictionary: what
/// THIS client holds, and the writes it was handed. It answers in the adapter's own
/// shape — a capture that is a COPY, so a command that edited the capture without
/// writing it back cannot pass a test that only asserts what the scene holds.
/// </summary>
internal sealed class FakeLocalItemAccess : ILocalItemAccess
{
	private readonly Dictionary<ulong, CharacterItemMsg> _held = [];

	/// <summary>What the next write answers, when a test needs a scene-side refusal; cleared once used.</summary>
	internal LocalItemWriteResult? NextResult { get; set; }

	/// <summary>Every state this "scene" was asked to write, in order.</summary>
	internal List<CharacterItemMsg> Written { get; } = [];

	/// <summary>The item this client holds — the local object, as its own capture.</summary>
	internal void Hold(CharacterItemMsg item) => _held[item.InstanceId] = item;

	/// <summary>What the scene holds now, after any writes.</summary>
	internal CharacterItemMsg? Held(ulong instanceId) =>
		_held.TryGetValue(instanceId, out var item) ? Copy(item) : null;

	public CharacterItemMsg? ReadLocalItem(ulong instanceId) =>
		_held.TryGetValue(instanceId, out var item) ? Copy(item) : null;

	public LocalItemWriteResult WriteLocalItem(ulong instanceId, CharacterItemMsg desired)
	{
		if (NextResult is { } forced)
		{
			NextResult = null;
			return forced;
		}

		if (!_held.ContainsKey(instanceId))
		{
			return LocalItemWriteResult.NotHeld;
		}

		Written.Add(Copy(desired));
		_held[instanceId] = Copy(desired);
		return LocalItemWriteResult.Applied;
	}

	private static CharacterItemMsg Copy(CharacterItemMsg item) => new()
	{
		InstanceId = item.InstanceId,
		ItemId = item.ItemId,
		Condition = item.Condition,
		SlotIndex = item.SlotIndex,
		Favourited = item.Favourited,
		Liquids = [.. item.Liquids.Select(liquid => new LiquidStackMsg { LiquidId = liquid.LiquidId, Amount = liquid.Amount })],
		Components =
		[
			.. item.Components.Select(component => new ComponentStateMsg
			{
				TypeName = component.TypeName,
				Fields = [.. component.Fields.Select(CopyField)],
			}),
		],
		Contents = [.. item.Contents],
	};

	private static ComponentFieldMsg CopyField(ComponentFieldMsg field) => new()
	{
		Name = field.Name,
		Kind = field.Kind,
		FloatValue = field.FloatValue,
		IntValue = field.IntValue,
		BoolValue = field.BoolValue,
		StringValue = field.StringValue,
		StringList = [.. field.StringList],
	};
}
