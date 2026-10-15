namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// The arithmetic behind a page's scroll affordance (ticket online-ui-paired-controls-and-page-scroll, S4):
/// whether a page needs one at all, how much of the track its handle fills, where the handle stands for the
/// position the page is scrolled to, and what position a click on the track asks for.
///
/// <para>
/// It is pure and lives in the Runtime for the reason <see cref="OnlineUiRowLayout"/> and
/// <see cref="OnlineUiControlBox"/> do: the user's report is "the Admin page has a part that goes past what is
/// shown, and I don't see one on the right", and the answer to it — a handle whose size and place say how much
/// more there is and where in it the player stands — is arithmetic that can be settled without a Unity
/// runtime. The adapter draws what this returns and converts the clicks back through it.
/// </para>
///
/// <para>
/// A page that FITS answers <see cref="Single"/>, and the caller draws nothing for it: a scrollbar over a page
/// with nothing to scroll is a control that does not work. A page with no measurable viewport yet (the frame
/// before the engine has laid the surface out) answers <see cref="Single"/> too, so the bar cannot flash at
/// full size on the first frame.
/// </para>
/// </summary>
public static class OnlineUiPageScroll
{
	/// <summary>The handle of a page that fits: the whole track, at the top. The caller reads this as "there is
	/// nothing to scroll" — a fraction of 1 is a handle as tall as the track it stands on.</summary>
	public static OnlineUiScrollHandle Single => new(1f, 0f);

	/// <summary>The bar's own width: the track its handle slides on, drawn at the page band's trailing edge. It
	/// is narrower than a control's own edge so a page reads it as a margin note rather than as another control,
	/// and wide enough for a pointer to land on and drag.</summary>
	public const float TrackWidth = 8f;

	/// <summary>The room the bar keeps from the band's own trailing edge, so it does not touch the frame.</summary>
	public const float TrackInset = 2f;

	/// <summary>
	/// The room the bar takes on a page's band, which the page's CONTENT keeps clear of: the bar occupies the
	/// outermost <see cref="TrackInset"/> plus <see cref="TrackWidth"/> units of the band, so a row's box stops
	/// this far inside the band instead of running under it (the user's first point of 2026-10-15: "the content
	/// should keep clear of the scrollbar — that is what normal front-end design does").
	///
	/// <para>
	/// The room is kept whether or not the bar is DRAWN, and that is the point rather than an oversight: how
	/// tall a page is depends on the width its rows are wrapped in, so a page that took the room only while its
	/// bar was up would wrap narrower the moment it overflowed, and the wrapping that decided the bar was needed
	/// would itself change with it. A page that fits leaves the room empty.
	/// </para>
	/// </summary>
	public const float GutterWidth = TrackInset + TrackWidth;

	/// <summary>
	/// The handle for one page: the fraction of the track it fills is the fraction of the content the viewport
	/// shows, and its place along the track is the fraction the page is scrolled by. A viewport that shows
	/// everything (or a content and a viewport that cannot be measured yet) answers <see cref="Single"/>.
	/// </summary>
	public static OnlineUiScrollHandle For(float viewportHeight, float contentHeight, float position)
	{
		if (viewportHeight <= 0f || contentHeight <= viewportHeight + FitsTolerance)
		{
			return Single;
		}

		var fraction = Clamp(viewportHeight / contentHeight, MinimumFraction, 1f);
		var offset = Clamp(1f - position, 0f, 1f);
		return new OnlineUiScrollHandle(fraction, offset);
	}

	/// <summary>
	/// The scroll position a click at <paramref name="offset"/> along the track asks for — the same number a
	/// position and a handle turn back into, so the bar and the page cannot disagree about where the player
	/// is. 1 is the top of the page, 0 its end (the convention every scroll view of this engine uses).
	/// </summary>
	public static float PositionFor(float offset) => Clamp(1f - offset, 0f, 1f);

	/// <summary>The engine's own clamp, written out because this tree targets a framework that does not ship
	/// one and the rule has to read the same on both sides.</summary>
	private static float Clamp(float value, float minimum, float maximum) =>
		value < minimum ? minimum : value > maximum ? maximum : value;

	/// <summary>How much taller than its viewport a page has to be before it is worth a bar. A page a fraction
	/// of a unit taller than the window is a page that fits, and a bar for it would be a bar that does not
	/// move.</summary>
	private const float FitsTolerance = 0.5f;

	/// <summary>The least of the track a handle ever fills, so the bar of a very long page is still something
	/// the player can see and grab.</summary>
	private const float MinimumFraction = 0.05f;
}
