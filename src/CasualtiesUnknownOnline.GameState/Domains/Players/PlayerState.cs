namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>
/// Authoritative terminal player fact in the kernel. High-frequency movement
/// fields remain a stream; only durable terminal status belongs here,
/// including the cross-player carry relation (one carrier / one carried).
/// </summary>
public sealed record PlayerState(
	ulong SteamId,
	bool Alive,
	bool Conscious,
	ulong? CarrierOfSteamId = null,
	ulong? CarriedBySteamId = null)
{
	public PlayerState WithCarry(ulong? carrierOf, ulong? carriedBy) =>
		this with
		{
			CarrierOfSteamId = carrierOf,
			CarriedBySteamId = carriedBy,
		};
}
