using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The reconnect restore's merge (batch 20260930-g): the host's transfer table is a
/// FLAT per-id record — one entry per id, its contents either bare instance ids (a
/// pickup digest) or absent entirely (an entry the kernel rebuilt) — while the guest's
/// snapshot is the recursive shape the restore consumes. Placement is the snapshot's
/// (the same rule the slot already follows), state is the table's, and an id the
/// snapshot carries NESTED is updated where it lies: lifting it beside its container
/// leaves it with its parent's slot, so the restore drops it and the container comes
/// back empty — the dog food that vanished on a reconnect whose table was intact.
/// </summary>
public class TransferTableRestoreMergeTests
{
	private const ulong BagId = 101;
	private const ulong DogFoodId = 102;
	private const ulong LightId = 103;

	private static CharacterItemMsg Item(ulong instanceId, string itemId, int slot, float condition = 1f, List<CharacterItemMsg>? contents = null) => new()
	{
		InstanceId = instanceId,
		ItemId = itemId,
		SlotIndex = slot,
		Condition = condition,
		Contents = contents ?? [],
	};

	private static WorldItem TableEntry(CharacterItemMsg item) =>
		new(item.InstanceId, item, default, default, 0, 0f, false);

	/// <summary>The guest's last report: a trash bag (slot 0) with a dog food nested inside it, and the emergency light (slot 3).</summary>
	private static CharacterDataMsg Snapshot() => new()
	{
		OwnerSteamId = 7,
		Items =
		[
			Item(BagId, "trashbag", slot: 0, condition: 0.9f, contents: [Item(DogFoodId, "dogfood", slot: 0, condition: 0.5f)]),
			Item(LightId, "emergencylight", slot: 3),
		],
	};

	[Fact]
	public void AContainedItemsEntry_UpdatesTheNodeWhereTheSnapshotCarriesIt()
	{
		var data = Snapshot();

		// The table's own shape: the bag's entry states no contents at all, and the dog food
		// has an entry of its own whose slot is the digest default (a content carries none).
		var outcome = TransferTableRestoreMerge.Apply(data,
		[
			TableEntry(Item(BagId, "trashbag", slot: -1, condition: 0.4f)),
			TableEntry(Item(DogFoodId, "dogfood", slot: -1, condition: 0.3f)),
		]);

		Assert.True(data.Items.Count == 2, $"the content must never be lifted beside its container, got {data.Items.Count} top-level item(s)");
		var bag = data.Items[0];
		Assert.True(bag.SlotIndex == 0, $"the snapshot's slot is the owner's local fact, got {bag.SlotIndex}");
		Assert.True(bag.Condition == 0.4f, $"the table's arbitrated state must win, got {bag.Condition}");

		var food = Assert.Single(bag.Contents);
		Assert.True(food.InstanceId == DogFoodId, "the content keeps its own identity");
		Assert.True(food.ItemId == "dogfood", $"the content keeps the definition the snapshot captured, got '{food.ItemId}'");
		Assert.True(food.Condition == 0.3f, $"the content's own entry updates it in place, got {food.Condition}");
		Assert.True(outcome.Matched == 2 && outcome.Appended == 0 && outcome.Unplaced == 0, $"both entries state a node the snapshot carries, got {outcome.Matched} matched / {outcome.Appended} appended / {outcome.Unplaced} unplaced");
	}

	[Fact]
	public void AnEntryTheSnapshotDoesNotCarryAnywhere_IsAppended()
	{
		// The pickup moments before the disconnect: the table is the only record of the id,
		// so the restore still gets it — with the slot the evidence carried.
		var data = Snapshot();

		var outcome = TransferTableRestoreMerge.Apply(data, [TableEntry(Item(999, "bandage", slot: 2, condition: 0.7f))]);

		Assert.True(data.Items.Count == 3, $"an id no snapshot node states must be appended, got {data.Items.Count} top-level item(s)");
		var appended = data.Items[2];
		Assert.True(appended.InstanceId == 999 && appended.SlotIndex == 2, "the appended entry keeps the slot its evidence stated");
		Assert.True(outcome.Matched == 0 && outcome.Appended == 1 && outcome.Unplaced == 0, $"the append must be COUNTED, not passed off as a match, got {outcome.Matched} matched / {outcome.Appended} appended / {outcome.Unplaced} unplaced");
	}

	[Fact]
	public void AnEntryTheSnapshotCannotPlace_IsCountedInsteadOfAppended()
	{
		// A container content has no placement of its own (its slot is its parent's, and a
		// pickup digest states -1). When the snapshot does not carry the id at all — an
		// item moved into a container after the last 1 Hz report — the merge must name the
		// loss instead of appending a node the restore's wearable path would drop while
		// blaming the limb encoding.
		var data = Snapshot();

		var outcome = TransferTableRestoreMerge.Apply(data, [TableEntry(Item(999, "dogfood", slot: -1, condition: 0.7f))]);

		Assert.True(data.Items.Count == 2, $"an unplaceable entry must not be appended, got {data.Items.Count} top-level item(s)");
		Assert.True(outcome.Matched == 0 && outcome.Appended == 0 && outcome.Unplaced == 1, $"the loss must be counted, got {outcome.Matched} matched / {outcome.Appended} appended / {outcome.Unplaced} unplaced");
	}

	[Fact]
	public void AWornItemsEntry_KeepsTheSnapshotsLimbSlotAndTakesTheTablesState()
	{
		var data = new CharacterDataMsg
		{
			OwnerSteamId = 7,
			Items = [Item(55, "headlamp", slot: -3, condition: 1f)],
		};

		TransferTableRestoreMerge.Apply(data, [TableEntry(Item(55, "headlamp", slot: -1, condition: 0.25f))]);

		var worn = Assert.Single(data.Items);
		Assert.True(worn.SlotIndex == -3, $"the limb encoding is the owner's local fact, got {worn.SlotIndex}");
		Assert.True(worn.Condition == 0.25f, $"the table's arbitrated condition must win, got {worn.Condition}");
	}
}
