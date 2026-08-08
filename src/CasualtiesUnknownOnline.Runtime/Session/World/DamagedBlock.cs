namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// A block whose state deviates from the generated baseline (host-side damage
/// table). Integer block-space coordinates; the domain-side twin of
/// <see cref="Protocol.Messages.BlockStateEntryMsg"/>
/// (handlers stay on domain types, the wire form lives in the protocol layer).
/// </summary>
public readonly record struct DamagedBlock(int X, int Y, ushort Block);
