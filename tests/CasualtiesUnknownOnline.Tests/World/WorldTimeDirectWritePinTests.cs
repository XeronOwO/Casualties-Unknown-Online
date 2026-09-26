using System;
using System.IO;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The mechanism pin for the ruled direct-write behaviour: the Game Adapter must
/// take its verdict from <c>WorldTimeDirectWrite</c> on both sides, and the host's
/// adoption must store the adopted speed and then re-state it SILENTLY, in that
/// order — the native write moves the clock but not <c>PlayerCamera.curTimeScale</c>,
/// which is what lights the HUD's speed icons (<c>HandleTimescaleIcons</c>,
/// PlayerCamera.cs:2146), so without the silent re-state the icon row keeps showing
/// the acceleration an earthquake just ended.
///
/// It reads <c>WorldTimeSync.cs</c> as text and lives in its OWN class on purpose:
/// the rule's matrix (<c>WorldTimeDirectWriteTests</c>) references a Runtime type
/// that does not exist at HEAD, while this pin compiles against HEAD's Game Adapter
/// and therefore fails there for the real reason — the red is reproducible on a tree
/// whose <c>src/</c> adapter is at HEAD, not a missing-type compile error. Comment-only
/// lines are dropped before matching, so a comment naming an API can neither satisfy
/// nor break a pin.
///
/// What the matchers prove and what they cannot: they require the ruled statements,
/// their COUNT (one re-state, one broadcast) and their ORDER (the adopted value is
/// stored before the re-state, which precedes the broadcast) — the reviewer's
/// counter-examples (a re-state primed with a constant, a re-ordered re-state, a
/// duplicated broadcast, a suppressed adoption, a missing re-state) are all
/// asserted rejected below. They cannot see reachability: an early <c>return;</c>
/// inserted inside the body, or a value primed from somewhere else, still carries
/// every required statement. That residual is declared in the cycle's self-check §7
/// rather than papered over.
/// </summary>
public class WorldTimeDirectWritePinTests
{
	[Fact]
	public void TheHostAdoptionCarriesTheRuledVerdictAndReStatesInOrder()
	{
		var body = AdoptionBody();
		Assert.True(
			AdoptionIsRuledAndSilent(body),
			"the host's direct-write adoption must take its verdict from WorldTimeDirectWrite, store the adopted speed and then re-state it SILENTLY (switchSound: false) before broadcasting, so PlayerCamera.curTimeScale — the HUD's speed icons — follows the actual clock");
	}

	[Fact]
	public void TheGuestEnforcementCarriesTheRuledVerdict()
	{
		var body = EnforcementBody();
		Assert.True(
			EnforcementIsRuled(body),
			"the guest's correction of a drifted clock must take its verdict from WorldTimeDirectWrite");
	}

	// --- the matchers' negative samples: the rejected shapes, and (where the shape
	// is the live text itself) the proof that the sample really differs from it, so
	// a text drift cannot silently turn a sample into a tautology ---

	[Fact]
	public void ThePinRejectsThePreFixAdoptionBody()
	{
		Assert.False(AdoptionIsRuledAndSilent(PreFixAdoptionBody));
		Assert.NotEqual(Normalize(AdoptionBody()), Normalize(PreFixAdoptionBody));
	}

	[Fact]
	public void ThePinRejectsAnAdoptionThatLeavesTheHudStale()
	{
		// the repair removed: the speed is adopted and broadcast, but the game's own
		// speed state keeps the pre-quake value — the defect this pin exists for
		var mutated = Mutate(AdoptionBody(), SilentRestate, string.Empty);
		Assert.False(AdoptionIsRuledAndSilent(mutated));
	}

	[Fact]
	public void ThePinRejectsASuppressedAdoption()
	{
		// the rejected ruling: swallowing the world's own write instead of adopting
		// it (the owner ruled that vanilla stands at an earthquake, 2026-09-26)
		var mutated = Mutate(AdoptionBody(), RuledAdoptGuard, SuppressedGuard);
		Assert.False(AdoptionIsRuledAndSilent(mutated));
	}

	[Fact]
	public void ThePinRejectsAnAdoptionThatKeepsTheOldAppliedSpeed()
	{
		// the bookkeeping the adoption must not lose: re-stating the OLD applied
		// speed would write the acceleration straight back over the world's own
		// reset — the "an ignored write leaves the domain believing a speed the
		// clock is not at" shape this ticket named
		var mutated = Mutate(AdoptionBody(), NativeSpeedStored, string.Empty);
		Assert.False(AdoptionIsRuledAndSilent(mutated));
	}

	[Fact]
	public void ThePinRejectsAReStateThatRunsBeforeTheAdoptedValueIsStored()
	{
		// the same statements in the wrong ORDER: re-stating before storing writes the
		// old speed over the world's own reset while every required statement is still
		// present — the shape a presence-only pin would accept
		var body = AdoptionBody();
		var stored = body.IndexOf(NativeSpeedStored, StringComparison.Ordinal);
		var restate = body.IndexOf(SilentRestate, StringComparison.Ordinal);
		Assert.True(stored > 0 && restate > stored, "the live adoption must store the adopted speed before it re-states it");
		var mutated = body.Remove(restate, SilentRestate.Length).Insert(stored, SilentRestate);
		Assert.NotEqual(body, mutated);
		Assert.False(AdoptionIsRuledAndSilent(mutated));
	}

	[Fact]
	public void ThePinRejectsAReStateThatWritesAFixedSpeed()
	{
		// a re-state primed with a constant instead of the adopted value: the console's
		// adopted 20× would be overwritten with 1×
		var mutated = Mutate(AdoptionBody(), SilentRestate, FixedSpeedRestate);
		Assert.False(AdoptionIsRuledAndSilent(mutated));
	}

