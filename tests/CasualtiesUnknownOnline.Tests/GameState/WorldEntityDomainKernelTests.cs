using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.GameState;

public class WorldEntityDomainKernelTests
{
	private static readonly RunEpoch Epoch = new(1);
	private static readonly ActorId Host = new(1001);

	[Fact]
	public void RecordFacts_UpdatesKernelWorldEntityState()
	{
		var kernel = new GameStateKernel(Epoch);

		Assert.True(RecordTrap(kernel, 1, new EntityPosition(1, 2), kind: 1, extra: 7, triggeredAtMs: 100).IsAccepted);
		Assert.True(RecordHealth(kernel, 2, new EntityPosition(3, 4), 12.5f).IsAccepted);
		Assert.True(RecordOpened(kernel, 3, new EntityPosition(5, 6)).IsAccepted);

		var state = kernel.QueryWorldEntities();
		Assert.NotNull(state);
		var trap = Assert.Single(state!.Consumptions);
		Assert.Equal(new EntityPosition(1, 2), trap.Position);
		Assert.Equal(1, trap.Kind);
		Assert.Equal(7, trap.Extra);
		Assert.Equal(100L, trap.TriggeredAtMs);

		var health = Assert.Single(state.BuildingHealth);
		Assert.Equal(new EntityPosition(3, 4), health.Position);
		Assert.Equal(12.5f, health.Health);

		var opened = Assert.Single(state.OpenedEntities);
		Assert.Equal(new EntityPosition(5, 6), opened.Position);
	}

	[Fact]
	public void ResetWorldEntities_ClearsAllFacts()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(RecordTrap(kernel, 1, new EntityPosition(1, 2), 1, 3, 10).IsAccepted);
		Assert.True(RecordHealth(kernel, 2, new EntityPosition(4, 5), 8f).IsAccepted);
		Assert.True(RecordOpened(kernel, 3, new EntityPosition(7, 8)).IsAccepted);
		Assert.True(RecordTrapState(kernel, 4, new EntityPosition(9, 10), 11, TrapPhase.Armed, 0, 100).IsAccepted);

		var decision = kernel.Execute(
			new ResetWorldEntitiesCommand(new OperationId(5), Host, Epoch, AuthorityKind.HostOnly),
			new CommandContext(Epoch, Host));

