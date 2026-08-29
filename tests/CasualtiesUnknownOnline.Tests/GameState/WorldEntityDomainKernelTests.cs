using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;
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
	public void SamePositionOpened_IsIdempotent()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(RecordOpened(kernel, 1, new EntityPosition(1, 2)).IsAccepted);
		Assert.True(RecordOpened(kernel, 2, new EntityPosition(1, 2)).IsAccepted);

		Assert.Single(kernel.QueryWorldEntities()!.OpenedEntities);
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

		var restored = WireCheckpointAssembler.Assemble(WireCheckpointAssembler.Split(kernel.CreateCheckpoint()));

		var state = restored.WorldEntities;
		Assert.NotNull(state);
		Assert.Single(state!.Consumptions);
		Assert.Single(state.BuildingHealth);
		Assert.Single(state.OpenedEntities);
	}

	[Fact]
	public void SaveLoad_RoundTripsWorldEntityState()
	{
		var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cuo-world-entities-{System.Guid.NewGuid():N}.bin");
		try
		{
			var authority = new ItemKernelAuthority(NullLogger<ItemKernelAuthority>.Instance);
			Assert.True(authority.TryRecordTrapConsumed(Host.Value, new EntityPosition(1, 2), 1, 3, 10, out _, out _));
			Assert.True(authority.TryRecordBuildingEntityHealth(Host.Value, new EntityPosition(4, 5), 8f, out _, out _));
			Assert.True(authority.TryRecordOpenedEntity(Host.Value, new EntityPosition(7, 8), out _, out _));

			var store = new KernelSaveFileStore(path, NullLogger<KernelSaveFileStore>.Instance);
			Assert.True(store.Save(authority.CreateCheckpoint()));
			Assert.True(store.TryLoad(out var loaded));

			var state = loaded.WorldEntities;
			Assert.NotNull(state);
			Assert.Single(state!.Consumptions);
			Assert.Single(state.BuildingHealth);
			Assert.Single(state.OpenedEntities);
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
}
