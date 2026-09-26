namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// What one world-space marker is (ticket online-ui-art-and-controls-overhaul, S6): a remote player's
/// nameplate — which becomes an edge arrow once the player leaves the screen — or a transient location
/// ping. Both are the same fact to the surface (a world point, a colour and the text that belongs to it),
/// and the kind is what says whether a mark is drawn AT the point.
///
/// <para>
/// It travels on the model rather than being inferred from an empty text: a marker with no glyph is a
/// nameplate BY DESIGN, and a decision the surface reads is one a test can hold.
/// </para>
/// </summary>
public enum OnlineUiWorldMarkerKind
{
	/// <summary>A remote player: the name above their head on screen, an arrow at the screen's edge with
	/// the name and the distance once they are off it.</summary>
	Nameplate,

	/// <summary>A location ping: its own mark (an exclamation or a dot) at the point on screen, the same
	/// mark beside the pinger's name once it is off screen.</summary>
	LocationPing,
}
