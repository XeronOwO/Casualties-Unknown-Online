using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session.World;

/// <summary>
/// The reconcile is a backstop: it must send nothing when nothing changed (an idle run pays no
/// bandwidth), send exactly the changed rows, and never repeat what a member was told.
/// </summary>
public sealed class BlockTableReconcilerTests
{
	private static DamagedBlock Cell(int x, int y, ushort block) => new(x, y, block, SupportLossSettled: false);

	[Fact]
	public void TheStepArmsOnceThenRunsOnItsOwnInterval()
	{
		var reconciler = new BlockTableReconciler();

		Assert.False(reconciler.TryBegin(0)); // arms the clock
		Assert.False(reconciler.TryBegin(59_999));
		Assert.True(reconciler.TryBegin(60_000));
		Assert.False(reconciler.TryBegin(60_001));
		Assert.True(reconciler.TryBegin(120_000));
	}

	[Fact]
	public void AnUnchangedTableSendsNothing()
	{
		var reconciler = new BlockTableReconciler();
		var cells = new List<DamagedBlock> { Cell(1, 2, 3) };

		var first = reconciler.Diff([11ul], cells);
		Assert.Single(first);
		Assert.Single(first[0].Rows);

		var second = reconciler.Diff([11ul], cells);
		Assert.Empty(second);
	}

	[Fact]
	public void OnlyTheChangedRowIsSent()
	{
		var reconciler = new BlockTableReconciler();
		reconciler.Diff([11ul], [Cell(1, 2, 3), Cell(4, 5, 6)]);

		var changed = reconciler.Diff([11ul], [Cell(1, 2, 3), Cell(4, 5, 0)]);

		var rows = Assert.Single(changed).Rows;
		var row = Assert.Single(rows);
		Assert.Equal(4, row.X);
		Assert.Equal(5, row.Y);
		Assert.Equal(0, row.Block);
	}

	[Fact]
	public void EachMemberHasItsOwnBaseline()
	{
		var reconciler = new BlockTableReconciler();
		var cells = new List<DamagedBlock> { Cell(7, 8, 9) };

		reconciler.Diff([11ul], cells);
		var other = reconciler.Diff([12ul], cells);

		// The second member was told nothing yet, so it receives the row even though the table did not change.
		Assert.Single(Assert.Single(other).Rows);
	}

	[Fact]
	public void ResetForgetsEveryBaseline()
	{
		var reconciler = new BlockTableReconciler();
		var cells = new List<DamagedBlock> { Cell(1, 1, 2) };
		reconciler.Diff([11ul], cells);

		reconciler.Reset();

		Assert.Single(Assert.Single(reconciler.Diff([11ul], cells)).Rows);
	}
}
