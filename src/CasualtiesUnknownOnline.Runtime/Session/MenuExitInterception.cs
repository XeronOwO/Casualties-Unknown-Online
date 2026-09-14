namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The scene-load interception's verdict for one call of the game's own "leave
/// the world" action (<c>PlayerCamera.ToMainMenu</c>). Pure, so the verdict is
/// L0-testable — the Harmony prefix is a one-line adapter over it, and an
/// inverted verdict here silently swallows the player's leave.
/// </summary>
public static class MenuExitInterception
{
	/// <summary>
	/// <c>true</c> = SUPPRESS the game's own scene load: the leave was recorded and
	/// the frame-end seam will take the mid-run cut and perform the leave on the
	/// pump. <c>false</c> = let the original load run — there is no world to leave,
	/// or this IS the recorded leave being honoured (deferring it again would make
	/// the world un-leavable).
	/// </summary>
	public static bool ShouldSuppressSceneLoad(bool hasLiveWorld, bool replayingLeave, bool wouldSeamLeave) =>
		hasLiveWorld && !replayingLeave && wouldSeamLeave;
}
