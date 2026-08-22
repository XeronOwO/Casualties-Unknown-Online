namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The per-frame drawing surface a mod UI window receives. The methods are
/// intentionally a tiny immediate-mode alphabet — CUO's UI bridge is the only
/// component that knows Unity's IMGUI, so a mod never references UnityEngine.
/// The window is re-invoked every frame while it is registered; the mod keeps
/// any mutable state in its own mod instance.
/// </summary>
public interface IModUiWindow
{
	/// <summary>Draw one line of non-interactive text.</summary>
	void Label(string text);

	/// <summary>Draw a button. Returns true when the user clicked it this frame.</summary>
	bool Button(string text);

	/// <summary>
	/// Draw a single-line text field seeded with <paramref name="current"/> and
	/// return the value the user has edited this frame. The mod is responsible
	/// for persisting the value across frames in its own state.
	/// </summary>
	string TextField(string current, int maxLength = 64);

	/// <summary>Draw a small vertical separator.</summary>
	void Separator();
}
