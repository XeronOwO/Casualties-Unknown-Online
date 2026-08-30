using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

public class PlayerInteractionAuthorityPolicyTests
{
	[Fact]
	public void CrossPlayerOperations_AreHostValidatedNoPrediction()
	{
		Assert.Equal(PlayerInteractionAuthority.HostValidatedNoPrediction, PlayerInteractionAuthorityPolicy.Take);
		Assert.Equal(PlayerInteractionAuthority.HostValidatedNoPrediction, PlayerInteractionAuthorityPolicy.Heal);
		Assert.Equal(PlayerInteractionAuthority.HostValidatedNoPrediction, PlayerInteractionAuthorityPolicy.Use);
		Assert.Equal(PlayerInteractionAuthority.HostValidatedNoPrediction, PlayerInteractionAuthorityPolicy.CarrySet);
		Assert.Equal(PlayerInteractionAuthority.HostValidatedNoPrediction, PlayerInteractionAuthorityPolicy.CarryClear);
	}

	[Fact]
	public void Push_IsPresentationOnly() =>
		Assert.Equal(PlayerInteractionAuthority.PresentationOnly, PlayerInteractionAuthorityPolicy.Push);

	[Fact]
	public void ToKernelAuthority_MapsPolicies()
	{
		Assert.Equal(AuthorityKind.HostOnly, PlayerInteractionAuthorityPolicy.ToKernelAuthority(PlayerInteractionAuthority.HostValidatedNoPrediction));
		Assert.Equal(AuthorityKind.OwnerPredictedHostValidated, PlayerInteractionAuthorityPolicy.ToKernelAuthority(PlayerInteractionAuthority.OwnerPredictedHostValidated));
		Assert.Equal(AuthorityKind.PresentationOnly, PlayerInteractionAuthorityPolicy.ToKernelAuthority(PlayerInteractionAuthority.PresentationOnly));
	}
}
