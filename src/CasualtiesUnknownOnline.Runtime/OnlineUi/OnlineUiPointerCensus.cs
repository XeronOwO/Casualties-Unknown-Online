namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// Which CUO Online UI surface the pointer is over, and which world input a covering surface must
/// therefore not receive (ticket online-ui-art-and-controls-overhaul, S4 — the retirement pass, closed
/// out by S5).
///
/// <para>
/// It exists because the two paths that ask the question — the world middle-click that becomes a location
/// ping and the world right-click that opens a player's in-world menu — used to keep their own lists of
/// surfaces, and the launcher's rectangle was on neither of them (the limit S2a recorded): one rule, two
/// questions, and no second list to drift away from the first.
/// </para>
///
/// <para>
/// The facts are pushed in, because where the pointer is belongs to whoever can see it: every CUO surface
/// is a uGUI control on the game's own canvas now, so the game's <c>EventSystem</c> is what sees the
/// pointer over them and the adapter polls each surface's own rectangle and reports a flip (the plugin
/// holds the last answer). S5 moved the quick panel and the player context menu onto that surface too, so
/// the rectangles the census used to hold — the two IMGUI panels' GUI-space rects — are gone with them:
/// four facts, no geometry, and nothing here is Unity, which is why the rule and its edges are testable
/// without a runtime.
/// </para>
/// </summary>
public sealed class OnlineUiPointerCensus
{
	/// <summary>True while the pointer is over the launcher control (the surface's own poll).</summary>
	public bool OverLauncher { get; set; }

	/// <summary>True while the pointer is over the window's own rectangle (the surface's own poll).</summary>
	public bool OverWindow { get; set; }

	/// <summary>True while the pointer is over the standalone quick panel (the surface's own poll).</summary>
	public bool OverQuickPanel { get; set; }

	/// <summary>True while the pointer is over the in-world player context menu (the surface's own poll).</summary>
	public bool OverContextMenu { get; set; }

	/// <summary>
	/// True while a CUO surface that covers the screen owns the input: the command console overlay, or
	/// the window the player opened. The game's own keyboard and mouse input is suppressed while one is
	/// open, so no world action belongs to the pointer at all.
	/// </summary>
	public bool ModalSurfaceOpen { get; set; }

	/// <summary>
	/// True when a world middle-click must not become a location ping: while a modal CUO surface owns the
	/// screen no world action belongs to the pointer anywhere, and over any CUO surface the click belongs
	/// to the surface.
	/// </summary>
	public bool BlocksWorldPing() => ModalSurfaceOpen || BlocksWorldMenu();

	/// <summary>
	/// True when a world right-click must not open, re-target or close the in-world player menu. Note the
	/// deliberate asymmetry with <see cref="BlocksWorldPing"/>: the modal surface owns the SCREEN, not the
	/// world menu, so a right-click outside the window's own rectangle still targets a player — the
	/// behaviour the IMGUI window had, kept.
	/// </summary>
	public bool BlocksWorldMenu() =>
		OverLauncher || OverWindow || OverQuickPanel || OverContextMenu;
}
