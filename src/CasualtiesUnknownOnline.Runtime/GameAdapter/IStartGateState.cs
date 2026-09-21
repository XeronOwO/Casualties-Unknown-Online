namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The start gate as the host side presents it: the whole lobby loads together,
/// so a held player is frozen under an overlay that names who is still missing
/// and shows the force-start countdown. The plugin reads it to suppress the
/// online UI's own actions and to draw that overlay.
/// </summary>
public interface IStartGateState
{
	/// <summary>True while the start gate holds this player (everyone loads together; frozen + overlay).</summary>
	bool IsWaitingForReady { get; }

	/// <summary>Overlay text while the gate holds: who we are waiting for and the force-start countdown.</summary>
	string WaitingText { get; }
}
