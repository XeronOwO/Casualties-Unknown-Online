using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// One answer to "what is this session waiting for": the kind of wait, WHO it is
/// waiting on by name, and how long is left. The screen, the log and any other
/// reader take this ONE value, so a wait that exists cannot be described by one
/// reader and not by another — the state is named before it is worded.
/// <para>
/// The names are the display names the session already carries; a wait with nobody
/// to name (the host, or the last one in) carries an empty list rather than a
/// placeholder, and the wording decides what to say about that.
/// </para>
/// </summary>
internal sealed record SessionWaitState(
	SessionWaitKind Kind,
	IReadOnlyList<string> WaitingNames,
	int RemainingSeconds);
