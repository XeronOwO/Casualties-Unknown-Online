namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// One unacknowledged local block write the guest re-reports until the host
/// answers: the cell, the block this side wrote there, and the write's own
/// presentation claim.
/// </summary>
/// <param name="PlayerBreak">
/// True when the write is the block-removal half of a local damage roll. It
/// rides the entry because a recovery re-report is the FIRST time the host
/// learns of that write, so it must carry exactly what the live report carried —
/// and the claim is not derivable from the entry: a guest's own quake break is
/// an air write too (<c>block == 0</c>) and it must stay silent on every other
/// side, exactly as it was silent on the side that ran it.
/// </param>
public readonly record struct PendingBlockReport(int X, int Y, ushort Block, bool PlayerBreak = false);
