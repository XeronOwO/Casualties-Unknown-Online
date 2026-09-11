using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// What the LIVE-WORLD half of a restore actually wrote. The Continue click
/// happens before the scene loads, so the rest of the restored cut — the block
/// diff, the game's own partial-damage list, the decided keypad/geyser values
/// and the radiation line — lands at the world-entry seam, long after
/// <c>TryContinue</c> returned. Without this report a restore could be reported
/// as a success while the game's own bounded tables refused a row (§6 forbids
/// exactly that), because the refusal only ever reached the replay's log.
///
/// <see cref="WorldRestoreAudit"/> carries this back to the caller of
/// <c>TryContinue</c>: the adapter's restore path is the subscriber, and the
/// command console renders it for the player.
/// </summary>
/// <param name="WorldId">The world that was restored.</param>
/// <param name="Complete">True = every restored row reached the live world.</param>
/// <param name="Refused">The rows the live world did not take, by class, as report fragments.</param>
/// <param name="Summary">The one-line account, ready for the log or the console.</param>
public sealed record WorldRestoreLiveWriteReport(
	string WorldId,
	bool Complete,
	IReadOnlyList<string> Refused,
	string Summary);
