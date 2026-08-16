namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The mod-domain rate policy (architecture.md §5.3: "mod messages are
/// rate-limited"). A token bucket per sender/domain bounds how much reliable
/// channel capacity one member's mods can consume: over-burst frames are
/// dropped WITH a log, never queued (a reliable queue would only move the
/// head-of-line blocking problem forward). Production and tests both read
/// these constants — the virtual clock makes the buckets deterministic.
/// </summary>
public static class ModRateLimitPolicy
{
	/// <summary>Sustained mod-message frames per sender per second (NetMsg.ModMessage).</summary>
	public const int ModMessagesPerSecond = 20;

	/// <summary>Instant mod-message burst a sender may emit before the sustained rate applies.</summary>
	public const int ModMessageBurst = 40;

	/// <summary>Sustained host-command requests per guest per second (NetMsg.ModCommandRequest).</summary>
	public const int CommandRequestsPerSecond = 4;

	/// <summary>Instant command-request burst a guest may emit before the sustained rate applies.</summary>
	public const int CommandRequestBurst = 8;
}
