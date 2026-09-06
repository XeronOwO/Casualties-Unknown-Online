using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session.AdaptiveSync;

public class AdaptiveStreamWireMapperTests
{
	[Theory]
	[InlineData(AdaptiveStreamId.PlayerStateBroadcast, WirePayloadType.PlayerStateStream)]
	[InlineData(AdaptiveStreamId.PlayerStateReport, WirePayloadType.PlayerStateStream)]
	[InlineData(AdaptiveStreamId.EnemyStateBroadcast, WirePayloadType.EnemyStateStream)]
	public void KernelStreams_MapToPayloadType(AdaptiveStreamId streamId, WirePayloadType expected)
	{
		Assert.True(AdaptiveStreamWireMapper.TryGet(streamId, out var payloadType, out var message));

		Assert.Equal(expected, payloadType);
		Assert.Null(message);
	}

	[Fact]
	public void TutorialClaw_MapToDirectMessage()
	{
		Assert.True(AdaptiveStreamWireMapper.TryGet(AdaptiveStreamId.TutorialClawBroadcast, out var payloadType, out var message));

		Assert.Null(payloadType);
		Assert.Equal(NetMsg.TutorialClawState, message);
	}

	[Fact]
	public void UnknownStream_ReturnsFalse() =>
		Assert.False(AdaptiveStreamWireMapper.TryGet((AdaptiveStreamId)999, out _, out _));

	[Fact]
	public void EveryCatalogStream_IsMappedByWireMapper()
	{
		Assert.All(AdaptiveStreamCatalog.All, profile =>
			Assert.True(
				AdaptiveStreamWireMapper.TryGet(profile.Id, out _, out _),
				$"catalog stream {profile.Id} has no wire mapping"));
	}
}
