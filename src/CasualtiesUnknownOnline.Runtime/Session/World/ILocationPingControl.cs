using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The local location-ping surface used by the plugin/UI. It owns the
/// one-marker-per-player transient buffer, the middle-click double-click
/// rule, and the send path to the world channel. It touches no Unity/game type.
/// </summary>
public interface ILocationPingControl
{
	/// <summary>Active non-expired pings, ordered by sender SteamId.</summary>
	IReadOnlyList<LocationPing> ActivePings { get; }

	/// <summary>
	/// Place/upgrade the local player's marker. A first click creates a circle;
	/// a second click within <see cref="LocationPingService.DoubleClickWindowMs"/>
	/// upgrades it to an exclamation at the new cursor position. Returns false
	/// when the session is not active or the local player is not in a world.
	/// </summary>
	bool TryPlace(float x, float y);

	/// <summary>Removes expired pings. Called by the UI frame path and the tests.</summary>
	void Prune();
}