		Assert.True(decision.IsAccepted);
		var state = kernel.QueryWorldEntities();
		Assert.NotNull(state);
		Assert.Empty(state!.Consumptions);
		Assert.Empty(state.BuildingHealth);
		Assert.Empty(state.OpenedEntities);
		Assert.Empty(state.TrapStates);
	}

	[Fact]
	public void SamePositionTrap_UpsertsInsteadOfDuplicating()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(RecordTrap(kernel, 1, new EntityPosition(1, 2), 1, 7, 100).IsAccepted);
		Assert.True(RecordTrap(kernel, 2, new EntityPosition(1, 2), 2, 9, 200).IsAccepted);

		var trap = Assert.Single(kernel.QueryWorldEntities()!.Consumptions);
		Assert.Equal(2, trap.Kind);
		Assert.Equal(9, trap.Extra);
		Assert.Equal(200L, trap.TriggeredAtMs);
	}

	[Fact]
	public void DestroyedBuilding_RejectsPositiveHealthReport()
	{
		var kernel = new GameStateKernel(Epoch);
		var position = new EntityPosition(3, 4);
		Assert.True(RecordHealth(kernel, 1, position, 0f).IsAccepted);

		var decision = RecordHealth(kernel, 2, position, 10f);

		Assert.False(decision.IsAccepted);
		Assert.Equal(RejectionReason.InvalidTransition, decision.Rejection!.Reason);
	}

	[Fact]
	public void DestroyedBuilding_AllowsIdempotentZeroHealthReport()
	{
		var kernel = new GameStateKernel(Epoch);
		var position = new EntityPosition(3, 4);
		Assert.True(RecordHealth(kernel, 1, position, 0f).IsAccepted);

		var decision = RecordHealth(kernel, 2, position, 0f);

		Assert.True(decision.IsAccepted);
		Assert.Single(kernel.QueryWorldEntities()!.BuildingHealth);
	}

	[Fact]
	public void SamePositionOpened_IsIdempotent()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(RecordOpened(kernel, 1, new EntityPosition(1, 2)).IsAccepted);
		Assert.True(RecordOpened(kernel, 2, new EntityPosition(1, 2)).IsAccepted);

		Assert.Single(kernel.QueryWorldEntities()!.OpenedEntities);
	}

	[Fact]
	public void RecordTrapState_UpdatesKernelWorldEntityState()
	{
		var kernel = new GameStateKernel(Epoch);
		var position = new EntityPosition(1, 2);

		Assert.True(RecordTrapState(kernel, 1, position, kind: 9, TrapPhase.Armed, extra: 0, ms: 10).IsAccepted);
		Assert.True(RecordTrapState(kernel, 2, position, kind: 9, TrapPhase.Triggered, extra: 3, ms: 20).IsAccepted);

		var state = kernel.QueryWorldEntities();
		Assert.NotNull(state);
		var trapState = Assert.Single(state!.TrapStates);
		Assert.Equal(position, trapState.Position);
		Assert.Equal(9, trapState.Kind);
		Assert.Equal(TrapPhase.Triggered, trapState.Phase);
		Assert.Equal(3, trapState.Extra);
		Assert.Equal(20L, trapState.TransitionedAtMs);
	}

	[Fact]
	public void IllegalTrapStateTransition_IsRejected()
	{
		var kernel = new GameStateKernel(Epoch);
		var position = new EntityPosition(3, 4);
		Assert.True(RecordTrapState(kernel, 1, position, 5, TrapPhase.Armed, 0, 10).IsAccepted);

		var decision = RecordTrapState(kernel, 2, position, 5, TrapPhase.Cooldown, 0, 20);

		Assert.False(decision.IsAccepted);
		Assert.Equal(RejectionReason.InvalidTransition, decision.Rejection!.Reason);
	}

	[Fact]
	public void DisabledTrapState_IsTerminal()
	{
		var kernel = new GameStateKernel(Epoch);
		var position = new EntityPosition(5, 6);
		Assert.True(RecordTrapState(kernel, 1, position, 7, TrapPhase.Disabled, 0, 10).IsAccepted);

		var decision = RecordTrapState(kernel, 2, position, 7, TrapPhase.Armed, 0, 20);

		Assert.False(decision.IsAccepted);
		Assert.Equal(RejectionReason.InvalidTransition, decision.Rejection!.Reason);
	}

	[Fact]
	public void WireBatchRoundTrip_PreservesTrapStateChangedEvent()
	{
		var source = new GameStateKernel(Epoch);
		var batch = RecordTrapState(source, 1, new EntityPosition(2, 3), 4, TrapPhase.Warning, 6, 700).Batch!;

		var restored = KernelWireMapper.FromWireBatch(KernelWireMapper.ToWireBatch(batch), Epoch);

		var @event = Assert.IsType<TrapStateChangedEvent>(Assert.Single(restored.Events));
		Assert.Equal(new EntityPosition(2, 3), @event.Position);
		Assert.Equal(4, @event.Kind);
		Assert.Equal(TrapPhase.Warning, @event.Phase);
		Assert.Equal(6, @event.Extra);
		Assert.Equal(700L, @event.TransitionedAtMs);
	}

	[Fact]
	public void WireCommandRoundTrip_BuildsRecordTrapStateCommand()
	{
		var header = new EnvelopeHeader
		{
			RunEpoch = Epoch.Value,
			SenderId = 1001,
			OperationId = 9,
		};
		var wire = new WireCommand
		{
			Kind = WireCommandKind.RecordTrapState,
			EntityPosition = new WireEntityPosition { X = 4, Y = 5 },
			EntityKind = 6,
			TrapPhase = (int)TrapPhase.Triggered,
			Extra = 2,
			TriggeredAtMs = 1234,
		};

		var command = Assert.IsType<RecordTrapStateCommand>(KernelWireMapper.FromWireCommand(wire, header));

		Assert.Equal(new EntityPosition(4, 5), command.Position);
		Assert.Equal(6, command.Kind);
		Assert.Equal(TrapPhase.Triggered, command.Phase);
		Assert.Equal(2, command.Extra);
		Assert.Equal(1234L, command.TransitionedAtMs);
	}

	[Fact]
	public void Apply_RunEntityBatch_ReplaysFactsOnGuestKernel()
	{
		var source = new GameStateKernel(Epoch);
		var batch = RecordHealth(source, 1, new EntityPosition(5, 6), 99f).Batch!;

		var guest = new GameStateKernel(Epoch);
		Assert.True(guest.Apply(batch).Success);

		var health = Assert.Single(guest.QueryWorldEntities()!.BuildingHealth);
		Assert.Equal(99f, health.Health);
	}

	[Fact]
	public void WireBatchRoundTrip_PreservesTrapConsumedEvent()
	{
		var source = new GameStateKernel(Epoch);
		var batch = RecordTrap(source, 1, new EntityPosition(2, 3), 4, 5, 600).Batch!;

		var restored = KernelWireMapper.FromWireBatch(KernelWireMapper.ToWireBatch(batch), Epoch);

		var @event = Assert.IsType<TrapConsumedEvent>(Assert.Single(restored.Events));
		Assert.Equal(new EntityPosition(2, 3), @event.Position);
		Assert.Equal(4, @event.Kind);
		Assert.Equal(5, @event.Extra);
		Assert.Equal(600L, @event.TriggeredAtMs);
	}

	[Fact]
	public void CheckpointSplitAssemble_RoundTripsWorldEntityState()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(RecordTrap(kernel, 1, new EntityPosition(1, 2), 1, 3, 10).IsAccepted);
		Assert.True(RecordHealth(kernel, 2, new EntityPosition(4, 5), 8f).IsAccepted);
		Assert.True(RecordOpened(kernel, 3, new EntityPosition(7, 8)).IsAccepted);
		Assert.True(RecordTrapState(kernel, 4, new EntityPosition(9, 10), 11, TrapPhase.Warning, 4, 500).IsAccepted);

		var restored = WireCheckpointAssembler.Assemble(WireCheckpointAssembler.Split(kernel.CreateCheckpoint()));

		var state = restored.WorldEntities;
		Assert.NotNull(state);
		Assert.Single(state!.Consumptions);
		Assert.Single(state.BuildingHealth);
		Assert.Single(state.OpenedEntities);
		Assert.Single(state.TrapStates);
		Assert.Equal(TrapPhase.Warning, state.TrapStates[0].Phase);
	}

	[Fact]
	public void SaveLoad_RoundTripsWorldEntityState()
	{
		var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cuo-world-entities-{System.Guid.NewGuid():N}.bin");
		try
		{
			var kernel = new GameStateKernel(Epoch);
			Assert.True(RecordTrap(kernel, 1, new EntityPosition(1, 2), 1, 3, 10).IsAccepted);
			Assert.True(RecordHealth(kernel, 2, new EntityPosition(4, 5), 8f).IsAccepted);
			Assert.True(RecordOpened(kernel, 3, new EntityPosition(7, 8)).IsAccepted);
			Assert.True(RecordTrapState(kernel, 4, new EntityPosition(9, 10), 11, TrapPhase.Cooldown, 5, 600).IsAccepted);

			var store = new KernelSaveFileStore(path, NullLogger<KernelSaveFileStore>.Instance);
			Assert.True(store.Save(kernel.CreateCheckpoint()));
			Assert.True(store.TryLoad(out var loaded));

			var state = loaded.WorldEntities;
			Assert.NotNull(state);
			Assert.Single(state!.Consumptions);
			Assert.Single(state.BuildingHealth);
			Assert.Single(state.OpenedEntities);
			Assert.Single(state.TrapStates);
			Assert.Equal(TrapPhase.Cooldown, state.TrapStates[0].Phase);
		}
		finally
		{
			if (System.IO.File.Exists(path))
			{
				System.IO.File.Delete(path);
			}
		}
	}

	private static Decision RecordTrap(GameStateKernel kernel, ulong op, EntityPosition position, int kind, byte extra, long triggeredAtMs) =>
		kernel.Execute(
			new RecordTrapConsumedCommand(new OperationId(op), Host, Epoch, AuthorityKind.HostOnly, position, kind, extra, triggeredAtMs),
			new CommandContext(Epoch, Host));

	private static Decision RecordHealth(GameStateKernel kernel, ulong op, EntityPosition position, float health) =>
		kernel.Execute(
			new RecordBuildingEntityHealthCommand(new OperationId(op), Host, Epoch, AuthorityKind.HostOnly, position, health),
			new CommandContext(Epoch, Host));

	private static Decision RecordOpened(GameStateKernel kernel, ulong op, EntityPosition position) =>
		kernel.Execute(
			new RecordOpenedEntityCommand(new OperationId(op), Host, Epoch, AuthorityKind.HostOnly, position),
			new CommandContext(Epoch, Host));

	private static Decision RecordTrapState(GameStateKernel kernel, ulong op, EntityPosition position, int kind, TrapPhase phase, byte extra, long ms) =>
		kernel.Execute(
			new RecordTrapStateCommand(new OperationId(op), Host, Epoch, AuthorityKind.HostOnly, position, kind, phase, extra, ms),
			new CommandContext(Epoch, Host));
}
