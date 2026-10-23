using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.GameState;

/// <summary>
/// The container-chain walk the item rules ask "who holds this" of. Its answers are the fact three
/// eligibility rules now read (`MayInstallOnOwnLimb`, `MayReportOn` and the container-sync home check), so
/// the fail-closed branches are pinned here once rather than only through those rules: a missing parent, a
/// cycle, a self-parent and a world/applied/terminal root all answer 0, and only a carried root names an
/// actor.
/// </summary>
public class ItemLocationChainTests
{
	private const ulong Owner = 2001;

	private static ItemState Row(ulong id, ItemLocation location) => new(new ItemIdentity(id, "test_item"), 1, location);

	private static ItemState? Find(Dictionary<ulong, ItemState> rows, ulong id) =>
		rows.TryGetValue(id, out var row) ? row : null;

	[Fact]
	public void ACarriedRow_ReturnsItsOwner()
	{
		var rows = new Dictionary<ulong, ItemState> { [42] = Row(42, ItemLocation.Carried(new ActorId(Owner))) };

		Assert.Equal(Owner, ItemLocationChain.HolderOf(rows[42], id => Find(rows, id)));
	}

	[Fact]
	public void AContainedChildOfACarriedRow_ReturnsTheCarriedOwner()
	{
		var rows = new Dictionary<ulong, ItemState>
		{
			[42] = Row(42, ItemLocation.Contained(new ActorId(999), 50)),
			[50] = Row(50, ItemLocation.Carried(new ActorId(Owner))),
		};

		Assert.Equal(Owner, ItemLocationChain.HolderOf(rows[42], id => Find(rows, id)));
	}

	[Fact]
	public void AMissingParent_ReturnsZero()
	{
		var rows = new Dictionary<ulong, ItemState> { [42] = Row(42, ItemLocation.Contained(new ActorId(Owner), 50)) };

		Assert.Equal(0ul, ItemLocationChain.HolderOf(rows[42], id => Find(rows, id)));
	}

	[Fact]
	public void ACycle_ReturnsZero()
	{
		var rows = new Dictionary<ulong, ItemState>
		{
			[42] = Row(42, ItemLocation.Contained(new ActorId(Owner), 43)),
			[43] = Row(43, ItemLocation.Contained(new ActorId(Owner), 42)),
		};

		Assert.Equal(0ul, ItemLocationChain.HolderOf(rows[42], id => Find(rows, id)));
	}

	[Fact]
	public void ASelfParent_ReturnsZero()
	{
		var rows = new Dictionary<ulong, ItemState> { [42] = Row(42, ItemLocation.Contained(new ActorId(Owner), 42)) };

		Assert.Equal(0ul, ItemLocationChain.HolderOf(rows[42], id => Find(rows, id)));
	}

	[Fact]
	public void AWorldRoot_ReturnsZero()
	{
		var rows = new Dictionary<ulong, ItemState>
		{
			[42] = Row(42, ItemLocation.Contained(new ActorId(Owner), 50)),
			[50] = Row(50, ItemLocation.World(1f, 2f)),
		};

		Assert.Equal(0ul, ItemLocationChain.HolderOf(rows[42], id => Find(rows, id)));
	}

	[Fact]
	public void AnAppliedRoot_ReturnsZero()
	{
		var rows = new Dictionary<ulong, ItemState>
		{
			[42] = Row(42, ItemLocation.Contained(new ActorId(Owner), 50)),
			[50] = Row(50, ItemLocation.Applied(new ActorId(Owner), 3)),
		};

		Assert.Equal(0ul, ItemLocationChain.HolderOf(rows[42], id => Find(rows, id)));
	}
}
