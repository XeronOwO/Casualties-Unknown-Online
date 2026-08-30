using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.GameState;

public class CompositeCommandKernelTests
{
	private static readonly RunEpoch Epoch = new(1);
	private static readonly ActorId Host = new(1001);

	[Fact]
	public void Composite_CommitsCrossDomainBatchAtomically()
	{
		var kernel = new GameStateKernel(Epoch);

		var decision = ExecuteComposite(kernel, 1);

		Assert.True(decision.IsAccepted);
		var batch = decision.Batch!;
		Assert.Equal(2, batch.Events.Count);
		Assert.Contains(batch.Events, e => e is ItemSpawnedEvent);
		Assert.Contains(batch.Events, e => e is PlayerStatusUpdatedEvent);

		Assert.Single(kernel.QueryItems());
		Assert.Single(kernel.QueryPlayers()!.Players);
	}

	[Fact]
	public void Composite_RejectsAllWhenAnyInnerCommandRejected()
	{
		var kernel = new GameStateKernel(Epoch);

		var decision = kernel.Execute(
			new CompositeGameCommand(
				new OperationId(2),
				Host,
				Epoch,
				AuthorityKind.HostOnly,
				[
					SpawnItem(),
					new UpdatePlayerStatusCommand(
						new OperationId(3),
						Host,
						Epoch,
						AuthorityKind.HostOnly,
						new PlayerState(2001, false, true)),
				]),
			new CommandContext(Epoch, Host));

		Assert.False(decision.IsAccepted);
		Assert.Empty(kernel.QueryItems());
		Assert.Null(kernel.QueryPlayers());
	}

	[Fact]
	public void Composite_ReplaysAllEventsOnGuestKernel()
	{
		var source = new GameStateKernel(Epoch);
		var batch = ExecuteComposite(source, 1).Batch!;

		var guest = new GameStateKernel(Epoch);
		Assert.True(guest.Apply(batch).Success);

		Assert.Single(guest.QueryItems());
		Assert.Single(guest.QueryPlayers()!.Players);
	}

	private static Decision ExecuteComposite(GameStateKernel kernel, ulong op) =>
		kernel.Execute(
			new CompositeGameCommand(
				new OperationId(op),
				Host,
				Epoch,
				AuthorityKind.HostOnly,
				[
					SpawnItem(),
					new UpdatePlayerStatusCommand(
						new OperationId(op * 10),
						Host,
						Epoch,
						AuthorityKind.HostOnly,
						new PlayerState(2001, true, true)),
				]),
			new CommandContext(Epoch, Host));

	private static SpawnItemCommand SpawnItem() =>
		new(
			new OperationId(100),
			Host,
			Epoch,
			AuthorityKind.HostOnly,
			new ItemIdentity(10, "water"),
			ItemLocation.World(1f, 2f),
			0);
}
