namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The spectator mode's patch port: the exploration receipt's own "back to main menu" button becomes the way INTO
/// watching instead of the way out of the run (the user's ruling). The button is scene-wired — its UnityEvent names
/// <c>PlayerCamera.ToMainMenu</c> — and a scene's persistent listeners cannot be removed at runtime
/// (<c>RemoveAllListeners</c> leaves them in place), so the interception is a Harmony prefix on that method: while
/// the receipt is up, a dead player clicking the button enters the mode and the scene load is skipped.
///
/// <para>
/// The patch cannot receive the adapter by construction, so the decision and the mode stay in
/// <see cref="World.SpectatorCameraService"/> and the patch only asks this door. The aggregate does not compose
/// this seam, so a patch that wants it reads <c>PatchBridge.Spectator</c> and the compiler keeps it off
/// <see cref="IPatchBridge"/>.
/// </para>
/// </summary>
internal interface ISpectatorPatchPort
{
	/// <summary>
	/// Enter spectator mode from the death screen's menu button. False when the local character is not dead, the
	/// receipt is not showing, or the client cannot watch at all (no session, no world, no local body) — in every
	/// case the caller lets the game's own menu return proceed.
	/// </summary>
	bool TryEnterFromDeathScreen();
}
