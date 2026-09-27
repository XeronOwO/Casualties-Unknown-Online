namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// The Online UI window's shell measurements, in the canvas units the game's own controls are laid out in
/// (ticket online-ui-layout-and-input-detail-pass, S1). The shell is three bands stacked from the top — the
/// title bar, the tab strip and the page — and this type is the ONE place their arithmetic lives, so a band
/// cannot drift into its neighbour: the page's top edge is <see cref="PageTop"/>, which is the tab strip's
/// own bottom edge plus <see cref="TabGap"/>, and <see cref="TabBottom"/> is where the tab strip ends.
///
/// <para>
/// It is pure and lives in the Runtime for the same reason <see cref="OnlineUiRowLayout"/> does: the shape
/// the player sees is decided and tested here, without a Unity runtime, and the adapter only reads it. The
/// values are the user's acceptance pass' own asks (2026-09-27): a page with room in it, a tab strip that
/// does not touch the body, and one compact height for every control of a page.
/// </para>
/// </summary>
public static class OnlineUiWindowLayout
{
	/// <summary>The window's own rect: the box the player drags around, wider and taller than the first cut so
	/// a page of live session facts has room. The text and the controls inside keep their own sizes — this is
	/// the frame, not a zoom.</summary>
	public const float Width = 1000f;

	/// <inheritdoc cref="Width"/>
	public const float Height = 700f;

	/// <summary>The window's inner margin: the title bar, the tab strip and the page all keep this much room
	/// from the frame's left and right edges.</summary>
	public const float Padding = 8f;

	/// <summary>The title bar's height, the band the player drags.</summary>
	public const float TitleHeight = 28f;

	/// <summary>The tab strip's height: one row of tabs, all of them this tall. 36 rather than the first cut's
	/// 30 because the game's font renders a Chinese caption 27.9 units tall at size 20: at 30 the glyphs sat
	/// within about a unit of the frame's edges ("the Chinese text touches the bottom" — user report,
	/// 2026-09-27) while the English captions had room.</summary>
	public const float TabHeight = 36f;

	/// <summary>The gap above and below the tab strip. Below it is what the acceptance pass asked for: the
	/// body of a page must not sit against the tabs.</summary>
	public const float TabGap = 10f;

	/// <summary>The height of one control of a page — a button, a dropdown, a field, a toggle, a slider, a
	/// colour block. They share it so a page reads as one column of controls instead of the game's own row
	/// prefabs' very different authored heights, and it tracks <see cref="TabHeight"/> for the same reason
	/// that height grew: a Chinese caption renders 27.9 units tall at size 20 (user report, 2026-09-27).</summary>
	public const float ControlHeight = 36f;

	/// <summary>
	/// The page's vertical rhythm, in the same canvas units. A page is a list of rows, and a uGUI layout group
	/// spaces its children evenly with no margin on any single one — so the room a block needs is a row of its
	/// own (<see cref="OnlineUiRowModel.Space"/>) and these are the numbers that row carries. They live here,
	/// beside the shell's measurements, because a page's rhythm is ONE scale: the acceptance pass found section
	/// headings with no room above them, because every drawer inserted a gap of its own devising.
	/// </summary>
	public const float RowGap = 4f;

	/// <summary>The room a heading takes above itself: it opens a block, so it is separated from whatever came
	/// before it by clearly more than two ordinary rows.</summary>
	public const float SectionGap = 16f;

	/// <summary>The room between a heading and the first row of its own block: enough to bind the heading to
	/// what it names, less than the room above it so the heading reads as that block's own.</summary>
	public const float SectionBodyGap = 6f;

	/// <summary>The room between two blocks that carry no heading of their own.</summary>
	public const float BlockGap = 10f;

	/// <summary>The title bar's top edge, below the frame's own padding — the shape the shell's own layout group
	/// produces when it stacks the bands: the engine places them, and these four derived values are what it
	/// places them AT, so the shell can be reasoned about and tested without a Unity runtime.</summary>
	public const float TitleTop = Padding;

	/// <summary>The tab strip's top edge, below the title bar and its gap.</summary>
	public const float TabTop = TitleTop + TitleHeight + TabGap;

	/// <summary>The tab strip's bottom edge. The page starts below this line — see <see cref="PageTop"/>.</summary>
	public const float TabBottom = TabTop + TabHeight;

	/// <summary>The page's top edge: the tab strip's bottom edge plus the page's own gap from the tabs.</summary>
	public const float PageTop = TabBottom + TabGap;

	/// <summary>The width a page's rows are laid out in: the frame minus its padding on both sides.</summary>
	public const float ContentWidth = Width - (2f * Padding);

	/// <summary>How much room a page has: the frame minus the padding at the bottom and the page's top edge.
	/// Exposed because the surface clamps the frame to the game's canvas, and a clamped frame has to keep a
	/// usable page.</summary>
	public const float PageHeight = Height - PageTop - Padding;
}
