using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>
/// Authoritative terminal player fact in the kernel. High-frequency movement
/// fields remain a stream; only durable terminal status belongs here,
/// including the cross-player carry relation (one carrier / one carried) and
/// the discrete limb latch set.
/// </summary>
public sealed record PlayerState(
	ulong SteamId,
	bool Alive,
	bool Conscious,
	ulong? CarrierOfSteamId = null,
	ulong? CarriedBySteamId = null,
	IReadOnlyList<PlayerLimbState>? Limbs = null)
{
	public IReadOnlyList<PlayerLimbState> LimbFacts => Limbs ?? [];

	public PlayerState WithCarry(ulong? carrierOf, ulong? carriedBy) =>
		this with
		{
			CarrierOfSteamId = carrierOf,
			CarriedBySteamId = carriedBy,
		};

	public PlayerState WithVitals(bool alive, bool conscious) =>
		this with
		{
			Alive = alive,
			Conscious = conscious,
		};

	public PlayerState WithLimbs(IReadOnlyList<PlayerLimbState> limbs) =>
		this with
		{
			Limbs = limbs,
		};
}
