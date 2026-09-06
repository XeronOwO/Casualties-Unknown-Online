using CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session.AdaptiveSync;

public class AdaptiveStreamCatalogTests
{
	[Fact]
	public void EveryAdaptiveStream_IsLatestWinsInStage1()
	{
		Assert.NotEmpty(AdaptiveStreamCatalog.All);
		Assert.All(AdaptiveStreamCatalog.All, p =>
			Assert.Equal(AdaptiveStreamDeliveryMode.LatestWins, p.DeliveryMode));
	}

	[Fact]
	public void Catalog_ContainsTheFourStage1Streams()
	{
		Assert.True(AdaptiveStreamCatalog.TryGet(AdaptiveStreamId.PlayerStateBroadcast, out _));
		Assert.True(AdaptiveStreamCatalog.TryGet(AdaptiveStreamId.PlayerStateReport, out _));
		Assert.True(AdaptiveStreamCatalog.TryGet(AdaptiveStreamId.EnemyStateBroadcast, out _));
		Assert.True(AdaptiveStreamCatalog.TryGet(AdaptiveStreamId.TutorialClawBroadcast, out _));
	}

	[Fact]
	public void AllProfiles_HaveValidHzRange()
	{
		Assert.All(AdaptiveStreamCatalog.All, p =>
		{
			Assert.True(p.MinHz > 0);
			Assert.True(p.MaxHz >= p.MinHz);
		});
	}
}
