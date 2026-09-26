using System;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The rule for the game's own DIRECT <c>Time.timeScale</c> writes — the sibling
/// <see cref="WorldTimeScaleCall"/> cannot see, because a field write carries no
/// native flag and no call to classify. The reported case is the earthquake start
/// (<c>WorldGeneration.cs:870</c>), which used to end a standing session
/// acceleration for everyone through the host's adoption; the owner ruled
/// 2026-09-26 that vanilla stands there — the reset is NOT suppressed, the host
/// adopts it as the shared speed, and the rule records that with no suppression
/// member at all. The mechanism pin that makes the adapter execute this table is
/// <c>WorldTimeDirectWritePinTests</c>, deliberately a separate class so it still
/// compiles, and still fails, against a Game Adapter left at HEAD.
/// </summary>
public class WorldTimeDirectWriteTests
{
	[Theory]
	// THE REPORTED CASE, host: the quake's reset owns the shared clock — the
	// acceleration ends for every screen, vanilla-style, with no suppression
	[InlineData(WorldTimeSpeed.Normal, WorldTimeSpeed.Fast, true, WorldTimeDirectWrite.Verdict.Adopt)]
	[InlineData(WorldTimeSpeed.Normal, WorldTimeSpeed.SuperFast, true, WorldTimeDirectWrite.Verdict.Adopt)]
	// the console's `timescale` command (ConsoleScript.cs:815) writes the same
	// numbers and cannot be told apart by VALUE — an admin's write is adopted too,
	// which is what keeps its meaning
	[InlineData(WorldTimeSpeed.SuperFast, WorldTimeSpeed.Normal, true, WorldTimeDirectWrite.Verdict.Adopt)]
	[InlineData(WorldTimeSpeed.Fast, WorldTimeSpeed.SuperFast, true, WorldTimeDirectWrite.Verdict.Adopt)]
	// the sleep speeds (25× and 3.5×) are domain speeds too and reachable as a live
	// value through the same mapping: a drift into one is adopted on the host — what
	// STANDS there is the sleep policy's own decision (WorldTimePolicy), not this
	// rule's — and a guest is put back on the applied speed
	[InlineData(WorldTimeSpeed.UnconsciousFast, WorldTimeSpeed.Normal, true, WorldTimeDirectWrite.Verdict.Adopt)]
	[InlineData(WorldTimeSpeed.DyingFast, WorldTimeSpeed.Fast, false, WorldTimeDirectWrite.Verdict.Restore)]
	// the same drift on a guest is not the shared clock changing: the host owns it,
	// so this screen is put back on the applied speed
	[InlineData(WorldTimeSpeed.Normal, WorldTimeSpeed.Fast, false, WorldTimeDirectWrite.Verdict.Restore)]
	[InlineData(WorldTimeSpeed.SuperFast, WorldTimeSpeed.Normal, false, WorldTimeDirectWrite.Verdict.Restore)]
	// the frame after an adoption: the clock already is the applied speed, so
	// nothing is written and no speed sound can be replayed
	[InlineData(WorldTimeSpeed.Fast, WorldTimeSpeed.Fast, true, WorldTimeDirectWrite.Verdict.None)]
	[InlineData(WorldTimeSpeed.Fast, WorldTimeSpeed.Fast, false, WorldTimeDirectWrite.Verdict.None)]
	// not a domain speed: Paused (0), Slowmo (0.16) and every value between two
	// speeds (a correction ramp) map to null and stay local presentation
	[InlineData(null, WorldTimeSpeed.Fast, true, WorldTimeDirectWrite.Verdict.None)]
	[InlineData(null, WorldTimeSpeed.Fast, false, WorldTimeDirectWrite.Verdict.None)]
	[InlineData(null, WorldTimeSpeed.Normal, false, WorldTimeDirectWrite.Verdict.None)]
	public void Classify_TheWorldsOwnWriteOwnsTheSharedClock(
		WorldTimeSpeed? liveClock,
		WorldTimeSpeed appliedSpeed,
		bool isHost,
		WorldTimeDirectWrite.Verdict expected) =>
		Assert.Equal(expected, WorldTimeDirectWrite.Classify(liveClock, appliedSpeed, isHost));

	// The owner's ruling (2026-09-26): vanilla stands at an earthquake, so CUO never
	// swallows the game's own write. A renamed or replaced member is a design change
	// that has to be ruled again, so the census pins the NAMES, not only the count,
	// and this test makes its author read the ruling's decision record first.
	[Fact]
	public void TheRulingHasNoSuppressionVerdict()
	{
		string[] expected = ["None", "Adopt", "Restore"];
		Assert.Equal(expected, Enum.GetNames(typeof(WorldTimeDirectWrite.Verdict)));
	}
}
