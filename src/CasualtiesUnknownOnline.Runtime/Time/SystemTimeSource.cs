using System;

namespace CasualtiesUnknownOnline.Runtime.Time;

/// <summary>The production clock: wall-clock time, exactly the readings the
/// domain services used before the seam (Environment.TickCount for throttles
/// and timeouts, DateTime.UtcNow.Ticks for RTT stamps and the session epoch) —
/// behaviour is unchanged, only the reading point is injectable now.</summary>
public sealed class SystemTimeSource : ITimeSource
{
	public long NowMs => Environment.TickCount;

	public long UtcNowTicks => DateTime.UtcNow.Ticks;
}
