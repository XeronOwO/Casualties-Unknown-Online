using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.PlayerInteractionTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Wire contract: the player-interaction request messages round-trip their limb-selection fields.
/// Split from PlayerInteractionServiceTests so xUnit v2 (one collection per class,
/// serial inside a class) does not serialize the whole player-interaction surface.
/// </summary>
[Trait("Category", "Integration")]
public class PlayerInteractionProtocolTests
{
	[Fact]
	public void HealRequest_RoundTripsSelectedLimbIndex()
	{
		var msg = new PlayerHealRequestMsg
		{
			TargetSteamId = HostId,
			ItemInstanceId = 42,
			LimbIndex = 0,
		};

		var decoded = NetPacket.DecodePayload<PlayerHealRequestMsg>(
			NetPacket.Encode(NetMsg.PlayerHealRequest, msg));

		Assert.Equal(HostId, decoded.TargetSteamId);
		Assert.Equal(42UL, decoded.ItemInstanceId);
		Assert.Equal(0, decoded.LimbIndex);
	}

	[Fact]
	public void HealRequest_RoundTripsAutoLimbSelection()
	{
		var msg = new PlayerHealRequestMsg
		{
			TargetSteamId = HostId,
			ItemInstanceId = 42,
			LimbIndex = -1,
		};

		var decoded = NetPacket.DecodePayload<PlayerHealRequestMsg>(
			NetPacket.Encode(NetMsg.PlayerHealRequest, msg));

		Assert.Equal(-1, decoded.LimbIndex);
	}

	[Fact]
	public void UseRequest_RoundTripsSelectedLimbIndex()
	{
		var msg = new PlayerItemUseRequestMsg
		{
			TargetSteamId = HostId,
			ItemInstanceId = 42,
			LimbIndex = 2,
		};

		var decoded = NetPacket.DecodePayload<PlayerItemUseRequestMsg>(
			NetPacket.Encode(NetMsg.PlayerItemUseRequest, msg));

		Assert.Equal(HostId, decoded.TargetSteamId);
		Assert.Equal(42UL, decoded.ItemInstanceId);
		Assert.Equal(2, decoded.LimbIndex);
	}
}
