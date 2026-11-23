using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The cut waits for a member's next report only while that member is actually reporting: the age
/// rule has to say "fresh", "stale" and "silent" apart, or the wait turns into a stall.
/// </summary>
public sealed class MemberSnapshotFreshnessTests
{
	private int _now = 1000;

	[Fact]
	public void AReport_CountsAsFreshUntilTheNextSecondIsDue()
	{
		var freshness = new MemberSnapshotFreshness(() => _now);
		freshness.NoteReported(7);

		Assert.False(freshness.IsStale(7));

		_now += MemberSnapshotFreshness.FreshMs + 1;
		Assert.True(freshness.IsStale(7));
	}

	[Fact]
	public void ASilentMember_IsNeverStale()
	{
		var freshness = new MemberSnapshotFreshness(() => _now);
		freshness.NoteReported(7);

		_now += MemberSnapshotFreshness.AliveMs + 1;

		Assert.False(freshness.IsStale(7));
	}

	[Fact]
	public void AnAuthoritativeWrite_RefreshesTheAge()
	{
		var freshness = new MemberSnapshotFreshness(() => _now);
		freshness.NoteReported(7);
		_now += MemberSnapshotFreshness.FreshMs + 1;
		Assert.True(freshness.IsStale(7));

		freshness.NoteWritten(7);

		Assert.False(freshness.IsStale(7));
	}

	[Fact]
	public void APeerThatNeverReported_IsNotWaitedFor()
	{
		var freshness = new MemberSnapshotFreshness(() => _now);

		Assert.False(freshness.IsStale(7));
	}

	[Fact]
	public void AForgottenPeer_IsNotWaitedFor()
	{
		var freshness = new MemberSnapshotFreshness(() => _now);
		freshness.NoteReported(7);
		freshness.Forget(7);

		_now += MemberSnapshotFreshness.FreshMs + 1;

		Assert.False(freshness.IsStale(7));
	}
}
