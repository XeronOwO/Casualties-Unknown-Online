namespace CasualtiesUnknownOnline.GameState.Domains.Entities;

/// <summary>
/// Kernel-side entity identity: session epoch, host allocation counter, and
/// generation. Mirrors the Runtime NetworkEntityId without referencing it.
/// </summary>
public readonly record struct EntityId(ulong Epoch, uint Counter, byte Generation);
