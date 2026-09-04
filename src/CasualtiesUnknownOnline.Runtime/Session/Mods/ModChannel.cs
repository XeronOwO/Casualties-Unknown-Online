using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;
using System;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The mod-message channel (NetMsg.ModMessage — Phase 4 Mod API). Report/定向
/// semantics, star topology, NO auto-relay: a guest's SendToHost reaches the
/// host's copy of the mod only; directed/broadcast sends are host-only. Guard
/// semantics are explicit per call: the wrong role is a no-op (a host mod
/// "sending to host" is talking to itself locally; a guest has no peer
/// channels), an inactive session is a no-op, and an over-length payload is
/// refused HERE (the mod learns immediately instead of the receive side
/// silently dropping it).
/// </summary>
public sealed class ModChannel(ISessionControl session, PacketSender sender, ILogger<ModChannel> log)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly ILogger<ModChannel> _log = log;

	/// <summary>
	/// The mod-payload policy cap — 64 KiB. NOT a line limit (Steam's single-
	/// message ceiling is 1 MB): it is a framework policy rail — a reliable-
	/// channel frame this size is the worst case for head-of-line blocking on a
	/// congested link, so a single mod must not saturate it (the same reasoning
	/// as the wire-transport decision).
	/// </summary>
	public const int MaxPayloadBytes = 64 * 1024;

	/// <summary>Guest: report a payload to the host's copy of the mod. No-op on the host and outside a session.</summary>
	public void SendToHost(string modId, byte[] payload)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive || !CheckLength(modId, payload))
		{
			return;
		}

		_sender.Send(_session.HostSteamId, NetMsg.ModMessage, new ModMessageMsg { ModId = modId, Payload = payload });
	}

	/// <summary>Host only: send a payload to one member's copy of the mod. No-op for a guest and outside a session.</summary>
	public void SendToPeer(string modId, ulong steamId, byte[] payload)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !CheckLength(modId, payload))
		{
			return;
		}

		_sender.Send(steamId, NetMsg.ModMessage, new ModMessageMsg { ModId = modId, Payload = payload });
	}

	/// <summary>
	/// Host only: broadcast a payload to every member's copy of the mod —
	/// including the host's own (the peers get the frame, the host gets a local
	/// fire with its own SteamId: "all sides run this" is one call).
	/// </summary>
	public void SendToAll(string modId, byte[] payload)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !CheckLength(modId, payload))
		{
			return;
		}

		var msg = new ModMessageMsg { ModId = modId, Payload = payload };
		_session.Broadcast(NetMsg.ModMessage, msg);
		FireModMessageReceived(_session.LocalSteamId, msg);
	}

	/// <summary>A mod frame arrived: a report on the host, a directed/broadcast frame on a guest.</summary>
	public event Action<ulong, ModMessageMsg>? ModMessageReceived;

	public void FireModMessageReceived(ulong sender, ModMessageMsg msg) => ModMessageReceived?.Invoke(sender, msg);

	private bool CheckLength(string modId, byte[] payload)
	{
		if (payload.Length <= MaxPayloadBytes)
		{
			return true;
		}

		_log.LogWarning("Mod {ModId} payload {Length} bytes exceeds the {Cap} cap — refused.", modId, payload.Length, MaxPayloadBytes);
		return false;
	}
}
