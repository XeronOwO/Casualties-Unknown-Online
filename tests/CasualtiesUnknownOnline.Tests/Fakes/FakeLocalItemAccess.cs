using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Data;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// The local scene behind the item-data write verb, as a test's own dictionary: what THIS
/// client holds, and the states it was handed. It answers in the adapter's own shape — a
/// document is bound by <see cref="FakeItemState"/> and the written capture is re-read from
/// the store — so a command that edited its own tree without writing it back cannot pass a
/// test that only asserts what the scene holds.
/// </summary>
internal sealed class FakeLocalItemAccess : ILocalItemAccess
{
	private readonly Dictionary<ulong, CharacterItemMsg> _held = [];

	/// <summary>What the next write answers, when a test needs a scene-side refusal; cleared once used.</summary>
	internal LocalItemWriteResult? NextResult { get; set; }

	/// <summary>What the pointer names for a <c>cursor</c> target: nothing, an id-less item, or the staged id.</summary>
	internal LocalCursorItemKind Cursor { get; set; }

	/// <summary>The id the pointer names when <see cref="Cursor"/> is <see cref="LocalCursorItemKind.Resolved"/>.</summary>
	internal ulong CursorInstanceId { get; set; }

	/// <summary>Every state this "scene" was asked to write, in order — the capture the adapter re-read.</summary>
	internal List<CharacterItemMsg> Written { get; } = [];

	/// <summary>The item this client holds — the local object, as its own capture.</summary>
	internal void Hold(CharacterItemMsg item) => _held[item.InstanceId] = item;

	/// <summary>What the scene holds now, after any writes.</summary>
	internal CharacterItemMsg? Held(ulong instanceId) =>
		_held.TryGetValue(instanceId, out var item) ? Copy(item) : null;

	/// <summary>The pointer's answer, in the scene's own shape: the id only when the answer is resolved.</summary>
	public LocalCursorItemKind ResolveCursorItem(out ulong instanceId)
	{
		instanceId = Cursor == LocalCursorItemKind.Resolved ? CursorInstanceId : 0;
		return Cursor;
	}

	public CharacterItemMsg? ReadLocalItem(ulong instanceId) =>
		_held.TryGetValue(instanceId, out var item) ? Copy(item) : null;

	public LocalItemWriteOutcome WriteLocalItem(ulong instanceId, SnbtValue desired)
	{
		if (NextResult is { } forced)
		{
			NextResult = null;
			return new LocalItemWriteOutcome(forced, forced == LocalItemWriteResult.Refused ? "the local scene refuses this state." : null, null);
		}

		if (!_held.TryGetValue(instanceId, out var held))
		{
			return new LocalItemWriteOutcome(LocalItemWriteResult.NotHeld, null, null);
		}

		if (!FakeItemState.TryBind(held, desired, out var bound, out var refusal))
		{
			return new LocalItemWriteOutcome(LocalItemWriteResult.Refused, refusal, null);
		}

		var written = Copy(bound);
		Written.Add(Copy(written));
		_held[instanceId] = written;
		return new LocalItemWriteOutcome(
			LocalItemWriteResult.Applied,
			null,
			Copy(written),
			written.Condition == 0f
				? "At 0 the item's own rule may destroy it (destroyAtZeroCondition), so it can be gone a moment from now."
				: null);
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
