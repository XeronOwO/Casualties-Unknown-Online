using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// ONE read of every native world table at a single instant — the decided keypad
/// codes, the geyser liquid types and the GAME's own partial-damage list
/// (<c>WorldGeneration.world.blockDamages</c>).
///
/// One call, not three, because a cut is a consistent instant: the three tables
/// belong to the same generated world, and reading them separately would let a
/// generation finish between the reads.
///
/// <see cref="Failure"/> is the reason this type exists rather than plain lists:
/// every one of these tables exists only while a GENERATED world object does. A
/// reader that met no world used to return an empty list, which the save layer
/// could not tell apart from "this world has no damage" — a cut would then store
/// a clean world and a restore would silently drop every crack the game was
/// holding. A failure is therefore reported, and the cut REFUSES (nothing is
/// written) instead of writing a lossy snapshot.
/// </summary>
/// <param name="Keypads">Every keypad code the host decided for this layer.</param>
/// <param name="Geysers">Every geyser's decided liquid type.</param>
/// <param name="BlockDamages">Every entry of the game's own partial block-damage list.</param>
/// <param name="Failure">Why the tables could not be read; null when the read succeeded (an empty list is then a real empty table).</param>
public readonly record struct NativeWorldFactCapture(
	IReadOnlyList<KeypadEntryMsg> Keypads,
	IReadOnlyList<GeyserStateEntryMsg> Geysers,
	IReadOnlyList<BlockDamageEntryMsg> BlockDamages,
	string? Failure)
{
	/// <summary>The live world is not there (or is still generating): no native table could be read, and the cut must be refused.</summary>
	public static NativeWorldFactCapture Unreadable(string failure) => new([], [], [], failure);
}
