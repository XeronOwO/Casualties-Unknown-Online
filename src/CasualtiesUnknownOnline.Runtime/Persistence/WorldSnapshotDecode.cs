using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// What one decode pass produced: the checkpoint the kernel can be restored
/// from, the characters the restore path can hand back to peers, and the reason
/// the snapshot cannot be applied at all (<see cref="Refusal"/>). A refusal is
/// never a silent restart — §6 forbids restoring a layer the snapshot does not
/// name, so the caller must refuse the continue instead.
/// </summary>
public sealed record WorldSnapshotDecode(GameCheckpoint? Checkpoint, IReadOnlyList<SavedCharacter> Characters, string? Refusal)
{
	public bool CanRestore => Checkpoint is not null;

	/// <summary>The characters of a refused snapshot are unusable too: nothing is applied from a refused cut.</summary>
	public IReadOnlyList<SavedCharacter> UsableCharacters => Checkpoint is null ? [] : Characters;

	public static WorldSnapshotDecode Refused(string reason) => new(null, [], reason);
}
