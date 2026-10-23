using CasualtiesUnknownOnline.Runtime.Protocol;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Direction contract, host-to-guest half, the DIRECTED-ANSWER family: the messages one host
/// instruction or one member request is answered with, plus the replay instructions. Split from
/// <see cref="HostToGuestDirectionTests"/> when the heal cheat verb's instruction and answer pushed
/// the single family past the per-class case limit — a class is one xUnit collection, so the split
/// is by behaviour family, not by convenience.
/// </summary>
[Trait("Category", "Integration")]
public class HostToGuestInteractionDirectionTests(DirectionProbe probe) : IClassFixture<DirectionProbe>
{
	public static TheoryData<NetMsg> HostToGuestInteractionMessages => new()
	{
		NetMsg.ModCommandResult,
		NetMsg.RemoteInventoryIntent,
		NetMsg.PlayerPlacementQuery,
		NetMsg.TraderRecruitResult,
		NetMsg.MedicalOperationStartAck,
		NetMsg.MedicalOperationState,
		NetMsg.MedicalOperationEndCommitted,
		NetMsg.MedicalOperationTargetCheckRequest,
		NetMsg.InventoryIntentRefused,
		NetMsg.PlayerInventoryGiveResult,
		NetMsg.WorldSummonResult,
		NetMsg.HealResetInstruction,
		NetMsg.HealResetResult,
		NetMsg.HostRuleChangeResult,
		NetMsg.LayerAdvanceRefused,
	};

	[Theory]
	[MemberData(nameof(HostToGuestInteractionMessages))]
	public void HostToGuestInteraction_AllowedOnGuest_RejectedOnHost(NetMsg msg)
	{
		Assert.True(probe.GuestAccepts(msg), $"{msg} must be valid at the guest");
		Assert.False(probe.HostAccepts(msg), $"{msg} must be dropped at the host");
	}
}
