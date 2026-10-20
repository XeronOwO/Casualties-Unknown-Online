using System;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Session;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The world half of a member's re-entry ask: whether THIS client may answer it with an entry
/// instruction. The member-side facts (handshaken, held out of the world, the per-sender
/// cooldown) are the Runtime channel's admission and are covered by <c>ReentryRequestTests</c>;
/// what is left is the adapter's own answer — a live world and a published run baseline — because
/// only the adapter can establish those. The adapter is compile-excluded, so this is exercised
/// reflectively through <see cref="GameAssemblyHost"/>.
/// <para>
/// Both refusals are real failure shapes rather than politeness: an invite with no live world
/// enters the member into a layer this host has not generated, and one with no run baseline holds
/// the member's generation on params that never arrive.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class ReentryPolicyTests
{
	private static readonly Type Policy = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Run.ReentryPolicy",
		throwOnError: true)!;

	private static string Decide(SessionRole role, bool sessionActive, bool hostHasLiveWorld, bool runBaselinePublished) =>
		Policy.GetMethod("Decide", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!
			.Invoke(null, [role, sessionActive, hostHasLiveWorld, runBaselinePublished])!
			.ToString()!;

	[Theory]
	[InlineData(SessionRole.Host, true, true, true, "Invite")]              // the host's world is up and it holds the run baseline the entry promises
	[InlineData(SessionRole.Host, true, false, true, "NoLiveWorld")]        // entering or regenerating: the member's window asks again
	[InlineData(SessionRole.Host, true, true, false, "NoRunBaseline")]      // an entry now would wait for params that never arrive
	[InlineData(SessionRole.Host, true, false, false, "NoLiveWorld")]       // no world is the first ground
	[InlineData(SessionRole.Guest, true, true, true, "NotThisSide")]        // a member has no world to invite anybody into
	[InlineData(SessionRole.None, true, true, true, "NotThisSide")]         // solo
	[InlineData(SessionRole.Host, false, true, true, "NotThisSide")]        // a dead session's ask must not enter the next one's world
	public void Decide_AnswersOnlyWithALiveWorldAndARunBaseline(SessionRole role, bool sessionActive, bool hostHasLiveWorld, bool runBaselinePublished, string expected) =>
		Assert.Equal(expected, Decide(role, sessionActive, hostHasLiveWorld, runBaselinePublished));
}
