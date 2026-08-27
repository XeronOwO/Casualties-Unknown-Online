using CasualtiesUnknownOnline.GameAdapter.Items;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

public class NativeOperationCoordinatorTests
{
	[Fact]
	public void OneOperation_ProducesExactlyOneObservation()
	{
		var coordinator = new NativeOperationCoordinator(NullLogger<NativeOperationCoordinator>.Instance);
		var handle = coordinator.Begin(NativeOperationKind.ItemDrop, 42, "before");

		coordinator.Observe(handle, "fragment-a");
		coordinator.Observe(handle, "fragment-b");
		var first = coordinator.Complete(handle, "after");

		Assert.NotNull(first);
		Assert.Equal(42ul, first!.Subject);
		Assert.Equal(2, first.Fragments.Count);
		Assert.Equal("after", first.After);

		var second = coordinator.Complete(handle, "after-again");
		Assert.Null(second);
	}

	[Fact]
	public void AbortedOperation_DoesNotProduceObservation()
	{
		var coordinator = new NativeOperationCoordinator(NullLogger<NativeOperationCoordinator>.Instance);
		var handle = coordinator.Begin(NativeOperationKind.ItemUse, 7, "before");

		coordinator.Abort(handle, "scene-left");
		var observation = coordinator.Complete(handle, "after");

		Assert.Null(observation);
	}

	[Fact]
	public void ObserveAfterComplete_IsIgnored()
	{
		var coordinator = new NativeOperationCoordinator(NullLogger<NativeOperationCoordinator>.Instance);
		var handle = coordinator.Begin(NativeOperationKind.ItemSlot, 9, "before");
		var first = coordinator.Complete(handle, "after");

		coordinator.Observe(handle, "late");
		Assert.NotNull(first);
	}

	[Fact]
	public void RemoteApplyBegin_ReturnsDefaultAndNeverCompletes()
	{
		var coordinator = new NativeOperationCoordinator(NullLogger<NativeOperationCoordinator>.Instance);
		var handle = coordinator.Begin(NativeOperationKind.ItemDestroy, 5, "before", remoteApply: true);

		Assert.Equal(0ul, handle.Token);
		Assert.Null(coordinator.Complete(handle, "after"));
	}

	[Fact]
	public void AbortAll_ClearsInFlightOperations()
	{
		var coordinator = new NativeOperationCoordinator(NullLogger<NativeOperationCoordinator>.Instance);
		_ = coordinator.Begin(NativeOperationKind.ItemCook, 1, "before");
		_ = coordinator.Begin(NativeOperationKind.Craft, 2, "before");

		Assert.Equal(2, coordinator.InFlightCount);
		coordinator.AbortAll("run-end");

		Assert.Equal(0, coordinator.InFlightCount);

		// New run re-arms.
		coordinator.ResetForRun();
		var handle = coordinator.Begin(NativeOperationKind.ItemDrop, 3, "before");
		Assert.NotEqual(0ul, handle.Token);
	}
}
