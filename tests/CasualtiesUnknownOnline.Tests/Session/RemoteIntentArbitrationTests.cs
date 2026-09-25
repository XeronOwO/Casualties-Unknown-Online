using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Behavior family: first-writer-wins arbitration of the native inventory
/// intents — one peer at a time per item instance, a lease that a competing peer
/// cannot break, and a table that never grows past the live leases.
/// </summary>
public class RemoteIntentArbitrationTests
{
	private const ulong First = 8001;
	private const ulong Second = 8002;
	private const ulong Item = 42;

	[Fact]
	public void TheFirstRequester_AdmitsAndHoldsTheItem()
	{
		var arbitration = new RemoteIntentArbitration();

		Assert.True(arbitration.TryAdmit(First, Item, nowMs: 1000, out var holder));
		Assert.Equal(0UL, holder);
		Assert.Equal(1, arbitration.Count);
	}

	[Fact]
	public void ACompetingPeer_IsRefusedWhileTheLeaseHolds()
	{
		var arbitration = new RemoteIntentArbitration();
		Assert.True(arbitration.TryAdmit(First, Item, nowMs: 1000, out _));

		Assert.False(arbitration.TryAdmit(Second, Item, nowMs: 1000 + RemoteIntentArbitration.LeaseMs - 1, out var holder));
		Assert.Equal(First, holder);
	}

	[Fact]
	public void TheSameRequester_MaySendItsNextGestureImmediately()
	{
		var arbitration = new RemoteIntentArbitration();
		Assert.True(arbitration.TryAdmit(First, Item, nowMs: 1000, out _));

		Assert.True(arbitration.TryAdmit(First, Item, nowMs: 1001, out _));
	}

	[Fact]
	public void AnotherItem_IsNotBlockedByAnInFlightIntent()
	{
		var arbitration = new RemoteIntentArbitration();
		Assert.True(arbitration.TryAdmit(First, Item, nowMs: 1000, out _));

		Assert.True(arbitration.TryAdmit(Second, Item + 1, nowMs: 1000, out _));
		Assert.Equal(2, arbitration.Count);
	}

	[Fact]
	public void AfterTheLeaseExpires_TheCompetingPeerIsAdmitted()
	{
		var arbitration = new RemoteIntentArbitration();
		Assert.True(arbitration.TryAdmit(First, Item, nowMs: 1000, out _));

		Assert.True(arbitration.TryAdmit(Second, Item, nowMs: 1000 + RemoteIntentArbitration.LeaseMs, out _));
		Assert.Equal(1, arbitration.Count);
	}

	[Fact]
	public void ExpiredLeases_LeaveTheTable()
	{
		var arbitration = new RemoteIntentArbitration();
		Assert.True(arbitration.TryAdmit(First, Item, nowMs: 1000, out _));

		Assert.True(arbitration.TryAdmit(Second, Item + 1, nowMs: 1000 + RemoteIntentArbitration.LeaseMs, out _));
		Assert.Equal(1, arbitration.Count);
	}
}
