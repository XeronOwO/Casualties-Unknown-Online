namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// One world-space marker for one frame (ticket online-ui-art-and-controls-overhaul, S6): the nameplates
/// and off-screen arrows that follow the remote players, and the transient location pings. A plain value
/// like every other model that crosses the adapter boundary — the plugin builds it from the runtime facts
/// it already reads, and the surface puts it on the game's own canvas.
///
/// <para>
/// <see cref="X"/> and <see cref="Y"/> are the marker's position in the GAME WORLD's own plane, not a
/// screen point: only the adapter may reach the camera, so the projection, the decision between the
/// on-screen and the off-screen form, and the clamp onto the screen's edge all belong to the surface —
/// through the Runtime's own rules (<see cref="OffScreenArrowGeometry"/>, <see cref="NameplateLayout"/>,
/// <see cref="OffScreenArrowText"/>), which is what keeps them testable without Unity.
/// </para>
///
/// <para>
/// The TEXT travels composed: <see cref="OnScreenText"/> is the label of the on-screen form,
/// <see cref="OffScreenText"/> the label of the off-screen one, and <see cref="Glyph"/> the mark drawn at
/// the point itself (empty for a nameplate, which draws no mark). How a label is built — the name, the
/// distance, the ping's mark beside it — is presentation and stays on the plugin side, where the
/// translations live; the surface only decides WHERE each of the three goes. <see cref="Kind"/> is what
/// decides whether a mark is drawn at all, and the two factories are the only production constructors, each
/// writing the kind and the glyph together — so an empty glyph and a nameplate cannot drift apart.
/// </para>
/// </summary>
public readonly record struct OnlineUiWorldMarker(
	OnlineUiWorldMarkerKind Kind,
	float X,
	float Y,
	string OnScreenText,
	string OffScreenText,
	string Glyph,
	OnlineUiNativeRgba Color)
{
	/// <summary>A remote player's marker: the name above the head while they are on screen, and the name
	/// with the distance beside the edge arrow once they are not. The colour is the player's own, so the
	/// marker and the member list agree about who is who.</summary>
	public static OnlineUiWorldMarker Nameplate(
		float x,
		float y,
		string name,
		string distanceText,
		OnlineUiNativeRgba color) =>
		new(
			OnlineUiWorldMarkerKind.Nameplate,
			x,
			y,
			name,
			string.Concat(name, "  ", distanceText),
			"",
			color);

	/// <summary>A location ping: its own mark at the point with the pinger's name under it on screen, and
	/// the same mark beside the name when it is pinned to the screen's edge.</summary>
	public static OnlineUiWorldMarker Ping(
		float x,
		float y,
		string name,
		string glyph,
		OnlineUiNativeRgba color) =>
		new(
			OnlineUiWorldMarkerKind.LocationPing,
			x,
			y,
			name,
			string.Concat(name, " ", glyph),
			glyph,
			color);
}
