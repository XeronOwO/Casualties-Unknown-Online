using System;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The guest's unacknowledged-report fallbacks as ONE surface: each channel
/// (block state — audit gap W1, partial damage — W2, break drops — W1's drop
/// half) owns a <see cref="PendingReportFallback"/> window because the three
/// tables fill and drain independently, but they share the clock, the role gate
/// and the world boundary, so <see cref="WorldService"/> holds one of these
/// instead of three fields plus their reset and pump calls. The cadence policy
/// itself stays in <see cref="PendingReportFallback"/>; this type only keeps the
/// channels together.
/// </summary>
internal sealed class GuestReportFallbacks(ISessionControl session)
{
	private readonly PendingReportFallback _blocks = new(session);
	private readonly PendingReportFallback _damages = new(session);
	private readonly PendingReportFallback _breakDrops = new(session);

	/// <summary>One frame of every channel's cadence — each one re-sends its own outstanding set when its window elapses (a no-op when that table is empty).</summary>
	internal void Pump(long nowMs, int pendingBlocks, Action resendBlocks, int pendingDamages, Action resendDamages, int pendingBreakDrops, Action resendBreakDrops)
	{
		_blocks.Pump(nowMs, pendingBlocks, resendBlocks);
		_damages.Pump(nowMs, pendingDamages, resendDamages);
		_breakDrops.Pump(nowMs, pendingBreakDrops, resendBreakDrops);
	}

	/// <summary>The session ended — every channel must start a fresh window in the next one.</summary>
	internal void Reset()
	{
		_blocks.Reset();
		_damages.Reset();
		_breakDrops.Reset();
	}
}
