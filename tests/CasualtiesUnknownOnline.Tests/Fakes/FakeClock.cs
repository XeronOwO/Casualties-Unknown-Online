using CasualtiesUnknownOnline.Runtime.Time;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// The virtual clock the simulations run on: one instance shared by every node
/// of a simulation (the network's delivery schedule, the domain services'
/// throttles and timeouts) so the whole session stack observes the SAME
/// advancing time — a 300 ms link delay shows up as a 300 ms RTT, a 2 s
/// presence check fires exactly 2 s after the lobby changed. Shared UtcTicks
/// baseline keeps RTT measurements exact (a per-node offset would leak into
/// every round trip).
/// </summary>
internal sealed class FakeClock : ITimeSource
{
	internal long NowMs { get; private set; }

	internal long UtcTicks { get; private set; }

	long ITimeSource.NowMs => NowMs;

	long ITimeSource.UtcNowTicks => UtcTicks;

	internal FakeClock(long startMs = 0, long startUtcTicks = 1_000_000_000L) // 100 s after epoch — a non-zero, realistic baseline
	{
		NowMs = startMs;
		UtcTicks = startUtcTicks;
	}

	/// <summary>Advance the whole simulation by <paramref name="ms"/> (both readings move together).</summary>
	internal void Advance(long ms)
	{
		NowMs += ms;
		UtcTicks += ms * 10_000;
	}
}
