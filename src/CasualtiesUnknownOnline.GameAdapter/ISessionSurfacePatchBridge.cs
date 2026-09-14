namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The session-surface half of the Harmony patch bridge: whether a CUO surface
/// owns the player's ESC/pause input right now (the modal Online UI window, or a
/// non-modal ESC-closing surface such as the quick panel). Kept as its own
/// interface so <see cref="IPatchBridge"/> stays under the architecture line gate
/// while the input-ownership surface has one focused seam (same reason
/// <see cref="IRemoteMedicalPatchBridge"/> exists).
/// </summary>
internal interface ISessionSurfacePatchBridge
{
	/// <summary>True while the CUO Online UI modal window is open — the adapter suppresses the game's native input handling (pause/ESC) behind it.</summary>
	bool IsOnlineUiModalOpen { get; }

	/// <summary>True while a non-modal CUO ESC-closing surface (currently the quick panel) is visible — the pause toggle must not open the native pause menu behind it.</summary>
	bool IsNonModalEscapeSurfaceOpen { get; }
}
