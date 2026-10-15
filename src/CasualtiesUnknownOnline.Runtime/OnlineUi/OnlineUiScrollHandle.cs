namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// Where a page's scroll handle stands (ticket online-ui-paired-controls-and-page-scroll, S4), in units of the
/// track rather than in canvas units, so the same answer works whatever the window's size or the player's
/// canvas scale.
///
/// <para>
/// <see cref="Fraction"/> is how much of the track the handle fills — the share of the page's content the
/// window is showing — and <see cref="Offset"/> is where its TOP stands from the top of the track, which is
/// what makes it move as the page is scrolled. Both are fractions of the track, so a caller multiplies them by
/// the track it drew; nothing here is a pixel.
/// </para>
/// </summary>
public readonly record struct OnlineUiScrollHandle(float Fraction, float Offset);
