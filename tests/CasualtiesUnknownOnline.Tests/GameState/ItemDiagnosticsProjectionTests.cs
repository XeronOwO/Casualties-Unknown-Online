using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.GameState.Projections;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.GameState;

public class ItemDiagnosticsProjectionTests
{
	private static readonly RunEpoch Epoch = new(1);
	private static readonly ActorId Host = new(1001);
	private static readonly ActorId Guest = new(2001);

	[Fact]
	public void BuildActiveFacts_ExcludesTerminalAndIncludesCarried()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(Spawn(kernel, 1, 10, "water", ItemLocation.World(1, 2)).IsAccepted);
		Assert.True(Destroy(kernel, 2, 10, expectedRevision: 1).IsAccepted);
		Assert.True(Spawn(kernel, 3, 11, "bread", ItemLocation.World(3, 4)).IsAccepted);
		Assert.True(Pickup(kernel, 4, 11, Guest, expectedRevision: 1).IsAccepted);

		var facts = ItemDiagnosticsProjection.BuildActiveFacts(kernel.QueryItems().Values);

		Assert.Equal(1, facts.Count);
		Assert.True(facts.ContainsKey(11));
		Assert.False(facts.ContainsKey(10));
		Assert.Equal(ItemLocationKind.Carried, facts[11].LocationKind);
		Assert.Equal(Guest.Value, facts[11].Owner);
	}

	[Fact]
	public void Compare_ReportsMissingUnexpectedAndDifferingFacts()
	{
		var expected = new Dictionary<ulong, ItemTerminalFact>
		{
			[10] = new(10, "water", ItemLocationKind.World, 0, 0, 1, 2, 1),
		};
		var actual = new Dictionary<ulong, ItemTerminalFact>
		{
			[10] = new(10, "water", ItemLocationKind.Carried, Guest.Value, 0, 0, 0, 2),
			[11] = new(11, "bread", ItemLocationKind.World, 0, 0, 3, 4, 1),
		};

		var diff = ItemDiagnosticsProjection.Compare(expected, actual);

		Assert.True(diff.HasDifferences);
		Assert.Equal(2, diff.Differences.Count);
	}

	[Fact]
	public void Compare_AgreesOnIdenticalActiveFacts()
	{
		var facts = new Dictionary<ulong, ItemTerminalFact>
		{
			[10] = new(10, "water", ItemLocationKind.World, 0, 0, 1, 2, 1),
		};

		var diff = ItemDiagnosticsProjection.Compare(facts, facts);

		Assert.False(diff.HasDifferences);
	}

	private static Decision Spawn(GameStateKernel kernel, ulong op, ulong id, string definition, ItemLocation location) =>
		kernel.Execute(
			new SpawnItemCommand(new OperationId(op), Host, Epoch, AuthorityKind.HostOnly, new ItemIdentity(id, definition), location, 0),
			new CommandContext(Epoch, Host));

	private static Decision Pickup(GameStateKernel kernel, ulong op, ulong id, ActorId owner, ulong expectedRevision) =>
		kernel.Execute(
			new PickUpItemCommand(new OperationId(op), owner, Epoch, AuthorityKind.OwnerPredictedHostValidated, id, owner, expectedRevision),
			new CommandContext(Epoch, owner));

	private static Decision Destroy(GameStateKernel kernel, ulong op, ulong id, ulong expectedRevision) =>
		kernel.Execute(
			new DestroyItemCommand(new OperationId(op), Host, Epoch, AuthorityKind.HostOnly, id, TerminalKind.Destroyed, expectedRevision),
			new CommandContext(Epoch, Host));
}
