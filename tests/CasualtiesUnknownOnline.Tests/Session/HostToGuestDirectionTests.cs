using CasualtiesUnknownOnline.Runtime.Protocol;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Direction contract, host-to-guest half: a one-way host→guest message is
/// accepted at the guest role and dropped at the host role before any handler
/// runs. Backed by <see cref="NetMessageRegistry"/> through the real composed
/// receivers of <see cref="DirectionProbe"/>; these rows lock the
/// classification. Split from the former single DirectionTests class so xUnit
/// v2 (serial inside a class) does not serialize all three direction families.
/// </summary>
public class HostToGuestDirectionTests(DirectionProbe probe) : IClassFixture<DirectionProbe>
{
	public static TheoryData<NetMsg> HostToGuestMessages => new()
	{
		NetMsg.HandshakeAck,
		NetMsg.WorldJoin,
		NetMsg.WorldReady,
		NetMsg.PlayerJoin,
		NetMsg.PlayerLeave,
		NetMsg.WorldBlockState,
		NetMsg.HostCharacterData,
		NetMsg.EarthquakeStart,
		NetMsg.KeypadCode,
		NetMsg.GeyserStateSnapshot,
		NetMsg.FluidRegion,
		NetMsg.TraderState,
		NetMsg.TrapLayoutSnapshot,
		NetMsg.BlockDamageSnapshot,
		NetMsg.EnemySnapshot,
		NetMsg.EnemyAttack,
		NetMsg.ModCommandResult,
		NetMsg.WorldTime,
		NetMsg.FluidPresentation,
		NetMsg.PlayerPushResult,
		NetMsg.RemoteInventoryApply,
		NetMsg.TutorialClawState,
		NetMsg.RadiationLineState,
		NetMsg.TraderRecruitResult,
		NetMsg.WorldSnapshotComplete,
		NetMsg.Kicked,
		NetMsg.Banned,
		NetMsg.MedicalOperationStartAck,
		NetMsg.MedicalOperationState,
		NetMsg.MedicalOperationEndCommitted,
	};

	[Theory]
	[MemberData(nameof(HostToGuestMessages))]
	public void HostToGuest_AllowedOnGuest_RejectedOnHost(NetMsg msg)
	{
		Assert.True(probe.GuestAccepts(msg), $"{msg} must be valid at the guest");
		Assert.False(probe.HostAccepts(msg), $"{msg} must be dropped at the host");
	}
}
