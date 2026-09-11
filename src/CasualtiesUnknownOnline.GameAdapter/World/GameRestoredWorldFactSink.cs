using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The Game Adapter's <see cref="IRestoredWorldFactSink"/>: the live-world writes
/// of a restored cut, each one a call into an existing table helper. It exists so
/// the replay's order, accounting and pending handover
/// (<see cref="RestoredWorldFactReplay"/>) stay testable without a running game —
/// every game-typed operation lives here and nowhere else.
///
/// The two non-obvious writes:
/// - the block diff is written WITHOUT marking building support loss: the building
///   that stood on a mined block already died in the saved world, and its drops
///   are checkpoint items — re-triggering the death would roll them a second time;
/// - the game's own damage list is CLEARED before the restored rows land, so a
///   crack the fresh world somehow already had cannot survive a cut that never
///   named it.
/// </summary>
internal sealed class GameRestoredWorldFactSink(
	BlockBreakSync blockBreaks,
	ILogger<GameRestoredWorldFactSink> log) : IRestoredWorldFactSink
{
	private readonly BlockBreakSync _blockBreaks = blockBreaks;
	private readonly ILogger<GameRestoredWorldFactSink> _log = log;

	/// <inheritdoc />
	public bool IsWorldReady
	{
		get
		{
			var world = WorldGeneration.world;
			return world != null && !HarmonyTraverse.IsGenerating(); // Unity object — ==
		}
	}

	/// <inheritdoc />
	public LiveWorldWriteOutcome WriteBlockStates(IReadOnlyList<BlockStateEntryMsg> states)
	{
		var world = WorldGeneration.world;
		if (world == null) // Unity object — == (the readiness check and this write are not atomic)
		{
			_log.LogError("[SaveFacts] no live world to write {Count} restored block-state row(s) into.", states.Count);
			return new LiveWorldWriteOutcome(0, states.Count);
		}

		return LiveWorldWriteOutcome.All(WorldBlockStateTable.Apply(
			[.. states.Select(state => new DamagedBlock(state.X, state.Y, state.Block, state.SupportLossSettled))],
			// The host's replay never settles support loss (see the class doc): the
			// host is the authority that settled it in the saved world, and the
			// restored rows carry that verdict to the guests.
			(pos, _) => _blockBreaks.OnBlockAirWrite(pos)));
	}

	/// <inheritdoc />
	public LiveWorldWriteOutcome ReplaceGameBlockDamages(IReadOnlyList<BlockDamageEntryMsg> rows)
	{
		var world = WorldGeneration.world;
		if (world == null) // Unity object — == (every row is refused rather than lost quietly)
		{
			_log.LogError("[SaveFacts] no live world to write {Count} restored partial-damage row(s) into.", rows.Count);
			return new LiveWorldWriteOutcome(0, rows.Count);
		}

		world.ClearBlockDamages(); // the cut is the whole truth for this list
		var result = GameBlockDamageTable.Apply(world, rows, "Restored cut (game's own damage table)", _log);
		RefreshCrackSprites(result.Written);
		return new LiveWorldWriteOutcome(result.Applied, result.Refused);
	}

	/// <inheritdoc />
	public LiveWorldWriteOutcome ApplyKeypadCodes(IReadOnlyList<KeypadEntryMsg> codes)
	{
		var applied = KeypadCodeTable.ApplyAbsolute(codes);
		return new LiveWorldWriteOutcome(applied, codes.Count - applied);
	}

	/// <inheritdoc />
	public LiveWorldWriteOutcome ApplyGeysers(IReadOnlyList<GeyserStateEntryMsg> geysers)
	{
		var applied = GeyserStateTable.Apply(geysers);
		return new LiveWorldWriteOutcome(applied, geysers.Count - applied);
	}

	/// <inheritdoc />
	public bool ApplyRadiationLine(RadiationLineStateMsg line) => RadiationLineTable.Apply(line);

	/// <summary>
	/// The crack sprites of the rows this apply wrote. Presentation only: the row
	/// and its damage are already in the list, so a refresh failure must not undo
	/// the repaired state.
	/// </summary>
	private void RefreshCrackSprites(IReadOnlyList<BlockDamage> written)
	{
		foreach (var damage in written)
		{
			try
			{
				damage.UpdateSprite();
			}
			catch (Exception ex)
			{
				_log.LogWarning(ex, "[SaveFacts] a restored partial-damage row was written but its crack sprite could not be refreshed at ({X},{Y}).",
					damage.pos.x, damage.pos.y);
			}
		}
	}
}
