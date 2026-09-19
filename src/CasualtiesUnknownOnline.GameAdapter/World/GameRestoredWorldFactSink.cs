using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Persistence;
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
/// The non-obvious writes:
/// - the block diff is written WITHOUT marking building support loss: the building
///   that stood on a mined block already died in the saved world, and its drops
///   are checkpoint items — re-triggering the death would roll them a second time;
/// - the game's own damage list is CLEARED before the restored rows land, so a
///   crack the fresh world somehow already had cannot survive a cut that never
///   named it;
/// - the world-entity rows go through the SAME appliers the guest's checkpoint
///   projection uses (the trap replay, the opened-entity apply, the health apply),
///   never a second implementation: a death applied there is a REMOTE death, so
///   the drops the saved world already rolled are not rolled again.
/// </summary>
internal sealed class GameRestoredWorldFactSink(
	BlockBreakSync blockBreaks,
	WorldBuildingEntitySync buildingEntities,
	EntityEventSync entityEvents,
	ILogger<GameRestoredWorldFactSink> log) : IRestoredWorldFactSink
{
	private readonly BlockBreakSync _blockBreaks = blockBreaks;
	private readonly WorldBuildingEntitySync _buildingEntities = buildingEntities;
	private readonly EntityEventSync _entityEvents = entityEvents;
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

		return WorldBlockStateTable.Apply(
			[.. states.Select(state => new DamagedBlock(state.X, state.Y, state.Block, state.SupportLossSettled))],
			// The host's replay never settles support loss (see the class doc): the
			// host is the authority that settled it in the saved world, and the
			// restored rows carry that verdict to the guests.
			(pos, _) => _blockBreaks.OnBlockAirWrite(pos),
			_log);
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
		var applied = KeypadCodeTable.ApplyAbsolute(codes, _log);
		return new LiveWorldWriteOutcome(applied, codes.Count - applied);
	}

	/// <inheritdoc />
	public LiveWorldWriteOutcome ApplyGeysers(IReadOnlyList<GeyserStateEntryMsg> geysers)
	{
		var applied = GeyserStateTable.Apply(geysers, _log);
		return new LiveWorldWriteOutcome(applied, geysers.Count - applied);
	}

	/// <inheritdoc />
	public LiveWorldWriteOutcome ApplyRecipeUnlocks(IReadOnlyList<SaveRecipeUnlockRow> recipes)
	{
		// The table is COMPLETE here — the game rebuilt it in Awake and CUO's
		// mod-content provider appended the custom recipes on a later Update frame —
		// which is what makes an index from the archive mean the recipe it meant when
		// the cut was written.
		var result = RecipeUnlockTable.Apply(recipes, _log);
		if (result.RefusedIndexes.Count > 0)
		{
			_log.LogWarning(
				"[SaveFacts] {Refused} restored recipe unlock row(s) name a recipe this world's table does not have ({Indices}); they are NOT written, so those recipes keep the live unlock state.",
				result.RefusedIndexes.Count, string.Join(", ", result.RefusedIndexes));
		}

		// A row whose write THREW is its own refusal class (the containment named it at error
		// level with its index), counted here so the restore's account names it lost — without
		// the missing-recipe warning above claiming an index this table does hold.
		return new LiveWorldWriteOutcome(result.Applied, result.RefusedIndexes.Count + result.RefusedByThrow);
	}

	/// <inheritdoc />
	public bool ApplyRadiationLine(RadiationLineStateMsg line) => RadiationLineTable.Apply(line);

	/// <inheritdoc />
	public LiveWorldWriteOutcome ApplyWorldEntities(RestoredWorldEntityFacts facts)
	{
		if (WorldGeneration.world == null) // Unity object — == (the readiness check and this write are not atomic)
		{
			_log.LogError("[SaveFacts] no live world to write {Count} restored world-entity row(s) into.", facts.Count);
			return new LiveWorldWriteOutcome(0, facts.Count);
		}

		// The SAME three appliers the guest's checkpoint projection calls, in the
		// same order: the trap facts (position-keyed replay onto the regenerated
		// entity), then the opened lockables, then the building health. Each returns
		// what the live world took, so a fact the regenerated layer does not have
		// reaches the restore report instead of the log alone.
		var traps = _entityEvents.OnTrapStateProjected(facts.Traps);
		var opened = _buildingEntities.OnOpenedEntitiesProjected(facts.Opened);
		var health = _buildingEntities.OnBuildingHealthProjected(facts.Health);
		return new LiveWorldWriteOutcome(
			traps.Applied + opened.Applied + health.Applied,
			traps.Refused + opened.Refused + health.Refused);
	}

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
