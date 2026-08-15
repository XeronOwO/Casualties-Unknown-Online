namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The client's lobby identity, decoupled from the session state machine so
/// data-plane components (PacketReceiver's direction validation) can depend on
/// it without a dependency cycle. Role follows the ACTUAL lobby state:
/// creator = Host, joiner = Guest, no lobby = None (OnLobbyLeft). It is
/// deliberately NOT cleared by EndSession — that method models a same-lobby
/// outage, where the identity survives for a rejoin. HostSteamId is cleared
/// when the session content ends.
/// </summary>
public sealed class SessionIdentity
{
	public SessionRole Role { get; internal set; }

	public ulong HostSteamId { get; internal set; }

	/// <summary>Local SteamID64 — set once Steam initializes (SessionService.Initialize).</summary>
	public ulong LocalSteamId { get; internal set; }
}
