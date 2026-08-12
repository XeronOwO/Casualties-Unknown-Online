using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The fluid region RLE decode — the absolute-overwrite semantics the
/// guest's grid apply must have (81dd26a: skipping zero runs left the old
/// liquid in place and the guest's water grew without bound — "the guest's
/// water is visibly more"). Every cell of the rectangle is written, zero
/// runs included; the uncovered tail is cleared; the region is clamped to
/// the world grid.
/// </summary>
public class FluidRleCodecTests
{
	private static List<(int X, int Y, byte Value)> Decode(byte[] cells, int width, int height, int originX = 0, int originY = 0, int gridW = 128, int gridH = 112)
	{
		var writes = new List<(int X, int Y, byte Value)>();
		FluidRleCodec.Decode(cells, width, height, originX, originY, gridW, gridH, (x, y, v) => writes.Add((x, y, v)));
		return writes;
	}

	[Fact]
	public void RowOfOneValue_WritesEveryCell()
	{
		var writes = Decode([5, 4], width: 4, height: 1);
		Assert.Equal(4, writes.Count);
		Assert.All(writes, w => Assert.Equal(5, w.Value));
		Assert.Equal([(0, 0, (byte)5), (1, 0, (byte)5), (2, 0, (byte)5), (3, 0, (byte)5)], writes);
	}

	[Fact]
	public void MidRegionZeroRun_MustClearTheCell()
	{
		// The 81dd26a bug's shape: a zero run in the MIDDLE of the region —
		// the old liquid must be overwritten with 0 (absolute snapshot), the
		// decoder must not skip it.
		var writes = Decode([5, 2, 0, 2, 5, 2], width: 6, height: 1);
		Assert.Equal(6, writes.Count);
		Assert.Equal((2, 0, (byte)0), writes[2]);
		Assert.Equal((3, 0, (byte)0), writes[3]);
	}

	[Fact]
	public void TrailingZeroRunsOmitted_ClearedByTheDecoder()
	{
		// The sender omits trailing zero runs — the decoder must clear the
		// uncovered tail (the old liquid there would otherwise survive).
		var writes = Decode([5, 2], width: 5, height: 1);
		Assert.Equal(5, writes.Count);
		Assert.Equal((byte)0, writes[2].Value);
		Assert.Equal((byte)0, writes[4].Value);
	}

	[Fact]
	public void TruncatedRunAtRectangleEnd_StopsCleanly()
	{
		// A run that overflows the rectangle must be cut at the rectangle end.
		var writes = Decode([5, 10], width: 4, height: 1);
		Assert.Equal(4, writes.Count);
		Assert.All(writes, w => Assert.Equal(5, w.Value));
	}

	[Fact]
	public void OriginOffsets_AppliedToRowMajorPositions()
	{
		var writes = Decode([7, 4], width: 4, height: 1, originX: 10, originY: 20);
		Assert.Equal((10, 20, (byte)7), writes[0]);
		Assert.Equal((13, 20, (byte)7), writes[3]);
	}

	[Fact]
	public void MultiRow_IsRowMajor()
	{
		var writes = Decode([1, 2, 2, 2], width: 2, height: 2);
		Assert.Equal(4, writes.Count);
		Assert.Equal((0, 0, (byte)1), writes[0]);
		Assert.Equal((1, 0, (byte)1), writes[1]);
		Assert.Equal((0, 1, (byte)2), writes[2]);
		Assert.Equal((1, 1, (byte)2), writes[3]);
	}

	[Fact]
	public void OutOfBoundsCells_ClampedToTheGrid()
	{
		// The region may stick out of the world grid (a viewport near an edge) —
		// the writes clamp to the grid bounds instead of writing out of range.
		var writes = Decode([3, 4], width: 4, height: 1, originX: -1, originY: -2, gridW: 4, gridH: 4);
		Assert.Equal((0, 0, (byte)3), writes[0]); // -1 clamps to 0
		Assert.Equal((2, 0, (byte)3), writes[3]); // the last cell sits at x = -1+3 = 2, in range
	}
}
