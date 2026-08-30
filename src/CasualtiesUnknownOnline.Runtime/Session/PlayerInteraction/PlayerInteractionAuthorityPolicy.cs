using System;
using CasualtiesUnknownOnline.GameState;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Single source for the authority policy of each cross-player interaction.
/// 4.3 keeps cross-player take/heal/use/carry host-validated with no client
/// prediction; push is presentation-only and creates no kernel command/event.
/// </summary>
public static class PlayerInteractionAuthorityPolicy
{
	public static PlayerInteractionAuthority Take => PlayerInteractionAuthority.HostValidatedNoPrediction;

	public static PlayerInteractionAuthority Heal => PlayerInteractionAuthority.HostValidatedNoPrediction;

	public static PlayerInteractionAuthority Use => PlayerInteractionAuthority.HostValidatedNoPrediction;

	public static PlayerInteractionAuthority CarrySet => PlayerInteractionAuthority.HostValidatedNoPrediction;

	public static PlayerInteractionAuthority CarryClear => PlayerInteractionAuthority.HostValidatedNoPrediction;

	public static PlayerInteractionAuthority Push => PlayerInteractionAuthority.PresentationOnly;

	public static AuthorityKind ToKernelAuthority(PlayerInteractionAuthority authority) =>
		authority switch
		{
			PlayerInteractionAuthority.HostValidatedNoPrediction => AuthorityKind.HostOnly,
			PlayerInteractionAuthority.OwnerPredictedHostValidated => AuthorityKind.OwnerPredictedHostValidated,
			PlayerInteractionAuthority.PresentationOnly => AuthorityKind.PresentationOnly,
			_ => throw new ArgumentOutOfRangeException(nameof(authority), authority, null),
		};
}
