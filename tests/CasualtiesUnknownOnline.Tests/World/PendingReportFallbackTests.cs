using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The pending-report fallback's cadence policy (sync-coverage audit gaps W1/W2/E3/I6) — ONE
/// policy shared by every unacknowledged guest report table. Its steady step is 60 s, but the
/// documented lazy-P2P swallow window runs up to ~30 s after a world entry, so a report whose
/// live send was dropped inside it would stay invisible on the host until the 60 s mark: the
/// guest's own InWorld report therefore opens an ENTRY phase whose step is 5 s for 60 s
/// (12 x 5 s, the readiness window's entry budget), and the steady step governs after it.
/// <para>
/// These cases pin both phases, the edge that opens them, the boundary where the entry phase
/// stops governing a step, and the clock wrap that must never leave the dense step stranded in
/// the future. The assertions use the literal 5 000 / 60 000 ms rather than the class's own
/// constants: the test states the DOCUMENTED cadence, so a change to the constants has to be a
/// deliberate change to this file too.
/// </para>
/// </summary>
public class PendingReportFallbackTests
{
	/// <summary>One window under test plus the counter its owner would re-send through.</summary>
	private sealed class Probe
	{
		internal Probe()
		{
			Session = new FakeSessionControl { Role = SessionRole.Guest };
			Fallback = new PendingReportFallback(Session);
		}

		internal FakeSessionControl Session { get; }

		internal PendingReportFallback Fallback { get; }

		internal int Resends { get; private set; }

		/// <summary>One frame of the owner's cadence, exactly as <see cref="WorldReportFallbackPump"/> drives it.</summary>
		internal void Pump(long nowMs, int pending) => Fallback.Pump(nowMs, pending, () => Resends++);

		/// <summary>The guest's own world entry. The next frame stamps the anchor — the pump's clock is the only time source the class reads.</summary>
		internal void EnterWorld(long nowMs)
		{
			Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
			Pump(nowMs, pending: 0);
		}

		/// <summary>The guest left the world — the entry ends with it.</summary>
		internal void LeaveWorld(long nowMs)
		{
			Session.ReportSceneState(SceneStateType.InMenu, "PreGen");
			Pump(nowMs, pending: 0);
		}
	}

	[Fact]
	public void LiveSend_IsNotDuplicatedImmediately()
	{
		var probe = new Probe();
		probe.Pump(1_000, pending: 1); // the owner sent its live report and recorded the entry
		probe.Pump(1_100, pending: 1);

		Assert.Equal(0, probe.Resends);
	}

	[Fact]
	public void SteadyPhase_WithoutAWorldEntry_ReSendsOncePerMinute()
	{
		// No entry edge has been seen (a session whose guest never re-reported its scene
		// state, or a set that became outstanding long after the entry phase): the cadence
		// is exactly the one this class had before the entry phase existed.
		var probe = new Probe();
		probe.Pump(1_000, pending: 1); // arm
		probe.Pump(60_999, pending: 1);
		Assert.Equal(0, probe.Resends);

		probe.Pump(61_000, pending: 1); // 60 000 ms after the arm
		Assert.Equal(1, probe.Resends);
		probe.Pump(120_999, pending: 1);
		Assert.Equal(1, probe.Resends);

		probe.Pump(121_000, pending: 1);
		Assert.Equal(2, probe.Resends);
	}

	[Fact]
	public void EntryPhase_ReSendsOnTheFiveSecondStep()
	{
		var probe = new Probe();
		probe.EnterWorld(1_000);
		probe.Pump(2_000, pending: 1); // arm
		probe.Pump(6_999, pending: 1); // 4 999 ms — the entry phase is open, the step is not due
		Assert.Equal(0, probe.Resends);

		probe.Pump(7_000, pending: 1); // 5 000 ms
		Assert.Equal(1, probe.Resends);
		probe.Pump(11_999, pending: 1);
		Assert.Equal(1, probe.Resends);

		probe.Pump(12_000, pending: 1);
		Assert.Equal(2, probe.Resends);
	}

	[Fact]
	public void ReportInsideTheSwallowWindow_GetsItsFirstReSendFiveSecondsLater()
	{
		// The ticket's core case: a report whose live send was swallowed 29 s after the
		// entry (inside the documented ~30 s window) converges one step later instead of
		// waiting for the 60 s mark. The entry anchor is the clock's own zero here, which
		// the window must treat as "an entry happened", not as "no entry".
		var probe = new Probe();
		probe.EnterWorld(0);
		probe.Pump(29_000, pending: 1);
		probe.Pump(33_999, pending: 1);
		Assert.Equal(0, probe.Resends);

		probe.Pump(34_000, pending: 1);
		Assert.Equal(1, probe.Resends);
	}

