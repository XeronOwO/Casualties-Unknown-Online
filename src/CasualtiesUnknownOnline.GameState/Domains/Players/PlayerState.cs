namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>
/// Authoritative terminal player fact in the kernel. High-frequency movement
/// fields remain a stream; only durable terminal status belongs here.
/// </summary>
public sealed record PlayerState(
	ulong SteamId,
	bool Alive,
	bool Conscious);
