using CasualtiesUnknownOnline.Runtime.OnlineUi;

namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The Online UI's native surface on the game's own UI (ticket online-ui-art-and-controls-overhaul,
/// S2a): a CUO canvas parented under the game's canvas, and on it the launcher button that opens the
/// Online UI window — the game's own control prefab, at the launcher's own screen rect, at the opacity
/// the idle rule derived.
///
/// <para>
/// The contract is deliberately total, like the probe's: <see cref="Push"/> never throws at the caller's
/// frame callback, and a frame that arrives before the game has a canvas is dropped — the surface
/// appears by itself once a canvas exists, and rebuilds if the canvas it was parented under goes away.
/// Nothing the surface created outlives the adapter.
/// </para>
///
/// <para>
/// What the player did comes back through <see cref="TryDequeueIntent"/>, one fact at a time, oldest
/// first: the surface reports the interaction, the Runtime decides what it means.
/// </para>
/// </summary>
public interface IOnlineUiSurface
{
	/// <summary>Shows one frame's state; a frame that cannot be shown yet is dropped, not queued.</summary>
	void Push(OnlineUiFrame frame);

	/// <summary>Takes the oldest queued intent; false when nothing happened since the last call.</summary>
	bool TryDequeueIntent(out OnlineUiIntent intent);
}
