namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>
/// One limb's authoritative terminal latch facts in the kernel. Continuous
/// per-limb fields (skin/muscle health, pain, bleed, timers) remain in the
/// high-frequency character snapshot/stream; only discrete durable latches and
/// anatomical identity belong to the domain.
/// </summary>
public sealed record PlayerLimbState(
	int Index,
	bool Broken,
	bool Dismembered,
	bool Dislocated,
	bool Splinted,
	bool Infected,
	bool BlockedBleeding,
	bool IsHead,
	bool IsVital);
