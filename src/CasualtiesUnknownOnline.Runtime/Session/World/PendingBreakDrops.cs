using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>One cell's unacknowledged break drops in the wire shape the re-report sends.</summary>
/// <param name="X">The cell's x coordinate.</param>
/// <param name="Y">The cell's y coordinate.</param>
/// <param name="PosX">The reported break position's x (the wire's world position).</param>
/// <param name="PosY">The reported break position's y.</param>
/// <param name="Drops">The break's block drops still unacknowledged.</param>
/// <param name="BuildingDrops">The break's building-death drops still unacknowledged.</param>
public sealed record PendingBreakDrops(
	int X,
	int Y,
	float PosX,
	float PosY,
	IReadOnlyList<BlockDropEntryMsg> Drops,
	IReadOnlyList<TrapDropEntryMsg> BuildingDrops);