	[Fact]
	public void ThePinRejectsASecondBroadcast()
	{
		// the census half of the matcher: a second broadcast is not this shape
		var mutated = Mutate(AdoptionBody(), BroadcastOnce, BroadcastOnce + " " + BroadcastOnce);
		Assert.False(AdoptionIsRuledAndSilent(mutated));
	}

	[Fact]
	public void ThePinRejectsAnEnforcementThatIsNotRuled()
	{
		Assert.False(EnforcementIsRuled(PreFixEnforcementBody));
		Assert.NotEqual(Normalize(EnforcementBody()), Normalize(PreFixEnforcementBody));
	}

	private const string RuledAdoptGuard = "WorldTimeDirectWrite.Classify(actual, _appliedSpeed, isHost: true) != WorldTimeDirectWrite.Verdict.Adopt";
	private const string SuppressedGuard = "WorldTimeDirectWrite.Classify(actual, _appliedSpeed, isHost: true) == WorldTimeDirectWrite.Verdict.Adopt";
	private const string SilentRestate = "ApplyLocalTime(_appliedSpeed, switchSound: false, force: false);";
	private const string FixedSpeedRestate = "ApplyLocalTime(WorldTimeSpeed.Normal, switchSound: false, force: false);";
	private const string NativeSpeedStored = "_appliedSpeed = actual.Value;";
	private const string RequestedSpeedStored = "_requestedSpeed = actual.Value;";
	private const string BroadcastOnce = "_worldTime.Broadcast(_appliedSpeed);";
	private const string RuledGuestGuard = "WorldTimeDirectWrite.Classify(actual, _appliedSpeed, isHost: false) == WorldTimeDirectWrite.Verdict.Restore";

	private static bool AdoptionIsRuledAndSilent(string body)
	{
		var flat = Normalize(body);
		if (!flat.Contains(RuledAdoptGuard, StringComparison.Ordinal)
			|| !flat.Contains(RequestedSpeedStored, StringComparison.Ordinal)
			|| !flat.Contains(NativeSpeedStored, StringComparison.Ordinal)
			|| Count(flat, SilentRestate) != 1
			|| Count(flat, "_worldTime.Broadcast(") != 1)
		{
			return false;
		}

		// ORDER: the adopted value is stored first, then re-stated, then broadcast.
		var stored = flat.IndexOf(NativeSpeedStored, StringComparison.Ordinal);
		var restated = flat.IndexOf(SilentRestate, StringComparison.Ordinal);
		var broadcast = flat.IndexOf("_worldTime.Broadcast(", StringComparison.Ordinal);
		return stored < restated && restated < broadcast;
	}

	private static bool EnforcementIsRuled(string body)
	{
		var flat = Normalize(body);
		return flat.Contains(RuledGuestGuard, StringComparison.Ordinal)
			&& Count(flat, "ApplyLocalTime(_appliedSpeed);") == 1;
	}

	private static string Mutate(string body, string from, string to)
	{
		Assert.True(body.Contains(from, StringComparison.Ordinal), $"the mutation anchor `{from}` is not in the live body — the sample would silently no-op");
		var mutated = body.Replace(from, to);
		Assert.NotEqual(body, mutated);
		return mutated;
	}

	private static int Count(string text, string needle)
	{
		var count = 0;
		for (var at = text.IndexOf(needle, StringComparison.Ordinal); at >= 0; at = text.IndexOf(needle, at + needle.Length, StringComparison.Ordinal))
		{
			count++;
		}

		return count;
	}

	private static string AdoptionBody() => MethodBody(ReadSource(), "private void AdoptDirectTimeScaleWrite()");

	private static string EnforcementBody() => MethodBody(ReadSource(), "private void EnforceAppliedSpeed()");

	/// <summary>The two bodies verbatim as HEAD has them — the red's reproducible shape.</summary>
	private const string PreFixAdoptionBody = """
		private void AdoptDirectTimeScaleWrite()
		{
			var actual = WorldTimeSpeedScale.FromTimeScale(Time.timeScale);
			if (actual == null || actual == _appliedSpeed)
			{
				return;
			}

			_log.LogInformation("[WorldTime] host direct timeScale write {Scale} adopted as {Speed}.", Time.timeScale, actual);
			_requestedSpeed = actual.Value;
			_appliedSpeed = actual.Value;
			_worldTime.Broadcast(_appliedSpeed);
		}
		""";

	private const string PreFixEnforcementBody = """
		private void EnforceAppliedSpeed()
		{
			var actual = WorldTimeSpeedScale.FromTimeScale(Time.timeScale);
			if (actual != null && actual != _appliedSpeed)
			{
				ApplyLocalTime(_appliedSpeed);
			}
		}
		""";

	private static string MethodBody(string source, string signature)
	{
		var stripped = StripCommentLines(source);
		var start = stripped.IndexOf(signature, StringComparison.Ordinal);
		Assert.True(start > 0, $"the pin's anchor {signature} was not found in WorldTimeSync.cs");
		var tail = stripped.Substring(start);
		var next = tail.IndexOf("\n\tprivate ", 1, StringComparison.Ordinal);
		return next > 0 ? tail.Substring(0, next) : tail;
	}

	private static string StripCommentLines(string source)
	{
		var lines = source.Split('\n');
		var kept = Array.FindAll(lines, line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal));
		return string.Join("\n", kept);
	}

	private static string Normalize(string text) =>
		string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

	private static string ReadSource() =>
		File.ReadAllText(Path.Combine(
			FindRepositoryRoot(),
			"src",
			"CasualtiesUnknownOnline.GameAdapter",
			"World",
			"WorldTimeSync.cs"));

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
