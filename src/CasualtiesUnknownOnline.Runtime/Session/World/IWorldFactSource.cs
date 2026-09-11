using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The world facts no kernel domain owns, read and rewritten as one unit so a
/// cut can carry them and a restore can put them back (the save system's world
/// diff). Three tables belong here, all of them already host-authoritative and
/// already shipped to a late joiner over the wire:
///
/// - the block difference table (block-space cell → current block id) — mined,
///   destroyed, built and reverted blocks;
/// - the partial block damage accumulated on blocks that have not broken;
/// - the radiation line's active flag and descent.
///
/// The shapes are the Protocol WIRE DTOs on purpose: a restore hands the same
/// DTOs to the same appliers the late-joiner snapshot uses, so there is one
/// validation path instead of two.
///
/// <see cref="ApplyFacts"/> is ABSOLUTE and resets first: the restored cut is the
/// whole truth for these tables, and a fact left over from the previous session
/// would be one the cut never had. There is deliberately no incremental variant.
/// </summary>
public interface IWorldFactSource
{
	/// <summary>The current block difference table (empty when nothing deviates from the generated baseline).</summary>
	IReadOnlyList<BlockStateEntryMsg> CaptureBlockStates();

	/// <summary>The current partial block damage (empty when no block carries accumulated damage).</summary>
	IReadOnlyList<BlockDamageEntryMsg> CaptureBlockDamages();

	/// <summary>The radiation line's host-authoritative state, or null when this world never had one.</summary>
	RadiationLineStateMsg? CaptureRadiationLine();

	/// <summary>Host only: apply a restored cut absolutely — the table is REPLACED, never merged.</summary>
	WorldFactApplyReport ApplyFacts(
		IReadOnlyList<BlockStateEntryMsg> blockStates,
		IReadOnlyList<BlockDamageEntryMsg> blockDamages,
		RadiationLineStateMsg? radiationLine);

	/// <summary>
	/// Host only: a restore put facts into these tables and the LIVE WORLD has
	/// not consumed them yet. The adapter's world-entry hook reads this to keep
	/// the restored facts instead of running the layer-boundary reset (which
	/// exists for a NEWLY generated layer) and to write them into the freshly
	/// generated world; it calls <see cref="ClearPendingLiveReplay"/> when the
	/// world has them. False for every normal generation, and false for a
	/// restored cut that carried no world fact at all (a layer-end cut).
	/// </summary>
	bool HasPendingLiveReplay { get; }

	/// <summary>
	/// The pending restore is done with: the live world has it (the adapter's
	/// replay), or a new run superseded it (the save layer's next
	/// <c>TryBeginRun</c>). Never called while a generation is still consuming
	/// the values — the flag exists to survive exactly that window.
	/// </summary>
	void ClearPendingLiveReplay();
}
