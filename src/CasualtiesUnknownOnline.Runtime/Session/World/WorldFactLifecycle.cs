using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The save/restore lifecycle of the Runtime-owned world facts: capture the
/// block difference table, the partial block damage and the radiation line as
/// the WIRE shapes a late joiner already receives, then put them back
/// absolutely (<see cref="IWorldFactSource"/>).
///
/// The tables themselves stay where they are owned — the block difference table
/// and the radiation line in <see cref="WorldStateMessageService"/>, the partial
/// damage in <see cref="BlockDamageRegistry"/> — so this type holds no state and
/// only sequences them: one capture builds one cut's payload, one apply is
/// always reset-then-apply, never an incremental merge (the restored cut is the
/// whole truth for these tables).
/// </summary>
internal sealed class WorldFactLifecycle(
	WorldStateMessageService messages,
	BlockDamageRegistry registry,
	ILogger<WorldService> log) : IWorldFactSource
{
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
	public IReadOnlyList<BlockDamageEntryMsg> CaptureBlockDamages()
	{
		if (!Authoritative("capture block damages"))
		{
			return [];
		}

		var damages = registry.CaptureEntries();
		log.LogDebug("[SaveFacts] captured {Count} partial block-damage row(s).", damages.Count);
		return damages;
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
	/// layer boundary never touched before the save layer existed.
	/// </summary>
	internal void ResetWorldDomainTables()
	{
		messages.ResetPerLayer();
		log.LogDebug("[SaveFacts] per-layer world-domain tables reset (blocks, partial damage, kernel world entities).");
	}

	/// <inheritdoc />
	public void ApplyFacts(
		IReadOnlyList<BlockStateEntryMsg> blockStates,
		IReadOnlyList<BlockDamageEntryMsg> blockDamages,
		RadiationLineStateMsg? radiationLine)
	{
		if (!Authoritative("apply restored world facts"))
		{
			return;
		}

		// Reset first, and the RESTORE reset is the save layer's own: the restored
		// cut names the whole world-fact set, so a fact left over from the previous
		// session — including a radiation line the cut does not carry — would be one
		// the cut never had. It deliberately leaves the KERNEL-backed world-entity
		// tables alone: those were just restored from the checkpoint and are not
		// this layer's facts to clear.
		messages.ResetForRestore();

		if (blockStates.Count > 0)
		{
			foreach (var entry in blockStates)
			{
				messages.ApplyBlockState(entry);
			}

			log.LogInformation("[SaveFacts] applied {Count} restored block-state row(s).", blockStates.Count);
		}

		if (blockDamages.Count > 0)
		{
			foreach (var entry in blockDamages)
			{
				// The same upsert the live report performs (latest wins, a
				// non-positive value is not a record) — the table is already empty
				// after the reset above, so this is an absolute set.
				registry.Report(entry.X, entry.Y, entry.Damage);
			}

			log.LogInformation("[SaveFacts] applied {Count} restored partial block-damage row(s).", blockDamages.Count);
		}

		if (radiationLine is not null)
		{
			messages.SetRadiationLineState(radiationLine);
			log.LogInformation("[SaveFacts] applied the restored radiation line (active={Active}, timeGone={TimeGone:F2}).",
				radiationLine.Active, radiationLine.TimeGone);
		}

		log.LogInformation(
			"[SaveFacts] restored world facts: {Blocks} block(s), {Damages} partial damage(s), radiation line {Radiation}.",
			blockStates.Count, blockDamages.Count, radiationLine is null ? "absent" : "applied");
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
