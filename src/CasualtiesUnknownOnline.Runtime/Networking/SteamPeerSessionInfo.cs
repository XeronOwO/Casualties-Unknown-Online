using Steamworks;

namespace CasualtiesUnknownOnline.Runtime.Networking;

/// <summary>
/// One reading of a peer's ISteamNetworkingMessages session: the connection
/// state, the connection end reason and Steam's own debug string. This is what
/// the send-failure diagnostics print, lifted out of <see cref="SteamTransport"/>
/// so the refusal path can be driven by a test without a live Steam client.
/// </summary>
internal readonly record struct SteamPeerSessionInfo(
	ESteamNetworkingConnectionState State,
	ESteamNetConnectionEnd EndReason,
	string Debug);
