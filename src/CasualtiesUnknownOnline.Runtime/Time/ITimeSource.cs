namespace CasualtiesUnknownOnline.Runtime.Time;

/// <summary>
/// The domain services' clock (SessionService throttles/retries, the entity
/// stream's 20 Hz pacing, the world start gate, the RTT stamps): the ONE time
/// seam for the whole runtime. Production uses <see cref="SystemTimeSource"/>
/// (Environment.TickCount / DateTime.UtcNow — the pre-seam behaviour);
/// simulations inject a virtual clock so throttles, timeouts and retries are
/// driven deterministically (phase-2 simulation, FakeClock in the tests).
/// Instances are only ever read, never advanced, by the runtime — a clock
/// that can move is a test-only concern.
/// </summary>
public interface ITimeSource
{
	/// <summary>Monotonic milliseconds (Environment.TickCount semantics — the throttle/timeout base).</summary>
	long NowMs { get; }

	/// <summary>Absolute ticks (DateTime.UtcNow.Ticks semantics — RTT stamps and the session epoch).</summary>
	long UtcNowTicks { get; }
}
