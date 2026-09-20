namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// Why a backup is being promoted over a world's live snapshot (§6). Both callers
/// replace the same folder and both write a pre-restore archive first; what they
/// owe the snapshot they replace is what differs, and it is a difference the player
/// pays for on disk if it is not named.
/// </summary>
internal enum WorldPromotionTrigger
{
	/// <summary>
	/// The live snapshot was REFUSED — a manifest that does not read, or a snapshot
	/// the decode refused — and a backup is promoted as the recovery. The refused
	/// snapshot is preserved as <c>damaged-&lt;stamp&gt;/</c> even when the pre-restore
	/// archive was written: it is the evidence of what CUO could not open, nothing
	/// else in the layout deletes it, and the next cut's transaction would otherwise
	/// overwrite the only copy of it.
	/// </summary>
	RefusedSnapshot,

	/// <summary>
	/// The PLAYER chose an archive in the world library (decision 198). The snapshot
	/// being replaced is a healthy one the player is deliberately stepping back from,
	/// so the pre-restore archive written into <c>backups/</c> IS its copy and the
	/// replacement is an ordinary live swap through <c>.previous/</c>, deleted once the
	/// new snapshot is in place: a <c>damaged-</c> folder per restore would accumulate
	/// one full snapshot each time, under a name that means "refused" and that no
	/// retention pass may ever delete. When the pre-restore archive could NOT be
	/// written, the folder is preserved after all — the copy is then the only one.
	/// </summary>
	PlayerChoice,
}
