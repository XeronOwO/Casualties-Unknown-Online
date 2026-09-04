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

	[Fact]
	public void Composite_LaterInnerCommandSeesEarlierStagedResult()
	{
		var kernel = new GameStateKernel(Epoch);
		var updatedData = new ItemData(1f, false, 2, [], []);

		var decision = kernel.Execute(
			new CompositeGameCommand(
				new OperationId(1),
				Host,
				Epoch,
				AuthorityKind.HostOnly,
				[
					SpawnItem(),
					new UpdateItemStateCommand(
						new OperationId(101),
						Host,
						Epoch,
						AuthorityKind.HostOnly,
						10,
						updatedData,
						1),
				]),
			new CommandContext(Epoch, Host));

		Assert.True(decision.IsAccepted);
		var item = kernel.FindItem(10);
		Assert.NotNull(item);
		Assert.Equal(2ul, item.Value.Revision);
		Assert.True(item.Value.Data.SemanticallyEquals(updatedData));
		Assert.Equal(2, decision.Batch!.Events.Count);
	}

	[Fact]
	public void Composite_Rollback_WhenLaterInnerCommandRejected()
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
					SpawnItem(id: 10, operation: 102),
				]),
			new CommandContext(Epoch, Host));

		Assert.False(decision.IsAccepted);
		Assert.Equal(RejectionReason.Conflict, decision.Rejection!.Reason);
		Assert.Empty(kernel.QueryItems());
		Assert.Null(kernel.QueryPlayers());
	}

	[Fact]
	public void Composite_DuplicateOperationId_ReturnsOriginalDecision()
	{
		var kernel = new GameStateKernel(Epoch);
		var command = new CompositeGameCommand(
			new OperationId(3),
			Host,
			Epoch,
			AuthorityKind.HostOnly,
			[SpawnItem()]);
		var context = new CommandContext(Epoch, Host);

		var first = kernel.Execute(command, context);
		var second = kernel.Execute(command, context);

		Assert.True(second.IsAccepted);
		Assert.Equal(first.Batch!.GlobalRevision, second.Batch!.GlobalRevision);
		Assert.Equal(first.Batch.Events.Count, second.Batch.Events.Count);
		Assert.Single(kernel.QueryItems());
	}

	[Fact]
	public void Composite_InnerOperationIdsAreNotSeparateIdempotencyKeys()
	{
		var kernel = new GameStateKernel(Epoch);
		var context = new CommandContext(Epoch, Host);
		var first = new CompositeGameCommand(
			new OperationId(4),
			Host,
			Epoch,
			AuthorityKind.HostOnly,
			[SpawnItem(operation: 100)]);

		Assert.True(kernel.Execute(first, context).IsAccepted);

		var second = new CompositeGameCommand(
			new OperationId(5),
			Host,
			Epoch,
			AuthorityKind.HostOnly,
			[SpawnItem(id: 11, operation: 100)]);

		Assert.True(kernel.Execute(second, context).IsAccepted);
		Assert.Equal(2, kernel.QueryItems().Count);
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

	private static SpawnItemCommand SpawnItem(ulong id = 10, ulong operation = 100) =>
		new(
			new OperationId(operation),
			Host,
			Epoch,
			AuthorityKind.HostOnly,
			new ItemIdentity(id, "water"),
			ItemLocation.World(1f, 2f),
			0);
}
