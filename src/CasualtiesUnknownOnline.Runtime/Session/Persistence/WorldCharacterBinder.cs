using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// Which character belongs to which member at a cut, and which member claims a
/// stored one at a restore. Identity is transport-scoped (decision 162): Steam
/// keys are <c>steam-&lt;steamId64&gt;</c>, IP-direct keys are
/// <c>name-&lt;sanitized display name&gt;</c>, and the two spaces never silently
/// collide.
///
/// It is separate from the save service because it is about identity, not about
/// the archive: the save service asks for the list a cut carries and hands the
/// list a restore produced back, and this type owns the resolution rules —
/// membership, not the "in world" flag, decides who is present, and a stored key
/// nobody claims is not an error (that player joins as a NEW character).
/// </summary>
internal sealed class WorldCharacterBinder(
	ISessionControl session,
	ICharacterDataControl characters,
	ITransportIdentity transport,
	ILogger<WorldCharacterBinder> log)
{
	/// <summary>
	/// The characters one cut carries: every peer present at the cut whose
	/// character snapshot exists. Membership — not the "in world" flag — is the
	/// predicate: a layer boundary is a LOADING moment, where every peer's scene
	/// state reads as "not in the world".
	/// </summary>
	internal List<SavedCharacter> Collect(CharacterDataMsg? hostCharacter)
	{
		var peers = PresentPeers();
		var space = LiveKeySpace();
		var localPeerId = LocalPeerId;
		var collected = new List<SavedCharacter>(peers.Count);
		foreach (var peer in peers)
		{
			var data = peer.PeerId == localPeerId
				? hostCharacter ?? characters.GetHostCharacterData()
				: characters.GetSavedCharacter(peer.PeerId);
			if (data is null)
			{
				// Decisions 162/166: the save holds every member PRESENT at the cut,
				// and a member with no snapshot simply adds no file.
				log.LogDebug("No character snapshot for peer {Peer} at this cut; it is not written (decisions 162/166: the save holds every member PRESENT at the cut).", peer.PeerId);
				continue;
			}

			collected.Add(new SavedCharacter(peer.KeyIn(space), data));
		}

		return collected;
	}

	/// <summary>
	/// Binds a restored snapshot's characters onto the peers that claim them (the
	/// host's own through the host slot, everyone else through the saved-character
	/// table the existing restore path reads). A key nobody claims is not an error
	/// — decision 162: that player joins as a NEW player.
	///
	/// Returns the character bound to the LOCAL peer, which is the one the caller
	/// has to apply to its own body: the store slot it was bound into is the same
	/// slot the live 1 Hz snapshot writes, so nothing downstream can tell "the
	/// archive's character, not yet on the body" from "the host's current state".
	/// Null = no stored key is ours (a new character) — or the whole set was
	/// refused, which is logged here.
	/// </summary>
	internal CharacterDataMsg? Apply(IReadOnlyList<SavedCharacter> stored)
	{
		if (stored.Count == 0)
		{
			return null;
		}

		var keys = stored.Select(character => character.PlayerKey).ToList();
		if (!PlayerKeyResolution.TrySpaceOfSet(keys, out var space))
		{
			log.LogError("The snapshot's character files mix transport key spaces ({Keys}); no character was applied.", string.Join(", ", keys));
			return null;
		}

		var live = LiveKeySpace();
		if (space == PlayerKeySpace.Unknown)
		{
			space = live;
		}
		else if (space != live)
		{
			// The two key spaces are separate (§2): a world written over IP-direct is
			// never claimed over Steam, even when a Steam persona happens to spell the
			// same name. Every key stays unclaimed — that player joins as a NEW
			// character (decision 162), and the files stay for a later claim.
			log.LogInformation("The snapshot's key space {Stored} differs from the live transport {Live}; no stored character is claimed in this session.", space, live);
			return null;
		}

		var peers = PresentPeers();
		var localPeerId = LocalPeerId;
		var applied = 0;
		var unclaimed = 0;
		CharacterDataMsg? local = null;
		foreach (var character in stored)
		{
			if (!PlayerKeyResolution.TryResolve(character.PlayerKey, space, peers, out var peerId))
			{
				unclaimed++;
				log.LogInformation("Character {PlayerKey} has no claimant in this session; decision 162: that player joins as a new character. The file stays in the archive for a later claim.", character.PlayerKey);
				continue;
			}

			if (peerId == localPeerId)
			{
				characters.SaveHostCharacterData(character.Character);
				local = character.Character;
			}
			else
			{
				characters.SaveCharacterData(peerId, character.Character);
			}

			applied++;
		}

		log.LogInformation("Restored characters: {Applied} bound to present peers, {Unclaimed} left unclaimed (key space {Space}).", applied, unclaimed, space);
		return local;
	}

	/// <summary>
	/// Every peer whose character a cut can carry: the local player plus every
	/// handshaken member. Whoever has no character snapshot is skipped by
	/// <see cref="Collect"/>, so a lobby-sitting member cannot add a file.
	/// </summary>
	private List<PlayerIdentity> PresentPeers()
	{
		var localPeerId = LocalPeerId;
		var peers = new List<PlayerIdentity> { new(localPeerId, transport.LocalDisplayName) };
		foreach (var member in session.Members)
		{
			if (!member.Handshaken || member.SteamId == localPeerId)
			{
				continue;
			}

			peers.Add(new PlayerIdentity(member.SteamId, member.DisplayName));
		}

		return peers;
	}

	/// <summary>The local peer's id — the session's when a session exists, the transport's otherwise (solo play has no session but still has an account).</summary>
	private ulong LocalPeerId => session.LocalSteamId != 0 ? session.LocalSteamId : transport.LocalPeerId;

	/// <summary>The key space the LIVE transport writes in (§2).</summary>
	private PlayerKeySpace LiveKeySpace() => transport.IsIpDirect ? PlayerKeySpace.IpDirect : PlayerKeySpace.Steam;
}
