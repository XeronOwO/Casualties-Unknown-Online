using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.GameAdapter;

namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// Which CUO Online UI surface the pointer is over, and which world input a covering surface must
/// therefore not receive (ticket online-ui-art-and-controls-overhaul, S4 — the retirement pass).
///
/// <para>
/// It exists because the two paths that ask the question — the world middle-click that becomes a location
/// ping and the world right-click that opens a player's in-world menu — used to keep their own lists of
/// surfaces, and the launcher's rectangle was on neither of them (the limit S2a recorded): one rule, two
/// questions, and no second list to drift away from the first.
/// </para>
///
/// <para>
/// The facts are pushed in, because where the pointer is belongs to whoever can see it: the migrated
/// controls are uGUI, so the game's own <c>EventSystem</c> is what sees the pointer over the launcher and
/// the window and the adapter polls their rectangles and reports a flip (the plugin holds the last
/// answer), while the panels CUO still draws in IMGUI are the plugin's own rectangles. Nothing here is
/// Unity, so the rule and its edges are testable without a runtime.
/// </para>
/// </summary>
public sealed class OnlineUiPointerCensus
{
	/// <summary>True while the pointer is over the launcher control (the surface's own poll).</summary>
	public bool OverLauncher { get; set; }

	/// <summary>True while the pointer is over the window's own rectangle (the surface's own poll).</summary>
	public bool OverWindow { get; set; }

	/// <summary>
	/// True while a CUO surface that covers the screen owns the input: the command console overlay, or
	/// the window the player opened. The game's own keyboard and mouse input is suppressed while one is
	/// open, so no world action belongs to the pointer at all.
	/// </summary>
	public bool ModalSurfaceOpen { get; set; }

	/// <summary>
	/// The GUI-space rectangles (origin top-left, Y down) of the surfaces CUO still draws itself — the
	/// quick panel and the player context menu. They are IMGUI, invisible to uGUI, so their own
	/// rectangles are the only fact there is; they are also the list the adapter's scoped raycast
	/// blockers are built from, and the two must never disagree.
	/// </summary>
	public IReadOnlyList<OnlineUiBlockRect> OverlayRects { get; set; } = [];

	/// <summary>
	/// True when a world middle-click at this GUI-space point must not become a location ping: while a
	/// modal CUO surface owns the screen no world action belongs to the pointer anywhere, and over any
	/// CUO surface the click belongs to the surface.
	/// </summary>
	public bool BlocksWorldPing(float guiX, float guiY) => ModalSurfaceOpen || BlocksWorldMenu(guiX, guiY);

	/// <summary>
	/// True when a world right-click at this GUI-space point must not open, re-target or close the
	/// in-world player menu. Note the deliberate asymmetry with <see cref="BlocksWorldPing"/>: the modal
	/// surface owns the SCREEN, not the world menu, so a right-click outside the window's own rectangle
	/// still targets a player — the behaviour the IMGUI window had, kept.
	/// </summary>
	public bool BlocksWorldMenu(float guiX, float guiY) =>
		OverLauncher || OverWindow || IsInsideOverlayRect(guiX, guiY);

	private bool IsInsideOverlayRect(float guiX, float guiY)
	{
		foreach (var rect in OverlayRects)
		{
			if (rect.Contains(guiX, guiY))
			{
				return true;
			}
		}

		return false;
	}
}
