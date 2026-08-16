namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Pure replay decision for the one-shot ArmsSwing clip on a render proxy
/// (no Unity): replay when the sender's swing sequence changed — every swing,
/// even several inside ONE held IsAttacking window (rapid mining swings with
/// an effective attack cooldown below the flag hold would otherwise merge into
/// a single rising edge and play fewer clips than the real body) — or, for an
/// old-version sender that never sends the sequence (SwingSeq stays 0), on the
/// held flag's rising edge (the pre-sequence behavior, degraded to today's
/// semantics instead of breaking).
/// </summary>
public static class SwingReplay
{
	/// <summary>
	/// Whether the proxy replays the swing clip this tick. The sequence edge
	/// only counts after the FIRST snapshot seeded the previous value — a
	/// late joiner whose sender already swung (SwingSeq > 0) must not replay
	/// the historical swing. The flag's rising edge keeps the old-sender
	/// fallback and deliberately also fires on the first snapshot when the
	/// sender is mid-swing (the clone appears with the visible swing).
	/// </summary>
	public static bool ShouldReplay(byte swingSeq, byte prevSwingSeq, bool isAttacking, bool prevAttacking, bool swingStateSeeded)
		=> (swingStateSeeded && swingSeq != prevSwingSeq) || (isAttacking && !prevAttacking);
}
