using System;
using System.IO;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The routing rule that keeps a session-wide acceleration standing: only an
/// ANNOUNCED speed change (<c>switchSound</c>) owns the shared clock, and
/// <see cref="WorldTimeScaleCall.Route"/> is the decision table the Game Adapter
/// executes verbatim. The rows are the native <c>PlayerCamera.SetTimeScale</c>
/// call sites the cycle inventoried (reversing/Assembly-CSharp/Assembly-CSharp/:
/// PlayerCamera.cs, PauseHandler.cs, Vomiter.cs, SpiderHandler.cs, SelfHarmer.cs,
/// SurvivorNote.cs, EPdaScript.cs) — 26 of them, plus four direct
/// <c>Time.timeScale</c> writes audited separately in the cycle's self-check.
/// </summary>
public class WorldTimeScaleCallTests
{
	[Theory]
	// the speed hotkeys (:887-895), the console's AutoTimeScaleSet (:649) and the
	// scripted sequences (:507/:562/:584) — the game plays its speed sound
	[InlineData(WorldTimeScaleCall.SpeedFamily.SharedClock, true, false, WorldTimeScaleCall.Kind.Deliberate)]
	// the movement rule (:921-924), waking up (:2197), the scene start (:734) and
	// the in-world event resets (Vomiter.cs:52/:105, SpiderHandler.cs:94/:269,
	// SelfHarmer.cs:46, SurvivorNote.cs:72)
	[InlineData(WorldTimeScaleCall.SpeedFamily.SharedClock, false, false, WorldTimeScaleCall.Kind.AutomaticReset)]
	// the pause path (PauseHandler.cs:155/:159), EndSequence (:2293) and the
	// self-destruct sequence (:170) — force overrides the paused/death guard
	[InlineData(WorldTimeScaleCall.SpeedFamily.SharedClock, false, true, WorldTimeScaleCall.Kind.Transition)]
	// never happens natively; the precedence is declared: the guard a forced call
	// overrides is what makes it a transition
	[InlineData(WorldTimeScaleCall.SpeedFamily.SharedClock, true, true, WorldTimeScaleCall.Kind.Transition)]
	// SurvivorNote's open (:48 Slowmo) and EPdaScript (:57 Slowmo), the editor's
	// debug Slowmo (:927) — local presentation, already local-only before this rule
	[InlineData(WorldTimeScaleCall.SpeedFamily.Presentation, false, false, WorldTimeScaleCall.Kind.LocalPresentation)]
	[InlineData(WorldTimeScaleCall.SpeedFamily.Presentation, true, false, WorldTimeScaleCall.Kind.LocalPresentation)]
	// HandleUnconsciousScreen's 25×/3.5× (:2235/:2239) — the host's all-unconscious
	// policy owns the sleep speeds, never the routing rule's "intent" reading
	[InlineData(WorldTimeScaleCall.SpeedFamily.SleepOwned, false, false, WorldTimeScaleCall.Kind.SleepOwned)]
	public void Classify_OnlyAnAnnouncedSpeedChangeIsASpeedIntent(
		WorldTimeScaleCall.SpeedFamily family,
		bool switchSound,
		bool force,
		WorldTimeScaleCall.Kind expected) =>
		Assert.Equal(expected, WorldTimeScaleCall.Classify(family, switchSound, force));

	[Theory]
	// THE REPORTED CASE, both roles: a member's movement (or any silent reset)
	// neither ends the shared acceleration nor dips this client's clock
	[InlineData(WorldTimeScaleCall.Kind.AutomaticReset, true, true, false, WorldTimeScaleCall.Action.SwallowAndKeepSessionSpeed)]
	[InlineData(WorldTimeScaleCall.Kind.AutomaticReset, false, true, false, WorldTimeScaleCall.Action.SwallowAndKeepSessionSpeed)]
	// while the start gate holds timeScale 0, a reset writes NOTHING on either side
	[InlineData(WorldTimeScaleCall.Kind.AutomaticReset, true, true, true, WorldTimeScaleCall.Action.Swallow)]
	[InlineData(WorldTimeScaleCall.Kind.AutomaticReset, false, true, true, WorldTimeScaleCall.Action.Swallow)]
	// an announced change: the host's own, or a guest's local-first report (and the
	// start gate keeps the clock for itself)
	[InlineData(WorldTimeScaleCall.Kind.Deliberate, true, true, false, WorldTimeScaleCall.Action.RunAsAuthority)]
	[InlineData(WorldTimeScaleCall.Kind.Deliberate, true, true, true, WorldTimeScaleCall.Action.RunAsAuthority)]
	[InlineData(WorldTimeScaleCall.Kind.Deliberate, false, true, false, WorldTimeScaleCall.Action.RunLocalFirstAndReport)]
	[InlineData(WorldTimeScaleCall.Kind.Deliberate, false, true, true, WorldTimeScaleCall.Action.DeferToStartGate)]
	// forced transitions and Slowmo/Paused stay local on both sides, and the gate
	// does not change that: a forced transition still runs (overriding the paused/
	// death guard is what `force` means) — acceptance row 7's test anchor
	[InlineData(WorldTimeScaleCall.Kind.Transition, false, true, false, WorldTimeScaleCall.Action.RunLocalOnly)]
	[InlineData(WorldTimeScaleCall.Kind.Transition, true, true, false, WorldTimeScaleCall.Action.RunLocalOnly)]
	[InlineData(WorldTimeScaleCall.Kind.Transition, true, true, true, WorldTimeScaleCall.Action.RunLocalOnly)]
	[InlineData(WorldTimeScaleCall.Kind.LocalPresentation, false, true, false, WorldTimeScaleCall.Action.RunLocalOnly)]
	[InlineData(WorldTimeScaleCall.Kind.LocalPresentation, false, true, true, WorldTimeScaleCall.Action.RunLocalOnly)]
	// the sleep fast-forward: suppressed on a guest, the host's own on the host
	[InlineData(WorldTimeScaleCall.Kind.SleepOwned, false, true, false, WorldTimeScaleCall.Action.Swallow)]
	[InlineData(WorldTimeScaleCall.Kind.SleepOwned, true, true, false, WorldTimeScaleCall.Action.RunLocalOnly)]
	// outside a session the vanilla behaviour stands and nothing is reported
	[InlineData(WorldTimeScaleCall.Kind.AutomaticReset, false, false, false, WorldTimeScaleCall.Action.RunLocalOnly)]
	[InlineData(WorldTimeScaleCall.Kind.Deliberate, false, false, false, WorldTimeScaleCall.Action.RunLocalOnly)]
	public void Route_DecidesWhoMayWriteTheClock(
		WorldTimeScaleCall.Kind kind,
		bool isHost,
		bool sessionActive,
		bool atStartGate,
		WorldTimeScaleCall.Action expected) =>
		Assert.Equal(expected, WorldTimeScaleCall.Route(kind, isHost, sessionActive, atStartGate));

