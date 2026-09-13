using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The save/restore lifecycle of the Runtime-owned world facts: capture the
/// block difference table and the radiation line as the WIRE shapes a late
/// joiner already receives, then put them back absolutely
/// (<see cref="IWorldFactSource"/>).
///
/// The tables themselves stay where they are owned — both in
/// <see cref="WorldStateMessageService"/> — so this type holds no state beyond
/// the pending-replay marker: one capture builds one cut's payload, one apply is
/// always reset-then-apply, never an incremental merge (the restored cut is the
/// whole truth for these tables).
///
/// The GAME's own tables are NOT part of this lifecycle: their partial block
/// damage has no Runtime table to sequence, and the adapter owns both halves
/// through <see cref="INativeWorldFacts"/>.
///
/// The marker exists because a restored cut is NOT a new layer: the host
/// regenerates the saved layer and must then write the restored facts into it,
/// while a newly generated layer legitimately starts from empty tables. The
/// adapter's world-entry hook reads it (through
/// <see cref="IWorldFactSource.HasPendingLiveReplay"/>) and clears it once the
/// live world has the facts.
/// </summary>
internal sealed class WorldFactLifecycle(
	WorldStateMessageService messages,
	ILogger<WorldService> log) : IWorldFactSource
{
	/// <summary>
	/// Host: a restore put facts into the tables and the live world has not
	/// consumed them yet. Set by <see cref="ApplyFacts"/>, cleared by the
	/// adapter's world-entry replay (or by the next run, or by a layer reset).
	/// </summary>
	private bool _pendingLiveReplay;

	/// <summary>
	/// Which restore attempt applied these tables (see
	/// <see cref="AppliedRestoreSequence"/>). Stamped on EVERY apply, armed or not:
	/// the "carried nothing" report the seam makes for a layer-end cut is a statement
	/// about the same attempt.
	/// </summary>
	private ulong _appliedRestoreSequence;

	/// <inheritdoc />
	public bool HasPendingLiveReplay => _pendingLiveReplay;

	/// <inheritdoc />
	public ulong AppliedRestoreSequence => _appliedRestoreSequence;

	/// <inheritdoc />
	public void ClearPendingLiveReplay()
	{
		if (!_pendingLiveReplay)
		{
			return;
		}

		_pendingLiveReplay = false;
		log.LogDebug("[SaveFacts] the restored world facts are no longer pending: the live world has them (or a new run superseded them).");
	}

	/// <inheritdoc />
	public IReadOnlyList<BlockStateEntryMsg> CaptureBlockStates()
	{
		if (!Authoritative("capture block states"))
		{
			return [];
		}

		var blocks = messages.CaptureBlockStates();
		log.LogDebug("[SaveFacts] captured {Count} block-state row(s).", blocks.Count);
		return blocks;
	}

	/// <inheritdoc />
	public RadiationLineStateMsg? CaptureRadiationLine()
	{
		if (!Authoritative("capture the radiation line"))
		{
			return null;
		}

		var state = messages.RadiationLineState;
		if (state is null)
		{
			log.LogDebug("[SaveFacts] captured radiation line: none.");
			return null;
		}

		log.LogDebug("[SaveFacts] captured radiation line: active={Active}, timeGone={TimeGone:F2}.", state.Active, state.TimeGone);
		return state;
	}

	/// <summary>
	/// Host only: the world-domain reset bus for a NEW LAYER — the per-layer facts
	/// start empty. It deliberately keeps the radiation line: that is run state the
	/// layer boundary never touched before the save layer existed. A reset also
	/// ends any pending restore replay: the facts it was waiting for are gone with
	/// the reset, so the live world must not be handed them later.
	/// </summary>
	internal void ResetWorldDomainTables()
	{
		_pendingLiveReplay = false;
		messages.ResetPerLayer();
		log.LogDebug("[SaveFacts] per-layer Runtime world-domain tables reset (blocks, kernel world entities); the game's own damage list is regenerated with the layer.");
	}

	/// <inheritdoc />
	public WorldFactApplyReport ApplyFacts(
		IReadOnlyList<BlockStateEntryMsg> blockStates,
		RadiationLineStateMsg? radiationLine,
		ulong restoreSequence)
	{
		if (!Authoritative("apply restored world facts"))
		{
			return new WorldFactApplyReport(0, 0, false);
		}

		// The identity of the attempt these tables now hold, stamped before anything
		// else: the seam's contribution belongs to THIS restore, arming a replay or not.
		_appliedRestoreSequence = restoreSequence;

		// Reset first, and the RESTORE reset is the save layer's own: the restored
		// cut names the whole world-fact set, so a fact left over from the previous
		// session — including a radiation line the cut does not carry — would be one
		// the cut never had. It deliberately leaves the KERNEL-backed world-entity
		// tables alone: those were just restored from the checkpoint and are not
		// this layer's facts to clear.
		messages.ResetForRestore();

		var blockStatesApplied = 0;
		var blockStatesRefused = 0;
		if (blockStates.Count > 0)
		{
			foreach (var entry in blockStates)
			{
				if (messages.ApplyBlockState(entry))
				{
					blockStatesApplied++;
				}
				else
				{
					blockStatesRefused++;
				}
			}

			log.LogInformation("[SaveFacts] applied {Count} restored block-state row(s) ({Refused} refused).",
				blockStatesApplied, blockStatesRefused);
		}

		if (radiationLine is not null)
		{
			messages.SetRadiationLineState(radiationLine);
			log.LogInformation("[SaveFacts] applied the restored radiation line (active={Active}, timeGone={TimeGone:F2}).",
				radiationLine.Active, radiationLine.TimeGone);
		}

		// A cut that carried a world fact owns the next generation's cache state:
		// the world-entry reset must keep these tables (they ARE the restored
		// layer's facts, not a previous layer's leftovers) and the adapter must
		// write them into the freshly generated world. A cut that carried nothing
		// (a layer-end cut) leaves the normal layer lifecycle alone.
		_pendingLiveReplay = blockStates.Count > 0 || radiationLine is not null;

		log.LogInformation(
			"[SaveFacts] restored world facts: {Blocks} block(s), radiation line {Radiation}, live-world replay {Replay}.",
			blockStatesApplied, radiationLine is null ? "absent" : "applied",
			_pendingLiveReplay ? "pending" : "not needed");

		return new WorldFactApplyReport(blockStatesApplied, blockStatesRefused, radiationLine is not null);
	}

	/// <summary>
	/// The cut and the restore are both SAVE operations, so the predicate is "not a
	/// guest" rather than "is exactly host": the host entry point
	/// (<c>WorldSaveService.TryBeginRun</c>) refuses guests and accepts everything
	/// else, which includes the no-lobby state the game reports as
	/// <see cref="SessionRole.None"/> before any lobby exists.
	/// </summary>
	private bool Authoritative(string operation)
	{
		if (messages.Role != SessionRole.Guest)
		{
			return true;
		}

		log.LogError("[SaveFacts] refusing to {Operation}: this peer is a guest, and the host is the only save authority (decision 164).", operation);
		return false;
	}
}
