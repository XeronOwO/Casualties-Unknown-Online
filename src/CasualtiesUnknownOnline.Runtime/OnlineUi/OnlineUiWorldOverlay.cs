using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// Everything CUO shows in the world for one frame (ticket online-ui-art-and-controls-overhaul, S6): the
/// network readout, and the markers that follow the remote players and the location pings. It rides on
/// <see cref="OnlineUiFrame"/>, so the overlays cross the adapter boundary with the same frame as the
/// launcher, the window and the panels — one push, one frame, no second surface with a visibility of its
/// own.
///
/// <para>
/// <see cref="Markers"/> is a list the plugin owns and refills: the frame is applied synchronously by the
/// surface, so the same buffer can carry every frame's markers without a copy, and an empty list is the
/// ordinary "nobody to show" state (no remote player in the world, no ping alive). A frame the surface
/// never built — <c>default(OnlineUiFrame)</c>, whose struct default carries no list — is not a supported
/// input, and the surface reads the members as optional rather than trusting this sentence.
/// </para>
/// </summary>
public readonly record struct OnlineUiWorldOverlay(
	OnlineUiNetworkHud? Hud,
	IReadOnlyList<OnlineUiWorldMarker> Markers)
{
	/// <summary>The one empty marker list there is: the state a frame pushes while the start gate or the
	/// command console owns the screen, and the one a session with no remote player in the world is in. One
	/// instance rather than a fresh list per access, so two suppressed frames cannot read as two different
	/// shapes.</summary>
	private static readonly IReadOnlyList<OnlineUiWorldMarker> NoMarkers = [];

	/// <summary>Nothing to show in the world: no readout, no markers.</summary>
	public static OnlineUiWorldOverlay None => new(null, NoMarkers);
}
