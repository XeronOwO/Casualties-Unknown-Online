using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.GameState.Domains.Fluids;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.GameState;

/// <summary>
/// Epoch-isolation property tests: a fresh kernel for a new run epoch must not
/// retain facts from an earlier epoch, and old-epoch commands/batches must be
/// rejected instead of leaking into the new run.
/// </summary>
public class EpochIsolationTests
{
	private static readonly RunEpoch OldEpoch = new(1);
	private static readonly RunEpoch NewEpoch = new(2);
	private static readonly ActorId Host = new(1001);

	[Fact]
	public void NewEpochKernel_HasNoResidueFromPreviousEpoch()
	{
		var old = new GameStateKernel(OldEpoch);
		Assert.True(StartRun(old, 1).IsAccepted);
		Assert.True(SpawnItem(old, 2).IsAccepted);
		Assert.True(UpsertEnemy(old, 3).IsAccepted);
		Assert.True(RecordTrap(old, 4).IsAccepted);
		Assert.True(UpdatePlayer(old, 5).IsAccepted);
		Assert.True(UpdateFluid(old, 6).IsAccepted);

		var fresh = new GameStateKernel(NewEpoch);
		Assert.Null(fresh.QueryRun());
		Assert.Empty(fresh.QueryItems());
		Assert.Null(fresh.QueryEnemies());
		Assert.Null(fresh.QueryWorldEntities());
		Assert.Null(fresh.QueryPlayers());
		Assert.Null(fresh.QueryFluids());
	}

	[Fact]
	public void OldEpochCommand_IsRejectedByNewEpochKernel()
	{
		var fresh = new GameStateKernel(NewEpoch);

		var decision = fresh.Execute(
			new SpawnItemCommand(
				new OperationId(1),
				Host,
				OldEpoch,
				AuthorityKind.HostOnly,
				new ItemIdentity(10, "water"),
				ItemLocation.World(1f, 2f),
				0),
			new CommandContext(NewEpoch, Host));

		Assert.False(decision.IsAccepted);
		Assert.Equal(RejectionReason.WrongEpoch, decision.Rejection!.Reason);
	}

	[Fact]
	public void OldEpochBatch_IsRejectedByNewEpochKernel()
	{
		var old = new GameStateKernel(OldEpoch);
		var batch = SpawnItem(old, 1).Batch!;

		var fresh = new GameStateKernel(NewEpoch);
		var result = fresh.Apply(batch);

		Assert.False(result.Success);
		Assert.Contains("epoch", result.Error);
	}

	private static Decision StartRun(GameStateKernel kernel, ulong op) =>
		kernel.Execute(
			new StartRunCommand(
				new OperationId(op),
				Host,
				OldEpoch,
				AuthorityKind.HostOnly,
				new RunState(42, [1, 2, 3], BiomeOverride: 0, BiomeDepth: 1, TotalTraveled: 0, LoadedRun: false, null, 0)),
			new CommandContext(OldEpoch, Host));

	private static Decision SpawnItem(GameStateKernel kernel, ulong op) =>
		kernel.Execute(
			new SpawnItemCommand(
				new OperationId(op),
				Host,
				OldEpoch,
				AuthorityKind.HostOnly,
				new ItemIdentity(10, "water"),
				ItemLocation.World(1f, 2f),
				0),
			new CommandContext(OldEpoch, Host));

	private static Decision UpsertEnemy(GameStateKernel kernel, ulong op) =>
		kernel.Execute(
			new UpsertEnemyCommand(
				new OperationId(op),
				Host,
				OldEpoch,
				AuthorityKind.HostOnly,
				new EnemyState(new EntityId(1, 2, 0), "spider", 10f, false, false)),
			new CommandContext(OldEpoch, Host));

	private static Decision RecordTrap(GameStateKernel kernel, ulong op) =>
		kernel.Execute(
			new RecordTrapConsumedCommand(
				new OperationId(op),
				Host,
				OldEpoch,
				AuthorityKind.HostOnly,
				new EntityPosition(1, 2),
				1,
				0,
				10),
			new CommandContext(OldEpoch, Host));

	private static Decision UpdatePlayer(GameStateKernel kernel, ulong op) =>
		kernel.Execute(
			new UpdatePlayerStatusCommand(
				new OperationId(op),
				Host,
				OldEpoch,
				AuthorityKind.HostOnly,
				new PlayerState(2001, true, true)),
			new CommandContext(OldEpoch, Host));

	private static Decision UpdateFluid(GameStateKernel kernel, ulong op) =>
		kernel.Execute(
			new UpdateFluidRegionCommand(
				new OperationId(op),
				Host,
				OldEpoch,
				AuthorityKind.HostOnly,
				new FluidRegionState(1, 2, 3, 4, 5)),
			new CommandContext(OldEpoch, Host));
}
