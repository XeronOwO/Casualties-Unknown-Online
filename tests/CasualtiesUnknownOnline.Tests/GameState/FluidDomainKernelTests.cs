using System;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Fluids;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.GameState;

public class FluidDomainKernelTests
{
	private static readonly RunEpoch Epoch = new(1);
	private static readonly ActorId Host = new(1001);

	[Fact]
	public void UpdateReset_DriveFluidRegionTable()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(Update(kernel, 1, new FluidRegionState(1, 2, 10, 1, 100)).IsAccepted);
		Assert.True(Update(kernel, 2, new FluidRegionState(1, 2, 12, 2, 200)).IsAccepted);

		var region = Assert.Single(kernel.QueryFluids()!.Regions);
		Assert.Equal(12, region.TotalAmount);
		Assert.Equal(2, region.MainType);

		Assert.True(kernel.Execute(
			new ResetFluidsCommand(new OperationId(3), Host, Epoch, AuthorityKind.HostOnly),
			new CommandContext(Epoch, Host)).IsAccepted);
		Assert.Empty(kernel.QueryFluids()!.Regions);
	}

	[Fact]
	public void NegativeTotal_IsRejectedByInvariant()
	{
		var kernel = new GameStateKernel(Epoch);

		var decision = Update(kernel, 1, new FluidRegionState(1, 2, -1, 1, 0));

		Assert.False(decision.IsAccepted);
		Assert.Equal(RejectionReason.InvariantViolation, decision.Rejection!.Reason);
	}

	[Fact]
	public void WireBatchRoundTrip_PreservesFluidRegionEvent()
	{
		var source = new GameStateKernel(Epoch);
		var batch = Update(source, 1, new FluidRegionState(3, 4, 20, 1, 300)).Batch!;

		var restored = KernelWireMapper.FromWireBatch(KernelWireMapper.ToWireBatch(batch), Epoch);

		var @event = Assert.IsType<FluidRegionUpdatedEvent>(Assert.Single(restored.Events));
		Assert.Equal(3, @event.State.ChunkX);
		Assert.Equal(4, @event.State.ChunkY);
		Assert.Equal(20, @event.State.TotalAmount);
	}

	[Fact]
	public void CheckpointSplitAssemble_RoundTripsFluids()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(Update(kernel, 1, new FluidRegionState(1, 2, 5, 1, 10)).IsAccepted);

		var restored = WireCheckpointAssembler.Assemble(WireCheckpointAssembler.Split(kernel.CreateCheckpoint()));

		var region = Assert.Single(restored.Fluids!.Regions);
		Assert.Equal(1, region.ChunkX);
		Assert.Equal(5, region.TotalAmount);
	}

	[Fact]
	public void SaveLoad_RoundTripsFluids()
	{
		var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cuo-fluids-{Guid.NewGuid():N}.bin");
		try
		{
			var authority = new ItemKernelAuthority(NullLogger<ItemKernelAuthority>.Instance);
			Assert.True(authority.TryUpdateFluidRegion(Host.Value, new FluidRegionState(1, 2, 7, 1, 50), out _, out _));

			var store = new KernelSaveFileStore(path, NullLogger<KernelSaveFileStore>.Instance);
			Assert.True(store.Save(authority.CreateCheckpoint()));
			Assert.True(store.TryLoad(out var loaded));

			var region = Assert.Single(loaded.Fluids!.Regions);
			Assert.Equal(1, region.ChunkX);
			Assert.Equal(7, region.TotalAmount);
		}
		finally
		{
			if (System.IO.File.Exists(path))
			{
				System.IO.File.Delete(path);
			}
		}
	}

	private static Decision Update(GameStateKernel kernel, ulong op, FluidRegionState state) =>
		kernel.Execute(
			new UpdateFluidRegionCommand(new OperationId(op), Host, Epoch, AuthorityKind.HostOnly, state),
			new CommandContext(Epoch, Host));
}
