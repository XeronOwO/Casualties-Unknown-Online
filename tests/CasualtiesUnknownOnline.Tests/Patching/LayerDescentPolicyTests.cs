using System;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Session;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The host descent's adapter decisions: which member may follow the edge, when its
/// own new-layer generation counts as settled, and when the follow must be abandoned.
/// The adapter is compile-excluded (it binds game/Unity assemblies), so the rules are
/// exercised reflectively through the shared <see cref="GameAssemblyHost"/>.
/// <para>
/// <c>IsSettled</c>'s launch fact is the one the runtime probe found the hard way: the
/// game's world object exists with <c>generatingWorld</c> already down while the
/// generation wrapper holds for the host's baseline, so "world exists and not
/// generating" alone would settle (and report InWorld) before the layer was generated.
/// <c>ShouldAbort</c> is the independent review's blocker: a follow that has no way out
/// refuses every WorldJoin for the rest of the session.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
[Collection(GameAssemblyCollection.Name)]
public class LayerDescentPolicyTests
{
	private static readonly Type Policy = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Run.LayerDescentPolicy",
		throwOnError: true)!;

	private static string DecideFollow(
		SessionRole role, bool sessionActive, bool inWorld, bool playingOrWaiting, bool generationInFlight, bool hasWorld) =>
		Policy.GetMethod("DecideFollow", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!
			.Invoke(null, [role, sessionActive, inWorld, playingOrWaiting, generationInFlight, hasWorld])!
			.ToString()!;

	private static bool IsSettled(bool generationStarted, bool generating, bool regenerating, bool liveWorld) =>
		(bool)Policy.GetMethod("IsSettled", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!
			.Invoke(null, [generationStarted, generating, regenerating, liveWorld])!;

	private static bool ShouldAbort(bool hasWorld, bool holdTimedOut, bool settleTimedOut) =>
		(bool)Policy.GetMethod("ShouldAbort", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!
			.Invoke(null, [hasWorld, holdTimedOut, settleTimedOut])!;

	[Fact]
	public void Follow_OnlyForAGuestPlayingInALiveWorldItStillHas() =>
		Assert.Equal("Follow", DecideFollow(SessionRole.Guest, sessionActive: true, inWorld: true, playingOrWaiting: true, generationInFlight: false, hasWorld: true));

	[Fact]
	public void Follow_AlsoForAMemberWaitingOnThisDescentForTheNextLayer() =>
		// A second host descent while the member's first layer is generated and only the release is
		// outstanding: the edge starts the next layer instead of being refused as a duplicate.
		Assert.Equal("Follow", DecideFollow(SessionRole.Guest, true, inWorld: true, playingOrWaiting: true, generationInFlight: false, hasWorld: true));

	[Theory]
	[InlineData(SessionRole.Host)]
	[InlineData(SessionRole.None)]
	public void Follow_RefusesASideThatIsNotAGuest(SessionRole role) =>
		Assert.Equal("NotThisSides", DecideFollow(role, true, true, true, false, true));

	[Fact]
	public void Follow_RefusesAGuestWithNoLiveSession() =>
		Assert.Equal("NotThisSides", DecideFollow(SessionRole.Guest, sessionActive: false, inWorld: true, playingOrWaiting: true, generationInFlight: false, hasWorld: true));

	[Theory]
	[InlineData(false, true)] // in the menu
	[InlineData(false, false)] // entering / generating
	[InlineData(true, false)] // the menu-return phase of a session that ended
	public void Follow_RefusesAMemberThatIsNotPlaying(bool inWorld, bool playingOrWaiting) =>
		Assert.Equal("NotPlaying", DecideFollow(SessionRole.Guest, true, inWorld, playingOrWaiting, false, true));

	[Fact]
	public void Follow_RefusesAPlayingMemberWithNoWorldObject() =>
		Assert.Equal("NoWorld", DecideFollow(SessionRole.Guest, true, true, true, false, false));

	[Fact]
	public void Follow_ReportsTheDuplicateBeforeTheTransientNotInWorldState() =>
		// A second edge arrives while the member's own generation is in flight (its world is down): it
		// is a duplicate, not a member in the menu.
		Assert.Equal("AlreadyDescending", DecideFollow(SessionRole.Guest, true, inWorld: false, playingOrWaiting: true, generationInFlight: true, hasWorld: true));

	[Theory]
	[InlineData(true, false, false, true, true)]   // started, generation idle, world live — settled
	[InlineData(false, false, false, true, false)] // not launched yet (the wrapper's baseline hold)
	[InlineData(true, true, false, false, false)]  // generating (live world is false too)
	[InlineData(true, false, true, false, false)]  // the regeneration's fade/clear
	[InlineData(true, false, false, false, false)] // no live world yet
	public void IsSettled_NeedsTheLaunchAndAnIdleLiveWorld(bool started, bool generating, bool regenerating, bool liveWorld, bool expected) =>
		Assert.Equal(expected, IsSettled(started, generating, regenerating, liveWorld));

	[Theory]
	[InlineData(false, false, false, true)]  // the world object is gone — the member left mid-descent
	[InlineData(true, true, false, true)]    // the host's baseline never arrived (hold timeout)
	[InlineData(true, false, true, true)]    // the generation never settled (settle timeout)
	[InlineData(true, false, false, false)]  // a healthy follow
	public void ShouldAbort_EndsTheFollowOnALostWorldOrATimedOutWait(bool hasWorld, bool holdTimedOut, bool settleTimedOut, bool expected) =>
		Assert.Equal(expected, ShouldAbort(hasWorld, holdTimedOut, settleTimedOut));
}
