namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// How long an armed cut WAITS for in-flight state to resolve, and what that wait has
/// decided so far. It is the trigger-policy half of the frame-end seam, separated from
/// <see cref="WorldSaveService"/> (which owns WHETHER and WHERE a cut is taken): the policy
/// is one bounded deadline, and the service sits at its architecture line limit right
/// before an interval trigger has to be added to it (S4.4).
///
/// The deadline exists because a deferral must never become a starvation: after
/// <see cref="MaxFrames"/> the cut proceeds and NAMES what it could not take. What a caller
/// must not do is forget to close the window — the frame the wait started on is what decides
/// between "wait" and "gave up", so it lives here, with the reset that a new trigger owes it,
/// rather than in a field a caller can leave set.
/// </summary>
internal sealed class WorldCutDeferral
{
	/// <summary>
	/// How many pump frames an armed cut waits for <see cref="WorldTransientVerdict.ResolveBeforeSave"/>
	/// state. The longest such window is the trap drop hold (two frames), so eight frames is
	/// generous for the live path while keeping a STUCK pending state from starving the
	/// request.
	/// </summary>
	internal const int MaxFrames = 8;

	private int? _startFrame;

	/// <summary>True = the cut is inside its resolution window (diagnostics and the tests read this).</summary>
	internal bool Waiting => _startFrame is not null;

	/// <summary>
	/// The frame the current window opened on, for the deadline's own log line — which has
	/// to name how long the cut waited. Zero when no cut is waiting.
	/// </summary>
	internal int StartedAtFrame => _startFrame ?? 0;

	/// <summary>No cut is armed any more: the next request opens its own window.</summary>
	internal void Reset() => _startFrame = null;

	/// <summary>
	/// The armed cut has in-flight state to wait for at <paramref name="frame"/>. True = wait
	/// for it (the window is still open); false = the deadline has passed and the cut
	/// proceeds without it. <paramref name="firstFrame"/> is true exactly once per window, so
	/// the caller logs the first wait at Information and the rest at Debug without keeping
	/// state of its own.
	/// </summary>
	internal bool ShouldWait(int frame, out bool firstFrame)
	{
		_startFrame ??= frame;
		firstFrame = _startFrame == frame;
		return frame - _startFrame.Value < MaxFrames;
	}
}
