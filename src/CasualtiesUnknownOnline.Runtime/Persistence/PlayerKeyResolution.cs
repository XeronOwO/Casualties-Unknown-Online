using System;
using System.Collections.Generic;
using System.Globalization;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// Both halves of the transport-scoped key plumbing (§2, decision 162): what
/// key a peer writes under, and which peer — if any — claims a stored key at
/// load time.
///
/// The two key spaces are disjoint by construction. A Steam key carries the
/// account id and is claimed by that account; an IP-direct key carries the
/// sanitized display name and is claimed by whoever currently advertises that
/// name. A key nobody claims is not an error: decision 162 says that player
/// joins as a NEW player with a fresh character and starting supplies.
/// </summary>
public static class PlayerKeyResolution
{
	/// <summary>The key <paramref name="peerId"/> writes under in <paramref name="space"/>.</summary>
	public static string KeyOf(ulong peerId, string? displayName, PlayerKeySpace space) => space switch
	{
		PlayerKeySpace.IpDirect => PlayerKey.ForDisplayName(displayName),
		// Steam is the default: an empty/unknown space still has to produce one
		// stable key, and the account id is the identity the game itself uses.
		_ => PlayerKey.ForSteam(peerId),
	};

	/// <summary>The key space a single stored key belongs to. A key that matches neither grammar is not one of ours.</summary>
	public static bool TrySpaceOf(string? playerKey, out PlayerKeySpace space)
	{
		space = PlayerKeySpace.Unknown;
		if (string.IsNullOrEmpty(playerKey))
		{
			return false;
		}

		if (playerKey!.StartsWith(PlayerKey.SteamPrefix, StringComparison.Ordinal))
		{
			space = PlayerKeySpace.Steam;
			return true;
		}

		if (playerKey.StartsWith(PlayerKey.NamePrefix, StringComparison.Ordinal))
		{
			space = PlayerKeySpace.IpDirect;
			return true;
		}

		return false;
	}

	/// <summary>
	/// The key space a whole snapshot's character set belongs to. An empty set
	/// is <see cref="PlayerKeySpace.Unknown"/> (nothing to derive it from); a set
	/// that mixes the two spaces is refused, because a snapshot is written by one
	/// transport and a mixed set means the files did not come from one cut.
	/// </summary>
	public static bool TrySpaceOfSet(IEnumerable<string> playerKeys, out PlayerKeySpace space)
	{
		space = PlayerKeySpace.Unknown;
		foreach (var key in playerKeys)
		{
			if (!TrySpaceOf(key, out var keySpace))
			{
				space = PlayerKeySpace.Unknown;
				return false;
			}

			if (space == PlayerKeySpace.Unknown)
			{
				space = keySpace;
				continue;
			}

			if (space != keySpace)
			{
				space = PlayerKeySpace.Unknown;
				return false;
			}
		}

		return true;
	}

	/// <summary>The Steam account id of a <c>steam-</c> key, or null when the key is not a well-formed Steam key.</summary>
	public static ulong? SteamIdOf(string? playerKey) =>
		TrySpaceOf(playerKey, out var space) && space == PlayerKeySpace.Steam
			&& ulong.TryParse(playerKey!.Substring(PlayerKey.SteamPrefix.Length), NumberStyles.None,
				CultureInfo.InvariantCulture, out var steamId)
				? steamId
				: null;

	/// <summary>
	/// Which of <paramref name="peers"/> claims <paramref name="playerKey"/>.
	/// Steam mode claims by account id; IP-direct mode claims by recomputing
	/// every peer's name key, so a name that only differs in case, spacing or
	/// punctuation still resolves (the game's own name handling). False = nobody
	/// present claims it (decision 162: that player is absent from the session).
	/// </summary>
	public static bool TryResolve(string? playerKey, PlayerKeySpace space, IReadOnlyList<PlayerIdentity> peers, out ulong peerId)
	{
		peerId = 0;
		if (string.IsNullOrEmpty(playerKey))
		{
			return false;
		}

		if (space == PlayerKeySpace.Steam)
		{
			var steamId = SteamIdOf(playerKey);
			if (steamId is null)
			{
				return false;
			}

			foreach (var peer in peers)
			{
				if (peer.PeerId == steamId.Value)
				{
					peerId = peer.PeerId;
					return true;
				}
			}

			return false;
		}

		foreach (var peer in peers)
		{
			if (string.Equals(KeyOf(peer.PeerId, peer.DisplayName, PlayerKeySpace.IpDirect), playerKey, StringComparison.Ordinal))
			{
				peerId = peer.PeerId;
				return true;
			}
		}

		return false;
	}
}
