using System;
using System.Collections.Generic;
using System.Linq;

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
		IReadOnlyCollection<ulong> savedPeers)
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

		return source;
	}
}
