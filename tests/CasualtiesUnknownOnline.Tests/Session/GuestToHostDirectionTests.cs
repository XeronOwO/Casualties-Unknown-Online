using CasualtiesUnknownOnline.Runtime.Protocol;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Direction contract, guest-to-host half: a one-way guest→host message is
/// accepted at the host role and dropped at the guest role before any handler
/// runs. Backed by <see cref="NetMessageRegistry"/> through the real composed
/// receivers of <see cref="DirectionProbe"/>; these rows lock the
/// classification — a message whose handler attribute carries the wrong
/// direction fails here. Split from the former single DirectionTests class so
/// xUnit v2 (serial inside a class) does not serialize all three direction
/// families.
/// </summary>
public class GuestToHostDirectionTests(DirectionProbe probe) : IClassFixture<DirectionProbe>
{
	public static TheoryData<NetMsg> GuestToHostMessages => new()
	{
		NetMsg.Handshake,
		NetMsg.HandshakeAckAck,
		NetMsg.TraderAction,
		NetMsg.CarriedInventory,
		NetMsg.ModCommandRequest,
		NetMsg.WorldTimeRequest,
		NetMsg.PlayerInventoryTakeRequest,
		NetMsg.PlayerCarryStartRequest,
		NetMsg.PlayerCarryStopRequest,
		NetMsg.PlayerHealRequest,
		NetMsg.PlayerItemUseRequest,
		NetMsg.PlayerPushRequest,
		NetMsg.TraderRecruitRequest,
		NetMsg.RemoteInventoryOperationRequest,
		NetMsg.MedicalOperationStartRequest,
		NetMsg.MedicalOperationUpdate,
		NetMsg.MedicalOperationEndRequest,
		NetMsg.MedicalOperationCancel,
	};

	[Theory]
	[MemberData(nameof(GuestToHostMessages))]
	public void GuestToHost_AllowedOnHost_RejectedOnGuest(NetMsg msg)
	{
		Assert.True(probe.HostAccepts(msg), $"{msg} must be valid at the host");
		Assert.False(probe.GuestAccepts(msg), $"{msg} must be dropped at the guest");
	}
}
