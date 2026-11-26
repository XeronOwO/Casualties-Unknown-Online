namespace CasualtiesUnknownOnline.Runtime.Networking;

/// <summary>
/// The identity facts of whatever transport currently carries the game, as the
/// save layer needs them (§2, decision 162): which key space a run is in, the
/// local peer's id, and the display name the transport advertises for it.
///
/// This is deliberately narrower than <c>INetworkTransport</c> — the save layer
/// never sends anything, it only needs to know who "we" are. In solo play no
/// session exists, but the local peer still has an identity: that is why the
/// local id comes from here and not from the session.
/// </summary>
public interface ITransportIdentity
{
	/// <summary>True = the active transport is IP-direct (its players key on display names, not accounts).</summary>
	bool IsIpDirect { get; }

	/// <summary>The local peer's id in the active transport (0 when it has none yet).</summary>
	ulong LocalPeerId { get; }

	/// <summary>The display name the active transport advertises for the local peer ("" when unknown).</summary>
	string LocalDisplayName { get; }

	/// <summary>
	/// The display name this client presents in the IP-direct key space, from the persisted
	/// configuration rather than from whichever transport is active now ("" when unconfigured).
	/// It stays readable after an IP session ends, because a continue opens the archive before
	/// the next session exists — and the IP-direct key IS this name, so a continue that only
	/// saw the Steam persona could not recognise the local player's own stored character.
	/// </summary>
	string LocalIpDirectDisplayName { get; }

	/// <summary>
	/// The local client's own persistent identity ("" when the transport has none). It is the
	/// fact that keeps two IP-direct players who share a display name apart: duplicate names
	/// are allowed, and the key gains a digest of this instead of collapsing both players
	/// onto one file (2026-11-19).
	/// </summary>
	string LocalClientIdentity { get; }
}
