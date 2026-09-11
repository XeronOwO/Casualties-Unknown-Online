using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.World;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The block-state rows of <c>world-blocks.json</c> written into a live world,
/// absolute: the cut is the whole truth for the cells it names, and a cell it
/// does not name keeps the generated baseline. Writes run under
/// <see cref="CallContext.Origin.RemoteApply"/> so the local-report hooks stay
/// silent — the rows are already the authoritative table, and echoing them back
/// would be a report of a report.
///
/// Two callers, one implementation: the guest's snapshot apply
/// (<see cref="WorldEventSync"/>) and the host's restored-world replay
/// (<see cref="GameRestoredWorldFactSink"/>). Whether an air write also settles
/// the building support loss is a PER-ROW decision carried by the row itself
/// (<see cref="DamagedBlock.SupportLossSettled"/>): a live write was settled by
/// the world that made it and must be re-settled here, while a row restored from
/// an archive was settled long ago — re-running it would kill a building the
/// authority still holds, and re-roll the drops of one that already died.
/// </summary>
internal static class WorldBlockStateTable
{
	/// <summary>
	/// Apply the absolute block diff, invoking <paramref name="onAirWrite"/> for
	/// every cell this call actually changed to air (with the row's support-loss
	/// verdict).
	/// </summary>
	internal static int Apply(IReadOnlyList<DamagedBlock> blocks, Action<Vector2Int, bool> onAirWrite)
	{
		var world = WorldGeneration.world;
		if (world == null) // Unity object — == (nothing to write into)
		{
			return 0;
		}

		var written = 0;
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			foreach (var block in blocks)
			{
				var pos = new Vector2Int(block.X, block.Y);
				var changed = world.GetBlock(pos) != block.Block;
				world.SetBlock(pos, block.Block);
				if (!changed)
				{
					continue; // already the row's value: not a write, so it is not counted as one
				}

				written++;
				if (block.Block == 0)
				{
					// A direct SetBlock(0) leaves the game's own BlockDamage entry
					// and its crack sprite behind ("fragmented air"), and the cell
					// may have carried a building that required the ground. An
					// UNCHANGED cell was already handled when it became air
					// locally — re-marking it would suppress this side's own
					// building-drop roll (RemoteEntityDeath).
					onAirWrite(pos, block.SupportLossSettled);
				}
			}
		}

		return written;
	}
}
