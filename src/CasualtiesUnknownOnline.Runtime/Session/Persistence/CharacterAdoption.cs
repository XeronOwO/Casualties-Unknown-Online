using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Persistence;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// The transport-handover rule (2026-11-11): a stored character whose key nobody claims is
/// adopted by a present peer when EXACTLY one peer matches its display name and that peer has
/// no character yet. Two matching characters, two matching peers or a nameless file adopt
/// nothing — decision 162's new-character default stays, and a guess is never made.
/// </summary>
internal static class CharacterAdoption
{
	/// <summary>The adopted pairs, plus the peers whose name matched more than one stored character (named, never guessed).</summary>
	internal static (List<(SavedCharacter Character, ulong PeerId)> Adopted, List<ulong> Ambiguous) Resolve(
		IReadOnlyList<SavedCharacter> unclaimed,
		IReadOnlyList<PlayerIdentity> peers,
		ICollection<ulong> peersWithCharacter)
	{
		var byPeer = new Dictionary<ulong, List<SavedCharacter>>();
		foreach (var character in unclaimed)
		{
			// The archive stores the display name in the row when the cut could read it; an
			// archive written before that field existed still names its owner in the key
			// itself (`name-<normalized display name>`), so the handover works for both.
			var name = !string.IsNullOrWhiteSpace(character.DisplayName)
				? PlayerKey.Normalize(character.DisplayName)
				: NameFromKey(character.PlayerKey);
			if (name is null)
			{
				continue;
			}
			var matches = peers
				.Where(peer => !peersWithCharacter.Contains(peer.PeerId)
					&& string.Equals(PlayerKey.Normalize(peer.DisplayName), name, StringComparison.Ordinal))
				.ToList();
			if (matches.Count != 1)
			{
				continue;
			}

			if (!byPeer.TryGetValue(matches[0].PeerId, out var candidates))
			{
				candidates = [];
				byPeer[matches[0].PeerId] = candidates;
			}

			candidates.Add(character);
		}

		var adopted = new List<(SavedCharacter Character, ulong PeerId)>(byPeer.Count);
		var ambiguous = new List<ulong>();
		foreach (var entry in byPeer)
		{
			if (entry.Value.Count == 1)
			{
				adopted.Add((entry.Value[0], entry.Key));
			}
			else
			{
				ambiguous.Add(entry.Key);
			}
		}

		return (adopted, ambiguous);
	}

	/// <summary>The normalized display name an IP-direct key carries, or null for any other key.</summary>
	private static string? NameFromKey(string playerKey)
	{
		const string prefix = PlayerKey.NamePrefix;
		return playerKey.StartsWith(prefix, StringComparison.Ordinal) && playerKey.Length > prefix.Length
			? playerKey.Substring(prefix.Length)
			: null;
	}
}
