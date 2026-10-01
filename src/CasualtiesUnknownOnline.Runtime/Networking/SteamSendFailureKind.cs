namespace CasualtiesUnknownOnline.Runtime.Networking;

/// <summary>
/// The coarse failure family behind a Steam P2P send failure. Kept separate
/// from the Steamworks enums so the transport can log a compact,
/// actionable label and tests can lock the classification without a full
/// Steam session.
/// </summary>
public enum SteamSendFailureKind
{
	/// <summary>No failure family identified (generic send error).</summary>
	Other,

	/// <summary>The remote peer rejected our Steam certificate (<c>Remote_BadCert</c>).</summary>
	BadCert,

	/// <summary>Steam Datagram Relay rendezvous / relay connectivity failed.</summary>
	Rendezvous,

	/// <summary>Steam reported the P2P connection attempt failed.</summary>
	ConnectFailed,

	/// <summary>No Steam P2P session exists for the peer.</summary>
	NoConnection,

	/// <summary>The P2P link timed out.</summary>
	Timeout,

	/// <summary>Steam refused to queue the message because the peer's send queue is full
	/// (<c>k_EResultLimitExceeded</c>) — transient congestion, not a dead link: the peer
	/// cannot drain what we send, so more sends right now are pointless.</summary>
	QueueFull,
}
