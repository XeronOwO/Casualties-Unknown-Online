namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The surface the drag-use cue is drawn on. The decision (whether a release would use the item, and on WHOM)
/// is <see cref="IDragUseCueState"/>'s; this port only carries the finished sentence to a place the player can
/// read. It exists because the game draws its own UI with TextMeshPro, and a plugin-side IMGUI box cannot use
/// the game's font or material — the user's 2026-11-07 ruling: a CUO surface the player sees must look like the
/// game's own UI, not like a Windows window.
/// </summary>
public interface IDragUseCuePresenter
{
	/// <summary>Shows <paramref name="text"/> beside the cursor at the given screen point, or moves it there.</summary>
	/// <param name="text">The localised sentence, already naming the player the release would use the item on.</param>
	/// <param name="screenX">Cursor x in screen pixels, measured from the left edge.</param>
	/// <param name="screenY">Cursor y in screen pixels, measured from the BOTTOM edge (Unity's own convention).</param>
	void Show(string text, float screenX, float screenY);

	/// <summary>Hides the label; safe to call every frame.</summary>
	void Hide();
}
