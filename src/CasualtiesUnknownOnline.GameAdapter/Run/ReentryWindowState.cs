namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>One step of the member's re-entry window — the verdict <see cref="ReentryWindow.Step"/> hands its owner.</summary>
internal enum ReentryWindowState
{
	/// <summary>The window is not armed (never armed, or closed on an earlier step).</summary>
	Idle,

	/// <summary>Armed, with nothing to do on this frame (still leaving, waiting for the host, inside the cadence, or on the way in).</summary>
	Waiting,

	/// <summary>Armed and eligible to send ONE ask now; the owner reports the send back with <see cref="ReentryWindow.NoteAsk"/>.</summary>
	Ready,

	/// <summary>The window closed on this step; <see cref="ReentryWindow.CloseReason"/> says why, once.</summary>
	Closed,
}
