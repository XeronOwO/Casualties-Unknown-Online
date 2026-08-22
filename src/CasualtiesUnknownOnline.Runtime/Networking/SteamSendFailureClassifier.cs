using Steamworks;

namespace CasualtiesUnknownOnline.Runtime.Networking;

/// <summary>
/// Pure classification for Steam P2P send failures: maps the low-level
/// <see cref="EResult"/> / <see cref="ESteamNetConnectionEnd"/> pair onto the
/// compact <see cref="SteamSendFailureKind"/> families the transport logs.
/// No transport/Steam state is touched here, so the mapping is L0-testable.
/// The remediation strings deliberately name the known local trigger first
/// (Clash on localhost:7890 has produced BadCert / rendezvous timeouts on
/// this machine) rather than burying it in a generic retry message.
/// </summary>
public static class SteamSendFailureClassifier
{
	public static SteamSendFailureKind Classify(EResult result, ESteamNetConnectionEnd endReason)
	{
		if (endReason == ESteamNetConnectionEnd.k_ESteamNetConnectionEnd_Remote_BadCert)
		{
			return SteamSendFailureKind.BadCert;
		}

		if (endReason == ESteamNetConnectionEnd.k_ESteamNetConnectionEnd_Misc_P2P_Rendezvous
			|| endReason == ESteamNetConnectionEnd.k_ESteamNetConnectionEnd_Local_ManyRelayConnectivity)
		{
			return SteamSendFailureKind.Rendezvous;
		}

		return result switch
		{
			EResult.k_EResultConnectFailed => SteamSendFailureKind.ConnectFailed,
			EResult.k_EResultNoConnection => SteamSendFailureKind.NoConnection,
			EResult.k_EResultTimeout => SteamSendFailureKind.Timeout,
			_ => SteamSendFailureKind.Other,
		};
	}

	public static string Remediation(SteamSendFailureKind kind) => kind switch
	{
		SteamSendFailureKind.BadCert =>
			"check the local proxy/network first (Clash on localhost:7890 has triggered BadCert), then the Steam client state, then the Steamworks.NET wrapper",
		SteamSendFailureKind.Rendezvous =>
			"check the local proxy/Steam Datagram Relay status (local proxy is the first known trigger for rendezvous timeouts), then Steam client state",
		SteamSendFailureKind.ConnectFailed =>
			"Steam P2P connection attempt failed; the host warm-up backoff keeps retrying and a healthy peer resets on the next successful send",
		SteamSendFailureKind.NoConnection =>
			"no Steam P2P session with this peer; verify both clients are online and in the same lobby",
		SteamSendFailureKind.Timeout =>
			"the P2P link timed out; retries self-heal when connectivity returns",
		_ => "generic send failure; see the session state and end reason below",
	};
}
