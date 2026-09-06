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
	public void MedicalStreams_MapToDedicatedPayloadTypes()
	{
		Assert.True(AdaptiveStreamWireMapper.TryGet(AdaptiveStreamId.MedicalInjectionReport, out var injectionPayload, out var injectionMessage));
		Assert.Equal(WirePayloadType.MedicalInjectionUpdate, injectionPayload);
		Assert.Null(injectionMessage);

		Assert.True(AdaptiveStreamWireMapper.TryGet(AdaptiveStreamId.ShrapnelPositionReport, out var shrapnelPayload, out var shrapnelMessage));
		Assert.Equal(WirePayloadType.MedicalShrapnelPositionUpdate, shrapnelPayload);
		Assert.Null(shrapnelMessage);
	}

	[Fact]
	public void Stage4Streams_MapToWireObservations()
	{
		Assert.True(AdaptiveStreamWireMapper.TryGet(AdaptiveStreamId.WorldItemMoveStream, out var itemMovePayload, out var itemMoveMessage));
		Assert.Equal(WirePayloadType.StateStream, itemMovePayload);
		Assert.Null(itemMoveMessage);

		Assert.True(AdaptiveStreamWireMapper.TryGet(AdaptiveStreamId.WorldItemSnapshotStream, out var itemSnapshotPayload, out var itemSnapshotMessage));
		Assert.Equal(WirePayloadType.ItemSnapshotStream, itemSnapshotPayload);
		Assert.Null(itemSnapshotMessage);

		Assert.True(AdaptiveStreamWireMapper.TryGet(AdaptiveStreamId.FluidRegionDiffStream, out var fluidDiffPayload, out var fluidDiffMessage));
		Assert.Equal(WirePayloadType.FluidRegionDiff, fluidDiffPayload);
		Assert.Null(fluidDiffMessage);

		Assert.True(AdaptiveStreamWireMapper.TryGet(AdaptiveStreamId.FluidRegionFullStream, out var fluidFullPayload, out var fluidFullMessage));
		Assert.Equal(WirePayloadType.FluidRegionFull, fluidFullPayload);
		Assert.Null(fluidFullMessage);

		Assert.True(AdaptiveStreamWireMapper.TryGet(AdaptiveStreamId.TraderStateStream, out var traderPayload, out var traderMessage));
		Assert.Null(traderPayload);
		Assert.Equal(NetMsg.TraderState, traderMessage);
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
