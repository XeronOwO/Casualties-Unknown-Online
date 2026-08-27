using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The ragdoll one-shot / entity-state race gate. The reliable
/// <c>CharacterRagdoll</c> event must not be overwritten by an older
/// <c>Standing=true</c> 20 Hz snapshot that is still in flight; but once the
/// stream confirms <c>Standing=false</c> (or the suppression window expires),
/// the next <c>Standing=true</c> is a real stand-up and must be allowed.
/// </summary>
public class RagdollPoseGateTests
{
	private const long Now = 10_000;
	private const long EventMs = Now - 100;

	[Fact]
	public void StaleStandingTrue_IsSuppressedWhilePendingAndNotConfirmed()
	{
		Assert.True(RagdollPoseGate.ShouldSuppressStanding(
			entityStanding: true,
			collapsePending: true,
			collapseConfirmed: false,
			collapseMs: EventMs,
			nowMs: Now),
			"a stale true snapshot arriving right after the ragdoll event must not stand the clone up");
	}

	[Fact]
	public void StandingFalseConfirms_ThenStandingTrueIsAllowed()
	{
		Assert.False(RagdollPoseGate.ShouldSuppressStanding(
			entityStanding: true,
			collapsePending: true,
			collapseConfirmed: true,
			collapseMs: EventMs,
			nowMs: Now),
			"after the state stream has confirmed the collapse, standing=true is a real stand-up");
	}

	[Fact]
	public void SuppressionWindowExpiry_AllowsStandingTrue()
	{
		Assert.False(RagdollPoseGate.ShouldSuppressStanding(
			entityStanding: true,
			collapsePending: true,
			collapseConfirmed: false,
			collapseMs: EventMs - RagdollPoseGate.SuppressWindowMs - 1,
			nowMs: Now),
			"once the suppression window expires the authoritative stream wins");
	}

	[Fact]
	public void NoPendingEvent_AllowsStandingTrue()
	{
		Assert.False(RagdollPoseGate.ShouldSuppressStanding(
			entityStanding: true,
			collapsePending: false,
			collapseConfirmed: false,
			collapseMs: EventMs,
			nowMs: Now));
	}

	[Fact]
	public void StandingFalse_NeverSuppresses()
	{
		Assert.False(RagdollPoseGate.ShouldSuppressStanding(
			entityStanding: false,
			collapsePending: true,
			collapseConfirmed: false,
			collapseMs: EventMs,
			nowMs: Now));
	}
}
