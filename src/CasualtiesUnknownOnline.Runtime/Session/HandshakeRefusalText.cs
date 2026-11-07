using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The guest's half of a handshake refusal: turn the host's dimension and two
/// values into a line from THIS client's catalogue. The host sends facts, not
/// text — the same refusal reads in each player's own language (the user's
/// 2026-11-07 ruling for the command console's answers, applied to the one
/// answer a member receives before its session even exists).
///
/// <para>
/// A dimension this build does not know (or the wire's <see cref="HandshakeDimension.Unknown"/>,
/// which is never legal) still reads as a line: a refusal must never be silent,
/// because the alternative is a join that does nothing and cannot be explained.
/// </para>
/// </summary>
public static class HandshakeRefusalText
{
	public static string Render(ILocalizationService text, HandshakeRefusedMsg msg) =>
		(HandshakeDimension)msg.Dimension switch
		{
			HandshakeDimension.Protocol => text.Format("handshake.refused.protocol", msg.HostValue, msg.GuestValue),
			HandshakeDimension.GameBuild => text.Format("handshake.refused.game_build", msg.HostValue, msg.GuestValue),
			HandshakeDimension.CuoBuild => text.Format("handshake.refused.cuo_build", msg.HostValue, msg.GuestValue),
			HandshakeDimension.SaveSchema => text.Format("handshake.refused.save_schema", msg.HostValue, msg.GuestValue),
			HandshakeDimension.ModSet => text.Format("handshake.refused.mod_set", msg.Detail, msg.HostValue, msg.GuestValue),
			HandshakeDimension.ModVersion => text.Format("handshake.refused.mod_version", msg.Detail, msg.HostValue, msg.GuestValue),
			_ => text.Format("handshake.refused.unknown", msg.Detail),
		};
}
