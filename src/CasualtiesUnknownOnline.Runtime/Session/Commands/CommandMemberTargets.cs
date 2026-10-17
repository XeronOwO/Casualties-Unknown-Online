using System;
using System.Globalization;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// Resolving an operator's member token — a decimal SteamId, a <c>0x</c> hex id or a
/// display name — against the session's presence table. One owner for every command
/// group that names a member (<c>/kick</c>, <c>/ban</c>, <c>/op</c>, <c>/deop</c>), so
/// the three accepted spellings and the display-name fallback exist once.
///
/// <para>
/// A display name is a CLAIM rather than an identity: the session admits by SteamId,
/// and the name is only a convenience for the operator. The returned id is always the
/// transport's id, which is what every command acts on.
/// </para>
/// </summary>
internal static class CommandMemberTargets
{
	/// <summary>The member id the token names, or false when it names none.</summary>
	internal static bool TryResolve(ISessionControl session, string text, out ulong steamId)
	{
		if (ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out steamId))
		{
			return true;
		}

		if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
			&& ulong.TryParse(text.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out steamId))
		{
			return true;
		}

		foreach (var member in session.Members)
		{
			if (!string.IsNullOrWhiteSpace(member.DisplayName)
				&& string.Equals(member.DisplayName.Trim(), text, StringComparison.OrdinalIgnoreCase))
			{
				steamId = member.SteamId;
				return true;
			}
		}

		steamId = 0;
		return false;
	}

	/// <summary>How an answer names one member: the id always, the display name beside it when the session carries one.</summary>
	internal static string Label(ISessionControl session, ulong steamId)
	{
		foreach (var member in session.Members)
		{
			if (member.SteamId == steamId && !string.IsNullOrWhiteSpace(member.DisplayName))
			{
				return $"{steamId.ToString(CultureInfo.InvariantCulture)} ({member.DisplayName})";
			}
		}

		return steamId.ToString(CultureInfo.InvariantCulture);
	}
}
