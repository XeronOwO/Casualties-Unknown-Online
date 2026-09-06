using CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session.AdaptiveSync;

public class AdaptiveStreamCatalogTests
{
	[Fact]
	public void EveryAdaptiveStream_HasLossTolerantDeliveryMode()
	{
		Assert.NotEmpty(AdaptiveStreamCatalog.All);
		Assert.All(AdaptiveStreamCatalog.All, p =>
			Assert.True(
				p.DeliveryMode is AdaptiveStreamDeliveryMode.LatestWins or AdaptiveStreamDeliveryMode.Cumulative,
				$"catalog stream {p.Id} must not be reliable control"));
	}

	[Fact]
	public void Catalog_ContainsKnownStreams()
	{
		Assert.True(AdaptiveStreamCatalog.TryGet(AdaptiveStreamId.PlayerStateBroadcast, out _));
		Assert.True(AdaptiveStreamCatalog.TryGet(AdaptiveStreamId.PlayerStateReport, out _));
		Assert.True(AdaptiveStreamCatalog.TryGet(AdaptiveStreamId.EnemyStateBroadcast, out _));
		Assert.True(AdaptiveStreamCatalog.TryGet(AdaptiveStreamId.TutorialClawBroadcast, out _));
		Assert.True(AdaptiveStreamCatalog.TryGet(AdaptiveStreamId.MedicalInjectionReport, out _));
		Assert.True(AdaptiveStreamCatalog.TryGet(AdaptiveStreamId.ShrapnelPositionReport, out _));
	}

	[Fact]
	public void AllProfiles_HaveValidHzRange()
	{
		Assert.All(AdaptiveStreamCatalog.All, p =>
		{
			Assert.True(p.MinHz > 0);
			Assert.True(p.MaxHz >= p.MinHz);
			Assert.True(p.MaxBytesPerSecond >= 0);
			Assert.True(p.BaseHz == 0 || (p.BaseHz >= p.MinHz && p.BaseHz <= p.MaxHz));
		});
	}

	[Fact]
	public void MedicalProgressStreams_UseFrameLevelBaseHz()
	{
		Assert.True(AdaptiveStreamCatalog.TryGet(AdaptiveStreamId.MedicalInjectionReport, out var injection));
		Assert.True(AdaptiveStreamCatalog.TryGet(AdaptiveStreamId.ShrapnelPositionReport, out var shrapnel));

		Assert.Equal(60, injection!.BaseHz);
		Assert.Equal(60, shrapnel!.BaseHz);
		Assert.Equal(AdaptiveStreamDeliveryMode.Cumulative, injection.DeliveryMode);
		Assert.Equal(AdaptiveStreamDeliveryMode.LatestWins, shrapnel.DeliveryMode);
	}
}
