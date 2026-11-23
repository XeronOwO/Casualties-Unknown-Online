using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The cut waits for a member's next report only while that member's carried set actually moved and the
/// member is still reporting: the rule has to say "unchanged", "changed" and "silent" apart, or the wait
/// turns into a stall.
/// </summary>
public sealed class MemberSnapshotFreshnessTests
{
	private int _now = 1000;

	[Fact]
	public void AMemberWhoseCarriedSetNeverMoved_IsNotWaitedFor()
	{
		var freshness = new MemberSnapshotFreshness(() => _now);
		freshness.NoteReported(7);

		Assert.False(freshness.IsStale(7));
	}

	[Fact]
	public void ACarriedSetThatMovedAfterTheReport_IsStale()
	{
		var freshness = new MemberSnapshotFreshness(() => _now);
		freshness.NoteReported(7);
		freshness.NoteChanged(7);

		Assert.True(freshness.IsStale(7));
	}

	[Fact]
	public void TheNextReport_ClearsTheStaleness()
	{
		var freshness = new MemberSnapshotFreshness(() => _now);
		freshness.NoteReported(7);
		freshness.NoteChanged(7);
		_now += 150;

		freshness.NoteReported(7);

		Assert.False(freshness.IsStale(7));
	}

	[Fact]
	public void ASilentMember_IsNeverWaitedFor()
	{
		var freshness = new MemberSnapshotFreshness(() => _now);
		freshness.NoteReported(7);
		freshness.NoteChanged(7);

		_now += MemberSnapshotFreshness.AliveMs + 1;

		Assert.False(freshness.IsStale(7));
	}

	[Fact]
	public void APeerThatNeverReported_IsNeverWaitedFor()
	{
		var freshness = new MemberSnapshotFreshness(() => _now);
		freshness.NoteChanged(7);

		Assert.False(freshness.IsStale(7));
	}

	[Fact]
	public void AForgottenPeer_IsNeverWaitedFor()
	{
		var freshness = new MemberSnapshotFreshness(() => _now);
		freshness.NoteReported(7);
		freshness.NoteChanged(7);
		freshness.Forget(7);

		Assert.False(freshness.IsStale(7));
	}
}
