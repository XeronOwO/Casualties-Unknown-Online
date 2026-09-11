using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The three restored native world facts of ONE cut, taken as a unit: the values
/// the Game Adapter holds between the Continue click and the world-entry seam,
/// because none of them can be written before the world exists.
///
/// Taken ONCE per generation — a second generation must never be handed the same
/// set — which is why the port exposes a TAKE rather than a read.
/// </summary>
public readonly record struct NativeWorldFactRestore(
	IReadOnlyList<KeypadEntryMsg> Keypads,
	IReadOnlyList<GeyserStateEntryMsg> Geysers,
	IReadOnlyList<BlockDamageEntryMsg> BlockDamages)
{
	/// <summary>Nothing pending: what a cut that carried no native row decodes to.</summary>
	public static NativeWorldFactRestore Empty { get; } = new([], [], []);
}