	[Fact]
	public void SetArmedAtTheEndOfTheEntryPhase_IsOnTheSteadyStep()
	{
		// The declared boundary: the entry phase governs a step only while it is OPEN. A set
		// that first becomes outstanding 55 s in is due 5 s later — by then the phase is over,
		// and its live send is ~30 s past the swallow window, so the steady step (the heal
		// this class always had) is what it gets.
		var probe = new Probe();
		probe.EnterWorld(0);
		probe.Pump(55_000, pending: 1); // arm
		probe.Pump(59_999, pending: 1);
		Assert.Equal(0, probe.Resends);

		probe.Pump(60_000, pending: 1); // the step is due, but the entry phase has just ended
		Assert.Equal(0, probe.Resends);
		probe.Pump(114_999, pending: 1);
		Assert.Equal(0, probe.Resends);

		probe.Pump(115_000, pending: 1); // 60 000 ms after the arm
		Assert.Equal(1, probe.Resends);
	}

	[Fact]
	public void DrainedSet_DisarmsAndPaysNothing()
	{
		var probe = new Probe();
		probe.EnterWorld(0);
		probe.Pump(1_000, pending: 1); // arm
		probe.Pump(2_000, pending: 0); // the host's answer dropped the entry
		probe.Pump(7_000, pending: 0);
		Assert.Equal(0, probe.Resends);

		// A later entry inside the same entry phase arms a fresh window: one step after it.
		probe.Pump(20_000, pending: 1); // arm
		probe.Pump(25_000, pending: 1);
		Assert.Equal(1, probe.Resends);
	}

	[Fact]
	public void HostRoleAndInactiveSession_NeverReSend()
	{
		var probe = new Probe();
		probe.EnterWorld(0);
		probe.Session.Role = SessionRole.Host;
		probe.Pump(1_000, pending: 1);
		probe.Pump(61_000, pending: 1);
		Assert.Equal(0, probe.Resends);

		probe.Session.Role = SessionRole.Guest;
		probe.Session.SessionActive = false;
		probe.Pump(70_000, pending: 1);
		probe.Pump(130_000, pending: 1);
		Assert.Equal(0, probe.Resends);
	}

	[Fact]
	public void LeavingTheWorld_ClosesTheEntryPhase()
	{
		var probe = new Probe();
		probe.EnterWorld(0);
		probe.LeaveWorld(1_000); // the entry this phase belonged to is over
		probe.Pump(2_000, pending: 1); // arm
		probe.Pump(6_999, pending: 1);
		Assert.Equal(0, probe.Resends);

		probe.Pump(62_000, pending: 1); // 60 000 ms after the arm
		Assert.Equal(1, probe.Resends);
	}

	[Fact]
	public void SessionReset_ClosesTheEntryPhase()
	{
		// The owners call Reset() when the session ends or a new world/layer baseline is
		// applied: the next session's first report must start from the steady step until its
		// own entry opens a fresh phase.
		var probe = new Probe();
		probe.EnterWorld(0);
		probe.Pump(1_000, pending: 1);
		probe.Fallback.Reset();
		probe.Pump(2_000, pending: 1); // arm
		probe.Pump(7_000, pending: 1);
		Assert.Equal(0, probe.Resends);

		probe.Pump(62_000, pending: 1);
		Assert.Equal(1, probe.Resends);
	}

	[Fact]
	public void BackwardsClock_ReArmsTheWindowAndRetiresTheStaleEntryAnchor()
	{
		var probe = new Probe();
		probe.EnterWorld(100_000);
		probe.Pump(101_000, pending: 1); // arm
		probe.Pump(160_999, pending: 1);
		Assert.Equal(0, probe.Resends);

		probe.Pump(161_000, pending: 1); // 60 000 ms after the arm — the entry phase is long over
		Assert.Equal(1, probe.Resends);

		// Environment.TickCount wraps every ~24.9 days: the reading goes backwards and the
		// window re-arms at the new reading instead of stalling on the old anchor.
		// The coverage note for this case: disabling the class's retirement of the stale
		// anchor makes THIS case fail (Expected 1 / Actual 2) — the anchor would read as
		// "the future" and the dense step would fire at +5 s, which is the negative sample.
		probe.Pump(1_000, pending: 1);
		Assert.Equal(1, probe.Resends);
		probe.Pump(6_000, pending: 1); // 5 000 ms later — a still-open entry phase would fire here
		Assert.Equal(1, probe.Resends);

		probe.Pump(61_000, pending: 1); // the steady step from the re-based arm
		Assert.Equal(2, probe.Resends);
	}
}
