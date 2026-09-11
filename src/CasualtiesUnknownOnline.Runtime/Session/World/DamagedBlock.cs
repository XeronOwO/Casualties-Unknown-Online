namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// A block whose state deviates from the generated baseline (host-side damage
/// table). Integer block-space coordinates; the domain-side twin of
/// <see cref="Protocol.Messages.BlockStateEntryMsg"/>
/// (handlers stay on domain types, the wire form lives in the protocol layer).
/// </summary>
/// <param name="SupportLossSettled">
/// True when the cell's air transition already had its building support loss
/// settled in the world that wrote the row (a restored row), so the receiver must
/// apply the cell without re-running the settlement.
/// </param>
public readonly record struct DamagedBlock(int X, int Y, ushort Block, bool SupportLossSettled = false);
