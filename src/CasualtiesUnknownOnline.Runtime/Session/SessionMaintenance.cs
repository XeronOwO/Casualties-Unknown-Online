using System;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The session's per-update peer work: warm up lobby peers that have not handshaken yet, run the
/// ping schedule, retry a pending handshake while no run is active, and check presence.
/// </summary>
/// <remarks>
/// The order is the behaviour and it lives here once. A host must send warmup traffic before the
/// ping schedule runs (a Steam P2P session only establishes with traffic from both directions, and
/// the periodic ping covers presence members only, so a late joiner would otherwise never receive
/// host-to-guest traffic and its handshake would die with a 5003 connect timeout). The heartbeat
/// does not depend on the world, so it ticks in both branches; the handshake retry only makes
/// sense while no run is active, and the presence check runs in both branches.
/// <para>
/// The session control keeps the ping itself (<c>SessionService.RequestPing</c>) and the peers'
/// kick/reset commands; this type only owns who runs when, per update.
/// </para>
/// </remarks>
internal sealed class SessionMaintenance(
	SessionPeerMaintenance peers,
	SessionHeartbeat heartbeat,
	Func<bool> sessionActive)
{
	private readonly SessionPeerMaintenance _peers = peers ?? throw new ArgumentNullException(nameof(peers));
	private readonly SessionHeartbeat _heartbeat = heartbeat ?? throw new ArgumentNullException(nameof(heartbeat));
	private readonly Func<bool> _sessionActive = sessionActive ?? throw new ArgumentNullException(nameof(sessionActive));

	/// <summary>Runs one update's peer work in the session's order.</summary>
	public void Tick()
	{
		_peers.SendPeerWarmup();
		_heartbeat.Tick();

		if (!_sessionActive())
		{
			_peers.RetryHandshakeIfNeeded();
		}

		// The entity-sync decisions and the 20 Hz stream run in EntitySyncService.Update; the
		// receive dispatch in PacketDispatcher (both registered after us).
		_peers.CheckPeerPresence();
	}
}
