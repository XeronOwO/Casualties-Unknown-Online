namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The start gate as the host side presents it: the whole lobby loads together,
/// so a held player is frozen under an overlay that names who is still missing
/// and shows the force-start countdown. The plugin reads it to suppress the
/// online UI's own actions and to draw that overlay — or, when the wait line is
/// already on the game's own loading screen, only to keep the HUD back.
/// </summary>
public interface IStartGateState
{
	/// <summary>True while the start gate holds this player (everyone loads together; frozen + overlay).</summary>
	bool IsWaitingForReady { get; }

	/// <summary>Overlay text while the gate holds: who we are waiting for and the force-start countdown.</summary>
	string WaitingText { get; }

	/// <summary>
	/// True when <see cref="WaitingText"/> is already written into the game's OWN loading-screen text, so a
	/// reader must not draw a second copy of it. False on a surface without that field (the menu, a build that
	/// renamed it): the caller's own overlay is then the only place the wait is described.
	/// </summary>
	bool WaitingTextIsOnTheNativeScreen { get; }
}
