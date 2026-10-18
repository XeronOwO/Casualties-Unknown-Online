using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// The host's continuous-vitals row (2026-10-18 ruling): the host computes a player's bleeding
/// values and the player's own client applies them to its own body. The row is data handed to
/// the adapter — it is never a body write by the host, and the receiver's own events still
/// decide everything terminal.
///
/// <para>
/// The sender is part of the verdict: the wire direction check proves only this client's ROLE,
/// so a frame whose sender is not this session's host is a CLAIM and is dropped by name — the
/// rule every host-addressed instruction follows. Without it any peer that can deliver a frame
/// to a guest could state that guest's blood volume and bleed.
/// </para>
/// </summary>
[PacketHandler(NetMsg.PlayerVitals, NetMessageDirection.HostToGuest)]
public sealed class PlayerVitalsHandler(ILogger<PlayerVitalsHandler> log) : PacketHandlerBase<PlayerVitalsMsg, ICharacterSessionHandlerContext>
{
	private readonly ILogger<PlayerVitalsHandler> _log = log;

	protected override void Handle(ulong sender, PlayerVitalsMsg msg, ICharacterSessionHandlerContext ctx)
	{
		if (sender != ctx.Session.HostSteamId)
		{
			_log.LogWarning("[Vitals] a row for {Player} came from {Sender}, which is not this session's host — dropped.",
				msg.SteamId, sender);
			return;
		}

		ctx.CharacterData.FirePlayerVitalsReceived(msg);
		_log.LogDebug("[Vitals] row for {Player}: blood {Blood:F1}, bleeding {Bleed:F2}, {Limbs} limbs.",
			msg.SteamId, msg.BloodVolume, msg.TotalBleedSpeed, msg.Limbs.Count);
	}
}
