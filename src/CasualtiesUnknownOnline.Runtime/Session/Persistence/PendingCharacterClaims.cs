using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// The characters a continuation could not bind, and the claim that answers a peer arriving
/// after the cut (2026-11-24). The restore binds once, against the peers present at the cut,
/// so a member that was out of the session then owns nothing and its bag comes back empty.
/// The archive it wrote is still the truth about it: when the character table asks for a peer
/// it has no snapshot for, the identity rule is applied again — now against the peers present
/// — and the file is handed over, once.
/// </summary>
internal sealed class PendingCharacterClaims(
	WorldRestoreApplier restore,
	ICharacterDataControl characters,
	ILogger log)
{
	private IReadOnlyList<SavedCharacter> _pending = [];

	/// <summary>The characters the last continuation could not bind.</summary>
	internal IReadOnlyList<SavedCharacter> Pending => _pending;

	/// <summary>A new continuation (or the end of one) replaces the set.</summary>
	internal void Keep(IReadOnlyList<SavedCharacter> pending) => _pending = pending;

	/// <summary>The session ended: nothing is owed to anyone any more.</summary>
	internal void Reset() => _pending = [];

	/// <summary>
	/// A stored character no cut-bound peer claimed, claimed by the peer that has just asked.
	/// The claim is removed so a second arrival cannot take the same file twice.
	/// </summary>
	internal SavedCharacter? ClaimFor(ulong peerId)
	{
		if (_pending.Count == 0)
		{
			return null;
		}

		var claimed = restore.ClaimPendingFor(_pending, peerId, []);
		if (claimed is null)
		{
			return null;
		}

		_pending = [.. _pending.Where(row => !ReferenceEquals(row, claimed))];
		return claimed;
	}

	/// <summary>The character table has no snapshot for this peer: hand it what the archive still carries.</summary>
	internal void OnMissing(ulong peerId)
	{
		var claimed = ClaimFor(peerId);
		if (claimed is null)
		{
			return;
		}

		characters.SeedSavedCharacter(peerId, claimed.Character, claimed.DisplayName);
		log.LogInformation(
			"Claimed the stored character written under {PlayerKey} for {Peer} — the cut could not bind it because that peer was not in the session then.",
			claimed.PlayerKey,
			peerId);
	}
}
