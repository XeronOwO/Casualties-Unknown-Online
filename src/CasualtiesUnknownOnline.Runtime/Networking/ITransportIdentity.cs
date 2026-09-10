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
}
