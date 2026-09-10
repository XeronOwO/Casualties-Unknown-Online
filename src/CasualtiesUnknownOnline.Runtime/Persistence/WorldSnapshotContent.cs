using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// The manifest of an opened snapshot plus the files the format layer decoded
/// from it. Domain decoders take this object, not a directory path, so the live
/// and backup-fallback cases behave identically. <see cref="SourcePath"/> names
/// where it came from — the live folder, or the backup archive that was opened
/// instead — so a report can say which file a restore is actually reading.
/// </summary>
public sealed record WorldSnapshotContent(
	WorldLoadState State,
	SaveManifest Manifest,
	IReadOnlyList<SnapshotFile> Files,
	string? SourcePath = null);
