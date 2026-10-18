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
///
/// <para>
/// The NAMED player is part of it too: the host sends each row to the player whose body it
/// describes, so a row naming anyone else is a misroute/forgery and is dropped before the refusal
/// edge. That keeps the handler's edge state (a singleton) scoped to this client's own field, and
/// stops a foreign row from being answered as if it were this player's.
/// </para>
/// </summary>
[PacketHandler(NetMsg.PlayerVitals, NetMessageDirection.HostToGuest)]
public sealed class PlayerVitalsHandler(ILogger<PlayerVitalsHandler> log) : PacketHandlerBase<PlayerVitalsMsg, ICharacterSessionHandlerContext>
{
	private readonly ILogger<PlayerVitalsHandler> _log = log;
	private string _lastRefusedField = "";

	protected override void Handle(ulong sender, PlayerVitalsMsg msg, ICharacterSessionHandlerContext ctx)
	{
		if (sender != ctx.Session.HostSteamId)
		{
			_log.LogWarning("[Vitals] a row for {Player} came from {Sender}, which is not this session's host — dropped.",
				msg.SteamId, sender);
			return;
		}

		if (msg.SteamId != ctx.Session.LocalSteamId)
		{
			// A row belongs to the player whose body it describes; the host sends each row to that
			// player's own client. A row naming anyone else is a misroute/forgery and must not feed
			// the refusal edge (the singleton handler's last-field state is this client's).
			_log.LogWarning("[Vitals] a row naming {Player} arrived on {Local}'s client — dropped.", msg.SteamId, ctx.Session.LocalSteamId);
			return;
		}

		AnswerRefusal(msg);

		ctx.CharacterData.FirePlayerVitalsReceived(msg);
		_log.LogDebug("[Vitals] row for {Player}: blood {Blood:F1}, bleeding {Bleed:F2}, {Limbs} limbs.",
			msg.SteamId, msg.BloodVolume, msg.TotalBleedSpeed, msg.Limbs.Count);
	}

	/// <summary>
	/// The row's answer half: the host names the field it refuses the owner's report for. Logged on
	/// the EDGE, not at 10 Hz — a persistent refusal must not become a warning flood, and the
	/// owner still needs to see it resolved. Nothing is written to the local body here; the row's
	/// values are applied by the adapter as the host's authority.
	/// </summary>
	private void AnswerRefusal(PlayerVitalsMsg msg)
	{
		if (msg.RefusedField == _lastRefusedField)
		{
			return;
		}

		if (msg.RefusedField.Length == 0)
		{
			_log.LogInformation("[Vitals] the host accepts {Player}'s character report again.", msg.SteamId);
		}
		else
		{
			_log.LogWarning("[Vitals] the host refused {Player}'s character report for a non-finite {Field} — its values are not being used until the report is finite.",
				msg.SteamId, msg.RefusedField);
		}

		_lastRefusedField = msg.RefusedField;
	}
}
