namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>
/// Kernel-shaped timed limb effect carried by a cross-player item-use result.
/// The effect is presentation/effect data, not a terminal fact: the target's
/// local body owns the exact native tick lambda and re-reports through the
/// normal character snapshot path.
/// </summary>
public sealed record PlayerInteractionTimedLimbEffect(
	int LimbIndex,
	float DurationSeconds,
	float BleedPerSecond);
