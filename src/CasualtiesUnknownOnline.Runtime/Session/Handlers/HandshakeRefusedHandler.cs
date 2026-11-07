using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → guest: the handshake was refused and the dimension is named. The
/// guest shows the line in its own language, logs the key and both values, and
/// ends the session — a refused member has nothing to do on a wire whose
/// semantics differ from its own.
///
/// <para>
/// The frame can arrive before a session is active (that is the whole point), so
/// there is no ack to learn the host from: the lobby's host id is the check when
/// it is known, and a frame from any other peer is dropped as a claim.
/// </para>
/// </summary>
[PacketHandler(NetMsg.HandshakeRefused, NetMessageDirection.HostToGuest)]
public sealed class HandshakeRefusedHandler(
	ILocalizationService localization,
	ISessionNotices notices,
	ILogger<HandshakeRefusedHandler> log)
	: PacketHandlerBase<HandshakeRefusedMsg, ISessionHandlerContext>
{
	private readonly ILocalizationService _localization = localization;
	private readonly ISessionNotices _notices = notices;
	private readonly ILogger<HandshakeRefusedHandler> _log = log;

	protected override void Handle(ulong sender, HandshakeRefusedMsg msg, ISessionHandlerContext ctx)
	{
		var session = ctx.Session;
		if (session.Role != SessionRole.Guest)
		{
			_log.LogWarning("[Handshake] a refusal arrived from {Sender} while this process is {Role} — dropped.", sender, session.Role);
			return;
		}

		if (session.HostSteamId != 0 && sender != session.HostSteamId)
		{
			_log.LogWarning("[Handshake] a refusal arrived from {Sender}, which is not this session's host — dropped.", sender);
			return;
		}

		if (msg.Dimension is 0 or > (byte)HandshakeDimension.ModVersion)
		{
			_log.LogWarning("[Handshake] a refusal arrived with an unknown dimension {Dimension} — dropped.", msg.Dimension);
			return;
		}

		// English log with the key, so the record is readable beside the host's own line; the player sees the
		// same facts rendered from this client's catalogue.
		_log.LogWarning(
			"[Handshake] the host refused this client: {Dimension} (host {Host}, member {Member}, detail {Detail}) — ending session.",
			(HandshakeDimension)msg.Dimension, msg.HostValue, msg.GuestValue, msg.Detail);

		_notices.Publish(HandshakeRefusalText.Render(_localization, msg));
		session.EndSession();
	}
}
