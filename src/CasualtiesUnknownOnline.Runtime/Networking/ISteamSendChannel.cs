using Steamworks;

namespace CasualtiesUnknownOnline.Runtime.Networking;

/// <summary>
/// The send side of ISteamNetworkingMessages behind one seam: the send call
/// plus the two diagnostic queries the failure path prints. It exists so the
/// refusal handling in <see cref="SteamTransport"/> is a pure decision the test
/// suite can drive to failure without a live Steam client — the same reason the
/// test suite's FakeTransport exists for the receive side. The receive pump is
/// deliberately NOT part of this seam: this change only hardens the send path.
/// </summary>
internal interface ISteamSendChannel
{
	/// <summary>Queues one message. The returned <see cref="EResult"/> is Steam's verdict on
	/// the queueing, never the delivery outcome of the message.</summary>
	EResult Send(ulong steamId, byte[] data, bool reliable);

	/// <summary>Session state, end reason and debug string for one peer — the failure diagnostics' context.</summary>
	SteamPeerSessionInfo DescribeSession(ulong steamId);

	/// <summary>Steam Datagram Relay availability — appended to the failure diagnostics.</summary>
	SteamRelayStatus DescribeRelay();
}
