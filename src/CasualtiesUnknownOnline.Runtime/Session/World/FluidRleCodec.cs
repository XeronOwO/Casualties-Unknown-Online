using System;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The fluid region RLE decode — pure, testable. A region is an ABSOLUTE
/// snapshot of its rectangle: EVERY cell in the rectangle is written (zero
/// runs included), and the uncovered tail (the omitted trailing zero runs)
/// is cleared. Skipping zero runs leaves the old liquid in place and the
/// guest's fluid keeps growing — the observed "the guest's water is visibly
/// more" (81dd26a). The decoder emits one write per cell; the caller applies
/// it to the actual grid (the game's fluid array).
/// </summary>
internal static class FluidRleCodec
{
	/// <summary>
	/// Decode a [value, count, ...] run list into absolute-overwrite writes.
	/// <paramref name="width"/>/<paramref name="height"/> = the region rectangle
	/// (row-major), <paramref name="gridWidth"/>/<paramref name="gridHeight"/> =
	/// the clamp bounds of the world grid, <paramref name="write"/> receives
	/// (x, y, value) for every covered cell, zero runs and the uncovered tail
	/// included.
	/// </summary>
	internal static void Decode(
		byte[] cells,
		int width,
		int height,
		int originX,
		int originY,
		int gridWidth,
		int gridHeight,
		Action<int, int, byte> write)
	{
		var total = width * height;
		var pos = 0;
		for (var i = 0; i + 1 < cells.Length && pos < total; i += 2)
		{
			var value = cells[i];
			var count = cells[i + 1];
			for (var c = 0; c < count && pos < total; c++, pos++)
			{
				// EVERY cell is written, zero runs included — a mid-region zero
				// run (liquid flowed away) MUST clear the cell.
				write(Clamp(originX + pos % width, gridWidth), Clamp(originY + pos / width, gridHeight), value);
			}
		}

		// The uncovered tail (the omitted trailing zero runs) = cleared cells.
		for (; pos < total; pos++)
		{
			write(Clamp(originX + pos % width, gridWidth), Clamp(originY + pos / width, gridHeight), 0);
		}
	}

	private static int Clamp(int value, int maxExclusive) =>
		value < 0 ? 0 : value >= maxExclusive ? maxExclusive - 1 : value;
}
