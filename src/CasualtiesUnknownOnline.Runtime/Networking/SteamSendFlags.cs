using Steamworks;

namespace CasualtiesUnknownOnline.Runtime.Networking;

/// <summary>
/// The Steam networking send-flag combination used by <see cref="SteamTransport"/>.
/// Every send carries <c>k_nSteamNetworkingSend_AutoRestartBrokenSession</c>:
/// ISteamNetworkingMessages sessions are implicitly created and can be broken
/// by a peer close or a P2P error (including the observed BadCert /
/// rendezvous failures). Without this flag a broken session keeps returning
/// <c>k_EResultNoConnection</c> until the caller closes it explicitly; with it
/// the next send automatically re-establishes a fresh session. The flag is a
/// no-op on healthy sessions, so it is safe for both the reliable CUO control
/// path and the unreliable 20 Hz state stream.
/// </summary>
internal static class SteamSendFlags
{
	public static int For(bool reliable) =>
		(reliable ? Constants.k_nSteamNetworkingSend_Reliable : Constants.k_nSteamNetworkingSend_Unreliable)
		| Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession;
}
