using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// What one decode pass produced: the checkpoint the kernel can be restored
/// from, the characters the restore path can hand back to peers, the world facts
/// the kernel does not own (the block diff and the transient world facts the
/// restore seam applies), and the reason the snapshot cannot be applied at all
/// (<see cref="Refusal"/>). A refusal is never a silent restart — §6 forbids
/// restoring a layer the snapshot does not name, so the caller must refuse the
/// continue instead.
///
/// A refused snapshot's world facts are unusable for the same reason its
/// characters are: nothing is applied from a refused cut.
/// </summary>
public sealed record WorldSnapshotDecode(
	GameCheckpoint? Checkpoint,
	IReadOnlyList<SavedCharacter> Characters,
	string? Refusal,
	IReadOnlyList<SaveWorldBlockRow>? WorldBlocks = null,
	IReadOnlyList<SaveWorldTransientRow>? WorldTransients = null,
	SaveNativeRunFields? NativeRunFields = null)
{
	public bool CanRestore => Checkpoint is not null;

	/// <summary>The characters of a refused snapshot are unusable too: nothing is applied from a refused cut.</summary>
	public IReadOnlyList<SavedCharacter> UsableCharacters => Checkpoint is null ? [] : Characters;

	/// <summary>The block diff of a refused snapshot: empty, because no cut that was refused may write world state.</summary>
	public IReadOnlyList<SaveWorldBlockRow> UsableWorldBlocks => Checkpoint is null ? [] : WorldBlocks ?? [];

	/// <summary>The transient world facts of a refused snapshot: empty, for the same reason.</summary>
	public IReadOnlyList<SaveWorldTransientRow> UsableWorldTransients => Checkpoint is null ? [] : WorldTransients ?? [];

	/// <summary>
	/// The native run fields of a refused snapshot: null, for the same reason. Null
	/// also means the snapshot CARRIES none (it predates the row), which the restore
	/// reports by name instead of letting the world continue with a clock that
	/// restarts at zero and every recipe re-locked.
	/// </summary>
	public SaveNativeRunFields? UsableNativeRunFields => Checkpoint is null ? null : NativeRunFields;

	public static WorldSnapshotDecode Refused(string reason) => new(null, [], reason);
}
