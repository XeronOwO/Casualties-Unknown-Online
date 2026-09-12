using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The LIVE-WORLD half of a restored-world replay: the writes only the Game
/// Adapter can perform, because only it holds the game's own types. The Runtime
/// owns everything else — which restored facts a cut carries, the order they land
/// in, what is accounted and when the pending handover is finished — through
/// <see cref="RestoredWorldFactReplay"/>, so a replay is verifiable without a
/// running game.
///
/// Every write is ABSOLUTE: the restored cut is the whole truth for each table a
/// method names, and none of them merges.
/// </summary>
internal interface IRestoredWorldFactSink
{
	/// <summary>True when the live world exists and its generation has completed — the world-entry seam's precondition.</summary>
	bool IsWorldReady { get; }

	/// <summary>
	/// Write the restored block difference into the freshly generated world.
	/// Returns the rows it actually changed and the rows it could NOT write — the
	/// readiness check and this write are not atomic, so a world that vanished in
	/// between refuses every row, and the replay must not treat that as restored.
	/// </summary>
	LiveWorldWriteOutcome WriteBlockStates(IReadOnlyList<BlockStateEntryMsg> states);

	/// <summary>
	/// Replace the game's own partial-damage list with the restored rows — the cut
	/// is the whole truth for it, so the list is emptied first. Returns the rows the
	/// write took and the rows it refused; a world that vanished in between refuses
	/// every row.
	/// </summary>
	LiveWorldWriteOutcome ReplaceGameBlockDamages(IReadOnlyList<BlockDamageEntryMsg> rows);

	/// <summary>Write the restored keypad codes onto the live Openables. Returns the codes applied and the codes the live world had no Openable for.</summary>
	LiveWorldWriteOutcome ApplyKeypadCodes(IReadOnlyList<KeypadEntryMsg> codes);

	/// <summary>Write the restored geyser liquid types. Returns the entries applied and the entries the live world had no geyser for.</summary>
	LiveWorldWriteOutcome ApplyGeysers(IReadOnlyList<GeyserStateEntryMsg> geysers);

	/// <summary>
	/// Write the restored recipe unlock table. It lands HERE rather than at the
	/// native save slot because the game rebuilds <c>Recipes.recipes</c> in
	/// <c>WorldGeneration.Awake</c> and CUO's mod-content provider appends the custom
	/// recipes on a later Update frame: by the world-entry edge the table is the one
	/// this world will use, so a saved row's index means the recipe it meant. Returns
	/// the rows the live table took and the rows it has no recipe for (a mod update
	/// removed it) — the restore report names those.
	/// </summary>
	LiveWorldWriteOutcome ApplyRecipeUnlocks(IReadOnlyList<SaveRecipeUnlockRow> recipes);

	/// <summary>Write the restored radiation line. False when the live world has no line object to write it onto.</summary>
	bool ApplyRadiationLine(RadiationLineStateMsg line);
}
