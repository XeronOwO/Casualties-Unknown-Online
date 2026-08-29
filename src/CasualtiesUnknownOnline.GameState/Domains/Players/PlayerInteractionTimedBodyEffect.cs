namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>
/// Kernel-shaped timed body effect carried by a cross-player item-use result.
/// The effect is presentation/effect data, not a terminal fact: the target's
/// local body runs the exact native tick op and re-reports through the normal
/// character snapshot path.
/// </summary>
public sealed record PlayerInteractionTimedBodyEffect(
	string EffectId,
	float DurationSeconds,
	float DoseMl);
