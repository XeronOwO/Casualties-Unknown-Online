using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// The live character table is keyed by the transport peer id, while the archive
/// is keyed by the stable player key (IP-direct: the display name). A reconnect
/// gets a NEW transport id, so the hand-back lookup misses and the guest becomes a
/// new player — losing its carried items, including their colours.
///
/// This rule finds the ONE old presence that is the same player by display name,
/// only while that old presence is no longer in the world. It never guesses:
/// two present players with the same name, or an old owner still playing, refuse.
/// </summary>
internal static class CharacterReconnectAdoption
{
	internal static ulong? FindSource(
		ulong newPeer,
		string? displayName,
		IReadOnlyList<ReconnectCandidate> members,
		IReadOnlyCollection<ulong> savedPeers,
		IReadOnlyDictionary<ulong, string>? savedNames = null)
	{
		if (string.IsNullOrWhiteSpace(displayName))
		{
			return null;
		}

		var name = displayName!.Trim();
		ulong? source = null;
		foreach (var member in members)
		{
			if (member.PeerId == newPeer || !member.Handshaken)
			{
				continue;
			}

			if (!string.Equals(member.DisplayName?.Trim(), name, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			// The old owner still in the world is a different player, not a reconnect.
			if (member.InWorld)
			{
				return null;
			}

			if (!savedPeers.Contains(member.PeerId))
			{
				continue;
			}

			if (source is not null)
			{
				return null; // two old same-name presences: do not guess
			}

			source = member.PeerId;
		}

		// The old presence may already be gone (the guest left the session before
		// reconnecting): the store remembers the name each report arrived under, so
		// the same one-match rule still applies over the saved table.
		return source ?? (savedNames is null ? null : FindSourceByName(displayName, savedNames, members, newPeer));
	}

	/// <summary>
	/// The fallback for a presence that already left the session: the one saved row whose
	/// recorded name matches, and whose old transport is gone or out of the world. The new
	/// peer's own row is not a second match — its first report arrives before this hand-back
	/// and would otherwise make the name look ambiguous.
	/// </summary>
	internal static ulong? FindSourceByName(
		string? displayName,
		IReadOnlyDictionary<ulong, string> savedNames,
		IReadOnlyList<ReconnectCandidate> members,
		ulong newPeer)
	{
		if (string.IsNullOrWhiteSpace(displayName))
		{
			return null;
		}

		var name = displayName!.Trim();
		var matches = savedNames
			.Where(pair => pair.Key != newPeer && string.Equals(pair.Value?.Trim(), name, StringComparison.OrdinalIgnoreCase))
			.Select(pair => pair.Key)
			.ToList();
		if (matches.Count != 1)
		{
			return null;
		}

		var candidate = matches[0];
		var oldPresence = members.FirstOrDefault(member => member.PeerId == candidate);
		return oldPresence is null || !oldPresence.InWorld ? candidate : null;
	}

	/// <summary>
	/// The old transport is the same player's previous connection: it cannot answer anything on
	/// the new link, and leaving it in the presence table makes the start gate wait for an InWorld
	/// report that can never arrive. Remove it through the normal kick edge, and record the move.
	/// </summary>
	internal static void RemoveStalePresence(ISessionControl session, ILogger log, ulong oldPeer, ulong newPeer)
	{
		if (session.Role == SessionRole.Host && session.KickMember(oldPeer, "reconnected under a new transport id"))
		{
			log.LogInformation(
				"[Character] adopted the saved character from the previous transport id {OldPeer} for {Peer}; removed the stale presence.",
				oldPeer,
				newPeer);
			return;
		}

		log.LogInformation(
			"[Character] adopted the saved character from the previous transport id {OldPeer} for {Peer}.",
			oldPeer,
			newPeer);
	}
}