	[Theory]
	// the movement case: the clock is already the session's — write nothing, so a
	// movement key produces no dip and no speed sound
	[InlineData(WorldTimeScaleCall.Kind.AutomaticReset, false, true, false)]
	// a local presentation effect (Slowmo) held this screen off the session speed
	// — e.g. the survivor note closing — put it back
	[InlineData(WorldTimeScaleCall.Kind.AutomaticReset, false, false, true)]
	// the local initiation deliberately leads the host: nothing may overwrite it
	[InlineData(WorldTimeScaleCall.Kind.AutomaticReset, true, false, false)]
	// only a silent reset goes through this path at all
	[InlineData(WorldTimeScaleCall.Kind.Deliberate, false, false, false)]
	[InlineData(WorldTimeScaleCall.Kind.Transition, false, false, false)]
	[InlineData(WorldTimeScaleCall.Kind.LocalPresentation, false, false, false)]
	public void ShouldRestoreSessionSpeed_OnlyForASilentResetThatMovedThisScreen(
		WorldTimeScaleCall.Kind kind,
		bool sessionClockInFlight,
		bool liveClockAtSessionSpeed,
		bool expected) =>
		Assert.Equal(expected, WorldTimeScaleCall.ShouldRestoreSessionSpeed(kind, sessionClockInFlight, liveClockAtSessionSpeed));

	[Fact]
	public void TheAdapterExecutesTheRoutingTable()
	{
		var sync = ReadSource("WorldTimeSync.cs");

		// The decision is the pure table; the adapter only executes its verdict.
		Assert.Contains("WorldTimeScaleCall.Route(", sync);
		Assert.Contains("case WorldTimeScaleCall.Action.SwallowAndKeepSessionSpeed:", sync);
		Assert.Contains("case WorldTimeScaleCall.Action.RunLocalFirstAndReport:", sync);

		// Exactly ONE reporting path, ANCHORED: the send must live inside
		// BeginLocalFirst and nowhere else in the file, so a router that reported a
		// silent reset again would have to add a second site inside that method (or
		// move the send out of it) and this pin fails either way.
		var begin = sync.IndexOf("private void BeginLocalFirst", StringComparison.Ordinal);
		Assert.True(begin > 0, "WorldTimeSync.BeginLocalFirst not found");
		var tail = sync.Substring(begin);
		var nextMember = tail.IndexOf("\n\tprivate ", 1, StringComparison.Ordinal);
		var body = nextMember > 0 ? tail.Substring(0, nextMember) : tail;
		Assert.Contains("_worldTime.SendRequest(", body);
		Assert.DoesNotContain("_worldTime.SendRequest(", sync.Substring(0, begin));
		Assert.True(
			nextMember < 0 || tail.IndexOf("_worldTime.SendRequest(", nextMember, StringComparison.Ordinal) < 0,
			"a report site outside BeginLocalFirst would let a silent reset be reported again");

		// …and the case that calls it is the announced one, checked on
		// whitespace-flattened text so re-indentation cannot break the anchor.
		var flat = string.Join(" ", sync.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
		Assert.Contains("case WorldTimeScaleCall.Action.RunLocalFirstAndReport: BeginLocalFirst(speed); return true;", flat);
	}

	[Fact]
	public void TheEnumCensusesAreDeclared()
	{
		// A new speed family or call kind must not slip into a table by default:
		// the Runtime table throws on an unmapped value, and this census makes the
		// author of a new member read the table and this test together.
		Assert.Equal(3, Enum.GetValues(typeof(WorldTimeScaleCall.SpeedFamily)).Length);
		Assert.Equal(5, Enum.GetValues(typeof(WorldTimeScaleCall.Kind)).Length);
		Assert.Equal(6, Enum.GetValues(typeof(WorldTimeScaleCall.Action)).Length);
	}

	private static string ReadSource(string fileName) =>
		File.ReadAllText(Path.Combine(
			FindRepositoryRoot(),
			"src",
			"CasualtiesUnknownOnline.GameAdapter",
			"World",
			fileName));

	private static string FindRepositoryRoot()
	{
		var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CasualtiesUnknownOnline.slnx")))
		{
			directory = directory.Parent;
		}

		if (directory is null)
		{
			throw new InvalidOperationException("could not locate repository root (CasualtiesUnknownOnline.slnx)");
		}

		return directory.FullName;
	}
}
