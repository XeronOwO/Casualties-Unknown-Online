namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>The guest's answer to the host's descent edge.</summary>
internal enum LayerDescentDecision
{
	/// <summary>This member is playing in a live world the host is leaving — generate the new layer now.</summary>
	Follow,

	/// <summary>Not a guest in a live session: the host's and a solo player's descent is not this edge.</summary>
	NotThisSides,

	/// <summary>A descent is already running on this member — a second edge changes nothing.</summary>
	AlreadyDescending,

	/// <summary>In the menu, entering, or otherwise not playing: the ordinary entry path is this member's.</summary>
	NotPlaying,

	/// <summary>Playing but no world object to regenerate: the member falls back to the ordinary entry.</summary>
	NoWorld,
}
