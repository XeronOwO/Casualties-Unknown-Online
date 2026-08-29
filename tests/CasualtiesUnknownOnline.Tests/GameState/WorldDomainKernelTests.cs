using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.GameState;

public class WorldDomainKernelTests
{
	private static readonly RunEpoch Epoch = new(1);
	private static readonly ActorId Host = new(1001);

	[Fact]
	public void StartRun_CommitsRunBaselineAndCheckpoint()
	{
		var kernel = new GameStateKernel(Epoch);
		var decision = Start(kernel, 1, Run());

		Assert.True(decision.IsAccepted);
		var run = kernel.QueryRun();
		Assert.NotNull(run);
		Assert.Equal(42ul, run!.RunId);
		Assert.Equal([1, 2, 3], run.RandomState);
		Assert.Equal(2, run.BiomeDepth);
		Assert.Equal(10, run.TotalTraveled);
		var setting = Assert.Single(run.RunSettings!);
		Assert.Equal("speed", setting.Key);
		Assert.Equal(RunSettingKind.Float, setting.Kind);

		var checkpoint = kernel.CreateCheckpoint();
		var restored = new GameStateKernel(new RunEpoch(99));
		Assert.True(restored.Restore(checkpoint).Success);
		var restoredRun = restored.QueryRun()!;
		Assert.Equal(42ul, restoredRun.RunId);
		Assert.Equal([1, 2, 3], restoredRun.RandomState);
	}

	[Fact]
	public void DuplicateStartRun_IsRejected()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(Start(kernel, 1, Run()).IsAccepted);
		var second = Start(kernel, 2, Run(99));

		Assert.False(second.IsAccepted);
		Assert.Equal(RejectionReason.Conflict, second.Rejection!.Reason);
		Assert.Equal(42ul, kernel.QueryRun()!.RunId);
	}

	[Fact]
	public void AdvanceLayer_RequiresRunAndUpdatesBaseline()
	{
		var beforeRun = new GameStateKernel(Epoch);
		var before = beforeRun.Execute(
			new AdvanceLayerCommand(new OperationId(1), Host, Epoch, AuthorityKind.HostOnly, Run()),
			new CommandContext(Epoch, Host));
		Assert.False(before.IsAccepted);
		Assert.Equal(RejectionReason.UnknownAggregate, before.Rejection!.Reason);

		var kernel = new GameStateKernel(Epoch);
		Assert.True(Start(kernel, 1, Run()).IsAccepted);
		var advanced = kernel.Execute(
			new AdvanceLayerCommand(new OperationId(2), Host, Epoch, AuthorityKind.HostOnly, Run(layerIndex: 1)),
			new CommandContext(Epoch, Host));

		Assert.True(advanced.IsAccepted);
		Assert.Equal(1, kernel.QueryRun()!.LayerIndex);
		Assert.Equal(1, advanced.Batch!.Events.Count);
		Assert.IsType<RunAdvancedEvent>(advanced.Batch.Events[0]);
	}

	[Fact]
	public void Apply_RunStartedBatch_ReplaysRunStateOnGuestKernel()
	{
		var source = new GameStateKernel(Epoch);
		var batch = Start(source, 1, Run()).Batch!;

		var guest = new GameStateKernel(Epoch);
		Assert.True(guest.Apply(batch).Success);

		var run = guest.QueryRun();
		Assert.NotNull(run);
		Assert.Equal(42ul, run!.RunId);
		Assert.Equal([1, 2, 3], run.RandomState);
	}

	[Fact]
	public void WireBatchRoundTrip_PreservesRunStartedEvent()
	{
		var source = new GameStateKernel(Epoch);
		var batch = Start(source, 1, Run()).Batch!;

		var wire = KernelWireMapper.ToWireBatch(batch);
		var restored = KernelWireMapper.FromWireBatch(wire, Epoch);

		Assert.Equal(batch.OperationId.Value, restored.OperationId.Value);
		Assert.Equal(batch.GlobalRevision, restored.GlobalRevision);
		var @event = Assert.IsType<RunStartedEvent>(Assert.Single(restored.Events));
		Assert.Equal(42ul, @event.Run.RunId);
		Assert.Equal([1, 2, 3], @event.Run.RandomState);
	}

	[Fact]
	public void CheckpointSplitAssemble_RoundTripsRunState()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(Start(kernel, 1, Run()).IsAccepted);
		var checkpoint = kernel.CreateCheckpoint();

		var restored = WireCheckpointAssembler.Assemble(WireCheckpointAssembler.Split(checkpoint));

		var run = restored.Run;
		Assert.NotNull(run);
		Assert.Equal(42ul, run!.RunId);
		Assert.Equal([1, 2, 3], run.RandomState);
		Assert.Equal(1ul, restored.GlobalRevision);
	}

	[Fact]
	public void SaveLoad_RoundTripsRunState()
	{
		var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cuo-world-run-{System.Guid.NewGuid():N}.bin");
		try
		{
			var authority = new ItemKernelAuthority(NullLogger<ItemKernelAuthority>.Instance);
			Assert.True(authority.TryStartRun(Host.Value, Run(), out _, out _));
			var checkpoint = authority.CreateCheckpoint();

			var store = new KernelSaveFileStore(path, NullLogger<KernelSaveFileStore>.Instance);
			Assert.True(store.Save(checkpoint));
			Assert.True(store.TryLoad(out var loaded));

			var run = loaded.Run;
			Assert.NotNull(run);
			Assert.Equal(42ul, run!.RunId);
			Assert.Equal([1, 2, 3], run.RandomState);
			Assert.Equal(2, run.BiomeDepth);
			var setting = Assert.Single(run.RunSettings!);
			Assert.Equal("speed", setting.Key);
		}
		finally
		{
			if (System.IO.File.Exists(path))
			{
				System.IO.File.Delete(path);
			}
		}
	}

	private static Decision Start(GameStateKernel kernel, ulong operation, RunState run) =>
		kernel.Execute(
			new StartRunCommand(new OperationId(operation), Host, Epoch, AuthorityKind.HostOnly, run),
			new CommandContext(Epoch, Host));

	private static RunState Run(ulong id = 42, int layerIndex = 0) =>
		new(
			id,
			[1, 2, 3],
			BiomeOverride: 0,
			BiomeDepth: 2,
			TotalTraveled: 10,
			LoadedRun: false,
			[new RunSetting("speed", RunSettingKind.Float, FloatValue: 1.5f)],
			layerIndex);
}
