using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>One cell's unacknowledged break drops in the wire shape the re-report sends.</summary>
/// <param name="X">The cell's x coordinate — the key the break message itself carries (protocol 32; the world position it used to derive the cell from is gone).</param>
/// <param name="Y">The cell's y coordinate.</param>
/// <param name="Drops">The break's block drops still unacknowledged.</param>
/// <param name="BuildingDrops">The break's building-death drops still unacknowledged.</param>
public sealed record PendingBreakDrops(
	int X,
	int Y,
	IReadOnlyList<BlockDropEntryMsg> Drops,
	IReadOnlyList<TrapDropEntryMsg> BuildingDrops);
