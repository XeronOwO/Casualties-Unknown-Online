namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// The guest run-follow phases — one enum replaces the scattered booleans (pending/started/ready/frozen). The phase says what this CLIENT shows and what it is
/// waiting for; the descent's own progress is <see cref="DescentPhase"/>.
/// </summary>
internal enum RunPhase
{
	/// <summary>Menu — not following any run.</summary>
	Idle,

	/// <summary>WorldJoin received, waiting for PreRunScript to start the run.</summary>
	JoinPending,

	/// <summary>StartRun/StartTutorial called — transition/loading underway.</summary>
	Starting,

	/// <summary>The generation boundary passed (the gen wrapper engaged).</summary>
	Generating,

	/// <summary>The host announced its own layer advance — this member generates its new layer in parallel.</summary>
	Descending,

	/// <summary>World generated — waiting for the host's WorldReady (gate freeze).</summary>
	WaitingReady,

	/// <summary>WorldReady received — playing.</summary>
	Playing,
}
