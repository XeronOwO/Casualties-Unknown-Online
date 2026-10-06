using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The snapshot-divergence monitor's contract, exercised reflectively (the adapter is
/// compile-excluded from the test project) through the two states a remote-driven container
/// move can leave behind. Ticket <c>backlog/todo/container-move-snapshot-only-sync.md</c> read
/// the Warn on every remote container move of batch `20261005-c`: the move was real and the
/// event was missing, so the fix belongs on the carrier side and the monitor keeps its rule.
///
/// <para>
/// Both halves are pinned here. With the move announced by its carried-fact event the next
/// snapshot is quiet — that is the state the carriers must produce, and the monitor is what
/// proves it arrived. Without the event the snapshot still warns with the two exact wordings
/// the batch observed, so "quiet the monitor" can never be the answer to a missed event.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class CloneFactTableDivergenceMonitorTests
{
	private static readonly Type CloneFactTable = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.CloneFactTable",
		throwOnError: true)!;

	private const ulong Owner = 4001;
	private const ulong TrashBagId = 1147077248963;
	private const ulong DogFoodId = 1155667183555;

	[Fact]
	public void AContainerMoveAnnouncedByItsEvent_LeavesTheNextSnapshotUnwarned()
	{
		var recorder = new RecordingLoggerFactory();
		var table = CreateTable(recorder);
		Invoke(table, "ApplySnapshot", Owner, Snapshot(Bag([]), TopLevel(DogFoodId, "dogfood")));

		// The carrier the fix restores: the owner's own container-content event, the same one a
		// local drag emits, applied before the periodic snapshot arrives.
		Invoke(table, "ApplyCarriedSync", Owner, Bag([Nested(DogFoodId, "dogfood")]), true);

		var carried = CloneData(table)[Owner];
		Assert.Equal(DogFoodId, Assert.Single(Assert.Single(carried.Items).Contents).InstanceId);

		Invoke(table, "ApplySnapshot", Owner, Snapshot(Bag([Nested(DogFoodId, "dogfood")])));

		Assert.Empty(recorder.Messages(LogLevel.Warning, string.Empty));
	}

	[Fact]
	public void AContainerMoveTheEventChainMissed_WarnsOnTheSnapshot()
	{
		var recorder = new RecordingLoggerFactory();
		var table = CreateTable(recorder);
		Invoke(table, "ApplySnapshot", Owner, Snapshot(Bag([]), TopLevel(DogFoodId, "dogfood")));

		// No event: the snapshot is the only carrier, which is the logic gap the monitor exists for.
		Invoke(table, "ApplySnapshot", Owner, Snapshot(Bag([Nested(DogFoodId, "dogfood")])));

		var warnings = recorder.Messages(LogLevel.Warning, string.Empty);
		Assert.Contains(warnings, warning => warning.Contains("nested container contents changed without an event sync", StringComparison.Ordinal));
		Assert.Contains(warnings, warning => warning.Contains("left the inventory without an event sync", StringComparison.Ordinal));
	}

	private static CharacterDataMsg Snapshot(params CharacterItemMsg[] items) =>
		new() { OwnerSteamId = Owner, Items = [.. items] };

	/// <summary>The trash bag as the carried root, whose Contents are the whole point of the comparison.</summary>
	private static CharacterItemMsg Bag(IReadOnlyList<CharacterItemMsg> contents) =>
		new() { InstanceId = TrashBagId, ItemId = "trashbag", SlotIndex = -1, Contents = [.. contents] };

	/// <summary>A top-level carried entry (in a body slot) rather than a nested one.</summary>
	private static CharacterItemMsg TopLevel(ulong id, string itemId) =>
		new() { InstanceId = id, ItemId = itemId, SlotIndex = 3 };

	private static CharacterItemMsg Nested(ulong id, string itemId) =>
		new() { InstanceId = id, ItemId = itemId };

	private static object CreateTable(RecordingLoggerFactory recorder) => Activator.CreateInstance(
		CloneFactTable,
		BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
		null,
		[recorder.CreateLogger("CloneFactTable")],
		null)!;

	private static void Invoke(object table, string name, params object[] args)
	{
		var method = CloneFactTable.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException($"method {name} not found on CloneFactTable");
		method.Invoke(table, args);
	}

	private static IReadOnlyDictionary<ulong, CharacterDataMsg> CloneData(object table)
	{
		var property = CloneFactTable.GetProperty("CloneData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("CloneData property not found on CloneFactTable");
		return (IReadOnlyDictionary<ulong, CharacterDataMsg>)property.GetValue(table)!;
	}
}
