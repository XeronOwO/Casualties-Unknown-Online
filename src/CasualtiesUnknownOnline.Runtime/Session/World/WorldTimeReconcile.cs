namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// What the initiator's own client must do with an arriving authoritative speed
/// (see <see cref="WorldTimeLocalInitiation"/>).
/// </summary>
public enum WorldTimeReconcile
{
	/// <summary>The local clock already runs this value — write nothing and replay no sound.</summary>
	None,

	/// <summary>The host's value is new for a client that was not ahead of it — apply it at once.</summary>
	Adopt,

	/// <summary>This client is ahead of the host (its local initiation was refused or overridden) — ramp down to the host's value.</summary>
	Ramp,
}
