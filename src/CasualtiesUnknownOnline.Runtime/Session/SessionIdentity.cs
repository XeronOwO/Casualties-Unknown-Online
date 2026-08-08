namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The client's lobby identity, decoupled from the session state machine so
/// data-plane components (PacketGateway's direction validation) can depend on
/// it without a dependency cycle. Role follows the lobby (creator = Host,
/// joiner = Guest) and is NEVER cleared by EndSession; HostSteamId is cleared
/// when the session ends.
/// </summary>
public sealed class SessionIdentity
{
	public SessionRole Role { get; internal set; }

	public ulong HostSteamId { get; internal set; }
}
