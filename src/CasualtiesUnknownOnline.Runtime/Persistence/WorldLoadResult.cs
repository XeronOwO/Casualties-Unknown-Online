namespace CasualtiesUnknownOnline.Runtime.Persistence;

using System.IO;

/// <summary>
/// What one load found: the state it settled on, the manifest that passed the
/// gate, the snapshot content, and every reason something was lost. The caller
/// must surface <see cref="DamageReport"/> (in-game per §6) — the report is not
/// advisory, it is the only account of what did not load.
/// </summary>
public sealed record WorldLoadResult(
	WorldLoadState State,
	string WorldId,
	WorldSnapshotContent? Content,
	DamageReport Report,
	string Summary)
{
	/// <summary>True = a snapshot was opened; content may still be damaged or salvaged.</summary>
	public bool Loaded => Content is not null;

	/// <summary>True = the live snapshot could not be opened at all.</summary>
	public bool Failed => Content is null;

	/// <summary>
	/// WHERE this load actually read from, in the words a player reads: the live folder, or
	/// the backup archive the load fell back to. It belongs on the result because it is a
	/// fact about the load, and it is player-facing because §6 requires the fallback to be
	/// surfaced in-game. The source PATH is deliberately not part of it — the log keeps that
	/// (<see cref="WorldSnapshotContent.SourcePath"/>); a player needs to know WHICH snapshot
	/// they got, not where their machine keeps it.
	/// </summary>
	public string SourceName =>
		State == WorldLoadState.BackupFallback && Content?.SourcePath is { Length: > 0 } path
			? $"backup {Path.GetFileName(path)}"
			: "the live snapshot";
}
